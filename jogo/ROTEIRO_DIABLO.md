# Roteiro "Diablo 1 com idle"

Pedido do dono (26/11): deixar a jogabilidade extremamente parecida com o Diablo 1, mantendo o idle. Fontes consultadas:
Boris the Brave (geração de masmorras do Diablo 1), Wikipedia/Giant Bomb/DiabloWiki (estrutura do jogo), Maxroll e
battle.net clássico (pacotes de campeões/únicos).

## O que define a exploração no Diablo 1 (resumo da pesquisa)
- 16 andares em 4 temas (Catedral, Catacumbas, Cavernas, Inferno), cada tema com arquitetura, luz e monstros próprios.
- Catedral: espinha central de salas grandes + salas menores que "brotam" delas; paredes divisórias com arcos, grades e portas.
- Salas temáticas fechadas com objetos e monstros prontos (biblioteca com estante e velas, fosso de monstros com um item).
- Peças fixas de missão coladas no andar (Açougueiro, Rei Esqueleto...), no máximo uma por andar; as missões de cada jogo são sorteadas.
- Monstros do andar sorteados a cada jogo (poucos tipos por andar), em bandos; únicos com nome, cor própria e séquito.
- Objetos: barris, sarcófagos, baús, santuários de efeito incerto, estantes/livros, alavancas, armadilhas.
- Itens: ouro, inventário de espaço limitado, não identificados, durabilidade e reparo, vendedores na cidade.
- Personagem: atributos distribuídos por nível; luz limitada ao redor do herói, o resto do mapa escuro até ser explorado.

## Fatias (uma por ciclo, sempre com teste + portão + captura)
1. [feito] Hordas maiores em matilhas + pacotes de Campeões (azuis).
2. [feito] Únicos com nome próprio e séquito (nome dourado sobre a barra; 1-2 por andar a partir do andar 4), com prêmio garantido.
3. [feito] Objetos quebráveis dentro das salas: barris (ossos/explosivos), sarcófagos (esqueleto sai ou tesouro), urnas — os minions quebram ao passar.
4. [feito, parte 1: névoa por sala e raio de luz no cliente; falta inimigos invisíveis fora da luz dentro da própria sala] Luz e névoa: raio de luz ao redor do grupo; salas não visitadas escuras no mapa (silhueta), reveladas ao entrar; inimigos fora da luz invisíveis.
5. [feito: ouro + Ferreiro compra e vende; reparo vai com a fatia 6] Ouro como moeda própria (cai dos monstros/objetos) + Mercador da cidade: comprar, vender, reparar.
6. [feito: 6a não identificados + 6b durabilidade e reparo] Itens não identificados (identificar custa ouro ou pergaminho) e durabilidade com reparo.
7. [já existia: o inventário é uma grade limitada (Cfg.LarguraDoInventario x AlturaDoInventario) com itens de tamanhos diferentes] Inventário em grade de espaço limitado.
8. [feito] Atributos do necromante (Força/Magia/Destreza/Vitalidade) com pontos por nível.
9. [feito: Biblioteca, Fosso de monstros, Sala de tortura, Arsenal] Salas temáticas do Diablo 1: Biblioteca (livro = XP/feitiço), Fosso de monstros, Sala de tortura, Arsenal.
10. [feito] Santuários de efeito incerto: 10 tipos de nome críptico (Misterioso, Sombrio, Estranho, Reluzente, Oculto, Religioso, Mágico, Ornado, Tétrico, Profano), usar ou seguir adiante, efeito só revelado depois de usado e anotado no Livro dos Santuários. Fontes: diablo2.diablowiki.net/D1_Shrines, strategywiki, gamebanshee, valarguild.
11. [feito: tema a cada 4 andares com paleta, fogo, família da casa e decoração própria; Cavernas e Inferno com andares maiores e irregulares] Temas a cada 4 andares (Catedral, Catacumbas, Cavernas, Inferno) com formas de sala diferentes e bestiário por tema sorteado por jogo.
12. [feito: Açougueiro (andar 2) e Rei Esqueleto (andar 3), cada um sorteado por save (2 em 3), opcionais, com sala e recompensa próprias] Missões sorteadas por jogo com peça fixa (Açougueiro no andar 2, Rei Esqueleto no 3...).
13. [feito e medido: Normal, Pesadelo e Inferno; cada dificuldade é uma descida própria que continua a escala da anterior (+20 e +40 andares de força)] Dificuldades (Normal/Pesadelo/Inferno) com tabelas de drop próprias.
14. [feito na Fase 2, C5] Árvore de ascensão muito maior.
15. [feito: melhorar +1 a +5 e imbuir afixo com ouro no Ferreiro] Imbuir/melhorar armaduras com ouro.
Andares maiores (mais salas, formas irregulares) entram junto com 4 e 11: o dono agora pediu "mais profundidade nos mapas".


## Fase 2 — grandes mudanças (pedido do dono em 26/11: "quero grandes mudanças a partir de agora")

O dono escolheu três frentes: masmorra contínua, combate e feitiços, progressão longa.

### A. Masmorra contínua (o andar como um lugar só, não uma fila de arenas)
- A1. [feito] Salas ligadas: a luta atrai a matilha de uma sala vizinha, que entra pela porta; a sala de origem fica vazia.
- A2. [feito: portas emperradas e emboscada de corredor; falta desenhar a porta no mapa e armadilhas de corredor] Corredores com vida: inimigos e armadilhas nos corredores entre salas; portas fechadas que o grupo abre; emboscada ao abrir.
- A3. [etapa 1 feita: despertar por proximidade, a frente marcha sala adentro] Andar em grade única: o grupo anda pelo andar inteiro (sem troca de arena), monstros espalhados despertam por raio de luz e linha de visão. É a reescrita espacial do núcleo; fazer em etapas com o modo antigo atrás de uma chave de config até o portão passar.
- A4. [feito no cliente: adormecidos fora da luz são vultos escuros] Raio de luz de verdade: o que está fora da luz não aparece nem ataca à distância (fecha a parte 2 da fatia 4).
- A5. [Portal da Cidade feito, opcional; faltam escadas] Escadas para cima/baixo e portal da cidade: voltar ao acampamento no meio da descida e retomar do mesmo andar.

### B. Combate e feitiços
- B1. [feito] Livros de feitiço: caem na masmorra, cada leitura sobe o nível do feitiço (como no Diablo 1); exigem Magia.
- B2. [já existiam 6 feitiços com mana e ordens automáticas: Lança de Ossos, Drenar Vida, Explosão de Cadáver, Reerguer, Maldição, Sacrifício; falta feitiço novo: Muralha de Ossos, Golem, Praga] Barra de feitiços do necromante com mana: Flecha de Osso, Muralha de Ossos, Explosão de Cadáver, Golem, Maldição, Praga; automático por padrão, com prioridades escolhidas pelo jogador.
- B3. [feito: diabrete foge, carniçal devora cadáver, atirador recua; falta bestas cercarem pelos flancos] Comportamento por espécie: quem foge com pouca vida, quem cerca, quem atira e recua, quem carrega em linha.
- B4. [Golem feito] Mais minions: Golem (um só, forte), Magos esqueletos por elemento, Espectros.
- B5. Resistências e imunidades por dificuldade (imunes a fogo/raio/magia no Inferno).

### C. Progressão longa
- C1. [feito] Ferreiro: melhorar (+1 a +5) e imbuir itens com ouro (fatia 15).
- C2. [feito: 10 Únicos de nome e afixos fixos, tabela por dificuldade, Livro de Únicos e um efeito próprio para cada um] Itens únicos com nome e efeito próprio, por tabela de dificuldade.
- C3. [feito: aba "Cidade" com Curandeira, Bruxa (livros), Ferreiro e Ancião (dicas e identificar)] Cidade (Tristram do necromante): Ferreiro, Curandeira (poções), Bruxa (livros e cajados), Ancião (identifica), cada um com estoque que renova.
- C4. [feito: vida e mana, cinto de 3 de cada, uso e reposição automáticos; falta o jogador escolher os limiares] Poções e cinto: vida e mana, uso automático com regra do jogador.
- C5. [feito: 5 ramos de 8 nós (o quinto, Herança, acelera a vida seguinte: medido -13% na vida 2 e -22% na vida 3), Cinzas + Selos do Abismo, nó final por ramo] Árvore de ascensão muito maior (fatia 14), com ramos por estilo: Invocador, Ossos, Sangue, Pragas.

- D1. [feito no item 179] Ato 3 (andares 21-30) com chefe próprio: Arcebispo Lázarus (muralha de fogo inimiga, teleporte, Súcubos). Medido: 20,68 h na primeira vida.
- D2. [feito no item 180] Ato 4 com Diablo no andar 40 (Apocalipse) e desfecho de campanha por dificuldade. Medido: 43,07 h a campanha do Normal na primeira vida.
- D3. [feito no item 181] Diablo desenhado em vetor e anel do Apocalipse (o anel inteiro ainda não foi visto em captura).
