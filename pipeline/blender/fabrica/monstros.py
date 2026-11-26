"""
Monstros do Ato 1, construídos proceduralmente (anatomia + materiais + rig + animações).
Cada função devolve (rig, acao_padrao, lista_de_acoes_para_config).
"""

import math
from mathutils import Vector
from . import base as B

# ---------------------------------------------------------------------------
# Rig humanoide parametrizado
# ---------------------------------------------------------------------------


def juntas(pernas=1.0, tronco=1.0, ombros=1.0, bracos=1.0, cabeca=1.0):
    zq = 0.95 * pernas
    J = {}
    J["quadril"] = Vector((0, 0, zq))
    J["coluna"] = Vector((0, 0, zq + 0.13 * tronco))
    J["peito"] = Vector((0, 0, zq + 0.33 * tronco))
    J["pescoco"] = Vector((0, 0, zq + 0.53 * tronco))
    J["cabeca"] = Vector((0, 0, zq + 0.61 * tronco))
    J["topo"] = J["cabeca"] + Vector((0, 0, 0.24 * cabeca))
    for s, l in ((1, "L"), (-1, "R")):
        om = Vector((s * 0.22 * ombros, 0, J["pescoco"].z - 0.04))
        J["ombro." + l] = om
        J["cotovelo." + l] = om + Vector((s * 0.04, 0.0, -0.30 * bracos))
        J["pulso." + l] = J["cotovelo." + l] + Vector((s * 0.02, -0.02, -0.28 * bracos))
        J["mao." + l] = J["pulso." + l] + Vector((0, -0.01, -0.12 * bracos))
        J["anca." + l] = Vector((s * 0.10, 0, zq))
        J["joelho." + l] = Vector((s * 0.11, -0.01, zq * 0.55))
        J["tornozelo." + l] = Vector((s * 0.11, 0.01, 0.10))
        J["dedo." + l] = Vector((s * 0.11, -0.15, 0.02))
    return J


def rig_humanoide(nome, J, extras=None):
    d = {
        "quadril": (J["quadril"], J["coluna"], None),
        "coluna": (J["coluna"], J["peito"], "quadril"),
        "peito": (J["peito"], J["pescoco"], "coluna"),
        "pescoco": (J["pescoco"], J["cabeca"], "peito"),
        "cabeca": (J["cabeca"], J["topo"], "pescoco"),
        "mandibula": (J["cabeca"] + Vector((0, -0.02, 0.04)), J["cabeca"] + Vector((0, -0.12, 0.0)), "cabeca"),
    }
    for l in ("L", "R"):
        d["braco." + l] = (J["ombro." + l], J["cotovelo." + l], "peito")
        d["antebraco." + l] = (J["cotovelo." + l], J["pulso." + l], "braco." + l)
        d["mao." + l] = (J["pulso." + l], J["mao." + l], "antebraco." + l)
        d["coxa." + l] = (J["anca." + l], J["joelho." + l], "quadril")
        d["canela." + l] = (J["joelho." + l], J["tornozelo." + l], "coxa." + l)
        d["pe." + l] = (J["tornozelo." + l], J["dedo." + l], "canela." + l)
    if extras:
        d.update(extras)
    rig = B.Rig(nome)
    rig.construir(d)
    return rig


# ---------------------------------------------------------------------------
# Animações compartilhadas
# ---------------------------------------------------------------------------

def _espelho(pose):
    out = {}
    for k, v in pose.items():
        if ".L" in k:
            k2 = k.replace(".L", ".R")
        elif ".R" in k:
            k2 = k.replace(".R", ".L")
        else:
            k2 = k
        if k.endswith("@loc"):
            out[k2] = (-v[0], v[1], v[2])
        elif ".L" in k or ".R" in k:
            out[k2] = v
        else:
            out[k2] = (v[0], -v[1], -v[2])
    return out


def anim_parado(rig, base, amplitude=1.0):
    a = amplitude
    resp = {"peito": (3 * a, 0, 0), "cabeca": (-3 * a, 0, 0), "braco.L": (3 * a, 2 * a, 0),
            "braco.R": (3 * a, 2 * a, 0), "quadril@loc": (0, 0, -0.012 * a)}
    return B.animar(rig, "Parado", {1: {}, 13: resp, 25: {}}, base)


def anim_andar(rig, base, passo=1.0, arrastar=False):
    p = passo
    contato = {
        "coxa.L": (32 * p, 0, 0), "canela.L": (-8, 0, 0), "pe.L": (10, 0, 0),
        "coxa.R": (-24 * p, 0, 0), "canela.R": (-30 * p, 0, 0),
        "braco.L": (-22 * p, 0, 0), "braco.R": (22 * p, 0, 0),
        "antebraco.L": (10, 0, 0), "antebraco.R": (18, 0, 0),
        "peito": (0, 0, 8 * p), "quadril": (0, 0, -6 * p),
    }
    passagem = {
        "coxa.L": (4, 0, 0), "canela.L": (-4, 0, 0),
        "coxa.R": (22 * p, 0, 0), "canela.R": (-58 * p, 0, 0),
        "quadril@loc": (0, 0, 0.025), "braco.L": (0, 0, 0), "braco.R": (0, 0, 0),
    }
    if arrastar:   # zumbi: uma perna dura, tronco balançando
        contato["canela.R"] = (-8, 0, 0)
        passagem["canela.R"] = (-15, 0, 0)
        contato["peito"] = (6, 8, 10)
        passagem["peito"] = (0, -6, 0)
    return B.animar(rig, "Andar", {1: contato, 7: passagem, 13: _espelho(contato),
                                    19: _espelho(passagem), 25: contato}, base)


def anim_atacar_arma(rig, base):
    return B.animar(rig, "Atacar", {
        1: {},
        6: {"braco.R": (165, 15, 0), "antebraco.R": (45, 0, 0), "peito": (-12, 0, -25),
            "coluna": (-6, 0, -10), "cabeca": (5, 0, 15), "coxa.R": (-12, 0, 0), "braco.L": (20, 15, 0)},
        9: {"braco.R": (70, -10, 0), "antebraco.R": (8, 0, 0), "peito": (22, 0, 28),
            "coluna": (12, 0, 10), "coxa.L": (30, 0, 0), "canela.L": (-25, 0, 0), "coxa.R": (-20, 0, 0),
            "quadril@loc": (0, -0.10, -0.05), "braco.L": (-25, 20, 0)},
        12: {"braco.R": (25, -25, 0), "antebraco.R": (15, 0, 0), "peito": (28, 0, 35),
             "coluna": (14, 0, 12), "coxa.L": (30, 0, 0), "canela.L": (-28, 0, 0), "coxa.R": (-20, 0, 0),
             "quadril@loc": (0, -0.12, -0.07), "braco.L": (-30, 20, 0)},
        20: {},
    }, base)


def anim_atacar_garras(rig, base):
    return B.animar(rig, "Atacar", {
        1: {},
        6: {"braco.R": (130, 35, 0), "braco.L": (110, 35, 0), "antebraco.R": (50, 0, 0),
            "antebraco.L": (50, 0, 0), "peito": (-18, 0, 0), "cabeca": (-15, 0, 0), "mandibula": (-30, 0, 0)},
        9: {"braco.R": (75, -15, 0), "braco.L": (70, -15, 0), "antebraco.R": (15, 0, 0),
            "antebraco.L": (15, 0, 0), "peito": (28, 0, 0), "coluna": (15, 0, 0), "cabeca": (15, 0, 0),
            "mandibula": (-40, 0, 0), "coxa.L": (25, 0, 0), "canela.L": (-20, 0, 0),
            "quadril@loc": (0, -0.12, -0.04)},
        12: {"braco.R": (35, -25, 0), "braco.L": (35, -25, 0), "antebraco.R": (25, 0, 0),
             "antebraco.L": (25, 0, 0), "peito": (32, 0, 0), "coluna": (15, 0, 0),
             "coxa.L": (25, 0, 0), "canela.L": (-22, 0, 0), "quadril@loc": (0, -0.12, -0.05)},
        20: {},
    }, base)


def anim_conjurar(rig, base):
    return B.animar(rig, "Conjurar", {
        1: {},
        7: {"cajado": (170, 0, 0), "braco.R": (150, 20, 0), "antebraco.R": (20, 0, 0), "braco.L": (60, 50, 0),
            "antebraco.L": (60, 0, 0), "peito": (-12, 0, 0), "cabeca": (-18, 0, 0),
            "quadril@loc": (0, 0, 0.03)},
        11: {"cajado": (170, 0, 0), "braco.R": (160, 25, 0), "antebraco.R": (10, 0, 0), "braco.L": (75, 55, 0),
             "antebraco.L": (50, 0, 0), "peito": (-15, 0, 0), "cabeca": (-22, 0, 0),
             "quadril@loc": (0, 0, 0.04)},
        15: {"cajado": (85, 0, 0), "braco.R": (80, 0, 0), "antebraco.R": (5, 0, 0), "braco.L": (85, 10, 0),
             "antebraco.L": (5, 0, 0), "peito": (15, 0, 0), "cabeca": (8, 0, 0)},
        22: {},
    }, base)


def anim_morrer(rig, base, queda=0.86):
    return B.animar(rig, "Morrer", {
        1: {},
        5: {"peito": (-22, 0, 10), "cabeca": (-28, 0, 0), "braco.L": (-20, 40, 0), "braco.R": (-20, 40, 0),
            "mandibula": (-35, 0, 0), "quadril@loc": (0, 0.05, 0)},
        12: {"coxa.L": (45, 0, 0), "canela.L": (-100, 0, 0), "coxa.R": (35, 0, 0), "canela.R": (-95, 0, 0),
             "peito": (20, 0, 0), "cabeca": (25, 0, 0), "braco.L": (20, 25, 0), "braco.R": (15, 25, 0),
             "quadril@loc": (0, -0.05, -0.42 * queda / 0.86)},
        20: {"quadril": (86, 0, 6), "coxa.L": (8, 6, 0), "canela.L": (-12, 0, 0), "coxa.R": (2, 10, 0),
             "canela.R": (-25, 0, 0), "peito": (4, 0, 8), "cabeca": (-8, 0, 30),
             "braco.L": (110, 40, 0), "antebraco.L": (40, 0, 0), "braco.R": (15, 50, 0),
             "antebraco.R": (30, 0, 0), "quadril@loc": (0, -0.25, -queda)},
        28: {"quadril": (89, 0, 6), "coxa.L": (4, 8, 0), "canela.L": (-6, 0, 0), "coxa.R": (0, 12, 0),
             "canela.R": (-18, 0, 0), "peito": (0, 0, 8), "cabeca": (-4, 0, 35),
             "braco.L": (115, 45, 0), "antebraco.L": (45, 0, 0), "braco.R": (12, 55, 0),
             "antebraco.R": (35, 0, 0), "quadril@loc": (0, -0.27, -queda - 0.02)},
    }, base)


ACOES_PADRAO = [
    {"nome": "parado", "acao_blender": "Parado", "frames": 4, "fps": 6, "loop": True},
    {"nome": "andar", "acao_blender": "Andar", "frames": 8, "fps": 10, "loop": True},
    {"nome": "atacar", "acao_blender": "Atacar", "lista_frames": [1, 4, 6, 9, 12, 16], "fps": 12, "loop": False},
    {"nome": "morrer", "acao_blender": "Morrer", "lista_frames": [1, 5, 9, 12, 16, 20, 28], "fps": 10, "loop": False},
]


# ---------------------------------------------------------------------------
# Peças reutilizáveis
# ---------------------------------------------------------------------------

class Montagem:
    def __init__(self):
        self.partes = []

    def tubo(self, *a, **k):
        o = B.tubo(*a, **k); self.partes.append(o); return o

    def esfera(self, *a, **k):
        o = B.esfera(*a, **k); self.partes.append(o); return o

    def caixa(self, *a, **k):
        o = B.caixa(*a, **k); self.partes.append(o); return o

    def cone(self, *a, **k):
        o = B.cone(*a, **k); self.partes.append(o); return o

    def membrana(self, *a, **k):
        o = B.membrana(*a, **k); self.partes.append(o); return o


def cranio(M, c, esc, m_osso, m_preto, m_olho, osso="cabeca", dentes=True, mandibula=True, chifres=None):
    """Crânio com órbitas, nariz, dentes e mandíbula articulada. c = base do crânio (nuca)."""
    c = Vector(c)
    e = esc

    def P(x, y, z):
        return c + Vector((x * e, y * e, z * e))

    M.esfera("Cranio", P(0, 0.01, 0.135), (0.105 * e, 0.125 * e, 0.11 * e), m_osso, osso, amplitude=0.006 * e, semente=1)
    M.esfera("Face", P(0, -0.075, 0.07), (0.072 * e, 0.06 * e, 0.062 * e), m_osso, osso)
    for s in (1, -1):
        M.esfera("Zigoma", P(s * 0.075, -0.07, 0.085), (0.03 * e, 0.04 * e, 0.018 * e), m_osso, osso)
        M.tubo("Sobrancelha", [P(s * 0.01, -0.118, 0.158), P(s * 0.05, -0.122, 0.168), P(s * 0.09, -0.085, 0.150)],
               [0.014 * e, 0.016 * e, 0.010 * e], m_osso, osso, subdiv=1)
        M.esfera("Orbita", P(s * 0.044, -0.108, 0.128), (0.031 * e, 0.022 * e, 0.030 * e), m_preto, osso)
        M.esfera("Olho", P(s * 0.044, -0.127, 0.126), (0.013 * e, 0.008 * e, 0.013 * e), m_olho, osso, segs=8, aneis=6)
    M.cone("Nariz", P(0, -0.128, 0.068), P(0, -0.126, 0.112), 0.02 * e, m_preto, osso, segs=3)
    if dentes:
        for i in range(7):
            x = (i - 3) * 0.017
            y = -0.128 + abs(i - 3) * 0.008
            M.cone("DenteS", P(x, y, 0.040), P(x, y - 0.002, 0.008), 0.0085 * e, m_osso, osso, segs=5)
    if mandibula:
        arco = [P(-0.078, -0.01, 0.03), P(-0.066, -0.075, -0.012), P(-0.03, -0.122, -0.03),
                P(0.03, -0.122, -0.03), P(0.066, -0.075, -0.012), P(0.078, -0.01, 0.03)]
        M.tubo("Mandibula", arco, [0.016 * e, 0.018 * e, 0.02 * e, 0.02 * e, 0.018 * e, 0.016 * e],
               m_osso, "mandibula", subdiv=1)
        if dentes:
            for i in range(6):
                x = (i - 2.5) * 0.018
                y = -0.124 + abs(i - 2.5) * 0.007
                M.cone("DenteI", P(x, y, -0.018), P(x, y - 0.002, 0.012), 0.008 * e, m_osso, "mandibula", segs=5)
    if chifres:
        m_ch, comp = chifres
        for s in (1, -1):
            pts = B.curva(P(s * 0.075, 0.0, 0.19), P(s * 0.20 * comp, 0.03, 0.26 * comp + 0.05),
                          P(s * 0.13 * comp, -0.10 * comp, 0.40 * comp), 6)
            M.tubo("Chifre", pts, [0.032 * e, 0.03 * e, 0.025 * e, 0.018 * e, 0.01 * e, 0.003 * e], m_ch, osso, subdiv=1)


def mao_garras(M, J, lado, m, n_dedos=4, comp=0.11, raio=0.011, m_garra=None):
    l = "L" if lado > 0 else "R"
    p = J["pulso." + l]
    M.esfera("Palma", p + Vector((0, -0.01, -0.035)), (0.028, 0.02, 0.045), m, "mao." + l, segs=8, aneis=6)
    for i in range(n_dedos):
        t = (i - (n_dedos - 1) / 2) / max(1, n_dedos - 1)
        b = p + Vector((lado * 0.0 + t * 0.045, -0.012, -0.065))
        fim = b + Vector((t * 0.035, -0.035, -comp))
        meio = b + Vector((t * 0.03, 0.012, -comp * 0.55))
        M.tubo("Dedo", B.curva(b, meio, fim, 4), [raio, raio * 0.9, raio * 0.7, raio * 0.25],
               m_garra or m, "mao." + l, subdiv=1)


def pe_garras(M, J, lado, m, n=3, m_garra=None):
    l = "L" if lado > 0 else "R"
    t = J["tornozelo." + l]
    M.esfera("Calcanhar", t + Vector((0, 0.02, -0.05)), (0.035, 0.05, 0.04), m, "pe." + l, segs=8, aneis=6)
    for i in range(n):
        a = (i - (n - 1) / 2) * 0.035
        M.tubo("DedoPe", [t + Vector((a * 0.4, -0.02, -0.06)), t + Vector((a, -0.10, -0.075)),
                          t + Vector((a * 1.5, -0.17, -0.09))], [0.02, 0.016, 0.004],
               m_garra or m, "pe." + l, subdiv=1)


def costelas(M, J, m, n=6, raio=0.16, prof=0.12, osso_sup="peito", osso_inf="coluna", lados=(1, -1),
             grossura=0.013):
    topo = J["pescoco"].z - 0.07
    for i in range(n):
        z = topo - i * 0.05
        k = 1.0 - abs(i - 1.5) * 0.07
        for s in lados:
            pts = B.arco((0, 0.025, z), raio * k, prof * k, 95, -78, 7, inclinacao=0.05)
            if s < 0:
                pts = B.espelhar(pts)
            M.tubo("Costela", pts, [grossura * 1.3, grossura, grossura, grossura, grossura * 0.9,
                                    grossura * 0.8, grossura * 0.6],
                   m, osso_sup if i < 4 else osso_inf, subdiv=1)
    M.tubo("Esterno", [(0, -prof + 0.012, topo + 0.02), (0, -prof - 0.004, topo - 0.10), (0, -prof + 0.01, topo - 0.2)],
           [(0.03, 0.012), (0.026, 0.012), (0.012, 0.008)], m, osso_sup, subdiv=1)


def vertebras(M, J, m, z0, z1, n=10):
    for i in range(n):
        z = z0 + (z1 - z0) * i / (n - 1)
        osso = "quadril" if z < J["coluna"].z else "coluna" if z < J["peito"].z else \
            "peito" if z < J["pescoco"].z else "pescoco"
        M.esfera("Vertebra", (0, 0.035, z), (0.028, 0.03, 0.02), m, osso, segs=8, aneis=6)
        M.cone("Espinha", (0, 0.05, z), (0, 0.10, z - 0.02), 0.012, m, osso, segs=4)


def espada(M, J, m_metal, m_cabo, m_sangue, comp=0.95, osso="mao.R"):
    h = J["mao.R"] + Vector((0, -0.01, 0.03))
    M.tubo("Cabo", [h + Vector((0, 0.10, 0.0)), h + Vector((0, -0.07, 0.0))], [0.016, 0.016], m_cabo, osso, subdiv=1)
    M.esfera("Pomo", h + Vector((0, 0.115, 0)), (0.026, 0.026, 0.026), m_metal, osso, segs=8, aneis=6)
    M.tubo("Guarda", [h + Vector((0, -0.08, -0.11)), h + Vector((0, -0.075, 0)), h + Vector((0, -0.08, 0.11))],
           [0.012, 0.02, 0.012], m_metal, osso, subdiv=1)
    y0 = -0.09
    pts = [h + Vector((0, y0, 0)), h + Vector((0, y0 - comp * 0.35, 0.0)), h + Vector((0, y0 - comp * 0.75, 0.004)),
           h + Vector((0, y0 - comp * 0.93, 0.0)), h + Vector((0, y0 - comp, -0.004))]
    M.tubo("Lamina", pts, [(0.009, 0.050), (0.009, 0.056), (0.008, 0.048), (0.006, 0.03), (0.002, 0.004)],
           m_metal, osso, subdiv=2, amplitude=0.006, escala_ruido=14.0, semente=4)
    M.tubo("SangueLamina", [h + Vector((0.004, y0 - comp * 0.55, 0.0)), h + Vector((0.004, y0 - comp * 0.95, 0.0))],
           [(0.008, 0.04), (0.004, 0.012)], m_sangue, osso, subdiv=1)


def trapos(M, pts_topo, comp, m, osso, semente=0, n=5):
    """Tiras de pano rasgado penduradas a partir de uma linha de pontos."""
    for i in range(len(pts_topo) - 1):
        a, b = Vector(pts_topo[i]), Vector(pts_topo[i + 1])
        c1 = comp * (0.6 + 0.4 * ((i * 37 + semente * 11) % 10) / 10.0)
        c2 = comp * (0.6 + 0.4 * ((i * 53 + semente * 7) % 10) / 10.0)
        fora = Vector(((a.x + b.x) * 0.15, (a.y + b.y) * 0.15, 0))
        cont = [a, b, b + fora + Vector((0, 0, -c2 * 0.6)), (a + b) / 2 + fora * 1.3 + Vector((0, 0, -max(c1, c2))),
                a + fora + Vector((0, 0, -c1 * 0.7))]
        M.membrana("Trapo", cont, m, osso, amplitude=0.012, semente=semente + i)


# ---------------------------------------------------------------------------
# ESQUELETO GUERREIRO
# ---------------------------------------------------------------------------

def esqueleto(variante="guerreiro"):
    arqueiro = variante == "arqueiro"
    colosso = variante == "colosso"   # o mesmo campeão, enorme: Colosso de Ossos
    elite = variante == "elite" or colosso
    B.cena_limpa()
    J = juntas()
    rig = rig_humanoide("Esqueleto", J)
    M = Montagem()
    osso = B.mat_osso()
    ferro = B.mat_metal("FerroVelho", qtd=0.42)
    couro = B.mat_pano("Couro", (0.10, 0.05, 0.03), (0.02, 0.01, 0.01))
    pano = B.mat_pano("Trapo", (0.20, 0.03, 0.03), (0.03, 0.01, 0.01))
    preto = B.mat_liso("Vazio", (0.004, 0.003, 0.003), 1.0)
    olho = B.mat_emissao("OlhoVerde", (0.25, 1.0, 0.35), 25.0)
    if elite:   # campeão: ossos enegrecidos, olhos de sangue, ferro negro
        osso = B.mat_osso("OssoNegro", (0.36, 0.30, 0.24), (0.05, 0.03, 0.025))
        olho = B.mat_emissao("OlhoRubro", (1.0, 0.1, 0.05), 35.0)
        ferro = B.mat_metal("FerroNegro", (0.09, 0.09, 0.10), (0.10, 0.03, 0.015), 0.3)
        pano = B.mat_pano("CapaRubra", (0.26, 0.02, 0.02), (0.03, 0.005, 0.005))
    sangue = B.mat_sangue()

    cranio(M, J["cabeca"] + Vector((0, 0, 0.0)), 1.0, osso, preto, olho)
    vertebras(M, J, osso, J["quadril"].z + 0.02, J["cabeca"].z - 0.01, 12)
    costelas(M, J, osso)

    # pélvis
    for s in (1, -1):
        pts = B.arco((0, 0.02, J["quadril"].z + 0.07), 0.15, 0.10, 100, -60, 6, inclinacao=0.11)
        if s < 0:
            pts = B.espelhar(pts)
        M.tubo("Iliaco", pts, [(0.03, 0.05), (0.03, 0.06), (0.028, 0.055), (0.024, 0.04), (0.02, 0.03), (0.016, 0.02)],
               osso, "quadril", subdiv=1)
    M.esfera("Sacro", J["quadril"] + Vector((0, 0.04, 0.02)), (0.05, 0.035, 0.07), osso, "quadril", segs=8, aneis=6)

    for s, l in ((1, "L"), (-1, "R")):
        om, ct, pu = J["ombro." + l], J["cotovelo." + l], J["pulso." + l]
        M.tubo("Clavicula", [(s * 0.02, -0.10, om.z + 0.03), (s * 0.12, -0.085, om.z + 0.05), om + Vector((0, -0.01, 0.02))],
               [0.014, 0.012, 0.016], osso, "peito", subdiv=1)
        M.esfera("Escapula", (s * 0.13, 0.10, om.z - 0.06), (0.07, 0.018, 0.09), osso, "peito", segs=8, aneis=6)
        M.tubo("Umero", [om, om + Vector((s * 0.01, 0, -0.03)), (om + ct) / 2, ct + Vector((0, 0, 0.02)), ct],
               [0.036, 0.022, 0.017, 0.022, 0.030], osso, "braco." + l, subdiv=1)
        M.tubo("Radio", [ct + Vector((s * 0.012, -0.01, 0)), (ct + pu) / 2 + Vector((s * 0.02, -0.012, 0)), pu + Vector((s * 0.012, 0, 0))],
               [0.018, 0.012, 0.016], osso, "antebraco." + l, subdiv=1)
        M.tubo("Ulna", [ct + Vector((-s * 0.012, 0.012, 0.01)), (ct + pu) / 2 + Vector((-s * 0.012, 0.012, 0)), pu + Vector((-s * 0.012, 0, 0))],
               [0.02, 0.011, 0.013], osso, "antebraco." + l, subdiv=1)
        mao_garras(M, J, s, osso, 4, 0.10, 0.010)
        an, jo, to = J["anca." + l], J["joelho." + l], J["tornozelo." + l]
        M.tubo("Femur", [an + Vector((s * 0.03, 0, 0.01)), an + Vector((s * 0.01, 0, -0.04)), (an + jo) / 2 + Vector((0, -0.015, 0)),
                         jo + Vector((0, 0, 0.03)), jo],
               [0.042, 0.026, 0.021, 0.028, 0.04], osso, "coxa." + l, subdiv=1)
        M.esfera("Rotula", jo + Vector((0, -0.035, 0.0)), (0.026, 0.018, 0.03), osso, "canela." + l, segs=8, aneis=6)
        M.tubo("Tibia", [jo + Vector((0, 0, -0.01)), (jo + to) / 2 + Vector((0, -0.012, 0)), to],
               [0.036, 0.02, 0.026], osso, "canela." + l, subdiv=1)
        M.tubo("Fibula", [jo + Vector((s * 0.03, 0.01, -0.03)), (jo + to) / 2 + Vector((s * 0.035, 0.01, 0)), to + Vector((s * 0.02, 0.01, 0))],
               [0.012, 0.009, 0.012], osso, "canela." + l, subdiv=1)
        pe_garras(M, J, s, osso, 3)

    if not arqueiro:
        # equipamento: ombreira enferrujada com espinhos, tiras de couro, tanga rasgada, elmo quebrado
        om = J["ombro.R"]
        M.esfera("Ombreira", om + Vector((-0.02, 0, 0.03)), (0.10, 0.10, 0.075), ferro, "peito", segs=12, aneis=8, amplitude=0.008, semente=3)
        for a in (-40, 0, 40):
            d = Vector((-math.cos(math.radians(a)) * 0.6, math.sin(math.radians(a)) * 0.6, 0.8)).normalized()
            b0 = om + Vector((-0.02, 0, 0.03)) + d * 0.075
            M.cone("Espinho", b0, b0 + d * 0.10, 0.022, ferro, "peito", segs=5)
        M.tubo("Alca", [om + Vector((0.0, -0.09, 0.0)), (0.0, -0.135, J["peito"].z + 0.05), (0.16, -0.10, J["coluna"].z + 0.06),
                        (0.17, 0.06, J["coluna"].z + 0.05), (0.0, 0.14, J["peito"].z + 0.06), om + Vector((0, 0.09, 0.0))],
               [(0.008, 0.028)] * 6, couro, "peito", subdiv=1)
    zc = J["quadril"].z + 0.10
    M.tubo("Cinto", [(0.16 * math.cos(a), 0.02 + 0.115 * math.sin(a), zc - 0.02 * math.cos(a * 2)) for a in
                     [math.radians(x) for x in range(-90, 271, 45)]], [(0.012, 0.03)] * 9, couro, "quadril", subdiv=1)
    M.esfera("Fivela", (0, -0.105, zc - 0.02), (0.035, 0.012, 0.035), ferro, "quadril", segs=8, aneis=6)
    trapos(M, [(-0.11, -0.10, zc), (-0.04, -0.115, zc), (0.04, -0.115, zc), (0.11, -0.10, zc)], 0.42, pano, "quadril", 1)
    trapos(M, [(0.11, 0.13, zc), (0.0, 0.145, zc), (-0.11, 0.13, zc)], 0.50, pano, "quadril", 5)
    if arqueiro:
        # capuz de trapo, aljava com flechas nas costas, arco longo de osso e tendão
        c = J["cabeca"]
        M.tubo("CapuzTrapo", [c + Vector((0, 0.04, -0.05)), c + Vector((0, 0.05, 0.12)), c + Vector((0, 0.01, 0.25)), c + Vector((0, -0.06, 0.29))],
               [(0.13, 0.11), (0.14, 0.15), (0.13, 0.14), (0.04, 0.04)], pano, "cabeca", amplitude=0.02, escala_ruido=7, semente=6)
        trapos(M, [(-0.14, 0.02, J["pescoco"].z + 0.02), (-0.08, -0.10, J["pescoco"].z), (0.08, -0.10, J["pescoco"].z), (0.14, 0.02, J["pescoco"].z + 0.02)], 0.20, pano, "peito", 11)
        pz = J["peito"].z
        M.tubo("Aljava", [(0.10, 0.15, pz - 0.18), (-0.06, 0.17, pz + 0.20)], [0.05, 0.06], couro, "peito", subdiv=1, amplitude=0.006, semente=12)
        for i in range(5):
            b0 = Vector((-0.06 + (i - 2) * 0.016, 0.17 + (i % 2) * 0.02, pz + 0.20))
            ponta = b0 + Vector((-0.07 + i * 0.01, 0.01, 0.20 + (i % 3) * 0.03))
            M.tubo("Flecha", [b0, ponta], [0.006, 0.006], couro, "peito", subdiv=0)
            M.membrana("Pena", [ponta, ponta + Vector((0.03, 0, -0.07)), ponta + Vector((0, 0, -0.09)), ponta + Vector((-0.03, 0, -0.07))], pano, "peito")
        h = J["mao.L"] + Vector((0, -0.02, 0.03))
        cima, baixo = h + Vector((0, 0.10, 0.62)), h + Vector((0, 0.10, -0.62))
        M.tubo("Arco", B.curva(cima, h + Vector((0, -0.22, 0)), baixo, 9), [0.008, 0.014, 0.018, 0.022, 0.026, 0.022, 0.018, 0.014, 0.008], osso, "mao.L", subdiv=1)
        M.tubo("Corda", [cima, baixo], [0.003, 0.003], couro, "mao.L", subdiv=0)
        for k in (cima, baixo):
            M.cone("ArcoPonta", k, k + Vector((0, 0.07, 0.05 if k is cima else -0.05)), 0.012, osso, "mao.L", segs=4)
    else:
        # elmo aberto, amassado, com crista
        tc = J["cabeca"] + Vector((0, 0.02, 0.165))
        M.tubo("ElmoAro", [(tc.x + 0.118 * math.cos(a), tc.y + 0.135 * math.sin(a), tc.z - 0.01) for a in
                           [math.radians(x) for x in range(-60, 241, 30)]], [(0.01, 0.03)] * 11, ferro, "cabeca", subdiv=1)
        M.tubo("ElmoCrista", B.curva(tc + Vector((0, -0.13, 0.0)), tc + Vector((0, 0, 0.16)), tc + Vector((0, 0.14, -0.02)), 6),
               [(0.012, 0.03)] * 6, ferro, "cabeca", subdiv=1)
        for s in (1, -1):
            M.tubo("ElmoLado", B.curva(tc + Vector((s * 0.118, 0, -0.01)), tc + Vector((s * 0.10, 0, 0.10)), tc + Vector((0, 0, 0.115)), 5),
                   [(0.01, 0.028)] * 5, ferro, "cabeca", subdiv=1)

        if elite:
            # segunda ombreira, chifres no elmo, capa rasgada e machado de duas lâminas
            oml = J["ombro.L"]
            M.esfera("OmbreiraE", oml + Vector((0.02, 0, 0.03)), (0.11, 0.11, 0.08), ferro, "peito", segs=12, aneis=8, amplitude=0.008, semente=13)
            for a in (-40, 0, 40):
                dd = Vector((math.cos(math.radians(a)) * 0.6, math.sin(math.radians(a)) * 0.6, 0.8)).normalized()
                b0 = oml + Vector((0.02, 0, 0.03)) + dd * 0.08
                M.cone("EspinhoE", b0, b0 + dd * 0.13, 0.024, ferro, "peito", segs=5)
            for sx in (1, -1):
                M.tubo("ChifreElmo", B.curva(tc + Vector((sx * 0.11, 0.0, 0.03)), tc + Vector((sx * 0.30, 0.02, 0.10)), tc + Vector((sx * 0.26, -0.06, 0.32)), 6),
                       [0.036, 0.034, 0.028, 0.02, 0.012, 0.003], osso, "cabeca", subdiv=1)
            pz = J["pescoco"].z
            M.membrana("CapaElite", [(-0.19, 0.10, pz - 0.02), (0.19, 0.10, pz - 0.02), (0.30, 0.22, 0.95), (0.22, 0.30, 0.42), (0.30, 0.34, 0.16), (0.08, 0.34, 0.26),
                                    (-0.06, 0.36, 0.10), (-0.20, 0.33, 0.30), (-0.31, 0.30, 0.12), (-0.28, 0.22, 0.9)], pano, "peito", amplitude=0.03, semente=15)
            h = J["mao.R"] + Vector((0, -0.01, 0.03))
            M.tubo("MachadoCabo", [h + Vector((0, 0.45, 0)), h, h + Vector((0, -0.80, 0))], [0.02, 0.022, 0.02], couro, "mao.R", subdiv=1)
            for sz in (1, -1):
                M.tubo("MachadoLamina", [h + Vector((0, -0.70, sz * 0.02)), h + Vector((0, -0.72, sz * 0.14)), h + Vector((0, -0.70, sz * 0.26))],
                       [(0.012, 0.05), (0.008, 0.14), (0.003, 0.20)], ferro, "mao.R", subdiv=2, amplitude=0.006, escala_ruido=18, semente=16 + sz)
                M.tubo("SangueMachado", [h + Vector((0.008, -0.71, sz * 0.2)), h + Vector((0.008, -0.70, sz * 0.27))], [(0.004, 0.12), (0.003, 0.17)], sangue, "mao.R", subdiv=1)
            M.cone("MachadoPonta", h + Vector((0, -0.80, 0)), h + Vector((0, -0.98, 0)), 0.028, ferro, "mao.R", segs=5)
        else:
            espada(M, J, ferro, couro, sangue)
            # escudo redondo rachado no antebraço esquerdo
            ec = (J["cotovelo.L"] + J["pulso.L"]) / 2 + Vector((0.075, -0.01, 0))
            M.esfera("Escudo", ec, (0.03, 0.21, 0.25), couro, "antebraco.L", segs=16, aneis=8, amplitude=0.01, semente=7)
            M.tubo("EscudoAro", [(ec.x + 0.012, ec.y + 0.21 * math.cos(a), ec.z + 0.25 * math.sin(a)) for a in
                                 [math.radians(x) for x in range(0, 331, 30)]], [0.018] * 12, ferro, "antebraco.L", subdiv=1)
            M.cone("EscudoPonta", ec + Vector((0.02, 0, 0)), ec + Vector((0.14, 0, 0)), 0.05, ferro, "antebraco.L", segs=6)
    rig.vincular_partes(M.partes)
    base = {"coluna": (6, 0, 0), "peito": (8, 0, 0), "cabeca": (-8, 0, 0), "braco.L": (10, 12, 0),
            "antebraco.L": (55, 0, 0), "braco.R": (8, 10, 0), "antebraco.R": (25, 0, 0),
            "coxa.L": (8, 4, 0), "canela.L": (-12, 0, 0), "coxa.R": (-4, 4, 0), "canela.R": (-8, 0, 0)}
    if arqueiro:
        base.update({"braco.L": (35, 10, 0), "antebraco.L": (30, 0, 0)})
    anim_parado(rig, base)
    anim_andar(rig, base)
    if arqueiro:
        mira = {"braco.L": (55, 5, 0), "antebraco.L": (-28, 0, 0), "peito": (-6, 0, 30), "coluna": (-4, 0, 14), "cabeca": (6, 0, -30),
                "coxa.L": (16, 0, 0), "coxa.R": (-14, 0, 0)}
        puxa = dict(mira, **{"braco.R": (78, -20, 0), "antebraco.R": (115, 0, 0)})
        solta = dict(mira, **{"braco.R": (60, 30, 0), "antebraco.R": (40, 0, 0)})
        B.animar(rig, "Atacar", {1: {}, 5: dict(mira, **{"braco.R": (70, -30, 0), "antebraco.R": (30, 0, 0)}), 10: puxa, 13: puxa, 15: solta, 22: {}}, base)
    else:
        anim_atacar_arma(rig, base)
    anim_morrer(rig, base)
    acoes = [dict(a) for a in ACOES_PADRAO]
    if arqueiro:
        acoes[2] = {"nome": "atacar", "acao_blender": "Atacar", "lista_frames": [1, 5, 8, 11, 14, 16], "fps": 10, "loop": False}
    return rig, "Parado", acoes, {"altura_px": 168 if colosso else 110 if elite else 96}


def esqueleto_elite():
    return esqueleto("elite")


def arqueiro_ossudo():
    return esqueleto("arqueiro")


# ---------------------------------------------------------------------------
# ZUMBI
# ---------------------------------------------------------------------------

def zumbi(variante="zumbi"):
    carnical = variante == "carnical"   # magro, cinza-azulado, olhos brancos: rápido e faminto
    B.cena_limpa()
    J = juntas(ombros=1.1)
    rig = rig_humanoide("Zumbi", J)
    M = Montagem()
    carne = B.mat_carne()
    carne_viva = B.mat_carne("CarneViva", ((0.17, 0.02, 0.02), (0.26, 0.06, 0.04), (0.08, 0.008, 0.012)),
                             sangue_qtd=0.6)
    osso = B.mat_osso()
    pano = B.mat_pano("Farrapo", (0.16, 0.13, 0.09), (0.03, 0.02, 0.02))
    preto = B.mat_liso("Vazio", (0.004, 0.003, 0.003), 1.0)
    olho = B.mat_emissao("OlhoAmarelo", (1.0, 0.75, 0.15), 18.0)
    sangue = B.mat_sangue()
    if carnical:
        carne = B.mat_carne("CarneFria", ((0.13, 0.16, 0.19), (0.24, 0.27, 0.30), (0.07, 0.08, 0.12)), veia=(0.03, 0.03, 0.08), sangue_qtd=0.55, rasgos=0.65)
        pano = B.mat_pano("Mortalha", (0.07, 0.07, 0.085), (0.015, 0.015, 0.02))
        olho = B.mat_emissao("OlhoBranco", (0.85, 0.95, 1.0), 26.0)

    q, co, pe, pc = J["quadril"], J["coluna"], J["peito"], J["pescoco"]
    c = J["cabeca"]
    # tronco inchado e torto; barriga aberta do lado esquerdo
    M.tubo("Bacia", [q + Vector((0, 0, -0.06)), q + Vector((0, 0, 0.06)), co], [(0.16, 0.12), (0.18, 0.14), (0.18, 0.15)],
           carne, "quadril", amplitude=0.02, escala_ruido=5, semente=2)
    M.tubo("Barriga", [co + Vector((0, 0, -0.02)), (co + pe) / 2 + Vector((0, -0.03, 0)), pe],
           [(0.165, 0.14), (0.185, 0.165), (0.18, 0.14)], carne, "coluna", amplitude=0.025, escala_ruido=5, semente=3)
    M.tubo("Torax", [pe + Vector((0, 0, -0.02)), (pe + pc) / 2, pc + Vector((0, 0, -0.01))],
           [(0.19, 0.155), (0.205, 0.14), (0.13, 0.09)], carne, "peito", amplitude=0.02, escala_ruido=5, semente=4)
    # ferida aberta: carne viva, costelas e tripas
    M.esfera("Ferida", (0.10, -0.115, co.z + 0.10), (0.085, 0.05, 0.12), carne_viva, "coluna", amplitude=0.012, escala_ruido=9, semente=5)
    for i in range(4):
        z = pe.z + 0.02 - i * 0.045
        M.tubo("CostelaExposta", [(0.185, 0.0, z + 0.02), (0.16, -0.12, z), (0.07, -0.168, z - 0.02), (0.035, -0.172, z - 0.03)],
               [0.012, 0.012, 0.011, 0.007], osso, "coluna" if i > 1 else "peito", subdiv=1)
    tripa = [(0.09, -0.15, co.z + 0.06), (0.14, -0.19, co.z + 0.0), (0.07, -0.20, co.z - 0.07), (0.13, -0.19, co.z - 0.13),
             (0.06, -0.18, co.z - 0.20), (0.10, -0.17, co.z - 0.30)]
    M.tubo("Tripas", tripa, [0.028, 0.03, 0.028, 0.026, 0.022, 0.016], carne_viva, "quadril", amplitude=0.006, escala_ruido=12, semente=8)
    M.tubo("Pescoco", [pc + Vector((0, 0, -0.03)), J["cabeca"] + Vector((0, -0.01, 0.03))], [0.065, 0.055], carne, "pescoco", amplitude=0.01, semente=6)

    # cabeça: metade com pele, metade crânio à mostra, mandíbula caída
    c = J["cabeca"]
    cranio(M, c, 1.0, osso, preto, olho, dentes=True, mandibula=True)
    M.esfera("PeleCabeca", c + Vector((-0.025, 0.02, 0.135)), (0.10, 0.125, 0.115), carne, "cabeca", amplitude=0.012, escala_ruido=7, semente=9)
    M.esfera("PeleRosto", c + Vector((-0.045, -0.072, 0.075)), (0.05, 0.06, 0.07), carne, "cabeca", amplitude=0.01, escala_ruido=9, semente=10)
    M.esfera("OlhoMorto", c + Vector((-0.044, -0.118, 0.127)), (0.022, 0.015, 0.02), B.mat_liso("Leitoso", (0.75, 0.72, 0.6), 0.3), "cabeca", segs=8, aneis=6)
    for i in range(5):
        M.tubo("Cabelo", [c + Vector((-0.06 + i * 0.03, 0.06, 0.24)), c + Vector((-0.09 + i * 0.045, 0.14, 0.12)), c + Vector((-0.10 + i * 0.05, 0.13, -0.02 - i * 0.01))],
               [(0.012, 0.004), (0.01, 0.004), (0.003, 0.002)], preto, "cabeca", subdiv=1)

    for s, l in ((1, "L"), (-1, "R")):
        om, ct, pu = J["ombro." + l], J["cotovelo." + l], J["pulso." + l]
        M.esfera("Ombro", om + Vector((-s * 0.03, 0, -0.01)), (0.10, 0.085, 0.085), carne, "peito", amplitude=0.012, semente=11 + s)
        M.esfera("JCotovelo", ct, (0.05, 0.05, 0.05), carne, "braco." + l, amplitude=0.008, semente=31)
        M.esfera("JJoelho", J["joelho." + l], (0.066, 0.066, 0.066), pano if s < 0 else carne, "coxa." + l, amplitude=0.008, semente=32)
        M.esfera("JAnca", J["anca." + l] + Vector((0, 0, -0.02)), (0.105, 0.10, 0.10), pano if s < 0 else carne, "quadril", amplitude=0.01, semente=33)
        M.tubo("Braco", [om, (om + ct) / 2, ct], [0.066, 0.058, 0.048], carne, "braco." + l, amplitude=0.012, escala_ruido=8, semente=12 + s)
        if s > 0:
            # braço esquerdo arrancado no cotovelo: toco com osso e carne pendurada
            M.esfera("Toco", ct + Vector((0, 0, -0.01)), (0.05, 0.05, 0.035), carne_viva, "braco.L", amplitude=0.012, escala_ruido=12, semente=13)
            M.tubo("OssoToco", [ct, ct + Vector((0.01, -0.01, -0.13))], [0.02, 0.012], osso, "antebraco.L", subdiv=1)
            M.cone("Lasca", ct + Vector((0.01, -0.01, -0.12)), ct + Vector((0.02, -0.02, -0.19)), 0.012, osso, "antebraco.L", segs=4)
            M.tubo("Tendao", [ct + Vector((-0.02, 0.01, -0.02)), ct + Vector((-0.03, 0.0, -0.12)), ct + Vector((-0.015, -0.02, -0.2))],
                   [0.012, 0.008, 0.003], carne_viva, "antebraco.L", subdiv=1)
        else:
            M.tubo("Antebraco", [ct, (ct + pu) / 2, pu], [0.048, 0.045, 0.032], carne, "antebraco." + l, amplitude=0.01, escala_ruido=9, semente=14)
            mao_garras(M, J, s, carne, 4, 0.12, 0.013, osso)
        an, jo, to = J["anca." + l], J["joelho." + l], J["tornozelo." + l]
        M.tubo("Coxa", [an + Vector((0, 0, 0.02)), (an + jo) / 2, jo], [0.095, 0.08, 0.06], pano if s < 0 else carne, "coxa." + l,
               amplitude=0.015, escala_ruido=6, semente=15 + s)
        if s > 0:
            M.tubo("Canela", [jo, (jo + to) / 2, to], [0.058, 0.05, 0.035], carne, "canela." + l, amplitude=0.012, semente=17)
        else:
            # perna direita roída: tíbia à mostra
            M.tubo("CanelaRoida", [jo, jo + Vector((0, 0, -0.10))], [0.058, 0.04], carne_viva, "canela." + l, amplitude=0.012, escala_ruido=11, semente=18)
            M.tubo("TibiaExposta", [jo + Vector((0, 0, -0.06)), (jo + to) / 2, to], [0.026, 0.018, 0.024], osso, "canela." + l, subdiv=1)
        pe_garras(M, J, s, carne if s > 0 else osso, 3)
    zc = q.z + 0.04
    trapos(M, [(-0.17, -0.06, zc), (-0.10, -0.135, zc), (0.0, -0.15, zc), (0.10, -0.135, zc), (0.17, -0.06, zc)], 0.30, pano, "quadril", 2)
    trapos(M, [(0.17, 0.08, zc), (0.08, 0.15, zc), (-0.08, 0.15, zc), (-0.17, 0.08, zc)], 0.36, pano, "quadril", 6)
    trapos(M, [(-0.21, -0.02, pc.z - 0.02), (-0.14, -0.13, pc.z - 0.04), (-0.03, -0.14, pc.z - 0.06)], 0.30, pano, "peito", 9)
    # pele solta pendurada, vértebras marcando as costas, machadinha esquecida no ombro
    M.membrana("PeleSolta1", [(-0.20, -0.05, pe.z + 0.02), (-0.23, 0.03, pe.z + 0.0), (-0.25, 0.02, pe.z - 0.20), (-0.21, -0.03, pe.z - 0.27)], carne, "peito", amplitude=0.01, semente=41)
    M.membrana("PeleSolta2", [(0.13, -0.16, co.z + 0.20), (0.19, -0.11, co.z + 0.18), (0.20, -0.14, co.z + 0.02), (0.15, -0.19, co.z - 0.03)], carne, "coluna", amplitude=0.01, semente=42)
    M.membrana("PeleSolta3", [(-0.05, -0.13, c.z + 0.03), (0.02, -0.14, c.z + 0.03), (0.01, -0.16, c.z - 0.09), (-0.03, -0.15, c.z - 0.13)], carne, "cabeca", amplitude=0.006, semente=43)
    for i in range(6):
        z = q.z + 0.10 + i * 0.085
        M.esfera("NoEspinha", (0, 0.135 + 0.02 * math.sin(i), z), (0.028, 0.03, 0.03), carne, "coluna" if z < pe.z else "peito", segs=8, aneis=6)
    ferro_z = B.mat_metal("FerroZumbi", qtd=0.55)
    M.tubo("MachadoCabo", [(0.16, 0.04, pc.z + 0.02), (0.30, 0.10, pc.z + 0.30)], [0.014, 0.014], B.mat_pano("Madeira", (0.14, 0.08, 0.04), (0.03, 0.02, 0.01)), "peito", subdiv=1)
    M.tubo("MachadoLamina", [(0.13, 0.02, pc.z - 0.03), (0.17, 0.045, pc.z + 0.05), (0.22, 0.07, pc.z + 0.10)], [(0.004, 0.02), (0.008, 0.07), (0.006, 0.05)], ferro_z, "peito", subdiv=1)
    # estaca cravada nas costas
    M.tubo("Estaca", [(-0.06, 0.10, pe.z + 0.05), (-0.10, 0.36, pe.z + 0.22)], [0.018, 0.016], B.mat_pano("Madeira", (0.14, 0.08, 0.04), (0.03, 0.02, 0.01)), "peito", subdiv=1)
    M.esfera("SangueEstaca", (-0.06, 0.12, pe.z + 0.04), (0.05, 0.03, 0.07), sangue, "peito", segs=8, aneis=6)

    rig.vincular_partes(M.partes)
    base = {"coluna": (16, 6, 0), "peito": (20, -4, 6), "pescoco": (10, 0, 0), "cabeca": (-6, 12, 10),
            "mandibula": (-22, 0, 0), "braco.R": (55, 8, 0), "antebraco.R": (35, 0, 0), "braco.L": (35, 14, 0),
            "antebraco.L": (20, 0, 0), "coxa.L": (14, 5, 0), "canela.L": (-22, 0, 0), "coxa.R": (-2, 6, 0),
            "canela.R": (-10, 0, 0), "quadril@loc": (0, 0, -0.03)}
    anim_parado(rig, base, 1.6)
    anim_andar(rig, base, 0.7, arrastar=True)
    anim_atacar_garras(rig, base)
    anim_morrer(rig, base)
    return rig, "Parado", ACOES_PADRAO, {"altura_px": 90 if carnical else 96}


# ---------------------------------------------------------------------------
# DIABRETE
# ---------------------------------------------------------------------------

def diabrete(variante="diabrete"):
    guerra = variante == "guerra"     # Diabo de Guerra: o mesmo corpo, grande, pele de carvão e brasa
    B.cena_limpa()
    J = juntas(pernas=0.62, tronco=0.72, ombros=0.85, bracos=1.05, cabeca=1.0)
    pe_, q = J["peito"], J["quadril"]
    extras = {
        "asa.L": (pe_ + Vector((0.08, 0.10, 0.10)), pe_ + Vector((0.50, 0.30, 0.45)), "peito"),
        "asa.R": (pe_ + Vector((-0.08, 0.10, 0.10)), pe_ + Vector((-0.50, 0.30, 0.45)), "peito"),
        "cauda1": (q + Vector((0, 0.10, 0.0)), q + Vector((0, 0.38, -0.12)), "quadril"),
        "cauda2": (q + Vector((0, 0.38, -0.12)), q + Vector((0, 0.66, 0.02)), "cauda1"),
    }
    rig = rig_humanoide("Diabrete", J, extras)
    M = Montagem()
    pele = B.mat_demonio("Carvao", (0.05, 0.035, 0.035), (0.01, 0.006, 0.006), (1.0, 0.25, 0.02), 14.0) if guerra else B.mat_demonio()
    membr = B.mat_demonio("Membrana", (0.16, 0.02, 0.03), (0.02, 0.0, 0.01), forca_brilho=0.0)
    chifre = B.mat_osso("Chifre", (0.12, 0.10, 0.09), (0.02, 0.015, 0.01))
    preto = B.mat_liso("Vazio", (0.004, 0.003, 0.003), 1.0)
    olho = B.mat_emissao("OlhoBrasa", (1.0, 0.45, 0.05), 30.0)
    dente = B.mat_osso("Presa", (0.85, 0.80, 0.65), (0.3, 0.2, 0.1))

    co, pc, c = J["coluna"], J["pescoco"], J["cabeca"]
    M.tubo("Tronco", [q + Vector((0, 0, -0.04)), co, pe_, pc + Vector((0, 0, -0.01))],
           [(0.12, 0.10), (0.10, 0.085), (0.15, 0.12), (0.09, 0.07)], pele, "coluna", amplitude=0.015, escala_ruido=8, semente=1)
    for i in range(5):
        z = q.z + 0.05 + i * 0.075
        M.cone("EspinhoCostas", (0, 0.10, z), (0, 0.20, z + 0.03), 0.022, chifre, "coluna" if z < pe_.z else "peito", segs=5)
    # cabeça grande, focinho, orelhas, chifres, boca aberta com presas
    M.esfera("Cabeca", c + Vector((0, 0.0, 0.12)), (0.125, 0.135, 0.115), pele, "cabeca", amplitude=0.012, escala_ruido=8, semente=2)
    M.esfera("Focinho", c + Vector((0, -0.11, 0.075)), (0.075, 0.075, 0.05), pele, "cabeca", amplitude=0.008, semente=3)
    M.esfera("Queixo", c + Vector((0, -0.085, 0.01)), (0.07, 0.085, 0.032), pele, "mandibula", amplitude=0.006, semente=4)
    M.esfera("Boca", c + Vector((0, -0.105, 0.04)), (0.058, 0.06, 0.02), preto, "cabeca", segs=8, aneis=6)
    for i in range(6):
        x = (i - 2.5) * 0.02
        M.cone("PresaS", c + Vector((x, -0.165 + abs(i - 2.5) * 0.008, 0.055)), c + Vector((x, -0.17 + abs(i - 2.5) * 0.008, 0.012)), 0.01, dente, "cabeca", segs=4)
        M.cone("PresaI", c + Vector((x, -0.155 + abs(i - 2.5) * 0.008, 0.02)), c + Vector((x, -0.16 + abs(i - 2.5) * 0.008, 0.06)), 0.009, dente, "mandibula", segs=4)
    for s in (1, -1):
        M.tubo("Arcada", [c + Vector((s * 0.01, -0.125, 0.15)), c + Vector((s * 0.06, -0.125, 0.165)), c + Vector((s * 0.11, -0.07, 0.15))],
               [0.02, 0.024, 0.014], pele, "cabeca", subdiv=1)
        M.esfera("Orbita", c + Vector((s * 0.055, -0.115, 0.125)), (0.032, 0.02, 0.022), preto, "cabeca", segs=8, aneis=6)
        M.esfera("Olho", c + Vector((s * 0.055, -0.128, 0.123)), (0.016, 0.01, 0.011), olho, "cabeca", segs=8, aneis=6)
        M.membrana("Orelha", [c + Vector((s * 0.11, 0.0, 0.14)), c + Vector((s * 0.12, 0.03, 0.08)), c + Vector((s * 0.30, 0.08, 0.20)),
                              c + Vector((s * 0.20, 0.04, 0.21))], pele, "cabeca")
        M.tubo("Chifre", B.curva(c + Vector((s * 0.07, 0.0, 0.20)), c + Vector((s * 0.16, 0.10, 0.33)), c + Vector((s * 0.09, -0.06, 0.45)), 6),
               [0.034, 0.032, 0.026, 0.02, 0.012, 0.003], chifre, "cabeca", subdiv=1)
        om, ct, pu = J["ombro." + l_(s)], J["cotovelo." + l_(s)], J["pulso." + l_(s)]
        M.esfera("Ombro", om, (0.065, 0.06, 0.06), pele, "peito", semente=5)
        M.esfera("JCotovelo", ct, (0.034, 0.034, 0.034), pele, "braco." + l_(s))
        M.esfera("JJoelho", J["joelho." + l_(s)], (0.045, 0.045, 0.045), pele, "coxa." + l_(s))
        M.esfera("JAnca", J["anca." + l_(s)], (0.075, 0.07, 0.07), pele, "quadril")
        M.tubo("Braco", [om, (om + ct) / 2, ct], [0.045, 0.036, 0.03], pele, "braco." + l_(s), amplitude=0.008, escala_ruido=10, semente=6)
        M.tubo("Antebraco", [ct, (ct + pu) / 2, pu], [0.032, 0.036, 0.024], pele, "antebraco." + l_(s), amplitude=0.008, escala_ruido=10, semente=7)
        M.cone("EspCotovelo", ct, ct + Vector((0, 0.09, 0.03)), 0.018, chifre, "braco." + l_(s), segs=4)
        mao_garras(M, J, s, pele, 3, 0.15, 0.014, chifre)
        an, jo, to = J["anca." + l_(s)], J["joelho." + l_(s)], J["tornozelo." + l_(s)]
        M.tubo("Coxa", [an, (an + jo) / 2 + Vector((0, -0.02, 0)), jo], [0.07, 0.06, 0.04], pele, "coxa." + l_(s), amplitude=0.01, semente=8)
        M.tubo("Canela", [jo, (jo + to) / 2 + Vector((0, 0.03, 0)), to], [0.04, 0.035, 0.024], pele, "canela." + l_(s), amplitude=0.008, semente=9)
        pe_garras(M, J, s, pele, 3, chifre)
        # asas: três "dedos" ósseos e membrana rasgada
        raiz = pe_ + Vector((s * 0.08, 0.10, 0.10))
        d1 = pe_ + Vector((s * 0.50, 0.30, 0.45))
        d2 = pe_ + Vector((s * 0.62, 0.36, 0.10))
        d3 = pe_ + Vector((s * 0.42, 0.34, -0.18))
        cot = pe_ + Vector((s * 0.26, 0.22, 0.34))
        M.tubo("AsaBraco", [raiz, cot, d1], [0.022, 0.018, 0.006], chifre, "asa." + l_(s), subdiv=1)
        M.tubo("AsaDedo2", [cot, (cot + d2) / 2 + Vector((s * 0.04, 0, 0.03)), d2], [0.014, 0.01, 0.004], chifre, "asa." + l_(s), subdiv=1)
        M.tubo("AsaDedo3", [cot, (cot + d3) / 2 + Vector((s * 0.03, 0, 0.0)), d3], [0.014, 0.01, 0.004], chifre, "asa." + l_(s), subdiv=1)
        M.cone("AsaGarra", cot, cot + Vector((s * 0.02, -0.06, 0.09)), 0.014, chifre, "asa." + l_(s), segs=4)
        M.membrana("AsaPele", [raiz, cot, d1, (d1 + d2) / 2 + Vector((-s * 0.06, 0, 0.0)), d2, (d2 + d3) / 2 + Vector((-s * 0.07, 0, 0.02)),
                               d3, raiz + Vector((s * 0.04, 0.03, -0.2))], membr, "asa." + l_(s), amplitude=0.01, semente=10 + s)
    # cauda com ponta em lâmina
    t0, t1, t2 = q + Vector((0, 0.10, 0.0)), q + Vector((0, 0.38, -0.12)), q + Vector((0, 0.66, 0.02))
    M.esfera("JCauda", t1, (0.032, 0.032, 0.032), pele, "cauda1")
    M.tubo("Cauda1", [t0, (t0 + t1) / 2 + Vector((0, 0, -0.03)), t1], [0.05, 0.04, 0.03], pele, "cauda1", amplitude=0.006, semente=12)
    M.tubo("Cauda2", [t1, (t1 + t2) / 2 + Vector((0, 0, -0.04)), t2], [0.03, 0.022, 0.014], pele, "cauda2", amplitude=0.005, semente=13)
    M.cone("CaudaPonta", t2, t2 + Vector((0, 0.13, 0.08)), 0.035, chifre, "cauda2", segs=4)

    rig.vincular_partes(M.partes)
    base = {"coluna": (20, 0, 0), "peito": (22, 0, 0), "pescoco": (-10, 0, 0), "cabeca": (-22, 0, 0),
            "mandibula": (-18, 0, 0), "braco.L": (30, 28, 0), "antebraco.L": (70, 0, 0), "braco.R": (30, 28, 0),
            "antebraco.R": (70, 0, 0), "coxa.L": (38, 10, 0), "canela.L": (-70, 0, 0), "pe.L": (25, 0, 0),
            "coxa.R": (38, 10, 0), "canela.R": (-70, 0, 0), "pe.R": (25, 0, 0), "quadril@loc": (0, 0, -0.10),
            "asa.L": (0, 0, 0), "asa.R": (0, 0, 0), "cauda1": (0, 0, 0)}
    for nome, f in (("Parado", lambda b: anim_parado(rig, b, 1.5)), ("Andar", lambda b: anim_andar(rig, b, 0.8)),
                    ("Atacar", lambda b: anim_atacar_garras(rig, b)), ("Morrer", lambda b: anim_morrer(rig, b, 0.50))):
        f(base)
    return rig, "Parado", ACOES_PADRAO, {"altura_px": 132 if guerra else 64}


def l_(s):
    return "L" if s > 0 else "R"


# ---------------------------------------------------------------------------
# NECROMANTE (o herói)
# ---------------------------------------------------------------------------

def necromante(variante="necromante"):
    acolito = variante == "acolito"
    foice = variante == "foice"      # mesma figura, de foice de guerra em vez de cajado: estilo corpo a corpo
    aparicao = variante == "aparicao"   # espectro: manto que brilha frio, sem arma
    carcereiro = variante == "carcereiro"   # chefe do Ato 2: manto negro-violeta, correntes, cajado-gaiola com alma roxa
    B.cena_limpa()
    J = juntas(tronco=1.05, ombros=1.05)
    extras = {"cajado": (J["mao.R"] + Vector((0, -0.02, 0.03)), J["mao.R"] + Vector((0, -0.02, 0.5)), "mao.R")}
    rig = rig_humanoide("Necromante", J, extras)
    M = Montagem()
    manto = B.mat_pano("Manto", (0.055, 0.03, 0.075), (0.008, 0.005, 0.012))
    forro = B.mat_pano("Forro", (0.22, 0.02, 0.03), (0.04, 0.0, 0.01))
    if acolito:   # hábito cor de sangue seco, estola de pele clara, olhos de brasa
        manto = B.mat_pano("Habito", (0.14, 0.022, 0.02), (0.02, 0.004, 0.004))
        forro = B.mat_pano("Pele", (0.30, 0.24, 0.17), (0.08, 0.03, 0.03))
    osso = B.mat_osso()
    ouro = B.mat_metal("Bronze", (0.35, 0.22, 0.06), (0.05, 0.12, 0.08), 0.35)
    madeira = B.mat_pano("MadeiraNegra", (0.05, 0.035, 0.03), (0.01, 0.008, 0.008))
    preto = B.mat_liso("Vazio", (0.003, 0.002, 0.004), 1.0)
    olho = B.mat_emissao("OlhoAlma", (0.3, 1.0, 0.55), 35.0)
    alma = B.mat_emissao("ChamaAlma", (0.25, 1.0, 0.5), 14.0)
    if acolito:
        olho = B.mat_emissao("OlhoBrasa", (1.0, 0.25, 0.05), 30.0)
        alma = B.mat_emissao("Brasa", (1.0, 0.3, 0.05), 12.0)
    if aparicao:
        manto = B.mat_emissao("Espectro", (0.30, 0.55, 1.0), 1.6)
        forro = B.mat_emissao("EspectroForro", (0.55, 0.8, 1.0), 3.0)
        osso = B.mat_osso("OssoFrio", (0.62, 0.72, 0.85), (0.10, 0.14, 0.22))
        olho = B.mat_emissao("OlhoGelo", (0.6, 0.9, 1.0), 40.0)
        alma = B.mat_emissao("ChamaFria", (0.5, 0.8, 1.0), 14.0)

    if carcereiro:
        manto = B.mat_pano("MantoCarcere", (0.035, 0.012, 0.06), (0.004, 0.002, 0.01))
        forro = B.mat_emissao("ForroViolet", (0.45, 0.15, 0.9), 2.2)
        osso = B.mat_osso("OssoCinza", (0.50, 0.46, 0.55), (0.10, 0.08, 0.16))
        olho = B.mat_emissao("OlhoViolet", (0.7, 0.3, 1.0), 45.0)
        alma = B.mat_emissao("AlmaRoxa", (0.6, 0.25, 1.0), 18.0)

    q, co, pe, pc, c = J["quadril"], J["coluna"], J["peito"], J["pescoco"], J["cabeca"]
    if carcereiro:
        # correntes cruzadas no peito e pendendo dos pulsos, com argolas
        ferro_c = B.mat_metal("Grilhao", (0.16, 0.15, 0.17), (0.08, 0.03, 0.02), 0.5)
        M.tubo("CorrenteA", [pe + Vector((0.20, -0.02, 0.16)), pe + Vector((0.05, -0.15, 0.02)), pe + Vector((-0.12, -0.16, -0.16)), q + Vector((-0.22, -0.08, 0.05))], [0.022] * 4, ferro_c, "peito", subdiv=1, amplitude=0.012, escala_ruido=45, semente=61)
        M.tubo("CorrenteB", [pe + Vector((-0.20, -0.02, 0.16)), pe + Vector((-0.05, -0.16, 0.02)), pe + Vector((0.12, -0.17, -0.16)), q + Vector((0.22, -0.08, 0.05))], [0.022] * 4, ferro_c, "peito", subdiv=1, amplitude=0.012, escala_ruido=45, semente=62)
        for lado in ("L", "R"):
            m = J["mao." + lado]
            M.tubo("Pendente" + lado, [m + Vector((0, 0, 0.02)), m + Vector((0.02, 0.02, -0.18)), m + Vector((-0.02, 0.0, -0.36)), m + Vector((0.01, 0.03, -0.5))], [0.02, 0.02, 0.018, 0.016], ferro_c, "mao." + lado, subdiv=1, amplitude=0.012, escala_ruido=45, semente=63)
            M.esfera("Argola" + lado, m + Vector((0.01, 0.03, -0.54)), (0.05, 0.02, 0.05), ferro_c, "mao." + lado, segs=8, aneis=4)
    # saia do manto: larga e rasgada na barra, cobrindo as pernas
    saia = M.tubo("Saia", [(0, 0.02, 0.05), (0, 0.02, 0.30), (0, 0.01, q.z - 0.15), q + Vector((0, 0, 0.08))],
                  [(0.33, 0.30), (0.27, 0.24), (0.19, 0.16), (0.155, 0.125)], manto, "quadril", subdiv=3, amplitude=0.015, escala_ruido=5, semente=1)
    B.pregas(saia, 10, 0.16, q.z + 0.08, 0.7)
    M.tubo("Torso", [q + Vector((0, 0, 0.04)), co, pe, pc + Vector((0, 0, -0.02))],
           [(0.155, 0.125), (0.15, 0.12), (0.185, 0.135), (0.12, 0.09)], manto, "coluna", amplitude=0.015, escala_ruido=7, semente=2)
    # estola com forro vermelho e fecho de caveira
    M.tubo("Estola", [(-0.13, 0.06, pc.z + 0.0), (-0.12, -0.10, pc.z - 0.06), (-0.05, -0.145, pe.z + 0.02), (-0.04, -0.15, co.z - 0.05),
                      (-0.05, -0.15, q.z - 0.30)], [(0.012, 0.055)] * 5, forro, "peito", subdiv=1, amplitude=0.006, semente=4)
    M.tubo("Estola2", [(0.13, 0.06, pc.z + 0.0), (0.12, -0.10, pc.z - 0.06), (0.05, -0.145, pe.z + 0.02), (0.04, -0.15, co.z - 0.05),
                       (0.05, -0.15, q.z - 0.22)], [(0.012, 0.055)] * 5, forro, "peito", subdiv=1, amplitude=0.006, semente=5)
    # cinto de ossos com caveirinhas e frascos
    zc = q.z + 0.10
    M.tubo("Cinto", [(0.165 * math.cos(a), 0.01 + 0.135 * math.sin(a), zc) for a in [math.radians(x) for x in range(-90, 271, 40)]],
           [(0.014, 0.03)] * 10, madeira, "quadril", subdiv=1)
    for i, a in enumerate((-125, -90, -55)):
        ar = math.radians(a)
        p = Vector((0.175 * math.cos(ar), 0.01 + 0.145 * math.sin(ar), zc - 0.06))
        M.esfera("Caveirinha", p, (0.04, 0.042, 0.045), osso, "quadril", segs=8, aneis=6)
        M.esfera("CavOlhoE", p + Vector((0.014 + 0.01 * math.cos(ar), -0.034, 0.008)), (0.010, 0.008, 0.012), preto, "quadril", segs=6, aneis=4)
        M.esfera("CavOlhoD", p + Vector((-0.014 + 0.01 * math.cos(ar), -0.034, 0.008)), (0.010, 0.008, 0.012), preto, "quadril", segs=6, aneis=4)
    M.tubo("Frasco", [(0.16, -0.06, zc - 0.03), (0.165, -0.065, zc - 0.13)], [0.02, 0.028], alma, "quadril", subdiv=1)

    # capuz fundo, rosto de caveira lá dentro, coroa de espinhos de osso
    cranio(M, c + Vector((0, -0.005, -0.01)), 0.92, osso, preto, olho, dentes=True, mandibula=True)
    capuz_pts = [c + Vector((0, 0.03, -0.06)), c + Vector((0, 0.05, 0.10)), c + Vector((0, 0.02, 0.24)), c + Vector((0, -0.07, 0.30))]
    M.tubo("Capuz", capuz_pts, [(0.15, 0.13), (0.165, 0.16), (0.15, 0.15), (0.05, 0.04)], manto, "cabeca", amplitude=0.02, escala_ruido=6, semente=6)
    for s in (1, -1):
        M.tubo("CapuzAba", B.curva(c + Vector((s * 0.11, -0.03, -0.04)), c + Vector((s * 0.15, -0.13, 0.12)), c + Vector((0, -0.14, 0.29)), 6),
               [(0.03, 0.05), (0.03, 0.06), (0.03, 0.06), (0.028, 0.055), (0.025, 0.05), (0.02, 0.04)], manto, "cabeca", subdiv=1, amplitude=0.008, semente=7 + s)
    if not acolito:
        for i, a in enumerate((-60, -30, 0, 30, 60)):
            ar = math.radians(a)
            b0 = c + Vector((math.sin(ar) * 0.13, -0.11 + abs(a) * 0.0012, 0.27 - abs(a) * 0.0012))
            M.cone("CoroaEspinho", b0, b0 + Vector((math.sin(ar) * 0.07, -0.03, 0.13 - abs(a) * 0.0008)), 0.018, osso, "cabeca", segs=5)
    M.tubo("Gola", [(0.13 * math.cos(a), 0.01 + 0.11 * math.sin(a), pc.z + 0.01) for a in [math.radians(x) for x in range(-90, 271, 45)]],
           [(0.03, 0.045)] * 9, manto, "peito", subdiv=1, amplitude=0.01, semente=9)

    for s, l in ((1, "L"), (-1, "R")):
        om, ct, pu = J["ombro." + l], J["cotovelo." + l], J["pulso." + l]
        if not acolito:
            # ombreiras de costela e crânio de besta
            M.esfera("Ombreira", om + Vector((s * 0.02, 0, 0.04)), (0.115, 0.105, 0.07), osso, "peito", amplitude=0.012, escala_ruido=8, semente=10 + s)
            for k, a in enumerate((-50, 0, 50)):
                ar = math.radians(a)
                b0 = om + Vector((s * 0.06, math.sin(ar) * 0.07, 0.07))
                M.tubo("OmbCostela", B.curva(b0, b0 + Vector((s * 0.12, math.sin(ar) * 0.06, 0.10)), b0 + Vector((s * 0.06, math.sin(ar) * 0.03, 0.22)), 5),
                       [0.02, 0.018, 0.015, 0.01, 0.003], osso, "peito", subdiv=1)
        M.tubo("Manga", [om + Vector((0, 0, 0.0)), (om + ct) / 2, ct], [0.075, 0.07, 0.075], manto, "braco." + l, amplitude=0.015, escala_ruido=8, semente=12 + s)
        M.tubo("MangaLarga", [ct, (ct + pu) / 2, pu + Vector((0, 0, 0.02))], [0.075, 0.085, 0.10], manto, "antebraco." + l, amplitude=0.02, escala_ruido=8, semente=14 + s)
        M.tubo("Punho", [pu + Vector((0, 0, 0.05)), pu + Vector((0, 0, -0.01))], [0.02, 0.018], osso, "antebraco." + l, subdiv=1)
        mao_garras(M, J, s, osso, 4, 0.10, 0.010)
    if acolito:
        # correntes cruzadas no peito, adaga ritual serrilhada, incensário aceso pendurado
        ferro = B.mat_metal("Corrente", qtd=0.5)
        for sx in (1, -1):
            M.tubo("Corrente", [(sx * 0.17, 0.02, pc.z - 0.03), (sx * 0.06, -0.15, pe.z + 0.02), (-sx * 0.12, -0.15, co.z), (-sx * 0.17, 0.0, q.z + 0.12),
                                (-sx * 0.05, 0.14, co.z), (sx * 0.14, 0.11, pe.z + 0.05)], [0.014] * 6, ferro, "coluna", subdiv=1, amplitude=0.006, escala_ruido=40, semente=3)
        h = J["mao.R"] + Vector((0, -0.01, 0.03))
        M.tubo("AdagaCabo", [h + Vector((0, 0.06, 0)), h + Vector((0, -0.05, 0))], [0.014, 0.014], madeira, "mao.R", subdiv=1)
        M.tubo("AdagaLamina", [h + Vector((0, -0.05, 0)), h + Vector((0, -0.20, 0.012)), h + Vector((0, -0.34, -0.01)), h + Vector((0, -0.42, 0.0))],
               [(0.007, 0.034), (0.007, 0.04), (0.006, 0.028), (0.002, 0.004)], ferro, "mao.R", subdiv=2, amplitude=0.008, escala_ruido=30, semente=5)
        hl = J["mao.L"] + Vector((0, -0.01, 0.0))
        M.tubo("IncensarioCorrente", [hl, hl + Vector((0, 0, -0.22))], [0.005, 0.005], ferro, "mao.L", subdiv=0)
        M.esfera("Incensario", hl + Vector((0, 0, -0.29)), (0.06, 0.06, 0.075), ferro, "mao.L", segs=10, aneis=8)
        M.esfera("IncensarioBrasa", hl + Vector((0, 0, -0.27)), (0.045, 0.062, 0.03), alma, "mao.L", segs=8, aneis=6)
        M.cone("IncensarioPonta", hl + Vector((0, 0, -0.36)), hl + Vector((0, 0, -0.46)), 0.02, ferro, "mao.L", segs=5)
    else:
        # capa nas costas, rasgada
        M.membrana("Capa", [(-0.17, 0.11, pc.z - 0.03), (0.17, 0.11, pc.z - 0.03), (0.30, 0.26, q.z - 0.1), (0.24, 0.33, 0.30), (0.33, 0.36, 0.06),
                            (0.12, 0.38, 0.14), (0.0, 0.40, 0.03), (-0.14, 0.38, 0.18), (-0.30, 0.36, 0.05), (-0.25, 0.32, 0.4), (-0.30, 0.26, q.z - 0.1)],
                   manto, "peito", amplitude=0.03, semente=20)

        # cajado: haste torta, caveira com chifres no topo, chama de alma, costelas em gaiola
        h = J["mao.R"] + Vector((0, -0.02, 0.03))
        if aparicao:
            pass      # mãos vazias: ataca com rajadas de sombra
        elif foice:
            # foice de guerra: haste longa de madeira negra, lâmina larga e curva suja de sangue, contrapeso de caveira
            aco = B.mat_metal("AcoFoice", (0.20, 0.20, 0.22), (0.10, 0.02, 0.02), 0.45)
            sangue = B.mat_sangue()
            haste = [h + Vector((0, 0.0, -0.80)), h + Vector((0.01, 0.01, -0.3)), h, h + Vector((-0.012, 0, 0.50)), h + Vector((0.0, 0, 1.02))]
            M.tubo("HasteFoice", haste, [0.014, 0.02, 0.022, 0.02, 0.022], madeira, "cajado", subdiv=1, amplitude=0.006, escala_ruido=12, semente=21)
            topo = h + Vector((0, 0, 1.0))
            lam = [topo + Vector((0, 0.05, 0.0)), topo + Vector((0, -0.16, 0.10)), topo + Vector((0, -0.40, 0.06)), topo + Vector((0, -0.60, -0.10)), topo + Vector((0, -0.72, -0.32))]
            M.tubo("LaminaFoice", lam, [(0.010, 0.050), (0.009, 0.085), (0.008, 0.080), (0.006, 0.055), (0.002, 0.006)], aco, "cajado", subdiv=2, amplitude=0.008, escala_ruido=26, semente=23)
            M.tubo("FioSangue", [topo + Vector((0, -0.30, 0.0)), topo + Vector((0, -0.50, -0.07)), topo + Vector((0, -0.66, -0.24))], [(0.011, 0.03), (0.009, 0.03), (0.004, 0.012)], sangue, "cajado", subdiv=1, amplitude=0.01, escala_ruido=30, semente=24)
            cranio(M, topo + Vector((0, 0.07, -0.06)), 0.5, osso, preto, alma, osso="cajado", dentes=True, mandibula=False)
            M.tubo("AnelFoice", [(h.x + 0.03 * math.cos(a), h.y + 0.03 * math.sin(a), h.z + 0.80) for a in [math.radians(x) for x in range(0, 361, 60)]], [0.009] * 7, ouro, "cajado", subdiv=1)
            for zz in (-0.76, -0.70):
                M.cone("PontaFoice", h + Vector((0, 0, zz)), h + Vector((0, 0, zz - 0.10)), 0.02, aco, "cajado", segs=5)
        if not foice and not aparicao:
            haste = [h + Vector((0, 0.0, -0.78)), h + Vector((0.01, 0.01, -0.3)), h, h + Vector((-0.012, 0, 0.45)), h + Vector((0.0, 0, 0.92))]
            M.tubo("Haste", haste, [0.012, 0.02, 0.022, 0.02, 0.024], madeira, "cajado", subdiv=1, amplitude=0.006, escala_ruido=12, semente=21)
            topo = h + Vector((0, 0, 0.92))
            cranio(M, topo + Vector((0, 0.0, -0.02)), 0.72, osso, preto, alma, osso="cajado", dentes=True, mandibula=False, chifres=(osso, 0.9))
            for k in range(4):
                a = math.radians(45 + k * 90)
                b0 = topo + Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, -0.02))
                M.tubo("Gaiola", B.curva(b0, b0 + Vector((math.cos(a) * 0.13, math.sin(a) * 0.13, -0.10)), b0 + Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, -0.24)), 5),
                       [0.012, 0.011, 0.01, 0.008, 0.004], osso, "cajado", subdiv=1)
            M.esfera("AlmaPresa", topo + Vector((0, 0, -0.13)), (0.045, 0.045, 0.06), alma, "cajado", segs=8, aneis=6, amplitude=0.01, escala_ruido=20, semente=22)
            M.tubo("Anel", [(h.x + 0.03 * math.cos(a), h.y + 0.03 * math.sin(a), h.z + 0.62) for a in [math.radians(x) for x in range(0, 361, 60)]],
                   [0.008] * 7, ouro, "cajado", subdiv=1)
    rig.vincular_partes(M.partes)
    base = {"coluna": (5, 0, 0), "peito": (8, 0, 0), "cabeca": (4, 0, 0), "braco.R": (38, 22, 0), "antebraco.R": (48, 0, 0),
            "cajado": (86, 0, 0),
            "braco.L": (12, 12, 0), "antebraco.L": (35, 0, 0)}
    anim_parado(rig, base, 1.2)
    # o manto cobre as pernas: andar "flutuando" com balanço
    B.animar(rig, "Andar", {1: {"quadril@loc": (0, 0, 0.0), "peito": (4, 0, 6), "braco.L": (-12, 0, 0)},
                            7: {"quadril@loc": (0, 0, 0.03), "peito": (2, 3, 0)},
                            13: {"quadril@loc": (0, 0, 0.0), "peito": (4, 0, -6), "braco.L": (14, 0, 0)},
                            19: {"quadril@loc": (0, 0, 0.03), "peito": (2, -3, 0)},
                            25: {"quadril@loc": (0, 0, 0.0), "peito": (4, 0, 6), "braco.L": (-12, 0, 0)}}, base)
    anim_conjurar(rig, base)
    if foice:
        anim_atacar_arma(rig, base)
    anim_morrer(rig, base)
    acoes = [dict(a) for a in ACOES_PADRAO if foice or a["nome"] != "atacar"]
    acoes.insert(2, {"nome": "conjurar", "acao_blender": "Conjurar", "lista_frames": [1, 4, 7, 11, 15, 19], "fps": 12, "loop": False})
    return rig, "Parado", acoes, {"altura_px": 96 if acolito else 100 if aparicao else 150 if carcereiro else 104}


def acolito_caido():
    return necromante("acolito")


# ---------------------------------------------------------------------------
# A ABADESSA COSTURADA (chefe do Ato 1)
# ---------------------------------------------------------------------------

def abadessa():
    B.cena_limpa()
    J = juntas(tronco=1.2, ombros=1.25, bracos=1.2)
    rig = rig_humanoide("Abadessa", J)
    M = Montagem()
    habito = B.mat_pano("HabitoNegro", (0.022, 0.02, 0.028), (0.004, 0.004, 0.006))
    linho = B.mat_pano("LinhoSujo", (0.34, 0.30, 0.24), (0.10, 0.03, 0.03))
    carne = B.mat_carne("CarneCosturada", ((0.22, 0.19, 0.15), (0.33, 0.27, 0.21), (0.16, 0.09, 0.11)), sangue_qtd=0.38)
    carne2 = B.mat_carne("RetalhoRoxo", ((0.14, 0.09, 0.14), (0.20, 0.13, 0.16), (0.08, 0.05, 0.09)), sangue_qtd=0.3)
    fio = B.mat_liso("FioNegro", (0.006, 0.004, 0.004), 0.7)
    ferro = B.mat_metal("FerroAbadessa", qtd=0.45)
    osso = B.mat_osso()
    preto = B.mat_liso("Vazio", (0.003, 0.002, 0.003), 1.0)
    olho = B.mat_emissao("OlhoSangue", (1.0, 0.08, 0.05), 30.0)
    sangue = B.mat_sangue()
    q, co, pe, pc, c = J["quadril"], J["coluna"], J["peito"], J["pescoco"], J["cabeca"]

    saia = M.tubo("Saia", [(0, 0.02, 0.05), (0, 0.02, 0.32), (0, 0.01, q.z - 0.12), q + Vector((0, 0, 0.1))],
                  [(0.42, 0.38), (0.34, 0.30), (0.24, 0.20), (0.20, 0.16)], habito, "quadril", subdiv=3, amplitude=0.02, escala_ruido=5, semente=2)
    B.pregas(saia, 12, 0.15, q.z + 0.1, 1.9)
    M.membrana("Avental", [(-0.15, -0.17, q.z + 0.08), (0.15, -0.17, q.z + 0.08), (0.22, -0.30, 0.5), (0.10, -0.36, 0.22), (0.0, -0.34, 0.34),
                           (-0.12, -0.36, 0.18), (-0.22, -0.30, 0.5)], linho, "quadril", amplitude=0.025, semente=3)
    M.esfera("AventalSangue", (0.02, -0.265, 0.60), (0.12, 0.025, 0.22), sangue, "quadril", segs=12, aneis=8, amplitude=0.05, escala_ruido=14, semente=4)
    # tronco de retalhos de carne costurados
    M.tubo("Ventre", [q + Vector((0, 0, 0.05)), co + Vector((0, -0.03, 0.02)), (co + pe) / 2 + Vector((0, -0.04, 0))],
           [(0.20, 0.16), (0.24, 0.20), (0.23, 0.19)], carne, "coluna", amplitude=0.03, escala_ruido=5, semente=5)
    M.tubo("Torax", [(co + pe) / 2, pe + Vector((0, 0, 0.05)), pc + Vector((0, 0, -0.02))],
           [(0.23, 0.18), (0.27, 0.19), (0.15, 0.11)], carne2, "peito", amplitude=0.025, escala_ruido=5, semente=6)
    M.esfera("Retalho1", (0.13, -0.15, co.z + 0.12), (0.12, 0.06, 0.14), carne2, "coluna", amplitude=0.012, semente=7)
    M.esfera("Retalho2", (-0.14, -0.13, pe.z + 0.05), (0.11, 0.06, 0.12), carne, "peito", amplitude=0.012, semente=8)
    M.esfera("Corcunda", (0.07, 0.10, pe.z + 0.08), (0.15, 0.10, 0.14), carne, "peito", amplitude=0.03, escala_ruido=6, semente=9)

    def costura(pts, osso_, passo=0.06):
        """Pontos de costura: grampos de fio cruzando uma cicatriz."""
        pts = [Vector(p) for p in pts]
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            n = max(1, int((b - a).length / passo))
            lado = (b - a).cross(Vector((0, -1, 0))).normalized() * 0.03
            for k in range(n):
                m_ = a.lerp(b, (k + 0.5) / n)
                M.tubo("Ponto", [m_ - lado, m_ + Vector((0, -0.018, 0)), m_ + lado], [0.006, 0.007, 0.006], fio, osso_, subdiv=0)
    costura([(0.02, -0.215, co.z - 0.04), (0.03, -0.235, co.z + 0.12), (-0.02, -0.225, pe.z - 0.02)], "coluna")
    costura([(-0.2, -0.16, pe.z + 0.12), (-0.05, -0.2, pe.z + 0.08), (0.12, -0.19, pe.z + 0.14), (0.24, -0.12, pe.z + 0.1)], "peito")
    costura([(0.2, -0.13, co.z + 0.0), (0.22, -0.12, co.z + 0.2)], "coluna")

    # cabeça: rosto costurado, boca selada, touca de linho e véu negro, auréola de pregos
    M.tubo("Pescoco", [pc + Vector((0, 0, -0.03)), c + Vector((0, -0.02, 0.04))], [0.075, 0.06], carne, "pescoco", amplitude=0.01, semente=10)
    M.esfera("Rosto", c + Vector((0, -0.03, 0.12)), (0.105, 0.12, 0.13), carne, "cabeca", amplitude=0.012, escala_ruido=8, semente=11)
    M.esfera("Queixo", c + Vector((0, -0.07, 0.03)), (0.07, 0.07, 0.06), carne2, "mandibula", amplitude=0.008, semente=12)
    for sx in (1, -1):
        M.esfera("Orbita", c + Vector((sx * 0.045, -0.135, 0.145)), (0.03, 0.02, 0.024), preto, "cabeca", segs=8, aneis=6)
        M.esfera("Olho", c + Vector((sx * 0.045, -0.15, 0.143)), (0.014, 0.009, 0.012), olho, "cabeca", segs=8, aneis=6)
    for i in range(5):
        x = (i - 2) * 0.022
        M.tubo("BocaCosturada", [c + Vector((x, -0.142, 0.075)), c + Vector((x + 0.006, -0.152, 0.05)), c + Vector((x, -0.138, 0.028))], [0.005, 0.006, 0.005], fio, "cabeca", subdiv=0)
    costura([c + Vector((-0.02, -0.148, 0.2)), c + Vector((0.03, -0.15, 0.12)), c + Vector((0.07, -0.12, 0.03))], "cabeca", 0.04)
    M.tubo("Touca", [c + Vector((0, 0.03, -0.04)), c + Vector((0, 0.04, 0.12)), c + Vector((0, 0.02, 0.26)), c + Vector((0, -0.03, 0.31))],
           [(0.14, 0.12), (0.15, 0.15), (0.14, 0.14), (0.05, 0.05)], linho, "cabeca", amplitude=0.015, escala_ruido=7, semente=13)
    M.esfera("ToucaTras", c + Vector((0, 0.035, 0.14)), (0.14, 0.135, 0.155), linho, "cabeca", amplitude=0.01, semente=21)
    veu = M.tubo("Veu", [c + Vector((0, 0.06, 0.30)), c + Vector((0, 0.13, 0.12)), (0, 0.20, pe.z), (0, 0.27, q.z), (0, 0.33, 0.42)],
                 [(0.13, 0.05), (0.19, 0.06), (0.30, 0.07), (0.36, 0.06), (0.42, 0.04)], habito, "peito", subdiv=2, amplitude=0.03, escala_ruido=5, semente=14)
    tc = c + Vector((0, 0.02, 0.34))
    M.tubo("Aureola", [(tc.x + 0.17 * math.cos(a), tc.y + 0.06 * math.sin(a) + 0.06, tc.z + 0.17 * math.sin(a) * 0.0 + 0.02 * math.cos(a * 2)) for a in
                       [math.radians(x) for x in range(0, 361, 30)]], [0.013] * 13, ferro, "cabeca", subdiv=1)
    for k in range(9):
        a = math.radians(-10 + k * 25)
        b0 = Vector((tc.x + 0.17 * math.cos(a), tc.y + 0.06, tc.z + 0.03 * abs(math.sin(a))))
        M.cone("Prego", b0 + Vector((0, 0, -0.03)), b0 + Vector((math.cos(a) * 0.05, 0, 0.17)), 0.012, ferro, "cabeca", segs=4)
    # rosário de crânios
    for k in range(9):
        a = math.radians(200 + k * 17.5)
        p = Vector((0.2 * math.cos(a), -0.12 + 0.02 * math.sin(a * 2), pc.z + 0.02 + 0.22 * math.sin(a) + 0.0))
        M.esfera("ContaCranio", p, (0.035, 0.035, 0.038), osso, "peito", segs=8, aneis=6)

    for s_, l in ((1, "L"), (-1, "R")):
        om, ct, pu = J["ombro." + l], J["cotovelo." + l], J["pulso." + l]
        M.esfera("Ombro", om, (0.11, 0.10, 0.10), carne2 if s_ > 0 else carne, "peito", amplitude=0.015, semente=15 + s_)
        M.tubo("Braco", [om, (om + ct) / 2, ct], [0.08, 0.07, 0.055], carne if s_ > 0 else carne2, "braco." + l, amplitude=0.014, escala_ruido=8, semente=16 + s_)
        M.esfera("JCotovelo", ct, (0.058, 0.058, 0.058), carne, "braco." + l)
        M.tubo("Antebraco", [ct, (ct + pu) / 2, pu], [0.055, 0.06, 0.04], carne2 if s_ > 0 else carne, "antebraco." + l, amplitude=0.012, escala_ruido=9, semente=18 + s_)
        costura([ct + Vector((s_ * 0.05, -0.03, 0.03)), ct + Vector((s_ * 0.05, -0.03, -0.03))], "braco." + l, 0.03)
        mao_garras(M, J, s_, carne, 4, 0.16, 0.015, osso)
        # segundo par de braços, costurado na cintura, em prece
        b0 = Vector((s_ * 0.22, -0.02, co.z + 0.06))
        M.tubo("BracoPrece", [b0, Vector((s_ * 0.27, -0.16, co.z - 0.06)), Vector((s_ * 0.02, -0.33, co.z + 0.10))], [0.05, 0.04, 0.03],
               carne2, "coluna", amplitude=0.01, semente=20 + s_)
        for k in range(3):
            base_d = Vector((s_ * 0.02, -0.33, co.z + 0.10))
            M.tubo("DedoPrece", [base_d, base_d + Vector((s_ * (0.0 + k * 0.012), -0.02, 0.06)), base_d + Vector((s_ * 0.005, -0.01, 0.13 + k * 0.01))],
                   [0.012, 0.01, 0.003], osso, "coluna", subdiv=1)
    # agulha colossal com linha vermelha, na mão direita
    h = J["mao.R"] + Vector((0, -0.01, 0.03))
    M.tubo("Agulha", [h + Vector((0, 0.35, 0.02)), h, h + Vector((0, -0.6, 0.0)), h + Vector((0, -1.15, -0.01))], [0.02, 0.022, 0.018, 0.002], ferro, "mao.R", subdiv=1)
    M.tubo("AgulhaOlho", [(h.x + 0.0, h.y + 0.35 + 0.05 * math.cos(a), h.z + 0.02 + 0.05 * math.sin(a)) for a in [math.radians(x) for x in range(0, 361, 45)]],
           [0.01] * 9, ferro, "mao.R", subdiv=1)
    M.tubo("Linha", [h + Vector((0, 0.38, 0.02)), h + Vector((0.05, 0.42, -0.25)), h + Vector((-0.04, 0.36, -0.55)), h + Vector((0.03, 0.40, -0.80))],
           [0.008, 0.008, 0.008, 0.005], sangue, "mao.R", subdiv=1)

    rig.vincular_partes(M.partes)
    base = {"coluna": (12, 4, 0), "peito": (14, -3, 0), "pescoco": (6, 0, 0), "cabeca": (-4, 8, 6), "braco.R": (30, 22, 0),
            "antebraco.R": (40, 0, 0), "braco.L": (20, 26, 0), "antebraco.L": (55, 0, 0)}
    anim_parado(rig, base, 1.4)
    B.animar(rig, "Andar", {1: {"peito": (4, 5, 6), "braco.L": (-10, 0, 0)}, 7: {"quadril@loc": (0, 0, 0.03)},
                            13: {"peito": (4, -5, -6), "braco.L": (12, 0, 0)}, 19: {"quadril@loc": (0, 0, 0.03)},
                            25: {"peito": (4, 5, 6), "braco.L": (-10, 0, 0)}}, base)
    anim_atacar_arma(rig, base)
    anim_morrer(rig, base)
    return rig, "Parado", ACOES_PADRAO, {"altura_px": 150}


# ---------------------------------------------------------------------------
# CÃO DE CRIPTA (quadrúpede)
# ---------------------------------------------------------------------------

def cao_de_cripta(variante="cao"):
    res = variante == "res"   # a Rês Profana do nível secreto: couro pálido malhado, chifres largos, olhos de brasa
    B.cena_limpa()
    V = Vector
    d = {
        "quadril": (V((0, 0.36, 0.62)), V((0, 0.10, 0.66)), None),
        "coluna": (V((0, 0.10, 0.66)), V((0, -0.20, 0.68)), "quadril"),
        "peito": (V((0, -0.20, 0.68)), V((0, -0.42, 0.70)), "coluna"),
        "pescoco": (V((0, -0.42, 0.70)), V((0, -0.58, 0.82)), "peito"),
        "cabeca": (V((0, -0.58, 0.82)), V((0, -0.84, 0.84)), "pescoco"),
        "mandibula": (V((0, -0.61, 0.775)), V((0, -0.88, 0.745)), "cabeca"),
        "cauda1": (V((0, 0.38, 0.64)), V((0, 0.60, 0.58)), "quadril"),
        "cauda2": (V((0, 0.60, 0.58)), V((0, 0.82, 0.44)), "cauda1"),
    }
    P = {}
    for sx, l in ((1, "L"), (-1, "R")):
        P["om" + l], P["ct" + l], P["pu" + l], P["de" + l] = V((sx * 0.13, -0.36, 0.62)), V((sx * 0.14, -0.32, 0.34)), V((sx * 0.14, -0.37, 0.09)), V((sx * 0.14, -0.47, 0.02))
        P["an" + l], P["jo" + l], P["to" + l], P["pe" + l] = V((sx * 0.12, 0.34, 0.58)), V((sx * 0.13, 0.24, 0.33)), V((sx * 0.13, 0.40, 0.15)), V((sx * 0.13, 0.31, 0.02))
        d["braco." + l] = (P["om" + l], P["ct" + l], "peito")
        d["antebraco." + l] = (P["ct" + l], P["pu" + l], "braco." + l)
        d["mao." + l] = (P["pu" + l], P["de" + l], "antebraco." + l)
        d["coxa." + l] = (P["an" + l], P["jo" + l], "quadril")
        d["canela." + l] = (P["jo" + l], P["to" + l], "coxa." + l)
        d["pe." + l] = (P["to" + l], P["pe" + l], "canela." + l)
    rig = B.Rig("CaoDeCripta")
    rig.construir(d)
    M = Montagem()
    if res:
        carne = B.mat_carne("CouroMalhado", ((0.42, 0.38, 0.33), (0.62, 0.58, 0.52), (0.05, 0.04, 0.04)), sangue_qtd=0.22)
    else:
        carne = B.mat_carne("CouroPodre", ((0.07, 0.06, 0.06), (0.14, 0.11, 0.10), (0.05, 0.03, 0.05)), sangue_qtd=0.36)
    viva = B.mat_carne("CarneViva", ((0.09, 0.012, 0.012), (0.15, 0.035, 0.025), (0.045, 0.005, 0.008)), sangue=(0.06, 0.002, 0.004), sangue_qtd=0.5)
    osso = B.mat_osso()
    ferro = B.mat_metal("Coleira", qtd=0.5)
    preto = B.mat_liso("Vazio", (0.003, 0.002, 0.003), 1.0)
    olho = B.mat_emissao("OlhoFatuo", (1.0, 0.25, 0.05) if res else (0.75, 1.0, 0.2), 30.0)

    # tronco magro, de peito fundo e cintura afilada
    M.tubo("Traseiro", [(0, 0.42, 0.60), (0, 0.30, 0.63), (0, 0.10, 0.65), (0, -0.04, 0.66)], [(0.07, 0.08), (0.115, 0.14), (0.10, 0.12), (0.10, 0.125)],
           carne, "quadril", amplitude=0.012, escala_ruido=9, semente=1)
    M.tubo("Lombo", [(0, 0.12, 0.65), (0, -0.05, 0.66), (0, -0.22, 0.67)], [(0.10, 0.12), (0.115, 0.15), (0.14, 0.19)], carne, "coluna", amplitude=0.012, escala_ruido=9, semente=2)
    M.tubo("Peito", [(0, -0.18, 0.67), (0, -0.32, 0.67), (0, -0.46, 0.70)], [(0.14, 0.19), (0.15, 0.20), (0.10, 0.12)], carne, "peito", amplitude=0.012, escala_ruido=9, semente=3)
    # flanco esquerdo esfolado: carne viva e costelas à mostra
    M.esfera("Esfolado", (0.095, -0.12, 0.66), (0.045, 0.17, 0.11), viva, "coluna", amplitude=0.01, escala_ruido=14, semente=4)
    for i in range(5):
        y = -0.30 + i * 0.075
        k = 1.0 - i * 0.07
        for sx in (1, -1):
            if sx < 0 and i % 2:
                continue
            pts = [(sx * 0.03, y, 0.67 + 0.19 * k), (sx * 0.14 * k, y + 0.01, 0.67 + 0.13 * k), (sx * 0.185 * k, y + 0.02, 0.66), (sx * 0.15 * k, y + 0.03, 0.66 - 0.14 * k), (sx * 0.04, y + 0.03, 0.66 - 0.2 * k)]
            M.tubo("Costela", pts, [0.014, 0.013, 0.012, 0.011, 0.007], osso, "peito" if y < -0.2 else "coluna", subdiv=1)
    for i in range(9):   # crista de vértebras
        y = 0.36 - i * 0.095
        z = 0.775 + 0.085 * math.sin(max(0.0, min(1.0, i / 8)) * 3.14) * 0.6 + (0.06 if i > 5 else 0)
        M.cone("Crista", (0, y, z - 0.03), (0, y + 0.05, z + 0.08), 0.022, osso, "quadril" if y > 0.1 else "coluna" if y > -0.2 else "peito", segs=4)
    # pescoço, coleira de ferro com espinhos e corrente partida
    M.tubo("Pescoco", [(0, -0.40, 0.70), (0, -0.50, 0.77), (0, -0.60, 0.83)], [(0.10, 0.11), (0.085, 0.09), (0.075, 0.08)], carne, "pescoco", amplitude=0.01, semente=5)
    cc = V((0, -0.50, 0.77))
    anel = [(cc.x + 0.105 * math.cos(a), cc.y + 0.05 * math.sin(a) * 0.3 - 0.0, cc.z + 0.115 * math.sin(a)) for a in [math.radians(x) for x in range(0, 361, 36)]]
    M.tubo("Coleira", anel, [(0.012, 0.03)] * 11, ferro, "pescoco", subdiv=1)
    for a in (20, 70, 110, 160):
        ar = math.radians(a)
        b0 = V((cc.x + 0.105 * math.cos(ar), cc.y, cc.z + 0.115 * math.sin(ar)))
        M.cone("EspinhoColeira", b0, b0 + V((math.cos(ar) * 0.08, 0, math.sin(ar) * 0.08)), 0.016, ferro, "pescoco", segs=4)
    M.tubo("Corrente", [cc + V((0, -0.01, -0.115)), cc + V((0.02, 0.0, -0.25)), cc + V((-0.02, 0.03, -0.38))], [0.014, 0.014, 0.012], ferro, "pescoco", subdiv=1, amplitude=0.008, escala_ruido=40, semente=6)
    if res:
        # barriga pesada e chifres largos, curvados para a frente
        M.esfera("Barriga", (0, -0.06, 0.58), (0.17, 0.30, 0.15), carne, "coluna", amplitude=0.012, escala_ruido=8, semente=31)
        for sx in (1, -1):
            M.tubo("Chifre", [(sx * 0.06, -0.63, 0.91), (sx * 0.17, -0.62, 0.97), (sx * 0.25, -0.68, 1.03), (sx * 0.27, -0.78, 1.07)], [0.034, 0.028, 0.02, 0.006], osso, "cabeca", subdiv=1)
            M.esfera("Orelha", (sx * 0.10, -0.60, 0.87), (0.05, 0.025, 0.03), carne, "cabeca", segs=6, aneis=4)
    # cabeça: crânio de lobo meio descarnado
    M.esfera("Cranio", (0, -0.665, 0.855), (0.085, 0.105, 0.075), osso, "cabeca", amplitude=0.006, semente=7)
    M.tubo("Focinho", [(0, -0.72, 0.84), (0, -0.84, 0.815), (0, -0.93, 0.80)], [(0.058, 0.045), (0.042, 0.034), (0.03, 0.024)], osso, "cabeca", subdiv=1)
    M.esfera("PeleTesta", (-0.02, -0.64, 0.885), (0.075, 0.09, 0.05), carne, "cabeca", amplitude=0.01, escala_ruido=10, semente=8)
    M.esfera("Nariz", (0, -0.945, 0.805), (0.022, 0.018, 0.018), preto, "cabeca", segs=6, aneis=4)
    for sx in (1, -1):
        M.esfera("Orbita", (sx * 0.05, -0.735, 0.865), (0.026, 0.024, 0.022), preto, "cabeca", segs=8, aneis=6)
        M.esfera("Olho", (sx * 0.054, -0.748, 0.866), (0.012, 0.01, 0.011), olho, "cabeca", segs=8, aneis=6)
        M.membrana("Orelha", [(sx * 0.05, -0.60, 0.91), (sx * 0.09, -0.62, 0.90), (sx * 0.13, -0.55, 1.02), (sx * 0.08, -0.56, 0.98)], carne, "cabeca")
        for k in range(5):
            y = -0.76 - k * 0.04
            comp = 0.05 if k in (3,) else 0.028
            M.cone("DenteS", (sx * (0.045 - k * 0.006), y, 0.80 - k * 0.004), (sx * (0.045 - k * 0.006), y - 0.004, 0.80 - k * 0.004 - comp), 0.008, osso, "cabeca", segs=4)
            M.cone("DenteI", (sx * (0.04 - k * 0.006), y + 0.01, 0.765 - k * 0.004), (sx * (0.04 - k * 0.006), y + 0.006, 0.765 - k * 0.004 + comp * 0.9), 0.007, osso, "mandibula", segs=4)
    M.tubo("Mandibula", [(-0.06, -0.62, 0.80), (-0.045, -0.74, 0.762), (0, -0.90, 0.745), (0.045, -0.74, 0.762), (0.06, -0.62, 0.80)],
           [0.016, 0.016, 0.014, 0.016, 0.016], osso, "mandibula", subdiv=1)
    M.tubo("Lingua", [(0, -0.70, 0.775), (0.01, -0.84, 0.76), (0.03, -0.90, 0.70)], [(0.02, 0.008), (0.02, 0.008), (0.008, 0.004)], viva, "mandibula", subdiv=1)
    # pernas finas e nodosas, garras de osso
    for sx, l in ((1, "L"), (-1, "R")):
        om, ct, pu, de = P["om" + l], P["ct" + l], P["pu" + l], P["de" + l]
        M.esfera("Paleta", om + V((0, 0, 0.02)), (0.07, 0.10, 0.12), carne, "peito", amplitude=0.01, semente=10)
        M.tubo("Braco", [om, (om + ct) / 2, ct], [0.055, 0.045, 0.034], carne, "braco." + l, amplitude=0.008, escala_ruido=12, semente=11)
        M.esfera("JCotovelo", ct, (0.036, 0.036, 0.036), carne, "braco." + l)
        M.tubo("Antebraco", [ct, (ct + pu) / 2, pu], [0.032, 0.026, 0.024], carne if sx > 0 else osso, "antebraco." + l, amplitude=0.004, semente=12)
        M.tubo("Carpo", [pu, de + V((0, 0.03, 0.03))], [0.024, 0.024], carne if sx > 0 else osso, "mao." + l, subdiv=1)
        an, jo, to, pe = P["an" + l], P["jo" + l], P["to" + l], P["pe" + l]
        M.esfera("Anca", an + V((0, 0, 0.0)), (0.075, 0.11, 0.12), carne, "quadril", amplitude=0.01, semente=13)
        M.tubo("Coxa", [an, (an + jo) / 2 + V((0, -0.02, 0)), jo], [0.065, 0.055, 0.036], carne, "coxa." + l, amplitude=0.008, escala_ruido=12, semente=14)
        M.esfera("JJoelho", jo, (0.038, 0.038, 0.038), carne, "coxa." + l)
        M.tubo("Canela", [jo, (jo + to) / 2, to], [0.034, 0.026, 0.022], carne, "canela." + l, amplitude=0.004, semente=15)
        M.tubo("Metatarso", [to, pe + V((0, 0.02, 0.03))], [0.022, 0.022], osso if sx > 0 else carne, "pe." + l, subdiv=1)
        for base_p, bone in ((de, "mao." + l), (pe, "pe." + l)):
            M.esfera("Pata", base_p + V((0, 0.02, 0.02)), (0.04, 0.05, 0.03), carne, bone, segs=8, aneis=6)
            for k in (-1, 0, 1):
                M.cone("Garra", base_p + V((k * 0.022, -0.02, 0.02)), base_p + V((k * 0.03, -0.09, -0.015)), 0.011, osso, bone, segs=4)
    # cauda: vértebras nuas
    for i in range(8):
        t = i / 7
        p0 = V((0, 0.40, 0.63)).lerp(V((0, 0.60, 0.58)), min(1, t * 2)) if t < 0.5 else V((0, 0.60, 0.58)).lerp(V((0, 0.82, 0.44)), (t - 0.5) * 2)
        M.esfera("VertCauda", p0, (0.03 - t * 0.016, 0.034 - t * 0.014, 0.03 - t * 0.016), osso, "cauda1" if t < 0.5 else "cauda2", segs=8, aneis=6)
    M.cone("PontaCauda", (0, 0.82, 0.44), (0, 0.93, 0.36), 0.014, osso, "cauda2", segs=4)

    rig.vincular_partes(M.partes)
    base = {"pescoco": (10, 0, 0), "cabeca": (8, 0, 0), "mandibula": (10, 0, 0)}
    B.animar(rig, "Parado", {1: {}, 13: {"peito": (3, 0, 0), "cabeca": (-6, 6, 0), "mandibula": (8, 0, 0), "cauda1": (0, 10, 0), "quadril@loc": (0, 0, -0.012)}, 25: {}}, base)
    estica = {"braco.L": (48, 0, 0), "antebraco.L": (-10, 0, 0), "braco.R": (38, 0, 0), "antebraco.R": (-5, 0, 0),
              "coxa.L": (-42, 0, 0), "canela.L": (-20, 0, 0), "coxa.R": (-34, 0, 0), "canela.R": (-15, 0, 0),
              "coluna": (-6, 0, 0), "cabeca": (-8, 0, 0), "cauda1": (-20, 0, 0), "quadril@loc": (0, 0, 0.05)}
    junta = {"braco.L": (-38, 0, 0), "antebraco.L": (55, 0, 0), "braco.R": (-30, 0, 0), "antebraco.R": (45, 0, 0),
             "coxa.L": (50, 0, 0), "canela.L": (-45, 0, 0), "coxa.R": (42, 0, 0), "canela.R": (-40, 0, 0),
             "coluna": (10, 0, 0), "peito": (8, 0, 0), "cabeca": (6, 0, 0), "cauda1": (15, 0, 0), "quadril@loc": (0, 0, -0.03)}
    B.animar(rig, "Andar", {1: estica, 5: {"quadril@loc": (0, 0, 0.02)}, 9: junta, 13: {"quadril@loc": (0, 0, 0.0)}, 17: estica}, base)
    B.animar(rig, "Atacar", {
        1: {},
        5: {"quadril@loc": (0, 0.08, -0.10), "braco.L": (-20, 0, 0), "antebraco.L": (40, 0, 0), "braco.R": (-20, 0, 0), "antebraco.R": (40, 0, 0),
            "coxa.L": (30, 0, 0), "canela.L": (-50, 0, 0), "coxa.R": (30, 0, 0), "canela.R": (-50, 0, 0), "pescoco": (-15, 0, 0), "cabeca": (-20, 0, 0), "mandibula": (40, 0, 0)},
        9: {"quadril@loc": (0, -0.38, 0.10), "braco.L": (55, 0, 0), "braco.R": (45, 0, 0), "coxa.L": (-45, 0, 0), "coxa.R": (-40, 0, 0),
            "pescoco": (-5, 0, 0), "cabeca": (-5, 0, 0), "mandibula": (48, 0, 0), "cauda1": (-25, 0, 0)},
        11: {"quadril@loc": (0, -0.40, 0.02), "braco.L": (30, 0, 0), "braco.R": (25, 0, 0), "coxa.L": (-30, 0, 0), "coxa.R": (-25, 0, 0),
             "pescoco": (12, 0, 0), "cabeca": (15, 12, 0), "mandibula": (-8, 0, 0)},
        14: {"quadril@loc": (0, -0.30, 0.0), "cabeca": (10, -14, 0), "mandibula": (-8, 0, 0)},
        20: {},
    }, base)
    B.animar(rig, "Morrer", {
        1: {},
        4: {"quadril@loc": (0, 0.08, 0.06), "pescoco": (-25, 0, 0), "cabeca": (-25, 0, 0), "mandibula": (45, 0, 0), "braco.L": (30, 0, 0), "braco.R": (30, 0, 0)},
        10: {"quadril": (0, 0, 55), "quadril@loc": (0.05, 0.08, -0.25), "braco.L": (-20, 0, 0), "antebraco.L": (60, 0, 0), "braco.R": (10, 0, 0),
             "coxa.L": (30, 0, 0), "canela.L": (-60, 0, 0), "coxa.R": (10, 0, 0), "cabeca": (15, 0, 0), "mandibula": (30, 0, 0)},
        16: {"quadril": (0, 0, 88), "quadril@loc": (0.10, 0.08, -0.47), "braco.L": (35, 0, 0), "antebraco.L": (20, 0, 0), "braco.R": (15, 0, 0), "antebraco.R": (35, 0, 0),
             "coxa.L": (-25, 0, 0), "canela.L": (-20, 0, 0), "coxa.R": (-5, 0, 0), "canela.R": (-40, 0, 0), "pescoco": (18, 0, 0), "cabeca": (20, 0, 0),
             "mandibula": (35, 0, 0), "cauda1": (30, 0, 0)},
        22: {"quadril": (0, 0, 90), "quadril@loc": (0.10, 0.08, -0.48), "braco.L": (40, 0, 0), "antebraco.L": (15, 0, 0), "braco.R": (18, 0, 0), "antebraco.R": (30, 0, 0),
             "coxa.L": (-30, 0, 0), "canela.L": (-15, 0, 0), "coxa.R": (-8, 0, 0), "canela.R": (-35, 0, 0), "pescoco": (22, 0, 0), "cabeca": (24, 0, 0),
             "mandibula": (38, 0, 0), "cauda1": (35, 0, 0)},
    }, base)
    acoes = [
        {"nome": "parado", "acao_blender": "Parado", "frames": 4, "fps": 6, "loop": True},
        {"nome": "andar", "acao_blender": "Andar", "lista_frames": [1, 3, 5, 7, 9, 11, 13, 15], "fps": 16, "loop": True},
        {"nome": "atacar", "acao_blender": "Atacar", "lista_frames": [1, 4, 6, 9, 11, 14], "fps": 12, "loop": False},
        {"nome": "morrer", "acao_blender": "Morrer", "lista_frames": [1, 4, 7, 10, 13, 16, 22], "fps": 10, "loop": False},
    ]
    return rig, "Parado", acoes, {"altura_px": 68 if res else 54}


MONSTROS = {"esqueleto": esqueleto, "zumbi": zumbi, "diabrete": diabrete, "necromante": necromante,
            "arqueiroossudo": arqueiro_ossudo, "acolitocaido": acolito_caido,
            "abadessacosturada": abadessa, "caodecripta": cao_de_cripta, "esqueleto_elite": esqueleto_elite,
            "necromante_foice": lambda: necromante("foice"),
            "carnical": lambda: zumbi("carnical"), "diabodeguerra": lambda: diabrete("guerra"),
            "aparicao": lambda: necromante("aparicao"), "colossodeossos": lambda: esqueleto("colosso"), "resprofana": lambda: cao_de_cripta("res"), "carcereirodealmas": lambda: necromante("carcereiro")}
