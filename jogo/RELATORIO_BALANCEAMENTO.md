# Relatório de balanceamento — Núcleo do Necromante

Gerado por `dotnet run --project Testes` (determinístico: mesmas sementes, mesmos números). 200 expedições por cenário.

Alvos de design: passar dos primeiros andares e empacar por volta do andar 6–10 sem melhorias; 30–60 s por andar.

Parâmetros globais usados: crescimento por andar 22.0% composto; inimigo comum = base da espécie × 0.30 (vida) / × 0.20 (dano); elite × 3.5 vida / × 1.4 dano; chefe × 1.8 vida / × 1.0 dano; orçamento de onda 6.0 + 0.40/andar + 1.0/onda; cura entre andares 60%.

## Números base das espécies (Ato 1)

| Espécie | Família | Porte | Papel | Vida | Dano | Vel. ataque | Armadura | Alcance | Vel. mov. | Tipo | 1º andar | Peso | Custo de onda |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Necromante | Espirito | Medio | Conjurador | 200 | 10.0 | 1.00 | 10 | 9.0 | 2.6 | Sombra | — | 0 | 1.0 |
| Esqueleto | MortoVivo | Pequeno | Lutador | 60 | 8.0 | 1.00 | 10 | 1.0 | 2.2 | Fisico | 1 | 10 | 1.0 |
| Arqueiro Ossudo | MortoVivo | Pequeno | Atirador | 40 | 7.0 | 0.80 | 0 | 6.0 | 2.0 | Fisico | 3 | 5 | 1.0 |
| Zumbi | MortoVivo | Pequeno | Tanque | 110 | 5.0 | 0.70 | 30 | 1.0 | 1.4 | Veneno | 1 | 5 | 1.0 |
| Cão de Cripta | Besta | Pequeno | Lutador | 50 | 9.0 | 1.30 | 5 | 1.0 | 3.2 | Fisico | 2 | 5 | 1.0 |
| Diabrete | Demonio | Pequeno | Enxame | 24 | 3.5 | 1.60 | 0 | 1.0 | 3.6 | Fogo | 4 | 6 | 0.5 |
| Acólito Caído | Aberracao | Medio | Conjurador | 90 | 12.0 | 0.45 | 5 | 6.0 | 1.8 | Sombra | 5 | 3 | 3.0 |
| A Abadessa Costurada | Aberracao | Colossal | Suporte | 700 | 24.0 | 0.70 | 60 | 1.8 | 1.5 | Fisico | 10 | 0 | 10.0 |

Os valores são os do minion nível 1 de qualidade Comum; o inimigo usa os mesmos números × fatores de inimigo × 1,09^(andar−1).

## Cenário 1 — Início de jogo (alvo principal)

Necromante nível 1 + 3 Esqueletos comuns nível 1, ordens padrão, modo Avançar a partir do andar 1, sem nenhuma melhoria entre expedições.

| Métrica | Valor |
|---|---|
| Expedições simuladas | 200 |
| Andar em que a expedição termina (média / mediana / p10–p90) | 5.2 / 5 / 5–6 |
| Duração média da expedição | 4.2 min |
| Tempo médio por andar concluído | 43.9 s |
| Almas ceifadas por hora | 35.34 |
| Orbes caídos / perdidos / ceifas interrompidas (por hora) | 35.69 / 0.00 / 0.22 |
| Itens caídos por hora (Comum / Mágico / Raro / Lendário / Profano) | 40.21 / 12.61 / 4.23 / 0.717 / 0.072 |
| Ossos / Sangue / Restos por hora | 2358 / 18.6 / 687 |
| XP por hora | 2765 |
| Inimigos mortos por expedição (elites) | 116.8 (1.24) |
| Minions perdidos por expedição | 7.39 |
| Overkills / Execuções por expedição | 39.50 / 0.06 |
| Expedições que venceram um chefe | 0.0% |

| Andar | Expedições que chegam | Morrem aqui | % das mortes | Tempo médio p/ concluir |
|---|---|---|---|---|
| 1 | 200 | 0 | 0.0% | 36.0 s |
| 2 | 200 | 0 | 0.0% | 36.4 s |
| 3 | 200 | 0 | 0.0% | 42.4 s |
| 4 | 200 | 12 | 6.0% | 54.5 s |
| 5 | 188 | 142 | 71.0% | 73.9 s |
| 6 | 46 | 46 | 23.0% | — |

**Leitura contra os alvos:**

- Passam dos 4 primeiros andares: 94.0% das expedições (mortes nos andares 1–4: 6.0%).
- Empacam (morrem) no andar 5: 71.0%; entre os andares 6 e 10: 23.0%.
- Andares com tempo médio dentro do alvo de 30–60 s: 1, 2, 3, 4. Fora do alvo: 5 (73.9 s).
- O tempo por andar não inclui a pausa de 3.0 s entre andares.

## Cenário 2 — Mesmo exército inicial, partindo da Ancoragem do andar 6

Mostra o que acontece depois de destravar o primeiro Ponto de Ancoragem sem ter melhorado nada.

| Métrica | Valor |
|---|---|
| Expedições simuladas | 200 |
| Andar em que a expedição termina (média / mediana / p10–p90) | 6.2 / 6 / 6–7 |
| Duração média da expedição | 1.1 min |
| Tempo médio por andar concluído | 67.3 s |
| Almas ceifadas por hora | 20.54 |
| Orbes caídos / perdidos / ceifas interrompidas (por hora) | 21.10 / 0.00 / 0.84 |
| Itens caídos por hora (Comum / Mágico / Raro / Lendário / Profano) | 25.60 / 8.44 / 3.94 / 0.000 / 0.281 |
| Ossos / Sangue / Restos por hora | 2761 / 0.0 / 69 |
| XP por hora | 3841 |
| Inimigos mortos por expedição (elites) | 22.4 (0.00) |
| Minions perdidos por expedição | 4.87 |
| Overkills / Execuções por expedição | 8.25 / 0.00 |
| Expedições que venceram um chefe | 0.0% |

| Andar | Expedições que chegam | Morrem aqui | % das mortes | Tempo médio p/ concluir |
|---|---|---|---|---|
| 6 | 200 | 164 | 82.0% | 67.3 s |
| 7 | 36 | 36 | 18.0% | — |

## Cenário 3 — Exército de Comando cheio (10), nível 8

Necromante nível 5; Zumbi, 3 Esqueletos, Cão de Cripta, 2 Arqueiros Ossudos e Acólito Caído, todos Comuns nível 8. A partir do andar 1.

| Métrica | Valor |
|---|---|
| Expedições simuladas | 200 |
| Andar em que a expedição termina (média / mediana / p10–p90) | 8.3 / 8 / 8–9 |
| Duração média da expedição | 4.8 min |
| Tempo médio por andar concluído | 30.0 s |
| Almas ceifadas por hora | 53.85 |
| Orbes caídos / perdidos / ceifas interrompidas (por hora) | 54.16 / 0.00 / 0.57 |
| Itens caídos por hora (Comum / Mágico / Raro / Lendário / Profano) | 60.58 / 21.89 / 5.72 / 0.629 / 0.252 |
| Ossos / Sangue / Restos por hora | 4793 / 27.1 / 917 |
| XP por hora | 6583 |
| Inimigos mortos por expedição (elites) | 206.1 (2.13) |
| Minions perdidos por expedição | 10.75 |
| Overkills / Execuções por expedição | 83.02 / 0.03 |
| Expedições que venceram um chefe | 0.0% |

| Andar | Expedições que chegam | Morrem aqui | % das mortes | Tempo médio p/ concluir |
|---|---|---|---|---|
| 1 | 200 | 0 | 0.0% | 25.0 s |
| 2 | 200 | 0 | 0.0% | 24.1 s |
| 3 | 200 | 0 | 0.0% | 24.5 s |
| 4 | 200 | 0 | 0.0% | 24.9 s |
| 5 | 200 | 0 | 0.0% | 27.6 s |
| 6 | 200 | 0 | 0.0% | 32.9 s |
| 7 | 200 | 2 | 1.0% | 41.0 s |
| 8 | 198 | 133 | 66.5% | 60.3 s |
| 9 | 65 | 65 | 32.5% | — |

## Cenário 4 — Exército avançado, partindo do andar 6

Necromante nível 12, Poder Necromântico +30%; mesma composição do cenário 3 em qualidade Vigorosa nível 20.

| Métrica | Valor |
|---|---|
| Expedições simuladas | 200 |
| Andar em que a expedição termina (média / mediana / p10–p90) | 12.1 / 12 / 12–13 |
| Duração média da expedição | 4.3 min |
| Tempo médio por andar concluído | 31.8 s |
| Almas ceifadas por hora | 68.13 |
| Orbes caídos / perdidos / ceifas interrompidas (por hora) | 68.41 / 0.00 / 0.56 |
| Itens caídos por hora (Comum / Mágico / Raro / Lendário / Profano) | 65.21 / 19.79 / 19.03 / 2.778 / 0.208 |
| Ossos / Sangue / Restos por hora | 6400 / 90.9 / 867 |
| XP por hora | 16897 |
| Inimigos mortos por expedição (elites) | 180.2 (1.51) |
| Minions perdidos por expedição | 14.03 |
| Overkills / Execuções por expedição | 70.30 / 0.04 |
| Expedições que venceram um chefe | 100.0% |

| Andar | Expedições que chegam | Morrem aqui | % das mortes | Tempo médio p/ concluir |
|---|---|---|---|---|
| 6 | 200 | 0 | 0.0% | 24.5 s |
| 7 | 200 | 0 | 0.0% | 25.3 s |
| 8 | 200 | 0 | 0.0% | 28.2 s |
| 9 | 200 | 0 | 0.0% | 30.7 s |
| 10 | 200 | 0 | 0.0% | 31.0 s |
| 11 | 200 | 12 | 6.0% | 47.8 s |
| 12 | 188 | 158 | 79.0% | 61.7 s |
| 13 | 30 | 30 | 15.0% | — |

## Cenário 5 — Modo Caçar no andar 3 (1 hora de jogo por execução)

Exército inicial repetindo o andar 3 por 1 hora simulada (10 execuções). No modo Caçar o exército é restaurado a cada repetição.

| Métrica | Valor |
|---|---|
| Expedições simuladas | 10 |
| Andar em que a expedição termina (média / mediana / p10–p90) | 3.0 / 3 / 3–3 |
| Duração média da expedição | 60.0 min |
| Tempo médio por andar concluído | 42.6 s |
| Almas ceifadas por hora | 37.80 |
| Orbes caídos / perdidos / ceifas interrompidas (por hora) | 37.80 / 0.00 / 0.00 |
| Itens caídos por hora (Comum / Mágico / Raro / Lendário / Profano) | 41.30 / 12.80 / 4.80 / 0.400 / 0.100 |
| Ossos / Sangue / Restos por hora | 2072 / 24.5 / 1368 |
| XP por hora | 2751 |
| Inimigos mortos por expedição (elites) | 1775.2 (24.40) |
| Minions perdidos por expedição | 165.40 |
| Overkills / Execuções por expedição | 224.00 / 0.10 |
| Expedições que venceram um chefe | 0.0% |
| Expedições cortadas pelo limite de tempo (necromante vivo) | 10 |

## Cenário 6 — Loot com auto-equipar ao longo de 20 expedições seguidas

40 campanhas. Cada campanha começa no novo jogo (necromante nível 1 + 3 Esqueletos) e faz 20 expedições no modo Avançar a partir do andar 1. Depois de cada expedição os itens caídos vão para o inventário e `Equipamento.AutoEquipar` troca o que tiver pontuação maior. **Só os itens passam de uma expedição para a outra** (XP, almas e ancoragem são descartados), para isolar o efeito do loot. O controle é a mesma semente rodada sem nenhum item (idêntico ao cenário 1).

| Expedição nº | Andar final com loot (média / mediana / p10–p90) | Andar final sem itens (média / mediana) | Itens equipados (média) | Pontuação do equipamento (média) | Tempo médio por andar | Venceu o chefe |
|---|---|---|---|---|---|---|
| 1 | 5.3 / 5 / 5–6 | 5.3 / 5 | 0.0 | 0.0 | 44.3 s | 0% |
| 2 | 5.4 / 5 / 5–6 | 5.2 / 5 | 2.7 | 3.5 | 44.7 s | 0% |
| 3 | 5.8 / 6 / 5–7 | 5.4 / 5 | 5.0 | 7.1 | 45.9 s | 0% |
| 4 | 6.1 / 6 / 5–7 | 5.0 / 5 | 6.5 | 10.3 | 46.1 s | 0% |
| 5 | 6.8 / 7 / 6–9 | 5.1 / 5 | 7.5 | 13.9 | 46.3 s | 0% |
| 6 | 6.9 / 7 / 6–8 | 5.3 / 5 | 8.2 | 16.7 | 46.1 s | 0% |
| 7 | 7.1 / 7 / 6–8 | 5.1 / 5 | 8.6 | 18.8 | 47.0 s | 0% |
| 8 | 7.3 / 7 / 6–8 | 5.0 / 5 | 8.7 | 20.7 | 47.4 s | 0% |
| 9 | 7.4 / 7 / 6–9 | 5.1 / 5 | 8.8 | 22.8 | 46.0 s | 0% |
| 10 | 7.4 / 7 / 6–8 | 5.0 / 5 | 8.9 | 24.1 | 44.0 s | 3% |
| 11 | 8.1 / 8 / 7–9 | 5.2 / 5 | 9.0 | 25.8 | 46.0 s | 3% |
| 12 | 7.9 / 8 / 7–10 | 5.2 / 5 | 9.0 | 28.2 | 46.7 s | 3% |
| 13 | 8.1 / 8 / 7–10 | 5.1 / 5 | 9.0 | 29.6 | 45.0 s | 3% |
| 14 | 8.1 / 8 / 7–9 | 5.3 / 5 | 9.0 | 30.9 | 45.8 s | 3% |
| 15 | 8.2 / 8 / 7–9 | 5.2 / 5 | 9.0 | 32.5 | 44.8 s | 3% |
| 16 | 8.2 / 8 / 7–10 | 5.3 / 5 | 9.0 | 33.8 | 44.6 s | 0% |
| 17 | 8.5 / 8 / 7–10 | 5.0 / 5 | 9.0 | 35.3 | 46.2 s | 3% |
| 18 | 8.6 / 9 / 7–10 | 5.1 / 5 | 9.0 | 36.2 | 45.6 s | 3% |
| 19 | 8.5 / 9 / 7–10 | 5.2 / 5 | 9.0 | 37.5 | 44.8 s | 3% |
| 20 | 8.7 / 8 / 8–10 | 5.2 / 5 | 9.0 | 38.2 | 45.5 s | 5% |

**Leitura:**

- Na 1ª expedição (sem nada equipado) o andar final médio é 5.3; na 20ª, com o que caiu nas anteriores, é 8.7 (controle sem itens na mesma semente: 5.2). Ganho do loot: **+3.5 andares**, mediana 5 → 8.
- Tempo de jogo por campanha: 122.5 min com loot (81.7 min no controle).
- Itens que não couberam ou perderam a vaga no inventário de 40 (desmanchados em Essência): 74.6 por campanha.
- Lendários equipados ao fim das 40 campanhas: 33 (Foice do Ceifeiro Faminto ×4, Grimório da Mãe Carniça ×3, Anel do Rei Solitário ×5, Coroa da Legião Muda ×11, Manto do Último Suspiro ×3, Amuleto da Maré Rubra ×7).

| Raridade | Itens caídos | Por hora de jogo | Por campanha | Observado | Design |
|---|---|---|---|---|---|
| Comum | 3419 | 41.87 | 85.48 | 69.14% | 70.0% |
| Magico | 1080 | 13.23 | 27.00 | 21.84% | 22.0% |
| Raro | 393 | 4.81 | 9.83 | 7.95% | 7.0% |
| Lendario | 49 | 0.600 | 1.23 | 0.99% | 0.9% |
| Profano | 4 | 0.049 | 0.10 | 0.08% | 0.1% |
| **Total** | 4945 | 60.56 | 123.63 | | |

Chances de drop usadas: inimigo comum 3.0%, elite 35.0%, chefe garante 2. Faixas dos afixos: 15.5% composto por nível de item (escala plena) ou +5.0% linear (taxas e chances).

## Raridade dos itens caídos (cenários 1 a 5 somados)

| Raridade | Quantidade | Observado | Design |
|---|---|---|---|
| Comum | 2967 | 66.02% | 70.0% |
| Magico | 967 | 21.52% | 22.0% |
| Raro | 486 | 10.81% | 7.0% |
| Lendario | 64 | 1.42% | 0.9% |
| Profano | 10 | 0.22% | 0.1% |

O observado fica um pouco acima do design nas raridades altas porque o chefe garante um item no mínimo Raro na primeira vitória.

## Qualidade das almas ceifadas (todos os cenários somados)

| Qualidade | Quantidade | Observado | Design |
|---|---|---|---|
| Fraca | 1614 | 58.0% | 60% |
| Comum | 708 | 25.5% | 25% |
| Vigorosa | 303 | 10.9% | 10% |
| Ancestral | 127 | 4.6% | 4% |
| Profana | 29 | 1.0% | 1% |

## Desempenho do executor sem tela

- 2080866 ticks simulados em 6.14 s de CPU (339 mil ticks/s, ou ~33902× o tempo real) neste computador, build Debug, uma thread.
- Esse número varia por máquina; o restante do relatório não.

