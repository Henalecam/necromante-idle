# Necromante Idle — projeto do jogo

Estado: protótipo jogável do núcleo. A simulação está pronta e testada; a camada visual da Unity está
escrita, mas ainda não foi aberta numa Unity de verdade (ver "O que foi e o que não foi testado").

## Pastas

```
necro-jogo/
  Nucleo/        simulação em C# puro (combate, ondas, almas, Comando, feitiços, eventos de gore)
  Testes/        48 testes + relatório de balanceamento (RELATORIO_BALANCEAMENTO.md)
  Replay/        exporta uma expedição real, tick a tick, para virar vídeo de prévia
  UnityProjeto/Assets/Necro/
    Nucleo/      cópia do núcleo para a Unity
    Visual/      sprites de 8 direções, ponte simulação -> tela, gore, sangue no chão, cena automática
    Editor/      importador dos sprites do pipeline do Blender
    Shaders/     SpriteGore (corte, estilhaço, fogo, gelo, veneno, clarão)
    Resources/   sprites dos 6 monstros e a cripta, já renderizados
```

## Rodar o núcleo (sem Unity)

Precisa do .NET 8 SDK.

```
dotnet run --project Testes                 # testes + relatório de balanceamento
dotnet run --project Testes -- --so-testes
dotnet run --project Replay -- replay.txt 1003 1500 1    # saida, semente, ticks, andar inicial
python3 ../necro-pipeline/ferramentas/replay_gif.py replay.txt previa.mp4 1080 1480
```

## Balanceamento (rodar depois de qualquer mudança)

```
dotnet run --project Testes -- --metas          # campanha completa por um jogador-robô; falha se sair das metas
dotnet run --project Testes -- --campanha 24 14 # o mesmo, com 24 campanhas de 14 h (gera METAS_BALANCEAMENTO.md)
dotnet run --project Testes -- --diagnostico    # de onde vem o poder do jogador (desliga uma fonte por vez)
dotnet run --project Testes -- --calibrar       # busca em grade nos botões principais
dotnet run --project Testes -- --experimentos 200   # variantes de regra (ceifa, overkill)
```

## Abrir na Unity

1. Instale a Unity 6.3 LTS pelo Unity Hub, com o módulo Web Build Support.
2. Crie um projeto novo com o modelo **Universal 2D**.
3. Copie a pasta `UnityProjeto/Assets/Necro` para dentro da pasta `Assets` do projeto novo.
4. Ao voltar para a Unity, os sprites são fatiados sozinhos (ou use o menu **Necro > Importar sprites do pipeline**).
5. Abra uma cena vazia e aperte **Play**. A cena se monta sozinha: cripta, câmera, necromante e 3 esqueletos.
   Teclas 1, 2 e 3 mudam a velocidade (1x, 2x, 4x); N abre a Necrópole e E o equipamento.

Se algo não compilar ou aparecer errado, me mande a mensagem do Console: esses scripts ainda não rodaram na Unity.

## O que foi e o que não foi testado

- **Testado aqui:** o núcleo (compila em .NET Standard 2.1 / C# 9, 48 testes passam, relatório de
  balanceamento gerado), o pipeline do Blender e o vídeo de prévia feito a partir da simulação real.
- **Só verificado por compilação contra "stubs" da UnityEngine:** tudo em `Visual/`. Tipos e sintaxe
  conferem, mas o comportamento na tela (shader de gore, sangue na RenderTexture, ordenação) não foi visto.
- **Não verificado:** `Editor/ImportadorSprites.cs` (usa APIs do editor) e o shader.

## Números atuais

Ver `METAS_BALANCEAMENTO.md` (curva de progressão da campanha completa) e `RELATORIO_BALANCEAMENTO.md` (cenários isolados).
