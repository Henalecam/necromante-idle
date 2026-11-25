# Necromante Idle

Idle game sombrio, na linha de Diablo: um necromante desce sozinho uma abadia afundada, ceifa as almas dos inimigos,
ergue-as como minions e as evolui. Combate automático e visual, com gore; o jogador presente rende mais que o ausente.

**Jogar no navegador:** a pasta `docs/` é o jogo pronto. Para publicar com GitHub Pages: Settings → Pages → branch `main`, pasta `/docs`.

## O que há aqui

| Pasta | Conteúdo |
|---|---|
| `jogo/Nucleo` | Regras do jogo em C# puro (.NET Standard 2.1), determinístico, 10 ticks/s. Sem Unity. |
| `jogo/Testes` | Testes (80) e o portão de balanceamento: um robô joga campanhas inteiras e mede 9 metas. |
| `jogo/Web` | Versão de navegador: o núcleo compilado para WebAssembly + cliente em canvas. `./montar.sh` gera o site. |
| `jogo/UnityProjeto` | Projeto Unity 6.3 LTS (camada visual). Conferido só por compilação; ainda não aberto numa Unity. |
| `jogo/Replay` | Exporta um replay da simulação para as prévias em vídeo. |
| `pipeline` | Fábrica de monstros em Blender (Python): modelos procedurais → sprites isométricos em 8 direções. |
| `docs` | Build web publicado. |

## Sistemas já no jogo

Combate automático com feitiços · ceifa de almas (automática ou por toque, com bônus) · poderes de toque (Detonar, Frenesi) ·
exército com Comando, níveis e estágios · fusão de almas · bestiário com Domínio por espécie · equipamento (9 espaços,
5 raridades, lendários) · Necrópole (5 construções) · missões com história · encruzilhadas com pactos · save local e
progresso ausente.

## Rodar

```bash
# testes e portão de balanceamento (9 metas; sai com erro se alguma ficar fora da faixa)
cd jogo && dotnet run -c Release --project Testes -- --so-testes
cd jogo && dotnet run -c Release --project Testes -- --metas 24 14

# versão de navegador (precisa de: dotnet workload install wasm-tools)
cd jogo/Web && ./montar.sh && python3 -m http.server -d dist
```

O histórico de decisões está em `jogo/PLANO_CICLOS.md`; as metas medidas, em `jogo/METAS_BALANCEAMENTO.md`.
Toda a arte é própria, gerada pelo pipeline deste repositório.
