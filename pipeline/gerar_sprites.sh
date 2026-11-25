#!/usr/bin/env bash
# Gera os sprites de um monstro. Uso: ./gerar_sprites.sh config/esqueleto_teste.json
# Defina BLENDER se o executável não estiver no PATH, ex.:
#   BLENDER=/Applications/Blender.app/Contents/MacOS/Blender ./gerar_sprites.sh config/x.json
set -e
BLENDER="${BLENDER:-blender}"
if [ -z "$1" ]; then
  echo "Informe a config. Exemplo: ./gerar_sprites.sh config/esqueleto_teste.json"
  exit 1
fi
DIR="$(cd "$(dirname "$0")" && pwd)"
"$BLENDER" -b --python "$DIR/blender/render_sprites.py" -- "$1"
