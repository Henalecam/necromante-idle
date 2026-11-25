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
