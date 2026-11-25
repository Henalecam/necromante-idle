"""
Gera os modelos .blend dos monstros e as configs do pipeline.

Uso:
    blender -b --python blender/gerar_monstros.py -- todos
    blender -b --python blender/gerar_monstros.py -- esqueleto zumbi
"""

import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
sys.path.insert(0, AQUI)

from fabrica import base as B          # noqa: E402
from fabrica import monstros as MO     # noqa: E402


def main():
    a = sys.argv
    nomes = a[a.index("--") + 1:] if "--" in a else a[1:]
    if not nomes or nomes == ["todos"]:
        nomes = list(MO.MONSTROS)
    for nome in nomes:
        rig, padrao, acoes, extra = MO.MONSTROS[nome]()
        pasta = os.path.join(RAIZ, "modelos", nome)
        os.makedirs(pasta, exist_ok=True)
        B.salvar(os.path.join(pasta, nome + ".blend"), rig, padrao)
        cfg = {"nome": nome, "modelo": f"../modelos/{nome}/{nome}.blend", "saida": f"../saida/{nome}",
               "estilo": "diablo", "acoes": acoes}
        cfg.update(extra)
        with open(os.path.join(RAIZ, "config", nome + ".json"), "w", encoding="utf-8") as f:
            json.dump(cfg, f, ensure_ascii=False, indent=2)
        print(f"[fabrica] {nome}: modelo e config gerados", flush=True)


if __name__ == "__main__":
    main()
