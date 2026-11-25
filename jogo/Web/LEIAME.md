# Versão de navegador

O mesmo núcleo C# do jogo (`../Nucleo`), compilado para WebAssembly (.NET 8, `browser-wasm`), com desenho em canvas
feito em JavaScript a partir dos sprites do pipeline. Não é o build WebGL da Unity: é um cliente web próprio que usa
as mesmas regras, o mesmo balanceamento e a mesma arte.

- `Program.cs` — ponte `[JSExport]` entre o núcleo e a página (avançar ticks, painéis, ações, save em JSON).
- `wwwroot/jogo.js` — desenho (arena, sprites em 8 direções, sangue, partículas), interface e save no navegador.
- `wwwroot/main.js` — carrega o runtime; a configuração binária do runtime vai embutida nele.
- `montar.sh` — publica e monta tudo em `dist/` (sprites convertidos para WebP, ~9 MB).

Para rodar: `./montar.sh` e servir `dist/` por HTTP (`python3 -m http.server -d dist`). Precisa do SDK .NET 8 com
`dotnet workload install wasm-tools`. A pasta `dist/` pode ser hospedada em qualquer site estático (itch.io, GitHub Pages).

Já tem: combate ao vivo, velocidade 1x/2x/4x, exército (erguer, ativar), Necrópole, equipamento, save local e
progresso ausente (até 8 h, recuperado ao voltar). Falta: som, efeitos de morte por condição, ordens e posturas.
