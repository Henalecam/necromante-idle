# Pipeline de sprites — Necromante Idle

Transforma um modelo 3D animado em sprite sheets isométricos de pixel art, prontos para a Unity.
Todos os monstros saem com a mesma câmera, a mesma luz e a mesma paleta, então o estilo fica uniforme
mesmo quando os modelos vêm de fontes diferentes (Quaternius, Mixamo ou feitos por nós).

Testado no Blender 5.3. Usa só o Python que já vem com o Blender, sem instalar nada além dele.

## Novidade: fábrica de monstros e estilo "diablo"

Os monstros agora são construídos por código no próprio Blender (anatomia, materiais procedurais, rig e
animações), sem depender de modelos baixados:

```
blender -b --python blender/gerar_monstros.py -- todos       # gera modelos/<nome>/<nome>.blend e config/<nome>.json
blender -b --python blender/render_sprites.py -- config/esqueleto.json            # sprite sheets do jogo
blender -b --python blender/render_sprites.py -- config/esqueleto.json --retrato  # 4 vistas grandes, para avaliar o modelo
blender -b --python blender/gerar_arena.py -- saida/arena 20 12 56                # cripta (chão + parede do fundo)
```

- `blender/fabrica/base.py`: peças (tubos orgânicos, crânios, garras, membranas), materiais (osso, carne podre,
  pele de demônio com rachaduras de lava, ferro enferrujado, pano) e o rig.
- `blender/fabrica/monstros.py`: esqueleto, arqueiro ossudo, zumbi, diabrete, acólito caído e o necromante.
  Um monstro novo é uma função nova; variantes (arqueiro, acólito) reaproveitam a base.
- `"estilo": "diablo"` na config: sprites grandes pré-renderizados (~96 px de altura), sem contorno e sem
  paleta reduzida. `"estilo": "pixel"` mantém o pixel art de baixa resolução.
- `ferramentas/replay_gif.py`: desenha um replay da simulação real com esses sprites (prévia do jogo).

Cada monstro leva de 5 a 10 minutos para renderizar só com processador (200 quadros).

## Estrutura

```
necro-pipeline/
  blender/render_sprites.py      o pipeline
  blender/criar_modelo_teste.py  gera um esqueleto de teste com 4 animações
  config/                        uma config (.json) por monstro
  modelos/                       os modelos 3D (.fbx, .glb, .blend, .obj)
  paletas/abismo32.hex           paleta gótica de 32 cores do jogo
  saida/                         sprite sheets e metadados gerados
  gerar_sprites.bat / .sh        atalhos para rodar
```

## Primeiro teste (5 minutos)

1. Instale o Blender (blender.org). No Windows, confira o caminho em `gerar_sprites.bat`.
2. Gere o modelo de teste:
   `blender -b --python blender/criar_modelo_teste.py -- modelos/teste`
3. Gere os sprites:
   - Windows: `gerar_sprites.bat config\esqueleto_teste.json`
   - Mac/Linux: `./gerar_sprites.sh config/esqueleto_teste.json`
4. Veja o resultado em `saida/esqueleto/`.

## O que sai

Para cada animação, um PNG: **linhas = 8 direções** (S, SE, E, NE, N, NW, W, SW, de cima para baixo) e
**colunas = frames**. Mais um JSON com o tamanho do quadro, o pivô (os pés, em pixels a partir do canto
inferior esquerdo, que é o padrão da Unity), o FPS e se a animação repete. O importador da Unity vai ler esse JSON.

O pivô fica sempre no mesmo pixel em todos os quadros e direções, então o sprite não "treme" na tela.

## Usando um modelo novo

1. Coloque o modelo em `modelos/<nome>/`.
2. Copie uma config e ajuste os campos.
3. Rode `gerar_sprites`.

Para descobrir o nome das animações dentro de um arquivo, rode o pipeline com um nome qualquer: o erro lista
todas as ações encontradas.

### Modelos do Mixamo

- Baixe o personagem em **FBX**, com skin.
- Baixe cada animação separada, em **FBX, Without Skin**, e marque **In Place** nas animações de andar e correr.
- Na config, use `arquivo_animacao` em cada ação (veja `config/modelo_mixamo.json`).

### Modelos da Quaternius

As animações já vêm dentro do mesmo arquivo. Use `acao_blender` com o nome da animação (ex.: `"Walk"`).

## Campos da config

| Campo | Para que serve | Padrão |
| --- | --- | --- |
| `nome` | Prefixo dos arquivos gerados | obrigatório |
| `modelo` | Caminho do modelo (relativo à config) | obrigatório |
| `saida` | Pasta de saída | `saida/<nome>` |
| `altura_px` | Altura do monstro em pé, em pixels. Define o porte: Pequeno ~40, Médio ~56, Grande ~80 | 48 |
| `tamanho_frame` | Força um quadro quadrado fixo; vazio = enquadramento automático justo | automático |
| `superamostragem` | Renderiza N vezes maior e reduz, para bordas limpas | 4 |
| `direcoes` | Quantidade de direções | 8 |
| `elevacao_camera` | 30 = isométrico 2:1 clássico | 30 |
| `rotacao_inicial_graus` | Use 180 se o modelo sair de costas na direção S | 0 |
| `motor` | `CYCLES` (qualquer PC) ou `BLENDER_EEVEE` (mais rápido com placa de vídeo) | CYCLES |
| `amostras` | Qualidade do Cycles | 16 |
| `paleta` | Arquivo .hex; `null` desliga a paleta | abismo32 |
| `contorno` / `cor_contorno` | Contorno escuro de 1 pixel | ligado |
| `sombra` / `opacidade_sombra` | Sombra projetada no chão embutida no sprite (o estilo diablo liga) | desligado / 0.55 |
| `ajuste_cor` | `saturacao`, `brilho`, `contraste` antes de aplicar a paleta | 0.8 / 1.0 / 1.1 |
| `luz` | Cores e forças da luz principal (tocha), contra-luz fria e ambiente | ver script |
| `acoes` | Lista de animações (abaixo) | obrigatório |

Cada ação:

| Campo | Para que serve |
| --- | --- |
| `nome` | Nome no jogo (`parado`, `andar`, `atacar`, `morrer`...) |
| `acao_blender` | Nome (ou parte do nome) da animação no arquivo |
| `arquivo_animacao` | FBX separado com a animação (Mixamo) |
| `frames` | Quantos quadros, espaçados igualmente |
| `lista_frames` | Frames exatos, ex.: `[1, 5, 8, 10, 12, 16]`. Use em ataques, para não perder as poses-chave |
| `fps` | Velocidade no jogo |
| `loop` | `true` para parado/andar, `false` para atacar/morrer |

## Dicas

- **Mesmo `altura_px` = mesmo porte.** Use os mesmos valores para todos os monstros do mesmo porte.
- **Poucos frames são o charme do pixel art:** parado 4, andar 6–8, atacar 5–6, morrer 6.
- **Evoluções e mutações:** duplique a config, troque o material ou a escala no Blender e mude o `nome`.
- **Gore:** a morte "normal" sai daqui. Cortes, explosões, queimaduras e derretimento são efeitos em tempo
  real na Unity, aplicados por cima desses mesmos sprites.
