"""
Fábrica de monstros — utilitários de geometria, materiais e rig.
Tudo procedural: nenhuma dependência de modelo externo.
"""

import bpy
import bmesh
import math
import random
from mathutils import Vector, Matrix, Quaternion, noise

FRENTE = Vector((0.0, -1.0, 0.0))   # os personagens olham para -Y
BAIXO = Vector((0.0, 0.0, -1.0))
CIMA = Vector((0.0, 0.0, 1.0))


# ---------------------------------------------------------------------------
# Cena
# ---------------------------------------------------------------------------

def cena_limpa():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def vincular(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------------------
# Materiais (Cycles)
# ---------------------------------------------------------------------------

class Nos:
    """Ajudante para montar árvores de nós de material com pouco código."""

    def __init__(self, nome):
        self.mat = bpy.data.materials.new(nome)
        try:
            self.mat.use_nodes = True
        except Exception:
            pass
        self.nt = self.mat.node_tree
        self.nt.nodes.clear()
        self.saida = self.nt.nodes.new("ShaderNodeOutputMaterial")
        self.bsdf = self.nt.nodes.new("ShaderNodeBsdfPrincipled")
        self.ligar(self.bsdf.outputs["BSDF"], self.saida.inputs["Surface"])

    def no(self, tipo, **props):
        n = self.nt.nodes.new(tipo)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def ligar(self, a, b):
        self.nt.links.new(a, b)

    def coord(self):
        tc = self.no("ShaderNodeTexCoord")
        return tc.outputs["Object"]

    def ruido(self, escala, detalhe=6.0, rugosidade=0.6, distorcao=0.0):
        n = self.no("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = escala
        n.inputs["Detail"].default_value = detalhe
        n.inputs["Roughness"].default_value = rugosidade
        if "Distortion" in n.inputs:
            n.inputs["Distortion"].default_value = distorcao
        self.ligar(self.coord(), n.inputs["Vector"])
        return n.outputs["Fac"]

    def voronoi_bordas(self, escala):
        n = self.no("ShaderNodeTexVoronoi")
        n.feature = "DISTANCE_TO_EDGE"
        n.inputs["Scale"].default_value = escala
        self.ligar(self.coord(), n.inputs["Vector"])
        return n.outputs["Distance"]

    def oclusao(self, distancia=0.15):
        ao = self.no("ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = distancia
        ao.samples = 8
        return ao.outputs["AO"]

    def rampa(self, entrada, paradas):
        """paradas: lista de (posição, (r,g,b)) ou (posição, valor_float)."""
        r = self.no("ShaderNodeValToRGB")
        cr = r.color_ramp
        while len(cr.elements) > 1:
            cr.elements.remove(cr.elements[-1])
        for i, (pos, cor) in enumerate(paradas):
            el = cr.elements[0] if i == 0 else cr.elements.new(pos)
            el.position = pos
            if isinstance(cor, (int, float)):
                cor = (cor, cor, cor)
            el.color = (*cor, 1.0)
        self.ligar(entrada, r.inputs["Fac"])
        return r.outputs["Color"]

    def matematica(self, op, a, b=None, valor_b=None):
        m = self.no("ShaderNodeMath", operation=op)
        self._entrada(a, m.inputs[0])
        if b is not None:
            self._entrada(b, m.inputs[1])
        elif valor_b is not None:
            m.inputs[1].default_value = valor_b
        return m.outputs[0]

    def misturar(self, fator, a, b, modo="MIX"):
        m = self.no("ShaderNodeMix", data_type="RGBA", blend_type=modo)
        ent = [s for s in m.inputs if s.enabled]
        self._entrada(fator, ent[0])
        self._entrada(a, ent[1])
        self._entrada(b, ent[2])
        return [s for s in m.outputs if s.enabled][0]

    def _entrada(self, valor, socket):
        if hasattr(valor, "is_output"):
            self.ligar(valor, socket)
        elif isinstance(valor, (tuple, list)):
            socket.default_value = (*valor, 1.0) if len(valor) == 3 else valor
        else:
            socket.default_value = valor

    def bump(self, altura, forca=0.4, distancia=0.02):
        b = self.no("ShaderNodeBump")
        b.inputs["Strength"].default_value = forca
        b.inputs["Distance"].default_value = distancia
        self.ligar(altura, b.inputs["Height"])
        self.ligar(b.outputs["Normal"], self.bsdf.inputs["Normal"])

    def p(self, nome, valor):
        sock = self.bsdf.inputs[nome]
        self._entrada(valor, sock)


def mat_osso(nome="Osso", base=(0.78, 0.70, 0.55), sujeira=(0.18, 0.11, 0.06)):
    n = Nos(nome)
    rd = n.ruido(9.0, 8, 0.65)
    ao = n.oclusao(0.12)
    manchas = n.rampa(rd, [(0.35, sujeira), (0.65, base)])
    fator_sujo = n.matematica("SUBTRACT", 1.0, ao)
    fator_sujo = n.matematica("MULTIPLY", fator_sujo, valor_b=2.2)
    cor = n.misturar(n.matematica("MINIMUM", fator_sujo, valor_b=1.0), manchas, sujeira)
    rachas = n.voronoi_bordas(14.0)
    linhas = n.rampa(rachas, [(0.0, 0.35), (0.04, 1.0)])
    cor = n.misturar(1.0, cor, linhas, "MULTIPLY")
    n.p("Base Color", cor)
    n.p("Roughness", 0.75)
    n.bump(n.ruido(30.0, 4), 0.35)
    return n.mat


def mat_carne(nome="Carne", tons=((0.11, 0.145, 0.09), (0.20, 0.185, 0.12), (0.09, 0.05, 0.09)),
              veia=(0.05, 0.015, 0.04), sangue=(0.13, 0.004, 0.008), sangue_qtd=0.42, rasgos=0.5,
              carne_viva=(0.16, 0.012, 0.014)):
    """Pele morta: manchas de podridão, veias, sangue seco e rasgos fundos que mostram a carne viva e úmida."""
    n = Nos(nome)
    rd = n.ruido(4.0, 8, 0.7, 0.8)
    podre = tuple(c * 0.35 for c in tons[2])
    pele = n.rampa(rd, [(0.28, podre), (0.42, tons[2]), (0.55, tons[0]), (0.74, tons[1])])
    # manchas grandes de necrose
    necrose = n.rampa(n.ruido(1.3, 4, 0.55, 0.4), [(0.50, 0.0), (0.62, 1.0)])
    pele = n.misturar(n.matematica("MULTIPLY", necrose, valor_b=0.75), pele, (0.018, 0.012, 0.02))
    veias = n.rampa(n.voronoi_bordas(7.0), [(0.0, 1.0), (0.03, 0.0)])
    pele = n.misturar(n.matematica("MULTIPLY", veias, valor_b=0.7), pele, veia)
    sang = n.rampa(n.ruido(2.5, 6, 0.6), [(1.0 - sangue_qtd, 0.0), (1.0 - sangue_qtd + 0.06, 1.0)])
    cor = n.misturar(n.matematica("MULTIPLY", sang, valor_b=0.85), pele, sangue)
    # rasgos: ilhas de bordas duras, com crosta escura em volta e carne úmida dentro
    rr = n.ruido(2.6, 5, 0.6, 1.6)
    lim = 0.70 - 0.16 * rasgos
    dentro = n.rampa(rr, [(lim, 0.0), (lim + 0.012, 1.0)])
    crosta = n.rampa(rr, [(lim - 0.05, 0.0), (lim - 0.005, 1.0), (lim + 0.012, 0.0)])
    fibras = n.rampa(n.ruido(26.0, 3, 0.5), [(0.3, tuple(c * 0.45 for c in carne_viva)), (0.7, carne_viva)])
    cor = n.misturar(crosta, cor, (0.012, 0.003, 0.003))
    cor = n.misturar(dentro, cor, fibras)
    ao = n.oclusao(0.1)
    cor = n.misturar(n.matematica("SUBTRACT", 1.0, ao), cor, (0.012, 0.004, 0.004))
    n.p("Base Color", cor)
    umido = n.matematica("MAXIMUM", dentro, n.matematica("MULTIPLY", sang, valor_b=0.7))
    n.ligar(n.misturar(umido, (0.8, 0.8, 0.8), (0.16, 0.16, 0.16)), n.bsdf.inputs["Roughness"])
    if "Subsurface Weight" in n.bsdf.inputs:
        n.p("Subsurface Weight", 0.05)
    relevo = n.matematica("SUBTRACT", n.matematica("ADD", n.ruido(18.0, 6), n.matematica("MULTIPLY", n.ruido(55.0, 3), valor_b=0.5)),
                          n.matematica("MULTIPLY", dentro, valor_b=1.6))
    n.bump(relevo, 0.75, 0.03)
    return n.mat


def mat_demonio(nome="Demonio", base=(0.25, 0.03, 0.02), escuro=(0.03, 0.01, 0.01),
                brilho=(1.0, 0.35, 0.05), forca_brilho=6.0):
    n = Nos(nome)
    rd = n.ruido(5.0, 8, 0.65, 0.5)
    pele = n.rampa(rd, [(0.35, escuro), (0.65, base)])
    ao = n.oclusao(0.12)
    pele = n.misturar(n.matematica("SUBTRACT", 1.0, ao), pele, escuro)
    rachas = n.rampa(n.voronoi_bordas(5.0), [(0.0, 1.0), (0.025, 0.0)])
    mascara = n.rampa(n.ruido(3.0, 3), [(0.45, 0.0), (0.6, 1.0)])
    lava = n.matematica("MULTIPLY", rachas, mascara)
    n.p("Base Color", pele)
    n.p("Roughness", 0.55)
    n.p("Emission Color", brilho)
    n.ligar(n.matematica("MULTIPLY", lava, valor_b=forca_brilho), n.bsdf.inputs["Emission Strength"])
    n.bump(n.ruido(22.0, 6), 0.45)
    return n.mat


def mat_metal(nome="Ferro", ferro=(0.20, 0.20, 0.22), ferrugem=(0.13, 0.055, 0.025), qtd=0.38):
    n = Nos(nome)
    rd = n.ruido(6.0, 10, 0.7, 1.0)
    fator = n.rampa(rd, [(1.0 - qtd - 0.05, 0.0), (1.0 - qtd + 0.1, 1.0)])
    cor = n.misturar(fator, ferro, ferrugem)
    ao = n.oclusao(0.1)
    cor = n.misturar(n.matematica("SUBTRACT", 1.0, ao), cor, (0.01, 0.01, 0.01))
    n.p("Base Color", cor)
    n.ligar(n.misturar(fator, (0.9, 0.9, 0.9), (0.0, 0.0, 0.0)), n.bsdf.inputs["Metallic"])
    n.ligar(n.misturar(fator, (0.35, 0.35, 0.35), (0.9, 0.9, 0.9)), n.bsdf.inputs["Roughness"])
    n.bump(n.ruido(25.0, 6), 0.5)
    return n.mat


def mat_pano(nome="Pano", cor=(0.18, 0.03, 0.03), escuro=(0.04, 0.02, 0.02)):
    n = Nos(nome)
    rd = n.ruido(8.0, 6, 0.6, 2.0)
    c = n.rampa(rd, [(0.3, escuro), (0.7, cor)])
    ao = n.oclusao(0.15)
    c = n.misturar(n.matematica("SUBTRACT", 1.0, ao), c, (0.01, 0.0, 0.0))
    n.p("Base Color", c)
    n.p("Roughness", 0.95)
    n.bump(n.ruido(40.0, 4), 0.3)
    return n.mat


def mat_emissao(nome, cor, forca=10.0):
    n = Nos(nome)
    n.p("Base Color", cor)
    n.p("Emission Color", cor)
    n.p("Emission Strength", forca)
    return n.mat


def mat_liso(nome, cor, rugosidade=0.5, metalico=0.0):
    n = Nos(nome)
    n.p("Base Color", cor)
    n.p("Roughness", rugosidade)
    n.p("Metallic", metalico)
    return n.mat


def mat_sangue(nome="Sangue"):
    return mat_liso(nome, (0.04, 0.0, 0.003), rugosidade=0.5)


# ---------------------------------------------------------------------------
# Geometria
# ---------------------------------------------------------------------------

def _malha_de_modificadores(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    malha = bpy.data.meshes.new_from_object(ev)
    dados_velhos = obj.data
    obj.modifiers.clear()
    obj.data = malha
    if dados_velhos.users == 0:
        bpy.data.meshes.remove(dados_velhos)
    return obj


def deformar_ruido(obj, amplitude, escala=3.0, semente=0.0):
    """Empurra os vértices pela normal com ruído: deixa a forma orgânica e irregular."""
    m = obj.data
    m.update()
    off = Vector((semente * 13.1, semente * 7.7, semente * 3.3))
    for v in m.vertices:
        p = v.co * escala + off
        d = noise.noise(p) + 0.5 * noise.noise(p * 2.3)
        v.co += v.normal * d * amplitude
    m.update()


def tubo(nome, pontos, raios, mat, osso, subdiv=2, amplitude=0.0, escala_ruido=3.0, semente=0.0,
         achatar=1.0):
    """Tubo orgânico ao longo de pontos (skin modifier): membros, costelas, chifres, garras, caudas."""
    malha = bpy.data.meshes.new(nome)
    vs = [Vector(p) for p in pontos]
    malha.from_pydata([tuple(v) for v in vs], [(i, i + 1) for i in range(len(vs) - 1)], [])
    obj = vincular(bpy.data.objects.new(nome, malha))
    mod = obj.modifiers.new("Pele", "SKIN")
    mod.use_smooth_shade = True
    sk = malha.skin_vertices[0].data
    for i, r in enumerate(raios):
        rx, ry = (r, r * achatar) if isinstance(r, (int, float)) else r
        sk[i].radius = (max(rx, 0.002), max(ry, 0.002))
    sk[0].use_root = True
    if subdiv:
        s = obj.modifiers.new("Suave", "SUBSURF")
        s.levels = s.render_levels = subdiv
    _malha_de_modificadores(obj)
    if amplitude:
        deformar_ruido(obj, amplitude, escala_ruido, semente)
    finalizar(obj, mat, osso)
    return obj


def esfera(nome, centro, raios, mat, osso, segs=16, aneis=10, amplitude=0.0, escala_ruido=3.0,
           semente=0.0, rot=(0, 0, 0)):
    malha = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=aneis, radius=1.0)
    m = Matrix.Translation(centro) @ Euler(rot) @ Matrix.Diagonal((*raios, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bm.to_mesh(malha)
    bm.free()
    obj = vincular(bpy.data.objects.new(nome, malha))
    for f in malha.polygons:
        f.use_smooth = True
    if amplitude:
        deformar_ruido(obj, amplitude, escala_ruido, semente)
    finalizar(obj, mat, osso)
    return obj


def caixa(nome, centro, tamanho, mat, osso, rot=(0, 0, 0), chanfro=0.0, amplitude=0.0, semente=0.0):
    malha = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    if chanfro:
        bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=chanfro, segments=2, affect="EDGES")
    m = Matrix.Translation(centro) @ Euler(rot) @ Matrix.Diagonal((*tamanho, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bm.to_mesh(malha)
    bm.free()
    obj = vincular(bpy.data.objects.new(nome, malha))
    if amplitude:
        deformar_ruido(obj, amplitude, 6.0, semente)
    finalizar(obj, mat, osso)
    return obj


def cone(nome, base, ponta, raio, mat, osso, segs=8):
    """Espinhos, dentes, pontas."""
    base, ponta = Vector(base), Vector(ponta)
    d = ponta - base
    malha = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=raio, radius2=0.0005, depth=d.length)
    rot = d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    m = Matrix.Translation((base + ponta) / 2) @ rot
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bm.to_mesh(malha)
    bm.free()
    obj = vincular(bpy.data.objects.new(nome, malha))
    finalizar(obj, mat, osso)
    return obj


def membrana(nome, contorno, mat, osso, amplitude=0.0, semente=0.0):
    """Superfície fina por um contorno fechado (asas, trapos)."""
    malha = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(p)) for p in contorno]
    f = bm.faces.new(vs)
    bmesh.ops.triangulate(bm, faces=[f])
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.012)
    bm.to_mesh(malha)
    bm.free()
    obj = vincular(bpy.data.objects.new(nome, malha))
    if amplitude:
        deformar_ruido(obj, amplitude, 4.0, semente)
    finalizar(obj, mat, osso)
    return obj


def Euler(rot):
    from mathutils import Euler as E
    return E(rot).to_matrix().to_4x4()


def finalizar(obj, mat, osso):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for f in obj.data.polygons:
        f.use_smooth = True
    obj["osso"] = osso


def arco(centro, raio_x, raio_y, ang_ini, ang_fim, n, z=0.0, inclinacao=0.0):
    """Pontos em arco no plano XY (costelas), com queda em Z ao longo do arco."""
    pts = []
    for i in range(n):
        t = i / (n - 1)
        a = math.radians(ang_ini + (ang_fim - ang_ini) * t)
        pts.append((centro[0] + math.cos(a) * raio_x,
                    centro[1] + math.sin(a) * raio_y,
                    centro[2] + z - inclinacao * t))
    return pts


def curva(p0, p1, p2, n=6):
    """Curva quadrática entre p0 e p2 controlada por p1 (chifres, garras, caudas)."""
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    return [tuple((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t ** 2 * p2)
            for t in [i / (n - 1) for i in range(n)]]


def espelhar(pts):
    return [(-x, y, z) for (x, y, z) in pts]


# ---------------------------------------------------------------------------
# Rig humanoide
# ---------------------------------------------------------------------------

class Rig:
    def __init__(self, nome):
        self.dados = bpy.data.armatures.new(nome)
        self.obj = vincular(bpy.data.objects.new(nome, self.dados))
        self.ossos = {}

    def construir(self, definicao):
        """definicao: {nome: (cabeça, cauda, pai)}"""
        bpy.context.view_layer.objects.active = self.obj
        bpy.ops.object.mode_set(mode="EDIT")
        for nome, (cab, cau, pai) in definicao.items():
            b = self.dados.edit_bones.new(nome)
            b.head, b.tail = Vector(cab), Vector(cau)
            if pai:
                b.parent = self.dados.edit_bones[pai]
                b.use_connect = False
        bpy.ops.object.mode_set(mode="OBJECT")
        for pb in self.obj.pose.bones:
            pb.rotation_mode = "QUATERNION"

    def vincular_partes(self, partes, nome="Corpo"):
        """Junta as partes numa malha só, cada parte presa 100% ao seu osso."""
        for p in partes:
            g = p.vertex_groups.new(name=p["osso"])
            g.add(list(range(len(p.data.vertices))), 1.0, "REPLACE")
        bpy.ops.object.select_all(action="DESELECT")
        for p in partes:
            p.select_set(True)
        bpy.context.view_layer.objects.active = partes[0]
        bpy.ops.object.join()
        corpo = bpy.context.view_layer.objects.active
        corpo.name = nome
        corpo.parent = self.obj
        mod = corpo.modifiers.new("Armadura", "ARMATURE")
        mod.object = self.obj
        return corpo

    # --- poses em termos intuitivos ---------------------------------------
    def _eixos(self, nome):
        b = self.dados.bones[nome]
        r = b.matrix_local.to_3x3()
        d = (b.tail_local - b.head_local).normalized()
        return r, d

    def quat(self, nome, frente=0.0, fora=0.0, giro=0.0, lado=1.0):
        """frente: a ponta do osso vai para a frente (graus); fora: a ponta abre para o lado
        (lado=+1 esquerdo/+X, -1 direito); giro: em torno do próprio osso."""
        r, d = self._eixos(nome)
        rinv = r.inverted()
        q = Quaternion()

        def eixo(alvo, reserva):
            e = d.cross(alvo)
            if e.length < 0.3:
                e = d.cross(reserva)
            return e.normalized()

        if frente:
            q = q @ Quaternion(rinv @ eixo(FRENTE, BAIXO), math.radians(frente))
        if fora:
            q = q @ Quaternion(rinv @ eixo(Vector((lado, 0, 0)), CIMA), math.radians(fora))
        if giro:
            q = q @ Quaternion((rinv @ d).normalized(), math.radians(giro))
        return q

    def loc(self, nome, mundo):
        r, _ = self._eixos(nome)
        return r.inverted() @ Vector(mundo)


def lado_de(nome):
    return -1.0 if nome.endswith(".R") else 1.0


def animar(rig, nome, chaves, base=None):
    """chaves: {frame: {osso: (frente, fora, giro) | 'osso@loc': (x,y,z)}}; base: pose somada a todas."""
    acao = bpy.data.actions.new(nome)
    acao.use_fake_user = True
    rig.obj.animation_data_create()
    rig.obj.animation_data.action = acao
    base = base or {}
    for f in sorted(chaves):
        pose = chaves[f]
        for pb in rig.obj.pose.bones:
            n = pb.name
            b = base.get(n, (0, 0, 0))
            v = pose.get(n, (0, 0, 0))
            soma = tuple(b[i] + v[i] for i in range(3))
            pb.rotation_quaternion = rig.quat(n, *soma, lado=lado_de(n))
            lb = base.get(n + "@loc", (0, 0, 0))
            lv = pose.get(n + "@loc", (0, 0, 0))
            pb.location = rig.loc(n, tuple(lb[i] + lv[i] for i in range(3)))
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)
    return acao


def salvar(caminho, rig, acao_padrao):
    rig.obj.animation_data.action = bpy.data.actions[acao_padrao]
    bpy.ops.wm.save_as_mainfile(filepath=caminho)


def pregas(obj, n=9, forca=0.14, z_topo=1.0, semente=0.0):
    """Dobras verticais de tecido: ondula o raio em torno do eixo Z, mais forte perto do chão."""
    m = obj.data
    for v in m.vertices:
        r = math.hypot(v.co.x, v.co.y)
        if r < 1e-4:
            continue
        ang = math.atan2(v.co.y, v.co.x)
        peso = max(0.0, 1.0 - v.co.z / z_topo) ** 0.7
        onda = math.sin(ang * n + semente + 1.7 * noise.noise(Vector((v.co.x * 3, v.co.y * 3, v.co.z * 1.5))))
        k = 1.0 + forca * peso * onda
        v.co.x *= k
        v.co.y *= k
        # barra irregular: sobe/desce a borda de baixo
        if v.co.z < 0.16:
            v.co.z += 0.07 * peso * (0.5 + 0.5 * math.sin(ang * 5 + semente * 2)) - 0.03
    m.update()
