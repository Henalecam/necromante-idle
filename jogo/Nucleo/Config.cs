using System;

namespace Necro.Nucleo
{
    /// <summary>Dados de um feitiço do necromante.</summary>
    [Serializable]
    public class DadosFeitico
    {
        public FeiticoId Id;
        public string Nome;
        public float CustoMana;
        public float Recarga;   // segundos
        public float Alcance;   // unidades de arena
        public float Potencia;  // multiplicador do Dano do necromante (ou valor do efeito)
        public float Raio;      // raio/largura do efeito
        public float Duracao;   // segundos (efeitos com duração)
    }

    /// <summary>
    /// Todos os números de design ficam aqui (e na TabelaEspecies), para serem trocados
    /// por ScriptableObjects mais tarde sem tocar na lógica.
    /// </summary>
    [Serializable]
    public class ConfigJogo
    {
        // ---- Simulação e arena ----
        public int TicksPorSegundo = 10;
        public float LarguraArena = 20f;
        public float AlturaArena = 12f;
        public float NecromanteX = 2.5f;

        // ---- Fórmulas de combate ----
        public float CriticoBase = 0.05f;
        public float CriticoMultiplicador = 1.5f;
        public float TetoMitigacao = 0.75f;
        public float ArmaduraConstante = 100f;
        public float ArmaduraPorAndar = 20f;

        // ---- Escala dos inimigos ----
        public int AlivioAteOAndar = 5;
        public float AlivioInicial = 0.82f;
        public float CrescimentoPorAndar = 0.305f;
        public float DegrauPorAto = 1.42f;           // multiplicador extra de vida/dano a cada Ato novo (composto): torna o 1º andar do Ato um degrau   // composto, vida e dano
        public float FatorVidaInimigo = 0.275f;      // inimigo comum = base da espécie x fator
        public float FatorDanoInimigo = 0.16f;
        public float EliteMultVida = 3.5f;
        public float EliteMultDano = 1.4f;
        public float ChefeMultVida = 2.05f;
        public float ChefeMultDano = 1.0f;

        // ---- Estrutura dos andares ----
        public int AndaresPorAto = 10;
        public float InclinacaoFimDeAto = 0.19f;       // crescimento extra por andar depois de AndarDaInclinacao, dentro de cada Ato
        public int AndarDaInclinacao = 6;
        public int OndasPorAndar = 3;
        public float ChanceOndaElite = 0.30f;
        public int AndaresPorAncoragem = 5;
        public int AndaresDeRecuo = 2;              // o Avançar parte N andares ANTES do Ponto de Ancoragem (aquecimento; alonga a expedição)
        public float OrcamentoBaseOnda = 6.0f;      // "pontos" de inimigo por onda no andar 1
        public float OrcamentoPorAndar = 0.40f;
        public float OrcamentoPorOnda = 1.0f;
        public float OrcamentoMaximo = 28f;
        // Estilo Diablo: mais inimigos por sala, cada um mais fraco e rendendo menos (o total da sala fica parecido)
        public float Horda = 1.6f;
        public int MatilhasPorSala = 3;              // os inimigos comuns ficam agrupados em até N matilhas, não espalhados
        public float MatilhaRaio = 1.7f;
        // Campeões: pacote de N da mesma espécie, mais fortes que os comuns (azuis)
        public float ChanceDeCampeoes = 0.3f; public int CampeoesAPartirDoAndar = 3, CampeoesPorPacote = 3;
        public float CampeaoMultVida = 2.2f, CampeaoMultDano = 1.25f, CampeaoPremio = 2.5f, CampeaoCusto = 2f;
        // ---- Ouro e Ferreiro (Diablo 1: o ouro cai dos monstros e dos objetos, e compra/vende na cidade) ----
        public float[] OuroPorPorte = { 1f, 2.5f, 5f, 10f };
        public float OuroElite = 4f, OuroChefe = 25f, OuroBarril = 2f, OuroUrna = 4f, OuroSarcofago = 12f, OuroBau = 20f;
        public long[] OuroVendaPorRaridade = { 3, 10, 40, 200, 1000 };
        public float OuroPorNivelDeItem = 0.25f, LojaMargem = 12f;
        public int LojaItens = 4;
        // ---- Atributos (Diablo 1: cinco pontos por nível entre Força, Magia, Destreza e Vitalidade) ----
        public int PontosDeAtributoPorNivel = 5; public long EssenciaPorPontoDeAtributoRedistribuido = 2;
        public float ForcaDanoDosMinions = 0.004f, MagiaDanoDeFeiticos = 0.006f, MagiaMana = 1f, DestrezaCritico = 0.0008f, DestrezaVelAtaque = 0.0015f, VitalidadeVidaDosMinions = 0.004f, VitalidadeVidaDoNecromante = 0.006f;
        // Durabilidade (Diablo 1): o equipamento gasta com o uso e o Ferreiro repara por ouro
        public bool Durabilidade = true; public int DurabilidadeBase = 30, DurabilidadePorRaridade = 10, DesgastePorExpedicao = 3;
        public float ReparoFracao = 0.6f;   // reparar do zero custa esta fração do valor de venda do item
        public bool ItensNaoIdentificados = true;                       // Raros para cima caem sem identificar
        public long[] OuroParaIdentificar = { 0, 0, 60, 200, 600 };   // por raridade; +10% por nível do item
        public float VendaNaoIdentificado = 0.4f;                       // vender sem identificar rende só uma fração
        // Objetos quebráveis das salas de combate (barris, urnas, sarcófagos)
        public int ObjetosPorSala = 5; public float ObjetoRaioDeQuebra = 1.0f;
        public float BarrilOssos = 2f, BarrilChanceDeOssos = 0.5f, UrnaOssos = 3f, UrnaChanceDeItem = 0.04f, SarcofagoOssos = 8f, SarcofagoChanceDeMorto = 0.45f;
        public float BarrilExplosivoRaio = 2.2f, BarrilExplosivoDano = 0.35f, BarrilExplosivoDanoElite = 0.08f, BarrilExplosivoDanoAliado = 0.06f;
        // Únicos: um monstro com nome próprio e séquito da mesma espécie, num ponto sorteado do andar (como no Diablo)
        public float ChanceDeUnico = 0.5f; public int UnicosAPartirDoAndar = 4, SequitoDoUnico = 4;
        public float UnicoMultVida = 1.35f, SequitoMultVida = 1.3f, UnicoCusto = 0.8f;   // o único já é elite; isto multiplica por cima
        public float IntervaloDeSpawn = 0.8f;       // segundos entre inimigos da mesma onda
        public float PausaEntreOndas = 2.0f;
        public float PausaEntreAndares = 3.0f;
        public float CuraEntreAndares = 0.6f;      // fração da vida máxima recuperada pelos vivos
        public bool CacarRestauraExercito = true;   // no modo Caçar, cada repetição recomeça com o exército inteiro
        public float TempoAteFuria = 240f;          // s no mesmo andar até os inimigos entrarem em fúria
        public float FuriaPorSegundo = 0.10f;       // +10% de dano inimigo por segundo de fúria

        // ---- Modificadores de elite ----
        public float VampiricoRoubo = 0.30f;
        public float VelozBonus = 0.40f;
        public float EscudoDeOssosFracao = 0.35f;
        public float ExplosaoAoMorrerMult = 1.5f;   // x Dano da entidade
        public float ExplosaoAoMorrerRaio = 2.0f;

        // ---- Chefe: A Abadessa Costurada ----
        public float PocaIntervalo = 7f;
        public float PocaDuracao = 9f;
        public float PocaRaio = 2.5f;
        public float PocaCuraPorSegundo = 0.006f;   // fração da vida máxima do chefe por segundo, por poça

        // ---- IA ----
        public float RaioDefensivo = 7f;
        public float RaioEscolta = 3.5f;
        public float RaioAreaConjurador = 2.0f;
        public float DuracaoProvocacao = 3f;
        public float VelLancaDeOssos = 24f;         // unidades/s da Lança de Ossos
        public float VelProjetil = 13f;             // unidades/s das flechas dos Atiradores
        public float RaioSeparacao = 0.6f;

        // ---- Necromante ----
        public float NecromanteCrescimentoPorNivel = 0.08f;
        public float RegenManaPorSegundo = 2.5f;
        public float RegenVidaPorSegundo = 0.5f;
        public int ComandoInicial = 10;
        public int EspacosDeOrdemIniciais = 3;
        public int EspacosDeOrdemMaximos = 8;

        // ---- Almas ----
        public float DropAlmaComum = 0.02f;
        public float DropAlmaElite = 0.15f;
        public float DropAlmaChefePrimeira = 1.0f;
        public float DropAlmaChefeRepetida = 0.25f;
        public float TempoDeCeifa = 1.5f;
        public float RaioAtracaoDaCeifa = 0f;       // 0 = desligado. >0 (experimental): inimigos a essa distância do necromante o atacam enquanto ele canaliza
        public float AlcanceCeifa = 0f;             // 0 = ceifa à distância. >0 (experimental): o necromante anda até ficar a essa distância do orbe
        public float LimiteDanoCeifa = 0.15f;       // fração da vida máxima que interrompe a canalização
        public float EsperaAposInterrupcao = 0.5f;
        public float DuracaoDoOrbe = 8f;
        public float[] ChanceQualidade = { 0.60f, 0.25f, 0.10f, 0.04f, 0.01f };
        public float[] MultQualidade = { 0.80f, 1.00f, 1.20f, 1.45f, 1.75f };
        public int[] AfixosMin = { 0, 1, 1, 2, 3 };
        public int[] AfixosMax = { 0, 1, 2, 3, 3 };
        public int[] EssenciaPorQualidade = { 1, 3, 8, 20, 60 };
        public int CapacidadeFilacterio = 20;
        // ---- Talentos do necromante ----
        public int NiveisPorPontoDeTalento = 2;            // nível 10 em ~1 h e ~126 em 14 h: a árvore (67 pontos) enche perto do fim do Ato 2
        public long EssenciaPorPontoRedistribuido = 10;
        public DadosTalento[] Talentos =
        {
            // Ossos: o exército
            new DadosTalento { Ramo = RamoDeTalento.Ossos, Nome = "Ossatura", Texto = "+{0}% de vida dos minions", Efeito = EfeitoDeTalento.VidaDosMinions, PorPonto = 0.03f },
            new DadosTalento { Ramo = RamoDeTalento.Ossos, Nome = "Fio de Osso", Texto = "+{0}% de dano dos minions", Efeito = EfeitoDeTalento.DanoDosMinions, PorPonto = 0.03f },
            new DadosTalento { Ramo = RamoDeTalento.Ossos, Nome = "Marcha Fúnebre", Texto = "+{0}% de velocidade de ataque dos minions", Efeito = EfeitoDeTalento.VelAtaqueDosMinions, PorPonto = 0.02f, PontosNoRamo = 4 },
            new DadosTalento { Ramo = RamoDeTalento.Ossos, Nome = "Sede dos Mortos", Texto = "Minions roubam {0}% do dano como vida", Efeito = EfeitoDeTalento.RouboDeVidaDosMinions, PorPonto = 0.01f, PontosNoRamo = 8 },
            new DadosTalento { Ramo = RamoDeTalento.Ossos, Nome = "Senhor da Legião", Texto = "+{0} de Comando", Efeito = EfeitoDeTalento.Comando, PorPonto = 1f, Maximo = 2, PontosNoRamo = 14 },
            // Sangue: o próprio necromante
            new DadosTalento { Ramo = RamoDeTalento.Sangue, Nome = "Carne Velha", Texto = "+{0}% de vida do necromante", Efeito = EfeitoDeTalento.VidaDoNecromante, PorPonto = 0.06f },
            new DadosTalento { Ramo = RamoDeTalento.Sangue, Nome = "Verbo Sombrio", Texto = "+{0}% de dano de feitiços", Efeito = EfeitoDeTalento.DanoDeFeiticos, PorPonto = 0.06f },
            new DadosTalento { Ramo = RamoDeTalento.Sangue, Nome = "Poço Profundo", Texto = "+{0} de mana por segundo", Efeito = EfeitoDeTalento.RegenMana, PorPonto = 0.4f, PontosNoRamo = 4 },
            new DadosTalento { Ramo = RamoDeTalento.Sangue, Nome = "Olho do Agouro", Texto = "+{0}% de chance de crítico do necromante", Efeito = EfeitoDeTalento.ChanceCritico, PorPonto = 0.02f, PontosNoRamo = 8 },
            new DadosTalento { Ramo = RamoDeTalento.Sangue, Nome = "Pressa do Túmulo", Texto = "Feitiços recarregam {0}% mais rápido", Efeito = EfeitoDeTalento.ReducaoDeRecarga, PorPonto = 0.03f, PontosNoRamo = 14 },
            // Sombras: almas e saque
            new DadosTalento { Ramo = RamoDeTalento.Sombras, Nome = "Mão Leve", Texto = "Ceifa {0}% mais rápida", Efeito = EfeitoDeTalento.VelocidadeDeCeifa, PorPonto = 0.08f },
            new DadosTalento { Ramo = RamoDeTalento.Sombras, Nome = "Isca de Almas", Texto = "+{0}% de chance de alma", Efeito = EfeitoDeTalento.ChanceDeAlma, PorPonto = 0.05f },
            new DadosTalento { Ramo = RamoDeTalento.Sombras, Nome = "Avareza", Texto = "+{0}% de Ossos por expedição", Efeito = EfeitoDeTalento.Ossos, PorPonto = 0.05f, PontosNoRamo = 4 },
            new DadosTalento { Ramo = RamoDeTalento.Sombras, Nome = "Toque Certeiro", Texto = "+{0}% de chance de a ceifa por toque melhorar a alma", Efeito = EfeitoDeTalento.ToqueMelhoraAlma, PorPonto = 0.05f, PontosNoRamo = 8 },
            new DadosTalento { Ramo = RamoDeTalento.Sombras, Nome = "Vontade Profana", Texto = "+{0}% de Poder Necromântico (todos os atributos dos minions)", Efeito = EfeitoDeTalento.PoderNecromantico, PorPonto = 0.02f, PontosNoRamo = 14 },
        };

        // ---- Sacrifício e Fragmentos de Alma ----
        public long[] FragmentosPorQualidade = { 2, 5, 12, 30, 80 };   // por qualidade do minion sacrificado
        public float FragmentosPorNivel = 0.05f;                     // +5% por nível do minion acima do 1º
        public long[] FragmentosParaImbuir = { 15, 60, 240, 900 };   // subir de Fraca, Comum, Vigorosa, Ancestral

        // ---- Oferendas (Restos de cadáver) e Ritos de Sangue ----
        public long RestosPorOferenda = 20;
        public float CrescimentoDaOferenda = 1.30f;
        public int NivelMaximoDaOferenda = 20;
        /// <summary>Uma por CondicaoCadaver, na ordem do enum: Intacto, Mutilado, Queimado, Congelado, Eletrizado, Putrefato, Oco.</summary>
        public DadosOferenda[] Oferendas =
        {
            new DadosOferenda { Nome = "Corpos intactos", Texto = "+{0}% de vida dos minions", Efeito = EfeitoDeTalento.VidaDosMinions, PorNivel = 0.02f },
            new DadosOferenda { Nome = "Corpos mutilados", Texto = "+{0}% de dano dos minions", Efeito = EfeitoDeTalento.DanoDosMinions, PorNivel = 0.02f },
            new DadosOferenda { Nome = "Corpos queimados", Texto = "+{0}% de dano de feitiços", Efeito = EfeitoDeTalento.DanoDeFeiticos, PorNivel = 0.03f },
            new DadosOferenda { Nome = "Corpos congelados", Texto = "+{0}% de vida do necromante", Efeito = EfeitoDeTalento.VidaDoNecromante, PorNivel = 0.03f },
            new DadosOferenda { Nome = "Corpos eletrizados", Texto = "+{0}% de velocidade de ataque dos minions", Efeito = EfeitoDeTalento.VelAtaqueDosMinions, PorNivel = 0.01f },
            new DadosOferenda { Nome = "Corpos putrefatos", Texto = "+{0}% de chance de alma", Efeito = EfeitoDeTalento.ChanceDeAlma, PorNivel = 0.03f },
            new DadosOferenda { Nome = "Corpos ocos", Texto = "+{0}% de Ossos por expedição", Efeito = EfeitoDeTalento.Ossos, PorNivel = 0.03f },
        };
        public float RitoColheitaChanceDeAlma = 1.0f;       // +100% de chance de alma na expedição
        public float RitoBanqueteOssos = 1.0f;              // +100% de Ossos na expedição
        public DadosRito[] Ritos =
        {
            new DadosRito { Id = Rito.Pressa, Nome = "Rito da Pressa", Efeito = "A próxima expedição parte direto do Ponto de Ancoragem, sem recuar 2 andares", Sangue = 15 },
            new DadosRito { Id = Rito.Colheita, Nome = "Rito da Colheita", Efeito = "+100% de chance de alma na próxima expedição", Sangue = 20 },
            new DadosRito { Id = Rito.Banquete, Nome = "Rito do Banquete", Efeito = "+100% de Ossos na próxima expedição", Sangue = 20 },
        };

        // ---- Ascensão (Legados comprados com Cinzas; custo do próximo nível = nível + 1) ----
        public DadosLegado[] Legados =
        {
            new DadosLegado { Nome = "Legado de Ossos", Texto = "+{0}% de Ossos por expedição", PorNivel = 0.06f, Maximo = 25 },
            new DadosLegado { Nome = "Legado de Sangue", Texto = "+{0}% em todos os atributos dos minions", PorNivel = 0.04f, Maximo = 25 },
            new DadosLegado { Nome = "Legado de Sombras", Texto = "+{0}% de chance de alma", PorNivel = 0.06f, Maximo = 15 },
        };

        // ---- Bestiário ----
        public long[] MarcosDoBestiario = { 100, 400, 1500, 5000, 15000 };   // mortes de uma espécie para cada nível de Domínio
        public float BonusPorDominio = 0.03f;               // vida e dano dos minions da espécie, por nível de Domínio
        public int AlmasPorFusao = 3;                      // fusão: N almas iguais (espécie e qualidade) viram uma de qualidade acima
        public long[] EssenciaParaFundir = { 2, 6, 20, 80 };   // custo por qualidade de origem (Fraca, Comum, Vigorosa, Ancestral)

        // ---- Necrópole ----
        public float CrescimentoCustoConstrucao = 1.15f;    // custo de cada nível = base x isto^nível
        public int NiveisDeCriptaPorComando = 5;            // Cripta: +1 de Comando a cada 5 níveis
        public int EspacosPorNivelDeTorre = 2;              // Torre das Almas: espaços de Filactério por nível
        public float ChanceDeAlmaPorNivelDeTorre = 0.03f;   // Torre das Almas: +3% (aumento) de chance de alma por nível
        public float[] EssenciaParaErguerPorPorte = { 4f, 12f, 30f, 80f };  // x multiplicador da qualidade da alma
        public float DescontoPorNivelDeAltar = 0.03f;       // Altar de Erguimento: -3% no custo de erguer por nível
        public float DescontoMaximoDoAltar = 0.6f;
        public float OssosPorNivelDeOssuario = 0.05f;       // Ossuário: +5% de Ossos por expedição, por nível
        public float EssenciaPorNivelDeMesa = 0.04f;        // Mesa de Dissecação: +4% de Essência ao desmanchar/dissolver, por nível
        public DadosConstrucao[] Construcoes =
        {
            new DadosConstrucao { Id = Construcao.AltarDeErguimento, Nome = "Altar de Erguimento", Efeito = "Erguer minions custa menos Essência", AndarDeDesbloqueio = 0, NivelMaximo = 20, OssosBase = 300, EssenciaBase = 30 },
            new DadosConstrucao { Id = Construcao.Cripta, Nome = "Cripta", Efeito = "+1 de Comando a cada 5 níveis", AndarDeDesbloqueio = 0, NivelMaximo = 50, OssosBase = 200, EssenciaBase = 0 },
            new DadosConstrucao { Id = Construcao.TorreDasAlmas, Nome = "Torre das Almas", Efeito = "+2 espaços no Filactério e +3% de chance de alma por nível", AndarDeDesbloqueio = 5, NivelMaximo = 30, OssosBase = 150, EssenciaBase = 20 },
            new DadosConstrucao { Id = Construcao.Ossuario, Nome = "Ossuário", Efeito = "+5% de Ossos por expedição, por nível", AndarDeDesbloqueio = 3, NivelMaximo = 30, OssosBase = 180, EssenciaBase = 0 },
            new DadosConstrucao { Id = Construcao.MesaDeDissecacao, Nome = "Mesa de Dissecação", Efeito = "+4% de Essência ao desmanchar itens e dissolver almas, por nível", AndarDeDesbloqueio = 8, NivelMaximo = 25, OssosBase = 250, EssenciaBase = 15 },
        };

        // ---- Missões (cadeia do Ato 1 e começo do Ato 2) ----
        /// <summary>Missões resgatadas necessárias para a interface mostrar cada Sistema (Exército, Necrópole, Equipamento).</summary>
        public int[] MissoesParaAbrir = { 1, 3, 5 };
        public DadosMissao[] Missoes =
        {
            new DadosMissao { Titulo = "O primeiro degrau", Historia = "A abadia afundou com o teu filactério dentro. Sem ele, cada morte tua é a última.", Objetivo = "Conclua o andar 2", Tipo = TipoMissao.AlcancarAndar, Alvo = 2, Ossos = 60 },
            new DadosMissao { Titulo = "Colheita", Historia = "Os mortos daqui ainda seguram a alma por um instante. É teu instante.", Objetivo = "Ceife 3 almas", Tipo = TipoMissao.CeifarAlmas, Alvo = 0, Quantidade = 3, Essencia = 15 },
            new DadosMissao { Titulo = "Carne para o trabalho", Historia = "Uma alma presa é só um peso. De pé, ela luta por ti.", Objetivo = "Erga 1 minion no Exército", Tipo = TipoMissao.ErguerMinions, Quantidade = 1, Ossos = 80 },
            new DadosMissao { Titulo = "Fundações", Historia = "Sob a abadia há espaço para uma cidade dos mortos. Começa pela primeira pedra.", Objetivo = "Melhore construções da Necrópole 2 vezes", Tipo = TipoMissao.MelhorarConstrucoes, Quantidade = 2, Essencia = 20 },
            new DadosMissao { Titulo = "Âncora", Historia = "No quinto andar o véu é fino: dá para fincar um ponto de retorno.", Objetivo = "Conclua o andar 5", Tipo = TipoMissao.AlcancarAndar, Alvo = 5, Ossos = 150 },
            new DadosMissao { Titulo = "Saque", Historia = "Os monges trancaram o que tinham de valor. As trancas apodreceram antes deles.", Objetivo = "Abra 3 baús na masmorra", Tipo = TipoMissao.AbrirBaus, Quantidade = 3, Ossos = 120 },
            new DadosMissao { Titulo = "Espólios", Historia = "Os monges enterraram relíquias com os seus. Já não lhes servem.", Objetivo = "Tenha 3 itens equipados", Tipo = TipoMissao.ItensEquipados, Quantidade = 3, Essencia = 25 },
            new DadosMissao { Titulo = "A mão do ceifador", Historia = "A alma escapa depressa. A tua mão é mais rápida que a reza.", Objetivo = "Ceife 5 almas tocando nos orbes", Tipo = TipoMissao.CeifarPorToque, Quantidade = 5, Essencia = 30 },
            new DadosMissao { Titulo = "Praga de diabretes", Historia = "Alguém abriu uma fresta para baixo. Os pequenos passaram primeiro.", Objetivo = "Mate 40 Diabretes", Tipo = TipoMissao.MatarEspecie, Alvo = (int)EspecieId.Diabrete, Quantidade = 40, Ossos = 200 },
            new DadosMissao { Titulo = "Três em uma", Historia = "Três almas fracas, bem costuradas, fazem uma que presta.", Objetivo = "Faça 1 fusão de almas no Exército", Tipo = TipoMissao.FundirAlmas, Quantidade = 1, Ossos = 150 },
            new DadosMissao { Titulo = "Alma de valor", Historia = "Nem toda alma é igual. Algumas ainda queimam.", Objetivo = "Ceife 1 alma Vigorosa ou melhor", Tipo = TipoMissao.CeifarAlmas, Alvo = (int)Qualidade.Vigorosa, Quantidade = 1, Essencia = 40 },
            new DadosMissao { Titulo = "Legião", Historia = "Um necromante sozinho é um homem com um cajado.", Objetivo = "Tenha 6 minions em campo", Tipo = TipoMissao.MinionsEmCampo, Quantidade = 6, Ossos = 300 },
            new DadosMissao { Titulo = "Sangue por poder", Historia = "Nas encruzilhadas alguma coisa oferece ajuda. Nunca de graça.", Objetivo = "Aceite 2 pactos nas encruzilhadas (do andar 5 em diante)", Tipo = TipoMissao.AceitarPactos, Quantidade = 2, Essencia = 40 },
            new DadosMissao { Titulo = "Caça aos campeões", Historia = "Os que brilham guardam o caminho. Apaga-os.", Objetivo = "Mate 5 elites", Tipo = TipoMissao.MatarElites, Quantidade = 5, Essencia = 60 },
            new DadosMissao { Titulo = "Frestas", Historia = "Entre um salão e outro há sempre uma porta que ninguém vigia.", Objetivo = "Atravesse 10 salas especiais (baús, santuários, fontes...)", Tipo = TipoMissao.SalasEspeciais, Quantidade = 10, Essencia = 60 },
            new DadosMissao { Titulo = "Desmanche", Historia = "O que não te serve inteiro serve em pedaços.", Objetivo = "Desmanche 10 itens", Tipo = TipoMissao.DesmancharItens, Quantidade = 10, Ossos = 250 },
            new DadosMissao { Titulo = "O preço", Historia = "Nem todo servo merece o lugar. Os fracos alimentam os fortes.", Objetivo = "Sacrifique 2 minions", Tipo = TipoMissao.SacrificarMinions, Quantidade = 2, Essencia = 60 },
            new DadosMissao { Titulo = "Cripta funda", Historia = "Mais nichos, mais mortos sob o teu comando.", Objetivo = "Leve a Cripta ao nível 5", Tipo = TipoMissao.NivelDeConstrucao, Alvo = (int)Construcao.Cripta, Quantidade = 5, Essencia = 50 },
            new DadosMissao { Titulo = "Oferenda", Historia = "Queimados, partidos, ocos: cada morte serve a um altar.", Objetivo = "Faça 3 oferendas na Necrópole", Tipo = TipoMissao.FazerOferendas, Quantidade = 3, Ossos = 300 },
            new DadosMissao { Titulo = "Descida", Historia = "O canto que sobe do fundo não é reza. É costura.", Objetivo = "Conclua o andar 9", Tipo = TipoMissao.AlcancarAndar, Alvo = 9, Ossos = 500 },
            new DadosMissao { Titulo = "Conhece o teu inimigo", Historia = "Quem já abriu mil esqueletos sabe onde cada osso cede.", Objetivo = "Chegue ao Domínio 2 de Esqueleto no Bestiário", Tipo = TipoMissao.DominioDeEspecie, Alvo = (int)EspecieId.Esqueleto, Quantidade = 2, Essencia = 80 },
            new DadosMissao { Titulo = "A Abadessa Costurada", Historia = "Ela remendou as irmãs em si mesma para não morrer sozinha. O teu filactério está no peito dela.", Objetivo = "Derrote a chefe do Ato 1 (andar 10)", Tipo = TipoMissao.DerrotarChefe, Alvo = 1, Ossos = 1000, Essencia = 150 },
            new DadosMissao { Titulo = "Além da abadia", Historia = "O filactério estava vazio. Alguém levou o que havia dentro, mais para baixo.", Objetivo = "Conclua o andar 15", Tipo = TipoMissao.AlcancarAndar, Alvo = 15, Ossos = 2000 },
            new DadosMissao { Titulo = "Caçador de ancestrais", Historia = "Há almas aqui mais velhas do que a pedra.", Objetivo = "Ceife 1 alma Ancestral ou melhor", Tipo = TipoMissao.CeifarAlmas, Alvo = (int)Qualidade.Ancestral, Quantidade = 1, Essencia = 200 },
            new DadosMissao { Titulo = "O segundo selo", Historia = "Quem te roubou deixou um guardião. Ele sabe o teu nome.", Objetivo = "Derrote o chefe do Ato 2 (andar 20)", Tipo = TipoMissao.DerrotarChefe, Alvo = 2, Ossos = 5000, Essencia = 400 },
            new DadosMissao { Titulo = "Cartógrafo", Historia = "Quem enxerga o andar inteiro não precisa andar às cegas.", Objetivo = "Escolha a rota 3 vezes (toque no símbolo de uma sala no mapa)", Tipo = TipoMissao.EscolherRota, Quantidade = 3, Essencia = 150 },
            new DadosMissao { Titulo = "Arrombador", Historia = "Toda tranca tem um dono. Todo dono tem um pescoço.", Objetivo = "Abra 1 cofre trancado (pegue a chave antes)", Tipo = TipoMissao.AbrirCofres, Quantidade = 1, Ossos = 1500 }
        };

        // ---- Itens (loot) ----
        public float DropItemComum = 0.03f;         // chance de um inimigo comum soltar um item
        public float DropItemElite = 0.35f;
        public int ItensDoChefe = 2;                // chefe garante N itens (o primeiro é no mínimo Raro na primeira vitória)
        public float[] ChanceRaridadeItem = { 0.70f, 0.22f, 0.07f, 0.009f, 0.001f };  // Comum, Mágico, Raro, Lendário, Profano
        public int[] AfixosItemMin = { 0, 1, 3, 4, 4 };
        public int[] AfixosItemMax = { 0, 2, 4, 4, 4 };
        public float CrescimentoItemPorAndar = 0.18f;       // composto; alinhado ao CrescimentoPorAndar dos inimigos (afixos de escala Plena)
        public float CrescimentoItemLinearPorAndar = 0.05f; // linear (afixos de escala Linear: taxas, chances, resistências)
        public int NiveisPorComandoExtra = 30;              // +Comando vale 1, e +1 a cada N níveis de item (0 = sempre 1)
        public float TetoReducaoDeRecarga = 0.50f;          // teto da soma de Redução de recarga
        public int CapacidadeInventario = 40;
        public int LarguraDoInventario = 10;                // grade do inventário, em espaços
        public int AlturaDoInventario = 8;
        public int[] EssenciaPorRaridadeItem = { 1, 3, 10, 40, 100 };
        public float EssenciaItemPorNivel = 0.10f;          // +10% de Essência por nível do item ao desmanchar

        // ---- Lendários ----
        // ---- Estilo de combate pela arma (cajado = à distância, sem mudanças; foice = corpo a corpo) ----
        public float FoiceAlcance = 1.9f;
        public float FoiceBonusDeDano = 0.4f;            // "mais" dano do ataque básico e dos feitiços que usam o dano do necromante
        public float FoiceBonusDeVida = 0.25f;
        public float FoiceArmadura = 15f;
        public int FoiceAlvosExtras = 1;                 // inimigos a mais feridos por golpe

        // ---- Feitiços vinculados ao equipamento (dano em múltiplos do dano do necromante) ----
        public float FeiticoDeItemPorRaridade = 0.15f;   // +15% de potência por degrau de raridade do item
        public float AutoFeiticoPenalidade = 1.5f;       // no automático a recarga é 50% maior
        public float RaioSombrioPotencia = 4f;  public float RaioSombrioRecarga = 12f;      // Cajado: um alvo
        public float CeifadaPotencia = 2.5f;      public float CeifadaRaio = 3.0f; public float CeifadaRecarga = 10f;   // Foice: em volta do necromante
        public float ChuvaDeOssosPotencia = 1.8f; public float ChuvaDeOssosRaio = 3.5f; public float ChuvaDeOssosRecarga = 16f;  // Grimório: área
        public float EscudoDeAlmasFracao = 0.10f; public float EscudoDeAlmasRecarga = 25f;  // Relicário: escudo em todos os aliados

        // ---- Aleatoriedade ----
        public float DomBase = 0.06f;                    // dons das almas: faixa = DomBase + DomPorQualidade x qualidade
        public float DomPorQualidade = 0.03f;
        public float DomParteNegativa = 0.6f;            // o piso da faixa é -faixa x isto (pende para cima)
        // ---- Masmorra: salas especiais entre os combates ----
        public float ChanceDeSalaEspecial = 0.45f;       // por intervalo entre duas salas de combate
        public int SalasEspeciaisAPartirDoAndar = 2;
        public float TempoDeSalaEspecial = 1.9f;         // s na sala: o necromante anda até o objeto e interage
        public float ViagemBase = 0.8f, ViagemPorPassagem = 0.7f, ViagemMaxima = 2.9f;   // s de caminhada entre salas (visível no mapa)
        public float PausaAposSalaEspecial = 0.8f;
        public float ChanceDeSegundaSalaVazia = 0.35f;
        public float ChanceDeSalaVazia = 0.7f;           // câmaras e corredores sem evento: dão corpo ao mapa
        public float TempoDeSalaVazia = 0.3f;
        public float SalaVaziaChanceDeOssos = 0.3f;
        public float ToqueBonusDeSala = 0.5f;            // o toque do jogador melhora o resultado da sala
        public float BauOssos = 14f;                     // x (1 + 10% por andar)
        public float BauChanceDeItem = 0.30f;
        public float BauChanceDeItemComToque = 0.75f;
        public float BauChanceDeMimico = 0.12f;
        public float BauChanceDourado = 0.06f;
        public int BauDouradoSangue = 3;
        public bool MapaIrregularNosTemasFundos = true;  // Cavernas e Inferno: galerias tortas com ramais em vez do bloco compacto
        public int MapaFundoLadoMaximo = 5;              // o andar irregular não passa disto de lado (para caber na tela)
        public float TemaFundoSalaVaziaExtra = 0.2f;     // chance somada de sala vazia (galeria) nas Cavernas e no Inferno: andares maiores
        public bool MissoesDeCenario = true;                   // missões sorteadas por save, com cenário próprio (Diablo 1)
        public bool GolemAtivo = true;                     // o necromante conjura o Golem sozinho quando há luta e nenhum Golem vivo
        public float GolemProvocaRaio = 5f;                // ao surgir, provoca os inimigos em volta por alguns segundos
        // ---- Itens Únicos ----
        public float UnicoForcaDosAfixos = 1.25f;                          // cada afixo vem no topo da faixa x isto
        public float[] ChanceDeItemUnico = { 0.006f, 0.015f, 0.03f };      // por item Mágico ou melhor que cai, por dificuldade
        // ---- Livros de feitiço ----
        public bool LivrosDeFeitico = true;
        public int LivroNivelMaximo = 10;
        public int LivroMagiaPorNivel = 3;                // Magia exigida = 3 x (nível alvo - 1)
        public float LivroPotenciaPorNivel = 0.03f;       // +3% de potência por nível
        public float LivroManaPorNivel = 0.02f;           // -2% de custo de mana por nível
        public float ChanceDeLivroDeElite = 0.06f;        // elites e Campeões
        public float ChanceDeLivroDeUnico = 0.2f;
        public long LivroRepetidoOuro = 120;              // livro de feitiço já no máximo vira ouro
        // ---- Ferreiro: melhorar e imbuir ----
        public int MelhoriaMaxima = 5;
        public float MelhoriaPorNivel = 0.06f;            // cada +1 aumenta em 6% o implícito e os afixos do item
        public float MelhoriaCustoBase = 30f;             // x (1 + raridade) x (1 + 0,25 x nível do item) x crescimento^melhoria
        public float MelhoriaCrescimento = 2.2f;
        public float ImbuirCustoBase = 80f;               // x (1 + 0,25 x nível) x (1 + 2 x afixos que já tem)
        public int ImbuirMaximoDeAfixos = 2;
        // ---- Masmorra contínua: as salas se ouvem ----
        public bool PortasFechadas = true;
        public int PortasAPartirDoAndar = 2;
        public float ChanceDePortaFechada = 0.2f;         // por travessia entre salas
        public float PortaFechadaTempo = 3f;              // segundos para o grupo arrombar (o toque abre na hora)
        public bool EmboscadaDeCorredor = true;
        public int EmboscadaDeCorredorAPartirDoAndar = 3;
        public float ChanceDeEmboscadaDeCorredor = 0.12f; // por sala de combate: os inimigos já atacam na entrada, sem dar tempo de formar
        public bool SalasLigadas = true;
        public float ChanceDeAtrairVizinha = 0.4f;        // por luta: a matilha de uma sala vizinha não visitada vem pela porta
        public float AtrairAtraso = 3.5f;                 // segundos até os primeiros chegarem
        // ---- Dificuldades (Normal, Pesadelo, Inferno) ----
        public int DificuldadeAbreNoAto = 2;                                  // vencer o chefe deste Ato abre a dificuldade seguinte
        public float[] DificuldadeVida = { 1f, 2.4f, 5.5f };                  // vida dos inimigos
        public float[] DificuldadeDano = { 1f, 1.6f, 2.6f };                  // dano dos inimigos
        public float[] DificuldadeOssos = { 1f, 1.6f, 2.5f };
        public float[] DificuldadeOuro = { 1f, 2f, 4f };
        public int[] DificuldadeNivelDeItem = { 0, 6, 14 };                   // somado ao nível de todo item que cai
        public float[] DificuldadeChanceDeSubirRaridade = { 0f, 0.15f, 0.35f }; // chance de o item cair um degrau acima (até Raro)
        public bool ForcarReiEsqueleto;                  // só para testes e capturas
        public int ReiAndar = 3;
        public float ReiMultVida = 6f;                   // sobre um esqueleto de elite
        public float ReiMultDano = 1.5f;
        public float ReiErgueACada = 4f;                 // segundos entre levas de esqueletos erguidos
        public int ReiErguePorVez = 2;
        public int ReiMaximoDeErguidos = 8;
        public int ReiAbreSozinhoNoRecorde = 7;
        public int ReiNivelDaCoroa = 8;
        public long ReiOuro = 250;
        public bool ForcarAcougueiro;                    // só para testes e capturas
        public int AcougueiroAndar = 2;
        public float AcougueiroMultVida = 5f;            // sobre um elite da espécie
        public float AcougueiroMultDano = 1.8f;
        public float AcougueiroMultVelocidade = 1.35f;
        public int AcougueiroAbreSozinhoNoRecorde = 5;   // sem resposta do jogador, o necromante só abre a porta depois de já ter concluído este andar
        public int AcougueiroNivelDoCutelo = 6;          // o Cutelo cai como se fosse deste andar (ou do atual, se maior)
        public long AcougueiroOuro = 150;
        public bool TemasDeAndar = true;                 // tema a cada 4 andares (Catedral, Catacumbas, Cavernas, Inferno): puxa o elenco para a família da casa
        public float TemaAfinidade = 1.8f;               // peso de sorteio das espécies da casa
        public float TemaForaDeCasa = 0.8f;              // peso das demais
        public float BencaoForca = 0.10f;                // Fúria: +dano; Proteção: -dano sofrido; Colheita: +ossos (x2,5)
        public float TempoDeEscolhaDaBencao = 4f;        // s para o jogador escolher; depois o necromante pega uma ao acaso
        public float FonteCura = 0.20f;
        public int EspelhosMaximo = 6;                     // Cripta dos Espelhos: reflexos no máximo
        public int ForcarSantuario = 0;                    // só para testes: 1..10 escolhe o santuário
        public int ForcarSalaEspecial = 0;                 // só para testes: põe este TipoSala logo depois da primeira sala de combate
        public int ForcarSecreto = 0;                      // só para testes: 1..3 escolhe o nível secreto
        public int TorturaSangue = 2, TorturaSangueDoToque = 1;   // Sala de tortura
        public float BibliotecaXp = 30f;                   // Biblioteca: XP do tomo (x escala do andar; um inimigo médio dá 3)
        public float FossoMult = 1.4f; public int FossoMaximo = 26;   // Fosso de monstros: horda de uma espécie só
        public float ForjaEscudo = 0.25f;                 // Forja de Ossos: couraça = fração da vida máxima de cada aliado
        public int NinhoCrias = 4, NinhoCriasMaximo = 8; public float NinhoPremioDaLuta = 0.4f;   // Ninho sem toque: diabretes que chocam (+1 a cada 6 andares)
        public float NinhoOssos = 1.5f;                    // Ninho com toque: Ossos = BauOssos x isto
        public float AlmasPresasChanceDeMelhorar = 0.5f;
        public float EmboscadaOrcamento = 0.5f;
        public float MercadorPreco = 40f;                // Ossos da própria expedição, x (1 + 10% por andar)
        public float MercadorDescontoComToque = 0.4f;
        public float MercadorChanceDeRaro = 0.2f;
        // habilidades de espécie (só para minions erguidos)
        public bool HabilidadesDeEspecie = true;
        public float MiasmaMult = 1.5f, MiasmaRaio = 1.6f;      // Zumbi
        public float SedeBonus = 0.25f, SedeDuracao = 4f;        // Cão de Cripta
        public float OlhoMortoBonus = 0.3f, OlhoMortoLimiar = 0.95f;   // Arqueiro Ossudo: dano extra em alvo (quase) inteiro
        public float MatilhaBonus = 0.08f; public int MatilhaMaximo = 3; // Diabrete: por outro diabrete vivo
        public float AgouroDuracao = 2.5f;                               // Acólito Caído: fraqueza no alvo (não chefe)
        public float BanqueteCura = 0.15f;                               // Carniçal: fração da vida máxima ao matar
        public float FuriaDeGuerraBonus = 0.25f, FuriaDeGuerraLimiar = 0.5f; // Diabo de Guerra: dano extra abaixo de metade da vida
        public int IncorporeaACada = 4;                                  // Aparição: ignora 1 a cada N golpes
        public float BaluarteReducao = 0.10f;                            // Colosso de Ossos: os outros minions sofrem menos dano
        public float InvestidaMult = 2f;                                 // Rês Profana: primeiro golpe de cada sala
        // patrulha errante
        public float ChanceDePatrulha = 0.3f, PatrulhaIntervalo = 9f;
        public int PatrulhaAPartirDoAndar = 6, PatrulhaTamanho = 3;
        public bool ForcarCofre;                         // teste: todo andar comum ganha um cofre trancado
        public float CofreOssos = 3f, CofreChanceDeRaro = 0.4f;   // cofre trancado: precisa da chave que está em outra sala
        public float ArmadilhaDano = 0.08f, ArmadilhaDanoNoNecromante = 0.05f;   // fração da vida máxima; nunca mata
        public float AltarCustoDeVida = 0.15f;           // o toque do jogador evita o custo
        public float AltarBonusDeAlma = 0.8f;            // até o fim do andar
        // nível secreto (o Pasto Profano)
        public float ChanceDeNivelSecreto = 0.025f;
        public int SecretoAPartirDoAndar = 5;
        public int SecretoHordaBase = 14, SecretoHordaMaxima = 26;
        public float SecretoOssos = 2f;
        public int SecretoSangue = 5;
        // ambientes da sala do chefe (fração da vida máxima)
        public float LavaIntervalo = 5f, LavaAviso = 1.5f, LavaRaio = 1.7f;
        public float LavaDanoEmAliados = 0.10f, LavaDanoNoNecromante = 0.06f, LavaDanoEmInimigos = 0.18f, LavaDanoNoChefe = 0.03f;
        public float NevoaIntervalo = 2f, NevoaDanoEmAliados = 0.012f, NevoaDanoEmInimigos = 0.02f, NevoaReducaoDeCura = 0.25f;
        public float GeadaIntervalo = 6f, GeadaDuracao = 1.8f;
        public float DesabamentoIntervalo = 4f, DesabamentoAviso = 1.3f, DesabamentoRaio = 1.1f, DesabamentoFator = 0.7f;   // pedras: menores e mais frequentes que a lava
        public float TesouroChanceDeLendario = 0.15f;
        // chefe do Ato 2 (Carcereiro de Almas)
        public float GrilhoesIntervalo = 9f, GrilhoesDuracao = 2.0f;
        public int GrilhoesAlvos = 2, GrilhoesMaximoDeAparicoes = 3;

        public float ChanceDePressagio = 0.35f;          // chance de um andar comum ter um presságio
        public int PressagioAPartirDoAndar = 7;         // a primeira expedição (até o andar 6) fica sem surpresas
        public float FolgaDoPoderSugerido = 0.6f;        // o Poder sugerido do andar é a escala dos inimigos x isto

        public float SorteMaisDropsPorPonto = 0.005f;        // Sorte: +0,5% de itens e almas por ponto
        public float SorteChanceDeMelhorPorPonto = 0.01f;   // Sorte: 1% por ponto de sortear duas vezes e ficar com o melhor (100 pontos = sempre)

        // ---- Tática ----
        public float RaioDeFoco = 1.6f;                  // minions trocam o alvo mais próximo pelo mais ferido dentro deste raio dele
        public float GolpeBrutalAviso = 1.4f;            // s de aviso antes do golpe de elite/chefe (janela para o jogador interromper)
        public float GolpeBrutalRaio = 2.3f;
        public float GolpeBrutalMult = 2.0f;             // x o dano do inimigo, em todos no raio
        public float GolpeBrutalRecarga = 11f;
        public float GolpeBrutalAtordoamento = 2.0f;     // s parado quando o jogador interrompe

        // ---- Mão do jogador (comandos de toque; ausente, nada disto acontece) ----
        public float SoproCura = 0.25f;                  // Sopro Profano: fração da vida máxima curada no aliado tocado
        public float SoproRecarga = 22f;
        public float MarcaDuracao = 10f;                 // Marca do Ceifador: toque num inimigo
        public float MarcaBonusDeDano = 0.25f;           // "mais" dano aliado no inimigo marcado
        public float ToqueChanceDeMelhorarAlma = 0.25f;  // ceifa por toque: chance de a alma subir uma qualidade (até Ancestral)
        public float DetonarRecarga = 20f;               // s
        public int DetonarMaxCorpos = 8;
        public float DetonarPotencia = 2.0f;             // x o dano por cadáver da Explosão de Cadáver
        public float FrenesiRecarga = 30f;               // s
        public float FrenesiDuracao = 8f;                // s
        public float FrenesiBonusDeDano = 0.50f;         // "mais" dano dos minions enquanto dura
        // ---- Encruzilhadas (pactos de expedição; só o jogador presente aceita) ----
        public int AndaresPorEncruzilhada = 2;           // a cada N andares concluídos na expedição abre uma oferta
        public int EncruzilhadaAPartirDoAndar = 5;       // antes disso o jogador novo ainda está aprendendo o básico
        public float TempoDaEncruzilhada = 25f;          // s até a oferta sumir
        public DadosPacto[] Pactos =
        {
            new DadosPacto { Nome = "Pacto de Sangue", Ganho = "Minions causam +30% de dano", Preco = "Inimigos causam +10% de dano", DanoMinion = 1.30f, DanoInimigo = 1.10f },
            new DadosPacto { Nome = "Pacto do Ossário", Ganho = "+60% de Ossos", Preco = "Inimigos novos têm +20% de vida", Ossos = 1.60f, VidaInimigo = 1.20f },
            new DadosPacto { Nome = "Pacto da Colheita", Ganho = "+60% de chance de alma", Preco = "Inimigos causam +10% de dano", ChanceDeAlma = 0.60f, DanoInimigo = 1.10f },
            new DadosPacto { Nome = "Pacto da Trégua", Ganho = "Cura total do exército agora", Preco = "-25% de Ossos", CuraAgora = true, Ossos = 0.75f },
            new DadosPacto { Nome = "Pacto da Pressa", Ganho = "Detonar e Frenesi recarregam 60% mais rápido", Preco = "Inimigos novos têm +12% de vida", RecargaDosPoderes = 1.60f, VidaInimigo = 1.12f },
            new DadosPacto { Nome = "Pacto do Avarento", Ganho = "+80% de chance de item", Preco = "Inimigos novos têm +15% de vida e causam +10% de dano", ChanceDeItem = 1.80f, VidaInimigo = 1.15f, DanoInimigo = 1.10f },
            new DadosPacto { Nome = "Pacto do Carniceiro", Ganho = "Minions causam +50% de dano", Preco = "Inimigos novos têm +25% de vida", DanoMinion = 1.50f, VidaInimigo = 1.25f },
            new DadosPacto { Nome = "Pacto da Fartura", Ganho = "+40% de Ossos e +40% de chance de item", Preco = "Inimigos causam +20% de dano", Ossos = 1.40f, ChanceDeItem = 1.40f, DanoInimigo = 1.20f },
            new DadosPacto { Nome = "Pacto do Covarde", Ganho = "Inimigos causam -20% de dano", Preco = "-40% de Ossos", DanoInimigo = 0.80f, Ossos = 0.60f },
            new DadosPacto { Nome = "Pacto das Mil Almas", Ganho = "+120% de chance de alma", Preco = "Inimigos novos têm +30% de vida", ChanceDeAlma = 1.20f, VidaInimigo = 1.30f },
        };

        public float CeifeiroFamintoCura = 0.05f;   // fração da vida máxima curada em cada minion por alma ceifada
        public float MaeCarnicaDuracao = 10f;       // s de vida do Zumbi deixado pela Explosão de Cadáver
        public float ReiSolitarioBonus = 1.00f;     // "mais" dano do necromante com 0 minions vivos
        public float LegiaoMudaPenalidadeVida = 0.20f; // -20% de vida dos minions Pequenos (que passam a custar 0,5 de Comando)
        public float MareRubraFracao = 0.30f;       // fração do dano excedente do overkill espalhada
        public float MareRubraRaio = 2.0f;

        // ---- Afixos e traços ----
        public float FamintoRoubo = 0.08f;
        public float FreneticoBonus = 0.20f;
        public float PetreoArmadura = 40f;
        public float RancorosoMult = 3.0f;       // x Dano do minion, em área, ao morrer
        public float LealReducao = 0.20f;           // -20% de dano sofrido perto do mestre (= +25% de vida efetiva)
        public float LealRaio = 4f;
        public float IncandescenteFracao = 0.30f;
        public float ColossoVida = 0.25f;
        public float CarniceiroDano = 0.20f;
        public float AgourentoCritico = 0.10f;

        // ---- Minions ----
        public int[] CustoPorPorte = { 1, 3, 6, 10 };
        public float MinionCrescimentoPorNivel = 0.05f;
        public int NivelMaximoPorEstagio = 20;      // 20 / 40 / 60
        public int EstagioMaximo = 3;
        public float FamiliaBonus3 = 0.10f;
        public float FamiliaBonus5 = 0.25f;

        // ---- Cadáveres e gore ----
        public float LimiarOverkill = 0.50f;
        public float LimiarExecucao = 0.10f;
        public float ChanceExecucao = 0.25f;
        public int LimiteDeCadaveres = 30;
        public float TempoMorrendo = 0.8f;

        // ---- Recursos ----
        public int[] OssosPorPorte = { 1, 3, 6, 12 };
        public float OssosExtrasOverkill = 0.5f;    // +50% de ossos quando o corpo explode
        public int SangueElite = 1;
        public int SangueChefe = 5;
        public int SangueExecucao = 1;
        public float[] XpPorPorte = { 1f, 3f, 7f, 20f };

        // ---- Feitiços (indexados por FeiticoId) ----
        public DadosFeitico[] Feiticos =
        {
            new DadosFeitico { Id = FeiticoId.LancaDeOssos, Nome = "Lança de Ossos", CustoMana = 12f, Recarga = 3f, Alcance = 14f, Potencia = 2.5f, Raio = 0.7f },
            new DadosFeitico { Id = FeiticoId.DrenarVida, Nome = "Drenar Vida", CustoMana = 15f, Recarga = 6f, Alcance = 9f, Potencia = 2.0f },
            new DadosFeitico { Id = FeiticoId.ExplosaoDeCadaver, Nome = "Explosão de Cadáver", CustoMana = 20f, Recarga = 5f, Alcance = 14f, Potencia = 1.5f, Raio = 2.5f },
            new DadosFeitico { Id = FeiticoId.Reerguer, Nome = "Reerguer", CustoMana = 30f, Recarga = 10f, Alcance = 99f, Potencia = 0.5f },
            new DadosFeitico { Id = FeiticoId.MaldicaoDaFraqueza, Nome = "Maldição da Fraqueza", CustoMana = 15f, Recarga = 12f, Alcance = 14f, Potencia = 0.20f, Raio = 4f, Duracao = 8f },
            new DadosFeitico { Id = FeiticoId.Sacrificio, Nome = "Sacrifício", CustoMana = 10f, Recarga = 20f, Alcance = 99f, Potencia = 0.50f, Duracao = 8f },
            // Golem (Diablo 1): um só por vez. Potência = multiplicador de vida e dano sobre um Colosso de Ossos comum do nível do necromante
            new DadosFeitico { Id = FeiticoId.Golem, Nome = "Golem de Ossos", CustoMana = 45f, Recarga = 60f, Alcance = 99f, Potencia = 1.0f, Duracao = 40f },
        };

        public float ExplosaoCadaverFracaoVida = 0.10f; // + fração da vida máxima de cada cadáver consumido
        public int ExplosaoCadaverMaxCorpos = 3;
        public float TempoDeConjuracao = 0.4f;

        public float DT { get { return 1f / TicksPorSegundo; } }

        public DadosFeitico Feitico(FeiticoId id) { return Feiticos[(int)id]; }
    }
}
