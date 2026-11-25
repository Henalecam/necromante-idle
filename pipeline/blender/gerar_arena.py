"""
Renderiza o chão da arena (cripta de lajes rachadas, com entulho) na mesma câmera isométrica dos sprites.
Uso: blender -b --python blender/gerar_arena.py -- saida/arena 20 12 56
     (largura e profundidade em unidades da simulação; pixels por unidade na horizontal)
"""
import bpy, math, os, sys, random
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from fabrica import base as B
from mathutils import Vector


def main():
    a = sys.argv
    a = a[a.index("--") + 1:] if "--" in a else a[1:]
    saida = os.path.abspath(a[0] if a else "saida/arena")
    LX, LY, PPU = float(a[1]) if len(a) > 1 else 20.0, float(a[2]) if len(a) > 2 else 12.0, int(a[3]) if len(a) > 3 else 56
    margem = 2.0
    os.makedirs(saida, exist_ok=True)
    cena = B.cena_limpa()
    random.seed(7)

    # chão: lajes (tijolo largo) com rachaduras, musgo escuro e manchas antigas
    n = B.Nos("Lajes")
    tij = n.no("ShaderNodeTexBrick")
    tij.inputs["Scale"].default_value = 1.0
    tij.inputs["Mortar Size"].default_value = 0.035
    tij.inputs["Brick Width"].default_value = 1.6
    tij.inputs["Row Height"].default_value = 1.1
    tij.offset = 0.5
    n.ligar(n.coord(), tij.inputs["Vector"])
    pedra = n.rampa(n.ruido(1.3, 10, 0.7, 0.6), [(0.25, (0.012, 0.011, 0.014)), (0.55, (0.04, 0.036, 0.036)), (0.8, (0.075, 0.066, 0.058))])
    cor = n.misturar(tij.outputs["Fac"], pedra, (0.008, 0.006, 0.006))
    rach = n.rampa(n.voronoi_bordas(1.7), [(0.0, 0.15), (0.02, 1.0)])
    cor = n.misturar(1.0, cor, rach, "MULTIPLY")
    mancha = n.rampa(n.ruido(0.35, 4, 0.5), [(0.55, 0.0), (0.7, 1.0)])
    cor = n.misturar(n.matematica("MULTIPLY", mancha, valor_b=0.75), cor, (0.035, 0.004, 0.004))
    n.p("Base Color", cor)
    n.p("Roughness", 0.85)
    alt = n.matematica("SUBTRACT", n.ruido(6.0, 8), tij.outputs["Fac"])
    n.bump(alt, 0.9, 0.05)

    import bmesh
    malha = bpy.data.meshes.new("Chao")
    bm = bmesh.new()
    hx, hy = LX / 2 + margem, LY / 2 + margem
    bm.faces.new([bm.verts.new(v) for v in ((-hx, -hy, 0), (hx, -hy, 0), (hx, hy, 0), (-hx, hy, 0))])
    bm.to_mesh(malha); bm.free()
    malha.materials.append(n.mat)
    cena.collection.objects.link(bpy.data.objects.new("Chao", malha))

    # parede do fundo: pedra bruta, pilares, nichos com crânios e braseiros acesos
    ALT = 3.4
    w = B.Nos("Parede")
    wp = w.rampa(w.ruido(1.6, 10, 0.7, 0.8), [(0.3, (0.008, 0.007, 0.010)), (0.6, (0.032, 0.028, 0.03)), (0.85, (0.06, 0.05, 0.045))])
    wr = w.rampa(w.voronoi_bordas(0.9), [(0.0, 0.05), (0.03, 1.0)])
    wp = w.misturar(1.0, wp, wr, "MULTIPLY")
    esc = w.rampa(w.ruido(0.5, 3, 0.5), [(0.55, 0.0), (0.72, 1.0)])
    wp = w.misturar(w.matematica("MULTIPLY", esc, valor_b=0.6), wp, (0.03, 0.003, 0.003))
    w.p("Base Color", wp); w.p("Roughness", 0.9)
    w.bump(w.matematica("ADD", w.ruido(5.0, 8), wr), 1.0, 0.08)
    B.caixa("Parede", (0, hy + 0.3, ALT / 2), (hx * 2, 0.6, ALT), w.mat, "")
    fogo = B.mat_emissao("Fogo", (1.0, 0.42, 0.08), 40.0)
    ferro = B.mat_metal("FerroArena", qtd=0.5)
    osso_m = B.mat_osso()
    preto = B.mat_liso("Breu", (0.002, 0.002, 0.002), 1.0)
    npil = int(LX // 4) + 1
    for i in range(npil):
        x = -LX / 2 + i * (LX / (npil - 1))
        B.tubo("Pilar", [(x, hy - 0.05, 0), (x, hy - 0.05, 0.3), (x, hy - 0.05, ALT - 0.4), (x, hy - 0.05, ALT)],
               [0.42, 0.30, 0.30, 0.44], w.mat, "", subdiv=2, amplitude=0.03, escala_ruido=4, semente=i)
        if i < npil - 1:
            xm = x + LX / (npil - 1) / 2
            B.caixa("Nicho", (xm, hy + 0.02, 1.9), (1.3, 0.2, 1.5), preto, "", chanfro=0.0)
            for k in range(3):
                B.esfera("CranioNicho", (xm - 0.4 + k * 0.4, hy - 0.06, 1.32 + (k % 2) * 0.02), (0.13, 0.14, 0.14), osso_m, "", segs=10, aneis=8, amplitude=0.01, semente=k + i)
                for sx in (-1, 1):
                    B.esfera("Orb", (xm - 0.4 + k * 0.4 + sx * 0.05, hy - 0.185, 1.34), (0.03, 0.02, 0.035), preto, "", segs=6, aneis=4)
            # correntes penduradas
            B.tubo("CorrenteParede", [(xm + 0.5, hy - 0.1, ALT - 0.2), (xm + 0.52, hy - 0.12, 2.2 - (i % 2) * 0.5)], [0.02, 0.02], ferro, "", subdiv=1, amplitude=0.012, escala_ruido=30, semente=i)
    tochas = []
    for x in (-LX * 0.30, LX * 0.30):
        B.tubo("BraseiroPe", [(x, hy - 0.9, 0), (x, hy - 0.9, 0.9)], [0.10, 0.05], ferro, "", subdiv=1)
        B.esfera("BraseiroTaca", (x, hy - 0.9, 1.0), (0.32, 0.32, 0.16), ferro, "", segs=12, aneis=6)
        B.esfera("BraseiroFogo", (x, hy - 0.9, 1.18), (0.22, 0.22, 0.26), fogo, "", segs=10, aneis=8, amplitude=0.08, escala_ruido=8, semente=3)
        tochas.append((x, hy - 1.2, 1.9))

    osso = B.mat_osso()
    pedra_m = B.mat_liso("Entulho", (0.07, 0.065, 0.06), 0.9)
    for i in range(46):
        x, y = random.uniform(-LX / 2 - 1, LX / 2 + 1), random.uniform(-LY / 2 - 1, LY / 2 + 1)
        if random.random() < 0.55:
            r = random.uniform(0.05, 0.16)
            B.esfera("Pedra", (x, y, r * 0.4), (r, r * random.uniform(0.6, 1.2), r * 0.6), pedra_m, "", segs=6, aneis=4, amplitude=r * 0.3, escala_ruido=9, semente=i)
        elif random.random() < 0.7:
            ang = random.uniform(0, 6.28)
            d = Vector((math.cos(ang), math.sin(ang), 0)) * random.uniform(0.15, 0.3)
            B.tubo("OssoSolto", [Vector((x, y, 0.03)) - d, Vector((x, y, 0.03)), Vector((x, y, 0.03)) + d], [0.035, 0.018, 0.03], osso, "", subdiv=1)
        else:
            B.esfera("CranioSolto", (x, y, 0.09), (0.10, 0.12, 0.10), osso, "", segs=10, aneis=8, amplitude=0.008, semente=i)

    # luz de tochas: quente no centro, cantos no escuro
    luzes = [(tx, ty, tz, 1500, (1.0, 0.5, 0.18)) for (tx, ty, tz) in tochas]
    luzes += [(-LX * 0.2, -1.0, 4.0, 600, (1.0, 0.6, 0.3)), (LX * 0.25, -2.0, 4.0, 500, (0.55, 0.65, 1.0)), (0, 1.5, 4.5, 380, (0.9, 0.7, 0.5))]
    for (x, y, z, f, cor_l) in luzes:
        d = bpy.data.lights.new("Tocha", "POINT")
        d.energy, d.color, d.shadow_soft_size = f, cor_l, 0.5
        o = bpy.data.objects.new("Tocha", d)
        o.location = (x, y, z)
        cena.collection.objects.link(o)
    mundo = bpy.data.worlds.new("Mundo")
    cena.world = mundo

    e = math.radians(30)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    larg_px = int((LX + margem * 2) * PPU)
    chao_px = int((LY + margem * 2) * PPU * math.sin(e))
    topo_px = int(ALT * PPU * math.cos(e)) + 8
    alt_px = chao_px + topo_px
    cam.data.ortho_scale = LX + margem * 2
    cam.data.sensor_fit = "HORIZONTAL"
    frente = Vector((0, math.cos(e), -math.sin(e)))
    cima_v = Vector((0, math.sin(e), math.cos(e)))
    cam.location = -frente * 60 + cima_v * (topo_px / 2.0 / PPU)
    cam.rotation_euler = (math.pi / 2 - e, 0, 0)
    cena.collection.objects.link(cam)
    cena.camera = cam
    r = cena.render
    r.engine = "CYCLES"
    cena.cycles.samples = 48
    cena.cycles.device = "CPU"
    r.resolution_x, r.resolution_y = larg_px, alt_px
    r.image_settings.file_format = "PNG"
    cena.view_settings.view_transform = "Standard"
    r.filepath = os.path.join(saida, "arena_chao.png")
    bpy.ops.render.render(write_still=True)
    import json
    json.dump({"arquivo": "arena_chao.png", "largura_sim": LX, "profundidade_sim": LY, "margem_sim": margem, "topo_px": topo_px,
               "px_por_unidade_x": PPU, "px_por_unidade_y": PPU * math.sin(e)},
              open(os.path.join(saida, "arena.json"), "w"), indent=2)
    print("[arena] pronto:", r.filepath, larg_px, alt_px, flush=True)


main()
