"""
Gera a arte dos equipamentos: um modelo 3D procedural por tipo de item, renderizado de frente como ícone
com fundo transparente. O tamanho do ícone segue o espaço que o item ocupa no inventário (64 px por espaço, x2).

Uso:
    python3 blender/gerar_itens.py            # todos
    python3 blender/gerar_itens.py foice elmo # só alguns
Saída: saida/itens/<tipo>.png e saida/itens/itens.json
"""

import json
import math
import os
import sys

import bpy
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
sys.path.insert(0, AQUI)

from fabrica import base as B          # noqa: E402
from fabrica import monstros as MO     # noqa: E402

PX_POR_ESPACO = 128
O = "item"   # os ajudantes pedem o nome de um osso; aqui não há esqueleto


def V(x, y, z):
    return Vector((x, y, z))


def materiais():
    return dict(
        osso=B.mat_osso(), madeira=B.mat_pano("MadeiraNegra", (0.06, 0.04, 0.03), (0.01, 0.008, 0.008)),
        aco=B.mat_metal("Aco", (0.22, 0.22, 0.25), (0.10, 0.03, 0.02), 0.4),
        bronze=B.mat_metal("Bronze", (0.38, 0.24, 0.07), (0.05, 0.13, 0.08), 0.35),
        couro=B.mat_pano("Couro", (0.13, 0.05, 0.035), (0.03, 0.012, 0.01)),
        pano=B.mat_pano("Manto", (0.07, 0.035, 0.09), (0.01, 0.006, 0.014)),
        sangue=B.mat_sangue(), preto=B.mat_liso("Vazio", (0.003, 0.002, 0.004), 1.0),
        alma=B.mat_emissao("Alma", (0.25, 1.0, 0.5), 9.0), brasa=B.mat_emissao("Brasa", (1.0, 0.25, 0.05), 8.0),
    )


# ---- modelos: cada função monta o item centrado na origem, de frente para -Y, em unidades de "espaço" (1 = um espaço) ----

def cajado(M, m):
    M.tubo("Haste", [V(0.02, 0, -1.35), V(-0.03, 0, -0.5), V(0.03, 0, 0.3), V(0, 0, 0.85)], [0.045, 0.06, 0.055, 0.06], m["madeira"], O, subdiv=1, amplitude=0.012, escala_ruido=6, semente=3)
    MO.cranio(M, V(0, 0, 0.80), 2.1, m["osso"], m["preto"], m["alma"], osso=O, dentes=True, mandibula=False, chifres=(m["osso"], 0.9))
    for k in range(4):
        a = math.radians(45 + 90 * k)
        b0 = V(math.cos(a) * 0.07, math.sin(a) * 0.07, 0.78)
        M.tubo("Gaiola", B.curva(b0, b0 + V(math.cos(a) * 0.26, math.sin(a) * 0.26, -0.22), b0 + V(math.cos(a) * 0.05, math.sin(a) * 0.05, -0.5), 5),
               [0.03, 0.028, 0.024, 0.018, 0.008], m["osso"], O, subdiv=1)
    M.esfera("AlmaPresa", V(0, 0, 0.52), (0.1, 0.1, 0.13), m["alma"], O, segs=10, aneis=8)
    M.tubo("Anel", [V(0.07 * math.cos(a), 0.07 * math.sin(a), -0.2) for a in [math.radians(x) for x in range(0, 361, 60)]], [0.02] * 7, m["bronze"], O, subdiv=1)


def foice(M, m):
    M.tubo("Haste", [V(0.55, 0, -1.35), V(0.5, 0, -0.4), V(0.56, 0, 0.5), V(0.5, 0, 1.2)], [0.045, 0.055, 0.05, 0.055], m["madeira"], O, subdiv=1, amplitude=0.012, escala_ruido=6, semente=5)
    topo = V(0.5, 0, 1.15)
    lam = [topo + V(0.12, 0, 0.02), topo + V(-0.35, 0, 0.2), topo + V(-0.85, 0, 0.1), topo + V(-1.2, 0, -0.25), topo + V(-1.38, 0, -0.75)]
    M.tubo("Lamina", lam, [(0.025, 0.14), (0.022, 0.24), (0.02, 0.22), (0.015, 0.15), (0.006, 0.02)], m["aco"], O, subdiv=2, amplitude=0.02, escala_ruido=14, semente=7)
    M.tubo("Fio", [topo + V(-0.75, -0.02, 0.0), topo + V(-1.12, -0.02, -0.3), topo + V(-1.33, -0.02, -0.7)], [(0.03, 0.08), (0.028, 0.07), (0.01, 0.02)], m["sangue"], O, subdiv=1, amplitude=0.02, escala_ruido=16, semente=8)
    MO.cranio(M, topo + V(0.12, 0, -0.2), 1.1, m["osso"], m["preto"], m["alma"], osso=O, dentes=True, mandibula=False)
    M.cone("Ponta", V(0.55, 0, -1.3), V(0.56, 0, -1.48), 0.05, m["aco"], O, segs=6)


def grimorio(M, m):
    M.caixa("Capa", V(0, 0, 0), (1.3, 0.34, 1.6), m["couro"], O, chanfro=0.05, amplitude=0.012, semente=2)
    M.caixa("Folhas", V(0.05, 0, 0), (1.22, 0.26, 1.5), m["osso"], O, chanfro=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            M.cone("Canto", V(sx * 0.6, -0.18, sz * 0.74), V(sx * 0.38, -0.2, sz * 0.5), 0.13, m["bronze"], O, segs=4)
    MO.cranio(M, V(0, -0.2, -0.22), 1.25, m["osso"], m["preto"], m["alma"], osso=O, dentes=True, mandibula=False)
    M.tubo("Fecho", [V(0.66, -0.1, 0.0), V(0.72, 0, 0.0), V(0.66, 0.1, 0.0)], [(0.05, 0.12)] * 3, m["bronze"], O, subdiv=1)
    M.tubo("Lombada", [V(-0.66, 0, -0.78), V(-0.66, 0, 0.78)], [0.17, 0.17], m["couro"], O, subdiv=1)


def relicario(M, m):
    M.tubo("Urna", [V(0, 0, -0.8), V(0, 0, -0.55), V(0, 0, 0.0), V(0, 0, 0.42), V(0, 0, 0.55)], [0.2, 0.34, 0.36, 0.24, 0.15], m["bronze"], O, subdiv=2, amplitude=0.01, escala_ruido=10, semente=4)
    M.esfera("Alma", V(0, -0.3, -0.1), (0.14, 0.08, 0.2), m["alma"], O, segs=10, aneis=8)
    M.cone("Tampa", V(0, 0, 0.55), V(0, 0, 0.92), 0.16, m["bronze"], O, segs=6)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        M.tubo("Garra", [V(math.cos(a) * 0.3, math.sin(a) * 0.3, -0.75), V(math.cos(a) * 0.4, math.sin(a) * 0.4, -0.35), V(math.cos(a) * 0.3, math.sin(a) * 0.3, 0.05)],
               [0.045, 0.04, 0.012], m["osso"], O, subdiv=1)


def elmo(M, m):
    M.esfera("Casco", V(0, 0, 0.05), (0.6, 0.62, 0.62), m["aco"], O, amplitude=0.02, escala_ruido=5, semente=6)
    M.caixa("Viseira", V(0, -0.5, -0.02), (0.75, 0.2, 0.12), m["preto"], O)
    M.caixa("Nasal", V(0, -0.58, -0.2), (0.1, 0.08, 0.5), m["aco"], O, chanfro=0.02)
    for s in (1, -1):
        M.esfera("Olho", V(s * 0.2, -0.52, -0.02), (0.05, 0.04, 0.04), m["brasa"], O, segs=8, aneis=6)
        M.tubo("Chifre", B.curva(V(s * 0.5, 0, 0.3), V(s * 0.95, -0.05, 0.5), V(s * 0.8, -0.1, 0.95), 6), [0.13, 0.12, 0.1, 0.075, 0.045, 0.012], m["osso"], O, subdiv=1, amplitude=0.01, semente=9 + s)
        M.tubo("Bochecha", [V(s * 0.45, -0.35, -0.2), V(s * 0.4, -0.42, -0.6), V(s * 0.22, -0.45, -0.78)], [(0.05, 0.16), (0.05, 0.14), (0.03, 0.06)], m["aco"], O, subdiv=1)
    for a in (-40, -20, 0, 20, 40):
        ar = math.radians(a)
        M.cone("Crista", V(math.sin(ar) * 0.25, -0.1, 0.62), V(math.sin(ar) * 0.4, -0.12, 0.92 - abs(a) * 0.004), 0.05, m["osso"], O, segs=5)


def peitoral(M, m):
    M.tubo("Torso", [V(0, 0, -1.1), V(0, 0, -0.5), V(0, 0, 0.35), V(0, 0, 0.95)], [(0.5, 0.3), (0.52, 0.32), (0.72, 0.38), (0.4, 0.26)], m["aco"], O, subdiv=2, amplitude=0.02, escala_ruido=5, semente=3)
    for i in range(5):
        z = 0.55 - i * 0.26
        for s in (1, -1):
            M.tubo("Costela", B.curva(V(s * 0.04, -0.36, z), V(s * (0.5 - i * 0.03), -0.42, z - 0.05), V(s * (0.68 - i * 0.04), -0.1, z - 0.16), 5),
                   [0.05, 0.048, 0.044, 0.036, 0.02], m["osso"], O, subdiv=1)
    M.tubo("Esterno", [V(0, -0.4, 0.7), V(0, -0.44, -0.55)], [0.06, 0.05], m["osso"], O, subdiv=1)
    for s in (1, -1):
        M.esfera("Ombreira", V(s * 0.72, 0, 0.82), (0.3, 0.3, 0.2), m["aco"], O, amplitude=0.02, semente=5 + s)
        M.cone("Espinho", V(s * 0.78, 0, 0.9), V(s * 0.98, -0.02, 1.3), 0.08, m["osso"], O, segs=5)
    M.tubo("Cinto", [V(0.52 * math.cos(a), 0.32 * math.sin(a), -0.95) for a in [math.radians(x) for x in range(-90, 271, 40)]], [(0.05, 0.11)] * 10, m["couro"], O, subdiv=1)
    M.esfera("Fivela", V(0, -0.36, -0.95), (0.12, 0.06, 0.12), m["bronze"], O, segs=8, aneis=6)


def luvas(M, m):
    M.tubo("Punho", [V(0.05, 0, -0.8), V(0, 0, -0.3), V(0, 0, 0.18)], [(0.36, 0.26), (0.32, 0.2), (0.42, 0.16)], m["aco"], O, subdiv=2, amplitude=0.015, escala_ruido=6, semente=2)
    M.tubo("Barra", [V(0.38 * math.cos(a), 0.27 * math.sin(a), -0.78) for a in [math.radians(x) for x in range(-90, 271, 45)]], [(0.05, 0.08)] * 9, m["bronze"], O, subdiv=1)
    for i, x in enumerate((-0.27, -0.09, 0.09, 0.27)):
        comp = 0.55 + (0.12 if i in (1, 2) else 0.0)
        M.tubo("Dedo", [V(x, 0, 0.1), V(x * 1.1, -0.05, 0.1 + comp * 0.55), V(x * 1.15, -0.16, 0.1 + comp)], [0.075, 0.065, 0.05], m["aco"], O, subdiv=1)
        M.cone("Garra", V(x * 1.15, -0.16, 0.08 + comp), V(x * 1.2, -0.3, 0.3 + comp), 0.045, m["osso"], O, segs=5)
    M.tubo("Polegar", [V(-0.36, 0, -0.15), V(-0.55, -0.05, 0.1), V(-0.6, -0.14, 0.34)], [0.085, 0.07, 0.05], m["aco"], O, subdiv=1)
    M.cone("GarraPolegar", V(-0.6, -0.14, 0.32), V(-0.66, -0.26, 0.52), 0.045, m["osso"], O, segs=5)
    for z in (-0.5, -0.2):
        M.cone("Rebite", V(0, -0.2, z), V(0, -0.36, z + 0.05), 0.06, m["bronze"], O, segs=5)


def botas(M, m):
    M.tubo("Cano", [V(-0.12, 0, 0.85), V(-0.15, 0, 0.2), V(-0.1, 0, -0.45)], [(0.3, 0.3), (0.26, 0.26), (0.28, 0.28)], m["couro"], O, subdiv=2, amplitude=0.02, escala_ruido=6, semente=2)
    M.tubo("Pe", [V(-0.2, 0, -0.62), V(0.2, 0, -0.66), V(0.62, 0, -0.74)], [(0.3, 0.26), (0.27, 0.24), (0.14, 0.14)], m["couro"], O, subdiv=2, amplitude=0.015, escala_ruido=7, semente=3)
    M.caixa("Sola", V(0.12, 0, -0.92), (1.05, 0.5, 0.1), m["preto"], O, chanfro=0.03)
    M.tubo("Dobra", [V(-0.12 + 0.33 * math.cos(a), 0.33 * math.sin(a), 0.82) for a in [math.radians(x) for x in range(-90, 271, 45)]], [(0.05, 0.12)] * 9, m["couro"], O, subdiv=1)
    for z in (0.5, 0.15, -0.2):
        M.tubo("Fivela", [V(-0.42, -0.05, z), V(-0.12, -0.3, z - 0.02), V(0.18, -0.05, z)], [(0.035, 0.07)] * 3, m["aco"], O, subdiv=1)
    M.cone("Biqueira", V(0.55, 0, -0.72), V(0.86, 0, -0.56), 0.1, m["osso"], O, segs=5)
    M.cone("Espora", V(-0.42, 0, -0.6), V(-0.72, 0, -0.5), 0.07, m["osso"], O, segs=5)


def amuleto(M, m):
    M.tubo("Cordao", B.arco(V(0, 0, 0.2), 0.36, 0.26, 200, -20, 12), [0.028] * 12, m["bronze"], O, subdiv=1)
    M.cone("PingenteCima", V(0, 0, -0.1), V(0, 0, 0.2), 0.2, m["bronze"], O, segs=6)
    M.cone("PingenteBaixo", V(0, 0, -0.1), V(0, 0, -0.46), 0.2, m["bronze"], O, segs=6)
    M.esfera("Gema", V(0, -0.14, -0.1), (0.1, 0.06, 0.13), m["alma"], O, segs=8, aneis=6)
    for s in (1, -1):
        M.cone("Presa", V(s * 0.16, 0, -0.08), V(s * 0.38, 0, -0.38), 0.05, m["osso"], O, segs=5)


def anel(M, m):
    M.tubo("Aro", [V(0.3 * math.cos(a), 0.1 * math.sin(a), -0.12 + 0.3 * math.sin(a) * 0.9) for a in [math.radians(x) for x in range(0, 361, 30)]], [0.055] * 13, m["bronze"], O, subdiv=1)
    MO.cranio(M, V(0, -0.02, 0.12), 1.25, m["osso"], m["preto"], m["brasa"], osso=O, dentes=True, mandibula=False)


#        nome        função     espaços (larg, alt)
ITENS = [("cajado", cajado, (1, 3)), ("foice", foice, (2, 3)), ("grimorio", grimorio, (2, 2)), ("relicario", relicario, (1, 2)),
         ("elmo", elmo, (2, 2)), ("peitoral", peitoral, (2, 3)), ("luvas", luvas, (2, 2)), ("botas", botas, (2, 2)),
         ("amuleto", amuleto, (1, 1)), ("anel", anel, (1, 1))]


def arco_seguro():
    """B.arco pode ter outra assinatura; o amuleto usa esta versão local."""
    def arco(centro, rx, rz, a0, a1, n):
        return [centro + V(rx * math.cos(math.radians(a0 + (a1 - a0) * i / (n - 1))), 0, rz * math.sin(math.radians(a0 + (a1 - a0) * i / (n - 1)))) for i in range(n)]
    B.arco = lambda c, rx, rz, a0, a1, n, **k: arco(c, rx, rz, a0, a1, n)


def luzes_e_camera(larg, alt):
    cena = bpy.context.scene
    cam_d = bpy.data.cameras.new("Cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = max(larg, alt) * 1.02
    cam = bpy.data.objects.new("Cam", cam_d); cena.collection.objects.link(cam)
    cam.location = (1.6, -8.0, 1.1)     # quase de frente, um pouco de lado e de cima, para dar volume
    cam.rotation_euler = (Vector((0, 0, 0)) - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    cena.camera = cam
    for nome, cor, forca, rot in (("Principal", (1.0, 0.86, 0.68), 4.2, (50, 0, 35)), ("Contra", (0.35, 0.55, 1.0), 2.4, (-60, 0, -150)), ("Frente", (1, 1, 1), 1.0, (80, 0, -10))):
        d = bpy.data.lights.new(nome, "SUN"); d.color = cor; d.energy = forca; d.angle = math.radians(8)
        o = bpy.data.objects.new(nome, d); cena.collection.objects.link(o); o.rotation_euler = tuple(math.radians(v) for v in rot)
    mundo = cena.world or bpy.data.worlds.new("Mundo"); cena.world = mundo; mundo.use_nodes = True
    fundo = mundo.node_tree.nodes.get("Background")
    fundo.inputs[0].default_value = (0.10, 0.09, 0.12, 1.0); fundo.inputs[1].default_value = 0.6
    r = cena.render
    r.engine = "CYCLES"; cena.cycles.samples = 48; cena.cycles.use_denoising = False; cena.cycles.device = "CPU"
    r.film_transparent = True
    r.resolution_x = larg * PX_POR_ESPACO; r.resolution_y = alt * PX_POR_ESPACO; r.resolution_percentage = 100
    r.image_settings.file_format = "PNG"; r.image_settings.color_mode = "RGBA"
    cena.view_settings.view_transform = "Standard"; cena.view_settings.look = "None"


def main():
    a = sys.argv
    pedidos = a[a.index("--") + 1:] if "--" in a else a[1:]
    saida = os.path.join(RAIZ, "saida", "itens"); os.makedirs(saida, exist_ok=True)
    arco_seguro()
    meta = {"px_por_espaco": PX_POR_ESPACO, "itens": []}
    for nome, func, (larg, alt) in ITENS:
        meta["itens"].append({"tipo": nome, "arquivo": nome + ".png", "largura": larg, "altura": alt})
        if pedidos and nome not in pedidos:
            continue
        B.cena_limpa()
        M = MO.Montagem()
        func(M, materiais())
        luzes_e_camera(larg, alt)
        bpy.context.scene.render.filepath = os.path.join(saida, nome + ".png")
        bpy.ops.render.render(write_still=True)
        print(f"[itens] {nome}: {larg}x{alt} espaços", flush=True)
    with open(os.path.join(saida, "itens.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
