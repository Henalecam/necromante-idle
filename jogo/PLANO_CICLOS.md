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
