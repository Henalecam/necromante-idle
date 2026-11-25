using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>Tipo de cada sala de um andar. As de combate correspondem às ondas; as outras ficam entre elas.</summary>
    public enum TipoSala { Combate = 0, Elite = 1, Chefe = 2, Bau = 3, Santuario = 4, AlmasPresas = 5, Emboscada = 6, Fonte = 7, Secreta = 8, Mercador = 9, AltarDeSangue = 10, Vazia = 11, Armadilha = 12 }

    /// <summary>Posição de uma sala no mapa do andar (grade) e de qual sala já colocada se chega a ela.</summary>
    [Serializable]
    public struct CelulaDoMapa { public int X; public int Y; public int Pai; }

    /// <summary>Ambiente da sala do chefe: muda as regras da luta para os dois lados.</summary>
    public enum AmbienteDeChefe { Nenhum = 0, Lava = 1, NevoaPutrida = 2, Geada = 3, Desabamento = 4 }

    /// <summary>Qual nível secreto se abriu.</summary>
    public enum NivelSecreto { Nenhum = 0, PastoProfano = 1, CamaraDoTesouro = 2 }

    /// <summary>Bênção de santuário: vale até o fim do andar.</summary>
    public enum Bencao { Nenhuma = 0, Furia = 1, Protecao = 2, Colheita = 3, Fartura = 4, Devocao = 5 }

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
        private bool _toqueNaSala, _emEmboscada, _mimico, _emSecreto, _secretoTestado;
        private float _bDanoAliado = 1f, _bProtecao = 1f, _bOssos = 1f, _bAlma, _bItem;
        /// <summary>Santuário: três bênçãos à escolha (null quando não há oferta). Sem escolha no prazo, o necromante pega uma ao acaso.</summary>
        public int[] BencaosOferecidas { get; private set; }
        public float TempoDaBencao { get; private set; }
        private int _escolhaDeBencao = -1;
        /// <summary>O jogador escolhe uma das três bênçãos oferecidas (0 a 2). Escolher conta como pôr a mão: a bênção vem mais forte.</summary>
        public void ComandoEscolherBencao(int opcao) { if (BencaosOferecidas != null && opcao >= 0 && opcao < 3 && _escolhaDeBencao < 0) { _escolhaDeBencao = opcao; Resultado.InteracoesPorToque++; } }
        private float _tempoAmbiente;
        private int _ticksDeExploracao;

        public TipoSala TipoDaSalaAtual { get { return SalaAtual >= 0 && SalaAtual < Salas.Count ? Salas[SalaAtual] : TipoSala.Combate; } }

        /// <summary>Toque do jogador no objeto da sala (baú, santuário, fonte, almas): o resultado melhora.</summary>
        public void ComandoInteragir() { if (Fase == FaseExpedicao.Explorando && !_toqueNaSala) { _toqueNaSala = true; Resultado.InteracoesPorToque++; } }

        private void PlanejarSalas(bool chefe)
        {
            Salas.Clear(); SalaAtual = 0;
            BencaoAtual = Bencao.Nenhuma; _bDanoAliado = 1f; _bProtecao = 1f; _bOssos = 1f; _bAlma = 0f; _bItem = 0f; BencaosOferecidas = null; _escolhaDeBencao = -1;
            _emEmboscada = false; _mimico = false; _emSecreto = false; _secretoTestado = false; _toqueNaSala = false;
            _perigos.Clear(); _tempoAmbiente = 0f; _ticksDeExploracao = 0;
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
            for (int w = 1; w <= TotalDeOndas; w++)
            {
                Salas.Add(_ondaEliteNesteAndar && w == TotalDeOndas ? TipoSala.Elite : TipoSala.Combate);
                if (w < TotalDeOndas && Andar >= Cfg.SalasEspeciaisAPartirDoAndar && _rngMasmorra.Chance(Cfg.ChanceDeSalaEspecial))
                {
                    float r = _rngMasmorra.Proximo01() * 100f;
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSalaVazia)) Salas.Add(TipoSala.Vazia);
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSegundaSalaVazia)) Salas.Add(TipoSala.Vazia);
                    Salas.Add(r < 28f ? TipoSala.Bau : r < 42f ? TipoSala.Santuario : r < 52f ? TipoSala.AlmasPresas : r < 65f ? TipoSala.Fonte : r < 76f ? TipoSala.Emboscada : r < 84f ? TipoSala.Mercador : r < 92f ? TipoSala.AltarDeSangue : TipoSala.Armadilha);
                }
                else if (w < TotalDeOndas)
                {
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSalaVazia)) Salas.Add(TipoSala.Vazia);
                    if (_rngMasmorra.Chance(Cfg.ChanceDeSegundaSalaVazia)) Salas.Add(TipoSala.Vazia);
                }
            }
            GerarMapa();
        }

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
            int primeiro = _rngMasmorra.Chance(0.7f) ? i - 1 : _rngMasmorra.Intervalo(0, i);
            for (int t = 0; t < i; t++)
            {
                int pai = (primeiro - t + i) % i;
                int d0 = _rngMasmorra.Chance(0.45f) ? 0 : _rngMasmorra.Intervalo(0, 4);   // puxa para o leste: a masmorra avança
                for (int k = 0; k < 4; k++)
                {
                    int d = (d0 + k) % 4, x = Mapa[pai].X + DX[d], y = Mapa[pai].Y + DY[d];
                    bool livre = true;
                    for (int j = 0; j < Mapa.Count; j++) if (Mapa[j].X == x && Mapa[j].Y == y) { livre = false; break; }
                    if (livre) { Mapa.Add(new CelulaDoMapa { X = x, Y = y, Pai = pai }); return; }
                }
            }
            Mapa.Add(new CelulaDoMapa { X = Mapa[i - 1].X + 1, Y = Mapa[i - 1].Y, Pai = i - 1 });   // nunca acontece numa árvore, mas não trava
        }

        /// <summary>Se o andar começa por uma sala sem combate (antecâmara do chefe), abre a exploração já.</summary>
        private void ComecarPelaPrimeiraSala()
        {
            var t = TipoDaSalaAtual;
            if (t != TipoSala.Combate && t != TipoSala.Elite && t != TipoSala.Chefe) IniciarSalaEspecial(t);
        }

        /// <summary>Entra na próxima sala do plano: combate volta ao fluxo das ondas; as outras abrem a exploração.</summary>
        private void EntrarNaProximaSala(float pausa)
        {
            SalaAtual++;
            var t = TipoDaSalaAtual;
            if (t == TipoSala.Combate || t == TipoSala.Elite || t == TipoSala.Chefe || SalaAtual >= Salas.Count) { Fase = FaseExpedicao.EntreOndas; _temporizadorDeFase = pausa; }
            else IniciarSalaEspecial(t);
        }

        /// <summary>Chamado quando a sala de combate fica vazia. Devolve true se assumiu o fluxo (o chamador não faz mais nada).</summary>
        private bool AoLimparSala()
        {
            if (_emEmboscada)
            {
                _emEmboscada = false;
                long ossos = (long)(Cfg.BauOssos * (_mimico ? 2f : 1f) * (1f + 0.1f * (Andar - 1)) * _pOssos + 0.5f);
                Resultado.Ossos += ossos;
                if (_mimico) SoltarItemEm(PosDoObjeto, RaridadeItem.Magico);
                Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)TipoSala.Emboscada, Valor = ossos, Critico = _mimico, Pos = PosDoObjeto, Alvo = -1, Origem = -1 });
                _mimico = false;
                EntrarNaProximaSala(Cfg.PausaEntreOndas);
                return true;
            }
            if (_emSecreto)
            {
                _emSecreto = false;
                PortaisAtivos = _portaisAntesDoSecreto;
                long ossos = (long)(Cfg.BauOssos * 6f * (1f + 0.1f * (Andar - 1)) * _pOssos + 0.5f);
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
                SalaAtual++;
                var t = TipoDaSalaAtual;
                if (t == TipoSala.Combate || t == TipoSala.Elite) return false;
                IniciarSalaEspecial(t);
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
            Fase = FaseExpedicao.Explorando;
            _temporizadorDeFase = t == TipoSala.Vazia ? Cfg.TempoDeSalaVazia : Cfg.TempoDeSalaEspecial;
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
            SecretoAtual = _rngMasmorra.Chance(0.5f) ? NivelSecreto.PastoProfano : NivelSecreto.CamaraDoTesouro;
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
                    // três bênçãos diferentes, das cinco
                    var op = new int[3]; int postas = 0;
                    while (postas < 3)
                    {
                        int b = 1 + Rng.Intervalo(0, 5); bool repetida = false;
                        for (int i = 0; i < postas; i++) if (op[i] == b) repetida = true;
                        if (!repetida) op[postas++] = b;
                    }
                    BencaosOferecidas = op; TempoDaBencao = Cfg.TempoDeEscolhaDaBencao; _escolhaDeBencao = -1;
                    return;
                }
                TempoDaBencao -= DT;
                if (_escolhaDeBencao < 0 && TempoDaBencao > 0f) return;
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
                    long ossos = (long)(Cfg.BauOssos * (dourado ? 3f : 1f) * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f) * (1f + 0.1f * (Andar - 1)) * _pOssos + 0.5f);
                    Resultado.Ossos += ossos; valor = ossos;
                    if (dourado) { SoltarItemEm(PosDoObjeto, RaridadeItem.Raro); Resultado.Sangue += Cfg.BauDouradoSangue; sub = 2; especial = true; }
                    else if (_rngItens.Chance(toque ? Cfg.BauChanceDeItemComToque : Cfg.BauChanceDeItem)) SoltarItemEm(PosDoObjeto, RaridadeItem.Comum);
                    break;
                }
                case TipoSala.Santuario:
                {
                    float f = Cfg.BencaoForca * (toque ? 1f + Cfg.ToqueBonusDeSala : 1f);
                    BencaoAtual = (Bencao)BencaosOferecidas[_escolhaDeBencao >= 0 ? _escolhaDeBencao : Rng.Intervalo(0, 3)];
                    BencaosOferecidas = null; _escolhaDeBencao = -1;
                    if (BencaoAtual == Bencao.Furia) _bDanoAliado = 1f + f;
                    else if (BencaoAtual == Bencao.Protecao) _bProtecao = 1f - f;
                    else if (BencaoAtual == Bencao.Colheita) _bOssos = 1f + f * 2.5f;
                    else if (BencaoAtual == Bencao.Fartura) _bItem = f * 4f;
                    else _bAlma += f * 3f;
                    sub = (int)BencaoAtual; valor = f;
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
                        long achado = (long)(Cfg.BauOssos * 0.3f * (1f + 0.1f * (Andar - 1)) * _pOssos + 0.5f);
                        Resultado.Ossos += achado; valor = achado;
                    }
                    Emitir(new EventoSim { Tipo = TipoEvento.SalaResolvida, Extra = (int)t, Valor = valor, Origem = 0, Pos = PosDoObjeto, Alvo = -1 });
                    EntrarNaProximaSala(Cfg.PausaAposSalaEspecial);
                    return;
                }
                case TipoSala.Armadilha:
                {
                    // placas de espinhos: sozinho, o grupo pisa e sangra; o toque do jogador desarma e ainda rende os ossos de quem caiu ali antes
                    if (toque)
                    {
                        long achado = (long)(Cfg.BauOssos * 0.6f * (1f + 0.1f * (Andar - 1)) * _pOssos + 0.5f);
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
                    long preco = (long)(Cfg.MercadorPreco * (1f + 0.1f * (Andar - 1)) + 0.5f);
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

        private void SoltarItemEm(Vec2 pos, RaridadeItem raridadeMinima)
        {
            var item = GeradorDeItens.Gerar(_rngItens, Cfg, Andar, raridadeMinima);
            if (_sorte > 0f && _rngItens.Chance(_sorte * Cfg.SorteChanceDeMelhorPorPonto))
            {
                var outro = GeradorDeItens.Gerar(_rngItens, Cfg, Andar, raridadeMinima);
                if (outro.Raridade > item.Raridade) item = outro;
            }
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
