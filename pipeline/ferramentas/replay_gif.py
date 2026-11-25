"""
Desenha um replay real da simulação (gerado por `dotnet run --project Replay`) usando os sprites
do pipeline e o chão da arena. É a prévia "do jogo rodando" fora da Unity.

Uso: python3 ferramentas/replay_gif.py replay.txt saida.mp4 [tick_ini] [tick_fim]
Precisa de Pillow e numpy; para .mp4 precisa do ffmpeg no PATH (senão, use .gif).
"""
import json, math, os, random, subprocess, sys
from PIL import Image, ImageDraw, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SUBST = {}       # espécie -> sprite substituto, para espécies ainda sem modelo próprio
ACAO = {"Parado": "parado", "Andando": "andar", "Atacando": "atacar", "Conjurando": "conjurar", "Morrendo": "morrer"}
COR_MORTE = {"Queimado": (20, 14, 12), "Congelado": (120, 170, 210), "Putrefato": (60, 90, 25),
             "Eletrizado": (40, 40, 60), "Oco": (50, 30, 80)}


class Monstro:
    def __init__(self, nome):
        p = os.path.join(RAIZ, "saida", nome)
        self.meta = json.load(open(os.path.join(p, nome + ".json")))
        self.W, self.H = self.meta["largura_frame"], self.meta["altura_frame"]
        self.pivo = self.meta["pivo_px"]
        self.acoes = {a["nome"]: (a, Image.open(os.path.join(p, a["arquivo"])).convert("RGBA")) for a in self.meta["acoes"]}

    def frame(self, acao, direcao, tempo):
        if acao not in self.acoes:
            acao = "atacar" if acao == "conjurar" and "atacar" in self.acoes else \
                   "conjurar" if acao == "atacar" and "conjurar" in self.acoes else "parado"
        a, img = self.acoes[acao]
        f = int(tempo * a["fps"])
        f = f % a["frames"] if a["loop"] else min(f, a["frames"] - 1)
        return img.crop((f * self.W, direcao * self.H, (f + 1) * self.W, (direcao + 1) * self.H))


def direcao(dx, dy):
    if abs(dx) + abs(dy) < 1e-6:
        return 0
    ang = math.degrees(math.atan2(dx, -dy)) % 360
    return int(round(ang / 45.0)) % 8


def ler(caminho):
    ticks, atual, arena = [], None, None
    for ln in open(caminho):
        p = ln.split()
        if p[0] == "ARENA":
            arena = (float(p[1]), float(p[2]), int(p[3]))
        elif p[0] == "T":
            atual = {"t": int(p[1]), "andar": int(p[2]), "E": [], "O": [], "C": [], "V": [], "P": []}
            ticks.append(atual)
        else:
            atual[p[0]].append(p[1:])
    return arena, ticks


def main():
    arq, saida = sys.argv[1], sys.argv[2]
    arena, ticks = ler(arq)
    t0 = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    t1 = int(sys.argv[4]) if len(sys.argv) > 4 else len(ticks)
    tps = arena[2]
    aj = json.load(open(os.path.join(RAIZ, "saida", "arena", "arena.json")))
    chao0 = Image.open(os.path.join(RAIZ, "saida", "arena", aj["arquivo"])).convert("RGBA")
    TOPO = aj.get("topo_px", 0)                  # faixa da parede do fundo, acima do chão
    Wc, Hc = chao0.size[0], chao0.size[1] - TOPO
    ppx, ppy, mg = aj["px_por_unidade_x"], aj["px_por_unidade_y"], aj["margem_sim"]

    def tela(x, y):
        return (mg + x) * ppx, TOPO + Hc - (mg + y) * ppy

    monstros = {}

    def M(especie, elite=False):
        nome = especie.lower()
        if elite and os.path.isdir(os.path.join(RAIZ, "saida", nome + "_elite")):
            nome = nome + "_elite"
        if not os.path.isdir(os.path.join(RAIZ, "saida", nome)):
            nome = SUBST.get(especie, nome)
        if nome not in monstros:
            monstros[nome] = Monstro(nome)
        return monstros[nome]

    sangue = Image.new("RGBA", (Wc, Hc + TOPO), (0, 0, 0, 0))
    ds = ImageDraw.Draw(sangue)
    rnd = random.Random(5)

    def mancha(x, y, raio, cor=(70, 4, 8), n=6):
        px, py = tela(x, y)
        for _ in range(n):
            ox, oy = rnd.gauss(0, raio * 0.6), rnd.gauss(0, raio * 0.3)
            r = rnd.uniform(raio * 0.15, raio * 0.6)
            c = tuple(int(v * rnd.uniform(0.6, 1.1)) for v in cor) + (rnd.randint(150, 230),)
            ds.ellipse((px + ox - r, py + oy - r * 0.5, px + ox + r, py + oy + r * 0.5), fill=c)

    estado = {}      # id -> dict(anim, t_ini, dir, especie, pos...)
    mortos = {}      # id entidade -> dict
    cad_ent = {}     # id cadáver -> id entidade
    particulas = []  # [x, y, z, vx, vy, vz, cor, vida]
    raios = []       # feitiços: (x0,y0,x1,y1,vida,cor)
    quadros = []
    andar_ant = None

    for i, tk in enumerate(ticks[:t1]):
        if andar_ant is not None and tk["andar"] != andar_ant:
            mortos.clear()
            sangue.paste((0, 0, 0, 0), (0, 0) + sangue.size)
        andar_ant = tk["andar"]
        vivos = set()
        for e in tk["E"]:
            eid = int(e[0])
            vivos.add(eid)
            s = estado.setdefault(eid, {"anim": None, "t": tk["t"], "dir": 0})
            anim = e[7]
            if anim != s["anim"]:
                s["anim"], s["t"] = anim, tk["t"]
            dx, dy = float(e[5]), float(e[6])
            s.update(especie=e[1], faccao=e[2], x=float(e[3]), y=float(e[4]), vida=float(e[9]), viva=e[8] == "1",
                     elite=e[10][0] == "1", chefe=e[10][1] == "1")
            if abs(dx) + abs(dy) > 1e-6:
                s["dir"] = direcao(dx, dy)
        for v in tk["V"]:
            tipo = v[0]
            x, y, dx, dy = float(v[9]), float(v[10]), float(v[11]), float(v[12])
            if tipo == "Dano" and float(v[3]) > 0:
                if rnd.random() < 0.6:
                    mancha(x + rnd.uniform(-0.3, 0.3), y + rnd.uniform(-0.2, 0.2), 5, n=2)
                for _ in range(3):
                    particulas.append([x, y, 0.9, rnd.uniform(-1.5, 1.5), rnd.uniform(-1, 1), rnd.uniform(1, 3.5), (120, 8, 12), 0.6])
            elif tipo == "Morte":
                eid = int(v[2])
                s = estado.get(eid)
                if not s:
                    continue
                over = v[7] == "1"
                mortos[eid] = dict(s, t=tk["t"], cond=v[6], over=over)
                mancha(x, y, 26 if over else 16, n=14 if over else 8)
                k = 46 if over else 14
                for _ in range(k):
                    f = 5.0 if over else 2.5
                    particulas.append([x, y, 0.8, dx * f * rnd.random() + rnd.uniform(-f, f) * 0.6, dy * f * rnd.random() + rnd.uniform(-f, f) * 0.4,
                                       rnd.uniform(1.5, 6.5 if over else 4), rnd.choice([(130, 8, 12), (90, 4, 8), (200, 190, 160)]) if over else (120, 8, 12), rnd.uniform(0.5, 1.1)])
            elif tipo == "CadaverCriado":
                cad_ent[int(v[2])] = int(v[1])
            elif tipo in ("CadaverConsumido", "CadaverRemovido"):
                eid = cad_ent.get(int(v[2]))
                if eid in mortos:
                    if tipo == "CadaverConsumido":
                        mancha(x, y, 22, n=12)
                        for _ in range(30):
                            particulas.append([x, y, 0.4, rnd.uniform(-4, 4), rnd.uniform(-3, 3), rnd.uniform(2, 6), rnd.choice([(130, 8, 12), (200, 190, 160)]), rnd.uniform(0.5, 1.0)])
                    del mortos[eid]
            elif tipo == "FeiticoLancado":
                n = estado.get(int(v[1]))
                if n and v[13] != "0":
                    raios.append([n["x"], n["y"], x, y, 0.4, (120, 255, 160)])
        if tk["t"] < t0:
            continue

        img = Image.new("RGBA", (Wc, Hc + TOPO), (6, 4, 6, 255))
        img.alpha_composite(chao0, (0, 0))
        img.alpha_composite(sangue)
        d = ImageDraw.Draw(img, "RGBA")
        for o in tk["O"]:
            px, py = tela(float(o[1]), float(o[2]))
            for r, a in ((16, 40), (10, 90), (5, 255)):
                d.ellipse((px - r, py - 30 - r, px + r, py - 30 + r), fill=(120, 255, 170, a))
        desenhar = []
        for eid, s in mortos.items():
            if s["over"]:
                continue
            desenhar.append((s["y"], s, "morrer", (tk["t"] - s["t"]) / tps, True))
        for eid in vivos:
            s = estado[eid]
            if not s["viva"] and eid in mortos:
                continue
            desenhar.append((s["y"], s, ACAO.get(s["anim"], "parado"), (tk["t"] - s["t"]) / tps, False))
        for _, s, acao, tempo, morto in sorted(desenhar, key=lambda q: -q[0]):
            m = M(s["especie"], s.get("elite", False))
            px, py = tela(s["x"], s["y"])
            if not morto:
                cor = (60, 255, 140, 70) if s["faccao"] != "Inimigo" else (0, 0, 0, 110)
                if not m.meta.get("sombra"):
                    d.ellipse((px - 20, py - 8, px + 20, py + 8), fill=(0, 0, 0, 120))
                if s["faccao"] != "Inimigo":
                    d.ellipse((px - 22, py - 9, px + 22, py + 9), outline=cor, width=2)
            fr = m.frame(acao, s["dir"], tempo)
            if False:
                r_, g_, b_, a_ = fr.split()
                fr = Image.merge("RGBA", (r_.point(lambda v: int(v * 0.8)), g_.point(lambda v: int(v * 0.75)), b_, a_))
            if morto and s.get("cond") in COR_MORTE:
                c = COR_MORTE[s["cond"]]
                tinta = Image.new("RGBA", fr.size, c + (0,))
                tinta.putalpha(fr.getchannel("A").point(lambda v: int(v * 0.35)))
                fr = Image.alpha_composite(fr, tinta)
            img.alpha_composite(fr, (int(px - m.pivo[0]), int(py - (m.H - m.pivo[1]))))
            if not morto and s["faccao"] == "Inimigo" and s["vida"] < 0.999:
                w = 30 if not s["chefe"] else 60
                topo = py - 108
                d.rectangle((px - w / 2, topo, px + w / 2, topo + 3), fill=(0, 0, 0, 200))
                d.rectangle((px - w / 2, topo, px - w / 2 + w * max(0, s["vida"]), topo + 3), fill=(190, 20, 20, 255))
        for pj in tk["P"]:
            px, py = tela(float(pj[1]), float(pj[2]))
            ddx, ddy = float(pj[3]) * 16, -float(pj[4]) * 8
            if len(pj) > 5 and pj[5] == "1":      # Lança de Ossos: longa, branca, com rastro verde
                d.line((px - ddx * 4, py - 52 - ddy * 4, px + ddx * 1.5, py - 52 + ddy * 1.5), fill=(120, 255, 160, 110), width=7)
                d.line((px - ddx * 2.5, py - 52 - ddy * 2.5, px + ddx * 1.5, py - 52 + ddy * 1.5), fill=(240, 235, 215, 255), width=3)
                continue
            d.line((px - ddx, py - 52 - ddy, px + ddx, py - 52 + ddy), fill=(225, 210, 170, 255), width=2)
            d.line((px + ddx * 0.6, py - 52 + ddy * 0.6, px + ddx, py - 52 + ddy), fill=(150, 150, 160, 255), width=3)
        for r in raios:
            x0, y0 = tela(r[0], r[1]); x1, y1 = tela(r[2], r[3])
            a = int(255 * r[4] / 0.4)
            d.line((x0, y0 - 80, x1, y1 - 40), fill=r[5] + (a,), width=3)
            d.ellipse((x1 - 14, y1 - 54, x1 + 14, y1 - 26), fill=r[5] + (a // 2,))
            r[4] -= 1.0 / tps
        raios[:] = [r for r in raios if r[4] > 0]
        dt = 1.0 / tps
        for p in particulas:
            p[5] -= 14 * dt
            p[0] += p[3] * dt; p[1] += p[4] * dt; p[2] += p[5] * dt
            p[7] -= dt
            px, py = tela(p[0], p[1])
            if p[2] <= 0:
                if p[6][0] < 180:
                    mancha(p[0], p[1], 4, n=1)
                p[7] = 0
            else:
                d.rectangle((px - 1, py - p[2] * ppx * 0.6 - 1, px + 1, py - p[2] * ppx * 0.6 + 1), fill=p[6] + (255,))
        particulas[:] = [p for p in particulas if p[7] > 0]
        d.text((14, 10), f"ANDAR {tk['andar']}", fill=(200, 180, 150, 255))
        quadros.append(img.convert("RGB"))

    print("quadros:", len(quadros))
    if saida.endswith(".gif"):
        qs = [q.resize((q.width * 2 // 3, q.height * 2 // 3), Image.LANCZOS) for q in quadros]
        qs[0].save(saida, save_all=True, append_images=qs[1:], duration=1000 // tps, loop=0, optimize=True)
    else:
        tmp = saida + "_frames"
        os.makedirs(tmp, exist_ok=True)
        for i, q in enumerate(quadros):
            q.save(os.path.join(tmp, f"{i:05d}.png"))
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(tps), "-i", os.path.join(tmp, "%05d.png"),
                        "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2,fps=30", "-c:v", "libx264", "-preset", "veryfast", "-crf", "20",
                        "-pix_fmt", "yuv420p", saida], check=True)
        for f in os.listdir(tmp):
            os.remove(os.path.join(tmp, f))
        os.rmdir(tmp)
    quadros[len(quadros) // 2].save(saida + ".png")


main()
