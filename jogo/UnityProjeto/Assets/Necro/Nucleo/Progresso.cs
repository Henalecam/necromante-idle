using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    public enum CondicaoOrdem
    {
        Sempre = 0,
        /// <summary>Vida do necromante abaixo de X% (Parametro = X).</summary>
        VidaAbaixoDe = 1,
        /// <summary>Mana do necromante acima de X%.</summary>
        ManaAcimaDe = 2,
        /// <summary>N ou mais cadáveres na arena.</summary>
        CadaveresPeloMenos = 3,
        ChefePresente = 4,
        ElitePresente = 5,
        /// <summary>Um minion de custo >= N morreu (e o cadáver dele ainda está na arena).</summary>
        MinionMortoDeCustoMinimo = 6,
        /// <summary>N ou mais inimigos vivos.</summary>
        InimigosPeloMenos = 7,
        OrbeDeAlmaPresente = 8,
        /// <summary>Algum minion com vida abaixo de X%.</summary>
        MinionComVidaAbaixoDe = 9
    }

    public enum AcaoNecromante
    {
        AtaqueBasico = 0, CeifarAlma = 1, LancaDeOssos = 2, DrenarVida = 3, ExplosaoDeCadaver = 4,
        Reerguer = 5, MaldicaoDaFraqueza = 6, Sacrificio = 7
    }

    /// <summary>Uma linha "SE condição ENTÃO ação" da lista de Ordens do necromante.</summary>
    [Serializable]
    public class Ordem
    {
        public CondicaoOrdem Condicao;
        public float Parametro;
        public AcaoNecromante Acao;

        public Ordem() { }
        public Ordem(CondicaoOrdem condicao, float parametro, AcaoNecromante acao) { Condicao = condicao; Parametro = parametro; Acao = acao; }
    }

    /// <summary>Um minion erguido (persistente entre expedições).</summary>
    [Serializable]
    public class MinionSalvo
    {
        public Alma Alma = new Alma();
        public int Nivel = 1;
        public float Xp;
        public int Estagio = 1;   // 1..3 -> nível máximo 20/40/60
        public bool Ativo;        // ocupa Comando e entra na expedição
        public int Grupo;         // índice em EstadoProgresso.PosturasDosGrupos
    }

    /// <summary>
    /// Estado de progresso do jogador: POCO com campos públicos, sem lógica e sem referência à simulação.
    /// Serializa direto com JsonUtility (Unity) ou System.Text.Json (IncludeFields).
    /// </summary>
    [Serializable]
    public class EstadoProgresso
    {
        public int Versao = 1;
        public int NivelNecromante = 1;
        public float XpNecromante;
        public float PoderNecromantico;     // 0.10 = +10% nos atributos dos minions
        public int ComandoMaximo = 10;
        public int EspacosDeOrdem = 3;

        public int MaiorAndarConcluido;
        public int AndarDeAncoragem = 1;    // de onde o modo Avançar parte
        public List<int> ChefesDerrotados = new List<int>();   // números dos Atos

        public long Ossos;
        public long Essencia;
        public long Sangue;
        public long[] Restos = new long[7]; // por CondicaoCadaver

        public List<Alma> Filacterio = new List<Alma>();
        public List<EspecieId> EspeciesConhecidas = new List<EspecieId>();
        /// <summary>Regra de descarte automático: almas de qualidade abaixo desta são dissolvidas em Essência.</summary>
        public Qualidade DissolverAbaixoDe = Qualidade.Fraca;

        // ---- Equipamento do necromante ----
        /// <summary>Itens guardados (limite: ConfigJogo.CapacidadeInventario). Só contam quando equipados.</summary>
        /// <summary>Nível de cada construção da Necrópole, indexado pelo enum Construcao.</summary>
        public int[] Construcoes = new int[Necropole.TotalDeConstrucoes];

        public List<Item> Inventario = new List<Item>();
        /// <summary>Os 9 espaços, indexados por EspacoEquipamento. Vazio = null ou item de Nivel 0 (use Equipamento.Obter).</summary>
        public Item[] Equipados = new Item[9];
        /// <summary>Regra de descarte automático: itens de raridade abaixo desta são desmanchados em Essência ao pegar (Magico = desmanchar Comuns).</summary>
        public RaridadeItem DesmancharAbaixoDe = RaridadeItem.Comum;

        public List<MinionSalvo> Minions = new List<MinionSalvo>();
        public Postura[] PosturasDosGrupos = { Postura.Agressiva, Postura.Defensiva, Postura.Escolta, Postura.Agressiva };
        public List<Ordem> Ordens = new List<Ordem>();          // vazia = prioridade padrão

        // ---- Missões ----
        public int VersaoDasMissoes;        // 0 = save anterior às missões de masmorra (ver Missoes.Migrar)
        public int MissaoAtual;             // índice em ConfigJogo.Missoes; = total quando todas foram resgatadas
        public long ProgressoMissao;
        public int FusoesFeitas;
        /// <summary>Sorte, em pontos (100 = +100%): mais itens e almas caem, e com mais chance de virem melhores.</summary>
        public float Sorte;
        /// <summary>O save já usou o painel de testes (cheats).</summary>
        public bool UsouCheats;
        /// <summary>Feitiços no automático: Detonar, Frenesi, Sopro, feitiço da Arma, feitiço da Mão secundária.</summary>
        public bool[] AutoFeiticos = new bool[5];
        // ---- Ascensão ----
        public int Ascensoes;
        public long Cinzas;
        public long CinzasGanhas;
        /// <summary>Nível de cada Legado (0 Ossos, 1 Sangue, 2 Sombras).</summary>
        public int[] Legados = new int[3];
        /// <summary>Nível de cada Oferenda, indexado por CondicaoCadaver (o tipo de Resto que ela consome).</summary>
        public int[] Oferendas = new int[7];
        /// <summary>Rito de Sangue preparado para a próxima expedição.</summary>
        public Rito RitoPreparado;
        /// <summary>Oferendas automáticas: ao fim de cada expedição os Restos viram níveis de altar sem o jogador clicar.</summary>
        public bool OferendasAutomaticas = true;
        /// <summary>Recurso do sacrifício de minions; gasta-se para Imbuir (subir a qualidade de um minion).</summary>
        public long FragmentosDeAlma;
        public int[] Atributos = new int[4]; public bool SemAutoAtributos;   // Força, Magia, Destreza, Vitalidade; por padrão o jogo distribui sozinho
        public int PergaminhosDePortal, PortalNoAndar, PortaisUsados; public bool PortalAutomatico;   // Portal da Cidade: pergaminhos, e o andar onde ficou um portal aberto (0 = nenhum)
        public int[] LivrosDaBruxa; public int LivrosComprados;   // prateleira da Bruxa (FeiticoId; -1 = vendido)
        public float PocaoVidaLimiar, PocaoManaLimiar;      // regra do jogador: beber abaixo desta fração (0 = padrão do jogo)
        public int PocoesDeVida, PocoesDeMana, PocoesCompradas; public bool SemReporPocoes;   // o cinto (repor ao voltar é ligado por padrão)
        public int UnicosAchados;                           // bits: itens Únicos já encontrados (o Livro de Únicos)
        public int[] NiveisDeFeitico = new int[7], LivrosGuardados = new int[7]; public int LivrosLidos;   // livros de feitiço: níveis ganhos e livros à espera de Magia
        public int[] AncoragemPorDificuldade;               // Ponto de Ancoragem guardado de cada dificuldade (a atual fica em AndarDeAncoragem)
        public int Dificuldade, DificuldadeLiberada;        // 0 Normal, 1 Pesadelo, 2 Inferno: a escolhida e a mais alta já aberta
        public int MissaoReiEsqueleto;                      // idem, para o Rei Esqueleto (andar 3)
        public int MissaoAcougueiro;                        // 0 = ainda não sorteada, 1 = existe neste save, 2 = não existe, 3 = vencida
        public int SantuariosVistos;                        // bits: santuários cujo efeito já foi descoberto (o Livro dos Santuários)
        public bool SemAutoReparo;                          // false (padrão): ao voltar de cada descida o Ferreiro já conserta o que o ouro alcançar
        public long Ouro;                                   // moeda da cidade: cai na masmorra, compra e vende no Ferreiro
        public List<Item> Loja = new List<Item>(); public int LojaRenovacoes, ItensVendidos, ItensComprados, ItensIdentificados, ItensReparados, ItensMelhorados, ItensImbuidos; public bool AutoMelhoria;
        public int MinionsSacrificados;
        /// <summary>Regra automática: minions NA RESERVA com qualidade abaixo desta são sacrificados ao fim de cada expedição.</summary>
        public Qualidade SacrificarAbaixoDe = Qualidade.Fraca;
        /// <summary>Pontos em cada talento, indexado como ConfigJogo.Talentos.</summary>
        public int[] Talentos = new int[15];
        /// <summary>Bestiário: inimigos mortos de cada espécie desde o começo do jogo, indexado por EspecieId.</summary>
        public long[] MortesPorEspecie = new long[16];        // contador da missão atual (missões de estado não usam)

        public ulong ContadorDeSementes = 1;  // para gerar sementes de expedição reproduzíveis
    }

    /// <summary>Resultado acumulado de uma expedição; aplicado ao progresso por Progressao.AplicarResultado.</summary>
    [Serializable]
    public class ResultadoExpedicao
    {
        public ModoExpedicao Modo;
        public int AndarInicial;
        public int AndarFinal;              // andar em que a expedição terminou
        public int MaiorAndarConcluido;
        public int AndaresConcluidos;
        public int AncoragemAlcancada;      // 0 = nenhuma nova
        public MotivoFim Motivo;
        public int Ticks;
        public List<int> AndaresRegistrados = new List<int>();  // andar de cada conclusão...
        public List<int> TicksPorAndar = new List<int>();       // ...e quantos ticks levou
        public long Ossos;
        public long Sangue;
        public long[] Restos = new long[7];
        public float Xp;
        public List<Alma> Almas = new List<Alma>();
        /// <summary>Itens que caíram (vão direto para cá, sem orbe para coletar).</summary>
        public List<Item> Itens = new List<Item>();
        public int OrbesCaidos;
        public int OrbesPerdidos;
        public int CeifasInterrompidas;
        public int CeifasPorToque;
        public int PactosAceitos;
        public int MarcasFeitas;
        public int GolpesBrutais;
        public int SoprosUsados;
        public int FeiticosDeItem;
        public int GolpesInterrompidos;
        public int SalasEspeciais;
        public int BausAbertos;
        public int NiveisSecretos;
        public int InteracoesPorToque;
        public int SantuariosVistos, SantuariosUsados; public bool SemDesgaste;
        public int AcougueiroSorteio; public bool AcougueiroMorto;
        public int ReiEsqueletoSorteio; public bool ReiEsqueletoMorto;
        public int Dificuldade;
        public List<int> ChefesVencidosNaDescida = new List<int>();   // Atos cujo chefe caiu nesta descida (inclusive repetidos)
        public int PocoesDeVidaUsadas, PocoesDeManaUsadas, PocoesDeVidaAchadas, PocoesDeManaAchadas;
        public int PortalNoAndar;   // > 0: a descida terminou por um Portal da Cidade aberto neste andar
        public int EfeitosDeUnicos;
        public int GolensConjurados, DespertaresPorProximidade, Fugas, Banquetes;
        public int SalasAtraidas, PortasArrombadas, PortasAbertasPorToque, EmboscadasDeCorredor;
        public List<int> Livros = new List<int>();   // livros de feitiço achados (FeiticoId)
        public int ObjetosQuebrados;
        public long Ouro;
        public int RotasEscolhidas;
        public int PatrulhasEnfrentadas;
        public int CofresAbertos;
        public int InimigosMortos;
        public int[] MortesPorEspecie = new int[16];   // indexado por EspecieId
        public int ElitesMortos;
        public int MinionsMortos;
        public int Execucoes;
        public int Overkills;
        public List<int> ChefesDerrotados = new List<int>();

        public float Segundos(ConfigJogo cfg) { return Ticks / (float)cfg.TicksPorSegundo; }
    }

    public enum DestinoDaAlma { Guardada, Dissolvida, GuardadaDissolvendoOutra }

    /// <summary>Regras de meta-progressão que mexem no EstadoProgresso (fora da simulação de combate).</summary>
    public static class Progressao
    {
        /// <summary>Novo jogo: necromante nível 1 com 3 Esqueletos comuns ativos.</summary>
        public static EstadoProgresso NovoJogo(ConfigJogo cfg)
        {
            var p = new EstadoProgresso { VersaoDasMissoes = 2, ComandoMaximo = cfg.ComandoInicial, EspacosDeOrdem = cfg.EspacosDeOrdemIniciais };
            for (int i = 0; i < 3; i++)
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Ativo = true });
            p.EspeciesConhecidas.Add(EspecieId.Esqueleto);
            return p;
        }

        public static int CustoDeComando(EspecieId especie, ConfigJogo cfg)
        {
            return cfg.CustoPorPorte[(int)TabelaEspecies.Obter(especie).Porte];
        }

        /// <summary>Custo em "meios de Comando" (x2), para a Coroa da Legião Muda: Pequenos custam 0,5 com ela.</summary>
        public static int CustoEmMeios(EspecieId especie, ConfigJogo cfg, bool legiaoMuda)
        {
            var porte = TabelaEspecies.Obter(especie).Porte;
            int custo = cfg.CustoPorPorte[(int)porte] * 2;
            return legiaoMuda && porte == Porte.Pequeno ? custo / 2 : custo;
        }

        private static bool TemLegiaoMuda(EstadoProgresso p)
        {
            if (p.Equipados == null) return false;
            for (int i = 0; i < p.Equipados.Length; i++)
                if (!Equipamento.Vazio(p.Equipados[i]) && p.Equipados[i].Efeito == EfeitoLendario.LegiaoMuda) return true;
            return false;
        }

        private static int MeiosEmUso(EstadoProgresso p, ConfigJogo cfg, bool legiaoMuda)
        {
            int meios = 0;
            for (int i = 0; i < p.Minions.Count; i++)
                if (p.Minions[i].Ativo) meios += CustoEmMeios(p.Minions[i].Alma.Especie, cfg, legiaoMuda);
            return meios;
        }

        /// <summary>Comando ocupado pelos minions ativos (com a Coroa da Legião Muda, o total é arredondado para cima).</summary>
        public static int ComandoEmUso(EstadoProgresso p, ConfigJogo cfg)
        {
            return (MeiosEmUso(p, cfg, TemLegiaoMuda(p)) + 1) / 2;
        }

        /// <summary>Comando máximo contando o +Comando do equipamento.</summary>
        /// <summary>Andar de onde o modo Avançar parte: o Ponto de Ancoragem menos o recuo configurado.</summary>
        public static int AndarDePartida(EstadoProgresso p, ConfigJogo cfg)
        {
            // Portal da Cidade aberto: a próxima descida volta ao andar do portal (vale uma vez)
            if (cfg.PortalDaCidade && p.PortalNoAndar > 0) return Math.Max(1, Math.Min(p.PortalNoAndar, p.MaiorAndarConcluido + 1));
            // Rito da Pressa: parte direto do Ponto de Ancoragem, sem o recuo
            return Math.Max(1, p.AndarDeAncoragem - (p.RitoPreparado == Rito.Pressa ? 0 : cfg.AndaresDeRecuo));
        }

        public static int ComandoMaximoEfetivo(EstadoProgresso p, ConfigJogo cfg)
        {
            return p.ComandoMaximo + Equipamento.CalcularBonus(p, cfg).Comando + Necropole.ComandoExtra(p, cfg);
        }

        public static bool PodeAtivar(EstadoProgresso p, int indiceMinion, ConfigJogo cfg)
        {
            var m = p.Minions[indiceMinion];
            if (m.Ativo) return true;
            bool legiao = TemLegiaoMuda(p);
            return MeiosEmUso(p, cfg, legiao) + CustoEmMeios(m.Alma.Especie, cfg, legiao) <= ComandoMaximoEfetivo(p, cfg) * 2;
        }

        /// <summary>Ativa o minion se couber no Comando. Retorna false (sem mudar nada) se não couber.</summary>
        public static bool Ativar(EstadoProgresso p, int indiceMinion, ConfigJogo cfg)
        {
            if (!PodeAtivar(p, indiceMinion, cfg)) return false;
            p.Minions[indiceMinion].Ativo = true;
            return true;
        }

        public static void Desativar(EstadoProgresso p, int indiceMinion) { p.Minions[indiceMinion].Ativo = false; }

        /// <summary>
        /// Se o Comando máximo caiu (troca de equipamento, talentos redistribuídos), tira de campo os últimos ativos até caber.
        /// Sem isto a Necrópole mostraria em campo minions que a simulação deixa de fora. Devolve quantos saíram.
        /// </summary>
        public static int AjustarAoComando(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0;
            for (int i = p.Minions.Count - 1; i >= 0 && ComandoEmUso(p, cfg) > ComandoMaximoEfetivo(p, cfg); i--)
                if (p.Minions[i].Ativo) { p.Minions[i].Ativo = false; n++; }
            return n;
        }

        /// <summary>Ergue um minion a partir de uma alma do Filactério (a alma sai do inventário), pagando Essência
        /// (Necropole.CustoDeErguer). Ativa se houver Comando. Retorna null, sem mudar nada, se faltar Essência.</summary>
        public static MinionSalvo ErguerMinion(EstadoProgresso p, int indiceNoFilacterio, ConfigJogo cfg)
        {
            var alma = p.Filacterio[indiceNoFilacterio];
            long custo = Necropole.CustoDeErguer(p, alma, cfg);
            if (p.Essencia < custo) return null;
            p.Essencia -= custo;
            p.Filacterio.RemoveAt(indiceNoFilacterio);
            var m = new MinionSalvo { Alma = alma };
            p.Minions.Add(m);
            Ativar(p, p.Minions.Count - 1, cfg);
            Missoes.Registrar(p, AcaoDeMissao.Erguer, cfg);
            return m;
        }

        /// <summary>
        /// Guarda uma alma no Filactério aplicando o descarte automático:
        /// 1) alma de espécie nova nunca é descartada; 2) abaixo da qualidade mínima da regra -> dissolve;
        /// 3) inventário cheio -> dissolve a pior alma (a nova ou a pior guardada).
        /// </summary>
        public static DestinoDaAlma GuardarAlma(EstadoProgresso p, Alma alma, ConfigJogo cfg)
        {
            bool especieNova = !p.EspeciesConhecidas.Contains(alma.Especie);
            if (especieNova) p.EspeciesConhecidas.Add(alma.Especie);

            if (!especieNova && alma.Qualidade < p.DissolverAbaixoDe)
            {
                Necropole.GanharEssencia(p, cfg.EssenciaPorQualidade[(int)alma.Qualidade], cfg);
                return DestinoDaAlma.Dissolvida;
            }
            if (p.Filacterio.Count < Necropole.CapacidadeDoFilacterio(p, cfg))
            {
                p.Filacterio.Add(alma);
                return DestinoDaAlma.Guardada;
            }
            // Cheio: acha a pior alma guardada (menor qualidade; em empate, a mais antiga).
            int pior = 0;
            for (int i = 1; i < p.Filacterio.Count; i++)
                if (p.Filacterio[i].Qualidade < p.Filacterio[pior].Qualidade) pior = i;

            if (!especieNova && alma.Qualidade <= p.Filacterio[pior].Qualidade)
            {
                Necropole.GanharEssencia(p, cfg.EssenciaPorQualidade[(int)alma.Qualidade], cfg);
                return DestinoDaAlma.Dissolvida;
            }
            Necropole.GanharEssencia(p, cfg.EssenciaPorQualidade[(int)p.Filacterio[pior].Qualidade], cfg);
            p.Filacterio.RemoveAt(pior);
            p.Filacterio.Add(alma);
            return DestinoDaAlma.GuardadaDissolvendoOutra;
        }

        public static int NivelMaximo(MinionSalvo m, ConfigJogo cfg) { return cfg.NivelMaximoPorEstagio * m.Estagio; }

        /// <summary>Dá XP a um minion respeitando o nível máximo do estágio.</summary>
        public static void DarXp(MinionSalvo m, float xp, ConfigJogo cfg)
        {
            int max = NivelMaximo(m, cfg);
            if (m.Nivel >= max) { m.Xp = 0f; return; }
            m.Xp += xp;
            while (m.Nivel < max && m.Xp >= Formulas.XpParaProximoNivelMinion(m.Nivel))
            {
                m.Xp -= Formulas.XpParaProximoNivelMinion(m.Nivel);
                m.Nivel++;
            }
            if (m.Nivel >= max) m.Xp = 0f;
        }

        /// <summary>Custo em Essência para evoluir ao próximo estágio (precisa estar no nível máximo do estágio atual).</summary>
        public static long CustoDeEvolucao(MinionSalvo m) { return 50L * m.Estagio * m.Estagio; }

        public static bool EvoluirEstagio(EstadoProgresso p, int indiceMinion, ConfigJogo cfg)
        {
            var m = p.Minions[indiceMinion];
            if (m.Estagio >= cfg.EstagioMaximo || m.Nivel < NivelMaximo(m, cfg)) return false;
            long custo = CustoDeEvolucao(m);
            if (p.Essencia < custo) return false;
            p.Essencia -= custo;
            m.Estagio++;
            return true;
        }

        /// <summary>Aplica ao progresso tudo o que a expedição rendeu (recursos, almas, itens, XP, ancoragem, chefes).</summary>
        public static void AplicarResultado(EstadoProgresso p, ResultadoExpedicao r, ConfigJogo cfg)
        {
            Missoes.RegistrarExpedicao(p, r, cfg);
            Dificuldades.Registrar(p, r, cfg);
            Bestiario.Registrar(p, r);
            p.Ossos += (long)Math.Round(r.Ossos * (double)cfg.DificuldadeOssos[r.Dificuldade] * (double)(Necropole.MultOssos(p, cfg) + Talentos.Total(p, EfeitoDeTalento.Ossos, cfg) + Oferendas.Total(p, EfeitoDeTalento.Ossos, cfg) + Ascensao.Valor(p, 0, cfg)));
            p.Sangue += r.Sangue;
            p.SantuariosVistos |= r.SantuariosVistos;
            if (p.MissaoAcougueiro == 0 && r.AcougueiroSorteio != 0) p.MissaoAcougueiro = r.AcougueiroSorteio;
            if (r.AcougueiroMorto) p.MissaoAcougueiro = 3;
            if (p.MissaoReiEsqueleto == 0 && r.ReiEsqueletoSorteio != 0) p.MissaoReiEsqueleto = r.ReiEsqueletoSorteio;
            if (r.ReiEsqueletoMorto) p.MissaoReiEsqueleto = 3;
            if (!r.SemDesgaste) Ferreiro.Desgastar(p, cfg);   // a descida gasta o equipamento (salvo pelo Santuário Oculto)
            p.Ouro += (long)Math.Round(r.Ouro * (double)cfg.DificuldadeOuro[r.Dificuldade]); Ferreiro.Renovar(p, cfg); Bruxa.Renovar(p, cfg);
            if (!p.SemAutoReparo) Ferreiro.RepararTudo(p, cfg);
            if (cfg.PocoesNoCinto) Pocoes.AoVoltar(p, r, cfg);
            if (p.AutoMelhoria) Ferreiro.MelhorarTudo(p, cfg);
            if (!p.SemAutoAtributos) Atributos.AutoDistribuir(p, cfg);
            Unicos.Registrar(p, r.Itens);
            // Portal da Cidade: o usado some do estoque e o andar fica marcado para a próxima descida; qualquer outra descida apaga a marca
            if (r.PortalNoAndar > 0) { p.PergaminhosDePortal = Math.Max(0, p.PergaminhosDePortal - 1); p.PortaisUsados++; }
            p.PortalNoAndar = r.PortalNoAndar;
            if (r.Livros != null) for (int li = 0; li < r.Livros.Count; li++) Grimorio.Receber(p, (FeiticoId)r.Livros[li], cfg);
            Grimorio.LerGuardados(p, cfg);   // jogo idle: quem está longe não volta para um exército de itens quebrados   // cada volta da masmorra, o Ferreiro tem estoque novo
            for (int i = 0; i < p.Restos.Length && i < r.Restos.Length; i++) p.Restos[i] += r.Restos[i];
            if (r.MaiorAndarConcluido > p.MaiorAndarConcluido) p.MaiorAndarConcluido = r.MaiorAndarConcluido;
            if (r.AncoragemAlcancada > p.AndarDeAncoragem) p.AndarDeAncoragem = r.AncoragemAlcancada;
            for (int i = 0; i < r.ChefesDerrotados.Count; i++)
                if (!p.ChefesDerrotados.Contains(r.ChefesDerrotados[i])) p.ChefesDerrotados.Add(r.ChefesDerrotados[i]);
            for (int i = 0; i < r.Almas.Count; i++) GuardarAlma(p, r.Almas[i], cfg);
            // Itens vão para o inventário (com o descarte automático); só passam a valer quando o jogador equipa.
            for (int i = 0; i < r.Itens.Count; i++) Equipamento.GuardarItem(p, r.Itens[i], cfg);

            // XP: o necromante recebe tudo; os minions ativos dividem igualmente.
            p.XpNecromante += r.Xp;
            while (p.XpNecromante >= Formulas.XpParaProximoNivelNecromante(p.NivelNecromante))
            {
                p.XpNecromante -= Formulas.XpParaProximoNivelNecromante(p.NivelNecromante);
                p.NivelNecromante++;
            }
            int ativos = 0;
            for (int i = 0; i < p.Minions.Count; i++) if (p.Minions[i].Ativo) ativos++;
            if (ativos > 0)
            {
                float parte = r.Xp / ativos;
                for (int i = 0; i < p.Minions.Count; i++)
                    if (p.Minions[i].Ativo) DarXp(p.Minions[i], parte, cfg);
            }
            Sacrificio.AplicarRegra(p, cfg);   // regra automática do jogador: reserva fraca vira Fragmentos
            if (p.OferendasAutomaticas) Oferendas.OfertarTudo(p, cfg);   // os Restos novos sobem os altares sozinhos
        }
    }
}
