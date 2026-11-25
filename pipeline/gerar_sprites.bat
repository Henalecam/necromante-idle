@echo off
REM Gera os sprites de um monstro. Uso: gerar_sprites.bat config\esqueleto_teste.json
REM Ajuste o caminho abaixo se o seu Blender estiver em outra pasta ou versao.
set BLENDER=C:\Program Files\Blender Foundation\Blender 5.3\blender.exe

if "%~1"=="" (
  echo Informe a config. Exemplo: gerar_sprites.bat config\esqueleto_teste.json
  exit /b 1
)
"%BLENDER%" -b --python "%~dp0blender\render_sprites.py" -- "%~1"
