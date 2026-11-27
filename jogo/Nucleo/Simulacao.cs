using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>
    /// Simulação determinística de uma expedição (10 ticks/s), totalmente separada da renderização.
    /// Uso: criar, chamar Tick() em passo fixo, ler Entidades/Cadaveres/Orbes/Pocas e consumir Eventos.
    /// A simulação nunca altera o EstadoProgresso: o que ela rende fica em Resultado.
    /// </summary>
    public sealed partial class Simulacao
    {
        private struct SpawnPendente
        {
            public EspecieId Especie;
            public bool Fraco; public bool Acougueiro; public bool Rei;   // inimigo de horda fora do plano do andar (fosso de monstros): mesma fraqueza e prêmio dos comuns de horda
            public bool Elite;
            public bool Chefe;
            public ModificadorElite Mods;
            public int Portal;
            public int TickDeSpawn;
            public InimigoPlanejado Planejado;   // null = entra por um portal (emboscadas, nível secreto)
        }

        /// <summary>Um inimigo que já está numa sala do andar, à espera do grupo.</summary>
        public sealed class InimigoPlanejado
        {
            public int Sala; public EspecieId Especie; public bool Elite, Chefe, Campeao; public bool Unico, Sequito; public string Nome; public ModificadorElite Mods; public Vec2 Pos; public int Atraso; public bool Desperto;
        }
        /// <summary>Quem está em cada sala de combate do andar (uma lista por onda).</summary>
        public readonly List<List<InimigoPlanejado>> PlanoDasOndas = new List<List<InimigoPlanejado>>(4);

        private struct ExplosaoPendente
        {
            public Vec2 Pos;
            public float Dano;
            public float Raio;
            public bool AtingeInimigos; // true = fere inimigos; false = fere necromante e minions
            public int OrigemId;
            public TipoDano Tipo;
        }

        // ---- Estado público (inspecionável pela camada visual) ----
        public readonly ConfigJogo Cfg;
        public readonly Rng Rng;
        public readonly List<Entidade> Entidades = new List<Entidade>(64);
        public readonly List<Cadaver> Cadaveres = new List<Cadaver>(32);
        public readonly List<OrbeDeAlma> Orbes = new List<OrbeDeAlma>(4);
        public readonly List<Projetil> Projeteis = new List<Projetil>(16);
        private int _proxProjetil = 1;
        public readonly List<PocaDeSangue> Pocas = new List<PocaDeSangue>(4);
        /// <summary>Eventos do tick atual (limpa a cada Tick).</summary>
        public readonly List<EventoSim> Eventos = new List<EventoSim>(64);
        public readonly ResultadoExpedicao Resultado = new ResultadoExpedicao();
        public readonly Vec2[] Portais;
        /// <summary>Bônus do equipamento do necromante, calculado uma vez na criação da expedição.</summary>
        public readonly BonusEquipamento Bonus;

        public Entidade Necromante { get; private set; }
        public int TickAtual { get; private set; }
        public ModoExpedicao Modo { get; private set; }
        public FaseExpedicao Fase { get; private set; }
        public int Andar { get; private set; }
        public int Onda { get; private set; }
        public int TotalDeOndas { get; private set; }
        public int PortaisAtivos { get; private set; }
        public int InimigosVivos { get; private set; }
        public bool Encerrada { get { return Fase == FaseExpedicao.Encerrada; } }
        /// <summary>Tempo restante do bônus de Sacrifício (+50% de dano dos minions).</summary>
        public float SacrificioTempo { get; private set; }
        /// <summary>true = o necromante está de foice: combate corpo a corpo.</summary>
        public bool NecromanteCorpoACorpo { get; private set; }
        /// <summary>Rito de Sangue em vigor nesta expedição.</summary>
        public Rito RitoDaExpedicao { get; private set; }
        public bool Ceifando { get { return _ceifaOrbe != null; } }
        public float ProgressoDaCeifa { get { return _ceifaOrbe == null ? 0f : _ceifaTempo / _tempoDeCeifa; } }
        /// <summary>Manto do Último Suspiro: true se equipado e ainda não usado neste andar.</summary>
        public bool UltimoSuspiroDisponivel { get { return Bonus.UltimoSuspiro && !_suspiroUsado; } }

        // ---- Opções ----
        /// <summary>Desligue em execuções "sem tela" que não leem eventos (um pouco mais rápido).</summary>
        public bool EmitirEventos = true;
        /// <summary>Encerra a expedição depois de concluir N andares (útil para rodar um andar só).</summary>
        public int LimiteDeAndares = int.MaxValue;
        /// <summary>Liga/desliga a IA do necromante (testes e depuração).</summary>
        public bool NecromanteAtivo = true;

        // ---- Estado interno ----
        private readonly float DT;
        private readonly bool _fluxoAutomatico;
        private readonly List<SpawnPendente> _filaDeSpawn = new List<SpawnPendente>(32);
        private readonly List<ExplosaoPendente> _explosoes = new List<ExplosaoPendente>(8);
        private readonly List<Entidade> _minions = new List<Entidade>(16); // todos os da expedição, vivos ou mortos
        private readonly List<Ordem> _ordens = new List<Ordem>(8);
        private readonly List<int> _chefesDerrotados = new List<int>(4);
        private readonly float[] _recargaFeitico = new float[Grimorio.Quantos];
        private readonly DadosFeitico[] _feiticos = new DadosFeitico[Grimorio.Quantos];   // os feitiços já no nível dos livros lidos
        private DadosFeitico Fei(FeiticoId id) { return _feiticos[(int)id]; }
        /// <summary>Um livro de feitiço cai: sorteia qual e avisa a tela.</summary>
        private void SoltarLivro(Vec2 pos)
        {
            if (!Cfg.LivrosDeFeitico) return;
            int qual = _rngItens.Intervalo(0, Grimorio.Quantos);
            Resultado.Livros.Add(qual);
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 24, Valor = qual, Alvo = -1, Origem = -1, Pos = pos });
        }
        private int _proximoId = 1;
        private float _temporizadorDeFase;
        private int _tickInicioDoAndar;
        private bool _ondaEliteNesteAndar;
        private float _fatorFuria = 1f;
        private bool _furiaAnunciada;
        private float _acumuladoCuraPoca;
        // Ceifa
        private OrbeDeAlma _ceifaOrbe;
        private float _ceifaTempo;
        private float _ceifaDano;
        private float _ceifaEspera;
        // Equipamento
        private readonly ulong _sementePactos;
        private readonly Rng _rngPressagio;
        private float _sorte;   // pontos de Sorte do progresso
        private float _restoDeOssos = 0.5f;
        private float _restoDeOuro = 0.5f;
        private int _ondaDoUnico, _dif;
        private float _aOrcamento = 1f, _aOssos = 1f, _aAlma, _aDanoInimigo = 1f;
        /// <summary>Presságio em vigor neste andar.</summary>
        public Pressagio PressagioAtual { get; private set; }
        private readonly Rng _rngItens;        // fluxo próprio: sortear itens não desloca os sorteios do combate
        private readonly float _tempoDeCeifa;
        private readonly float _regenMana;
        private readonly float _fatorRecarga;
        private readonly float _poderTotal;    // Poder Necromântico do progresso + equipamento
        private readonly int _nivelNecromante;
        private readonly float _bonusAlmaNecropole;
        private bool _suspiroUsado;

        /// <param name="fluxoAutomatico">false = arena vazia, sem andares/ondas automáticos (testes e sandbox).</param>
        public Simulacao(ConfigJogo cfg, EstadoProgresso progresso, ulong semente, ModoExpedicao modo, int andarInicial, bool fluxoAutomatico = true)
        {
            Cfg = cfg;
            DT = cfg.DT;
            Rng = new Rng(semente);
            _rngItens = new Rng(semente ^ 0x4954454E53UL); // "ITENS"
            _sementePactos = semente ^ 0x50414354UL;       // "PACT"
            _rngPressagio = new Rng(semente ^ 0x50524553UL);
            _rngMasmorra = new Rng(semente ^ 0x4D41534DUL);    // "MASM"   // "PRES"
            Bonus = Equipamento.CalcularBonus(progresso, cfg);
            _tempoDeCeifa = cfg.TempoDeCeifa / (1f + Bonus.VelocidadeDeCeifa);
            _regenMana = cfg.RegenManaPorSegundo + Bonus.RegenMana;
            _fatorRecarga = 1f - Bonus.ReducaoDeRecarga;
            _poderTotal = progresso.PoderNecromantico + Bonus.PoderNecromantico;
            _nivelNecromante = progresso.NivelNecromante;
            _bonusAlmaNecropole = Necropole.BonusChanceDeAlma(progresso, cfg);
            _santuariosVistos = progresso.SantuariosVistos;
            // missões sorteadas por save (Diablo 1): 2 em cada 3 saves têm o Açougueiro. O sorteio sai da semente da primeira descida e fica gravado
            _recordeInicial = progresso.MaiorAndarConcluido; _acougueiro = progresso.MissaoAcougueiro;
            if (_acougueiro == 0 && Cfg.MissoesDeCenario) { _acougueiro = (semente * 2654435761UL >> 7) % 3UL != 0UL ? 1 : 2; Resultado.AcougueiroSorteio = _acougueiro; }
            if (Cfg.ForcarAcougueiro) _acougueiro = 1;
            _dif = Dificuldades.Atual(progresso); Resultado.Dificuldade = _dif;
            _golemAprendido = cfg.LivrosDeFeitico && Grimorio.Nivel(progresso, FeiticoId.Golem) >= 1;
            for (int fi = 0; fi < Grimorio.Quantos; fi++)
            {
                var b0 = cfg.Feitico((FeiticoId)fi); int nv = cfg.LivrosDeFeitico ? Grimorio.Nivel(progresso, (FeiticoId)fi) : 1;
                _feiticos[fi] = new DadosFeitico { Id = b0.Id, Nome = b0.Nome, CustoMana = b0.CustoMana * Grimorio.FatorDeMana(nv, cfg), Recarga = b0.Recarga, Alcance = b0.Alcance, Potencia = b0.Potencia * Grimorio.FatorDePotencia(nv, cfg), Raio = b0.Raio, Duracao = b0.Duracao };
            }
            _limiarVida = progresso.PocaoVidaLimiar > 0f ? Math.Min(0.9f, progresso.PocaoVidaLimiar) : cfg.PocaoVidaAbaixoDe; _limiarMana = progresso.PocaoManaLimiar > 0f ? Math.Min(0.9f, progresso.PocaoManaLimiar) : cfg.PocaoManaAbaixoDe;
            _pocoesVida = cfg.PocoesNoCinto ? progresso.PocoesDeVida : 0; _pocoesMana = cfg.PocoesNoCinto ? progresso.PocoesDeMana : 0;
            _reiEsqueleto = progresso.MissaoReiEsqueleto;
            if (_reiEsqueleto == 0 && Cfg.MissoesDeCenario) { _reiEsqueleto = (semente * 0x9E3779B97F4A7C15UL >> 11) % 3UL != 0UL ? 1 : 2; Resultado.ReiEsqueletoSorteio = _reiEsqueleto; }
            // Rito de Sangue preparado na Necrópole (quem cria a simulação chama Oferendas.ConsumirRito em seguida)
            PrepararFeiticosDeItem(progresso);
            _sorte = Math.Max(0f, progresso.Sorte);
            RitoDaExpedicao = progresso.RitoPreparado;
            if (RitoDaExpedicao == Rito.Colheita) _pAlma += cfg.RitoColheitaChanceDeAlma;
            if (RitoDaExpedicao == Rito.Banquete) _pOssos *= 1f + cfg.RitoBanqueteOssos;
            Modo = modo;
            Andar = andarInicial < 1 ? 1 : andarInicial;
            _fluxoAutomatico = fluxoAutomatico;
            Resultado.Modo = modo;
            Resultado.AndarInicial = Andar;
            Resultado.AndarFinal = Andar;
            Resultado.Motivo = MotivoFim.EmAndamento;

            Portais = new[]
            {
                new Vec2(cfg.LarguraArena - 0.5f, cfg.AlturaArena * 0.5f),
                new Vec2(cfg.LarguraArena * 0.75f, 0.5f),
                new Vec2(cfg.LarguraArena * 0.75f, cfg.AlturaArena - 0.5f)
            };

            for (int i = 0; i < progresso.ChefesDerrotados.Count; i++) _chefesDerrotados.Add(progresso.ChefesDerrotados[i]);
            int limiteOrdens = Math.Min(progresso.EspacosDeOrdem, cfg.EspacosDeOrdemMaximos);
            for (int i = 0; i < progresso.Ordens.Count && i < limiteOrdens; i++) _ordens.Add(progresso.Ordens[i]);

            CriarNecromante(progresso);
            CriarMinions(progresso);

            Fase = FaseExpedicao.Preparando;
            if (_fluxoAutomatico) IniciarAndar();
        }

        // =====================================================================
        //  Laço principal
        // =====================================================================

        /// <summary>Avança a simulação em um passo fixo (1 / TicksPorSegundo).</summary>
        public void Tick()
        {
            Eventos.Clear();
            if (Fase == FaseExpedicao.Encerrada) return;
            TickAtual++;
            Resultado.Ticks = TickAtual;

            if (_fluxoAutomatico) AtualizarFluxo();
            if (Fase == FaseExpedicao.Encerrada) return;
            AtualizarEfeitosGlobais();
            AtualizarAmbiente();
            AtualizarPatrulha();
            AtualizarObjetos();

            AtualizarComandosDoJogador();
            AtualizarFeiticosDeItem();
            if (Necromante.Viva && NecromanteAtivo) AtualizarNecromante();

            int n = Entidades.Count; // unidades criadas neste tick só agem no próximo
            for (int i = 0; i < n; i++)
            {
                var e = Entidades[i];
                if (e.Viva && e != Necromante) AtualizarUnidade(e);
            }

            ProcessarExplosoes();
            SepararUnidades();
            AtualizarProjeteis();
            AtualizarOrbes();
            LimparMortos();

            if (!Necromante.Viva) Encerrar(MotivoFim.NecromanteMorreu);
        }

        /// <summary>O jogador abandona a expedição (mantém o que já ganhou).</summary>
        public void Recuar() { Encerrar(MotivoFim.Retirada); }

        /// <summary>Encerra por limite externo (ex.: orçamento de tempo do progresso offline).</summary>
        public void EncerrarPorTempo() { Encerrar(MotivoFim.LimiteDeTempo); }

        private void Encerrar(MotivoFim motivo)
        {
            if (Fase == FaseExpedicao.Encerrada) return;
            Fase = FaseExpedicao.Encerrada;
            Resultado.Motivo = motivo;
            Resultado.AndarFinal = Andar;
            Emitir(new EventoSim { Tipo = TipoEvento.ExpedicaoEncerrada, Extra = (int)motivo, Valor = Resultado.MaiorAndarConcluido, Alvo = -1, Origem = -1 });
        }

        private void Emitir(EventoSim ev)
        {
            if (!EmitirEventos) return;
            ev.Tick = TickAtual;
            Eventos.Add(ev);
        }

        // =====================================================================
        //  Criação de unidades
        // =====================================================================

        private Entidade CriarBase(Faccao faccao, DadosEspecie d, Vec2 pos)
        {
            var e = new Entidade
            {
                Id = _proximoId++, Faccao = faccao, Especie = d.Id, Papel = d.Papel, Porte = d.Porte, Familia = d.Familia,
                Pos = pos, Olhar = new Vec2(faccao == Faccao.Inimigo ? -1f : 1f, 0f),
                VidaMax = d.Vida, Dano = d.Dano, VelAtaque = d.VelAtaque, Armadura = d.Armadura, Alcance = d.Alcance,
                VelMovimento = d.VelMovimento, TipoDano = d.TipoDano,
                ChanceCritico = Cfg.CriticoBase, MultCritico = Cfg.CriticoMultiplicador,
                Custo = Cfg.CustoPorPorte[(int)d.Porte], Posto = pos
            };
            for (int i = 0; i < 6; i++) e.Resistencias[i] = d.Resistencias[i];
            return e;
        }

        private void Adicionar(Entidade e)
        {
            e.Vida = e.VidaMax;
            e.NaLista = true;
            Entidades.Add(e);
        }

        private void CriarNecromante(EstadoProgresso p)
        {
            var d = TabelaEspecies.Obter(EspecieId.Necromante);
            var e = CriarBase(Faccao.Necromante, d, new Vec2(Cfg.NecromanteX, Cfg.AlturaArena * 0.5f));
            float f = Formulas.FatorNivelNecromante(p.NivelNecromante, Cfg);
            e.VidaMax = d.Vida * f * (1f + Bonus.VidaDoNecromantePercentual) + Bonus.Vida;
            e.Dano = d.Dano * f;
            // A arma define o estilo: cajado luta de longe; foice vai para a frente, bate em arco e aguenta mais.
            var arma = Equipamento.Obter(p, EspacoEquipamento.Arma);
            NecromanteCorpoACorpo = !Equipamento.Vazio(arma) && arma.Tipo == TipoItem.Foice;
            if (NecromanteCorpoACorpo)
            {
                e.Alcance = Cfg.FoiceAlcance;
                e.Dano *= 1f + Cfg.FoiceBonusDeDano;
                e.VidaMax *= 1f + Cfg.FoiceBonusDeVida;
                e.Armadura += Cfg.FoiceArmadura;
                e.TipoDano = TipoDano.Fisico;
            }
            e.ManaMax = 100f + 5f * (p.NivelNecromante - 1) + Bonus.Mana;
            e.Mana = e.ManaMax;
            e.Custo = 0;
            // Equipamento: valores planos somam; "Dano de feitiços" entra na soma dos aumentos da fórmula de dano.
            e.Armadura += Bonus.Armadura;
            e.AumentoDano += Bonus.DanoDeFeiticos;
            e.ChanceCritico += Bonus.ChanceCritico;
            e.MultCritico += Bonus.DanoCritico;
            for (int i = 0; i < 6; i++) e.Resistencias[i] += Bonus.Resistencias[i];
            Necromante = e;
            Adicionar(e);
        }

        private void CriarMinions(EstadoProgresso p)
        {
            // 1) Seleciona os ativos que cabem no Comando (na ordem da lista).
            //    A conta é feita em "meios de Comando": com a Coroa da Legião Muda os Pequenos custam 0,5
            //    e o total é arredondado para cima (meios <= 2 x máximo equivale a teto(meios / 2) <= máximo).
            int meios = 0;
            // o mesmo teto que a Necrópole usa para ativar (inclui a Cripta); antes a Cripta ficava de fora e os últimos ativos não entravam
            int meiosMaximos = Progressao.ComandoMaximoEfetivo(p, Cfg) * 2;
            var contagemFamilia = new int[5];
            var escolhidos = new List<int>(16);
            for (int i = 0; i < p.Minions.Count; i++)
            {
                var m = p.Minions[i];
                if (!m.Ativo) continue;
                var d = TabelaEspecies.Obter(m.Alma.Especie);
                int custo = Progressao.CustoEmMeios(m.Alma.Especie, Cfg, Bonus.LegiaoMuda);
                if (meios + custo > meiosMaximos) continue;
                meios += custo;
                escolhidos.Add(i);
                contagemFamilia[(int)d.Familia]++;
            }

            // 2) Cria as entidades em formação à frente do necromante.
            int frente = 0, tras = 0;
            float cy = Cfg.AlturaArena * 0.5f;
            for (int k = 0; k < escolhidos.Count; k++)
            {
                var m = p.Minions[escolhidos[k]];
                var d = TabelaEspecies.Obter(m.Alma.Especie);
                bool retaguarda = d.Papel == Papel.Atirador || d.Papel == Papel.Conjurador || d.Papel == Papel.Suporte;
                int idx = retaguarda ? tras++ : frente++;
                int coluna = idx / 4, linha = idx % 4;
                float x = retaguarda ? Cfg.NecromanteX + 1.2f + coluna * 0.9f : Cfg.NecromanteX + 3.0f + coluna * 0.9f;
                float y = cy + (linha - 1.5f) * 1.3f;
                var e = CriarBase(Faccao.Minion, d, new Vec2(x, y));
                e.IndiceMinion = escolhidos[k];
                int grupo = m.Grupo >= 0 && m.Grupo < p.PosturasDosGrupos.Length ? m.Grupo : 0;
                e.Postura = p.PosturasDosGrupos[grupo];

                // Atributo = Base da espécie x Qualidade x fator de Nível x (1 + Poder Necromântico [progresso + equipamento])
                e.VidaMax = Formulas.AtributoMinion(d.Vida, m.Alma.Qualidade, m.Nivel, _poderTotal, Cfg);
                e.Dano = Formulas.AtributoMinion(d.Dano, m.Alma.Qualidade, m.Nivel, _poderTotal, Cfg);
                e.Armadura = Formulas.AtributoMinion(d.Armadura, m.Alma.Qualidade, m.Nivel, _poderTotal, Cfg);
                float dominio = Bestiario.MultMinion(p, m.Alma.Especie, Cfg);   // Bestiário: espécie bem conhecida luta melhor
                e.VidaMax *= dominio; e.Dano *= dominio;
                e.VidaMax *= 1f + m.Alma.DomVida; e.Dano *= 1f + m.Alma.DomDano; e.VelAtaque *= 1f + m.Alma.DomVelocidade;   // dons de nascença
                AplicarEquipamentoAoMinion(e);

                // Afixos
                if (m.Alma.Tem(Afixo.Faminto)) e.RouboDeVida += Cfg.FamintoRoubo;
                if (m.Alma.Tem(Afixo.Frenetico)) e.VelAtaque *= 1f + Cfg.FreneticoBonus;
                if (m.Alma.Tem(Afixo.Petreo)) e.Armadura += Cfg.PetreoArmadura;
                if (m.Alma.Tem(Afixo.Rancoroso)) e.Rancoroso = true;
                if (m.Alma.Tem(Afixo.Leal)) e.Leal = true;
                if (m.Alma.Tem(Afixo.Incandescente)) e.FracaoFogo = Cfg.IncandescenteFracao;
                // Traço profano
                if (m.Alma.Traco == TracoProfano.Colosso) e.VidaMax *= 1f + Cfg.ColossoVida;
                else if (m.Alma.Traco == TracoProfano.Carniceiro) e.MaisDano *= 1f + Cfg.CarniceiroDano;
                else if (m.Alma.Traco == TracoProfano.Agourento) e.ChanceCritico += Cfg.AgourentoCritico;

                // Bônus de família (3 = bônus, 5 = bônus maior), vale para os membros da família.
                int nFam = contagemFamilia[(int)d.Familia];
                float b = nFam >= 5 ? Cfg.FamiliaBonus5 : nFam >= 3 ? Cfg.FamiliaBonus3 : 0f;
                if (b > 0f)
                {
                    switch (d.Familia)
                    {
                        case Familia.MortoVivo: e.VidaMax *= 1f + b; break;
                        case Familia.Demonio: e.MaisDano *= 1f + b; break;
                        case Familia.Besta: e.VelAtaque *= 1f + b; break;
                        case Familia.Aberracao: e.Armadura += 200f * b; break;
                        case Familia.Espirito: for (int r = 0; r < 6; r++) e.Resistencias[r] += b; break;
                    }
                }
                _minions.Add(e);
                Adicionar(e);
            }
        }

        /// <summary>Bônus do equipamento do necromante que valem para os minions (inclusive os temporários).</summary>
        private void AplicarEquipamentoAoMinion(Entidade e)
        {
            e.VidaMax *= 1f + Bonus.VidaDosMinions;
            e.AumentoDano += Bonus.DanoDosMinions;       // entra na soma dos aumentos da fórmula de dano
            e.VelAtaque *= 1f + Bonus.VelAtaqueDosMinions;
            e.RouboDeVida += Bonus.RouboDeVidaDosMinions;
            // Coroa da Legião Muda: Pequenos custam meio Comando, mas têm menos vida.
            if (Bonus.LegiaoMuda && e.Porte == Porte.Pequeno) e.VidaMax *= 1f - Cfg.LegiaoMudaPenalidadeVida;
        }

        /// <summary>
        /// Grimório da Mãe Carniça: Zumbi temporário (Comum, no nível do necromante) que luta por alguns segundos.
        /// Não ocupa Comando, não deixa cadáver e não conta como minion perdido.
        /// </summary>
        public Entidade CriarZumbiTemporario(Vec2 pos, float duracao)
        {
            var d = TabelaEspecies.Obter(EspecieId.Zumbi);
            var e = CriarBase(Faccao.Minion, d, Limitar(pos));
            int nivel = Math.Min(_nivelNecromante, Cfg.NivelMaximoPorEstagio * Cfg.EstagioMaximo);
            e.VidaMax = Formulas.AtributoMinion(d.Vida, Qualidade.Comum, nivel, _poderTotal, Cfg);
            e.Dano = Formulas.AtributoMinion(d.Dano, Qualidade.Comum, nivel, _poderTotal, Cfg);
            e.Armadura = Formulas.AtributoMinion(d.Armadura, Qualidade.Comum, nivel, _poderTotal, Cfg);
            AplicarEquipamentoAoMinion(e);
            e.Postura = Postura.Agressiva;
            e.Custo = 0;
            e.Temporario = true;
            e.TempoDeVida = duracao;
            Adicionar(e);
            Emitir(new EventoSim { Tipo = TipoEvento.EntidadeSurgiu, Alvo = e.Id, Origem = Necromante.Id, Pos = e.Pos, Especie = e.Especie });
            return e;
        }

        /// <summary>Minion temporário chegou ao fim do tempo: some sem deixar cadáver.</summary>
        /// <summary>Para onde a linha de frente marcha quando não há inimigo acordado mas ainda há quem durma na sala.</summary>
        public bool HaAlvoDeMarcha { get; private set; }
        public Vec2 AlvoDeMarcha { get; private set; }

        /// <summary>
        /// Despertar por proximidade: um inimigo adormecido acorda quando um aliado chega ao raio de alerta dele, e leva junto
        /// os companheiros de matilha que estiverem colados. Sem ninguém acordado, aponta a marcha para o adormecido mais próximo.
        /// </summary>
        private void DespertarPorProximidade()
        {
            HaAlvoDeMarcha = false;
            if (_filaDeSpawn.Count == 0) return;
            float r2 = Cfg.RaioDeAlerta * Cfg.RaioDeAlerta, m2 = Cfg.RaioDeAlertaDaMatilha * Cfg.RaioDeAlertaDaMatilha;
            bool mudou = false;
            for (int i = 0; i < _filaDeSpawn.Count; i++)
            {
                var s = _filaDeSpawn[i];
                if (s.Planejado == null || s.TickDeSpawn <= TickAtual) continue;
                bool perto = false;
                for (int k = 0; k < Entidades.Count && !perto; k++) { var a = Entidades[k]; if (a.Viva && a.Faccao == Faccao.Minion && Vec2.Dist2(a.Pos, s.Planejado.Pos) <= r2) perto = true; }
                if (!perto) continue;
                s.TickDeSpawn = TickAtual; _filaDeSpawn[i] = s; mudou = true;
                for (int j = 0; j < _filaDeSpawn.Count; j++)
                {
                    var o = _filaDeSpawn[j];
                    if (j == i || o.Planejado == null || o.TickDeSpawn <= TickAtual + 2 || Vec2.Dist2(o.Planejado.Pos, s.Planejado.Pos) > m2) continue;
                    o.TickDeSpawn = TickAtual + 1 + (j % 3); _filaDeSpawn[j] = o;
                }
            }
            if (mudou) { _filaDeSpawn.Sort((a, b) => a.TickDeSpawn.CompareTo(b.TickDeSpawn)); Resultado.DespertaresPorProximidade++; }
            if (InimigosVivos > 0) return;
            // ninguém acordado: a frente avança até o adormecido mais próximo do necromante
            float melhor = float.MaxValue; var np = Necromante.Pos;
            for (int i = 0; i < _filaDeSpawn.Count; i++)
            {
                var s = _filaDeSpawn[i]; if (s.Planejado == null) continue;
                float d = Vec2.Dist2(np, s.Planejado.Pos);
                if (d < melhor) { melhor = d; AlvoDeMarcha = s.Planejado.Pos; HaAlvoDeMarcha = true; }
            }
        }

        /// <summary>O Golem do feitiço: um Colosso de Ossos conjurado, forte e passageiro. Só existe um por vez.</summary>
        private bool _golemAprendido;
        private int _pocoesVida, _pocoesMana; private float _pocaoEspera, _limiarVida, _limiarMana;
        /// <summary>Poções que restam no cinto nesta descida.</summary>
        public int PocoesDeVidaNoCinto { get { return _pocoesVida; } }
        public int PocoesDeManaNoCinto { get { return _pocoesMana; } }

        /// <summary>O necromante bebe sozinho: vida quando está ferido, mana quando está seco e há luta.</summary>
        private void BeberPocoes()
        {
            if (_pocaoEspera > 0f) { _pocaoEspera -= DT; return; }
            var n = Necromante;
            if (_pocoesVida > 0 && n.Vida < n.VidaMax * _limiarVida)
            {
                _pocoesVida--; Resultado.PocoesDeVidaUsadas++; _pocaoEspera = Cfg.PocaoRecarga;
                float cura = n.VidaMax * Cfg.PocaoVidaCura; n.Vida = Math.Min(n.VidaMax, n.Vida + cura);
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 33, Valor = cura, Alvo = n.Id, Origem = n.Id, Pos = n.Pos });
            }
            else if (_pocoesMana > 0 && InimigosVivos > 0 && n.Mana < n.ManaMax * _limiarMana)
            {
                _pocoesMana--; Resultado.PocoesDeManaUsadas++; _pocaoEspera = Cfg.PocaoRecarga;
                float m = n.ManaMax * Cfg.PocaoManaCura; n.Mana = Math.Min(n.ManaMax, n.Mana + m);
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 34, Valor = m, Alvo = n.Id, Origem = n.Id, Pos = n.Pos });
            }
        }
        public Entidade GolemVivo { get { for (int i = 0; i < Entidades.Count; i++) if (Entidades[i].Golem && Entidades[i].Viva) return Entidades[i]; return null; } }

        private bool LancarGolem()
        {
            if (!Cfg.GolemAtivo || !_golemAprendido || InimigosVivos == 0 || !PodeLancar(FeiticoId.Golem) || GolemVivo != null) return false;
            var f = Fei(FeiticoId.Golem); var n = Necromante;
            var d = TabelaEspecies.Obter(EspecieId.ColossoDeOssos);
            var e = CriarBase(Faccao.Minion, d, Limitar(new Vec2(n.Pos.X + 2.2f, n.Pos.Y)));
            int nivel = Math.Min(_nivelNecromante, Cfg.NivelMaximoPorEstagio * Cfg.EstagioMaximo);
            e.VidaMax = Formulas.AtributoMinion(d.Vida, Qualidade.Comum, nivel, _poderTotal, Cfg) * f.Potencia;
            e.Dano = Formulas.AtributoMinion(d.Dano, Qualidade.Comum, nivel, _poderTotal, Cfg) * f.Potencia;
            e.Armadura = Formulas.AtributoMinion(d.Armadura, Qualidade.Comum, nivel, _poderTotal, Cfg);
            AplicarEquipamentoAoMinion(e);
            e.Vida = e.VidaMax; e.Postura = Postura.Agressiva; e.Custo = 0; e.Temporario = true; e.TempoDeVida = f.Duracao; e.Golem = true; e.NomeProprio = "Golem de Ossos";
            Adicionar(e);
            PagarFeitico(FeiticoId.Golem, e, e.Pos, 0f);
            Emitir(new EventoSim { Tipo = TipoEvento.EntidadeSurgiu, Alvo = e.Id, Origem = n.Id, Pos = e.Pos, Especie = e.Especie });
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 29, Alvo = e.Id, Origem = n.Id, Pos = e.Pos });
            // surge provocando: os inimigos em volta viram para ele
            float r2 = Cfg.GolemProvocaRaio * Cfg.GolemProvocaRaio;
            for (int i = 0; i < Entidades.Count; i++) { var o = Entidades[i]; if (o.Viva && o.Faccao == Faccao.Inimigo && Vec2.Dist2(o.Pos, e.Pos) <= r2) { o.ProvocadoPor = e.Id; o.ProvocadoTempo = 3f; } }
            Resultado.GolensConjurados++;
            return true;
        }

        private void ExpirarTemporario(Entidade e)
        {
            e.Viva = false;
            e.Vida = 0f;
            e.Animacao = EstadoAnimacao.Morrendo;
            e.TempoMorrendo = Cfg.TempoMorrendo;
            Emitir(new EventoSim { Tipo = TipoEvento.Morte, Alvo = e.Id, Origem = -1, Condicao = CondicaoCadaver.Intacto, Pos = e.Pos, Especie = e.Especie, Extra = 2 });
        }

        private bool HaMinionVivo()
        {
            for (int i = 0; i < Entidades.Count; i++)
                if (Entidades[i].Viva && Entidades[i].Faccao == Faccao.Minion) return true;
            return false;
        }

        /// <summary>
        /// Drop de itens na morte de um inimigo: comum e elite por chance; chefe garante cfg.ItensDoChefe
        /// (o primeiro é no mínimo Raro na primeira vitória). O item vai direto para Resultado.Itens.
        /// </summary>
        private void SoltarItens(Entidade alvo, bool chefeJaDerrotado)
        {
            if (alvo.Chefe)
            {
                for (int i = 0; i < Cfg.ItensDoChefe; i++)
                    SoltarItem(alvo, i == 0 && !chefeJaDerrotado ? RaridadeItem.Raro : RaridadeItem.Comum);
            }
            else if (alvo.Unico) SoltarItem(alvo, RaridadeItem.Magico);   // único sempre larga algo ao menos Mágico
            else if (_rngItens.Chance(GeradorDeItens.ChanceDeDrop(Cfg, alvo.Elite) * (alvo.DaHorda ? 1f / Cfg.Horda : alvo.Campeao ? Cfg.CampeaoPremio : 1f) * _pItem * (1f + _bItem) * (1f + _sorte * Cfg.SorteMaisDropsPorPonto)))
            {
                SoltarItem(alvo, RaridadeItem.Comum);
            }
        }

        private void SoltarItem(Entidade origem, RaridadeItem raridadeMinima)
        {
            var item = GeradorDeItens.Gerar(_rngItens, Cfg, Andar + Cfg.DificuldadeNivelDeItem[_dif], raridadeMinima);
            if (_sorte > 0f && _rngItens.Chance(_sorte * Cfg.SorteChanceDeMelhorPorPonto))
            {
                // sorte: sorteia outro e fica com o de maior raridade
                var outro = GeradorDeItens.Gerar(_rngItens, Cfg, Andar + Cfg.DificuldadeNivelDeItem[_dif], raridadeMinima);
                if (outro.Raridade > item.Raridade) item = outro;
            }
            item = TalvezUnico(item);
            item.NaoIdentificado = Cfg.ItensNaoIdentificados && item.Raridade >= RaridadeItem.Raro;
            Resultado.Itens.Add(item);
            Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = origem.Id, Alvo = Resultado.Itens.Count - 1, Pos = origem.Pos, Extra = (int)item.Raridade, Valor = item.Nivel, Especie = origem.Especie });
        }

        /// <summary>Cria um inimigo já escalado para o andar atual. Público para sandbox/testes; o fluxo normal usa os portais.</summary>
        public Entidade CriarInimigo(EspecieId especie, Vec2 pos, bool elite = false, ModificadorElite mods = ModificadorElite.Nenhum, bool chefe = false)
        {
            var d = TabelaEspecies.Obter(especie);
            var e = CriarBase(Faccao.Inimigo, d, pos);
            float escala = Formulas.EscalaInimigo(Andar, Cfg);
            e.VidaMax = d.Vida * Cfg.FatorVidaInimigo * escala * _pVidaInimigo * Cfg.DificuldadeVida[_dif];
            e.Dano = d.Dano * Cfg.FatorDanoInimigo * escala * Cfg.DificuldadeDano[_dif];
            if (chefe)
            {
                e.Chefe = true;
                e.VidaMax *= Cfg.ChefeMultVida;
                e.Dano *= Cfg.ChefeMultDano;
                e.TempoHabilidade = Cfg.PocaIntervalo * 0.5f;
            }
            else if (elite)
            {
                e.Elite = true;
                e.Modificadores = mods;
                e.VidaMax *= Cfg.EliteMultVida;
                e.Dano *= Cfg.EliteMultDano;
                if ((mods & ModificadorElite.Vampirico) != 0) e.RouboDeVida += Cfg.VampiricoRoubo;
                if ((mods & ModificadorElite.Veloz) != 0) { e.VelAtaque *= 1f + Cfg.VelozBonus; e.VelMovimento *= 1f + Cfg.VelozBonus; }
                if ((mods & ModificadorElite.EscudoDeOssos) != 0) e.Escudo = e.VidaMax * Cfg.EscudoDeOssosFracao;
            }
            Adicionar(e);
            InimigosVivos++;
            Emitir(new EventoSim { Tipo = TipoEvento.EntidadeSurgiu, Alvo = e.Id, Origem = -1, Pos = pos, Especie = especie, Extra = (int)e.Modificadores, Critico = chefe });
            return e;
        }

        /// <summary>Solta um orbe de alma na arena (o fluxo normal faz isso na morte dos inimigos).</summary>
        public OrbeDeAlma CriarOrbe(Vec2 pos, Alma alma, int entidadeOrigem = -1)
        {
            var o = new OrbeDeAlma { Id = _proximoId++, Pos = pos, Alma = alma, TempoRestante = Cfg.DuracaoDoOrbe };
            Orbes.Add(o);
            Resultado.OrbesCaidos++;
            Emitir(new EventoSim { Tipo = TipoEvento.AlmaCaiu, Alvo = o.Id, Origem = entidadeOrigem, Pos = pos, Especie = alma.Especie, Extra = (int)alma.Qualidade });
            return o;
        }

        public Entidade AcharEntidade(int id)
        {
            for (int i = 0; i < Entidades.Count; i++) if (Entidades[i].Id == id) return Entidades[i];
            return null;
        }

        // =====================================================================
        //  Fluxo: andares e ondas
        // =====================================================================

        private void IniciarAndar()
        {
            Onda = 0;
            _tickInicioDoAndar = TickAtual;
            _fatorFuria = 1f;
            _furiaAnunciada = false;
            _suspiroUsado = false; // Manto do Último Suspiro: uma vez por andar
            bool chefe = Formulas.AndarDeChefe(Andar, Cfg);
            _ondaEliteNesteAndar = !chefe && Rng.Chance(Cfg.ChanceOndaElite);
            // Presságio do andar: sorteado num fluxo próprio; muda as regras só deste andar
            PressagioAtual = Pressagio.Nenhum; _aOrcamento = 1f; _aOssos = 1f; _aAlma = 0f; _aDanoInimigo = 1f;
            if (!chefe && Andar >= Cfg.PressagioAPartirDoAndar && _rngPressagio.Chance(Cfg.ChanceDePressagio))
            {
                PressagioAtual = (Pressagio)(1 + _rngPressagio.Intervalo(0, 6));
                switch (PressagioAtual)
                {
                    case Pressagio.Horda: _aOrcamento = 1.4f; _aOssos = 1.3f; break;
                    case Pressagio.Ossario: _aOssos = 1.7f; break;
                    case Pressagio.AlmasInquietas: _aAlma = 1.0f; break;
                    case Pressagio.SedeDeSangue: _aDanoInimigo = 1.2f; _aOssos = 1.4f; break;
                    case Pressagio.CampeaoErrante: _ondaEliteNesteAndar = true; break;
                    case Pressagio.Calmaria: _aOrcamento = 0.7f; break;
                }
            }
            TotalDeOndas = chefe ? 1 : Cfg.OndasPorAndar + (_ondaEliteNesteAndar ? 1 : 0);
            PlanejarSalas(chefe);
            int noAto = Formulas.AndarNoAto(Andar, Cfg);
            PortaisAtivos = noAto <= 3 ? 1 : noAto <= 7 ? 2 : 3;
            Resultado.AndarFinal = Andar;
            Fase = FaseExpedicao.EntreOndas;
            _temporizadorDeFase = 1.0f;
            Emitir(new EventoSim { Tipo = TipoEvento.AndarIniciado, Extra = Andar, Alvo = -1, Origem = -1 });
            PlanejarInimigos();
            PlanejarPatrulha();
            PlanejarObjetos();
            ComecarPelaPrimeiraSala();
        }

        private void AtualizarFluxo()
        {
            switch (Fase)
            {
                case FaseExpedicao.EntreAndares:
                    _temporizadorDeFase -= DT;
                    if (_temporizadorDeFase <= 0f) IniciarAndar();
                    break;
                case FaseExpedicao.EntreOndas:
                    _temporizadorDeFase -= DT;
                    if (_temporizadorDeFase <= 0f) IniciarOnda();
                    break;
                case FaseExpedicao.Explorando:
                    AtualizarExploracao();
                    break;
                case FaseExpedicao.EmOnda:
                    if (_lutaDoRei) AtualizarReiEsqueleto();
                    if (Cfg.DespertarPorProximidade) DespertarPorProximidade();
                    while (_filaDeSpawn.Count > 0 && _filaDeSpawn[0].TickDeSpawn <= TickAtual)
                    {
                        var s = _filaDeSpawn[0];
                        _filaDeSpawn.RemoveAt(0);
                        var portal = Portais[s.Portal];
                        var pos = new Vec2(portal.X, portal.Y + Rng.Intervalo(-0.8f, 0.8f));
                        if (s.Planejado != null) { pos = s.Planejado.Pos; s.Planejado.Desperto = true; }   // já estava na sala, à espera: só desperta
                        var novo = CriarInimigo(s.Especie, Limitar(pos), s.Elite, s.Mods, s.Chefe);
                        if (s.Planejado != null && s.Planejado.Unico) { novo.Unico = true; novo.NomeProprio = s.Planejado.Nome; novo.VidaMax *= Cfg.UnicoMultVida; novo.Vida = novo.VidaMax; Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 20, Alvo = novo.Id, Origem = -1, Pos = novo.Pos }); }
                        else if (s.Rei) { novo.Unico = true; novo.NomeProprio = "Rei Esqueleto"; novo.VidaMax *= Cfg.ReiMultVida; novo.Vida = novo.VidaMax; novo.Dano *= Cfg.ReiMultDano; Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 20, Alvo = novo.Id, Origem = -1, Pos = novo.Pos }); }
                        else if (s.Acougueiro) { novo.Unico = true; novo.NomeProprio = "O Açougueiro"; novo.VidaMax *= Cfg.AcougueiroMultVida; novo.Vida = novo.VidaMax; novo.Dano *= Cfg.AcougueiroMultDano; novo.VelMovimento *= Cfg.AcougueiroMultVelocidade; Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 20, Alvo = novo.Id, Origem = -1, Pos = novo.Pos }); }
                        else if (s.Planejado != null && s.Planejado.Sequito) { novo.VidaMax *= Cfg.SequitoMultVida; novo.Vida = novo.VidaMax; }
                        else if (s.Planejado != null && s.Planejado.Campeao) { novo.Campeao = true; novo.VidaMax *= Cfg.CampeaoMultVida; novo.Vida = novo.VidaMax; novo.Dano *= Cfg.CampeaoMultDano; }
                        else if ((s.Planejado != null || s.Fraco) && !s.Elite && !s.Chefe && Cfg.Horda > 1f) { novo.DaHorda = true; novo.VidaMax /= Cfg.Horda; novo.Vida = novo.VidaMax; novo.Dano /= Cfg.Horda; }
                    }
                    // Fúria: garante que nenhum andar empate para sempre.
                    float tempoNoAndar = (TickAtual - _tickInicioDoAndar - _ticksDeExploracao) * DT;
                    if (tempoNoAndar > Cfg.TempoAteFuria)
                    {
                        _fatorFuria = 1f + Cfg.FuriaPorSegundo * (tempoNoAndar - Cfg.TempoAteFuria);
                        if (!_furiaAnunciada)
                        {
                            _furiaAnunciada = true;
                            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 4, Alvo = -1, Origem = -1 });
                        }
                    }
                    if (_filaDeSpawn.Count == 0 && InimigosVivos == 0)
                    {
                        if (AoLimparSala()) break;
                        if (Onda < TotalDeOndas)
                        {
                            Fase = FaseExpedicao.EntreOndas;
                            _temporizadorDeFase = Cfg.PausaEntreOndas;
                        }
                        else if (Orbes.Count == 0)
                        {
                            ConcluirAndar(); // só depois de resolver os orbes pendentes
                        }
                    }
                    break;
            }
        }

        private void IniciarOnda()
        {
            Onda++;
            bool ondaDeChefe = Formulas.AndarDeChefe(Andar, Cfg);
            bool ondaDeElite = _ondaEliteNesteAndar && Onda == TotalDeOndas;
            // os inimigos desta sala foram sorteados e postos nela quando o andar começou (ver PlanejarInimigos): agora despertam, um a um
            if (Onda - 1 < PlanoDasOndas.Count)
            {
                var plano = PlanoDasOndas[Onda - 1];
                // Diablo 1: os monstros estão espalhados e dormem até o grupo chegar perto. O relógio de cada um fica mais lento (rede de segurança);
                // quem acorda de verdade é a proximidade (ver DespertarPorProximidade)
                float lento = Cfg.DespertarPorProximidade && !ondaDeChefe ? Cfg.DespertarFatorDeTempo : 1f;
                for (int i = 0; i < plano.Count; i++)
                    _filaDeSpawn.Add(new SpawnPendente { Especie = plano[i].Especie, Elite = plano[i].Elite, Mods = plano[i].Mods, Chefe = plano[i].Chefe, Portal = 0, TickDeSpawn = TickAtual + 1 + (int)(plano[i].Atraso * lento), Planejado = plano[i] });
                PortaFechada = false; _portaExtra = 0f;
                // emboscada de corredor: os inimigos desta sala não esperam — já caem em cima do grupo na entrada
                if (Cfg.EmboscadaDeCorredor && !ondaDeChefe && !ondaDeElite && Onda != _ondaDoUnico && Onda > 1 && Andar >= Cfg.EmboscadaDeCorredorAPartirDoAndar && plano.Count > 0 && _rngMasmorra.Chance(Cfg.ChanceDeEmboscadaDeCorredor))
                {
                    var n0 = Necromante.Pos;
                    for (int i = 0; i < plano.Count; i++) { float ang = i * 2.399963f, rr = 2.2f + 0.35f * (i % 5); plano[i].Pos = Limitar(new Vec2(n0.X + 1.5f + rr * (float)Math.Cos(ang), n0.Y + rr * (float)Math.Sin(ang))); plano[i].Atraso = i / 3; }
                    _filaDeSpawn.Clear();
                    for (int i = 0; i < plano.Count; i++)
                        _filaDeSpawn.Add(new SpawnPendente { Especie = plano[i].Especie, Elite = plano[i].Elite, Mods = plano[i].Mods, Chefe = plano[i].Chefe, Portal = 0, TickDeSpawn = TickAtual + 1 + plano[i].Atraso, Planejado = plano[i] });
                    Resultado.EmboscadasDeCorredor++;
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 27, Valor = plano.Count, Alvo = -1, Origem = -1, Pos = n0 });
                }
                if (Cfg.SalasLigadas && !ondaDeChefe && !ondaDeElite) AtrairSalaVizinha(plano);
                _filaDeSpawn.Sort((a, b) => a.TickDeSpawn.CompareTo(b.TickDeSpawn));
            }
            for (int i = 0; i < _minions.Count; i++) _minions[i].Aparou = false;   // cada sala, um aparo novo
            TalvezJuntarPatrulha();
            Fase = FaseExpedicao.EmOnda;
            Emitir(new EventoSim { Tipo = TipoEvento.OndaIniciada, Extra = Onda, Valor = TotalDeOndas, Critico = ondaDeChefe || ondaDeElite, Alvo = -1, Origem = -1 });
        }

        /// <summary>
        /// Masmorra contínua (Diablo 1): as salas não são arenas isoladas. O barulho da luta pode atrair a matilha de uma sala
        /// vizinha ainda não visitada: ela entra pela porta que liga as duas, e aquela sala fica vazia quando o grupo chegar lá.
        /// </summary>
        private void AtrairSalaVizinha(List<InimigoPlanejado> planoAqui)
        {
            if (Onda - 1 >= _salaDaOnda.Count) return;
            int aqui = _salaDaOnda[Onda - 1];
            if (aqui < 0 || aqui >= Mapa.Count) return;
            for (int w = Onda; w < PlanoDasOndas.Count && w < _salaDaOnda.Count; w++)
            {
                int la = _salaDaOnda[w]; var pl = PlanoDasOndas[w];
                if (pl.Count == 0 || la < 0 || la >= Mapa.Count || la == PatrulhaSala) continue;
                if ((_ondaEliteNesteAndar && w + 1 == TotalDeOndas) || w + 1 == _ondaDoUnico) continue;   // elite e Único esperam na sala deles
                int dx = Mapa[la].X - Mapa[aqui].X, dy = Mapa[la].Y - Mapa[aqui].Y;
                if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                if (!_rngMasmorra.Chance(Cfg.ChanceDeAtrairVizinha)) continue;
                // a porta entre as duas salas e a direção "para dentro" desta
                float L = Cfg.LarguraArena, A = Cfg.AlturaArena;
                var porta = dx == 1 ? new Vec2(L - 0.8f, A * 0.5f) : dx == -1 ? new Vec2(0.8f, A * 0.5f) : dy == 1 ? new Vec2(L * 0.5f, A - 0.8f) : new Vec2(L * 0.5f, 0.8f);
                int atraso = (int)(Cfg.AtrairAtraso * Cfg.TicksPorSegundo + 0.5f);
                for (int j = 0; j < pl.Count; j++)
                {
                    var q = pl[j]; float lado = ((j % 3) - 1) * 0.9f, fundo = (j / 3) * 0.7f;
                    q.Sala = aqui; q.Atraso = atraso + j * 2;
                    q.Pos = Limitar(dx != 0 ? new Vec2(porta.X - dx * fundo, porta.Y + lado) : new Vec2(porta.X + lado, porta.Y - dy * fundo));
                    planoAqui.Add(q);
                    _filaDeSpawn.Add(new SpawnPendente { Especie = q.Especie, Elite = q.Elite, Mods = q.Mods, Chefe = q.Chefe, Portal = 0, TickDeSpawn = TickAtual + 1 + q.Atraso, Planejado = q });
                }
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 23, Valor = pl.Count, Alvo = la, Origem = -1, Pos = porta });
                Resultado.SalasAtraidas++;
                pl.Clear();
                return;   // uma vizinha por luta
            }
        }

        /// <summary>Sorteia, no começo do andar, quem está em cada sala de combate e onde. Assim o mapa inteiro já mostra o que espera o grupo.</summary>
        private void PlanejarInimigos()
        {
            PlanoDasOndas.Clear(); _salaDaOnda.Clear();
            bool chefe = Formulas.AndarDeChefe(Andar, Cfg);
            int noAto = Formulas.AtoDoAndar(Andar, Cfg) > 1 ? Cfg.AndaresPorAto : Formulas.AndarNoAto(Andar, Cfg);
            int intervalo = Math.Max(1, (int)(Cfg.IntervaloDeSpawn * Cfg.TicksPorSegundo + 0.5f));
            int sala = -1;
            // em qual sala de combate mora o Único deste andar (nunca na do chefe nem na da elite)
            _ondaDoUnico = 0;
            if (!chefe && Andar >= Cfg.UnicosAPartirDoAndar && _rngMasmorra.Chance(Cfg.ChanceDeUnico))
            {
                int livres = TotalDeOndas - (_ondaEliteNesteAndar ? 1 : 0);
                if (livres >= 1) _ondaDoUnico = 1 + _rngMasmorra.Intervalo(0, livres);
            }
            for (int onda = 1; onda <= TotalDeOndas; onda++)
            {
                // a sala desta onda: a próxima sala de combate do plano
                do { sala++; } while (sala < Salas.Count && Salas[sala] != TipoSala.Combate && Salas[sala] != TipoSala.Elite && Salas[sala] != TipoSala.Chefe);
                var lista = new List<InimigoPlanejado>();
                bool ondaDeElite = _ondaEliteNesteAndar && onda == TotalDeOndas;
                float orcamento = Cfg.OrcamentoBaseOnda + Cfg.OrcamentoPorAndar * (Andar - 1) + Cfg.OrcamentoPorOnda * (onda - 1);
                if (orcamento > Cfg.OrcamentoMaximo) orcamento = Cfg.OrcamentoMaximo;
                orcamento *= _aOrcamento;
                int k = 0;
                // matilhas: os comuns ficam em grupos, como os bandos do Diablo
                int nMat = Math.Max(1, Cfg.MatilhasPorSala);
                var centros = new Vec2[nMat];
                for (int c = 0; c < nMat; c++) centros[c] = new Vec2(_rngMasmorra.Intervalo(Cfg.LarguraArena * 0.58f, Cfg.LarguraArena - 2.6f), _rngMasmorra.Intervalo(2.4f, Cfg.AlturaArena - 2.4f));
                if (chefe)
                {
                    lista.Add(new InimigoPlanejado { Sala = sala, Especie = TabelaEspecies.ChefeDoAto(Formulas.AtoDoAndar(Andar, Cfg)), Chefe = true, Atraso = 0, Pos = new Vec2(Cfg.LarguraArena * 0.74f, Cfg.AlturaArena * 0.5f) });
                    orcamento *= 0.5f; k = 4;
                }
                else if (ondaDeElite)
                {
                    lista.Add(new InimigoPlanejado { Sala = sala, Especie = SortearEspecie(noAto, 99f), Elite = true, Mods = SortearModificadores(), Atraso = 0, Pos = new Vec2(Cfg.LarguraArena * 0.74f, Cfg.AlturaArena * 0.5f) });
                    orcamento *= 0.5f; k = 2;
                }
                // pacote de Campeões: três da mesma espécie, juntos
                if (!chefe && !ondaDeElite && Andar >= Cfg.CampeoesAPartirDoAndar && _rngMasmorra.Chance(Cfg.ChanceDeCampeoes))
                {
                    EspecieId ec = SortearEspecie(noAto, orcamento / (Cfg.CampeoesPorPacote * Cfg.CampeaoCusto));
                    if (ec != EspecieId.Necromante)
                    {
                        var cc = centros[0];
                        for (int q = 0; q < Cfg.CampeoesPorPacote; q++)
                        {
                            lista.Add(new InimigoPlanejado { Sala = sala, Especie = ec, Campeao = true, Atraso = k * intervalo / PortaisAtivos, Pos = new Vec2(cc.X + _rngMasmorra.Intervalo(-1.1f, 1.1f), cc.Y + _rngMasmorra.Intervalo(-1.1f, 1.1f)) });
                            k++;
                        }
                        orcamento -= Cfg.CampeoesPorPacote * Cfg.CampeaoCusto * TabelaEspecies.Obter(ec).CustoOnda;
                    }
                }
                // Único: nome próprio, modificadores de elite e um séquito da mesma espécie em volta
                if (onda == _ondaDoUnico)
                {
                    EspecieId eu = SortearEspecie(noAto, 99f);
                    if (eu != EspecieId.Necromante)
                    {
                        var cu = centros[nMat - 1];
                        lista.Add(new InimigoPlanejado { Sala = sala, Especie = eu, Elite = true, Unico = true, Mods = SortearModificadores(), Nome = NomesDeUnicos.Sortear(_rngMasmorra), Atraso = k * intervalo / PortaisAtivos, Pos = cu });
                        k++;
                        for (int q = 0; q < Cfg.SequitoDoUnico; q++)
                        {
                            lista.Add(new InimigoPlanejado { Sala = sala, Especie = eu, Sequito = true, Atraso = k * intervalo / PortaisAtivos, Pos = new Vec2(cu.X + _rngMasmorra.Intervalo(-1.4f, 1.4f), cu.Y + _rngMasmorra.Intervalo(-1.4f, 1.4f)) });
                            k++;
                        }
                        orcamento *= Cfg.UnicoCusto;
                    }
                }
                orcamento *= Cfg.Horda; int passo = Math.Max(1, (int)(intervalo / PortaisAtivos / Cfg.Horda));
                int inicioDaHorda = k, nHorda = 0;
                while (orcamento > 0.01f)
                {
                    EspecieId esp = SortearEspecie(noAto, orcamento);
                    if (esp == EspecieId.Necromante) break;
                    orcamento -= TabelaEspecies.Obter(esp).CustoOnda;
                    // espalhados pela metade do fundo da sala; despertam aos poucos, como antes chegavam pelos portais
                    var cm = centros[nHorda % nMat];
                    var pos = new Vec2(cm.X + _rngMasmorra.Intervalo(-Cfg.MatilhaRaio, Cfg.MatilhaRaio), cm.Y + _rngMasmorra.Intervalo(-Cfg.MatilhaRaio, Cfg.MatilhaRaio));
                    lista.Add(new InimigoPlanejado { Sala = sala, Especie = esp, Atraso = inicioDaHorda * intervalo / PortaisAtivos + nHorda * passo, Pos = pos });
                    k++; nHorda++;
                }
                PlanoDasOndas.Add(lista); _salaDaOnda.Add(sala);
            }
        }

        /// <summary>Sorteia uma espécie comum por peso, entre as liberadas no andar e que cabem no orçamento.</summary>
        private EspecieId SortearEspecie(int andarNoAto, float orcamento)
        {
            var todas = TabelaEspecies.Todas;
            float total = 0f;
            for (int i = 0; i < todas.Length; i++)
            {
                var d = todas[i];
                if (d.PesoOnda > 0f && !d.Chefe && d.AndarMinimo <= andarNoAto && d.CustoOnda <= orcamento + 0.01f) total += d.PesoOnda * Temas.Peso(Cfg, Andar, d.Familia);
            }
            if (total <= 0f) return EspecieId.Necromante;
            float r = Rng.Proximo01() * total;
            EspecieId ultima = EspecieId.Necromante;
            for (int i = 0; i < todas.Length; i++)
            {
                var d = todas[i];
                if (d.PesoOnda > 0f && !d.Chefe && d.AndarMinimo <= andarNoAto && d.CustoOnda <= orcamento + 0.01f)
                {
                    ultima = d.Id;
                    r -= d.PesoOnda * Temas.Peso(Cfg, Andar, d.Familia);
                    if (r < 0f) return d.Id;
                }
            }
            return ultima;
        }

        /// <summary>1 a 3 modificadores distintos (o máximo cresce com o andar: 1 até o 3, 2 até o 6, 3 depois).</summary>
        public ModificadorElite SortearModificadores()
        {
            int max = 1 + (Andar - 1) / 3;
            if (max > 3) max = 3;
            int n = Rng.Intervalo(1, max + 1);
            ModificadorElite mods = ModificadorElite.Nenhum;
            int postos = 0;
            while (postos < n)
            {
                var m = (ModificadorElite)(1 << Rng.Intervalo(0, 4));
                if ((mods & m) != 0) continue;
                mods |= m;
                postos++;
            }
            return mods;
        }

        private void ConcluirAndar()
        {
            // Cadáveres de inimigos não usados viram Restos (por condição).
            for (int i = 0; i < Cadaveres.Count; i++)
            {
                var c = Cadaveres[i];
                if (!c.DeMinion)
                {
                    Resultado.Restos[(int)c.Condicao]++;
                    Emitir(new EventoSim { Tipo = TipoEvento.RecursoGanho, Extra = 2, Valor = 1f, Condicao = c.Condicao, Pos = c.Pos, Alvo = c.Id, Origem = -1 });
                }
                Emitir(new EventoSim { Tipo = TipoEvento.CadaverRemovido, Alvo = c.Id, Pos = c.Pos, Origem = -1 });
            }
            Cadaveres.Clear();
            Projeteis.Clear();
            for (int i = 0; i < Pocas.Count; i++) Emitir(new EventoSim { Tipo = TipoEvento.PocaExpirou, Alvo = Pocas[i].Id, Pos = Pocas[i].Pos, Origem = -1 });
            Pocas.Clear();

            int ticks = TickAtual - _tickInicioDoAndar;
            Resultado.AndaresConcluidos++;
            Resultado.AndaresRegistrados.Add(Andar);
            Resultado.TicksPorAndar.Add(ticks);
            if (Andar > Resultado.MaiorAndarConcluido) Resultado.MaiorAndarConcluido = Andar;
            Emitir(new EventoSim { Tipo = TipoEvento.AndarConcluido, Extra = Andar, Valor = ticks * DT, Alvo = -1, Origem = -1 });
            TalvezAbrirEncruzilhada();
            PressagioAtual = Pressagio.Nenhum; _aOrcamento = 1f; _aOssos = 1f; _aAlma = 0f; _aDanoInimigo = 1f;   // o presságio acaba com o andar

            if (Andar % Cfg.AndaresPorAncoragem == 0 && Andar + 1 > Resultado.AncoragemAlcancada)
            {
                Resultado.AncoragemAlcancada = Andar + 1;
                Emitir(new EventoSim { Tipo = TipoEvento.AncoragemAlcancada, Extra = Andar + 1, Alvo = -1, Origem = -1 });
            }

            if (Resultado.AndaresConcluidos >= LimiteDeAndares) { Encerrar(MotivoFim.LimiteDeAndares); return; }

            // Recuperação parcial dos sobreviventes; minions mortos só voltam no fim da expedição (ou por Reerguer).
            bool restaurar = Modo == ModoExpedicao.Cacar && Cfg.CacarRestauraExercito;
            if (restaurar)
            {
                for (int i = 0; i < _minions.Count; i++)
                {
                    var m = _minions[i];
                    if (!m.Viva) Reviver(m, m.Posto, 1f);
                }
            }
            for (int i = 0; i < Entidades.Count; i++)
            {
                var e = Entidades[i];
                if (!e.Viva || e.Faccao == Faccao.Inimigo) continue;
                e.Vida += e.VidaMax * (restaurar ? 1f : Cfg.CuraEntreAndares);
                if (e.Vida > e.VidaMax) e.Vida = e.VidaMax;
            }

            if (Modo == ModoExpedicao.Avancar) Andar++;
            Fase = FaseExpedicao.EntreAndares;
            _temporizadorDeFase = Cfg.PausaEntreAndares;
        }

        private void Reviver(Entidade m, Vec2 pos, float fracaoDeVida)
        {
            m.Viva = true;
            m.Vida = m.VidaMax * fracaoDeVida;
            m.Pos = pos;
            m.Animacao = EstadoAnimacao.Parado;
            m.TempoAcao = 0f;
            m.Recarga = 0f;
            m.FraquezaTempo = 0f;
            m.ProvocadoPor = -1;
            if (!m.NaLista) { m.NaLista = true; Entidades.Add(m); }
        }

        // =====================================================================
        //  Efeitos globais, orbes, limpeza
        // =====================================================================

        private void AtualizarEfeitosGlobais()
        {
            if (SacrificioTempo > 0f) SacrificioTempo -= DT;

            // Poças de sangue: curam o chefe dono enquanto ele estiver dentro do raio.
            for (int i = Pocas.Count - 1; i >= 0; i--)
            {
                var p = Pocas[i];
                p.TempoRestante -= DT;
                var dono = AcharEntidade(p.DonoId);
                if (p.TempoRestante <= 0f || dono == null || !dono.Viva)
                {
                    Emitir(new EventoSim { Tipo = TipoEvento.PocaExpirou, Alvo = p.Id, Pos = p.Pos, Origem = p.DonoId });
                    Pocas.RemoveAt(i);
                    continue;
                }
                if (Vec2.Dist2(dono.Pos, p.Pos) <= p.Raio * p.Raio && dono.Vida < dono.VidaMax)
                {
                    float cura = dono.VidaMax * Cfg.PocaCuraPorSegundo * DT;
                    dono.Vida = Math.Min(dono.VidaMax, dono.Vida + cura);
                    _acumuladoCuraPoca += cura;
                }
            }
            if (_acumuladoCuraPoca > 0f && TickAtual % Cfg.TicksPorSegundo == 0)
            {
                // Um evento de cura por segundo (evita inundar a camada visual).
                for (int i = 0; i < Entidades.Count; i++)
                    if (Entidades[i].Chefe && Entidades[i].Viva)
                    {
                        Emitir(new EventoSim { Tipo = TipoEvento.Cura, Alvo = Entidades[i].Id, Origem = -1, Valor = _acumuladoCuraPoca, Pos = Entidades[i].Pos });
                        break;
                    }
                _acumuladoCuraPoca = 0f;
            }
        }

        private void AtualizarOrbes()
        {
            for (int i = Orbes.Count - 1; i >= 0; i--)
            {
                var o = Orbes[i];
                o.TempoRestante -= DT;
                if (o.TempoRestante <= 1e-3f) // tolerância ao acúmulo de float: some exatamente após DuracaoDoOrbe
                {
                    if (_ceifaOrbe == o) { _ceifaOrbe = null; _ceifaTempo = 0f; }
                    Resultado.OrbesPerdidos++;
                    Emitir(new EventoSim { Tipo = TipoEvento.OrbeExpirou, Alvo = o.Id, Pos = o.Pos, Origem = -1 });
                    Orbes.RemoveAt(i);
                }
            }
        }

        private void LimparMortos()
        {
            for (int i = Entidades.Count - 1; i >= 0; i--)
            {
                var e = Entidades[i];
                if (e.Viva || e == Necromante) continue;
                e.TempoMorrendo -= DT;
                if (e.TempoMorrendo <= 0f)
                {
                    e.NaLista = false;
                    Entidades.RemoveAt(i);
                }
            }
        }

        /// <summary>Empurra levemente unidades da mesma facção que estejam sobrepostas (só estético/espacial).</summary>
        private void SepararUnidades()
        {
            float raio = Cfg.RaioSeparacao, raio2 = raio * raio;
            int n = Entidades.Count;
            for (int i = 0; i < n; i++)
            {
                var a = Entidades[i];
                if (!a.Viva || a == Necromante) continue;
                for (int j = i + 1; j < n; j++)
                {
                    var b = Entidades[j];
                    if (!b.Viva || b == Necromante || b.Faccao != a.Faccao) continue;
                    float dx = b.Pos.X - a.Pos.X, dy = b.Pos.Y - a.Pos.Y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= raio2) continue;
                    float d = MathF.Sqrt(d2);
                    float nx, ny;
                    if (d < 1e-4f) { nx = 0f; ny = (a.Id < b.Id) ? 1f : -1f; d = 0f; }
                    else { nx = dx / d; ny = dy / d; }
                    float empurrao = (raio - d) * 0.25f;
                    a.Pos = Limitar(new Vec2(a.Pos.X - nx * empurrao, a.Pos.Y - ny * empurrao));
                    b.Pos = Limitar(new Vec2(b.Pos.X + nx * empurrao, b.Pos.Y + ny * empurrao));
                }
            }
        }

        private Vec2 Limitar(Vec2 p)
        {
            if (p.X < 0.3f) p.X = 0.3f; else if (p.X > Cfg.LarguraArena - 0.3f) p.X = Cfg.LarguraArena - 0.3f;
            if (p.Y < 0.3f) p.Y = 0.3f; else if (p.Y > Cfg.AlturaArena - 0.3f) p.Y = Cfg.AlturaArena - 0.3f;
            return p;
        }

        // =====================================================================
        //  Hash de estado (testes de determinismo, detecção de dessincronia)
        // =====================================================================

        public ulong HashDoEstado()
        {
            ulong h = 14695981039346656037UL;
            Misturar(ref h, TickAtual); Misturar(ref h, (int)Fase); Misturar(ref h, Andar); Misturar(ref h, Onda);
            Misturar(ref h, (int)(Rng.Estado & 0xFFFFFFFF)); Misturar(ref h, (int)(Rng.Estado >> 32));
            for (int i = 0; i < Entidades.Count; i++)
            {
                var e = Entidades[i];
                Misturar(ref h, e.Id); Misturar(ref h, (int)e.Especie); Misturar(ref h, e.Viva ? 1 : 0); Misturar(ref h, (int)e.Animacao);
                Misturar(ref h, e.Pos.X); Misturar(ref h, e.Pos.Y); Misturar(ref h, e.Vida); Misturar(ref h, e.Mana);
                Misturar(ref h, e.Recarga); Misturar(ref h, e.Escudo);
            }
            for (int i = 0; i < Cadaveres.Count; i++) { Misturar(ref h, Cadaveres[i].Id); Misturar(ref h, (int)Cadaveres[i].Condicao); }
            for (int i = 0; i < Orbes.Count; i++) { Misturar(ref h, Orbes[i].Id); Misturar(ref h, Orbes[i].TempoRestante); Misturar(ref h, (int)Orbes[i].Alma.Qualidade); }
            for (int i = 0; i < Pocas.Count; i++) { Misturar(ref h, Pocas[i].Id); Misturar(ref h, Pocas[i].TempoRestante); }
            for (int i = 0; i < Projeteis.Count; i++) { Misturar(ref h, Projeteis[i].Id); Misturar(ref h, Projeteis[i].Pos.X); Misturar(ref h, Projeteis[i].Pos.Y); }
            Misturar(ref h, (int)Resultado.Ossos); Misturar(ref h, (int)Resultado.Sangue); Misturar(ref h, Resultado.Almas.Count);
            Misturar(ref h, (int)(_rngItens.Estado & 0xFFFFFFFF)); Misturar(ref h, (int)(_rngItens.Estado >> 32));
            Misturar(ref h, Resultado.Itens.Count);
            for (int i = 0; i < Resultado.Itens.Count; i++)
            {
                var it = Resultado.Itens[i];
                Misturar(ref h, (int)it.Tipo); Misturar(ref h, (int)it.Raridade); Misturar(ref h, it.Nivel); Misturar(ref h, (int)it.Efeito);
                Misturar(ref h, it.Implicito.Valor);
                for (int k = 0; k < it.Afixos.Count; k++) { Misturar(ref h, (int)it.Afixos[k].Afixo); Misturar(ref h, it.Afixos[k].Valor); }
            }
            Misturar(ref h, Resultado.InimigosMortos); Misturar(ref h, Resultado.Xp);
            return h;
        }

        private static void Misturar(ref ulong h, int v)
        {
            unchecked
            {
                uint u = (uint)v;
                for (int i = 0; i < 4; i++) { h ^= (u >> (i * 8)) & 0xFF; h *= 1099511628211UL; }
            }
        }

        private static void Misturar(ref ulong h, float v) { Misturar(ref h, BitConverter.SingleToInt32Bits(v)); }
    }
}
