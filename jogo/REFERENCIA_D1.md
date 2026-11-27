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
