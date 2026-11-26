# Plano dos ciclos de melhoria

Cada ciclo: gerar -> olhar o resultado -> criticar -> corrigir. O que já foi feito e o que vem a seguir.

## Feito

1. Pipeline Blender -> sprites (8 direções, pivô fixo nos pés, metadados para a Unity).
2. Fábrica de monstros por código: esqueleto, arqueiro ossudo, zumbi, diabrete, acólito caído, necromante, abadessa.
   - Crítica 1: metal laranja demais, olhos apagados -> ferrugem escurecida, olhos para fora da órbita.
   - Crítica 2: zumbi "boneco de salsicha", sangue vermelho-brinquedo -> juntas, carne e sangue escurecidos.
   - Crítica 3: manto do necromante era um cone liso, cajado de ponta-cabeça -> pregas no tecido, cajado corrigido.
   - Crítica 4: véu da abadessa era uma placa dura -> véu em tubo de tecido, touca cobrindo a nuca.
3. Estilo: pixel art 44 px descartado; sprites pré-renderizados de ~96 px (estilo Diablo 1/2).
4. Cripta: lajes pequenas e escuras, parede com pilares, nichos de crânios, braseiros.
5. Núcleo C#: simulação completa do Ato 1, 48 testes, relatório de balanceamento.
6. Hordas: dobro de inimigos, metade da vida e do dano.
7. Prévia em vídeo a partir da simulação real.
8. Projeto Unity: importador, animador de 8 direções, ponte, gore, sangue, cena automática.

9. Cão de Cripta: rig quadrúpede, galope, mordida com bote, morte tombando de lado.
   - Crítica: patas dianteiras soltas das pernas, ferida parecia um balão vermelho -> carpo ligando a pata, ferida achatada e escura.
10. Projéteis: a flecha do Atirador agora viaja e o dano só chega no impacto (teste novo, 49 de 49 passando; balanceamento não mudou).
11. Todas as 8 espécies do Ato 1 têm sprite próprio; nenhum substituto.

12. Carne podre refeita: necrose, crosta e rasgos que mostram carne viva; zumbi com pele solta, vértebras e machadinha
    cravada; sangue fosco em vez de vermelho brilhante. Zumbi e Abadessa re-renderizados.
13. Esqueleto de elite (campeão): ossos enegrecidos, olhos rubros, elmo com chifres, capa e machado duplo. Elites usam
    o sprite "<especie>_elite" quando existe.
14. Lança de Ossos virou projétil perfurante visível (o dano acontece quando a lança passa pelo inimigo).
15. Modo de experimentos (`dotnet run --project Testes -- --experimentos 200`): ceifa de perto e limiar de overkill
    medidos. Resultado: andar até o orbe não cria tensão sozinho; overkill a 100% reduz explosões de 47% para 29%.
    As duas variantes ficam desligadas por padrão (Config.AlcanceCeifa, Config.LimiarOverkill).

16. Loot no núcleo: 9 espaços, 5 raridades, 22 afixos, 6 lendários que mudam mecânica, 1 conjunto, inventário,
    auto-equipar. 72 testes passando.
17. BALANCEAMENTO COMO PARTE DO DESENVOLVIMENTO (pedido do usuário):
    - `Testes/Campanha.cs`: jogador-robô joga a campanha inteira (tudo persiste) e mede a curva de progressão.
    - `dotnet run --project Testes -- --metas` compara com as metas de design e devolve erro se algo sair da faixa.
      Rodar depois de QUALQUER mudança de mecânica ou número. `--diagnostico` mostra de onde vem o poder;
      `--calibrar` faz busca em grade nos botões principais.
    - Achado: com tudo ligado a progressão estava ~5x rápida demais (chefe do Ato 1 em 0,5 h; andar 46 em 12 h),
      puxada pelos itens, que cresciam 9% por andar igual aos inimigos.
    - Calibragem aplicada: inimigos 18%/andar (era 9%), itens 12%/andar, XP pela metade, chefe vida x3 (era x4)
      e dano x1,3 (era x1,7). Config anterior guardada em /home/claude/tmp/Config.antes_calibragem.cs.
    - Resultado (24 campanhas): 6 de 7 metas na faixa; andar 15 sai em 2,37 h (meta 2,5 a 6 h).
    - Efeito colateral: jogo novo sem melhorias agora empaca no andar 5 (antes 8).
18. Sombra projetada embutida no sprite (testada no esqueleto). Falta re-renderizar os outros 8 monstros.
19. Variante "almas atraem inimigos durante a ceifa": medida, quase sem efeito; desligada por padrão.

20. Paredes de chefe suavizadas (ciclo 5): novo `DegrauPorAto` (x1,1 a cada Ato), inimigos 22%/andar, itens 15,5%/andar,
    chefe vida x1,8 e dano x1,0, cura da poça 0,6%/s (era 1,2%), fúria aos 240 s (era 150), cura entre andares 60%
    (era 35%). Nova meta "maior espera num único andar" (<= 35% do tempo até o andar 20): caiu de 58% para 20%.
    Resultado (24 campanhas): 7 de 8 metas. Fora: duração da expedição (2,5 min; meta 3 a 20).
    Testado: ancoragem a cada 10 andares resolve a duração (4,65 min) mas recria a parede do chefe 2 (13,3 h).
    `--cfg Campo=valor,...` permite testar números sem editar o código.

21. Necrópole no núcleo (ciclo 6): Altar de Erguimento (erguer custa Essência; o Altar barateia), Cripta (+1 Comando a cada
    5 níveis), Torre das Almas (+2 espaços de Filactério e +3% de chance de alma por nível; abre no andar 5). Custos
    x1,15 por nível. O jogador-robô paga para erguer e compra a melhoria mais barata. 74 testes.
22. `AndaresDeRecuo = 2`: o Avançar parte 2 andares antes do Ponto de Ancoragem (aquecimento). Resolveu a duração da
    expedição (3,65 min) sem recriar parede. RESULTADO: 8 de 8 metas na faixa (24 campanhas de 14 h).
    Falta da Necrópole: Forja de Ossos, Altar de Costura, Fosso de Carne, Biblioteca, Portal (dependem de sistemas
    ainda não feitos: criação de itens, fusão, Restos, Ordens extras).

23. Ciclo 7 — balanceamento: nova meta "andar em que termina a primeira expedição" (6 a 10). Para cumpri-la sem
    regredir: inimigos-base mais fracos (vida x0,24, dano x0,16), crescimento 25%/andar (itens 17,5%), sem degrau
    por Ato, chefe vida x2,1; Altar 2,5x mais caro. RESULTADO: 9 de 9 metas (24 campanhas). 74 testes.
24. Interface provisória na Unity (`Visual/Interface.cs`, IMGUI): barra de recursos, vida/mana, painel da Necrópole
    (construções, erguer almas, ativar/desativar minions; tecla N) e de Equipamento (equipar, tirar, desmanchar,
    auto-equipar; tecla E). Conferida só por compilação contra stubs.

## Próximos ciclos (em ordem)

0. Sempre: rodar `--metas` ao fim de cada ciclo e corrigir o que sair da faixa antes de seguir.
0b. Re-renderizar com sombra (~40 min cada; esqueleto, diabrete e caodecripta já feitos): arqueiroossudo, acolitocaido, zumbi, necromante,
    esqueleto_elite, abadessacosturada (um por vez) e copiar para a pasta Resources da Unity.

2. Reiluminar todos os monstros com mais contraste (luz principal mais forte, ambiente mais fraco) e re-renderizar.
   Elites das outras espécies. Sombra projetada no chão vinda do render.
3. Inimigos atacarem o necromante enquanto ele canaliza a ceifa (variante desligada por padrão, medir).
4. Ceifa com deslocamento (se aprovada) e limite de overkill (se aprovado).
5. Ato 2: cenário (catacumbas alagadas?) e 6 espécies novas.
6. Interface: barra de vida/mana, lista de Ordens, tela da Necrópole, Filactério.
7. Loot: itens, afixos e lendários no núcleo.
8. Sons: foley de gore e ambiente da cripta.

## Ciclo 7 (fechamento) — versão de navegador
25. **Roda no navegador.** `Web/`: núcleo compilado para WebAssembly + cliente em canvas com os sprites (não é o WebGL da Unity).
    Testado em Chrome sem tela (celular 390 px e desktop): carrega, luta, salva, recarrega, recupera 1 h ausente em ~3 s
    (16 mil ticks/s). Publicado como artefato; **não foi possível conferir o carregamento dentro do claude.ai** (página privada).
26. Arqueiro Ossudo re-renderizado com sombra (4 de 9 conjuntos prontos).

Próximos: sombras de acolitocaido, zumbi, necromante, esqueleto_elite, abadessacosturada; atualizar o documento de design
com a versão web; na web, mostrar o saque da expedição em curso e tingir mortes por condição; construções que faltam; Ascensão.

## Ciclo 8
27. **Necrópole: Ossuário e Mesa de Dissecação.** Ossuário +5% de Ossos/nível (andar 3, máx. 30, 180 Ossos); Mesa +4% de
    Essência ao desmanchar/dissolver por nível (andar 8, máx. 25, 250 Ossos + 15 Ess.). Com 3% o Ossuário deixava a
    "maior parede" em 0,34 (limite 0,35); com 5% fica em 0,27. Metas 9/9 (chefe Ato 1 1,01 h — rente ao piso de 1 h;
    chefe Ato 2 9,43 h). 75 testes. Saves antigos (3 construções) continuam válidos.
    Atenção: Altar 20/20 e Ossuário 29/30 ao fim de 14 h — as construções estão acabando cedo; rever níveis máximos ou custo.
    Obs.: `--cfg` precisa vir ANTES de `--metas` na linha de comando; o relatório é gravado na pasta de onde se roda.
28. Web: saque da expedição em curso no canto da arena; cadáveres tingidos pela condição da morte; painel já lista as 5 construções.
    Artefato republicado (versão 1795613949-7b54). Documento de design atualizado (rev 42).
29. Acólito Caído re-renderizado com sombra (5 de 9).

Próximos: sombras de zumbi, necromante, esqueleto_elite, abadessacosturada; folga na meta do chefe do Ato 1 (1,01 h);
níveis máximos das construções; Ascensão (prestígio); ordens/posturas na interface web; som.

## Virada de rumo (retorno do dono do projeto, ciclo 8)
"Muito vazio, sem sentido, falta mecânica e atividade que prenda." Diagnóstico e plano no documento de design
(seção "Diagnóstico de gameplay e plano de profundidade"). Ordem: 1 missões e desbloqueios · 2 mão do jogador no combate
(toque para ceifar, feitiços de toque) · 3 almas com profundidade (fusão, evolução ramificada, bestiário) · 4 encruzilhadas
(pactos a cada 5 andares) · 5 talentos do necromante · 6 Ascensão e rituais com tempo real.
Regra: o robô passa a usar cada sistema novo e as metas são medidas a cada passo.

30. **Missões (item 1 do plano) — feito.** `Nucleo/Missoes.cs`: cadeia de 17 missões com história (Ato 1 e começo do Ato 2),
    recompensa em Ossos/Essência, resgate manual (vale também no meio da expedição, sem contar em dobro) e desbloqueio
    das abas Exército/Necrópole/Equipamento (1/3/5 missões). `ResultadoExpedicao.MortesPorEspecie` novo. Robô resgata tudo.
    Metas 9/9 (chefe Ato 1 1,03 h; Ato 2 9,82 h). 76 testes. Web: cartão de missão com barra e botão Resgatar; testado
    jogando as 7 primeiras no Chrome sem tela. Artefato na versão 1795617015-af8d. Falta levar o cartão para a Interface.cs da Unity.

Próximo: item 2 (toque para ceifar com bônus; feitiços de toque), depois item 3.

## Ciclo 9 — itens 2, 3 (parcial) e 4 do plano de profundidade
31. **Mão do jogador (item 2).** `Simulacao.Jogador.cs`: toque no orbe = ceifa imediata, 25% de subir uma qualidade (até Ancestral);
    Detonar (até 8 cadáveres, recarga 25 s); Frenesi (+35% dano dos minions por 6 s, recarga 40 s). Sem mana. Comandos em fila,
    valem no tick seguinte (determinístico). Frenesi entrou no lugar do "sacrifício de toque" (Sacrifício já é feitiço automático).
    Robô ganhou `--ativo`. Web: toque na arena, dois botões com recarga; a câmera do celular agora segue a briga e os orbes
    (antes a luta ficava fora do quadro em tela estreita).
32. **Fusão (item 3).** `Fusao.cs`: 3 almas iguais -> 1 de qualidade acima, herda afixos; custo 2/6/20/80 Ess. Robô funde quando
    o banco passa do Comando (`--sem-fusao` desliga). Web: bloco "Fusão de almas", traços das almas com descrição, barra de XP,
    botão Evoluir (a evolução de estágio já existia no núcleo e não aparecia).
33. **Bestiário (item 3).** `Bestiario.cs`: mortes por espécie acumulam no save; marcos 100/400/1500/5000/15000 -> Domínio;
    +3% vida e dano dos minions da espécie por nível. Aba nova na web.
34. **Rebalanceamento.** Fusão + Bestiário derrubaram o chefe do Ato 1 para 0,80 h (portão reprovou). Mexer na vida do chefe criava
    parede (0,39). Solução: CrescimentoPorAndar 0,26 e CrescimentoItemPorAndar 0,18. Ausente: 9/9, chefe 1 em 1,06 h, chefe 2 em 9,89 h.
35. **Encruzilhadas (item 4).** Pactos em `Simulacao.Jogador.cs` + `DadosPacto`: do andar 5 em diante, a cada 2 andares da expedição,
    3 ofertas por 25 s, sorteadas em fluxo próprio de Rng (ignorar = mesma simulação, com teste de hash). Seis pactos.
    Robô ativo com critério: chefe 1 em 0,86 h (19% mais rápido), chefe 2 em 9,73 h (2%). Aceitar sempre o ofensivo PIORA (10,4 h).
    80 testes. Artefato na versão 1795620187-0442; documento rev 46.

Pendências: vantagem do jogador ativo some no fim de jogo (rever pactos/poderes para escalar); piso da meta do chefe 1 (1,06 h rente a 1 h);
níveis máximos das construções; Interface.cs da Unity ainda sem missões, poderes, fusão, bestiário e pactos (só a web tem);
evolução ramificada por espécie (pede modelos). Próximos: item 5 (talentos), item 6 (Ascensão e rituais), sombras que faltam
(necromante, esqueleto_elite, abadessacosturada).

## Repositório Git (pedido do dono do projeto)
Repositório local pronto em `/home/claude/necromante-idle` (jogo/, pipeline/, docs/ = build web para GitHub Pages, README, primeiro commit).
Falta existir no GitHub: a sessão não cria repositórios, só acessa os já existentes. Quando o dono criar `Henalecam/necromante-idle` vazio:
add_repo (push) → `git remote add origin` → `git push -u origin main`. A cada ciclo: copiar necro-jogo e necro-pipeline para lá
(sem bin/obj/dist), copiar Web/dist para docs/, trocar em jogo/Web/montar.sh o caminho absoluto do pipeline por `../../pipeline/saida`, commit e push.

## Ciclo 10 — talentos + apontamentos do dono (inventário, foice, sacrifício, ficha, marca)
36. **Talentos (item 5).** `Talentos.cs`: 15 talentos em 3 ramos, 1 ponto a cada 2 níveis (nível 10 em ~1 h, ~120 em 14 h), pré-requisito por
    pontos no ramo, redistribuir por 10 Ess./ponto. Somam no agregado `BonusEquipamento` (campos novos VidaDoNecromantePercentual, ToqueMelhoraAlma).
37. **Inventário em grade.** `Equipamento.Tamanho/Arrumar/Cabe`: grade 10x8, itens com tamanho; cheio = não cabe. Bug achado pelo portão: item
    grande melhor era desmanchado porque tirar 1 item pequeno não abria espaço (chefe 2 "não alcançado"); agora desmancha vários piores até caber.
    Web: gaveta à esquerda com grade, ícones por tipo, ficha do item, regra "desmanchar abaixo de".
38. **Foice = corpo a corpo.** `NecromanteCorpoACorpo`: alcance 1,9, +40% dano, +25% vida, +15 armadura, 1 alvo extra; avança no inimigo mais próximo.
    Valores iniciais (+100%/+40%/2 alvos) estouraram as metas.
39. **Sacrifício.** `Sacrificio.cs`: Fragmentos de Alma, Imbuir (15/60/240/900), regra automática só na reserva (`SacrificarAbaixoDe`).
40. **Marca do Ceifador.** Toque num inimigo: minions priorizam, +25% dano aliado nele, 10 s.
41. **Web/visual.** Ficha do minion (retrato animado, atributos reais, traços, ações), barra do chefe, clarão de impacto, almas voando até o
    necromante, penumbra com tocha, aura do Frenesi, HUD com Sangue e Fragmentos.
42. **Balanceamento.** Alavancas novas: `InclinacaoFimDeAto` 0,065 (andares 7 a 10 de cada Ato) e `DegrauPorAto` 1,2. Poderes de toque mais fortes
    (Detonar x2 / 20 s; Frenesi +50% por 8 s / 30 s). Ausente 9/9: chefe 1 1,15 h; andar 15 2,92 h; chefe 2 8,25 h. Ativo: 0,95 h; 2,54 h; 8,67 h —
    a vantagem do ativo NÃO existe no fim de jogo (ruído de ±0,5 h; pactos custam caro em expedição longa). 85 testes.
43. **Arte.** Variante `necromante_foice` no pipeline (foice de guerra + animação de ataque); retrato conferido, sprites ainda não renderizados.

Pedidos do dono ainda abertos: visual do necromante conforme o equipamento (renderizar necromante_foice e trocar o sprite na web/Unity; depois
elmo/armadura); mais recursos (dar uso a Sangue e aos 7 Restos); mais inteligência (tática de inimigos e minions); "mais mecânicas, mais visual,
mais interação, mais profundidade"; Git parado a pedido dele (repo local em /home/claude/necromante-idle desatualizado desde o 1º commit).
Próximos: render necromante_foice → sprite por arma; Restos/Sangue (oferendas e batismo); IA tática; Ascensão; rituais; Ato 2 com inimigos novos.
44. **Oferendas e Ritos de Sangue.** `Oferendas.cs`: os 7 Restos de cadáver alimentam 7 bônus permanentes (20 Restos x 1,3^nível, teto 20);
    Sangue compra um rito para a próxima expedição (Pressa 15, Colheita 20, Banquete 20). Robô oferta sempre e prepara Banquete. 9/9:
    chefe 1 1,14 h; andar 15 3,03 h; chefe 2 8,63 h; expedição média 3,27 min (rente ao piso de 3). 86 testes.
45. Render do necromante com sombra ABORTADO: com os testes rodando junto, levaria ~2 h (36 s por quadro). Folha `parado` restaurada do zip
    anterior. Refazer num ciclo sem carga de CPU em paralelo, e depois o necromante_foice.

## Entre ciclos 10 e 11 — pedidos do dono (14 h)
46. **Travar e vender tudo.** `Item.Bloqueado`; `Equipamento.DesmancharTudo/ValorDeTudo`; travado nunca sai para abrir espaço. Web: Travar/Destravar
    na ficha, selo "travado" na grade, "Vender tudo" com teto de raridade, prévia e confirmação.
47. **Arte dos equipamentos.** `pipeline/blender/gerar_itens.py`: um modelo 3D procedural por tipo (10), ícone com o tamanho do item na grade
    (128 px por espaço), ~8 s para todos. Saída em saida/itens; usados na web (arte/itens) e copiados para Resources/Itens da Unity.
    Fraquezas: luva com dedos soltos da palma; uma só arte por tipo (sem variação por raridade/nível além do brilho colorido).
48. **Conteúdo.** +6 missões (toque, fusão, pactos, sacrifício, oferendas, Domínio) = 23; +4 pactos = 10. Atenção: missões de toque e de pacto
    travam a cadeia para quem joga 100% ausente.
49. **Oferendas automáticas** (pedido: recurso único para aquele upgrade, não há escolha): `OferendasAutomaticas` (padrão ligado), sobem ao fim de
    cada expedição; na web viraram barras de progresso.
50. **Ambiente vivo (web).** Marcas no chão por tipo de morte (queimado, congelado, putrefato, eletrizado, oco), pilhas de ossos onde cadáveres somem,
    crateras e clarão de explosão, brasas e poeira no ar, névoa rasteira, tom do andar (vermelho nos andares 6 a 9, pulso no chefe, verde-frio no Ato 2);
    as marcas desbotam pela metade a cada andar em vez de sumir.
    Portão: 9/9 (chefe 1 1,09 h; andar 15 2,84 h; chefe 2 8,17 h). 87 testes. Artefato 1795626701-b741.
Ainda aberto do dono: equipamento mudando o visual do personagem (falta render do necromante_foice e variantes de elmo/armadura), mais conteúdo
(inimigos e Ato 2 próprios), mais inteligência, mais impacto no ambiente (cenário reagindo: tochas, portões, altares).

## Ciclo 11
51. **BUG (relatado pelo dono): Abadessa ativa não entrava em campo.** `CriarMinions` usava `ComandoMaximo + Bonus.Comando`, sem a Cripta; a Necrópole
    usava `ComandoMaximoEfetivo`. Os últimos ativos da lista ficavam de fora, calados. A Cripta nunca valeu no combate. Corrigido + teste.
    `Progressao.AjustarAoComando`: se o teto cai (equipamento/talentos), os últimos ativos saem de campo também na tela.
52. **Tática.** Golpe Brutal de elites/chefes (aviso 1,4 s, raio 2,3, 2x dano, recarga 11 s; marcar durante o aviso interrompe e atordoa 2 s);
    foco dos minions no mais ferido (raio 1,6); postura do exército na web.
53. **Ascensão.** `Ascensao.cs`: depois da chefe 1; Cinzas = (maior andar/5)²; 3 Legados (Ossos +6%, Sangue +4% Poder, Sombras +6% alma; custo nível+1);
    itens travados atravessam. O robô não ascende (as metas medem a primeira vida).
54. **4 inimigos novos** (ids 8 a 11): Carniçal, Diabo de Guerra, Aparição, Colosso de Ossos. Web: sprite provisório (existente, recolorido e
    escalado) via `PROVISORIO`; some sozinho quando existir arte/<especie>. Fábrica: variantes prontas em monstros.py (carnical, diabodeguerra,
    aparicao, colossodeossos) — falta gerar modelo, conferir retrato e renderizar.
55. **Balanceamento.** Com a Cripta valendo: DegrauPorAto 1,35 e InclinacaoFimDeAto 0,075. Ausente 9/9: chefe 1 1,02 h (rente), andar 15 2,62 h,
    chefe 2 8,55 h. Ativo (antes dos inimigos novos): 0,88 / 2,45 / 7,52 h — a vantagem no fim de jogo voltou (12%), graças à interrupção de golpes.
    91 testes. Artefato 1795628838-dca4. A página carrega no claude.ai (o dono está jogando).
Fila de render (um por vez, sem carga em paralelo): necromante_foice (em curso) → carnical → diabodeguerra → aparicao → colossodeossos →
necromante (sombra) → esqueleto_elite, abadessacosturada (sombra). Depois: elmo/armadura visíveis; itens com variação por raridade.

## Ciclo 11 (continuação) — registro
56. HUD redesenhado (chips de recurso, barra de XP, Poder × "andar pede", ajuda de primeira vez, selos nas abas), cores por qualidade/ameaça.
57. Sopro Profano, feitiços de item (Raio Sombrio, Ceifada, Chuva de Ossos, Escudo de Almas) e conjuração automática com penalidade 1,5×.
58. Dons aleatórios das almas (vida/dano/velocidade), Poder calculado (itens, minions, necromante, total), presságios a partir do andar 7.
59. Sorte + painel de cheats para teste (recursos, cinzas, atributos, almas, missão).
60. Aba Combate: dano causado, dano tankado, cura e abates por aliado; dano por tipo de inimigo; expedição atual e anterior.
61. Exército: filtro (campo/reserva, família, papel, qualidade) e ordem (Poder, nível, qualidade, dano/s, vida, espécie); filactério por qualidade.
Decisão com o dono: prioridade = clareza do HUD; estilo = meio a meio. Git em pausa a pedido.
Pendências: render necromante_foice (em curso), 4 espécies novas com sprite provisório, medir portão ativo após dons/presságios/Sorte, Interface.cs do Unity sem os sistemas novos.

## Ciclo 12 — rework: masmorra explorada + layout em três regiões
62. Núcleo `Simulacao.Masmorra.cs`: cada andar é uma sequência de salas (combates = ondas; entre eles Baú, Santuário, Almas presas, Fonte, Emboscada; baú pode ser mímico ou dourado). Fase `Explorando`; `ComandoInteragir` (toque rende +50%).
63. Sala do chefe com ambiente sorteado (Lava, Névoa pútrida, Geada), valendo para os dois lados.
64. Nível secreto "Pasto Profano" (2,5% por andar comum a partir do 5; cheat `secreto`): horda de Reses Profanas (espécie 12, capturável) + Rei do Pasto; paga Ossos, Sangue e 2 itens.
65. Portão 9/9 após ajuste: CrescimentoPorAndar 0,27, ChefeMultVida 2,35, BauChanceDeItem 0,30, FonteCura 0,20, BencaoForca 0,10. Medido: andar 33,8 s; expedição 3,08 min; chefe Ato 1 1,05 h (8 tentativas); andar 15 2,85 h; chefe Ato 2 7,89 h. Margens finas em expedição média, chefe Ato 1 (1,05 h, piso 1) e tentativas (8, teto 8).
66. Web: troca de sala com chão correndo, faixa de salas, objetos desenhados, avisos de lava, gelo, tom por ambiente, diário da descida (aba Masmorra).
67. Layout: esquerda = Exército/Inventário; direita = Combate/Masmorra/Bestiário; embaixo = Necrópole/Talentos. No celular os lados são gavetas abertas pela doca.
Pendências: arte própria da Rês Profana e dos objetos de sala; mais eventos (mercador, altar de escolha), segundo nível secreto; missões ligadas a salas; medir portão ativo; Unity Interface.cs.
68. Salas Mercador (troca Ossos da expedição por item; toque dá 40% de desconto) e Altar de sangue (15% da vida por +80% de chance de alma no andar; toque isenta). Portão 9/9 com CrescimentoPorAndar 0,275, ChefeMultVida 2,45, DegrauPorAto 1,42 (chefe Ato 1 em 1,01 h — no limite).
69. Arte necromante_foice completa (morrer renderizado à parte e encaixado no quadro 288x252), publicada.
Dono pediu ciclos a cada 5 minutos.

## Ciclo 13 — vista isométrica, mapa sorteado, Git
70. Missões "Saque" (3 baús) e "Frestas" (10 salas especiais), com migração de saves (`Missoes.Migrar`).
71. Mapa do andar: `Simulacao.Mapa` (uma célula por sala numa grade, árvore sem sobreposição), câmaras vazias (`TipoSala.Vazia`), antecâmaras antes do chefe; o grupo visita todas as salas antes de descer.
72. Web: sala isométrica 2:1 gerada no navegador (lajes, duas paredes, tochas, portas conforme o mapa), câmera segue o grupo nos dois eixos, minimapa no canto e mapa inteiro em modal e na aba Masmorra.
73. Tira de retratos do grupo com vida e toque para curar; resumo da expedição com salas.
74. Arte: Carniçal com sprites próprios. Diabo de Guerra em render.
75. Portão 9/9: FatorVidaInimigo 0,245, ChefeMultVida 2,2, CrescimentoPorAndar 0,275, DegrauPorAto 1,42. Medido: andar 39,2 s; expedição 3,43 min; chefe Ato 1 1,19 h (8 tentativas, teto 8); andar 15 2,99 h; chefe Ato 2 8,90 h.
76. Git retomado: repositório Henalecam/necromante-idle.
77. Salas decoradas por tipo (selo do chefe, tapetes, moedas, runas, entulho, grades), pilares e estandartes, escada na última sala, saída acesa.
78. Troca de sala: o grupo caminha até a porta de saída, a tela escurece e ele entra pela porta oposta da sala seguinte.
79. Névoa no mapa (salas distantes aparecem como '?'), lições de toque uma por vez (orbe, objeto, golpe, cura, marca, detonar, mapa). Doc de design atualizado com o ciclo 13.
80. Segundo nível secreto (Câmara do Tesouro: três guardiões de elite, 3 itens com 15% de Lendário) e ambiente de chefe Desabamento. Portão 9/9 (7 tentativas no chefe do Ato 1; chefe 1,15 h; expedição 3,55 min). Render retomável por folha (render_sprites.py pula folhas já prontas).
81. Som sintetizado (golpes, mortes, explosões, almas, baús, bênçãos, portas, chefe, perigo, nível secreto, fim) com botão ♪ para ligar/desligar.
82. Sala Armadilha (8% da vida de todos; o toque desarma e rende Ossos). Portão 9/9 (andar 37,5 s; chefe Ato 1 1,12 h, 7 tentativas).
83. Santuário oferece 3 de 5 bênçãos (Fúria, Proteção, Colheita, Fartura, Devoção); o jogador escolhe em 4 s (vem 50% mais forte) ou sai uma ao acaso. Alívio inicial (andares 1 a 5 com escala 0,88) para a primeira expedição passar da âncora com folga. Portão 9/9: FatorVidaInimigo 0,24, InclinacaoFimDeAto 0,085; chefe Ato 1 1,11 h (7 tentativas), andar 15 2,88 h, chefe Ato 2 9,11 h.
84. Campeões: cada elite recebe nome e epíteto pelos modificadores, barra de vida própria no topo e entradas no diário. Corrigida a transformação da câmera depois da barra do chefe (faltava o eixo vertical).
85. Arte: Diabo de Guerra com sprites próprios (render com superamostragem 2 para caber num ciclo). Aparição em render.
86. Cenário próprio do Ato 2 (criptas alagadas: pedra esverdeada, poças, raízes nas paredes, fogo-fátuo no lugar das tochas).
87. Arte: Aparição com sprites próprios (com tom azul espectral aplicado no cliente). Variante Rês Profana escrita na fábrica; Colosso de Ossos em render.
88. Arte: Colosso de Ossos com sprites próprios. Rês Profana em render.
89. Arte: Rês Profana com sprites próprios. Todas as 12 espécies têm arte própria.
90. Chefe próprio do Ato 2: o Carcereiro de Almas (acorrenta os 2 minions de mais vida por 2 s a cada 9 s e chama Aparições, até 3). Sprite provisório (Aparição grande e roxa). Portão 9/9 (chefe Ato 2 em 9,75 h). 101 testes.
91. Encruzilhada recolhida (uma linha, toque para abrir) enquanto há chefe ou campeão em cena. Modelo do Carcereiro de Almas na fábrica (manto negro-violeta, correntes, cajado-gaiola); em render.
92. REWORK pedido pelo dono: o andar inteiro é um só mundo isométrico à vista (todas as salas nas suas células, corredores entre as portas, rótulo do que há em cada sala). O grupo caminha pelos corredores de sala em sala (núcleo: TempoDeViagem pelo caminho da árvore, 0,8 s + 0,7 s por passagem, teto 2,9 s), sem escurecer; só a troca de andar escurece. Botão ⤢ alterna Perto / Médio / Andar inteiro (padrão: andar inteiro; figuras ampliadas nessa vista). Sem névoa. Portão 9/9: andar 41,6 s; expedição 3,94 min; chefe Ato 1 1,20 h (8 tentativas, teto); andar 15 3,37 h; chefe Ato 2 10,94 h.
93. Arte própria do Carcereiro de Almas (manto negro-violeta, correntes, cajado-gaiola).
94. Arena: arrastar move a vista, pinça/roda do mouse dá zoom contínuo, ⤢ recentra; o toque passou a agir ao soltar (toque curto sem arrasto).
95. Travessia em fila: cada aliado parte com um pequeno atraso e o grupo se espreme para o eixo do corredor, em vez de atravessar em bloco.
96. Inimigos já postos nas salas desde o começo do andar (núcleo: PlanejarInimigos/PlanoDasOndas; despertam um a um no lugar onde estavam, em vez de sair de portais) e objetos das salas especiais visíveis de longe. Portão 9/9: andar 39,2 s; chefe Ato 1 1,10 h (8 tentativas); andar 15 3,14 h; chefe Ato 2 10,35 h.
97. Setas douradas correndo pelos corredores até a próxima sala, fio verde nos corredores já percorridos, anel pulsando na próxima sala; adormecidos com a tinta certa.
98. Pedido do dono: mapa mais 'quadrado'. ColocarNoMapa agora escolhe a célula que deixa o andar mais próximo de um bloco (menor caixa, mais vizinhas, preferência pela sala anterior); toda sala vizinha na grade é ligada por corredor e o grupo anda pelo caminho mais curto (busca em largura no núcleo e na web). Portão 9/9: andar 37,7 s; chefe Ato 1 1,09 h (8 tentativas); andar 15 3,11 h; chefe Ato 2 10,58 h.
99. Fim do andar: o grupo anda até a escada da última sala e some por ela; no andar novo entra em fila pela porta. Escada também na sala do chefe.
100. Mapa mais compacto (PASSO 29x21) e figuras maiores na vista do andar inteiro em telas estreitas; avisos de dica duplicados removidos (ficam as lições).
101. Rota do jogador: tocar no símbolo de uma sala ainda não visitada faz dela a próxima (núcleo: ComandoIrPara reordena Salas/Mapa e as ondas pré-postas; a última sala fica sempre por último). Não muda o balanceamento do robô (ele não usa). 102 testes.
102. Rótulos das salas mostram o que cada uma guarda: número de inimigos à espera (e campeão) nas de combate; nome e efeito nas especiais (a dica de efeito aparece na próxima sala ou em telas largas).
103. Cofre trancado (TipoSala.Cofre): a chave fica com os inimigos de uma sala de combate comum, marcada no mapa; na ordem sorteada o cofre pode vir antes e ficar fechado — mudar a rota resolve. Paga 3x os Ossos de um baú e um item (40% Raro). Portão 9/9: andar 37,8 s; chefe Ato 1 1,10 h (8 tentativas); andar 15 3,03 h; chefe Ato 2 9,99 h. 103 testes.
104. Inimigos adormecidos encaram o grupo onde quer que ele esteja. README do repositório com capturas do mapa, dos rótulos e da descida.
105. Cheat 'Cofre em todo andar' (Config.ForcarCofre) para testar; conferido na tela: mapa marca a sala da chave e o cofre, 'Chave do cofre encontrada!' e 'Cofre aberto!' aparecem. O caso trancado segue coberto só pelo teste do núcleo.
106. Duas missões no fim da cadeia: Cartógrafo (escolher a rota 3 vezes) e Arrombador (abrir 1 cofre). Contadores RotasEscolhidas e CofresAbertos. Sem efeito no portão (vêm depois do chefe do Ato 2).
107. Patrulha errante (andar 6+, 30% dos andares comuns): 3 inimigos que mudam de sala de combate a cada 9 s; se estiverem na sala quando a onda começa, entram na luta e, vencidos, deixam um item Mágico. Visível no mapa e no rótulo da sala. Portão 9/9: chefe Ato 1 1,04 h (7 tentativas); andar 15 2,91 h; chefe Ato 2 8,77 h.
108. A patrulha anda pelos corredores (de centro em centro de sala, pelo caminho mais curto) em vez de cortar em linha reta.
109. Teste do núcleo para a patrulha (104 testes). Doc de design com o ciclo 17 (rota, cofre, patrulha). README com capturas do cofre e da patrulha.
110. Medição do jogador ativo (robô que toca em tudo e escolhe rota: chave antes do cofre, santuário cedo): chefe do Ato 1 em 0,58 h (ausente 1,04 h), andar 15 em 1,80 h (2,91 h), chefe do Ato 2 em 5,32 h (8,77 h), 4 tentativas no chefe do Ato 1 (7). O ativo perfeito anda ~40% mais rápido; é o teto do 'meio a meio'.
111. Conferido na tela: 'Cofre trancado: a chave estava em outra sala' (cofre antes da chave) e 'Cofre aberto!' no mesmo teste, sem erros.
112. Habilidades de espécie, lote 1 (uma passiva por espécie de minion): Esqueleto "Aparar" (ignora o primeiro golpe de cada sala), Zumbi "Miasma" (ao cair, explosão de veneno em volta), Cão de Cripta "Sede" (+25% de velocidade de ataque por 4 s depois de matar). Aparecem na ficha do minion e no combate ("Aparou!", "Sede!"). Teste do núcleo (105 testes). O exército ficou mais forte e o chefe do Ato 1 caiu para 0,87 h; compensado com InclinacaoFimDeAto 0,085 -> 0,1 e ChefeMultVida 2,2 -> 2,4. Portão 9/9: andar 38,2 s, expedição 3,53 min, chefe Ato 1 1,06 h (7 tentativas), andar 15 2,71 h, chefe Ato 2 8,78 h. Ativo: 0,56 h / 1,56 h / 5,27 h (vantagem ~40%, igual a antes).
113. Habilidades de espécie, lote 2: Arqueiro Ossudo "Olho Morto" (+30% em alvo com vida cheia), Diabrete "Matilha" (+8% por outro Diabrete vivo, até 3), Acólito Caído "Agouro" (golpe enfraquece o alvo por 2,5 s, não chefes), Carniçal "Banquete" (cura 15% da vida ao matar, evento Extra 18, número verde na tela). Texto na ficha. 106 testes. Portão 9/9 sem mexer na dificuldade, mas com o chefe do Ato 1 em 1,00 h (no piso; 7 tentativas), andar 15 2,61 h, chefe Ato 2 8,51 h. Ativo 0,58 / 1,57 / 5,16 h. Variantes testadas para dar folga (FatorVidaInimigo 0,247; Inclinação 0,112; Inclinação 0,105 + chefe 2,5) não melhoraram de forma clara: o lote 3 vai precisar de um reajuste junto.
114. Folga no balanceamento depois dos dois lotes de habilidades: CrescimentoPorAndar 0,275 -> 0,28 e InclinacaoFimDeAto 0,1 -> 0,095 (melhor de quatro variantes; as outras estouravam a maior espera ou deixavam o chefe no piso). Portão 9/9: chefe Ato 1 1,10 h (7 tentativas), andar 15 3,08 h, chefe Ato 2 9,02 h, maior espera 0,27, andar 5 em 0,05 h (no piso, observar). Ativo 0,56 / 1,61 / 5,52 h. A habilidade da espécie agora aparece também em cada linha da lista do exército.
115. Habilidades de espécie, lote 3 (todas as espécies de minion têm passiva agora): Diabo de Guerra "Fúria de Guerra" (+25% de dano abaixo de metade da vida), Aparição "Incorpórea" (ignora 1 a cada 4 golpes, evento Extra 19), Colosso de Ossos "Baluarte" (os outros minions sofrem 10% menos dano enquanto ele vive), Rês Profana "Investida" (primeiro golpe de cada sala em dobro). 107 testes. Portão 9/9 sem reajuste: chefe Ato 1 1,10 h (7 tentativas), andar 15 3,07 h, chefe Ato 2 9,28 h, maior espera 0,28. Ativo 0,56 / 1,64 / 5,53 h (espera 0,35, no teto do ativo — observar).
116. Bestiário mostra o retrato de cada espécie e a habilidade que ela ganha quando erguida como minion (só web; núcleo intocado, portão do item 115 segue valendo). README com a lista das 11 habilidades.
117. Vista "Andar inteiro" corrigida: as salas de baixo ficavam escondidas atrás dos botões e avisos, e a cada andar novo a câmera demorava a enquadrar (mapa cortado à esquerda). Agora o enquadramento reserva a faixa da base e salta direto para o mapa completo ao chegar num andar. Conferido por captura em 1280 e 390 px. Sobre a ordenação por profundidade entre salas vizinhas: conferido por captura, as paredes mal alcançam a sala de trás (o vão do corredor é da altura da parede); tirado da fila. Só web. Pendente de conferência visual: aba Bestiário com retratos (fica trancada no começo do jogo; usar /home/claude/tmp/foto.py com um save avançado).
118. Conjuntos de itens: o jogo já tinha um conjunto Profano (Vestes do Coveiro) mas a tela não mostrava nada dele. Agora a ficha de uma peça mostra o conjunto, quantas peças estão equipadas, a lista de peças e os bônus de 2 e 3 peças (apagados enquanto não valem). Segundo conjunto: Garras do Carniceiro (Luvas, Amuleto, Anel): 2 peças +12% dano dos minions; 3 peças +8% velocidade de ataque e +3% roubo de vida dos minions. Teste no núcleo. Botão "+ peças de conjunto" no painel de testes. Conferido por captura. Portão 9/9: chefe Ato 1 1,10 h, andar 15 3,07 h, chefe Ato 2 9,33 h, espera 0,29; ativo 0,56 / 1,61 / 5,42 h.
119. Inventário: peças de conjunto levam um losango roxo na grade; ao selecionar uma, as outras do mesmo conjunto acendem. Equipar avisa "Conjunto: bônus de N peças ativo!" (com som) ou "1 de 3 peças". Conferido por captura. Também conferida por captura a aba Bestiário com retratos e habilidades (ok) e a habilidade na lista do exército (ok). Só web.
120. Sala nova: Forja de Ossos (TipoSala 14). Cada aliado vivo sai com uma couraça de ossos (Escudo) de 25% da vida máxima; o toque reforça como nas outras salas. Tirada da fatia do Baú no sorteio (Baú 28% -> 22%, Forja 6%). Objeto próprio no mapa (bigorna com brasa), rótulo, dica, aviso e diário. Teste no núcleo (108). Portão 9/9: chefe Ato 1 1,08 h (7 tentativas), andar 15 2,88 h, chefe Ato 2 8,50 h, espera 0,24; ativo 0,56 / 1,68 / 5,50 h. Na captura o aviso da sala apareceu e sem erros, mas o desenho da bigorna ficou atrás do painel de encruzilhada: FALTA conferir o desenho do objeto por captura (python3 /home/claude/tmp/foto_forja.py, ajustar para capturar antes de resolver). Observado: avisos de sala se sobrepõem ao painel de Encruzilhada quando coincidem.
121. Conferido por captura (zoom perto) o objeto da Forja de Ossos: brasa, ossos e bigorna aparecem; bigorna clareada e com contorno. Com o painel de Encruzilhada ou de Bênçãos aberto: o aviso de sala desce para 64% da cena (não fica mais por cima do painel) e, nos zooms Perto/Médio, a câmera baixa a ação para ela continuar à vista sob o painel (antes o painel cobria a luta por até 10 s). O deslocamento da câmera com painel aberto NÃO foi conferido por captura (a captura não pegou uma encruzilhada aberta). Gancho de teste window.__objetoTipo(). Só web.
122. Couraça/escudo visível: a ponte manda a fração de escudo de cada unidade (campo 16 da linha E); faixa clara acima da barra de vida na cena (a barra aparece mesmo com vida cheia enquanto houver escudo) e nos retratos do grupo. Vale para a Forja de Ossos e para o Escudo de Almas. Conferido por captura depois de uma Forja. Câmera com Encruzilhada aberta conferida por captura: 0,74 empurrava a luta para baixo dos botões; ajustado para 0,67 (este valor final não foi recapturado). Simulação intocada (portão do item 120 vale).
123. Sala nova: Ninho (TipoSala 15), tirada de parte da fatia da Emboscada (Emboscada 11% -> 6%, Ninho 5%). Com toque, os ovos são esmagados e rendem Ossos (BauOssos x 1,5). Sem toque, as crias chocam: 4 Diabretes (+1 a cada 6 andares, até 8) e a sala vira luta, com prêmio menor (x0,4). Rótulo, dica, aviso, diário e objeto (ovos pulsando; cascas depois). Teste no núcleo nos dois modos (109). Primeira versão (3 crias, prêmio cheio) derrubou o chefe do Ato 1 para 0,98 h; corrigido. Portão 9/9: chefe Ato 1 1,05 h (6 tentativas), andar 15 2,89 h, chefe Ato 2 8,79 h, espera 0,28; ativo 0,61 / 1,60 / 5,22 h. Captura: rótulo "Ninho" no mapa inteiro ok; o desenho dos ovos de perto NÃO foi visto com nitidez (câmera não assenta a tempo no avanço rápido) — conferir de perto.
124. Ferramenta de captura de perto: gancho window.__focar() (assenta a câmera no próximo quadro) e /home/claude/tmp/perto.py "<condição js>" <png> [espera_ms] [ticks por passo] (recorta só a cena; usar ticks 4 e espera ~2300 para pegar uma sala especial antes de resolver; com passos de 100 ticks a detecção atrasa até 10 s de jogo e as posições desenhadas ficam defasadas). Conferido de perto: ovos do Ninho (brilham, legíveis), cascas e crias depois de chocar com o aviso "Os ovos chocaram!", e a câmera com a Encruzilhada aberta no fator 0,67 (a luta fica entre o painel e a faixa de dica). Nada a corrigir. README com Forja, Ninho e conjuntos + capturas novas. Jogo: só o gancho de teste mudou.
125. Portas que abrem: grade levadiça na porta de saída da sala atual quando a saída fica numa das paredes do fundo (oeste/norte). Fechada enquanto houver inimigo vivo ou adormecido na sala; sobe (com som) quando a sala é limpa. Saídas da frente (leste/sul) não têm parede, ficam como estavam. Gancho window.__grade(). Conferido de perto por captura: grade fechada em combate, porta aberta com o grupo passando. Só web. Pendente: perto.py agora tem limite de 6000 passos e deve ser chamado com timeout (uma condição nunca satisfeita travou o ciclo por 10 min).
126. Terceiro nível secreto: Cripta dos Espelhos (NivelSecreto 3). Um reflexo inimigo de cada minion vivo do exército (mesma espécie, até 6; o primeiro vem como elite com modificadores; sem minions, uma Aparição de elite). Sorteio do secreto: Pasto 40%, Câmara 35%, Espelhos 25% (antes 50/50). Tesouro como o do Pasto (Ossos + 2 itens, um Raro). Reflexos desenhados em azul sombrio; aviso, diário; painel de testes com um botão por nível secreto (Cfg.ForcarSecreto). Teste no núcleo (110). Conferido de perto por captura. Portão 9/9: chefe Ato 1 1,06 h (7 tentativas), andar 15 2,83 h, chefe Ato 2 8,80 h, espera 0,30; ativo 0,57 / 1,83 / 5,59 h.
127. Início do roteiro "Diablo 1 com idle" (ver ROTEIRO_DIABLO.md, com a pesquisa). Fatia 1: hordas 1,6x maiores (cada comum com vida e dano /1,6 e rendendo /1,6 em Ossos, XP, alma e item; restos de Ossos acumulam em vez de arredondar para cima), comuns agrupados em até 3 matilhas por sala, e pacotes de 3 Campeões da mesma espécie (30% das salas comuns a partir do andar 3; vida x2,2, dano x1,25, prêmio x2,5; azuis). Como dano em área rende mais contra hordas, FatorVidaInimigo 0,24 -> 0,275. 111 testes. Portão 9/9 NO LIMITE: chefe Ato 1 1,06 h, tentativas 8 (teto 8), espera 0,34 (teto 0,35), andar 15 2,55 h (piso 2,5), chefe Ato 2 8,94 h; ativo 0,58 / 1,46 / 5,04 h. Conferido por captura (campeões azuis). Precisa de um ciclo de folga.
128. Roteiro Diablo, fatia 2: Únicos. A partir do andar 4, metade dos andares tem um monstro único numa sala de combate sorteada (nunca a do chefe nem a da elite): nome próprio sorteado (NomesDeUnicos: "Rói-Medula, o Maldito"), elite com modificadores e vida x1,35, séquito de 4 da mesma espécie (vida x1,3, sem o desconto da horda), e sempre larga um item ao menos Mágico. Ponte: linha "U id nome"; cliente: nome dourado sobre o único (legível em qualquer zoom), aviso e diário ao surgir e ao cair. Corrigido no caminho: o bloco do nome tinha entrado antes da definição de "prov" e quebrava o quadro (pego pela captura). Reajuste do portão: CrescimentoPorAndar 0,28 -> 0,29, InclinacaoFimDeAto 0,095 -> 0,105, ChefeMultVida 2,4 -> 2,2, UnicoCusto 0,8. 112 testes. Portão 9/9: chefe Ato 1 1,02 h (8 tentativas, no teto), andar 15 2,79 h, chefe Ato 2 9,48 h, espera 0,30; ativo 0,55 / 1,55 / 5,49 h. Ainda sem folga no chefe do Ato 1.
129. Roteiro Diablo, fatia 3: objetos quebráveis (Simulacao.Objetos.cs). Cada sala de combate (menos a do chefe) recebe 3 a 5 objetos sorteados no começo do andar, perto de onde os bandos esperam: barril (45%, às vezes Ossos), barril explosivo (15%, fere 35% da vida dos inimigos comuns em volta, 8% de elites/chefes e 6% dos aliados), urna (20%, Ossos e 4% de item) e sarcófago (20%: 45% um Esqueleto se levanta e entra na luta, senão tesouro). Quebram quando um aliado passa a 1 unidade, ou pelo toque do jogador durante a luta (toque garante os Ossos do barril e dobra a chance de item da urna). Ponte: linha Q e Quebrar(i); cliente desenha os objetos em todas as salas (inteiros e destroços), partículas, números e aviso "O morto se levanta!". Robô ativo quebra um objeto a cada 2,5 s. Três testes antigos passaram a rodar sem objetos (mediam só combate/sorte). Reajuste: AlivioInicial 0,88 -> 0,84, InclinacaoFimDeAto 0,105 -> 0,112. 113 testes. Portão 9/9 com folga: chefe Ato 1 1,06 h (7 tentativas), andar 15 3,14 h, chefe Ato 2 9,36 h, espera 0,30; ativo 0,57 / 1,69 / 5,55 h. Conferido por captura de perto.
130. Roteiro Diablo, fatia 4 (parte 1, só web): luz e névoa. O traçado do andar inteiro continua visível (pedido antigo do dono: ver o mapa completo), mas salas ainda não vistas ficam escuras, com rótulo "?" e sem mostrar inimigos, objetos nem tipo; uma sala se revela quando o grupo entra nela ou numa vizinha (e fica revelada); o covil do chefe sempre se conhece; salas limpas ficam à meia-luz. Raio de luz ao redor do necromante escurece o que está longe. Minimapa e aviso de rota respeitam a névoa (dá para mandar o grupo a uma "sala desconhecida"). Conferido por captura num andar de 9 salas. Observado: no mapa inteiro, a sala mais ao norte de um andar de 9 salas fica com o rótulo cortado no topo.
131. Roteiro Diablo, fatia 5: Ouro e Ferreiro (Nucleo/Ferreiro.cs). Ouro cai de todo inimigo (por porte, x4 elite, x25 chefe, com o desconto da horda e o prêmio dos campeões), de barris, urnas, sarcófagos com tesouro e baús; entra no progresso ao fim da expedição (EstadoProgresso.Ouro; zera na ascensão). Ferreiro (aba nova embaixo, abre junto com o Inventário): 4 itens à venda do nível do andar mais fundo vencido (3 Mágicos + 1 Raro), estoque novo a cada expedição; vender do inventário por ouro (3/10/40/200/1000 por raridade, +25% por nível); comprar custa 12x a venda. Na ficha do item, "Desmanchar" (Essência) e "Vender" (ouro) agora são botões separados. Ficha de Ouro no topo. O robô do portão compra tudo o que o ouro alcança. Reajuste: LojaMargem 12, InclinacaoFimDeAto 0,112 -> 0,12. 114 testes. Portão 9/9: chefe Ato 1 1,10 h (7 tentativas), andar 15 3,00 h, chefe Ato 2 8,53 h, espera 0,27; ativo 0,56 / 1,68 / 5,36 h. Conferido por captura (1280 e 390 px), com venda e compra feitas no navegador (4046 + 60 - 176 = 3930). Notas: o inventário já era uma grade limitada (fatia 7 já existia); com inventário cheio o botão mostra "Inventário cheio".
132. Roteiro Diablo, fatia 6a: itens não identificados. Raros, Lendários e Profanos caem fechados (Item.NaoIdentificado; saves antigos e itens da loja continuam identificados): mostram só tipo, raridade e nível, não equipam (PodeEquipar/AutoEquipar os ignoram) e vendem por 40% do valor. Identificar custa ouro (60/200/600 por raridade, +10% por nível): botão na ficha do item, e no Ferreiro um bloco "Identificar" com lista e "Identificar tudo". Na grade, o item fechado fica escuro com um "?". O robô identifica tudo antes de comprar. Rótulos corrigidos: "Desmanchar tudo" (Essência) deixou de se chamar "Vender". Reajuste: CrescimentoPorAndar 0,29 -> 0,293. 115 testes. Portão 9/9: chefe Ato 1 1,09 h (7 tentativas), andar 15 3,00 h, chefe Ato 2 9,23 h, espera 0,29; ativo 0,59 / 1,64 / 5,35 h. Capturas em 1280 e 390 px. Observado: num save avançado sem auto-desmanche o inventário enche de Raros fechados e a loja mostra "Inventário cheio" — considerar um aviso/atalho "vender os fechados".
133. Roteiro Diablo, fatia 6b: durabilidade e reparo. Item.Desgaste (0 = novo); durabilidade máxima 30 + 10 por raridade; cada expedição gasta 3 de tudo o que está equipado; com durabilidade zero o item quebra e não dá bônus nenhum (CalcularBonus o pula) até o reparo. Reparar no Ferreiro custa até 60% do valor de venda, proporcional ao desgaste; "Reparar tudo" conserta do mais gasto ao menos gasto. Por ser idle: reparo automático LIGADO por padrão (ao voltar de cada descida o Ferreiro conserta o que o ouro alcançar; botão para desligar). Tela: barrinha de durabilidade em cada vaga do boneco, ✖ no quebrado, linha de durabilidade e botão "Reparar" na ficha, bloco "Reparar" no Ferreiro. Atalho "Vender fechados" no bloco de identificação. Reajuste: ChefeMultVida 2,2 -> 2,05, InclinacaoFimDeAto 0,12 -> 0,13 (a espera máxima tinha ido a 0,37). 116 testes. Portão 9/9: chefe Ato 1 1,07 h (7 tentativas), andar 15 3,01 h, chefe Ato 2 9,54 h, espera 0,27; ativo 0,59 / 1,68 / 5,60 h. Capturas em 1280 e 390 px.
134. Roteiro Diablo, fatia 8: atributos (Nucleo/Atributos.cs). Força, Magia, Destreza e Vitalidade; 5 pontos por nível do necromante (níveis já ganhos contam). Por ponto: Força +0,4% de dano dos minions; Magia +0,6% de dano de feitiços e +1 de mana; Destreza +0,08% de crítico e +0,15% de velocidade de ataque dos minions; Vitalidade +0,4% de vida dos minions e +0,6% de vida do necromante. Entram no mesmo agregado de bônus dos talentos. Distribuição automática ligada por padrão (o ponto novo vai para o atributo mais baixo); dá para desligar e usar +1/+5; "Redistribuir" devolve tudo por Essência e passa para manual. Zeram na ascensão. Painel no topo da aba Talentos. O robô distribui por igual. Reajuste: CrescimentoPorAndar 0,293 -> 0,298, InclinacaoFimDeAto 0,13 -> 0,14. 117 testes. Portão 9/9: chefe Ato 1 1,09 h (7 tentativas), andar 15 2,82 h, chefe Ato 2 7,88 h, espera 0,27; ativo 0,61 / 1,62 / 4,85 h. Capturas em 390 e 1280 px.
135. Roteiro Diablo, fatia 9a: salas temáticas. Biblioteca (TipoSala 16, 5% das salas especiais, tirada da Fonte): estante com velas e um tomo no atril; o necromante lê e ganha XP (30 x a escala do andar; +50% com o toque). Fosso de Monstros (17, 4%, tirado das Almas Presas): horda de uma espécie só (1,4x o orçamento de uma sala, versão de horda, até 26), que ao cair deixa um item ao menos Mágico e Ossos em dobro. Rótulos, dicas, avisos, diário e legenda do mapa. Botão/cheat de teste "sala" (Cfg.ForcarSalaEspecial) para forçar um tipo logo depois da primeira sala de combate. 118 testes. Portão 9/9: chefe Ato 1 1,06 h (7 tentativas), andar 15 2,60 h (piso 2,5: pouca folga), chefe Ato 2 7,80 h, espera 0,26; ativo 0,56 / 1,56 / 4,80 h. Conferidas de perto por captura.
136. Roteiro Diablo, fatia 9b: Sala de Tortura (TipoSala 18, 3% das salas especiais, tirada do Altar de Sangue): trave com correntes e um corpo pendurado; dá 2 de Sangue (3 com o toque). Arsenal (19, 3%, tirado do Mercador): suporte de armas, escudo e manequim de armadura; dá uma arma ou peça de armadura ao menos Mágica (Rara, e por isso fechada, se você tocar). Rótulos, dicas, avisos, diário, legenda. A ideia de deixar cadáveres prontos na Sala de Tortura ficou de fora: os cadáveres da simulação nascem de uma entidade morta e somem entre salas. Folga no portão: CrescimentoPorAndar 0,298 -> 0,303. 119 testes. Portão 9/9: chefe Ato 1 1,16 h (6 tentativas), andar 15 2,86 h, chefe Ato 2 8,13 h, espera 0,26; ativo 0,63 / 1,62 / 4,89 h. As duas conferidas de perto por captura.
137. Roteiro Diablo, fatia 10: santuários de efeito incerto. A sala Santuário deixa de oferecer três bênçãos à escolha: sorteia 1 de 10 santuários de nome críptico com uma frase enigmática; o jogador usa ou segue adiante (sem resposta, o necromante usa). O efeito (bom, misto ou ruim) só aparece depois do uso e fica anotado no Livro dos Santuários (aba Masmorra), guardado no save (SantuariosVistos, máscara de bits). Efeitos: Misterioso (+dano, mais dano sofrido), Sombrio (protege, menos dano), Estranho (+dano), Reluzente (identifica o saque e melhora itens), Oculto (expedição sem desgaste), Religioso (cura total), Mágico (escudo), Ornado (XP, menos ossos), Tétrico (aliados perdem vida, ouro), Profano (mais dano sofrido, mais almas). Teste novo; teste do Colosso passou a somar 8 sementes. Ajuste: AlivioInicial 0,84 -> 0,82, CrescimentoPorAndar 0,303 -> 0,305, InclinacaoFimDeAto 0,14 -> 0,16. Portão 9/9: chefe do Ato 1 1,23 h (8 tentativas, no limite), andar 15 3,10 h, chefe do Ato 2 8,67 h, espera 0,25; ativo 0,64/1,56/5,49 h. 120 testes. Publicado 1795718297-f103.
138. Pedido do dono: inimigos desproporcionais no mapa. No andar inteiro as figuras eram ampliadas até 3,2x (ocupavam meia sala); o reforço agora vai no máximo a 1,7x. O zoom "Médio" virou "Sala": a sala atual cabe inteira na altura (ou na largura, em tela larga), a câmera fica presa à sala e só desliza de lado atrás da luta, com as figuras em proporção natural. No celular o título do andar ganhou linha própria (antes aparecia cortado em "A…"). Só cliente web; teste_web4 sem erros.
139. Roteiro Diablo, fatia 11 (parte 1): temas de andar (Nucleo/Temas.cs). A cada 4 andares muda o tema, repetindo depois do Inferno: Catedral (mortos-vivos), Catacumbas (espíritos e aberrações), Cavernas (bestas), Inferno (demônios). No sorteio de espécies a família da casa pesa 1,8x e as outras 0,8x (Cfg.TemasDeAndar, TemaAfinidade, TemaForaDeCasa). No cliente: cor da pedra e do fogo por tema, nome do tema no cabeçalho, aviso e nota no diário ao entrar num tema novo; o visual alagado do Ato 2 em diante fica só nos andares de Catedral. Teste novo (121). Portão 9/9 sem ajuste: chefe do Ato 1 1,29 h (7 tentativas), andar 15 2,98 h, chefe do Ato 2 7,96 h, espera 0,25; ativo 0,65/1,67/5,06 h. Publicado 1795719xxx (ver histórico). Falta: objetos quebráveis próprios por tema e andares maiores/irregulares.
140. Roteiro Diablo, fatia 11 (parte 2a): decoração do chão por tema, junto às bordas da sala. Catacumbas: lápides e ossos com crânio; Cavernas: estalagmites e veios de brasa; Inferno: fendas de lava, poças de sangue e estacas com crânio. Só cliente web (sem mudança de balanceamento); os três temas conferidos por captura; teste_web4 sem erros. Falta da fatia 11: andares maiores e de forma irregular. Visto na captura: o nome de um Único pode ficar por cima do aviso central.
141. Roteiro Diablo, fatia 11 (parte 2b): andares maiores e irregulares nas Cavernas e no Inferno. Nesses temas o gerador troca o bloco compacto por galerias tortas com ramais (Cfg.MapaIrregularNosTemasFundos), limitado a 5 salas de lado (MapaFundoLadoMaximo), e ganha +20% de chance de cada sala vazia (TemaFundoSalaVaziaExtra): andares de ~10 salas. Cliente: mais margem no topo do andar inteiro (o rótulo da sala mais ao norte não corta mais) e a barra do elite desceu para não cobrir o rótulo "Sala x/y". Teste novo (122). Portão 9/9 sem ajuste: chefe do Ato 1 1,31 h (7 tentativas), andar 15 2,98 h, chefe do Ato 2 8,54 h, espera 0,26, 36 s por andar; ativo 0,80/1,76/5,23 h. Fatia 11 fechada.
142. Roteiro Diablo, fatia 12 (parte 1): missão do Açougueiro. Cada save sorteia se a missão existe (2 em cada 3; EstadoProgresso.MissaoAcougueiro: 0 não sorteada, 1 existe, 2 não existe, 3 vencida). No andar 2 aparece a sala "Porta do Açougueiro" (TipoSala 20): o jogador escolhe abrir ou seguir; sem resposta, o necromante só abre depois de já ter concluído o andar 5 (o ausente não morre de graça). Aberta, luta contra um Único de nome fixo (elite com vida x5, dano x1,8, velocidade x1,35; no cliente, maior e vermelho). Vencido: Cutelo do Açougueiro (foice Rara de nível 6 ou do andar, identificada e travada), 150 de ouro, missão gravada como cumprida e a sala não volta. Aba Masmorra mostra "Missões deste save". Robô ativo só abre a porta com recorde >= 5. Cheat "acougueiro". Teste novo (123). Portão 9/9 sem ajuste: chefe do Ato 1 1,29 h (8 tentativas, no limite), andar 15 2,99 h, chefe do Ato 2 8,45 h; ativo 0,72/1,83/5,18 h.
143. Roteiro Diablo, fatia 12 (parte 2): missão do Rei Esqueleto. Sorteio por save independente do Açougueiro (2 em 3; EstadoProgresso.MissaoReiEsqueleto). No andar 3 aparece a "Tumba do Rei" (TipoSala 21, sarcófago com coroa e velas): abrir ou seguir; sem resposta só abre com recorde >= 7. O Rei é um esqueleto de elite Único (vida x6, dano x1,5; no cliente maior e dourado) que, enquanto vive, ergue 2 esqueletos a cada 4 s (teto de 8 na sala; evento Extra 22, aviso "O Rei ergue mais esqueletos!"). Vencido: Coroa do Rei Morto (Elmo Raro de nível 8 ou do andar, identificado e travado), 250 de ouro, missão cumprida. "Missões deste save" lista as duas. Cheat "rei". Teste novo (124). Portão 9/9 sem ajuste: chefe do Ato 1 1,24 h (7 tentativas), andar 15 3,01 h, chefe do Ato 2 8,18 h; ativo 0,69/1,63/5,58 h. Fatia 12 fechada.
144. Roteiro Diablo, fatia 13: dificuldades (Nucleo/Dificuldade.cs). Normal, Pesadelo e Inferno; vencer o chefe do Ato 2 na mais alta já aberta libera a seguinte (EstadoProgresso.Dificuldade e DificuldadeLiberada); a escolha fica na aba Necrópole e vale na descida seguinte; o cabeçalho mostra a dificuldade da descida. Inimigos: vida x1 / 2,4 / 5,5 e dano x1 / 1,6 / 2,6. Saque: ouro x1 / 2 / 4, ossos x1 / 1,6 / 2,5, nível de item +0 / +6 / +14, chance de subir um degrau de raridade 0 / 15% / 35% (até Raro). No Normal nada muda (multiplicadores 1, sem sorteio extra): portão idêntico ao do item 143 (9/9). Cheat "dificuldade". Teste novo (125). Também: o nome flutuante de um Único some enquanto o aviso central está na tela (não se embolam mais). NÃO medido: quanto tempo o robô leva em Pesadelo e Inferno — os multiplicadores são um primeiro palpite.
145. Fase 2 (dono: "quero grandes mudanças a partir de agora"; escolheu masmorra contínua, combate e feitiços, progressão longa — plano no fim do ROTEIRO_DIABLO.md). A1, salas ligadas: ao começar uma luta, a matilha de uma sala de combate vizinha ainda não visitada pode ser atraída (40%; nunca a do elite, do Único ou da patrulha): entra pela porta que liga as duas depois de 3,5 s e a sala de origem fica vazia (Simulacao.AtrairSalaVizinha, evento Extra 23, Resultado.SalasAtraidas). Medido: 14 atrações em 36 andares. Cliente: aviso, tremor e nota no diário. Dois testes novos (127); dois testes de saque ganharam configuração própria. Portão 9/9 sem ajuste: chefe do Ato 1 1,27 h (7 tentativas), andar 15 2,90 h, chefe do Ato 2 8,16 h; ativo 0,63/1,67/5,25 h. Sem captura da matilha chegando pela porta (não consegui flagrar o momento).
146. Fase 2, C1 (fatia 15): melhorar e imbuir no Ferreiro. Item.Melhoria (+0 a +5): cada nível aumenta em 6% o implícito e os afixos (Ferreiro.FatorDeMelhoria, aplicado em CalcularBonus e na pontuação do auto-equipar); custo 30 x (1 + raridade) x (1 + 0,25 x nível) x 2,2^melhoria. Imbuir: item Comum ou Mágico ganha um afixo novo (até 2; Comum vira Mágico e ganha nome), custo 80 x (1 + 0,25 x nível) x (1 + 2 x afixos). Aba Ferreiro: seção "Melhorar e imbuir" com os itens vestidos, botões e "Melhoria automática" (desligada por padrão; ligada, gasta o ouro que sobra do reparo ao voltar). Nome do item mostra "+N". Robô da campanha melhora o que está vestido depois de comprar. Teste novo (128). Portão 9/9 sem ajuste: chefe do Ato 1 1,26 h (7 tentativas), andar 15 2,90 h, chefe do Ato 2 7,70 h; ativo 0,63/1,55/4,76 h (o ativo ficou mais rápido no Ato 2: vigiar).
147. Fase 2, B1: livros de feitiço (Nucleo/Grimorio.cs). O necromante já tinha 6 feitiços com mana; agora cada um tem nível (1 a 10). Cada livro lido sobe 1 nível: +3% de potência e -2% de custo de mana por nível (Simulacao._feiticos traz os feitiços já escalados). Ler exige Magia = 3 x (nível alvo - 1); livro sem Magia fica guardado e é lido sozinho quando der; no nível máximo vira 120 de ouro. Fontes: chefe (sempre), Único (20%), elite e Campeão (6%), Biblioteca (sempre). Aba Talentos: "Livro de feitiços" com nível, potência, mana e Magia pedida. Evento Extra 24 (aviso e diário). Cheat "livros". Teste novo (129). Portão: com +6% e 35% o andar 15 caiu para 2,36 h (FORA); ajustado para +3% e 20%: 9/9, chefe do Ato 1 1,26 h (8 tentativas, no limite), andar 15 2,66 h, chefe do Ato 2 6,51 h; ativo 0,68/1,55/4,49 h — o ativo no Ato 2 ficou rápido demais (meta 5 h): corrigir no próximo ciclo.
148. Fase 2, A2: corredores com vida. (1) Porta emperrada: a partir do andar 2, 20% das travessias entre salas têm a porta emperrada; o grupo arromba em 3 s, ou o jogador abre na hora por um botão que aparece sobre a cena (Simulacao.PortaFechada, ComandoAbrirPorta, eventos Extra 25/26; conferido em 390 px, o clique abre). (2) Emboscada de corredor: a partir do andar 3, 12% das salas de combate (nunca a primeira, a do elite, do chefe ou do Único) começam com os inimigos já em volta do grupo, sem tempo de formar (evento Extra 27, aviso e tremor). Robô ativo abre as portas. Teste novo (130); dois testes antigos dependentes de semente ganharam configuração própria. Portão 9/9 sem ajuste: 40 s por andar, chefe do Ato 1 1,24 h (7 tentativas), andar 15 2,66 h, chefe do Ato 2 6,87 h; ativo 0,63/1,60/4,35 h. Sobre o ativo: testei cortar o bônus de toque em sala (0,5 -> 0,25) e a chance de melhorar alma por toque (0,25 -> 0,1) e o tempo do Ato 2 ativo não mudou (4,44 e 4,46 h); a vantagem vem de Detonar/Frenesi manuais, ceifa de orbes, interrupção de golpes e portas. Razão ativo/ausente ~0,63: aceito como prêmio por jogar; a vigiar é a razão (não deixar cair abaixo de 0,55), não o valor de 5 h.
149. Fase 2, C2: itens Únicos (Nucleo/Unicos.cs). Tabela de 10 Únicos com nome, tipo e afixos fixos; cada afixo vem no topo da faixa do nível x 1,25 (Comando não multiplica). 2 de missão (Cutelo do Açougueiro, Coroa do Rei Morto, que passaram a vir desta tabela), 4 do Normal (andares 3, 5, 6, 8 em diante), 2 só de Pesadelo/Inferno, 2 só do Inferno. Queda: todo item Mágico ou Raro que cai tem 0,6% / 1,5% / 3% (por dificuldade) de virar um Único disponível (evento Extra 28). Item.UnicoId; nome com ★; "Livro de Únicos" na aba Masmorra (EstadoProgresso.UnicosAchados). Cheat "unico". Teste novo (131). NÃO feito: efeito próprio (mecânico) de cada Único — hoje são só números. Portão: o andar 15 caiu para 2,35 h (FORA); InclinacaoFimDeAto 0,16 -> 0,19: 9/9, chefe do Ato 1 1,32 h (7 tentativas), andar 15 2,80 h, chefe do Ato 2 7,37 h; ativo 0,72/1,42/4,59 h (razão 0,62).
