# Referência do Diablo 1 (regras e números)

Fonte: tabelas públicas do DevilutionX (`assets/txtdata`, reimplementação aberta do jogo). O dono tem o Diablo da GOG (pasta conferida em 27/11); o jogo original serve para ele jogar e comparar. Nada de arte, som, música ou texto do jogo entra no Necromante Idle: daqui só saem regras e números.

## Escala dos monstros (largura do quadro do sprite, em pixels)
- 96: morcegos e Hollow/Pain Master (pequenos).
- 128: quase todos — zumbis, esqueletos, Fallen, Hidden, cabras, Súcubos, Conselheiros/Magistrados, e também **o Açougueiro**.
- 160: os grandes — Rhinos, Gárgulas, Balrogs, Cavaleiros Negros, Serpentes, **o Rei Esqueleto e o próprio Diablo**.
- Conclusão: no original, o maior monstro tem só **1,25×** a largura de um comum, e os chefes não são desenhados maiores que um Balrog. Nossos chefes colossais (até 2-3× um comum) estão fora dessa proporção.

## Vida média dos monstros comuns por andar (Normal)
| andar | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| vida | 4 | 9 | 15 | 24 | 33 | 42 | 56 | 66 | 78 | 95 | 107 | 129 | 144 | 148 | 159 | 164 |
- Cresce rápido no começo (~60%/andar nos 3 primeiros), ~20%/andar no meio e quase para no fim (~4%/andar nos 4 últimos). O nosso jogo usa ~30%/andar o tempo todo, em 40 andares.

## Chefes e Únicos de missão (vida no Normal)
- Açougueiro 220 (andar 2) · Rei Esqueleto 240 (3) · Zhar 360 · Senhor da Guerra de Sangue 850 (13) · Lázarus 600 (15) · Diablo 1666 (16).
- Relação com os comuns do andar: Açougueiro ~25×, Rei ~16×, Lázarus ~4×, Diablo ~10×.

## Resistências e imunidades
- No original, a imunidade já existe no Normal (esqueletos e zumbis imunes a magia; demônios de lava imunes a fogo; Storm/Death Wing imunes a raio) e o Inferno usa uma tabela própria, mais dura. Nosso jogo só cria imunidade no Pesadelo/Inferno.

## Comparação da vida (item 191)
Crescimento da vida de um comum, andar N ÷ andar 1 (nosso = EscalaInimigo; D1 = média dos monstros do andar):

| andar | 2 | 4 | 6 | 8 | 10 | 12 | 14 | 16 |
|---|---|---|---|---|---|---|---|---|
| D1 | 2,2 | 6,0 | 10,5 | 16,5 | 23,8 | 32,2 | 37,0 | 41,0 |
| nosso | 1,3 | 2,2 | 4,6 | 11,1 | 26,8 | 32,4 | 55,1 | 93,9 |

Até o andar 12 a curva bate com a do D1 (começa mais suave, para a primeira expedição passar). Depois disso a nossa continua exponencial de propósito: o jogo tem 40 andares e progressão idle, o D1 tem 16.

Chefe ÷ comum do mesmo andar: D1 Açougueiro ~24×, Rei Esqueleto ~16×, Lázarus ~4×, Diablo ~10×. Nosso: Açougueiro 17,5× (elite 3,5 × 5), Rei 21×, chefes dos Atos 1 e 2 ~15-17× (proporcionais). Lázarus estava ~13-19× e o chefe do Ato 4 ~18-31× → vida base 560→300 e 900→520 (agora ~7× e ~11×). Tempo da campanha quase não muda: chefe do Ato 3 20,7 → 18,4 h; chefe do Ato 4 34,6 → 36,4 h na 1ª vida (dentro do ruído; a parede são os andares, não os chefes).

## Resistências por família (item 193)
No D1 (monstdat.tsv, coluna resistance do Normal): mortos-vivos quase todos IMUNES a magia (16 de 21), alguns resistem a fogo/raio; demônios quase todos resistem ou são imunes a magia (≈85%) e muitos a fogo; animais em geral sem resistência. "Magia" do D1 ≈ nossa Sombra (o dano do necromante). Aqui: mortos-vivos e demônios comuns ganharam 25% contra sombra (Cavaleiro Negro 30%); Pesadelo 45%, Inferno 60%, abaixo do limiar de imunidade (70%) para o necromante não ficar inútil. Fogo dos demônios (50%) já batia; feras seguem quase sem resistência.
