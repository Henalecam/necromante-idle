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
        public float CrescimentoPorAndar = 0.26f;
        public float DegrauPorAto = 1.0f;           // multiplicador extra de vida/dano a cada Ato novo (composto): torna o 1º andar do Ato um degrau   // composto, vida e dano
        public float FatorVidaInimigo = 0.24f;      // inimigo comum = base da espécie x fator
        public float FatorDanoInimigo = 0.16f;
        public float EliteMultVida = 3.5f;
        public float EliteMultDano = 1.4f;
        public float ChefeMultVida = 2.1f;
        public float ChefeMultDano = 1.0f;

        // ---- Estrutura dos andares ----
        public int AndaresPorAto = 10;
        public int OndasPorAndar = 3;
        public float ChanceOndaElite = 0.30f;
        public int AndaresPorAncoragem = 5;
        public int AndaresDeRecuo = 2;              // o Avançar parte N andares ANTES do Ponto de Ancoragem (aquecimento; alonga a expedição)
        public float OrcamentoBaseOnda = 6.0f;      // "pontos" de inimigo por onda no andar 1
        public float OrcamentoPorAndar = 0.40f;
        public float OrcamentoPorOnda = 1.0f;
        public float OrcamentoMaximo = 28f;
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
            new DadosMissao { Titulo = "Espólios", Historia = "Os monges enterraram relíquias com os seus. Já não lhes servem.", Objetivo = "Tenha 3 itens equipados", Tipo = TipoMissao.ItensEquipados, Quantidade = 3, Essencia = 25 },
            new DadosMissao { Titulo = "Praga de diabretes", Historia = "Alguém abriu uma fresta para baixo. Os pequenos passaram primeiro.", Objetivo = "Mate 40 Diabretes", Tipo = TipoMissao.MatarEspecie, Alvo = (int)EspecieId.Diabrete, Quantidade = 40, Ossos = 200 },
            new DadosMissao { Titulo = "Alma de valor", Historia = "Nem toda alma é igual. Algumas ainda queimam.", Objetivo = "Ceife 1 alma Vigorosa ou melhor", Tipo = TipoMissao.CeifarAlmas, Alvo = (int)Qualidade.Vigorosa, Quantidade = 1, Essencia = 40 },
            new DadosMissao { Titulo = "Legião", Historia = "Um necromante sozinho é um homem com um cajado.", Objetivo = "Tenha 6 minions em campo", Tipo = TipoMissao.MinionsEmCampo, Quantidade = 6, Ossos = 300 },
            new DadosMissao { Titulo = "Caça aos campeões", Historia = "Os que brilham guardam o caminho. Apaga-os.", Objetivo = "Mate 5 elites", Tipo = TipoMissao.MatarElites, Quantidade = 5, Essencia = 60 },
            new DadosMissao { Titulo = "Desmanche", Historia = "O que não te serve inteiro serve em pedaços.", Objetivo = "Desmanche 10 itens", Tipo = TipoMissao.DesmancharItens, Quantidade = 10, Ossos = 250 },
            new DadosMissao { Titulo = "Cripta funda", Historia = "Mais nichos, mais mortos sob o teu comando.", Objetivo = "Leve a Cripta ao nível 5", Tipo = TipoMissao.NivelDeConstrucao, Alvo = (int)Construcao.Cripta, Quantidade = 5, Essencia = 50 },
            new DadosMissao { Titulo = "Descida", Historia = "O canto que sobe do fundo não é reza. É costura.", Objetivo = "Conclua o andar 9", Tipo = TipoMissao.AlcancarAndar, Alvo = 9, Ossos = 500 },
            new DadosMissao { Titulo = "A Abadessa Costurada", Historia = "Ela remendou as irmãs em si mesma para não morrer sozinha. O teu filactério está no peito dela.", Objetivo = "Derrote a chefe do Ato 1 (andar 10)", Tipo = TipoMissao.DerrotarChefe, Alvo = 1, Ossos = 1000, Essencia = 150 },
            new DadosMissao { Titulo = "Além da abadia", Historia = "O filactério estava vazio. Alguém levou o que havia dentro, mais para baixo.", Objetivo = "Conclua o andar 15", Tipo = TipoMissao.AlcancarAndar, Alvo = 15, Ossos = 2000 },
            new DadosMissao { Titulo = "Caçador de ancestrais", Historia = "Há almas aqui mais velhas do que a pedra.", Objetivo = "Ceife 1 alma Ancestral ou melhor", Tipo = TipoMissao.CeifarAlmas, Alvo = (int)Qualidade.Ancestral, Quantidade = 1, Essencia = 200 },
            new DadosMissao { Titulo = "O segundo selo", Historia = "Quem te roubou deixou um guardião. Ele sabe o teu nome.", Objetivo = "Derrote o chefe do Ato 2 (andar 20)", Tipo = TipoMissao.DerrotarChefe, Alvo = 2, Ossos = 5000, Essencia = 400 },
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
        public int[] EssenciaPorRaridadeItem = { 1, 3, 10, 40, 100 };
        public float EssenciaItemPorNivel = 0.10f;          // +10% de Essência por nível do item ao desmanchar

        // ---- Lendários ----
        // ---- Mão do jogador (comandos de toque; ausente, nada disto acontece) ----
        public float ToqueChanceDeMelhorarAlma = 0.25f;  // ceifa por toque: chance de a alma subir uma qualidade (até Ancestral)
        public float DetonarRecarga = 25f;               // s
        public int DetonarMaxCorpos = 8;
        public float DetonarPotencia = 1.0f;             // x o dano por cadáver da Explosão de Cadáver
        public float FrenesiRecarga = 40f;               // s
        public float FrenesiDuracao = 6f;                // s
        public float FrenesiBonusDeDano = 0.35f;         // "mais" dano dos minions enquanto dura
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
        };

        public float ExplosaoCadaverFracaoVida = 0.10f; // + fração da vida máxima de cada cadáver consumido
        public int ExplosaoCadaverMaxCorpos = 3;
        public float TempoDeConjuracao = 0.4f;

        public float DT { get { return 1f / TicksPorSegundo; } }

        public DadosFeitico Feitico(FeiticoId id) { return Feiticos[(int)id]; }
    }
}
