using System;
using System.Collections.Generic;
using System.Text;
using Necro.Nucleo;

namespace Necro.Testes
{
    /// <summary>
    /// Campanha completa jogada por um "jogador-robô": tudo passa de uma expedição para a outra (XP, almas,
    /// itens, ancoragem). Depois de cada expedição ele ergue as almas, monta o melhor exército que cabe no
    /// Comando, auto-equipa e evolui quem puder. Serve para medir a CURVA DE PROGRESSÃO contra as metas de
    /// design e travar o balanceamento: se um número sair da faixa, `--metas` falha.
    /// Uso: dotnet run --project Testes -- --campanha [campanhas] [horas]     (relatório)
    ///      dotnet run --project Testes -- --metas                            (passa/falha)
    /// </summary>
    public static class Campanha
    {
        public sealed class Resultado
        {
            public double[] HorasAteAndar = new double[41];     // horas de jogo até CONCLUIR o andar i (NaN = não chegou)
            public int MaiorAndar, Expedicoes, NivelNecromante, MinionsAtivos, Especies;
            public int[] Construcoes; public long OssosSobrando, EssenciaSobrando; public int Comando;
            public double HorasChefe1 = double.NaN, SegPorAndar, ExpedicaoMediaMin, ExpedicaoMaisCurtaMin = 1e9;
            public int TentativasNoChefe1, AndarDaPrimeiraExpedicao;
            public List<double> SegPorAndarPorFaixa = new List<double>();
        }

        /// <summary>Força aproximada de um minion por ponto de Comando, para o robô escolher o exército.</summary>
        static float Valor(MinionSalvo m, ConfigJogo cfg)
        {
            var d = TabelaEspecies.Obter(m.Alma.Especie);
            float f = cfg.MultQualidade[(int)m.Alma.Qualidade] * Formulas.FatorNivelMinion(m.Nivel, cfg);
            float poder = (float)Math.Sqrt(Math.Max(1f, d.Vida) * Math.Max(1f, d.Dano * d.VelAtaque)) * f;
            return poder / Math.Max(1, Progressao.CustoDeComando(m.Alma.Especie, cfg));
        }

        public static void TurnoDoJogador(EstadoProgresso p, ConfigJogo cfg)
        {
            // 1) ergue as melhores almas (mantém o plantel em até 40 para não crescer sem fim)
            // 0) com o banco já maior que o Comando, quantidade não ajuda: funde trincas em almas melhores
            if (UsarFusao && p.Minions.Count >= Progressao.ComandoMaximoEfetivo(p, cfg))
                for (int q = 0; q < (int)Qualidade.Profana; q++)
                    for (int e = 1; e < 16; e++)
                        while (Fusao.Fundir(p, (EspecieId)e, (Qualidade)q, cfg) != null) { }
            p.Filacterio.Sort((a, b) => ((int)b.Qualidade).CompareTo((int)a.Qualidade));
            p.DissolverAbaixoDe = Qualidade.Comum;      // Fracas de espécie já conhecida viram Essência
            while (p.Filacterio.Count > 0 && p.Minions.Count < 40)
                if (Progressao.ErguerMinion(p, 0, cfg) == null) break;     // faltou Essência: fica para depois
            while (Missoes.Resgatar(p, cfg)) { }          // resgata toda missão completa
            // oferendas: gasta os Restos sempre que der; rito: Banquete quando sobra Sangue
            if (UsarOferendas)
            {
                bool ofertou = true;
                while (ofertou) { ofertou = false; for (int t = 0; t < Oferendas.Tipos; t++) if (Oferendas.Ofertar(p, t, cfg)) ofertou = true; }
                if (p.RitoPreparado == Rito.Nenhum) Oferendas.PrepararRito(p, Rito.Banquete, cfg);
            }
            // talentos: política simples, um ponto por vez na ordem de preferência, respeitando os pré-requisitos
            if (UsarTalentos)
                while (Talentos.Disponiveis(p, cfg) > 0)
                {
                    int[] pref = { 1, 0, 2, 3, 4, 11, 12, 14, 10, 5, 6, 7, 8, 9, 13 };
                    int escolhido = -1, menor = int.MaxValue;
                    for (int k = 0; k < pref.Length; k++)
                        if (Talentos.PodeInvestir(p, pref[k], cfg) && Talentos.Pontos(p, pref[k]) < menor && k < 5 + Talentos.GastosNoRamo(p, RamoDeTalento.Ossos, cfg)) { menor = Talentos.Pontos(p, pref[k]); escolhido = pref[k]; }
                    if (escolhido < 0) for (int k = 0; k < pref.Length && escolhido < 0; k++) if (Talentos.PodeInvestir(p, pref[k], cfg)) escolhido = pref[k];
                    if (escolhido < 0 || !Talentos.Investir(p, escolhido, cfg)) break;
                }
            // 1b) Necrópole: compra sempre a melhoria mais barata que couber no bolso
            while (true)
            {
                int melhor = -1; long menor = long.MaxValue;
                for (int c = 0; c < Necropole.TotalDeConstrucoes; c++)
                {
                    if (!Necropole.PodeMelhorar(p, (Construcao)c, cfg)) continue;
                    long o, e; Necropole.Custo(p, (Construcao)c, cfg, out o, out e);
                    if (o + 5 * e < menor) { menor = o + 5 * e; melhor = c; }
                }
                if (melhor < 0) break;
                Necropole.Melhorar(p, (Construcao)melhor, cfg);
            }
            // 2) melhor exército que cabe no Comando: sempre 1 tanque se houver, depois por valor
            for (int i = 0; i < p.Minions.Count; i++) Progressao.Desativar(p, i);
            var ordem = new List<int>();
            for (int i = 0; i < p.Minions.Count; i++) ordem.Add(i);
            ordem.Sort((a, b) => Valor(p.Minions[b], cfg).CompareTo(Valor(p.Minions[a], cfg)));
            for (int k = 0; k < ordem.Count; k++) Progressao.Ativar(p, ordem[k], cfg);
            // 2b) reserva além de 4 suplentes vira Fragmentos; os Fragmentos sobem a qualidade do melhor minion em campo
            if (UsarSacrificio)
            {
                int reserva = 0;
                for (int k = 0; k < ordem.Count; k++) if (!p.Minions[ordem[k]].Ativo) reserva++;
                var fora = new List<MinionSalvo>();
                for (int k = ordem.Count - 1; k >= 0 && reserva > 4; k--) if (!p.Minions[ordem[k]].Ativo) { fora.Add(p.Minions[ordem[k]]); reserva--; }
                for (int k = 0; k < fora.Count; k++) Sacrificio.Sacrificar(p, p.Minions.IndexOf(fora[k]), cfg);
                bool imbuiu = true;
                while (imbuiu)
                {
                    imbuiu = false; int melhor = -1;
                    for (int i = 0; i < p.Minions.Count; i++)
                        if (p.Minions[i].Ativo && Sacrificio.PodeImbuir(p, i, cfg) && (melhor < 0 || Valor(p.Minions[i], cfg) > Valor(p.Minions[melhor], cfg))) melhor = i;
                    if (melhor >= 0) imbuiu = Sacrificio.Imbuir(p, melhor, cfg);
                }
            }
            // 3) evolui de estágio quem chegou ao teto, e equipa
            for (int i = 0; i < p.Minions.Count; i++) if (p.Minions[i].Ativo) Progressao.EvoluirEstagio(p, i, cfg);
            // Ferreiro: compra o que o ouro alcançar (do mais caro para o mais barato) e deixa o auto-equipar decidir
            Atributos.AutoDistribuir(p, cfg);   // pontos de atributo por igual
            Ferreiro.RepararTudo(p, cfg);       // conserta o que a descida gastou
            Ferreiro.IdentificarTudo(p, cfg);   // primeiro revela o que caiu sem identificar
            if (p.Loja != null) for (int li = p.Loja.Count - 1; li >= 0; li--) Ferreiro.Comprar(p, li, cfg);
            Equipamento.AutoEquipar(p, cfg);
            Bruxa.ComprarTudo(p, cfg);          // livros de feitiço da Bruxa, se o ouro der
            Ferreiro.MelhorarTudo(p, cfg);      // o ouro que sobra vira +N no que está vestido
            p.DesmancharAbaixoDe = RaridadeItem.Magico;
        }

        /// <summary>true = o robô também usa a mão do jogador (toca nos orbes, Detonar e Frenesi); false = jogador ausente.</summary>
        public static bool JogadorAtivo;
        /// <summary>true = o robô funde trincas de almas quando o banco de minions já passa do Comando.</summary>
        public static bool UsarFusao = true;
        /// <summary>true = o robô gasta os pontos de talento.</summary>
        public static bool UsarTalentos = true;
        /// <summary>true = o robô sacrifica a reserva excedente e imbui os melhores minions.</summary>
        public static bool UsarSacrificio = true;
        /// <summary>true = o robô faz oferendas e prepara ritos.</summary>
        public static bool UsarOferendas = true;

        /// <summary>Expedição com um jogador presente: ceifa por toque todo orbe e usa os dois poderes quando valem a pena.</summary>
        public static ResultadoExpedicao RodarComJogador(ConfigJogo cfg, EstadoProgresso p, ulong semente, int inicio)
        {
            var sim = new Simulacao(cfg, p, semente, ModoExpedicao.Avancar, inicio);
            sim.EmitirEventos = false;
            Oferendas.ConsumirRito(p);
            while (!sim.Encerrada)
            {
                if (sim.TickAtual >= ExecutorSemTela.TicksMaximosPadrao) sim.EncerrarPorTempo();
                for (int i = 0; i < sim.Orbes.Count; i++) sim.ComandoCeifar(sim.Orbes[i].Id);
                if (sim.RecargaDetonar <= 0f && sim.CadaveresDetonaveis >= 3 && sim.InimigosVivos >= 2) sim.ComandoDetonar();
                if (sim.RecargaFrenesi <= 0f && sim.InimigosVivos >= 3) sim.ComandoFrenesi();
                if (sim.InimigosVivos >= 2) { sim.ComandoFeiticoDeItem(0); sim.ComandoFeiticoDeItem(1); }
                if (sim.RecargaSopro <= 0f)
                {
                    // Sopro no aliado mais ferido, se alguém está abaixo de 45%
                    Entidade pior = null;
                    for (int i = 0; i < sim.Entidades.Count; i++)
                    {
                        var al = sim.Entidades[i];
                        if (al.Viva && al.Faccao != Faccao.Inimigo && al.Vida < al.VidaMax * 0.45f && (pior == null || al.Vida / al.VidaMax < pior.Vida / pior.VidaMax)) pior = al;
                    }
                    if (pior != null) sim.ComandoCurar(pior.Id);
                }
                // rota: passa pela sala da chave antes do cofre; santuário o quanto antes (a bênção vale por mais salas)
                {
                    int cofre = sim.Salas.IndexOf(TipoSala.Cofre);
                    if (cofre > sim.SalaAtual && sim.ChaveNaSala > cofre && !sim.TemChave) sim.ComandoIrPara(sim.ChaveNaSala);
                    int sant = sim.Salas.IndexOf(TipoSala.Santuario);
                    if (sant > sim.SalaAtual + 1 && sant < sim.Salas.Count - 1 && sim.BencaoAtual == Bencao.Nenhuma && !(cofre > sim.SalaAtual && !sim.TemChave)) sim.ComandoIrPara(sant);
                }
                if (sim.PortaFechada) sim.ComandoAbrirPorta();   // jogador ativo: abre a porta emperrada na hora
                if (sim.Fase == FaseExpedicao.Explorando) sim.ComandoInteragir();
                if (sim.Fase == FaseExpedicao.EmOnda && sim.TickAtual % 25 == 0) for (int qi = 0; qi < sim.Objetos.Count; qi++) if (sim.ComandoQuebrar(qi)) break;   // quebra um barril de vez em quando
                if (sim.BencaosOferecidas != null) sim.ComandoEscolherBencao((sim.BencaosOferecidas[0] == 11 && p.MaiorAndarConcluido < cfg.AcougueiroAbreSozinhoNoRecorde) || (sim.BencaosOferecidas[0] == 12 && p.MaiorAndarConcluido < cfg.ReiAbreSozinhoNoRecorde) ? 1 : 0);   // toca no baú/santuário/fonte
                // interrompe o Golpe Brutal: marca quem está armando (com meio segundo de reação)
                for (int i = 0; i < sim.Entidades.Count; i++)
                {
                    var eg = sim.Entidades[i];
                    if (eg.Viva && eg.PreparandoGolpe > 0f && eg.PreparandoGolpe < cfg.GolpeBrutalAviso - 0.5f) { sim.ComandoMarcar(eg.Id); break; }
                }
                if (sim.MarcadoId < 0 && sim.InimigosVivos > 0)
                {
                    // marca o chefe ou elite; senão, o inimigo mais ferido
                    Entidade alvoM = null;
                    for (int i = 0; i < sim.Entidades.Count; i++)
                    {
                        var e = sim.Entidades[i];
                        if (!e.Viva || e.Faccao != Faccao.Inimigo) continue;
                        bool esp = e.Chefe || e.Elite, espM = alvoM != null && (alvoM.Chefe || alvoM.Elite);
                        if (alvoM == null || (esp && !espM) || (esp == espM && e.Vida < alvoM.Vida)) alvoM = e;
                    }
                    if (alvoM != null) sim.ComandoMarcar(alvoM.Id);
                }
                if (sim.PactosOferecidos != null)
                {
                    // preferência fixa do robô: trégua se o necromante está mal; senão dano, almas, ossos, pressa, itens
                    int[] pref = sim.Necromante.Vida < sim.Necromante.VidaMax * 0.4f ? new[] { 3, 8, 0, 2, 1, 4, 5, 6, 7, 9 } : new[] { 0, 6, 2, 1, 7, 9, 4, 5, 8, 3 };
                    int melhor = 0, pos = 99;
                    for (int o = 0; o < 3; o++) { int k = System.Array.IndexOf(pref, sim.PactosOferecidos[o]); if (k < pos) { pos = k; melhor = o; } }
                    // com o necromante ferido só aceita a trégua; inteiro, aceita a melhor oferta
                    if (sim.Necromante.Vida >= sim.Necromante.VidaMax * 0.6f || sim.PactosOferecidos[melhor] == 3 || sim.PactosOferecidos[melhor] == 8) sim.ComandoPacto(melhor);
                }
                sim.Tick();
            }
            return sim.Resultado;
        }

        public static Resultado Jogar(ConfigJogo cfg, ulong semente, double horasMax, int andarAlvo = 40)
        {
            var r = new Resultado();
            for (int i = 0; i < r.HorasAteAndar.Length; i++) r.HorasAteAndar[i] = double.NaN;
            var p = Progressao.NovoJogo(cfg);
            long ticks = 0, ticksAndares = 0; int andares = 0;
            double tps = cfg.TicksPorSegundo;
            while (ticks / tps / 3600.0 < horasMax && p.MaiorAndarConcluido < andarAlvo)
            {
                int inicio = Progressao.AndarDePartida(p, cfg);
                var res = JogadorAtivo
                    ? RodarComJogador(cfg, p, semente * 7919UL + (ulong)r.Expedicoes, inicio)
                    : ExecutorSemTela.RodarExpedicao(cfg, p, semente * 7919UL + (ulong)r.Expedicoes, ModoExpedicao.Avancar, inicio);
                long t0 = ticks, acum = 0;
                for (int k = 0; k < res.TicksPorAndar.Count; k++)
                {
                    acum += res.TicksPorAndar[k]; ticksAndares += res.TicksPorAndar[k]; andares++;
                    int a = res.AndaresRegistrados[k];
                    if (a < r.HorasAteAndar.Length && double.IsNaN(r.HorasAteAndar[a])) r.HorasAteAndar[a] = (t0 + acum) / tps / 3600.0;
                }
                ticks += res.Ticks + (long)(20 * tps);     // 20 s na Necrópole entre expedições
                if (r.Expedicoes == 0) r.AndarDaPrimeiraExpedicao = res.AndarFinal;
                r.Expedicoes++;
                double min = res.Ticks / tps / 60.0;
                if (min < r.ExpedicaoMaisCurtaMin) r.ExpedicaoMaisCurtaMin = min;
                bool tinhaChefe = p.ChefesDerrotados.Contains(1);
                if (!tinhaChefe && res.AndarFinal >= 10 && inicio <= 10) r.TentativasNoChefe1++;
                Progressao.AplicarResultado(p, res, cfg);
                if (!tinhaChefe && p.ChefesDerrotados.Contains(1)) r.HorasChefe1 = ticks / tps / 3600.0;
                TurnoDoJogador(p, cfg);
            }
            r.Construcoes = (int[])p.Construcoes.Clone(); r.OssosSobrando = p.Ossos; r.EssenciaSobrando = p.Essencia; r.Comando = Progressao.ComandoMaximoEfetivo(p, cfg);
            r.MaiorAndar = p.MaiorAndarConcluido; r.NivelNecromante = p.NivelNecromante; r.Especies = p.EspeciesConhecidas.Count;
            for (int i = 0; i < p.Minions.Count; i++) if (p.Minions[i].Ativo) r.MinionsAtivos++;
            r.SegPorAndar = ticksAndares / tps / Math.Max(1, andares);
            r.ExpedicaoMediaMin = ticks / tps / 60.0 / Math.Max(1, r.Expedicoes);
            return r;
        }

        /// <summary>
        /// Mede a dificuldade seguinte: joga o Normal até abrir o Pesadelo, troca, e marca as horas (contadas desde a troca) até
        /// vencer o chefe do Ato 1 e do Ato 2 no Pesadelo; se abrir o Inferno, troca de novo e mede o chefe do Ato 1 do Inferno.
        /// </summary>
        /// <summary>
        /// Mede a Ascensão: joga até vencer o chefe do Ato 1, ascende, gasta as Cinzas (árvore e/ou legados, em rodízio) e joga de novo.
        /// Devolve as horas de cada vida até o chefe do Ato 1 (mediana das campanhas).
        /// </summary>
        public static string MedirAscensao(int campanhas, int vidas, ConfigJogo cfg, bool usarArvore, bool usarLegados, int ato = 1, bool usarHeranca = false)
        {
            var horas = new List<double>[vidas]; var cinzas = new List<double>[vidas];
            for (int v = 0; v < vidas; v++) { horas[v] = new List<double>(); cinzas[v] = new List<double>(); }
            double tps = cfg.TicksPorSegundo;
            for (ulong semente = 1; semente <= (ulong)campanhas; semente++)
            {
                var p = Progressao.NovoJogo(cfg); int exp = 0;
                for (int v = 0; v < vidas; v++)
                {
                    long ticks = 0; double h = 999;
                    while (ticks / tps / 3600.0 < 12.0 * ato)
                    {
                        var res = ExecutorSemTela.RodarExpedicao(cfg, p, semente * 7919UL + (ulong)exp++, ModoExpedicao.Avancar, Progressao.AndarDePartida(p, cfg));
                        ticks += res.Ticks + (long)(20 * tps);
                        Progressao.AplicarResultado(p, res, cfg);
                        TurnoDoJogador(p, cfg);
                        if (p.ChefesDerrotados.Contains(ato)) { h = ticks / tps / 3600.0; break; }
                    }
                    horas[v].Add(h);
                    long ganho = Ascensao.Ascender(p, cfg); cinzas[v].Add(ganho);
                    bool comprou = true; int rod = 0;
                    while (usarHeranca && Arvore.Comprar(p, Arvore.Heranca, cfg)) { }
                    while (comprou)
                    {
                        comprou = false;
                        for (int k = 0; k < 7 && !comprou; k++)
                        {
                            int q = (rod + k) % 7;
                            if (q < 4) { if (usarArvore && Arvore.Comprar(p, q, cfg)) comprou = true; }
                            else if (usarLegados && Ascensao.Comprar(p, q - 4, cfg)) comprou = true;
                        }
                        rod++;
                    }
                }
            }
            var sb = new StringBuilder();
            for (int v = 0; v < vidas; v++) sb.Append("vida ").Append(v + 1).Append(": chefe do Ato ").Append(ato).Append(" em ").Append(Mediana(horas[v]).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" h (rendeu ").Append(Mediana(cinzas[v]).ToString("0")).Append(" Cinzas)\n");
            return sb.ToString();
        }

        public static bool Rastro;
        public static string MedirDificuldades(int campanhas, double horasMax, ConfigJogo cfg)
        {
            var hN = new List<double>(); var hP1 = new List<double>(); var hP2 = new List<double>(); var hI1 = new List<double>();
            double tps = cfg.TicksPorSegundo;
            for (ulong semente = 1; semente <= (ulong)campanhas; semente++)
            {
                int maxP = 0; var p = Progressao.NovoJogo(cfg); long ticks = 0; int exp = 0; double trocaP = double.NaN, trocaI = double.NaN, p1 = double.NaN, p2 = double.NaN, i1 = double.NaN, normal = double.NaN;
                while (ticks / tps / 3600.0 < horasMax)
                {
                    int inicio = Progressao.AndarDePartida(p, cfg);
                    var res = ExecutorSemTela.RodarExpedicao(cfg, p, semente * 7919UL + (ulong)exp++, ModoExpedicao.Avancar, inicio);
                    ticks += res.Ticks + (long)(20 * tps);
                    Progressao.AplicarResultado(p, res, cfg);
                    TurnoDoJogador(p, cfg);
                    double agora = ticks / tps / 3600.0;
                    if (Rastro && res.Dificuldade >= 1 && (res.AndarFinal > maxP || exp % 40 == 0)) { maxP = Math.Max(maxP, res.AndarFinal); Console.WriteLine("   [" + semente + "] dif " + res.Dificuldade + " andar " + res.AndarFinal + " (partida " + inicio + ") em " + (agora - trocaP).ToString("0.00") + " h, motivo " + res.Motivo + ", chefes " + res.ChefesVencidosNaDescida.Count); }
                    if (res.Dificuldade == 1 && res.ChefesVencidosNaDescida.Contains(1) && double.IsNaN(p1)) p1 = agora - trocaP;
                    if (res.Dificuldade == 1 && res.ChefesVencidosNaDescida.Contains(2) && double.IsNaN(p2)) p2 = agora - trocaP;
                    if (res.Dificuldade == 2 && res.ChefesVencidosNaDescida.Contains(1) && double.IsNaN(i1)) { i1 = agora - trocaI; break; }
                    if (p.Dificuldade == 0 && p.DificuldadeLiberada >= 1) { normal = agora; trocaP = agora; Dificuldades.Definir(p, 1); }
                    else if (p.Dificuldade == 1 && p.DificuldadeLiberada >= 2) { trocaI = agora; Dificuldades.Definir(p, 2); }
                }
                hN.Add(double.IsNaN(normal) ? 999 : normal); hP1.Add(double.IsNaN(p1) ? 999 : p1); hP2.Add(double.IsNaN(p2) ? 999 : p2); hI1.Add(double.IsNaN(i1) ? 999 : i1);
            }
            Func<double, string> f = v => v >= 999 ? "não chegou" : v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " h";
            return "Normal até o chefe do Ato 2: " + f(Mediana(hN)) + "\nPesadelo (desde a troca) chefe do Ato 1: " + f(Mediana(hP1)) + " · chefe do Ato 2: " + f(Mediana(hP2)) + "\nInferno (desde a troca) chefe do Ato 1: " + f(Mediana(hI1));
        }

        static double Mediana(List<double> v) { if (v.Count == 0) return double.NaN; v.Sort(); return v[v.Count / 2]; }

        public sealed class Medidas
        {
            public string Necropole = ""; public double PrimeiraExpedicao, HorasAltarMax; public double MaiorParede, HorasAndar5, HorasChefe1, HorasAndar15, HorasAndar20, SegPorAndar, ExpedicaoMediaMin, TentativasChefe, FracaoVenceChefe, AndarEm8h;
        }

        public static Medidas Medir(ConfigJogo cfg, int n, double horas, StringBuilder sb = null)
        {
            var h5 = new List<double>(); var hc = new List<double>(); var h15 = new List<double>(); var h20 = new List<double>();
            var spa = new List<double>(); var exp = new List<double>(); var tent = new List<double>(); var fim = new List<double>();
            int venceu = 0;
            var nc = new List<double>[Necropole.TotalDeConstrucoes]; for (int c = 0; c < nc.Length; c++) nc[c] = new List<double>();
            var pe = new List<double>(); var niveis = new List<double>();
            var lo = new List<double>(); var le = new List<double>(); var lc = new List<double>();
            var porAndar = new List<double>[41];
            for (int i = 0; i < 41; i++) porAndar[i] = new List<double>();
            for (int i = 0; i < n; i++)
            {
                var r = Jogar(cfg, 100UL + (ulong)i, horas);
                // quem não chegou conta como "mais que o limite" na mediana
                h5.Add(double.IsNaN(r.HorasAteAndar[5]) ? 999 : r.HorasAteAndar[5]);
                hc.Add(double.IsNaN(r.HorasChefe1) ? 999 : r.HorasChefe1);
                h15.Add(double.IsNaN(r.HorasAteAndar[15]) ? 999 : r.HorasAteAndar[15]);
                h20.Add(double.IsNaN(r.HorasAteAndar[20]) ? 999 : r.HorasAteAndar[20]);
                if (!double.IsNaN(r.HorasChefe1)) venceu++;
                for (int c = 0; c < nc.Length; c++) nc[c].Add(r.Construcoes[c]);
                niveis.Add(r.NivelNecromante);
                pe.Add(r.AndarDaPrimeiraExpedicao);
                lo.Add(r.OssosSobrando); le.Add(r.EssenciaSobrando); lc.Add(r.Comando);
                spa.Add(r.SegPorAndar); exp.Add(r.ExpedicaoMediaMin); tent.Add(r.TentativasNoChefe1); fim.Add(r.MaiorAndar);
                for (int a = 1; a <= 40; a++) porAndar[a].Add(double.IsNaN(r.HorasAteAndar[a]) ? 999 : r.HorasAteAndar[a]);
            }
            // maior espera num único andar (até o 20) como fração do tempo total até o andar 20
            double parede = 1.0, t20 = Mediana(porAndar[20]);
            if (t20 < 900)
            {
                parede = 0; double ant = 0;
                for (int a = 1; a <= 20; a++) { double v = Mediana(porAndar[a]); parede = Math.Max(parede, (v - ant) / t20); ant = v; }
            }
            var m = new Medidas { MaiorParede = parede, HorasAndar5 = Mediana(h5), HorasChefe1 = Mediana(hc), HorasAndar15 = Mediana(h15), HorasAndar20 = Mediana(h20),
                                  SegPorAndar = Mediana(spa), ExpedicaoMediaMin = Mediana(exp), TentativasChefe = Mediana(tent),
                                  FracaoVenceChefe = venceu / (double)n, AndarEm8h = Mediana(fim) };
            m.PrimeiraExpedicao = Mediana(pe);
            m.Necropole = "Ao fim da campanha (medianas): Altar nível " + Mediana(nc[0]) + ", Cripta nível " + Mediana(nc[1]) + ", Torre das Almas nível " + Mediana(nc[2]) + ", Ossuário nível " + Mediana(nc[3]) + ", Mesa de Dissecação nível " + Mediana(nc[4])
                + "; Comando " + Mediana(lc) + "; sobrando " + Mediana(lo) + " Ossos e " + Mediana(le) + " de Essência. Nível do necromante: " + Mediana(niveis) + ".";
            if (sb != null)
            {
                sb.AppendLine("| Andar concluído | Horas de jogo (mediana) |").AppendLine("|---|---|");
                for (int a = 1; a <= 40; a++)
                {
                    double v = Mediana(porAndar[a]);
                    if (v > 900) { sb.Append("| ").Append(a).AppendLine(" | não alcançado |"); break; }
                    sb.Append("| ").Append(a).Append(a % 10 == 0 ? " (chefe)" : "").Append(" | ").Append(v.ToString("0.00")).AppendLine(" |");
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ metas de design

        sealed class Meta { public string Nome; public double Min, Max; public Func<Medidas, double> Valor; public string Unidade; }

        static readonly Meta[] Metas =
        {
            new Meta { Nome = "Tempo por andar", Min = 30, Max = 60, Unidade = "s", Valor = m => m.SegPorAndar },
            new Meta { Nome = "Duração média de uma expedição", Min = 3, Max = 20, Unidade = "min", Valor = m => m.ExpedicaoMediaMin },
            new Meta { Nome = "Andar em que termina a primeira expedição (jogo novo)", Min = 6, Max = 10, Unidade = "", Valor = m => m.PrimeiraExpedicao },
            new Meta { Nome = "Primeiro Ponto de Ancoragem (andar 5)", Min = 0.05, Max = 0.5, Unidade = "h", Valor = m => m.HorasAndar5 },
            new Meta { Nome = "Chefe do Ato 1 derrotado", Min = 1.0, Max = 3.0, Unidade = "h", Valor = m => m.HorasChefe1 },
            new Meta { Nome = "Tentativas no chefe do Ato 1 até vencer", Min = 2, Max = 8, Unidade = "", Valor = m => m.TentativasChefe },
            new Meta { Nome = "Maior espera num único andar (fração do tempo até o andar 20)", Min = 0.0, Max = 0.35, Unidade = "", Valor = m => m.MaiorParede },
            new Meta { Nome = "Andar 15", Min = 2.5, Max = 6.0, Unidade = "h", Valor = m => m.HorasAndar15 },
            new Meta { Nome = "Chefe do Ato 2 (andar 20)", Min = 5.0, Max = 12.0, Unidade = "h", Valor = m => m.HorasAndar20 },
        };

        /// <summary>De onde vem a velocidade de progressão: desliga uma fonte de poder por vez.</summary>
        public static string Diagnostico(int n, double horas)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Variante | Andar 5 (h) | Chefe 1 (h) | Andar 15 (h) | Andar 20 (h) | Maior andar | s/andar |").AppendLine("|---|---|---|---|---|---|---|");
            var vs = new List<KeyValuePair<string, Action<ConfigJogo>>>
            {
                new KeyValuePair<string, Action<ConfigJogo>>("Atual", c => { }),
                new KeyValuePair<string, Action<ConfigJogo>>("Sem itens", c => { c.DropItemComum = 0; c.DropItemElite = 0; c.ItensDoChefe = 0; }),
                new KeyValuePair<string, Action<ConfigJogo>>("Sem nível de minion", c => c.MinionCrescimentoPorNivel = 0),
                new KeyValuePair<string, Action<ConfigJogo>>("Sem nível de necromante", c => c.NecromanteCrescimentoPorNivel = 0),
                new KeyValuePair<string, Action<ConfigJogo>>("Sem almas novas", c => { c.DropAlmaComum = 0; c.DropAlmaElite = 0; }),
            };
            foreach (var v in vs)
            {
                var cfg = new ConfigJogo(); v.Value(cfg);
                var m = Medir(cfg, n, horas);
                Func<double, string> f = x => x > 900 ? "—" : x.ToString("0.00");
                sb.Append("| ").Append(v.Key).Append(" | ").Append(f(m.HorasAndar5)).Append(" | ").Append(f(m.HorasChefe1)).Append(" | ").Append(f(m.HorasAndar15)).Append(" | ")
                  .Append(f(m.HorasAndar20)).Append(" | ").Append(m.AndarEm8h.ToString("0")).Append(" | ").Append(m.SegPorAndar.ToString("0.0")).AppendLine(" |");
            }
            return sb.ToString();
        }

        /// <summary>Busca em grade nos botões principais; ordena pela distância às metas.</summary>
        public static string Calibrar(int n, double horas)
        {
            var linhas = new List<KeyValuePair<double, string>>();
            float[] itens = { 1.0f, 1.25f, 1.5f }; float[] chefeVida = { 2f, 3f }; float[] xp = { 0.14f, 0.16f, 0.18f }; float[] chefeDano = { 1.0f, 1.3f };
            // nesta grade: coluna 1 = degrau por Ato; coluna 3 = dano do chefe; coluna 4 = crescimento dos inimigos por andar
            foreach (float gi in itens) foreach (float cv in chefeVida) foreach (float fx in xp) foreach (float cd in chefeDano)
            {
                var cfg = new ConfigJogo { DegrauPorAto = gi, CrescimentoPorAndar = fx, CrescimentoItemPorAndar = fx * 0.667f, ChefeMultVida = cv, ChefeMultDano = cd };
                var m = Medir(cfg, n, horas);
                double erro = 0; int fora = 0;
                foreach (var meta in Metas)
                {
                    double v = meta.Valor(m);
                    if (v > 900) { erro += 5; fora++; continue; }
                    double meio = Math.Sqrt(meta.Min * meta.Max);
                    erro += Math.Abs(Math.Log(v / meio)) / Math.Log(meta.Max / meta.Min);
                    if (v < meta.Min || v > meta.Max) fora++;
                }
                Func<double, string> f = x => x > 900 ? "—" : x.ToString("0.00");
                linhas.Add(new KeyValuePair<double, string>(erro + fora, "| " + gi + " | " + cv + " | " + cd + " | " + fx + " | " + f(m.HorasChefe1) + " | " + m.TentativasChefe + " | " + f(m.HorasAndar15) + " | " + f(m.HorasAndar20) + " | " + m.MaiorParede.ToString("0.00") + " | " + m.SegPorAndar.ToString("0.0") + " | " + m.ExpedicaoMediaMin.ToString("0.0") + " | " + fora + " |"));
            }
            linhas.Sort((a, b) => a.Key.CompareTo(b.Key));
            var sb = new StringBuilder();
            sb.AppendLine("| Degrau/Ato | Chefe vida | Chefe dano | Inimigos/andar | Chefe 1 (h) | Tentativas | Andar 15 (h) | Andar 20 (h) | Parede | s/andar | Expedição (min) | Metas fora |").AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < Math.Min(12, linhas.Count); i++) sb.AppendLine(linhas[i].Value);
            return sb.ToString();
        }

        /// <summary>Ajustes de teste vindos da linha de comando: "--cfg Campo=valor,Campo=valor" (campos float/int de ConfigJogo).</summary>
        public static string Ajustes = "";

        public static ConfigJogo ConfigComAjustes()
        {
            var cfg = new ConfigJogo();
            foreach (var par in Ajustes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = par.Split('=');
                var f = typeof(ConfigJogo).GetField(kv[0]);
                if (f == null) throw new ArgumentException("Campo de ConfigJogo não existe: " + kv[0]);
                if (kv[0] == "XpEscala") continue;
                f.SetValue(cfg, Convert.ChangeType(double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture), f.FieldType));
            }
            return cfg;
        }

        public static string Relatorio(int n, double horas, out bool tudoOk)
        {
            var cfg = ConfigComAjustes();
            var sb = new StringBuilder();
            var curva = new StringBuilder();
            var m = Medir(cfg, n, horas, curva);
            tudoOk = true;
            sb.AppendLine("## Metas de balanceamento (campanha completa, jogador-robô)").AppendLine();
            sb.Append(n).Append(" campanhas de até ").Append(horas.ToString("0")).AppendLine(" h de jogo cada. Tudo passa de uma expedição para a outra; o robô ergue as almas, monta o melhor exército que cabe no Comando e auto-equipa.").AppendLine();
            sb.AppendLine("| Meta | Faixa-alvo | Medido (mediana) | Situação |").AppendLine("|---|---|---|---|");
            foreach (var meta in Metas)
            {
                double v = meta.Valor(m);
                bool ok = v >= meta.Min && v <= meta.Max;
                if (!ok) tudoOk = false;
                sb.Append("| ").Append(meta.Nome).Append(" | ").Append(meta.Min.ToString("0.##")).Append(" a ").Append(meta.Max.ToString("0.##")).Append(' ').Append(meta.Unidade)
                  .Append(" | ").Append(v > 900 ? "não alcançado" : v.ToString("0.00") + " " + meta.Unidade).Append(" | ").AppendLine(ok ? "ok |" : "**FORA** |");
            }
            sb.AppendLine().Append("Campanhas que vencem o chefe do Ato 1 dentro do limite: ").Append((m.FracaoVenceChefe * 100).ToString("0")).Append("%. Maior andar concluído (mediana): ").Append(m.AndarEm8h.ToString("0")).AppendLine(".").AppendLine();
            sb.AppendLine(m.Necropole).AppendLine();
            sb.AppendLine("### Curva de progressão").AppendLine().Append(curva);
            return sb.ToString();
        }
    }
}
