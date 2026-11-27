#!/bin/bash
# Monta a versão de navegador em Web/dist: núcleo em WebAssembly + página + sprites (convertidos para WebP).
set -e
cd "$(dirname "$0")"
dotnet publish -c Release -nologo -v q 2>&1 | grep -E " error |rror CS" && exit 1
rm -rf dist && mkdir -p dist && cp -r bin/Release/net8.0/browser-wasm/AppBundle/. dist/
cp wwwroot/main.js wwwroot/jogo.js wwwroot/index.html dist/ && cp -r wwwroot/fontes dist/ && rm -f dist/*.woff2 dist/OFL-*.txt
rm -f dist/_framework/*.map dist/_framework/*.symbols dist/package.json dist/*.runtimeconfig.json
python3 - <<'PY'
import os, shutil
from PIL import Image
src = '/home/claude/necro-pipeline/saida'; dst = 'dist/arte'
for nome in sorted(os.listdir(src)):
    if nome.endswith('_retrato'): continue      # retratos de inspeção não vão para o jogo
    os.makedirs(f'{dst}/{nome}', exist_ok=True)
    for f in os.listdir(f'{src}/{nome}'):
        if f.endswith('.json'): shutil.copy(f'{src}/{nome}/{f}', f'{dst}/{nome}/{f}')
        elif f.endswith('.png') and not f.startswith('retrato'):
            Image.open(f'{src}/{nome}/{f}').save(f'{dst}/{nome}/{f[:-4]}.webp', quality=88, method=4)
PY
python3 - <<'PY'
import base64
b = base64.b64encode(open('dist/_framework/supportFiles/0_runtimeconfig.bin','rb').read()).decode()
s = open('dist/main.js').read().replace('__RUNTIMECONFIG__', b); open('dist/main.js','w').write(s)
PY
rm -rf dist/_framework/supportFiles dist/.stamp
mv dist/_framework dist/nucleo   # o hospedeiro reserva nomes começados por _
du -sh dist; find dist -type f | wc -l
