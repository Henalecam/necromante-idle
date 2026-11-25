"""
Pipeline 3D -> pixel art isométrica (Necromante Idle)

Uso (linha de comando, sem abrir a interface do Blender):
    blender -b --python blender/render_sprites.py -- config/esqueleto.json

O script:
  1. Abre/importa o modelo (.blend, .fbx, .glb/.gltf, .obj)
  2. Monta câmera ortográfica isométrica e luz padronizada (iguais para todos os monstros)
  3. Para cada animação da config, renderiza N frames em 8 direções
  4. Converte para pixel art (redução, ajuste de cor, paleta, contorno)
  5. Gera um sprite sheet por animação + um JSON de metadados para a Unity

Tudo roda dentro do Python do Blender (só usa bpy e numpy, que já vêm com ele).
"""

import bpy
import json
import math
import os
import sys
import tempfile
import time

import numpy as np
from mathutils import Vector

# ---------------------------------------------------------------------------
# Configuração
# ---------------------------------------------------------------------------

PADRAO = {
    "altura_px": 48,            # altura do monstro em pé, em pixels na tela
    "tamanho_frame": None,      # None = calcula automático; ou um número fixo (ex.: 64)
    "superamostragem": 4,       # renderiza N vezes maior e reduz (bordas mais limpas)
    "direcoes": 8,
    "elevacao_camera": 30.0,    # 30 graus = isométrico 2:1 clássico
    "rotacao_inicial_graus": 0.0,  # use 180 se o modelo olhar para +Y
    "motor": "CYCLES",          # CYCLES (funciona em qualquer PC) ou BLENDER_EEVEE (mais rápido com GPU)
    "amostras": 16,
    "paleta": "../paletas/abismo32.hex",
    "contorno": True,
    "sombra": False,            # sombra projetada no chão, já embutida no sprite (estilo diablo liga)
    "opacidade_sombra": 0.55,
    "cor_contorno": "0b0809",
    "limiar_alpha": 0.5,
    "ajuste_cor": {"saturacao": 0.8, "brilho": 1.0, "contraste": 1.1},
    "luz": {
        "principal_cor": "ffd9b0", "principal_forca": 4.0,
        "contra_cor": "8fa8ff", "contra_forca": 3.0,
        "ambiente_cor": "1a1622", "ambiente_forca": 0.6,
    },
    "pixels_por_unidade_unity": 32,
    "acoes": [],
}

NOMES_DIRECOES_8 = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
ROT_LUZ_PRINCIPAL = (math.radians(50), 0.0, math.radians(-35))


def direcao_da_luz():
    from mathutils import Euler
    return Euler(ROT_LUZ_PRINCIPAL).to_matrix() @ Vector((0.0, 0.0, -1.0))


def log(msg):
    print(f"[necro-pipeline] {msg}", flush=True)


ESTILOS = {
    # sprites grandes pré-renderizados, sem contorno, cores ricas e escuras (estilo Diablo 1/2)
    "diablo": {"superamostragem": 3, "contorno": False, "paleta": None, "amostras": 24, "sombra": True,
               "ajuste_cor": {"saturacao": 0.95, "brilho": 1.0, "contraste": 1.12}},
    # pixel art de baixa resolução com paleta fixa e contorno
    "pixel": {},
}

RETRATO = False   # modo de inspeção: 1 frame grande em 4 direções, para avaliar o modelo


def ler_argumentos():
    global RETRATO
    argv = sys.argv
    if "--" in argv:
        args = argv[argv.index("--") + 1:]
    else:
        args = argv[1:]
    if "--retrato" in args:
        RETRATO = True
        args = [a for a in args if a != "--retrato"]
    if not args:
        raise SystemExit("Informe o arquivo de configuração: blender -b --python render_sprites.py -- config.json")
    return args[0]


def carregar_config(caminho):
    with open(caminho, "r", encoding="utf-8") as f:
        cfg = json.load(f)
    base = os.path.dirname(os.path.abspath(caminho))
    final = json.loads(json.dumps(PADRAO))
    for k, v in ESTILOS.get(cfg.get("estilo", "pixel"), {}).items():
        final[k] = v
    for k, v in cfg.items():
        if isinstance(v, dict) and isinstance(final.get(k), dict):
            final[k].update(v)
        else:
            final[k] = v

    def resolver(p):
        return p if (p is None or os.path.isabs(p)) else os.path.normpath(os.path.join(base, p))

    final["modelo"] = resolver(final["modelo"])
    final["saida"] = resolver(final.get("saida", f"saida/{final['nome']}"))
    final["paleta"] = resolver(final["paleta"]) if final.get("paleta") else None
    for a in final["acoes"]:
        if a.get("arquivo_animacao"):
            a["arquivo_animacao"] = resolver(a["arquivo_animacao"])
    if not final["acoes"]:
        raise SystemExit("A config precisa de pelo menos uma ação em 'acoes'.")
    if RETRATO:
        quais = [a for a in final["acoes"] if a["nome"] in ("parado", "atacar", "conjurar")] or final["acoes"][:1]
        final["acoes"] = [{"nome": a["nome"], "acao_blender": a["acao_blender"],
                           "lista_frames": [1] if a["nome"] == "parado" else [9]} for a in quais]
        final.update({"altura_px": 380, "superamostragem": 1, "paleta": None, "contorno": False,
                      "direcoes": 4, "amostras": 48, "saida": final["saida"] + "_retrato",
                      "ajuste_cor": {"saturacao": 1.0, "brilho": 1.0, "contraste": 1.0}})
    return final


def hex_para_rgb(h):
    h = h.strip().lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# ---------------------------------------------------------------------------
# Cena
# ---------------------------------------------------------------------------

def preparar_cena(cfg):
    caminho = cfg["modelo"]
    ext = os.path.splitext(caminho)[1].lower()
    if not os.path.exists(caminho):
        raise SystemExit(f"Modelo não encontrado: {caminho}")

    if ext == ".blend":
        bpy.ops.wm.open_mainfile(filepath=caminho)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if ext == ".fbx":
            bpy.ops.import_scene.fbx(filepath=caminho, automatic_bone_orientation=True)
        elif ext in (".glb", ".gltf"):
            bpy.ops.import_scene.gltf(filepath=caminho)
        elif ext == ".obj":
            bpy.ops.wm.obj_import(filepath=caminho)
        else:
            raise SystemExit(f"Formato não suportado: {ext}")

    cena = bpy.context.scene
    # remove câmeras e luzes que vieram com o modelo
    for obj in list(cena.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)

    importar_animacoes_extras(cena, cfg)

    # agrupa tudo sob um "pivô raiz" que gira para gerar as direções
    raiz = bpy.data.objects.new("PivoRaiz", None)
    cena.collection.objects.link(raiz)
    for obj in list(cena.objects):
        if obj is raiz:
            continue
        if obj.parent is None:
            mw = obj.matrix_world.copy()
            obj.parent = raiz
            obj.matrix_world = mw
    return cena, raiz


def importar_animacoes_extras(cena, cfg):
    """Importa ações de FBX separados (ex.: cada animação baixada do Mixamo 'Without Skin')."""
    for a in cfg["acoes"]:
        arq = a.get("arquivo_animacao")
        if not arq:
            continue
        if not os.path.exists(arq):
            raise SystemExit(f"Arquivo de animação não encontrado: {arq}")
        antes_acoes = set(bpy.data.actions)
        antes_objs = set(cena.objects)
        bpy.ops.import_scene.fbx(filepath=arq, automatic_bone_orientation=True)
        novas = [x for x in bpy.data.actions if x not in antes_acoes]
        for obj in [o for o in cena.objects if o not in antes_objs]:
            bpy.data.objects.remove(obj, do_unlink=True)
        if not novas:
            raise SystemExit(f"Nenhuma animação encontrada em {arq}")
        escolhida = novas[0]
        if a.get("acao_blender"):
            alvo = a["acao_blender"].lower()
            escolhida = next((x for x in novas if alvo in x.name.lower()), novas[0])
        for x in novas:
            if x is not escolhida:
                bpy.data.actions.remove(x)
        escolhida.name = f"__extra__{a['nome']}"
        escolhida.use_fake_user = True
        a["acao_blender"] = escolhida.name
        log(f"Animação '{a['nome']}' importada de {os.path.basename(arq)}")


def objetos_malha(cena):
    return [o for o in cena.objects if o.type == "MESH" and not o.hide_render]


def armaduras(cena):
    return [o for o in cena.objects if o.type == "ARMATURE"]


def achar_acao(nome):
    alvo = nome.lower()
    candidatos = list(bpy.data.actions)
    for a in candidatos:
        if a.name.lower() == alvo:
            return a
    for a in candidatos:
        if a.name.lower().split("|")[-1] == alvo:
            return a
    for a in candidatos:
        if alvo in a.name.lower():
            return a
    disponiveis = ", ".join(a.name for a in candidatos) or "(nenhuma)"
    raise SystemExit(f"Ação '{nome}' não encontrada. Ações no arquivo: {disponiveis}")


def aplicar_acao(cena, acao):
    for arm in armaduras(cena) or [o for o in cena.objects if o.animation_data]:
        ad = arm.animation_data or arm.animation_data_create()
        for trilha in ad.nla_tracks:
            trilha.mute = True
        ad.action = acao
        # Blender 4.4+: ações em camadas usam "slots"
        if hasattr(ad, "action_slot") and ad.action_slot is None and len(getattr(acao, "slots", [])):
            try:
                ad.action_slot = acao.slots[0]
            except Exception:
                pass


def frames_da_acao(acao, quantidade, loop):
    ini, fim = acao.frame_range
    ini, fim = float(ini), float(fim)
    if quantidade <= 1 or fim <= ini:
        return [ini]
    if loop:  # não repete o último frame (igual ao primeiro num loop)
        passo = (fim - ini) / quantidade
        return [ini + i * passo for i in range(quantidade)]
    passo = (fim - ini) / (quantidade - 1)
    return [ini + i * passo for i in range(quantidade)]


def escolher_frames(acao, a_cfg):
    """'lista_frames' (frames exatos, ex.: as poses-chave de um ataque) ou 'frames' (quantidade, espaçada igualmente)."""
    if a_cfg.get("lista_frames"):
        return [float(x) for x in a_cfg["lista_frames"]]
    return frames_da_acao(acao, a_cfg.get("frames", 6), a_cfg.get("loop", True))


def definir_frame(cena, f):
    inteiro = int(math.floor(f))
    cena.frame_set(inteiro, subframe=f - inteiro)


def vertices_mundo(cena):
    dg = bpy.context.evaluated_depsgraph_get()
    blocos = []
    for obj in objetos_malha(cena):
        ev = obj.evaluated_get(dg)
        malha = ev.to_mesh()
        n = len(malha.vertices)
        if n:
            co = np.empty(n * 3, dtype=np.float32)
            malha.vertices.foreach_get("co", co)
            co = co.reshape(n, 3)
            m = np.array(ev.matrix_world, dtype=np.float32)
            co = co @ m[:3, :3].T + m[:3, 3]
            blocos.append(co)
        ev.to_mesh_clear()
    return np.concatenate(blocos) if blocos else np.zeros((0, 3), dtype=np.float32)


# ---------------------------------------------------------------------------
# Enquadramento: mesma escala e mesmo pivô em todos os frames e direções
# ---------------------------------------------------------------------------

def medir(cena, cfg):
    """Mede o modelo em todos os frames e em todas as direções, já projetado na tela."""
    e = math.radians(cfg["elevacao_camera"])
    n_dir = int(cfg["direcoes"])
    rot0 = math.radians(cfg["rotacao_inicial_graus"])
    angulos = [rot0 + d * 2 * math.pi / n_dir for d in range(n_dir)]
    sx_min = sy_min = float("inf")
    sx_max = sy_max = float("-inf")
    altura_pe = None
    for i, a_cfg in enumerate(cfg["acoes"]):
        acao = achar_acao(a_cfg["acao_blender"])
        aplicar_acao(cena, acao)
        for k, f in enumerate(escolher_frames(acao, a_cfg)):
            definir_frame(cena, f)
            v = vertices_mundo(cena)
            if not len(v):
                continue
            if i == 0 and k == 0:
                altura_pe = float(v[:, 2].max() - v[:, 2].min())
            for ang in angulos:
                c, s_ = math.cos(ang), math.sin(ang)
                x = v[:, 0] * c - v[:, 1] * s_
                y = v[:, 0] * s_ + v[:, 1] * c
                tela_x = x
                tela_y = y * math.sin(e) + v[:, 2] * math.cos(e)
                if cfg.get("sombra"):
                    # inclui no enquadramento a sombra de cada vértice projetada no chão (z = 0)
                    L = direcao_da_luz()
                    k = np.maximum(v[:, 2], 0.0) / max(1e-4, -L.z)
                    sx_, sy_ = x + L.x * k, y + L.y * k
                    tela_x = np.concatenate([tela_x, sx_])
                    tela_y = np.concatenate([tela_y, sy_ * math.sin(e)])
                sx_min, sx_max = min(sx_min, float(tela_x.min())), max(sx_max, float(tela_x.max()))
                sy_min, sy_max = min(sy_min, float(tela_y.min())), max(sy_max, float(tela_y.max()))
    if altura_pe is None or altura_pe <= 0:
        raise SystemExit("Não foi possível medir o modelo (nenhuma malha visível?).")
    return (sx_min, sx_max, sy_min, sy_max), altura_pe


def montar_camera_e_luz(cena, cfg, limites, altura_pe):
    e = math.radians(cfg["elevacao_camera"])
    ppu = cfg["altura_px"] / (altura_pe * math.cos(e))  # pixels por unidade do Blender
    sx_min, sx_max, sy_min, sy_max = limites
    margem = 1 + (1 if cfg["contorno"] else 0)

    # tudo em pixels, com a origem (os pés) em coordenada inteira: sprite nítido e sem tremer
    if cfg["tamanho_frame"]:
        largura = altura = int(cfg["tamanho_frame"])
        esquerda = -largura // 2
        baixo = int(math.floor(sy_min * ppu)) - margem
    else:
        esquerda = int(math.floor(sx_min * ppu)) - margem
        direita = int(math.ceil(sx_max * ppu)) + margem
        baixo = int(math.floor(sy_min * ppu)) - margem
        cima = int(math.ceil(sy_max * ppu)) + margem
        largura = direita - esquerda
        altura = cima - baixo
        largura += largura % 2
        altura += altura % 2

    centro_x = (esquerda + largura / 2.0) / ppu
    centro_y = (baixo + altura / 2.0) / ppu
    frente = Vector((0.0, math.cos(e), -math.sin(e)))     # câmera olha para +Y, inclinada para baixo
    cima_v = Vector((0.0, math.sin(e), math.cos(e)))
    direita_v = Vector((1.0, 0.0, 0.0))
    alvo = direita_v * centro_x + cima_v * centro_y
    dist = 100.0 + 10.0 * max(largura, altura) / ppu

    cam_dados = bpy.data.cameras.new("CamIso")
    cam_dados.type = "ORTHO"
    cam_dados.ortho_scale = max(largura, altura) / ppu
    cam_dados.sensor_fit = "AUTO"
    cam_dados.clip_start = 0.01
    cam_dados.clip_end = dist * 4
    cam = bpy.data.objects.new("CamIso", cam_dados)
    cena.collection.objects.link(cam)
    cam.location = alvo - frente * dist
    cam.rotation_euler = (math.pi / 2 - e, 0.0, 0.0)
    cena.camera = cam

    luz = cfg["luz"]
    if cfg.get("sombra"):
        # plano "apanhador de sombra": invisível, só registra a sombra com transparência
        import bmesh
        m_ = bpy.data.meshes.new("ChaoSombra")
        bm = bmesh.new()
        R = 50.0
        bm.faces.new([bm.verts.new(p) for p in ((-R, -R, 0), (R, -R, 0), (R, R, 0), (-R, R, 0))])
        bm.to_mesh(m_); bm.free()
        chao = bpy.data.objects.new("ChaoSombra", m_)
        cena.collection.objects.link(chao)
        chao.is_shadow_catcher = True

    def sol(nome, cor, forca, rot):
        d = bpy.data.lights.new(nome, "SUN")
        d.color = hex_para_rgb(cor)
        d.energy = forca
        o = bpy.data.objects.new(nome, d)
        o.rotation_euler = rot
        cena.collection.objects.link(o)

    # principal: de cima, à esquerda e à frente (tocha); contra-luz fria por trás à direita
    sol("LuzPrincipal", luz["principal_cor"], luz["principal_forca"],
        ROT_LUZ_PRINCIPAL)
    sol("LuzContra", luz["contra_cor"], luz["contra_forca"],
        (math.radians(-60), 0.0, math.radians(-30)))

    mundo = cena.world or bpy.data.worlds.new("Mundo")
    cena.world = mundo
    if not mundo.node_tree:
        mundo.use_nodes = True
    fundo = mundo.node_tree.nodes.get("Background")
    if fundo is None:
        fundo = mundo.node_tree.nodes.new("ShaderNodeBackground")
    fundo.inputs[0].default_value = (*hex_para_rgb(luz["ambiente_cor"]), 1.0)
    fundo.inputs[1].default_value = luz["ambiente_forca"]

    r = cena.render
    r.engine = cfg["motor"]
    if cfg["motor"] == "CYCLES":
        cena.cycles.samples = int(cfg["amostras"])
        cena.cycles.use_denoising = False
        cena.cycles.device = "CPU"
    r.film_transparent = True
    r.resolution_x = largura * int(cfg["superamostragem"])
    r.resolution_y = altura * int(cfg["superamostragem"])
    r.resolution_percentage = 100
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    r.image_settings.color_depth = "8"
    cena.view_settings.view_transform = "Standard"
    cena.view_settings.look = "None"

    # pivô (os pés) em pixels, com origem no canto inferior esquerdo (padrão da Unity)
    pivo_px = (-esquerda, -baixo)
    return (largura, altura), pivo_px, ppu


# ---------------------------------------------------------------------------
# Pixel art
# ---------------------------------------------------------------------------

def carregar_paleta(caminho):
    if not caminho:
        return None
    cores = []
    with open(caminho, "r", encoding="utf-8") as f:
        for linha in f:
            linha = linha.strip()
            if linha and not linha.startswith(";"):
                cores.append(hex_para_rgb(linha))
    return np.array(cores, dtype=np.float32)


def ler_png(caminho):
    img = bpy.data.images.load(caminho, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[::-1]  # de cima para baixo


def salvar_png(arr, caminho):
    h, w, _ = arr.shape
    img = bpy.data.images.new("saida", w, h, alpha=True)
    img.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).ravel().astype(np.float32))
    img.filepath_raw = caminho
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def reduzir(hi, k):
    h, w, _ = hi.shape
    blocos = hi.reshape(h // k, k, w // k, k, 4)
    a = blocos[..., 3]
    soma_a = a.sum(axis=(1, 3))
    rgb = (blocos[..., :3] * a[..., None]).sum(axis=(1, 3))
    rgb = np.where(soma_a[..., None] > 0, rgb / np.maximum(soma_a[..., None], 1e-6), 0)
    return np.concatenate([rgb, (soma_a / (k * k))[..., None]], axis=-1)


def ajustar_cor(img, ajuste):
    rgb = img[..., :3]
    lum = (rgb * np.array([0.299, 0.587, 0.114], dtype=np.float32)).sum(-1, keepdims=True)
    rgb = lum + (rgb - lum) * ajuste.get("saturacao", 1.0)
    rgb = (rgb - 0.5) * ajuste.get("contraste", 1.0) + 0.5
    rgb = rgb * ajuste.get("brilho", 1.0)
    img[..., :3] = np.clip(rgb, 0, 1)
    return img


def quantizar(img, paleta):
    if paleta is None:
        return img
    pesos = np.array([0.30, 0.59, 0.11], dtype=np.float32)
    rgb = img[..., :3].reshape(-1, 1, 3)
    d = (((rgb - paleta[None, :, :]) ** 2) * pesos).sum(-1)
    img[..., :3] = paleta[d.argmin(-1)].reshape(img.shape[:2] + (3,))
    return img


def contornar(img, cor):
    opaco = img[..., 3] > 0
    viz = np.zeros_like(opaco)
    viz[1:, :] |= opaco[:-1, :]
    viz[:-1, :] |= opaco[1:, :]
    viz[:, 1:] |= opaco[:, :-1]
    viz[:, :-1] |= opaco[:, 1:]
    borda = viz & ~opaco
    img[borda, :3] = cor
    img[borda, 3] = 1.0
    return img


def para_pixel_art(hi, cfg, paleta):
    img = reduzir(hi, int(cfg["superamostragem"]))
    img = ajustar_cor(img, cfg["ajuste_cor"])
    if cfg.get("sombra"):
        # corpo opaco; o resto (sombra no chão e bordas suaves) vira preto translúcido
        a = img[..., 3]
        corpo = a >= cfg["limiar_alpha"]
        lum = img[..., :3].max(-1)
        sombra = (~corpo | (lum < 0.004)) & (a > 0.03)
        solido = corpo & ~sombra
        img[sombra, :3] = 0
        img[..., 3] = np.where(solido, 1.0, np.where(sombra, np.minimum(a, 1.0) * cfg["opacidade_sombra"], 0.0))
    else:
        img[..., 3] = (img[..., 3] >= cfg["limiar_alpha"]).astype(np.float32)
    img[img[..., 3] == 0, :3] = 0
    img = quantizar(img, paleta)
    img[img[..., 3] == 0, :3] = 0
    if cfg["contorno"]:
        img = contornar(img, np.array(hex_para_rgb(cfg["cor_contorno"]), dtype=np.float32))
    return img


# ---------------------------------------------------------------------------
# Principal
# ---------------------------------------------------------------------------

def main():
    inicio = time.time()
    cfg = carregar_config(ler_argumentos())
    log(f"Monstro: {cfg['nome']}  |  modelo: {cfg['modelo']}")
    os.makedirs(cfg["saida"], exist_ok=True)

    cena, raiz = preparar_cena(cfg)
    limites, altura_pe = medir(cena, cfg)
    (larg, alt), pivo_px, ppu = montar_camera_e_luz(cena, cfg, limites, altura_pe)
    log(f"Frame {larg}x{alt} px  |  pivô (pés) em {pivo_px[0]},{pivo_px[1]}")

    paleta = carregar_paleta(cfg["paleta"])
    n_dir = int(cfg["direcoes"])
    nomes_dir = NOMES_DIRECOES_8 if n_dir == 8 else [f"D{i}" for i in range(n_dir)]
    rot0 = math.radians(cfg["rotacao_inicial_graus"])
    tmp = tempfile.mkdtemp(prefix="necro_")
    caminho_tmp = os.path.join(tmp, "frame.png")

    meta = {
        "nome": cfg["nome"],
        "largura_frame": larg,
        "altura_frame": alt,
        "pivo_px": [pivo_px[0], pivo_px[1]],
        "pivo_normalizado": [round(pivo_px[0] / larg, 4), round(pivo_px[1] / alt, 4)],
        "pixels_por_unidade_unity": cfg["pixels_por_unidade_unity"],
        "direcoes": nomes_dir,
        "sombra": bool(cfg.get("sombra")),
        "layout": "linhas = direções (de cima para baixo, na ordem de 'direcoes'); colunas = frames",
        "acoes": [],
    }

    for a_cfg in cfg["acoes"]:
        acao = achar_acao(a_cfg["acao_blender"])
        aplicar_acao(cena, acao)
        frames = escolher_frames(acao, a_cfg)
        folha = np.zeros((n_dir * alt, len(frames) * larg, 4), dtype=np.float32)
        arquivo_pronto = os.path.join(cfg["saida"], f"{cfg['nome']}_{a_cfg['nome']}.png")
        if os.path.exists(arquivo_pronto) and os.environ.get("NECRO_REFAZER") != "1":
            # retomada: a folha desta ação já foi renderizada numa execução anterior (NECRO_REFAZER=1 força refazer)
            log(f"Ação '{a_cfg['nome']}': folha já existe, pulando")
            meta["acoes"].append({"nome": a_cfg["nome"], "arquivo": os.path.basename(arquivo_pronto), "frames": len(frames),
                                  "fps": a_cfg.get("fps", 8), "loop": a_cfg.get("loop", True)})
            continue
        log(f"Ação '{a_cfg['nome']}' ({acao.name}): {len(frames)} frames x {n_dir} direções")
        for c, f in enumerate(frames):
            definir_frame(cena, f)
            for d in range(n_dir):
                raiz.rotation_euler = (0.0, 0.0, rot0 + d * 2 * math.pi / n_dir)
                bpy.context.view_layer.update()
                cena.render.filepath = caminho_tmp
                bpy.ops.render.render(write_still=True)
                px = para_pixel_art(ler_png(caminho_tmp), cfg, paleta)
                folha[d * alt:(d + 1) * alt, c * larg:(c + 1) * larg] = px
        arquivo = f"{cfg['nome']}_{a_cfg['nome']}.png"
        salvar_png(folha, os.path.join(cfg["saida"], arquivo))
        meta["acoes"].append({
            "nome": a_cfg["nome"],
            "arquivo": arquivo,
            "frames": len(frames),
            "fps": a_cfg.get("fps", 8),
            "loop": a_cfg.get("loop", True),
        })

    with open(os.path.join(cfg["saida"], f"{cfg['nome']}.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)
    log(f"Pronto em {time.time() - inicio:.1f}s -> {cfg['saida']}")


if __name__ == "__main__":
    main()
