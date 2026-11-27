using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>Tipo de cada sala de um andar. As de combate correspondem às ondas; as outras ficam entre elas.</summary>
    public enum TipoSala { Combate = 0, Elite = 1, Chefe = 2, Bau = 3, Santuario = 4, AlmasPresas = 5, Emboscada = 6, Fonte = 7, Secreta = 8, Mercador = 9, AltarDeSangue = 10, Vazia = 11, Armadilha = 12, Cofre = 13, ForjaDeOssos = 14, Ninho = 15, Biblioteca = 16, FossoDeMonstros = 17, SalaDeTortura = 18, Arsenal = 19, Acougue = 20, TumbaDoRei = 21, FonteEnvenenada = 22, CamaraDeOssos = 23, SaloesDosCegos = 24, FortalezaRubra = 25, PocoDosLamentos = 26 }

    /// <summary>Posição de uma sala no mapa do andar (grade) e de qual sala já colocada se chega a ela.</summary>
    [Serializable]
    public struct CelulaDoMapa { public int X; public int Y; public int Pai; }

    /// <summary>Ambiente da sala do chefe: muda as regras da luta para os dois lados.</summary>
    public enum AmbienteDeChefe { Nenhum = 0, Lava = 1, NevoaPutrida = 2, Geada = 3, Desabamento = 4 }

    /// <summary>Qual nível secreto se abriu.</summary>
    public enum NivelSecreto { Nenhum = 0, PastoProfano = 1, CamaraDoTesouro = 2, CriptaDosEspelhos = 3 }

    /// <summary>Bênção de santuário: vale até o fim do andar.</summary>
    /// <summary>
    /// Santuários de efeito incerto, como os do Diablo 1: cada um tem um nome e uma frase enigmática, e o que ele faz só se
    /// descobre usando. Uns ajudam, outros cobram um preço.
    /// </summary>
    public enum Bencao { Nenhuma = 0, Misterioso = 1, Sombrio = 2, Estranho = 3, Reluzente = 4, Oculto = 5, Religioso = 6, Magico = 7, Ornado = 8, Tetrico = 9, Profano = 10 }

    /// <summary>
    /// A masmorra: cada andar é uma sequência de salas. O necromante atravessa salas de combate (as ondas) e, entre
    /// elas, encontra baús, santuários, almas presas, fontes e emboscadas. A sala do chefe tem um ambiente sorteado.
    /// Raramente, o fim de um andar abre um nível secreto. Tudo funciona sozinho; o toque do jogador na sala rende mais.
    /// </summary>
    public sealed partial class Simulacao
    {
        private struct Perigo { public Vec2 Pos; public int TickDoEstouro; public bool Pedra; }
        public NivelSecreto SecretoAtual { get; private set; }

        /// <summary>Plano de salas do andar atual e a sala em que o grupo está.</summary>
        public readonly List<TipoSala> Salas = new List<TipoSala>(8);
        /// <summary>Mapa do andar: uma célula por sala, na mesma ordem de Salas. O grupo visita todas antes de descer.</summary>
        public readonly List<CelulaDoMapa> Mapa = new List<CelulaDoMapa>(12);
        public int SalaAtual { get; private set; }
        public AmbienteDeChefe Ambiente { get; private set; }
        public Bencao BencaoAtual { get; private set; }
        public bool EmNivelSecreto { get { return _emSecreto; } }
        /// <summary>Onde está o objeto da sala especial (baú, santuário...), enquanto Fase == Explorando.</summary>
        public Vec2 PosDoObjeto { get; private set; }
        /// <summary>Cheat/teste: o próximo andar comum concluído abre o nível secreto.</summary>
        public bool ForcarNivelSecreto;

        private readonly Rng _rngMasmorra;
        private readonly List<Perigo> _perigos = new List<Perigo>(4);
        private bool _ninhoChocou, _fosso;
        private bool _toqueNaSala, _emEmboscada, _mimico, _emSecreto, _secretoTestado;
        private float _bDanoAliado = 1f, _bProtecao = 1f, _bOssos = 1f, _bAlma, _bItem;
        /// <summary>Santuário: três bênçãos à escolha (null quando não há oferta). Sem escolha no prazo, o necromante pega uma ao acaso.</summary>
        public int[] BencaosOferecidas { get; private set; }
        public float TempoDaBencao { get; private set; }
        private int _escolhaDeBencao = -1;
        private int _santuariosVistos;   // bits dos santuários cujo efeito o jogador já conhece (vem do progresso)
        /// <summary>O jogador escolhe uma das três bênçãos oferecidas (0 a 2). Escolher conta como pôr a mão: a bênção vem mais forte.</summary>
        public void ComandoEscolherBencao(int opcao) { if (BencaosOferecidas != null && opcao >= 0 && opcao < 2 && _escolhaDeBencao < 0) { _escolhaDeBencao = opcao; Resultado.InteracoesPorToque++; } }
        private float _tempoAmbiente;
        private int _acougueiro, _recordeInicial, _reiEsqueleto, _fonte, _camara, _cegos, _fortaleza, _poco; private bool _lutaDoAcougueiro, _lutaDoRei, _lutaDaFonte, _lutaDaCamara, _lutaDosCegos, _lutaDaFortaleza, _lutaDoPoco; private float _tempoDoRei;
        private int _ticksDeExploracao;
        private float _viagem;    // caminhada até a sala atual (0 na primeira sala do andar)
        /// <summary>Verdadeiro enquanto o grupo ainda caminha até o objeto da sala (o necromante só vai até ele depois).</summary>
        public bool CaminhandoAteASala { get { return Fase == FaseExpedicao.Explorando && _temporizadorDeFase > (TipoDaSalaAtual == TipoSala.Vazia ? Cfg.TempoDeSalaVazia : Cfg.TempoDeSalaEspecial); } }

        public TipoSala TipoDaSalaAtual { get { return SalaAtual >= 0 && SalaAtual < Salas.Count ? Salas[SalaAtual] : TipoSala.Combate; } }

        /// <summary>Toque do jogador no objeto da sala (baú, santuário, fonte, almas): o resultado melhora.</summary>
        /// <summary>Há uma porta emperrada entre o grupo e a próxima sala (vale até ser aberta ou arrombada).</summary>
        public bool PortaFechada { get; private set; }
        private float _portaExtra;
        /// <summary>O jogador abre a porta emperrada na hora (em vez de esperar o grupo arrombar).</summary>
        public bool ComandoAbrirPorta()
        {
            if (!PortaFechada || _portaExtra <= 0f) return false;
            _temporizadorDeFase = Math.Max(0f, _temporizadorDeFase - _portaExtra); _viagem = Math.Max(0f, _viagem - _portaExtra);
            _portaExtra = 0f; PortaFechada = false; Resultado.InteracoesPorToque++; Resultado.PortasArrombadas--; Resultado.PortasAbertasPorToque++;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 26, Alvo = SalaAtual, Origem = -1, Pos = Necromante.Pos });
            return true;
        }
        public void ComandoInteragir() { if (Fase == FaseExpedicao.Explorando && !_toqueNaSala) { _toqueNaSala = true; Resultado.InteracoesPorToque++; } }

        private void PlanejarSalas(bool chefe)
        {
            Salas.Clear(); SalaAtual = 0; ChaveNaSala = -1; TemChave = false;
            BencaoAtual = Bencao.Nenhuma; _bDanoAliado = 1f; _bProtecao = 1f; _bOssos = 1f; _bAlma = 0f; _bItem = 0f; BencaosOferecidas = null; _escolhaDeBencao = -1;
            _fosso = false; _ninhoChocou = false; _emEmboscada = false; _mimico = false; _emSecreto = false; _secretoTestado = false; _toqueNaSala = false;
            _perigos.Clear(); _tempoAmbiente = 0f; _ticksDeExploracao = 0; _viagem = 0f;
            Ambiente = AmbienteDeChefe.Nenhum;
            if (chefe)
            {
                // antecâmaras: o covil do chefe fica no fundo do andar
                Salas.Add(TipoSala.Vazia);
                if (_rngMasmorra.Chance(0.5f)) Salas.Add(TipoSala.Vazia);
                Salas.Add(TipoSala.Chefe);
                Ambiente = (AmbienteDeChefe)(1 + _rngMasmorra.Intervalo(0, 4));
                Emitir(new EventoSim { Tipo = TipoEvento.AmbienteDefinido, Extra = (int)Ambiente, Alvo = -1, Origem = -1 });
                GerarMapa();
                return;
            }
            float vaziaExtra = Cfg.TemasDeAndar && Temas.Do(Andar) >= TemaDeAndar.Cavernas ? Cfg.TemaFundoSalaVaziaExtra : 0f;
            bool temCofre = false;
            for (int w = 1; w <= TotalDeOndas; w++)
            {
                Salas.Add(_ondaEliteNesteAndar && w == TotalDeOndas ? TipoSala.Elite : TipoSala.Combate);
                if (w == 1 && TotalDeOndas > 1 && (_acougueiro == 1 || (Cfg.ForcarAcougueiro && _acougueiro != 3)) && Andar == Cfg.AcougueiroAndar) Salas.Add(TipoSala.Acougue);   // missão do Açougueiro: a porta trancada do andar 2
                if (w == 1 && TotalDeOndas > 1 && (_reiEsqueleto == 1 || (Cfg.ForcarReiEsqueleto && _reiEsqueleto != 3)) && Andar == Cfg.ReiAndar) Salas.Add(TipoSala.TumbaDoRei);   // missão do Rei Esqueleto: a tumba do andar 3
                if (w == 1 && TotalDeOndas > 1 && (_fonte == 1 || (Cfg.ForcarFonte && _fonte != 3)) && Andar == Cfg.FonteAndar) Salas.Add(TipoSala.FonteEnvenenada);   // missão da Fonte Envenenada
                if (w == 1 && TotalDeOndas > 1 && (_camara == 1 || (Cfg.ForcarCamara && _camara != 3)) && Andar == Cfg.CamaraAndar) Salas.Add(TipoSala.CamaraDeOssos);   // missão da Câmara de Ossos
                if (w == 1 && TotalDeOndas > 1 && (_cegos == 1 || (Cfg.ForcarCegos && _cegos != 3)) && Andar == Cfg.CegosAndar) Salas.Add(TipoSala.SaloesDosCegos);   // missão dos Salões dos Cegos
                if (w == 1 && TotalDeOndas > 1 && (_fortaleza == 1 || (Cfg.ForcarFortaleza && _fortaleza != 3)) && Andar == Cfg.FortalezaAndar) Salas.Add(TipoSala.FortalezaRubra);   // missão da Fortaleza Rubra
                if (w == 1 && TotalDeOndas > 1 && (_poco == 1 || (Cfg.ForcarPoco && _poco != 3)) && Andar == Cfg.PocoAndar) Salas.Add(TipoSala.PocoDosLamentos);   // missão do Poço dos Lamentos
                bool forcar = Cfg.ForcarCofre && !temCofre && w < TotalDeOndas;   // só para testes
                bool forcarTipo = Cfg.ForcarSalaEspecial > 0 && w == 1 && TotalDeOndas > 1;   // só para testes e capturas
                if (forcar || forcarTipo || (w < TotalDeOndas && Andar >= Cfg.SalasEspeciaisAPartirDoAndar && _rngMasmorra.Chance(Cfg.ChanceDeSalaEspecial)))
                {
                    float r = forcar ? 99f : _rngMasmorra.Proximo01() * 100f;
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSalaVazia + vaziaExtra)) Salas.Add(TipoSala.Vazia);
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSegundaSalaVazia + vaziaExtra)) Salas.Add(TipoSala.Vazia);
                    Salas.Add(r < 22f ? TipoSala.Bau : r < 28f ? TipoSala.ForjaDeOssos : r < 42f ? TipoSala.Santuario : r < 48f ? TipoSala.AlmasPresas : r < 52f ? TipoSala.FossoDeMonstros : r < 60f ? TipoSala.Fonte : r < 65f ? TipoSala.Biblioteca : r < 71f ? TipoSala.Emboscada : r < 76f ? TipoSala.Ninho : r < 81f ? TipoSala.Mercador : r < 84f ? TipoSala.Arsenal : r < 88f ? TipoSala.AltarDeSangue : r < 91f ? TipoSala.SalaDeTortura : r < 95f || temCofre ? TipoSala.Armadilha : TipoSala.Cofre);
                    if (forcarTipo) Salas[Salas.Count - 1] = (TipoSala)Cfg.ForcarSalaEspecial;
                    if (Salas[Salas.Count - 1] == TipoSala.Cofre) temCofre = true;
                }
                else if (w < TotalDeOndas)
                {
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSalaVazia + vaziaExtra)) Salas.Add(TipoSala.Vazia);
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSegundaSalaVazia + vaziaExtra)) Salas.Add(TipoSala.Vazia);
                }
            }
            // cofre trancado: a chave fica com os inimigos de uma sala de combate comum. Na ordem sorteada o cofre pode vir antes
            // da chave (e fica fechado); o jogador que olha o mapa muda a rota e passa primeiro pela chave.
            if (temCofre)
            {
                int candidatas = 0;
                for (int i = 0; i < Salas.Count - 1; i++) if (Salas[i] == TipoSala.Combate) candidatas++;
                if (candidatas == 0) { for (int i = 0; i < Salas.Count; i++) if (Salas[i] == TipoSala.Cofre) Salas[i] = TipoSala.Bau; }
                else
                {
                    int qual = _rngMasmorra.Intervalo(0, candidatas);
                    for (int i = 0; i < Salas.Count - 1; i++) if (Salas[i] == TipoSala.Combate && qual-- == 0) { ChaveNaSala = i; break; }
                }
            }
            GerarMapa();
        }

        // ---- patrulha errante: um bando que muda de sala enquanto o grupo explora; quem cruzar com ele luta contra mais gente ----
        /// <summary>Sala em que a patrulha está agora (-1 = não há patrulha neste andar, ou ela já foi enfrentada).</summary>
        public int PatrulhaSala { get; private set; } = -1;
        public readonly List<EspecieId> Patrulha = new List<EspecieId>(4);
        private float _tempoPatrulha; private bool _patrulhaEmCampo;

        private void PlanejarPatrulha()
        {
            PatrulhaSala = -1; Patrulha.Clear(); _patrulhaEmCampo = false;
            if (Formulas.AndarDeChefe(Andar, Cfg) || Andar < Cfg.PatrulhaAPartirDoAndar || _salaDaOnda.Count < 2 || !_rngMasmorra.Chance(Cfg.ChanceDePatrulha)) return;
            int noAto = Formulas.AtoDoAndar(Andar, Cfg) > 1 ? Cfg.AndaresPorAto : Formulas.AndarNoAto(Andar, Cfg);
            for (int k = 0; k < Cfg.PatrulhaTamanho; k++) { var esp = SortearEspecie(noAto, 2.01f); if (esp != EspecieId.Necromante) Patrulha.Add(esp); }
            if (Patrulha.Count == 0) return;
            PatrulhaSala = _salaDaOnda[_salaDaOnda.Count - 1];
            _tempoPatrulha = Cfg.PatrulhaIntervalo;
        }

        private void AtualizarPatrulha()
        {
            if (PatrulhaSala < 0) return;
            _tempoPatrulha -= DT;
            if (_tempoPatrulha > 0f) return;
            _tempoPatrulha = Cfg.PatrulhaIntervalo;
            // muda para outra sala de combate ainda não visitada (se houver)
            int opcoes = 0;
            for (int w = Onda; w < _salaDaOnda.Count; w++) if (_salaDaOnda[w] != PatrulhaSala) opcoes++;
            if (opcoes == 0) return;
            int qual = _rngMasmorra.Intervalo(0, opcoes);
            for (int w = Onda; w < _salaDaOnda.Count; w++) if (_salaDaOnda[w] != PatrulhaSala && qual-- == 0) { PatrulhaSala = _salaDaOnda[w]; break; }
        }

        /// <summary>Chamado quando uma onda começa: se a patrulha está nesta sala, entra na luta.</summary>
        private void TalvezJuntarPatrulha()
        {
            if (PatrulhaSala < 0 || Onda - 1 >= _salaDaOnda.Count || _salaDaOnda[Onda - 1] != PatrulhaSala) return;
            for (int k = 0; k < Patrulha.Count; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = Patrulha[k], Portal = k % 3, TickDeSpawn = TickAtual + 15 + k * 6 });
            _filaDeSpawn.Sort((a, b) => a.TickDeSpawn.CompareTo(b.TickDeSpawn));
            PatrulhaSala = -1; _patrulhaEmCampo = true; Resultado.PatrulhasEnfrentadas++;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 14, Alvo = -1, Origem = -1, Pos = Necromante.Pos });
        }

        /// <summary>Sala de combate cujos inimigos guardam a chave do cofre (-1 se o andar não tem cofre).</summary>
        public int ChaveNaSala { get; private set; } = -1;
        public bool TemChave { get; private set; }

        private static readonly int[] DX = { 1, 0, 0, -1 }, DY = { 0, 1, -1, 0 };

        /// <summary>Espalha as salas numa grade: cada sala nova nasce colada a uma já colocada (quase sempre a anterior), sem sobrepor.</summary>
        private void GerarMapa()
        {
            Mapa.Clear();
            for (int i = 0; i < Salas.Count; i++) ColocarNoMapa(i);
        }

        private void ColocarNoMapa(int i)
        {
            if (i == 0) { Mapa.Add(new CelulaDoMapa { X = 0, Y = 0, Pai = -1 }); return; }
            // Mapa compacto ("quadrado"): entre todas as células livres coladas a uma sala já posta, fica a que deixa o
            // andar mais próximo de um bloco, com preferência por continuar da sala anterior (o grupo anda menos).
            int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
            for (int j = 0; j < Mapa.Count; j++) { x0 = Math.Min(x0, Mapa[j].X); x1 = Math.Max(x1, Mapa[j].X); y0 = Math.Min(y0, Mapa[j].Y); y1 = Math.Max(y1, Mapa[j].Y); }
            bool fundo = Cfg.MapaIrregularNosTemasFundos && Cfg.TemasDeAndar && Temas.Do(Andar) >= TemaDeAndar.Cavernas;
            float melhor = float.MaxValue; var escolha = new CelulaDoMapa { X = Mapa[i - 1].X + 1, Y = Mapa[i - 1].Y, Pai = i - 1 };
            for (int pai = i - 1; pai >= 0; pai--)
            {
                for (int d = 0; d < 4; d++)
                {
                    int x = Mapa[pai].X + DX[d], y = Mapa[pai].Y + DY[d];
                    bool livre = true; int vizinhas = 0;
                    for (int j = 0; j < Mapa.Count; j++)
                    {
                        if (Mapa[j].X == x && Mapa[j].Y == y) { livre = false; break; }
                        if (Math.Abs(Mapa[j].X - x) + Math.Abs(Mapa[j].Y - y) == 1) vizinhas++;
                    }
                    if (!livre) continue;
                    int larg = Math.Max(x1, x) - Math.Min(x0, x) + 1, alt = Math.Max(y1, y) - Math.Min(y0, y) + 1;
                    // Cavernas e Inferno: o andar deixa de ser um bloco. Galerias tortas, com ramais e becos, até o limite de 5 salas de lado
                    float nota = fundo
                        ? (Math.Max(larg, alt) > Cfg.MapaFundoLadoMaximo ? 1000f : 0f) + Math.Max(larg, alt) * 1.5f + vizinhas * 1.2f + (pai == i - 1 ? 0f : 1.2f) + _rngMasmorra.Proximo01() * 6f
                        : Math.Max(larg, alt) * 10f + (larg + alt) - vizinhas * 0.8f + (pai == i - 1 ? 0f : 2.5f) + _rngMasmorra.Proximo01() * 1.5f;
                    if (nota < melhor) { melhor = nota; escolha = new CelulaDoMapa { X = x, Y = y, Pai = pai }; }
                }
            }
            Mapa.Add(escolha);
        }

        /// <summary>Se o andar começa por uma sala sem combate (antecâmara do chefe), abre a exploração já.</summary>
        private void ComecarPelaPrimeiraSala()
        {
            var t = TipoDaSalaAtual;
            if (t != TipoSala.Combate && t != TipoSala.Elite && t != TipoSala.Chefe) IniciarSalaEspecial(t);
        }

        /// <summary>
        /// Rota do jogador: a sala indicada passa a ser a próxima visitada. Só vale para salas ainda não visitadas e nunca
        /// para a última (a da escada, do campeão ou do chefe). Devolve true se a ordem mudou.
        /// </summary>
        public bool ComandoIrPara(int sala)
        {
            int p = SalaAtual + 1;
            if (_emSecreto || sala <= p || sala >= Salas.Count - 1 || p >= Salas.Count - 1) return sala == p && p < Salas.Count;
            var tipo = Salas[sala]; var celula = Mapa[sala]; _chaveRemapeada = false;
            for (int i = sala; i > p; i--) { Salas[i] = Salas[i - 1]; Mapa[i] = Mapa[i - 1]; }
            Salas[p] = tipo; Mapa[p] = celula;
            // os inimigos já postos acompanham as suas salas, e as ondas voltam a seguir a ordem de visita
            for (int w = 0; w < PlanoDasOndas.Count; w++)
            {
                int s0 = _salaDaOnda[w], s1 = s0 == sala ? p : (s0 >= p && s0 < sala ? s0 + 1 : s0);
                _salaDaOnda[w] = s1;
                if (w == 0 && PatrulhaSala >= 0) PatrulhaSala = PatrulhaSala == sala ? p : (PatrulhaSala >= p && PatrulhaSala < sala ? PatrulhaSala + 1 : PatrulhaSala);
                if (w == 0 && ChaveNaSala >= 0 && !_chaveRemapeada) { ChaveNaSala = ChaveNaSala == sala ? p : (ChaveNaSala >= p && ChaveNaSala < sala ? ChaveNaSala + 1 : ChaveNaSala); _chaveRemapeada = true; }
                for (int k = 0; k < PlanoDasOndas[w].Count; k++) PlanoDasOndas[w][k].Sala = s1;
            }
            for (int q = 0; q < Objetos.Count; q++) { int so = Objetos[q].Sala; Objetos[q].Sala = so == sala ? p : (so >= p && so < sala ? so + 1 : so); }
            for (int a = Onda; a < PlanoDasOndas.Count; a++)
                for (int b = a + 1; b < PlanoDasOndas.Count; b++)
                    if (_salaDaOnda[b] < _salaDaOnda[a])
                    {
                        int ts = _salaDaOnda[a]; _salaDaOnda[a] = _salaDaOnda[b]; _salaDaOnda[b] = ts;
                        var tl = PlanoDasOndas[a]; PlanoDasOndas[a] = PlanoDasOndas[b]; PlanoDasOndas[b] = tl;
                    }
            Resultado.InteracoesPorToque++; Resultado.RotasEscolhidas++;
            return true;
        }
        private readonly List<int> _salaDaOnda = new List<int>(4);
        private bool _chaveRemapeada;

        /// <summary>Quantas passagens o grupo atravessa para ir de uma sala a outra no mapa (caminho pela árvore).</summary>
        public int DistanciaNoMapa(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Mapa.Count || b >= Mapa.Count || a == b) return 1;
            // salas vizinhas na grade são sempre ligadas por um corredor: busca em largura pelo caminho mais curto
            int n = Mapa.Count; var dist = new int[n]; for (int k = 0; k < n; k++) dist[k] = -1;
            var fila = new Queue<int>(); fila.Enqueue(a); dist[a] = 0;
            while (fila.Count > 0)
            {
                int u = fila.Dequeue();
                if (u == b) return Math.Max(1, dist[u]);
                for (int v = 0; v < n; v++)
                    if (dist[v] < 0 && Math.Abs(Mapa[u].X - Mapa[v].X) + Math.Abs(Mapa[u].Y - Mapa[v].Y) == 1) { dist[v] = dist[u] + 1; fila.Enqueue(v); }
            }
            return 1;
        }

        /// <summary>Tempo de caminhada entre duas salas: o grupo anda pelo mapa, não se teletransporta.</summary>
        private float TempoDeViagem(int de, int para)
        {
            return Math.Min(Cfg.ViagemMaxima, Cfg.ViagemBase + Cfg.ViagemPorPassagem * DistanciaNoMapa(de, para));
        }

        /// <summary>Entra na próxima sala do plano: combate volta ao fluxo das ondas; as outras abrem a exploração.</summary>
        private void EntrarNaProximaSala(float pausa)
        {
            SalaAtual++;
            var t = TipoDaSalaAtual;
            _viagem = SalaAtual < Salas.Count ? TempoDeViagem(SalaAtual - 1, SalaAtual) : pausa;
            // corredores com vida: a porta para a próxima sala pode estar emperrada. O grupo a arromba em alguns segundos; o toque do jogador abre na hora
            PortaFechada = false; _portaExtra = 0f;
            if (Cfg.PortasFechadas && SalaAtual < Salas.Count && Andar >= Cfg.PortasAPartirDoAndar && _rngMasmorra.Chance(Cfg.ChanceDePortaFechada))
            {
                PortaFechada = true; _portaExtra = Cfg.PortaFechadaTempo; _viagem += _portaExtra; Resultado.PortasArrombadas++;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 25, Valor = _portaExtra, Alvo = SalaAtual, Origem = -1, Pos = Necromante.Pos });
            }
            if (t == TipoSala.Combate || t == TipoSala.Elite || t == TipoSala.Chefe || SalaAtual >= Salas.Count) { Fase = FaseExpedicao.EntreOndas; _temporizadorDeFase = _viagem; }
            else IniciarSalaEspecial(t);
        }

        /// <summary>Chamado quando a sala de combate fica vazia. Devolve true se assumiu o fluxo (o chamador não faz mais nada).</summary>
        private bool AoLimparSala()
        {
            if (_patrulhaEmCampo && !_emEmboscada && !_emSecreto)
            {
                // a patrulha carregava o que saqueou pelo caminho
                _patrulhaEmCampo = false;
                SoltarItemEm(Necromante.Pos, RaridadeItem.Magico);
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 15, Alvo = -1, Origem = -1, Pos = Necromante.Pos });
            }
            if (!_emEmboscada && !_emSecreto && SalaAtual == ChaveNaSala && !TemChave)
            {
                TemChave = true;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 13, Alvo = -1, Origem = -1, Pos = Necromante.Pos });
            }
            if (_emEmboscada)
            {
                _emEmboscada = false;
                long ossos = (long)(Cfg.BauOssos * (_mimico ? 2f : _ninhoChocou ? Cfg.NinhoPremioDaLuta : 1f) * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                Resultado.Ossos += ossos;
                if (_mimico || _fosso) SoltarItemEm(PosDoObjeto, RaridadeItem.Magico);
                if (_lutaDoRei)
                {
                    _lutaDoRei = false; _reiEsqueleto = 3; Resultado.ReiEsqueletoMorto = true;
                    var coroa = Unicos.Gerar(_rngItens, Cfg, Math.Max(Andar, Cfg.ReiNivelDaCoroa), Unicos.Obter(2));
                    Resultado.Itens.Add(coroa); Resultado.Ouro += Cfg.ReiOuro;
                    Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = -1, Alvo = Resultado.Itens.Count - 1, Pos = PosDoObjeto, Extra = (int)coroa.Raridade, Valor = coroa.Nivel });
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.TumbaDoRei, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_lutaDoPoco)
                {
                    _lutaDoPoco = false; _poco = 3; Resultado.PocoVencido = true; Resultado.Ouro += Cfg.PocoOuro;
                    if (Cfg.LivrosDeFeitico) { int qual = LivroDaCamara(); Resultado.Livros.Add(qual); Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 24, Valor = qual, Alvo = -1, Origem = -1, Pos = PosDoObjeto }); }
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.PocoDosLamentos, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_lutaDaFortaleza)
                {
                    _lutaDaFortaleza = false; _fortaleza = 3; Resultado.FortalezaVencida = true; Resultado.Ouro += Cfg.FortalezaOuro;
                    var estandarte = Unicos.Gerar(_rngItens, Cfg, Math.Max(Andar, Cfg.FortalezaAndar), Unicos.Obter(12));
                    Resultado.Itens.Add(estandarte);
                    Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = -1, Alvo = Resultado.Itens.Count - 1, Pos = PosDoObjeto, Extra = (int)estandarte.Raridade, Valor = estandarte.Nivel });
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.FortalezaRubra, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_lutaDosCegos)
                {
                    _lutaDosCegos = false; _cegos = 3; Resultado.CegosVencidos = true; Resultado.Ouro += Cfg.CegosOuro;
                    var olho = Unicos.Gerar(_rngItens, Cfg, Math.Max(Andar, Cfg.CegosAndar), Unicos.Obter(11));
                    Resultado.Itens.Add(olho);
                    Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = -1, Alvo = Resultado.Itens.Count - 1, Pos = PosDoObjeto, Extra = (int)olho.Raridade, Valor = olho.Nivel });
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.SaloesDosCegos, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_lutaDaCamara)
                {
                    _lutaDaCamara = false; _camara = 3; Resultado.CamaraVencida = true; Resultado.Ouro += Cfg.CamaraOuro;
                    if (Cfg.LivrosDeFeitico) { int qual = LivroDaCamara(); Resultado.Livros.Add(qual); Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 24, Valor = qual, Alvo = -1, Origem = -1, Pos = PosDoObjeto }); }
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.CamaraDeOssos, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_lutaDaFonte)
                {
                    _lutaDaFonte = false; _fonte = 3; Resultado.FonteLimpa = true; Resultado.Ouro += Cfg.FonteOuro;
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.FonteEnvenenada, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_lutaDoAcougueiro)
                {
                    _lutaDoAcougueiro = false; _acougueiro = 3; Resultado.AcougueiroMorto = true;
                    var cutelo = Unicos.Gerar(_rngItens, Cfg, Math.Max(Andar, Cfg.AcougueiroNivelDoCutelo), Unicos.Obter(1));
                    Resultado.Itens.Add(cutelo); Resultado.Ouro += Cfg.AcougueiroOuro;
                    Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = -1, Alvo = Resultado.Itens.Count - 1, Pos = PosDoObjeto, Extra = (int)cutelo.Raridade, Valor = cutelo.Nivel });
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.Acougue, Valor = ossos, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                    EntrarNaProximaSala(Cfg.PausaEntreOndas);
                    return true;
                }
                if (_fosso) { ossos *= 2; Resultado.Ossos += ossos / 2; }
                Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)(_fosso ? TipoSala.FossoDeMonstros : TipoSala.Emboscada), Valor = ossos, Critico = _mimico, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                _mimico = false; _ninhoChocou = false; _fosso = false;
                EntrarNaProximaSala(Cfg.PausaEntreOndas);
                return true;
            }
            if (_emSecreto)
            {
                _emSecreto = false;
                PortaisAtivos = _portaisAntesDoSecreto;
                long ossos = (long)(Cfg.BauOssos * 6f * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                Resultado.Ossos += ossos; Resultado.Sangue += Cfg.SecretoSangue;
                var centro = new Vec2(Cfg.LarguraArena * 0.6f, Cfg.AlturaArena * 0.5f);
                SoltarItemEm(centro, RaridadeItem.Raro);
                SoltarItemEm(centro, RaridadeItem.Magico);
                if (SecretoAtual == NivelSecreto.CamaraDoTesouro)
                {
                    // o tesouro: mais um item, com chance de Lendário
                    SoltarItemEm(centro, _rngItens.Chance(Cfg.TesouroChanceDeLendario) ? RaridadeItem.Lendario : RaridadeItem.Raro);
                    long extra = ossos; Resultado.Ossos += extra; ossos += extra;
                }
                Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.Secreta, Valor = ossos, Critico = true, Pos = centro, Alvo = -1, Origem = (int)SecretoAtual });
                SecretoAtual = NivelSecreto.Nenhum;
                return false;   // segue para a conclusão normal do andar
            }
            if (Onda < TotalDeOndas)
            {
                EntrarNaProximaSala(Cfg.PausaEntreOndas);
                return true;
            }
            if (!_secretoTestado)
            {
                _secretoTestado = true;
                bool abre = ForcarNivelSecreto || (Andar >= Cfg.SecretoAPartirDoAndar && _rngMasmorra.Chance(Cfg.ChanceDeNivelSecreto));
                if (abre && !Formulas.AndarDeChefe(Andar, Cfg)) { ForcarNivelSecreto = false; IniciarNivelSecreto(); return true; }
            }
            return false;
        }

        private void IniciarSalaEspecial(TipoSala t)
        {
            _toqueNaSala = false;
            var n = Necromante.Pos;
            PosDoObjeto = Limitar(new Vec2(Math.Min(n.X + 4.5f, Cfg.LarguraArena * 0.7f), Cfg.AlturaArena * 0.5f));
            if (t != TipoSala.Vazia) Resultado.SalasEspeciais++;
            Emitir(new EventoSim { Tipo = TipoEvento.SalaIniciada, Extra = (int)t, Valor = SalaAtual, Alvo = Salas.Count, Pos = PosDoObjeto, Origem = -1 });
            if (t == TipoSala.Emboscada) { IniciarEmboscada(false); return; }
            if (t == TipoSala.FossoDeMonstros) { IniciarFosso(); return; }
            if (t == TipoSala.FonteEnvenenada) { IniciarFonteEnvenenada(); return; }
            if (t == TipoSala.SaloesDosCegos) { IniciarSaloesDosCegos(); return; }
            if (t == TipoSala.FortalezaRubra) { IniciarFortalezaRubra(); return; }
            if (t == TipoSala.PocoDosLamentos) { IniciarPocoDosLamentos(); return; }
            Fase = FaseExpedicao.Explorando;
            _temporizadorDeFase = _viagem + (t == TipoSala.Vazia ? Cfg.TempoDeSalaVazia : Cfg.TempoDeSalaEspecial);
        }

        /// <summary>Fosso de monstros (Diablo 1): uma sala entupida de uma única espécie, guardando um item no meio.</summary>
        private void IniciarFosso()
        {
            _emEmboscada = true; _mimico = false; _fosso = true;
            int noAto = Formulas.AtoDoAndar(Andar, Cfg) > 1 ? Cfg.AndaresPorAto : Formulas.AndarNoAto(Andar, Cfg);
            EspecieId esp = SortearEspecie(noAto, 2.01f);
            if (esp == EspecieId.Necromante) esp = EspecieId.Esqueleto;
            float orcamento = Math.Min(Cfg.OrcamentoMaximo, Cfg.OrcamentoBaseOnda + Cfg.OrcamentoPorAndar * (Andar - 1)) * Cfg.Horda * Cfg.FossoMult;
            int n = Math.Max(6, Math.Min(Cfg.FossoMaximo, (int)(orcamento / Math.Max(0.5f, TabelaEspecies.Obter(esp).CustoOnda))));
            for (int k = 0; k < n; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = esp, Fraco = true, Portal = k % 3, TickDeSpawn = TickAtual + 1 + k * 2 });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>A Fonte Envenenada (Diablo 1, "Poisoned Water Supply"): a nascente da vila, tomada por mortos que apodrecem a água. Limpa a sala, a fonte volta a correr.</summary>
        private void IniciarFonteEnvenenada()
        {
            _emEmboscada = true; _mimico = false; _lutaDaFonte = true;
            for (int k = 0; k < Cfg.FonteInimigos; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = k % 3 == 2 ? EspecieId.Carnical : EspecieId.Zumbi, Elite = k == 0, Mods = k == 0 ? SortearModificadores() : ModificadorElite.Nenhum, Portal = k % 3, TickDeSpawn = TickAtual + 1 + k * 3 });
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 44, Alvo = -1, Origem = -1, Valor = Cfg.FonteInimigos, Pos = Necromante.Pos });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>Os Salões dos Cegos (Diablo 1, "Halls of the Blind"): sala às escuras onde os Ocultos só se mostram quando já estão em cima do grupo.</summary>
        private void IniciarSaloesDosCegos()
        {
            _emEmboscada = true; _mimico = false; _lutaDosCegos = true;
            for (int k = 0; k < Cfg.CegosInimigos; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Aparicao, Oculto = true, Portal = k % 3, TickDeSpawn = TickAtual + 1 + k * 4 });
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 47, Alvo = -1, Origem = -1, Valor = Cfg.CegosInimigos, Pos = Necromante.Pos });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>O Poço dos Lamentos (missão original): as almas presas no poço sobem em três levas de Aparições; quem aguenta leva um livro de feitiço.</summary>
        private void IniciarPocoDosLamentos()
        {
            _emEmboscada = true; _mimico = false; _lutaDoPoco = true;
            for (int leva = 0; leva < Cfg.PocoLevas; leva++)
                for (int k = 0; k < Cfg.PocoPorLeva; k++)
                    _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Aparicao, Elite = leva == Cfg.PocoLevas - 1 && k == 0, Portal = k % 3, TickDeSpawn = TickAtual + 1 + leva * Cfg.PocoIntervaloTicks + k * 3 });
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 54, Alvo = -1, Origem = -1, Valor = Cfg.PocoLevas, Pos = Necromante.Pos });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>A Fortaleza Rubra (missão original, no molde das do Diablo 1): um senhor da guerra com estandarte de sangue e o séquito dele, entrincheirados numa sala de armas.</summary>
        private void IniciarFortalezaRubra()
        {
            _emEmboscada = true; _mimico = false; _lutaDaFortaleza = true;
            _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.CavaleiroNegro, Elite = true, Senhor = true, Portal = 0, TickDeSpawn = TickAtual + 1 });
            for (int k = 0; k < Cfg.FortalezaSequito; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = k % 2 == 0 ? EspecieId.DiaboDeGuerra : EspecieId.CavaleiroNegro, Elite = k < 2, Mods = k < 2 ? SortearModificadores() : ModificadorElite.Nenhum, Portal = (k + 1) % 3, TickDeSpawn = TickAtual + 4 + k * 3 });
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 51, Alvo = -1, Origem = -1, Valor = Cfg.FortalezaSequito, Pos = Necromante.Pos });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>A Câmara de Ossos (Diablo 1, "Chamber of Bone"): sala selada cheia de esqueletos que guarda um livro de feitiço.</summary>
        private void IniciarCamaraDeOssos()
        {
            _emEmboscada = true; _mimico = false; _lutaDaCamara = true;
            for (int k = 0; k < Cfg.CamaraInimigos; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = k % 3 == 1 ? EspecieId.ArqueiroOssudo : EspecieId.Esqueleto, Elite = k < 2, Mods = k < 2 ? SortearModificadores() : ModificadorElite.Nenhum, Portal = k % 3, TickDeSpawn = TickAtual + 1 + k * 3 });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>O livro que a Câmara guarda: o primeiro dos feitiços só-por-livro que o jogador ainda não tem; se já tem todos, um qualquer.</summary>
        private int LivroDaCamara()
        {
            for (int f = 0; f < Grimorio.Quantos; f++) if (Grimorio.SoPorLivro((FeiticoId)f) && Grimorio.Nivel(_progressoInicial, (FeiticoId)f) == 0 && !Resultado.Livros.Contains(f)) return f;
            return _rngItens.Intervalo(0, Grimorio.Quantos);
        }

        /// <summary>O Açougueiro (Diablo 1): um Único de nome fixo, forte e rápido, sozinho numa sala de corpos. Vencido, larga o Cutelo.</summary>
        private void IniciarAcougueiro()
        {
            _emEmboscada = true; _mimico = false; _lutaDoAcougueiro = true;
            _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Carnical, Elite = true, Acougueiro = true, Portal = 0, TickDeSpawn = TickAtual + 1 });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>O Rei Esqueleto (Diablo 1): sai do sarcófago e, enquanto vive, ergue esqueletos para lutar por ele.</summary>
        private void IniciarReiEsqueleto()
        {
            _emEmboscada = true; _mimico = false; _lutaDoRei = true; _tempoDoRei = Cfg.ReiErgueACada * 0.5f;
            _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Esqueleto, Elite = true, Rei = true, Portal = 0, TickDeSpawn = TickAtual + 1 });
            Fase = FaseExpedicao.EmOnda;
        }

        /// <summary>Enquanto o Rei vive, de tempos em tempos mais esqueletos se erguem (até um teto de inimigos na sala).</summary>
        private void AtualizarReiEsqueleto()
        {
            bool reiVivo = false; int vivos = 0;
            for (int i = 0; i < Entidades.Count; i++) { var e = Entidades[i]; if (!e.Viva || e.Faccao != Faccao.Inimigo) continue; vivos++; if (e.NomeProprio == "Rei Esqueleto") reiVivo = true; }
            if (!reiVivo) return;
            _tempoDoRei -= DT;
            if (_tempoDoRei > 0f) return;
            _tempoDoRei = Cfg.ReiErgueACada;
            for (int k = 0; k < Cfg.ReiErguePorVez && vivos + k < Cfg.ReiMaximoDeErguidos + 1; k++)
                _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Esqueleto, Fraco = true, Portal = (k + TickAtual) % 3, TickDeSpawn = TickAtual + 1 + k });
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 22, Alvo = -1, Origem = -1, Pos = PosDoObjeto });
        }

        private void IniciarEmboscada(bool mimico)
        {
            _emEmboscada = true; _mimico = mimico;
            int noAto = Formulas.AtoDoAndar(Andar, Cfg) > 1 ? Cfg.AndaresPorAto : Formulas.AndarNoAto(Andar, Cfg);
            int k = 0;
            int intervalo = Math.Max(1, (int)(Cfg.IntervaloDeSpawn * Cfg.TicksPorSegundo + 0.5f));
            if (mimico)
            {
                _filaDeSpawn.Add(new SpawnPendente { Especie = SortearEspecie(noAto, 2.01f), Elite = true, Mods = SortearModificadores(), Portal = 0, TickDeSpawn = TickAtual + 1 });
            }
            else
            {
                float orcamento = (Cfg.OrcamentoBaseOnda + Cfg.OrcamentoPorAndar * (Andar - 1)) * Cfg.EmboscadaOrcamento;
                if (orcamento > Cfg.OrcamentoMaximo * Cfg.EmboscadaOrcamento) orcamento = Cfg.OrcamentoMaximo * Cfg.EmboscadaOrcamento;
                while (orcamento > 0.01f)
                {
                    EspecieId esp = SortearEspecie(noAto, orcamento);
                    if (esp == EspecieId.Necromante) break;
                    orcamento -= TabelaEspecies.Obter(esp).CustoOnda;
                    // a emboscada vem de todos os lados
                    _filaDeSpawn.Add(new SpawnPendente { Especie = esp, Portal = k % 3, TickDeSpawn = TickAtual + 1 + k * intervalo / 3 });
                    k++;
                }
            }
            Fase = FaseExpedicao.EmOnda;
        }

        private int _portaisAntesDoSecreto;
        private void IniciarNivelSecreto()
        {
            _emSecreto = true;
            Resultado.NiveisSecretos++;
            Salas.Add(TipoSala.Secreta); SalaAtual = Salas.Count - 1; ColocarNoMapa(SalaAtual);
            _portaisAntesDoSecreto = PortaisAtivos; PortaisAtivos = 3;
            float qual = _rngMasmorra.Proximo01();
            SecretoAtual = Cfg.ForcarSecreto != 0 ? (NivelSecreto)Cfg.ForcarSecreto : qual < 0.4f ? NivelSecreto.PastoProfano : qual < 0.75f ? NivelSecreto.CamaraDoTesouro : NivelSecreto.CriptaDosEspelhos;
            if (SecretoAtual == NivelSecreto.CriptaDosEspelhos)
            {
                // Cripta dos Espelhos: um reflexo sombrio de cada minion do exército (mesma espécie), e o maior deles vem como elite
                int n = 0;
                for (int i = 0; i < _minions.Count && n < Cfg.EspelhosMaximo; i++)
                {
                    if (!_minions[i].Viva || _minions[i].Temporario) continue;
                    _filaDeSpawn.Add(new SpawnPendente { Especie = _minions[i].Especie, Elite = n == 0, Mods = n == 0 ? SortearModificadores() : ModificadorElite.Nenhum, Portal = n % 3, TickDeSpawn = TickAtual + 10 + n * 6 });
                    n++;
                }
                if (n == 0) _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Aparicao, Elite = true, Mods = SortearModificadores(), Portal = 0, TickDeSpawn = TickAtual + 10 });
            }
            else
            if (SecretoAtual == NivelSecreto.PastoProfano)
            {
                int n = Math.Min(Cfg.SecretoHordaMaxima, Cfg.SecretoHordaBase + Andar / 2);
                int intervalo = Math.Max(1, (int)(Cfg.IntervaloDeSpawn * Cfg.TicksPorSegundo * 0.5f + 0.5f));
                for (int k = 0; k < n; k++)
                    _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.ResProfana, Portal = k % 3, TickDeSpawn = TickAtual + 10 + k * intervalo });
                // o Rei do Pasto chega por último
                _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.ResProfana, Elite = true, Mods = ModificadorElite.EscudoDeOssos, Portal = 0, TickDeSpawn = TickAtual + 10 + n * intervalo + 10 });
            }
            else
            {
                // Câmara do Tesouro: três guardiões de elite, um por porta
                int noAto = Formulas.AtoDoAndar(Andar, Cfg) > 1 ? Cfg.AndaresPorAto : Formulas.AndarNoAto(Andar, Cfg);
                for (int k = 0; k < 3; k++)
                    _filaDeSpawn.Add(new SpawnPendente { Especie = SortearEspecie(noAto, 2.01f), Elite = true, Mods = SortearModificadores(), Portal = k, TickDeSpawn = TickAtual + 10 + k * 15 });
            }
            Fase = FaseExpedicao.EmOnda;
            Emitir(new EventoSim { Tipo = TipoEvento.SalaIniciada, Extra = (int)TipoSala.Secreta, Valor = SalaAtual, Alvo = Salas.Count, Pos = new Vec2(Cfg.LarguraArena * 0.6f, Cfg.AlturaArena * 0.5f), Origem = (int)SecretoAtual });
        }

        private void AtualizarExploracao()
        {
            _temporizadorDeFase -= DT;
            _ticksDeExploracao++;      // explorar não conta para a Fúria
            if (_temporizadorDeFase > 0f) return;
            var t = TipoDaSalaAtual;
            if (t == TipoSala.Santuario)
            {
                if (BencaosOferecidas == null)
                {
                    // um santuário de nome sorteado: [tipo, 1 se o efeito já é conhecido, 0]. O jogador pode usar (0) ou recusar (1); sem resposta, o necromante usa.
                    int tipoS = Cfg.ForcarSantuario > 0 ? Cfg.ForcarSantuario : 1 + _rngMasmorra.Intervalo(0, 10);
                    BencaosOferecidas = new[] { tipoS, (_santuariosVistos & (1 << tipoS)) != 0 ? 1 : 0, 0 }; TempoDaBencao = Cfg.TempoDeEscolhaDaBencao; _escolhaDeBencao = -1;
                    return;
                }
                TempoDaBencao -= DT;
                if (_escolhaDeBencao < 0 && TempoDaBencao > 0f) return;
            }
            if (t == TipoSala.CamaraDeOssos)
            {
                // a Câmara de Ossos: [13, 0, 0]. Romper o selo (0) ou deixar (1). Sem resposta, só rompe com recorde suficiente.
                if (BencaosOferecidas == null) { BencaosOferecidas = new[] { 13, 0, 0 }; TempoDaBencao = Cfg.TempoDeEscolhaDaBencao; _escolhaDeBencao = -1; return; }
                TempoDaBencao -= DT;
                if (_escolhaDeBencao < 0 && TempoDaBencao > 0f) return;
                bool abreC = _escolhaDeBencao == 0 || (_escolhaDeBencao < 0 && _recordeInicial >= Cfg.CamaraAbreSozinhoNoRecorde);
                BencaosOferecidas = null; _escolhaDeBencao = -1;
                if (abreC) { IniciarCamaraDeOssos(); return; }
                Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = -1f, Origem = 0, Pos = PosDoObjeto, Alvo = -1 });
                EntrarNaProximaSala(Cfg.PausaAposSalaEspecial);
                return;
            }
            if (t == TipoSala.TumbaDoRei)
            {
                // a tumba do Rei Esqueleto: [12, 0, 0]. Abrir (0) ou seguir (1). Sem resposta, só abre com recorde suficiente.
                if (BencaosOferecidas == null) { BencaosOferecidas = new[] { 12, 0, 0 }; TempoDaBencao = Cfg.TempoDeEscolhaDaBencao; _escolhaDeBencao = -1; return; }
                TempoDaBencao -= DT;
                if (_escolhaDeBencao < 0 && TempoDaBencao > 0f) return;
                bool abreT = _escolhaDeBencao == 0 || (_escolhaDeBencao < 0 && _recordeInicial >= Cfg.ReiAbreSozinhoNoRecorde);
                BencaosOferecidas = null; _escolhaDeBencao = -1;
                if (abreT) { IniciarReiEsqueleto(); return; }
                Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = -1f, Origem = 0, Pos = PosDoObjeto, Alvo = -1 });
                EntrarNaProximaSala(Cfg.PausaAposSalaEspecial);
                return;
            }
            if (t == TipoSala.Acougue)
            {
                // a porta do Açougueiro: [11, 0, 0]. Abrir (0) ou seguir (1). Sem resposta, o necromante só abre se já foi fundo o bastante para aguentar.
                if (BencaosOferecidas == null) { BencaosOferecidas = new[] { 11, 0, 0 }; TempoDaBencao = Cfg.TempoDeEscolhaDaBencao; _escolhaDeBencao = -1; return; }
                TempoDaBencao -= DT;
                if (_escolhaDeBencao < 0 && TempoDaBencao > 0f) return;
                bool abre = _escolhaDeBencao == 0 || (_escolhaDeBencao < 0 && _recordeInicial >= Cfg.AcougueiroAbreSozinhoNoRecorde);
                BencaosOferecidas = null; _escolhaDeBencao = -1;
                if (abre) { IniciarAcougueiro(); return; }
                Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = -1f, Origem = 0, Pos = PosDoObjeto, Alvo = -1 });
                EntrarNaProximaSala(Cfg.PausaAposSalaEspecial);
                return;
            }
            bool toque = _toqueNaSala || _escolhaDeBencao >= 0;
            float valor = 0f; int sub = 0; bool especial = toque;
            switch (t)
            {
                case TipoSala.Bau:
                {
                    Resultado.BausAbertos++;
                    if (!toque && Rng.Chance(Cfg.BauChanceDeMimico))
                    {
                        // era um mímico: o baú vira uma luta, e paga em dobro se vencida
                        Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = 0f, Origem = 1, Pos = PosDoObjeto, Alvo = -1 });
                        IniciarEmboscada(true);
                        return;
                    }
                    bool dourado = Rng.Chance(Cfg.BauChanceDourado * (1f + _sorte * Cfg.SorteChanceDeMelhorPorPonto));
                    long ossos = (long)(Cfg.BauOssos * (dourado ? 3f : 1f) * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f) * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                    Resultado.Ouro += (long)(Cfg.OuroBau * (1f + 0.1f * (AndarEfetivo - 1)) * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f) + 0.5f); Resultado.Ossos += ossos; valor = ossos;
                    if (dourado) { SoltarItemEm(PosDoObjeto, RaridadeItem.Raro); Resultado.Sangue += Cfg.BauDouradoSangue; sub = 2; especial = true; }
                    else if (_rngItens.Chance(toque ? Cfg.BauChanceDeItemComToque : Cfg.BauChanceDeItem)) SoltarItemEm(PosDoObjeto, RaridadeItem.Comum);
                    break;
                }
                case TipoSala.Santuario:
                {
                    float f = Cfg.BencaoForca * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f);
                    var qual = (Bencao)BencaosOferecidas[0]; bool recusou = _escolhaDeBencao == 1;
                    BencaosOferecidas = null; _escolhaDeBencao = -1;
                    if (recusou) { sub = (int)qual; valor = -1f; especial = true; break; }   // o jogador mandou seguir adiante
                    BencaoAtual = qual; _santuariosVistos |= 1 << (int)qual; Resultado.SantuariosVistos |= 1 << (int)qual; Resultado.SantuariosUsados++;
                    switch (qual)
                    {
                        case Bencao.Misterioso: _bDanoAliado *= 1f + 2f * f; _bProtecao *= 1f + 0.6f * f; break;                 // bate muito mais, apanha um pouco mais
                        case Bencao.Sombrio: _bProtecao *= 1f - 1.5f * f; _bDanoAliado *= 1f - 0.5f * f; break;                   // apanha bem menos, bate um pouco menos
                        case Bencao.Estranho: _bDanoAliado *= 1f + f; break;
                        case Bencao.Reluzente:
                            for (int i = 0; i < Resultado.Itens.Count; i++) Resultado.Itens[i].NaoIdentificado = false;         // revela o que já caiu
                            _bItem += f * 4f; break;
                        case Bencao.Oculto: Resultado.SemDesgaste = true; break;                                                  // o equipamento não gasta nesta descida
                        case Bencao.Religioso:
                            for (int i = 0; i < Entidades.Count; i++) { var e = Entidades[i]; if (e.Viva && e.Faccao != Faccao.Inimigo) e.Vida = e.VidaMax; }
                            break;
                        case Bencao.Magico:
                            for (int i = 0; i < Entidades.Count; i++) { var e = Entidades[i]; if (e.Viva && e.Faccao != Faccao.Inimigo && e.Escudo < e.VidaMax * 3f * f) e.Escudo = e.VidaMax * 3f * f; }
                            break;
                        case Bencao.Ornado: Resultado.Xp += Cfg.BibliotecaXp * 2f * Formulas.EscalaInimigo(AndarEfetivo, Cfg); _bOssos *= 1f - 2f * f; break;   // saber em troca de Ossos
                        case Bencao.Tetrico:
                            for (int i = 0; i < Entidades.Count; i++) { var e = Entidades[i]; if (e.Viva && e.Faccao != Faccao.Inimigo) e.Vida = Math.Max(1f, e.Vida * (1f - 2f * f)); }
                            Resultado.Ouro += (long)(Cfg.OuroBau * 3f * (1f + 0.1f * (AndarEfetivo - 1)) + 0.5f); break;                 // sangue em troca de ouro
                        case Bencao.Profano: _bProtecao *= 1f + 1.5f * f; _bAlma += f * 4f; break;                                // os inimigos ferem mais, as almas caem mais
                    }
                    sub = (int)qual; valor = f;
                    break;
                }
                case TipoSala.AlmasPresas:
                {
                    int noAto = Formulas.AtoDoAndar(Andar, Cfg) > 1 ? Cfg.AndaresPorAto : Formulas.AndarNoAto(Andar, Cfg);
                    var esp = SortearEspecie(noAto, 99f);
                    var q = GeradorDeAlmas.SortearQualidade(Rng, Cfg);
                    if (toque && q < Qualidade.Profana && Rng.Chance(Cfg.AlmasPresasChanceDeMelhorar)) q = (Qualidade)((int)q + 1);
                    CriarOrbe(PosDoObjeto, GeradorDeAlmas.Gerar(Rng, Cfg, esp, q));
                    sub = (int)q; valor = 1f;
                    break;
                }
                case TipoSala.Vazia:
                {
                    // câmara abandonada: às vezes há ossos esquecidos num canto
                    if (Rng.Chance(Cfg.SalaVaziaChanceDeOssos))
                    {
                        long achado = (long)(Cfg.BauOssos * 0.3f * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                        Resultado.Ossos += achado; valor = achado;
                    }
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = valor, Origem = 0, Pos = PosDoObjeto, Alvo = -1 });
                    EntrarNaProximaSala(Cfg.PausaAposSalaEspecial);
                    return;
                }
                case TipoSala.Cofre:
                {
                    if (TemChave)
                    {
                        long ossos = (long)(Cfg.BauOssos * Cfg.CofreOssos * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                        Resultado.Ossos += ossos; valor = ossos; sub = 1; especial = true;
                        SoltarItemEm(PosDoObjeto, _rngItens.Chance(Cfg.CofreChanceDeRaro) ? RaridadeItem.Raro : RaridadeItem.Magico);
                        Resultado.BausAbertos++; Resultado.CofresAbertos++;
                    }
                    break;      // sem a chave, fica trancado: o grupo segue adiante
                }
                case TipoSala.Armadilha:
                {
                    // placas de espinhos: sozinho, o grupo pisa e sangra; o toque do jogador desarma e ainda rende os ossos de quem caiu ali antes
                    if (toque)
                    {
                        long achado = (long)(Cfg.BauOssos * 0.6f * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                        Resultado.Ossos += achado; valor = achado; sub = 1;
                    }
                    else
                    {
                        for (int i = 0; i < Entidades.Count; i++)
                        {
                            var e = Entidades[i];
                            if (!e.Viva || e.Faccao == Faccao.Inimigo) continue;
                            float d = Math.Min(e.Vida - 1f, e.VidaMax * (e == Necromante ? Cfg.ArmadilhaDanoNoNecromante : Cfg.ArmadilhaDano));
                            if (d <= 0f) continue;
                            e.Vida -= d;
                            Emitir(new EventoSim { Tipo = TipoEvento.Dano, Origem = -1, Alvo = e.Id, Valor = d, TipoDano = TipoDano.Fisico, Pos = e.Pos });
                        }
                        valor = Cfg.ArmadilhaDano;
                    }
                    break;
                }
                case TipoSala.Mercador:
                {
                    // troca os Ossos já colhidos nesta expedição por um item; sem Ossos, o mercador só acena
                    long preco = (long)(Cfg.MercadorPreco * (1f + 0.1f * (AndarEfetivo - 1)) + 0.5f);
                    if (toque) preco = (long)(preco * (1f - Cfg.MercadorDescontoComToque));
                    if (Resultado.Ossos >= preco)
                    {
                        Resultado.Ossos -= preco;
                        SoltarItemEm(PosDoObjeto, _rngItens.Chance(Cfg.MercadorChanceDeRaro) ? RaridadeItem.Raro : RaridadeItem.Magico);
                        valor = preco; sub = 1;
                    }
                    break;
                }
                case TipoSala.AltarDeSangue:
                {
                    // cobra vida de todos e, em troca, as almas deste andar caem com muito mais frequência
                    if (!toque)
                        for (int i = 0; i < Entidades.Count; i++)
                        {
                            var e = Entidades[i];
                            if (e.Viva && e.Faccao != Faccao.Inimigo) e.Vida = Math.Max(1f, e.Vida - e.VidaMax * Cfg.AltarCustoDeVida);
                        }
                    _bAlma = Cfg.AltarBonusDeAlma; valor = Cfg.AltarBonusDeAlma;
                    break;
                }
                case TipoSala.Ninho:
                {
                    // Ninho: quem toca esmaga os ovos e leva os restos; sem toque, as crias chocam e atacam
                    if (!toque)
                    {
                        Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = 0f, Origem = 2, Pos = PosDoObjeto, Alvo = -1 });
                        _emEmboscada = true; _mimico = false; _ninhoChocou = true;
                        int crias = Math.Min(Cfg.NinhoCriasMaximo, Cfg.NinhoCrias + Andar / 6);
                        for (int k = 0; k < crias; k++)
                            _filaDeSpawn.Add(new SpawnPendente { Especie = EspecieId.Diabrete, Portal = k % 3, TickDeSpawn = TickAtual + 1 + k * 3 });
                        Fase = FaseExpedicao.EmOnda;
                        return;
                    }
                    long restos = (long)(Cfg.BauOssos * Cfg.NinhoOssos * (1f + 0.1f * (AndarEfetivo - 1)) * _pOssos + 0.5f);
                    Resultado.Ossos += restos; valor = restos; sub = 1;
                    break;
                }
                case TipoSala.SalaDeTortura:
                {
                    // Sala de tortura: o necromante colhe o sangue dos supliciados
                    int sangue = Cfg.TorturaSangue + (toque ? Cfg.TorturaSangueDoToque : 0);
                    Resultado.Sangue += sangue; valor = sangue;
                    break;
                }
                case TipoSala.Arsenal:
                {
                    // Arsenal: nos suportes sobrou uma arma ou peça de armadura (ao menos Mágica; Rara se você mesmo escolher)
                    var tipoDoItem = (TipoItem)_rngItens.Intervalo(0, 8);   // Cajado..Botas
                    var achado = GeradorDeItens.GerarDoTipo(_rngItens, Cfg, Andar, toque ? RaridadeItem.Raro : RaridadeItem.Magico, tipoDoItem);
                    achado.NaoIdentificado = Cfg.ItensNaoIdentificados && achado.Raridade >= RaridadeItem.Raro;
                    Resultado.Itens.Add(achado);
                    Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = -1, Alvo = Resultado.Itens.Count - 1, Pos = PosDoObjeto, Extra = (int)achado.Raridade, Valor = achado.Nivel });
                    valor = (int)achado.Raridade; sub = (int)tipoDoItem;
                    break;
                }
                case TipoSala.Biblioteca:
                {
                    // Biblioteca: o necromante lê um tomo e aprende (XP); quem toca escolhe o tomo certo
                    float xp = Cfg.BibliotecaXp * Formulas.EscalaInimigo(AndarEfetivo, Cfg) * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f);
                    Resultado.Xp += xp; valor = xp;
                    SoltarLivro(PosDoObjeto);   // e leva um livro de feitiço da estante
                    break;
                }
                case TipoSala.ForjaDeOssos:
                {
                    // Forja de Ossos: cada aliado vivo sai com uma couraça de ossos que absorve dano até quebrar
                    float f = Cfg.ForjaEscudo * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f);
                    for (int i = 0; i < Entidades.Count; i++)
                    {
                        var e = Entidades[i];
                        if (e.Viva && e.Faccao != Faccao.Inimigo && e.Escudo < e.VidaMax * f) e.Escudo = e.VidaMax * f;
                    }
                    valor = f;
                    break;
                }
                case TipoSala.Fonte:
                {
                    float f = Cfg.FonteCura * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f);
                    for (int i = 0; i < Entidades.Count; i++)
                    {
                        var e = Entidades[i];
                        if (e.Viva && e.Faccao != Faccao.Inimigo) Curar(null, e, e.VidaMax * f);
                    }
                    valor = f;
                    break;
                }
            }
            Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = valor, Origem = sub, Critico = especial, Pos = PosDoObjeto, Alvo = -1 });
            EntrarNaProximaSala(Cfg.PausaAposSalaEspecial);
        }

        /// <summary>Um item Mágico ou melhor pode, raramente, cair como um Único da tabela (mais chance em Pesadelo e Inferno).</summary>
        private Item TalvezUnico(Item item)
        {
            if (item.Raridade < RaridadeItem.Magico || item.Raridade > RaridadeItem.Raro || !_rngItens.Chance(Cfg.ChanceDeItemUnico[_dif])) return item;
            var d = Unicos.Sortear(_rngItens, Andar, _dif);
            if (d == null) return item;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 28, Valor = d.Id, Alvo = -1, Origem = -1, Pos = Necromante.Pos });
            return Unicos.Gerar(_rngItens, Cfg, item.Nivel, d);
        }

        private void SoltarItemEm(Vec2 pos, RaridadeItem raridadeMinima)
        {
            if (_dif > 0 && raridadeMinima < RaridadeItem.Raro && _rngItens.Chance(Cfg.DificuldadeChanceDeSubirRaridade[_dif])) raridadeMinima++;   // Pesadelo e Inferno: saque um degrau acima
            var item = GeradorDeItens.Gerar(_rngItens, Cfg, Andar + Cfg.DificuldadeNivelDeItem[_dif], raridadeMinima);
            if (_sorte > 0f && _rngItens.Chance(_sorte * Cfg.SorteChanceDeMelhorPorPonto))
            {
                var outro = GeradorDeItens.Gerar(_rngItens, Cfg, Andar + Cfg.DificuldadeNivelDeItem[_dif], raridadeMinima);
                if (outro.Raridade > item.Raridade) item = outro;
            }
            item = TalvezUnico(item);
            item.NaoIdentificado = Cfg.ItensNaoIdentificados && item.Raridade >= RaridadeItem.Raro;
            Resultado.Itens.Add(item);
            Emitir(new EventoSim { Tipo = TipoEvento.ItemCaiu, Origem = -1, Alvo = Resultado.Itens.Count - 1, Pos = pos, Extra = (int)item.Raridade, Valor = item.Nivel });
        }

        /// <summary>Ambiente da sala do chefe. Vale para os dois lados; o chefe em si sofre menos.</summary>
        private void AtualizarAmbiente()
        {
            if (Ambiente == AmbienteDeChefe.Nenhum || Fase != FaseExpedicao.EmOnda) return;
            // erupções de lava já anunciadas
            for (int i = _perigos.Count - 1; i >= 0; i--)
            {
                if (_perigos[i].TickDoEstouro > TickAtual) continue;
                var p = _perigos[i]; _perigos.RemoveAt(i);
                float raio = p.Pedra ? Cfg.DesabamentoRaio : Cfg.LavaRaio, fator = p.Pedra ? Cfg.DesabamentoFator : 1f;
                var tipoDoPerigo = p.Pedra ? TipoDano.Fisico : TipoDano.Fogo;
                Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = -1, Pos = p.Pos, Valor = raio, TipoDano = tipoDoPerigo, Alvo = -1 });
                float r2 = raio * raio;
                for (int j = Entidades.Count - 1; j >= 0; j--)
                {
                    var e = Entidades[j];
                    if (!e.Viva || Vec2.Dist2(e.Pos, p.Pos) > r2) continue;
                    float f = e.Chefe ? Cfg.LavaDanoNoChefe : e.Faccao == Faccao.Inimigo ? Cfg.LavaDanoEmInimigos : e == Necromante ? Cfg.LavaDanoNoNecromante : Cfg.LavaDanoEmAliados;
                    CausarDano(null, e, e.VidaMax * f * fator, tipoDoPerigo, false);
                }
            }
            _tempoAmbiente += DT;
            switch (Ambiente)
            {
                case AmbienteDeChefe.Lava:
                    if (_tempoAmbiente >= Cfg.LavaIntervalo)
                    {
                        _tempoAmbiente = 0f;
                        int espera = (int)(Cfg.LavaAviso * Cfg.TicksPorSegundo);
                        for (int k = 0; k < 3; k++)
                        {
                            // uma sob um aliado, uma sob um inimigo, uma ao acaso
                            Vec2 pos = new Vec2(_rngMasmorra.Intervalo(2f, Cfg.LarguraArena - 2f), _rngMasmorra.Intervalo(1.5f, Cfg.AlturaArena - 1.5f));
                            var alvo = k == 2 ? null : SortearViva(k == 0);
                            if (alvo != null) pos = alvo.Pos;
                            _perigos.Add(new Perigo { Pos = pos, TickDoEstouro = TickAtual + espera });
                            Emitir(new EventoSim { Tipo = TipoEvento.PerigoAnunciado, Pos = pos, Valor = Cfg.LavaRaio, Extra = espera, Alvo = -1, Origem = -1 });
                        }
                    }
                    break;
                case AmbienteDeChefe.Desabamento:
                    if (_tempoAmbiente >= Cfg.DesabamentoIntervalo)
                    {
                        _tempoAmbiente = 0f;
                        int espera = (int)(Cfg.DesabamentoAviso * Cfg.TicksPorSegundo);
                        for (int k = 0; k < 5; k++)
                        {
                            // o teto cede: duas pedras sobre aliados, duas sobre inimigos, uma ao acaso
                            Vec2 pos = new Vec2(_rngMasmorra.Intervalo(2f, Cfg.LarguraArena - 2f), _rngMasmorra.Intervalo(1.5f, Cfg.AlturaArena - 1.5f));
                            var alvo = k == 4 ? null : SortearViva(k < 2);
                            if (alvo != null) pos = alvo.Pos;
                            _perigos.Add(new Perigo { Pos = pos, TickDoEstouro = TickAtual + espera, Pedra = true });
                            Emitir(new EventoSim { Tipo = TipoEvento.PerigoAnunciado, Pos = pos, Valor = Cfg.DesabamentoRaio, Extra = espera, Alvo = -1, Origem = -1, TipoDano = TipoDano.Fisico });
                        }
                    }
                    break;
                case AmbienteDeChefe.NevoaPutrida:
                    if (_tempoAmbiente >= Cfg.NevoaIntervalo)
                    {
                        _tempoAmbiente = 0f;
                        for (int j = Entidades.Count - 1; j >= 0; j--)
                        {
                            var e = Entidades[j];
                            if (!e.Viva || e.Chefe) continue;
                            float d = e.VidaMax * (e.Faccao == Faccao.Inimigo ? Cfg.NevoaDanoEmInimigos : Cfg.NevoaDanoEmAliados);
                            if (e.Vida - d < 1f) continue;      // a névoa enfraquece, não mata
                            e.Vida -= d;
                            Emitir(new EventoSim { Tipo = TipoEvento.Dano, Origem = -1, Alvo = e.Id, Valor = d, TipoDano = TipoDano.Veneno, Pos = e.Pos });
                        }
                    }
                    break;
                case AmbienteDeChefe.Geada:
                    if (_tempoAmbiente >= Cfg.GeadaIntervalo)
                    {
                        _tempoAmbiente = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            var e = SortearViva(k < 2);
                            if (e == null || e.Chefe || e == Necromante) continue;
                            e.TempoAcao = Cfg.GeadaDuracao; e.Animacao = EstadoAnimacao.Parado;
                            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 11, Alvo = e.Id, Origem = -1, Valor = Cfg.GeadaDuracao, Pos = e.Pos });
                        }
                    }
                    break;
            }
        }

        private Entidade SortearViva(bool aliada)
        {
            int n = 0;
            for (int i = 0; i < Entidades.Count; i++) { var e = Entidades[i]; if (e.Viva && (e.Faccao != Faccao.Inimigo) == aliada) n++; }
            if (n == 0) return null;
            int alvo = _rngMasmorra.Intervalo(0, n);
            for (int i = 0; i < Entidades.Count; i++) { var e = Entidades[i]; if (e.Viva && (e.Faccao != Faccao.Inimigo) == aliada && alvo-- == 0) return e; }
            return null;
        }
    }
}
