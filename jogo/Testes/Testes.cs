using System;
using System.Collections.Generic;
using System.Text.Json;
using Necro.Nucleo;

namespace Necro.Testes
{
    /// <summary>Testes simples com asserts, sem framework. Cada teste lança exceção ao falhar.</summary>
    public static partial class Testes
    {
        private static int _falhas, _total;
        private static readonly ConfigJogo Cfg = new ConfigJogo();

        private sealed class Falha : Exception { public Falha(string m) : base(m) { } }

        private static void Afirmar(bool condicao, string mensagem) { if (!condicao) throw new Falha(mensagem); }

        private static void Perto(float obtido, float esperado, float tolerancia, string oQue)
        {
            if (Math.Abs(obtido - esperado) > tolerancia) throw new Falha(oQue + ": esperado " + esperado + " ± " + tolerancia + ", obtido " + obtido);
        }

        private static void Teste(string nome, Action corpo)
        {
            _total++;
            try { corpo(); Console.WriteLine("  [ok]    " + nome); }
            catch (Exception ex) { _falhas++; Console.WriteLine("  [FALHA] " + nome + " -> " + ex.Message); }
        }

        // ---- Utilitários de cenário ----

        /// <summary>Arena sem ondas automáticas, com o exército do novo jogo.</summary>
        private static Simulacao Arena(ulong semente = 7, EstadoProgresso p = null, int andar = 1, bool necromanteAtivo = true)
        {
            var sim = new Simulacao(Cfg, p ?? Progressao.NovoJogo(Cfg), semente, ModoExpedicao.Cacar, andar, false);
            sim.NecromanteAtivo = necromanteAtivo;
            return sim;
        }

        /// <summary>Inimigo "boneco": vida exata, sem armadura nem resistências, sem dano.</summary>
        private static Entidade Boneco(Simulacao sim, float vidaMax, float vida, Vec2 pos, bool elite = false, bool chefe = false)
        {
            var e = sim.CriarInimigo(chefe ? EspecieId.AbadessaCosturada : EspecieId.Esqueleto, pos, elite, ModificadorElite.Nenhum, chefe);
            e.VidaMax = vidaMax; e.Vida = vida; e.Armadura = 0f; e.Dano = 0f; e.Escudo = 0f;
            for (int i = 0; i < 6; i++) e.Resistencias[i] = 0f;
            return e;
        }

        private static bool Tem(Simulacao sim, TipoEvento tipo)
        {
            for (int i = 0; i < sim.Eventos.Count; i++) if (sim.Eventos[i].Tipo == tipo) return true;
            return false;
        }

        private static bool Achar(Simulacao sim, TipoEvento tipo, out EventoSim ev)
        {
            for (int i = 0; i < sim.Eventos.Count; i++) if (sim.Eventos[i].Tipo == tipo) { ev = sim.Eventos[i]; return true; }
            ev = default(EventoSim);
            return false;
        }

        private static Entidade PrimeiroMinion(Simulacao sim)
        {
            for (int i = 0; i < sim.Entidades.Count; i++) if (sim.Entidades[i].Faccao == Faccao.Minion) return sim.Entidades[i];
            return null;
        }

        private static int ContarMinionsVivos(Simulacao sim)
        {
            int n = 0;
            for (int i = 0; i < sim.Entidades.Count; i++) if (sim.Entidades[i].Faccao == Faccao.Minion && sim.Entidades[i].Viva) n++;
            return n;
        }

        private static EstadoProgresso ProgressoCom(params EspecieId[] especies)
        {
            var p = new EstadoProgresso { ComandoMaximo = Cfg.ComandoInicial };
            for (int i = 0; i < especies.Length; i++)
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = especies[i], Qualidade = Qualidade.Comum }, Ativo = true });
            return p;
        }

        public static int RodarTodos()
        {
            _falhas = 0; _total = 0;

            Console.WriteLine("- Determinismo");
            Teste("mesma semente -> mesmo hash de estado em todos os pontos de verificação", () =>
            {
                var a = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 42, ModoExpedicao.Avancar, 1);
                var b = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 42, ModoExpedicao.Avancar, 1);
                for (int t = 0; t < 6000 && !a.Encerrada; t++)
                {
                    a.Tick(); b.Tick();
                    if (t % 50 == 0) Afirmar(a.HashDoEstado() == b.HashDoEstado(), "hash divergiu no tick " + t);
                }
                Afirmar(a.HashDoEstado() == b.HashDoEstado(), "hash final divergiu");
                Afirmar(a.TickAtual == b.TickAtual && a.Resultado.Ossos == b.Resultado.Ossos, "resultado divergiu");
            });
            Teste("mão do jogador: toque ceifa na hora, Detonar e Frenesi têm recarga, e os mesmos comandos dão o mesmo hash", () =>
            {
                Func<bool, Simulacao> rodar = ativo =>
                {
                    var s = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 77, ModoExpedicao.Avancar, 1);
                    for (int t = 0; t < 4000 && !s.Encerrada; t++)
                    {
                        if (ativo)
                        {
                            for (int i = 0; i < s.Orbes.Count; i++) s.ComandoCeifar(s.Orbes[i].Id);
                            if (s.CadaveresDetonaveis >= 2) s.ComandoDetonar();
                            if (s.InimigosVivos >= 2) s.ComandoFrenesi();
                        }
                        s.Tick();
                        if (ativo) Afirmar(s.Orbes.Count <= 1, "orbe tocado some no tick seguinte (sobra no máximo o recém-caído)");
                        Afirmar(s.RecargaDetonar <= Cfg.DetonarRecarga && s.RecargaFrenesi <= Cfg.FrenesiRecarga, "recarga nunca passa do teto");
                    }
                    return s;
                };
                var a = rodar(true); var b = rodar(true); var c = rodar(false);
                Afirmar(a.HashDoEstado() == b.HashDoEstado() && a.Resultado.Almas.Count == b.Resultado.Almas.Count, "mesmos comandos -> mesmo resultado");
                Afirmar(a.Resultado.CeifasPorToque > 0 && a.Resultado.CeifasPorToque == a.Resultado.Almas.Count, "toda alma veio por toque: " + a.Resultado.CeifasPorToque + "/" + a.Resultado.Almas.Count);
                Afirmar(c.Resultado.CeifasPorToque == 0, "ausente não ceifa por toque");
                Afirmar(a.Resultado.OrbesPerdidos == 0, "com toque nenhum orbe se perde");
                var s2 = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 78, ModoExpedicao.Avancar, 1);
                s2.ComandoDetonar(); s2.ComandoFrenesi(); s2.ComandoCeifar(12345); s2.Tick();
                Afirmar(s2.RecargaDetonar <= 0f, "Detonar sem cadáver não gasta a recarga");
            });
            Teste("encruzilhada: oferta de três pactos distintos, some se ignorada, e não muda nada para quem não aceita", () =>
            {
                var forte = Progressao.NovoJogo(Cfg); forte.PoderNecromantico = 3f;     // forte o bastante para passar do andar em que as encruzilhadas começam
                var semPacto = new Simulacao(new ConfigJogo { PortasFechadas = false, EmboscadaDeCorredor = false }, forte, 91, ModoExpedicao.Avancar, 1);
                var ignora = new Simulacao(new ConfigJogo { PortasFechadas = false, EmboscadaDeCorredor = false }, forte, 91, ModoExpedicao.Avancar, 1);
                var aceita = new Simulacao(new ConfigJogo { PortasFechadas = false, EmboscadaDeCorredor = false }, forte, 91, ModoExpedicao.Avancar, 1);
                bool viuOferta = false; int aceitos = 0;
                for (int t = 0; t < 9000 && !semPacto.Encerrada; t++)
                {
                    semPacto.Tick(); ignora.Tick();
                    if (!aceita.Encerrada)
                    {
                        if (aceita.PactosOferecidos != null)
                        {
                            var o = aceita.PactosOferecidos; viuOferta = true;
                            Afirmar(o.Length == 3 && o[0] != o[1] && o[1] != o[2] && o[0] != o[2], "três pactos distintos");
                            aceita.ComandoPacto(0); aceitos++;
                        }
                        aceita.Tick();
                    }
                }
                Afirmar(viuOferta && aceita.PactosAtivos.Count > 0 && aceita.Resultado.PactosAceitos == aceita.PactosAtivos.Count, "o pacto aceito fica ativo: " + aceita.PactosAtivos.Count);
                Afirmar(semPacto.HashDoEstado() == ignora.HashDoEstado(), "ignorar a oferta não muda a simulação");
                Afirmar(ignora.PactosAtivos.Count == 0, "sem comando, nenhum pacto");
                var s3 = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 92, ModoExpedicao.Avancar, 1);
                s3.ComandoPacto(1); s3.Tick();
                Afirmar(s3.PactosAtivos.Count == 0, "comando sem oferta aberta não faz nada");
            });
            Teste("arma define o estilo: de cajado o necromante fica atrás; de foice avança, tem mais vida e bate de perto", () =>
            {
                var rng = new Rng(8);
                var pc = Progressao.NovoJogo(Cfg); Equipamento.Equipar(pc, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Cajado));
                var pf = Progressao.NovoJogo(Cfg); Equipamento.Equipar(pf, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Foice));
                var semSalas = new ConfigJogo { ChanceDeSalaEspecial = 0f, ChanceDeSalaVazia = 0f, ObjetosPorSala = 0, MissoesDeCenario = false };   // sem baús nem barris no caminho: o teste mede só o combate
                var sc = new Simulacao(semSalas, pc, 12, ModoExpedicao.Avancar, 1);
                var sf = new Simulacao(semSalas, pf, 12, ModoExpedicao.Avancar, 1);
                Afirmar(!sc.NecromanteCorpoACorpo && sf.NecromanteCorpoACorpo, "foice liga o corpo a corpo");
                Afirmar(sf.Necromante.VidaMax > sc.Necromante.VidaMax * 1.2f && sf.Necromante.Alcance < 3f && sc.Necromante.Alcance > 5f, "foice: mais vida, alcance curto");
                float x0 = sf.Necromante.Pos.X, maxF = x0, maxC = x0;
                for (int t = 0; t < 600 && !sf.Encerrada && !sc.Encerrada; t++)
                {
                    sc.Tick(); sf.Tick();
                    if (sf.Necromante.Pos.X > maxF) maxF = sf.Necromante.Pos.X;
                    if (sc.Necromante.Pos.X > maxC) maxC = sc.Necromante.Pos.X;
                }
                Afirmar(maxF > x0 + 2f, "de foice o necromante avança: " + (maxF - x0));
                Afirmar(maxC < x0 + 1f, "de cajado ele fica no posto: " + (maxC - x0));
            });
            Teste("marca do ceifador: inimigo marcado vira o alvo dos minions, sofre mais dano e a marca some quando ele morre", () =>
            {
                var s = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 33, ModoExpedicao.Avancar, 1);
                Entidade alvo = null; int marcas = 0; bool morreuMarcado = false;
                for (int t = 0; t < 1500 && !s.Encerrada; t++)
                {
                    if (s.MarcadoId < 0)
                    {
                        if (alvo != null && !alvo.Viva) morreuMarcado = true;
                        alvo = null;
                        // marca o inimigo MAIS LONGE do necromante: sem a marca, ninguém iria nele primeiro
                        foreach (var e in s.Entidades) if (e.Viva && e.Faccao == Faccao.Inimigo && (alvo == null || e.Pos.X > alvo.Pos.X)) alvo = e;
                        if (alvo != null) { s.ComandoMarcar(alvo.Id); marcas++; }
                    }
                    s.Tick();
                }
                Afirmar(marcas > 0 && s.Resultado.MarcasFeitas > 0, "marcou");
                Afirmar(morreuMarcado, "um inimigo marcado morreu e a marca foi liberada");
                var s2 = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 33, ModoExpedicao.Avancar, 1);
                s2.ComandoMarcar(s2.Necromante.Id); s2.Tick();
                Afirmar(s2.MarcadoId < 0, "não dá para marcar um aliado");
            });
            Teste("golpe brutal: elite avisa antes de bater; marcar durante o aviso interrompe e atordoa", () =>
            {
                Func<bool, Simulacao> rodar = interrompe =>
                {
                    var s = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 55, ModoExpedicao.Avancar, 1);
                    // um elite quase imortal plantado no meio do exército
                    Entidade minion = null; foreach (var e in s.Entidades) if (e.Faccao == Faccao.Minion) { minion = e; break; }
                    var elite = s.CriarInimigo(EspecieId.Zumbi, minion.Pos + new Vec2(1f, 0f), true);
                    elite.VidaMax = 1e7f; elite.Vida = 1e7f; elite.Dano = 0.01f;
                    for (int t = 0; t < 1200 && !s.Encerrada && s.Resultado.GolpesBrutais + s.Resultado.GolpesInterrompidos < 3; t++)
                    {
                        if (interrompe) foreach (var e in s.Entidades) if (e.Viva && e.PreparandoGolpe > 0f) { s.ComandoMarcar(e.Id); break; }
                        s.Tick();
                        foreach (var e in s.Entidades) if (e.PreparandoGolpe > 0f) Afirmar(e.Elite || e.Chefe, "só elite ou chefe arma o golpe");
                    }
                    return s;
                };
                var livre = rodar(false); var cortado = rodar(true);
                Afirmar(livre.Resultado.GolpesBrutais > 0 && livre.Resultado.GolpesInterrompidos == 0, "sem o jogador, o golpe sai: " + livre.Resultado.GolpesBrutais);
                Afirmar(cortado.Resultado.GolpesInterrompidos > 0 && cortado.Resultado.GolpesBrutais == 0, "com a marca no aviso, o golpe não sai: " + cortado.Resultado.GolpesInterrompidos + " interrompidos, " + cortado.Resultado.GolpesBrutais + " desferidos");
            });
            Teste("todo minion ativo entra em campo: o Comando da Cripta vale na simulação (caso da Abadessa)", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Construcoes[(int)Construcao.Cripta] = 10 * Cfg.NiveisDeCriptaPorComando;     // +10 de Comando só pela Cripta
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.AbadessaCosturada, Qualidade = Qualidade.Comum } });
                Afirmar(Progressao.PodeAtivar(p, p.Minions.Count - 1, Cfg), "a Necrópole deixa ativar a Abadessa");
                Progressao.Ativar(p, p.Minions.Count - 1, Cfg);
                var s = new Simulacao(Cfg, p, 4, ModoExpedicao.Avancar, 1);
                int ativos = 0, emCampo = 0; bool abadessa = false;
                foreach (var m in p.Minions) if (m.Ativo) ativos++;
                foreach (var e in s.Entidades) if (e.Faccao == Faccao.Minion) { emCampo++; if (e.Especie == EspecieId.AbadessaCosturada) abadessa = true; }
                Afirmar(abadessa && emCampo == ativos, "em campo " + emCampo + " de " + ativos + " ativos; Abadessa presente: " + abadessa);
            });
            Teste("sopro profano: cura o aliado tocado, respeita a recarga e não cura inimigo", () =>
            {
                var s = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 21, ModoExpedicao.Avancar, 1);
                Entidade m = null, ini = null;
                foreach (var e in s.Entidades) { if (e.Faccao == Faccao.Minion && m == null) m = e; if (e.Faccao == Faccao.Inimigo && ini == null) ini = e; }
                m.Vida = m.VidaMax * 0.2f; float antes = m.Vida;
                s.ComandoCurar(m.Id); s.Tick();
                Afirmar(m.Vida > antes + m.VidaMax * Cfg.SoproCura * 0.8f && s.RecargaSopro > 0f && s.Resultado.SoprosUsados == 1, "curou e entrou em recarga");
                m.Vida = m.VidaMax * 0.2f; antes = m.Vida; s.ComandoCurar(m.Id); s.Tick();
                Afirmar(s.Resultado.SoprosUsados == 1, "em recarga não cura de novo");
                var s2 = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 21, ModoExpedicao.Avancar, 1);
                foreach (var e in s2.Entidades) if (e.Faccao == Faccao.Inimigo) { e.Vida = e.VidaMax * 0.5f; s2.ComandoCurar(e.Id); break; }
                s2.Tick(); Afirmar(s2.Resultado.SoprosUsados == 0 && s2.RecargaSopro <= 0f, "inimigo não recebe o sopro");
            });
            Teste("feitiços de item: cada arma dá o seu, a raridade pesa, o automático tem recarga maior e nada acontece sem comando", () =>
            {
                var rng = new Rng(9);
                Func<TipoItem, RaridadeItem, EstadoProgresso> com = (tipo, r) => { var p = Progressao.NovoJogo(Cfg); Equipamento.Equipar(p, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, r, tipo)); return p; };
                var pc = com(TipoItem.Cajado, RaridadeItem.Comum);
                var s = new Simulacao(Cfg, pc, 14, ModoExpedicao.Avancar, 1);
                Afirmar(s.FeiticoDoEspaco(0) == FeiticoDeItem.RaioSombrio && s.FeiticoDoEspaco(1) == FeiticoDeItem.Nenhum, "cajado dá Raio Sombrio; mão vazia não dá nada");
                Afirmar(new Simulacao(Cfg, com(TipoItem.Foice, RaridadeItem.Comum), 14, ModoExpedicao.Avancar, 1).FeiticoDoEspaco(0) == FeiticoDeItem.Ceifada, "foice dá Ceifada");
                Afirmar(new Simulacao(Cfg, com(TipoItem.Grimorio, RaridadeItem.Comum), 14, ModoExpedicao.Avancar, 1).FeiticoDoEspaco(1) == FeiticoDeItem.ChuvaDeOssos, "grimório dá Chuva de Ossos");
                Afirmar(new Simulacao(Cfg, com(TipoItem.Relicario, RaridadeItem.Comum), 14, ModoExpedicao.Avancar, 1).FeiticoDoEspaco(1) == FeiticoDeItem.EscudoDeAlmas, "relicário dá Escudo de Almas");
                var parado = new Simulacao(Cfg, pc, 14, ModoExpedicao.Avancar, 1);
                for (int t = 0; t < 400; t++) parado.Tick();
                Afirmar(parado.Resultado.FeiticosDeItem == 0, "sem comando e sem automático, o feitiço não sai");
                for (int t = 0; t < 200 && s.InimigosVivos == 0; t++) s.Tick();     // espera a primeira onda entrar
                s.ComandoFeiticoDeItem(0); s.Tick();
                Afirmar(s.Resultado.FeiticosDeItem == 1, "o comando lança");
                Perto(s.RecargaDoFeiticoDeItem(0), Cfg.RaioSombrioRecarga, 0.2f, "recarga manual");
                s.ComandoFeiticoDeItem(0); s.Tick(); Afirmar(s.Resultado.FeiticosDeItem == 1, "em recarga não lança");
                var pa = com(TipoItem.Cajado, RaridadeItem.Comum); pa.AutoFeiticos[Simulacao.AutoArma] = true;
                var sa = new Simulacao(Cfg, pa, 14, ModoExpedicao.Avancar, 1);
                float recargaAuto = 0f;
                for (int t = 0; t < 300 && recargaAuto == 0f; t++) { sa.Tick(); if (sa.Resultado.FeiticosDeItem > 0) recargaAuto = sa.RecargaDoFeiticoDeItem(0); }
                Perto(recargaAuto, Cfg.RaioSombrioRecarga * Cfg.AutoFeiticoPenalidade, 0.3f, "no automático a recarga é maior");
                // Escudo de Almas protege
                var pe = com(TipoItem.Relicario, RaridadeItem.Raro);
                var se = new Simulacao(Cfg, pe, 14, ModoExpedicao.Avancar, 1);
                for (int t = 0; t < 200 && se.InimigosVivos == 0; t++) se.Tick();
                se.ComandoFeiticoDeItem(1); se.Tick();
                float esperado = se.Necromante.VidaMax * Cfg.EscudoDeAlmasFracao * (1f + Cfg.FeiticoDeItemPorRaridade * (int)RaridadeItem.Raro);
                Afirmar(se.Necromante.Escudo > esperado * 0.5f && se.Necromante.Escudo <= esperado * 1.01f, "escudo no necromante: " + se.Necromante.Escudo + " de " + esperado);
            });
            Teste("Atirador dispara projétil: o dano só chega depois do voo, e todo projétil termina", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Minions.Clear();
                for (int i = 0; i < 4; i++)
                    p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.ArqueiroOssudo, Qualidade = Qualidade.Comum }, Nivel = 5, Ativo = true });
                var s2 = new Simulacao(Cfg, p, 7, ModoExpedicao.Avancar, 1);
                int lancados = 0, terminados = 0, tickPrimeiroLancado = -1, tickPrimeiroFim = -1;
                for (int t = 0; t < 1500 && !s2.Encerrada && terminados < 20; t++)
                {
                    s2.Tick();
                    for (int i = 0; i < s2.Eventos.Count; i++)
                    {
                        var ev = s2.Eventos[i];
                        if (ev.Tipo == TipoEvento.ProjetilLancado) { lancados++; if (tickPrimeiroLancado < 0) tickPrimeiroLancado = ev.Tick; }
                        if (ev.Tipo == TipoEvento.ProjetilTerminou) { terminados++; if (tickPrimeiroFim < 0) tickPrimeiroFim = ev.Tick; }
                    }
                }
                Afirmar(lancados >= 20, "poucos projéteis lançados: " + lancados);
                Afirmar(tickPrimeiroFim > tickPrimeiroLancado, "o projétil deveria levar pelo menos 1 tick para chegar");
                Afirmar(lancados - terminados == s2.Projeteis.Count, "projéteis perdidos: lançados " + lancados + ", terminados " + terminados + ", em voo " + s2.Projeteis.Count);
            });
            Teste("sementes diferentes -> hashes diferentes", () =>
            {
                var a = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 42, ModoExpedicao.Avancar, 1);
                var b = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 43, ModoExpedicao.Avancar, 1);
                for (int t = 0; t < 600; t++) { a.Tick(); b.Tick(); }
                Afirmar(a.HashDoEstado() != b.HashDoEstado(), "hashes iguais com sementes diferentes");
            });
            Teste("emitir ou não eventos não muda a simulação; executor sem tela = laço de Tick()", () =>
            {
                var a = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 99, ModoExpedicao.Avancar, 1);
                var b = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 99, ModoExpedicao.Avancar, 1);
                b.EmitirEventos = false;
                while (!a.Encerrada) { a.Tick(); b.Tick(); }
                Afirmar(b.Encerrada && a.HashDoEstado() == b.HashDoEstado(), "divergiu com eventos desligados");
                var r = ExecutorSemTela.RodarExpedicao(Cfg, Progressao.NovoJogo(Cfg), 99, ModoExpedicao.Avancar, 1);
                Afirmar(r.Ticks == a.Resultado.Ticks && r.Ossos == a.Resultado.Ossos && r.AndarFinal == a.Resultado.AndarFinal, "executor divergiu");
            });
            Teste("RNG: sequência reproduzível e uniforme", () =>
            {
                var r1 = new Rng(123); var r2 = new Rng(123);
                for (int i = 0; i < 1000; i++) Afirmar(r1.ProximoULong() == r2.ProximoULong(), "sequência divergiu");
                var r = new Rng(5); double soma = 0; int n = 200000;
                for (int i = 0; i < n; i++) { float v = r.Proximo01(); Afirmar(v >= 0f && v < 1f, "fora de [0,1)"); soma += v; }
                Perto((float)(soma / n), 0.5f, 0.005f, "média do RNG");
            });

            Console.WriteLine("- Fórmulas de dano, mitigação e tetos");
            Teste("mitigação da armadura = A / (A + 100 + 20 x andar)", () =>
            {
                Perto(Formulas.MitigacaoArmadura(100f, 1, Cfg), 100f / 220f, 1e-5f, "armadura 100 no andar 1");
                Perto(Formulas.MitigacaoArmadura(100f, 10, Cfg), 100f / 400f, 1e-5f, "armadura 100 no andar 10");
                Perto(Formulas.MitigacaoArmadura(0f, 5, Cfg), 0f, 1e-6f, "sem armadura");
                Afirmar(Formulas.MitigacaoArmadura(50f, 8, Cfg) < Formulas.MitigacaoArmadura(50f, 2, Cfg), "armadura deveria valer menos em andares altos");
            });
            Teste("tetos de 75% (armadura, resistência e total)", () =>
            {
                Perto(Formulas.MitigacaoArmadura(1e7f, 1, Cfg), 0.75f, 1e-6f, "teto da armadura");
                Perto(Formulas.ResistenciaEfetiva(0.95f, Cfg), 0.75f, 1e-6f, "teto da resistência");
                Perto(Formulas.MitigacaoTotal(1e7f, 0.9f, TipoDano.Fisico, 1, Cfg), 0.75f, 1e-6f, "teto do total");
                Perto(Formulas.MitigacaoTotal(1e7f, 0.3f, TipoDano.Fogo, 1, Cfg), 0.3f, 1e-6f, "armadura não mitiga dano elemental");
            });
            Teste("Dano = Base x (1 + Σ aumentos) x Π(1 + mais) x (1 - mitigação)", () =>
            {
                Perto(Formulas.Dano(100f, 0.5f, 1.2f * 1.5f, 0.25f), 100f * 1.5f * 1.8f * 0.75f, 1e-3f, "fórmula");
                Perto(Formulas.Dano(10f, 0f, 1f, 0f), 10f, 1e-6f, "sem modificadores");
            });
            Teste("escala dos inimigos: composta por andar (Config.CrescimentoPorAndar)", () =>
            {
                Perto(Formulas.EscalaInimigo(1, Cfg), Cfg.AlivioInicial, 1e-6f, "andar 1 (com o alívio inicial)");
                Perto(Formulas.EscalaInimigo(2, Cfg), (1f + Cfg.CrescimentoPorAndar) * Cfg.AlivioInicial, 1e-5f, "andar 2");
                Perto(Formulas.EscalaInimigo(11, Cfg), (float)Math.Pow(1.0 + Cfg.CrescimentoPorAndar, 10) * Cfg.DegrauPorAto, 1e-2f, "andar 11 (com o degrau do Ato 2)");
                var s1 = Arena(1, null, 1); var s5 = Arena(1, null, 5);
                var e1 = s1.CriarInimigo(EspecieId.Zumbi, new Vec2(10, 6)); var e5 = s5.CriarInimigo(EspecieId.Zumbi, new Vec2(10, 6));
                Perto(e5.VidaMax / e1.VidaMax, (float)Math.Pow(1.0 + Cfg.CrescimentoPorAndar, 4), 1e-3f, "vida andar 5 / andar 1");
                Perto(e5.Dano / e1.Dano, (float)Math.Pow(1.0 + Cfg.CrescimentoPorAndar, 4), 1e-3f, "dano andar 5 / andar 1");
            });
            Teste("dano aplicado na simulação respeita mitigação, tetos e vulnerabilidade", () =>
            {
                var sim = Arena(1, null, 1, false);
                var e = Boneco(sim, 1e6f, 1e6f, new Vec2(10, 6));
                Perto(sim.CausarDano(null, e, 100f, TipoDano.Fisico, false), 100f, 1e-3f, "sem mitigação");
                e.Armadura = 120f; // andar 1: 120 / (120 + 120) = 50%
                Perto(sim.CausarDano(null, e, 100f, TipoDano.Fisico, false), 50f, 1e-3f, "armadura 50%");
                Perto(sim.CausarDano(null, e, 100f, TipoDano.Gelo, false), 100f, 1e-3f, "armadura não afeta Gelo");
                e.Armadura = 1e7f;
                Perto(sim.CausarDano(null, e, 100f, TipoDano.Fisico, false), 25f, 1e-3f, "teto de armadura");
                e.Resistencias[(int)TipoDano.Fogo] = 0.99f;
                Perto(sim.CausarDano(null, e, 100f, TipoDano.Fogo, false), 25f, 1e-3f, "teto de resistência");
                e.Resistencias[(int)TipoDano.Raio] = -0.2f;
                Perto(sim.CausarDano(null, e, 100f, TipoDano.Raio, false), 120f, 1e-3f, "vulnerabilidade");
            });
            Teste("crítico: ~5% dos golpes, 150% de dano", () =>
            {
                var sim = Arena(3, null, 1, false);
                var e = Boneco(sim, 1e9f, 1e9f, new Vec2(10, 6));
                var m = PrimeiroMinion(sim);
                int crits = 0, n = 40000; bool valoresOk = true;
                for (int i = 0; i < n; i++)
                {
                    float d = sim.CausarDano(m, e, 10f, TipoDano.Fisico, true);
                    if (Math.Abs(d - 15f) < 1e-3f) crits++; else if (Math.Abs(d - 10f) > 1e-3f) valoresOk = false;
                    sim.Eventos.Clear();
                }
                Afirmar(valoresOk, "dano fora de 10 ou 15");
                Perto(crits / (float)n, 0.05f, 0.006f, "taxa de crítico");
            });
            Teste("Maldição da Fraqueza (-20%) e Sacrifício (+50%) entram como multiplicadores 'mais'", () =>
            {
                var sim = Arena(1, null, 1, false);
                var ini = Boneco(sim, 1e6f, 1e6f, new Vec2(10, 6));
                var m = PrimeiroMinion(sim);
                m.Armadura = 0f; for (int i = 0; i < 6; i++) m.Resistencias[i] = 0f; m.VidaMax = 1e6f; m.Vida = 1e6f;
                ini.FraquezaTempo = 5f;
                Perto(sim.CausarDano(ini, m, 100f, TipoDano.Sombra, false), 80f, 1e-3f, "fraqueza");
            });

            Console.WriteLine("- Almas: drop, qualidade, afixos");
            Teste("distribuição de qualidade 60/25/10/4/1 (400 mil sorteios)", () =>
            {
                var rng = new Rng(2024); int n = 400000; var c = new int[5];
                for (int i = 0; i < n; i++) c[(int)GeradorDeAlmas.SortearQualidade(rng, Cfg)]++;
                float[] esperado = { 0.60f, 0.25f, 0.10f, 0.04f, 0.01f };
                float[] tol = { 0.005f, 0.005f, 0.004f, 0.003f, 0.0015f };
                for (int i = 0; i < 5; i++) Perto(c[i] / (float)n, esperado[i], tol[i], "qualidade " + (Qualidade)i);
            });
            Teste("multiplicadores e nº de afixos por qualidade; Profana sempre tem traço", () =>
            {
                float[] mult = { 0.8f, 1.0f, 1.2f, 1.45f, 1.75f };
                int[] min = { 0, 1, 1, 2, 3 }, max = { 0, 1, 2, 3, 3 };
                var rng = new Rng(11);
                for (int q = 0; q < 5; q++)
                {
                    Perto(Cfg.MultQualidade[q], mult[q], 1e-6f, "multiplicador " + (Qualidade)q);
                    bool viuMin = false, viuMax = false;
                    for (int i = 0; i < 2000; i++)
                    {
                        var a = GeradorDeAlmas.Gerar(rng, Cfg, EspecieId.Esqueleto, (Qualidade)q);
                        Afirmar(a.Afixos.Count >= min[q] && a.Afixos.Count <= max[q], (Qualidade)q + " com " + a.Afixos.Count + " afixos");
                        if (a.Afixos.Count == min[q]) viuMin = true;
                        if (a.Afixos.Count == max[q]) viuMax = true;
                        var vistos = new HashSet<Afixo>(a.Afixos);
                        Afirmar(vistos.Count == a.Afixos.Count, "afixo repetido");
                        Afirmar((a.Traco != TracoProfano.Nenhum) == (q == (int)Qualidade.Profana), "traço só em Profana");
                    }
                    Afirmar(viuMin && viuMax, "faixa de afixos de " + (Qualidade)q + " não foi coberta");
                }
            });
            Teste("chance de drop: comum 2%, elite 15% (medido matando inimigos na simulação)", () =>
            {
                var sim = Arena(77, null, 1, false);
                int n = 30000, orbes = 0;
                for (int i = 0; i < n; i++)
                {
                    var e = Boneco(sim, 100f, 10f, new Vec2(10, 6));
                    sim.CausarDano(null, e, 20f, TipoDano.Fisico, false);
                    orbes += sim.Orbes.Count;
                    sim.Orbes.Clear(); sim.Cadaveres.Clear(); sim.Entidades.Remove(e); sim.Eventos.Clear();
                }
                Perto(orbes / (float)n, 0.02f, 0.004f, "drop comum");
                orbes = 0; n = 12000;
                for (int i = 0; i < n; i++)
                {
                    var e = Boneco(sim, 100f, 10f, new Vec2(10, 6), true);
                    sim.CausarDano(null, e, 20f, TipoDano.Fisico, false);
                    orbes += sim.Orbes.Count;
                    sim.Orbes.Clear(); sim.Cadaveres.Clear(); sim.Entidades.Remove(e); sim.Eventos.Clear();
                }
                Perto(orbes / (float)n, 0.15f, 0.012f, "drop de elite");
            });
            Teste("chefe: 100% na primeira vitória, 25% depois", () =>
            {
                var sim = Arena(5, null, 10, false);
                var chefe = Boneco(sim, 100f, 10f, new Vec2(10, 6), false, true);
                sim.CausarDano(null, chefe, 20f, TipoDano.Fisico, false);
                Afirmar(sim.Orbes.Count == 1, "primeira vitória deveria soltar alma");
                Afirmar(sim.Resultado.ChefesDerrotados.Contains(1), "chefe do Ato 1 não registrado");
                Afirmar(sim.Orbes[0].Alma.Especie == EspecieId.AbadessaCosturada, "alma da espécie errada");
                int orbes = 0, n = 8000;
                for (int i = 0; i < n; i++)
                {
                    sim.Orbes.Clear(); sim.Cadaveres.Clear(); sim.Entidades.Remove(chefe); sim.Eventos.Clear();
                    chefe = Boneco(sim, 100f, 10f, new Vec2(10, 6), false, true);
                    sim.CausarDano(null, chefe, 20f, TipoDano.Fisico, false);
                    orbes += sim.Orbes.Count;
                }
                Perto(orbes / (float)n, 0.25f, 0.015f, "drop repetido de chefe");
                Perto(GeradorDeAlmas.ChanceDeDrop(Cfg, false, true, false), 1f, 1e-6f, "tabela: primeira");
            });

            Console.WriteLine("- Ceifa de almas");
            Teste("canalizar 1,5 s ceifa a alma", () =>
            {
                var sim = Arena();
                sim.CriarOrbe(new Vec2(12, 6), GeradorDeAlmas.Gerar(sim.Rng, Cfg, EspecieId.Zumbi));
                sim.Tick();
                Afirmar(Tem(sim, TipoEvento.CeifaIniciada) && sim.Ceifando, "deveria começar a canalizar no 1º tick");
                int inicio = sim.TickAtual, fim = -1;
                for (int t = 0; t < 40 && fim < 0; t++) { sim.Tick(); if (Tem(sim, TipoEvento.AlmaCeifada)) fim = sim.TickAtual; }
                Afirmar(fim - inicio == 15, "ceifa deveria levar 15 ticks, levou " + (fim - inicio));
                Afirmar(sim.Resultado.Almas.Count == 1 && sim.Resultado.Almas[0].Especie == EspecieId.Zumbi && sim.Orbes.Count == 0, "alma não entrou no resultado");
            });
            Teste("dano > 15% da vida máxima interrompe; tenta de novo enquanto o orbe existir", () =>
            {
                var sim = Arena();
                var n = sim.Necromante; n.Armadura = 0f; for (int i = 0; i < 6; i++) n.Resistencias[i] = 0f;
                sim.CriarOrbe(new Vec2(12, 6), GeradorDeAlmas.Gerar(sim.Rng, Cfg, EspecieId.Zumbi));
                for (int t = 0; t < 5; t++) sim.Tick();
                Afirmar(sim.Ceifando, "deveria estar canalizando");
                sim.CausarDano(null, n, n.VidaMax * 0.10f, TipoDano.Sombra, false);
                Afirmar(sim.Ceifando, "10% não deveria interromper");
                sim.CausarDano(null, n, n.VidaMax * 0.06f, TipoDano.Sombra, false);
                Afirmar(!sim.Ceifando && Tem(sim, TipoEvento.CeifaInterrompida), "16% acumulado deveria interromper");
                Afirmar(sim.Orbes.Count == 1 && sim.Resultado.Almas.Count == 0, "orbe deveria continuar na arena");
                bool reiniciou = false, ceifou = false;
                for (int t = 0; t < 40; t++)
                {
                    sim.Tick();
                    if (Tem(sim, TipoEvento.CeifaIniciada)) reiniciou = true;
                    if (Tem(sim, TipoEvento.AlmaCeifada)) ceifou = true;
                }
                Afirmar(reiniciou && ceifou, "deveria tentar de novo e conseguir");
                Afirmar(sim.Resultado.CeifasInterrompidas == 1, "contador de interrupções");
            });
            Teste("orbe some em 8 s se a ceifa nunca termina", () =>
            {
                var sim = Arena();
                var n = sim.Necromante; n.VidaMax = 1e6f; n.Vida = 1e6f; n.Armadura = 0f; for (int i = 0; i < 6; i++) n.Resistencias[i] = 0f;
                sim.CriarOrbe(new Vec2(12, 6), GeradorDeAlmas.Gerar(sim.Rng, Cfg, EspecieId.Zumbi));
                int expirou = -1;
                for (int t = 1; t <= 100 && expirou < 0; t++)
                {
                    sim.Tick();
                    if (Tem(sim, TipoEvento.OrbeExpirou)) expirou = t;
                    n.Vida = n.VidaMax;
                    if (sim.Ceifando) sim.CausarDano(null, n, n.VidaMax * 0.2f, TipoDano.Sombra, false);
                }
                Afirmar(expirou == 80, "orbe deveria expirar no tick 80, expirou em " + expirou);
                Afirmar(sim.Resultado.Almas.Count == 0 && sim.Resultado.OrbesPerdidos == 1 && sim.Resultado.CeifasInterrompidas >= 2, "orbe perdido / várias tentativas");
            });

            Console.WriteLine("- Comando, minions e famílias");
            Teste("custo por porte 1/3/6/10 e limite de Comando", () =>
            {
                Afirmar(Cfg.CustoPorPorte[0] == 1 && Cfg.CustoPorPorte[1] == 3 && Cfg.CustoPorPorte[2] == 6 && Cfg.CustoPorPorte[3] == 10, "tabela de custos");
                Afirmar(Progressao.CustoDeComando(EspecieId.AcolitoCaido, Cfg) == 3 && Progressao.CustoDeComando(EspecieId.AbadessaCosturada, Cfg) == 10, "custo por espécie");
                var p = Progressao.NovoJogo(Cfg);
                Afirmar(p.ComandoMaximo == 10 && Progressao.ComandoEmUso(p, Cfg) == 3, "novo jogo: 3 de 10");
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.AcolitoCaido } });   // 3
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.AcolitoCaido } });   // 3
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.AcolitoCaido } });   // não cabe
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Diabrete } });       // 1
                Afirmar(Progressao.Ativar(p, 3, Cfg) && Progressao.Ativar(p, 4, Cfg), "dois Acólitos cabem (9)");
                Afirmar(!Progressao.Ativar(p, 5, Cfg) && !p.Minions[5].Ativo, "terceiro Acólito não cabe (12 > 10)");
                Afirmar(Progressao.Ativar(p, 6, Cfg) && Progressao.ComandoEmUso(p, Cfg) == 10, "Diabrete fecha em 10");
            });
            Teste("a simulação nunca coloca em campo mais que o Comando", () =>
            {
                var p = new EstadoProgresso { ComandoMaximo = 10 };
                for (int i = 0; i < 14; i++) p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = i == 2 ? EspecieId.AcolitoCaido : EspecieId.Esqueleto }, Ativo = true });
                var sim = Arena(1, p);
                int custo = 0;
                for (int i = 0; i < sim.Entidades.Count; i++) if (sim.Entidades[i].Faccao == Faccao.Minion) custo += sim.Entidades[i].Custo;
                Afirmar(custo == 10, "custo em campo = " + custo);
            });
            Teste("Atributo = Base x Qualidade x Nível x (1 + Poder)", () =>
            {
                var d = TabelaEspecies.Obter(EspecieId.Esqueleto);
                Perto(Formulas.AtributoMinion(d.Vida, Qualidade.Ancestral, 11, 0.2f, Cfg), d.Vida * 1.45f * 1.5f * 1.2f, 1e-2f, "fórmula");
                var p = new EstadoProgresso { PoderNecromantico = 0.2f };
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Ancestral }, Nivel = 11, Ativo = true });
                var m = PrimeiroMinion(Arena(1, p));
                Perto(m.VidaMax, d.Vida * 1.45f * 1.5f * 1.2f, 1e-2f, "vida na simulação");
                Perto(m.Dano, d.Dano * 1.45f * 1.5f * 1.2f, 1e-2f, "dano na simulação");
            });
            Teste("família: 3 = bônus, 5 = bônus maior; afixos alteram atributos", () =>
            {
                float vidaBase = TabelaEspecies.Obter(EspecieId.Esqueleto).Vida;
                Perto(PrimeiroMinion(Arena(1, ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto))).VidaMax, vidaBase, 1e-3f, "2 mortos-vivos");
                Perto(PrimeiroMinion(Arena(1, ProgressoCom(EspecieId.Esqueleto, EspecieId.Zumbi, EspecieId.ArqueiroOssudo))).VidaMax, vidaBase * 1.10f, 1e-3f, "3 mortos-vivos");
                Perto(PrimeiroMinion(Arena(1, ProgressoCom(EspecieId.Esqueleto, EspecieId.Zumbi, EspecieId.ArqueiroOssudo, EspecieId.Esqueleto, EspecieId.Esqueleto))).VidaMax, vidaBase * 1.25f, 1e-3f, "5 mortos-vivos");
                var p = ProgressoCom(EspecieId.Esqueleto);
                p.Minions[0].Alma.Afixos.Add(Afixo.Frenetico); p.Minions[0].Alma.Afixos.Add(Afixo.Petreo); p.Minions[0].Alma.Afixos.Add(Afixo.Faminto);
                var m = PrimeiroMinion(Arena(1, p)); var d = TabelaEspecies.Obter(EspecieId.Esqueleto);
                Perto(m.VelAtaque, d.VelAtaque * 1.2f, 1e-4f, "Frenético");
                Perto(m.Armadura, d.Armadura + Cfg.PetreoArmadura, 1e-3f, "Pétreo");
                Perto(m.RouboDeVida, Cfg.FamintoRoubo, 1e-6f, "Faminto");
            });
            Teste("XP dividido entre ativos; nível máximo 20/40/60 por estágio", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Zumbi }, Ativo = false });
                var r = new ResultadoExpedicao { Xp = 300f };
                Progressao.AplicarResultado(p, r, Cfg);
                Afirmar(p.Minions[0].Nivel == p.Minions[1].Nivel && p.Minions[0].Nivel > 1, "ativos deveriam subir igual");
                Afirmar(p.Minions[3].Nivel == 1 && p.Minions[3].Xp == 0f, "inativo não ganha XP");
                var m = new MinionSalvo { Alma = new Alma { Especie = EspecieId.Zumbi } };
                Progressao.DarXp(m, 1e9f, Cfg); Afirmar(m.Nivel == 20, "estágio 1 trava em 20");
                m.Estagio = 2; Progressao.DarXp(m, 1e9f, Cfg); Afirmar(m.Nivel == 40, "estágio 2 trava em 40");
                m.Estagio = 3; Progressao.DarXp(m, 1e9f, Cfg); Afirmar(m.Nivel == 60, "estágio 3 trava em 60");
            });
            Teste("morte de minion não encerra; morte do necromante encerra", () =>
            {
                var sim = Arena(1, null, 1, false);
                for (int i = 0; i < sim.Entidades.Count; i++)
                    if (sim.Entidades[i].Faccao == Faccao.Minion) sim.CausarDano(null, sim.Entidades[i], 1e6f, TipoDano.Fisico, false);
                sim.Tick();
                Afirmar(!sim.Encerrada && ContarMinionsVivos(sim) == 0, "expedição deveria continuar sem minions");
                sim.CausarDano(null, sim.Necromante, 1e6f, TipoDano.Fisico, false);
                sim.Tick();
                Afirmar(sim.Encerrada && sim.Resultado.Motivo == MotivoFim.NecromanteMorreu && Tem(sim, TipoEvento.ExpedicaoEncerrada), "deveria encerrar");
            });

            Console.WriteLine("- Cadáveres, overkill e execução");
            Teste("tipo de dano do golpe final define a condição do cadáver", () =>
            {
                var esperado = new[] { CondicaoCadaver.Mutilado, CondicaoCadaver.Queimado, CondicaoCadaver.Congelado, CondicaoCadaver.Eletrizado, CondicaoCadaver.Putrefato, CondicaoCadaver.Oco };
                for (int t = 0; t < 6; t++)
                {
                    var sim = Arena(1, null, 1, false);
                    var e = Boneco(sim, 100f, 30f, new Vec2(10, 6));
                    sim.CausarDano(null, e, 40f, (TipoDano)t, false); // 40 < 50% de 100: sem overkill
                    EventoSim morte;
                    Afirmar(Achar(sim, TipoEvento.Morte, out morte) && !morte.Overkill && morte.Condicao == esperado[t], "evento de morte " + (TipoDano)t);
                    Afirmar(sim.Cadaveres.Count == 1 && sim.Cadaveres[0].Condicao == esperado[t], (TipoDano)t + " -> " + esperado[t]);
                    Afirmar(Tem(sim, TipoEvento.CadaverCriado), "evento CadaverCriado");
                }
            });
            Teste("overkill (golpe final > 50% da vida máxima): explode, sem cadáver, ossos extras", () =>
            {
                var sim = Arena(1, null, 1, false);
                var a = Boneco(sim, 100f, 100f, new Vec2(10, 6));
                sim.CausarDano(null, a, 50f, TipoDano.Fisico, false);
                Afirmar(a.Viva, "50 de dano não mata 100 de vida");
                sim.CausarDano(null, a, 50f, TipoDano.Fisico, false); // exatamente 50%: não é overkill
                Afirmar(!a.Viva && sim.Cadaveres.Count == 1 && sim.Resultado.Overkills == 0, "50% exato deixa cadáver");
                long ossosNormais = sim.Resultado.Ossos;
                sim.Eventos.Clear();
                var b = Boneco(sim, 100f, 100f, new Vec2(11, 6));
                b.Vida = 20f;
                sim.CausarDano(sim.Necromante, b, 51f / (1f - sim.MitigacaoDe(b, TipoDano.Sombra)), TipoDano.Sombra, false);
                EventoSim morte;
                Afirmar(Achar(sim, TipoEvento.Morte, out morte) && morte.Overkill && !morte.Execucao, "morte deveria ser overkill");
                Afirmar(morte.Dir.X > 0.9f, "direção do golpe deveria apontar para longe do necromante");
                Afirmar(sim.Cadaveres.Count == 1 && !Tem(sim, TipoEvento.CadaverCriado), "overkill não deixa cadáver");
                Afirmar(sim.Resultado.Ossos - ossosNormais > ossosNormais && sim.Resultado.Overkills == 1, "ossos extras no overkill");
            });
            Teste("execução: elite/chefe < 10% de vida, 25% por golpe do necromante; cadáver Intacto; +Sangue", () =>
            {
                var sim = Arena(9, null, 1, false);
                var m = PrimeiroMinion(sim); // Esqueleto é Pequeno: não executa
                int n = 6000, exec = 0, intactos = 0;
                for (int i = 0; i < n; i++)
                {
                    var e = Boneco(sim, 1e6f, 1e6f * 0.05f, new Vec2(10, 6), true);
                    sim.CausarDano(sim.Necromante, e, 1f, TipoDano.Sombra, true);
                    if (!e.Viva)
                    {
                        exec++;
                        if (sim.Cadaveres.Count == 1 && sim.Cadaveres[0].Condicao == CondicaoCadaver.Intacto) intactos++;
                    }
                    sim.Cadaveres.Clear(); sim.Orbes.Clear(); sim.Entidades.Remove(e); sim.Eventos.Clear();
                }
                Perto(exec / (float)n, 0.25f, 0.02f, "taxa de execução");
                Afirmar(intactos == exec && sim.Resultado.Execucoes == exec, "toda execução deixa cadáver Intacto");
                Afirmar(sim.Resultado.Sangue == exec * (Cfg.SangueElite + Cfg.SangueExecucao), "Sangue de elite + execução");
                for (int i = 0; i < 300; i++)
                {
                    var comum = Boneco(sim, 1e6f, 1e6f * 0.05f, new Vec2(10, 6));
                    sim.CausarDano(sim.Necromante, comum, 1f, TipoDano.Sombra, true);
                    Afirmar(comum.Viva, "inimigo comum não pode ser executado");
                    var elite = Boneco(sim, 1e6f, 1e6f * 0.05f, new Vec2(10, 6), true);
                    sim.CausarDano(m, elite, 1f, TipoDano.Fisico, true);
                    Afirmar(elite.Viva, "minion Pequeno não executa");
                    var cheio = Boneco(sim, 1e6f, 1e6f * 0.5f, new Vec2(10, 6), true);
                    sim.CausarDano(sim.Necromante, cheio, 1f, TipoDano.Sombra, true);
                    Afirmar(cheio.Viva, "acima de 10% não executa");
                    sim.Entidades.Remove(comum); sim.Entidades.Remove(elite); sim.Entidades.Remove(cheio); sim.Eventos.Clear();
                }
                m.Porte = Porte.Grande; int execMinion = 0;
                for (int i = 0; i < 2000; i++)
                {
                    var e = Boneco(sim, 1e6f, 1e6f * 0.05f, new Vec2(10, 6), false, true);
                    sim.CausarDano(m, e, 1f, TipoDano.Fisico, true);
                    if (!e.Viva) execMinion++;
                    sim.Cadaveres.Clear(); sim.Orbes.Clear(); sim.Entidades.Remove(e); sim.Eventos.Clear();
                }
                Perto(execMinion / 2000f, 0.25f, 0.03f, "execução por minion Grande em chefe");
            });
            Teste("limite de 30 cadáveres na arena", () =>
            {
                var sim = Arena(1, null, 1, false);
                for (int i = 0; i < 45; i++)
                {
                    var e = Boneco(sim, 100f, 10f, new Vec2(5 + i * 0.2f, 6));
                    sim.CausarDano(null, e, 20f, TipoDano.Gelo, false);
                }
                Afirmar(sim.Cadaveres.Count == 30, "cadáveres = " + sim.Cadaveres.Count);
                Afirmar(sim.Resultado.InimigosMortos == 45 && sim.Resultado.Ossos >= 45, "todo inimigo morto rende Ossos");
            });
            Teste("Rancoroso explode ao morrer; elite Explosivo fere o lado do jogador", () =>
            {
                var p = ProgressoCom(EspecieId.Esqueleto);
                p.Minions[0].Alma.Afixos.Add(Afixo.Rancoroso);
                var sim = Arena(1, p, 1, false);
                var m = PrimeiroMinion(sim);
                var ini = Boneco(sim, 1000f, 1000f, new Vec2(m.Pos.X + 1f, m.Pos.Y));
                sim.CausarDano(null, m, m.VidaMax * 0.4f, TipoDano.Fisico, false);
                sim.CausarDano(null, m, m.VidaMax, TipoDano.Fisico, false);
                sim.Tick();
                Afirmar(Tem(sim, TipoEvento.Explosao) && ini.Vida < 1000f, "Rancoroso deveria ferir o inimigo vizinho");

                var sim2 = Arena(1, null, 1, false);
                var m2 = PrimeiroMinion(sim2);
                float antes = m2.Vida;
                var elite = sim2.CriarInimigo(EspecieId.Esqueleto, new Vec2(m2.Pos.X + 1f, m2.Pos.Y), true, ModificadorElite.Explosivo);
                sim2.CausarDano(null, elite, elite.VidaMax * 0.4f, TipoDano.Fisico, false);
                sim2.CausarDano(null, elite, elite.VidaMax * 0.4f, TipoDano.Fisico, false);
                sim2.CausarDano(null, elite, elite.VidaMax * 0.4f, TipoDano.Fisico, false);
                sim2.NecromanteAtivo = false; sim2.Tick();
                Afirmar(!elite.Viva && m2.Vida < antes, "elite Explosivo deveria ferir o minion vizinho");
            });

            Console.WriteLine("- Elites, andares e chefe");
            Teste("elites: 1 a 3 modificadores distintos; Escudo de Ossos absorve; Veloz acelera", () =>
            {
                var sim = Arena(4, null, 9, false);
                var vistos = new int[4];
                for (int i = 0; i < 3000; i++)
                {
                    int bits = 0; int m = (int)sim.SortearModificadores();
                    for (int b = 0; b < 4; b++) if ((m & (1 << b)) != 0) bits++;
                    Afirmar(bits >= 1 && bits <= 3, "modificadores = " + bits);
                    vistos[bits]++;
                }
                Afirmar(vistos[1] > 0 && vistos[2] > 0 && vistos[3] > 0, "deveria sortear 1, 2 e 3 modificadores");
                var e = sim.CriarInimigo(EspecieId.Esqueleto, new Vec2(10, 6), true, ModificadorElite.EscudoDeOssos | ModificadorElite.Veloz);
                var comum = sim.CriarInimigo(EspecieId.Esqueleto, new Vec2(10, 7));
                Perto(e.VidaMax, comum.VidaMax * Cfg.EliteMultVida, 1e-2f, "vida de elite");
                Perto(e.VelMovimento, comum.VelMovimento * 1.4f, 1e-4f, "Veloz");
                Perto(e.Escudo, e.VidaMax * Cfg.EscudoDeOssosFracao, 1e-3f, "escudo inicial");
                float vida = e.Vida;
                sim.CausarDano(null, e, 5f, TipoDano.Gelo, false);
                Afirmar(e.Vida == vida && e.Escudo < e.VidaMax * Cfg.EscudoDeOssosFracao, "escudo deveria absorver");
            });
            Teste("Vampírico cura o elite ao causar dano", () =>
            {
                var sim = Arena(4, null, 1, false);
                var e = sim.CriarInimigo(EspecieId.Esqueleto, new Vec2(10, 6), true, ModificadorElite.Vampirico);
                e.VidaMax = 1000f; e.Vida = 500f; float antes = e.Vida;   // vida folgada: a cura não pode bater no teto
                var m = PrimeiroMinion(sim); m.Armadura = 0f; m.Resistencias[0] = 0f; m.VidaMax = 1000f; m.Vida = 1000f;
                float d = sim.CausarDano(e, m, 100f, TipoDano.Fisico, false);
                Perto(e.Vida - antes, d * Cfg.VampiricoRoubo, 1e-2f, "roubo de vida");
            });
            Teste("andar = 3 ondas + ~30% de onda extra de elite; andar 10 é o chefe", () =>
            {
                int comElite = 0, n = 4000;
                for (int i = 0; i < n; i++)
                {
                    var s = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), (ulong)(1000 + i), ModoExpedicao.Cacar, 2);
                    Afirmar(s.TotalDeOndas == 3 || s.TotalDeOndas == 4, "ondas = " + s.TotalDeOndas);
                    if (s.TotalDeOndas == 4) comElite++;
                }
                Perto(comElite / (float)n, 0.30f, 0.025f, "chance de onda de elite");
                var sc = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 1, ModoExpedicao.Cacar, 10);
                Afirmar(sc.TotalDeOndas == 1, "andar de chefe tem uma onda");
                Entidade chefe = null;
                for (int t = 0; t < 120 && chefe == null; t++)
                {
                    sc.Tick();
                    for (int i = 0; i < sc.Entidades.Count; i++) if (sc.Entidades[i].Chefe) chefe = sc.Entidades[i];
                }
                Afirmar(chefe != null && chefe.Especie == EspecieId.AbadessaCosturada, "a Abadessa deveria surgir no andar 10");
            });
            Teste("onda de elite traz um elite; ondas respeitam o andar mínimo das espécies", () =>
            {
                bool viuElite = false;
                for (ulong s = 1; s < 40 && !viuElite; s++)
                {
                    var sim = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), s, ModoExpedicao.Cacar, 1);
                    sim.Necromante.VidaMax = 1e7f; sim.Necromante.Vida = 1e7f;
                    if (sim.TotalDeOndas != 4) continue;
                    for (int t = 0; t < 4000 && !sim.Encerrada && !viuElite; t++)
                    {
                        sim.Tick();
                        for (int i = 0; i < sim.Eventos.Count; i++)
                        {
                            var ev = sim.Eventos[i];
                            if (ev.Tipo != TipoEvento.EntidadeSurgiu) continue;
                            Afirmar(ev.Especie == EspecieId.Esqueleto || ev.Especie == EspecieId.Zumbi, "andar 1 só tem Esqueleto e Zumbi, veio " + ev.Especie);
                            if (ev.Extra != 0) { viuElite = true; Afirmar(sim.Onda == 4, "elite fora da onda extra"); }
                        }
                    }
                }
                Afirmar(viuElite, "nenhuma onda de elite observada");
            });
            Teste("Abadessa invoca poças de sangue que a curam", () =>
            {
                var sim = Arena(2, new EstadoProgresso(), 10, false);
                var chefe = sim.CriarInimigo(EspecieId.AbadessaCosturada, new Vec2(15, 6), false, ModificadorElite.Nenhum, true);
                chefe.Vida = chefe.VidaMax * 0.5f;
                sim.Necromante.VidaMax = 1e7f; sim.Necromante.Vida = 1e7f;
                bool poca = false; float antes = chefe.Vida;
                for (int t = 0; t < 120; t++) { sim.Tick(); if (Tem(sim, TipoEvento.PocaCriada)) poca = true; }
                Afirmar(poca && sim.Pocas.Count >= 1, "deveria invocar poça");
                Afirmar(chefe.Vida > antes, "poça deveria curar o chefe");
            });
            Teste("Ponto de Ancoragem a cada 5 andares; Restos no fim do andar; ancoragem aplicada ao progresso", () =>
            {
                var p = ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Zumbi, EspecieId.CaoDeCripta);
                for (int i = 0; i < p.Minions.Count; i++) { p.Minions[i].Nivel = 20; p.Minions[i].Alma.Qualidade = Qualidade.Ancestral; }
                var sim = new Simulacao(Cfg, p, 3, ModoExpedicao.Avancar, 4);
                sim.LimiteDeAndares = 2;
                bool ancoragem = false; long restos = 0;
                while (!sim.Encerrada)
                {
                    sim.Tick();
                    EventoSim ev;
                    if (Achar(sim, TipoEvento.AncoragemAlcancada, out ev)) { ancoragem = true; Afirmar(ev.Extra == 6, "ancoragem deveria apontar para o andar 6"); }
                }
                for (int i = 0; i < 7; i++) restos += sim.Resultado.Restos[i];
                Afirmar(sim.Resultado.Motivo == MotivoFim.LimiteDeAndares && sim.Resultado.AndaresConcluidos == 2, "deveria concluir os andares 4 e 5");
                Afirmar(ancoragem && sim.Resultado.AncoragemAlcancada == 6, "ancoragem não registrada");
                Afirmar(restos > 0 && sim.Cadaveres.Count == 0, "cadáveres não usados deveriam virar Restos");
                Progressao.AplicarResultado(p, sim.Resultado, Cfg);
                Afirmar(p.AndarDeAncoragem == 6 && p.MaiorAndarConcluido == 5 && p.Ossos > 0, "progresso não atualizado");
            });
            Teste("modo Caçar repete o mesmo andar; modo Avançar sobe", () =>
            {
                var forte = ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Zumbi);
                for (int i = 0; i < forte.Minions.Count; i++) forte.Minions[i].Nivel = 20;
                var r = ExecutorSemTela.RodarExpedicao(Cfg, forte, 5, ModoExpedicao.Cacar, 2, 3);
                Afirmar(r.AndaresConcluidos == 3 && r.AndaresRegistrados[0] == 2 && r.AndaresRegistrados[2] == 2 && r.MaiorAndarConcluido == 2, "Caçar deveria repetir o andar 2");
                var r2 = ExecutorSemTela.RodarExpedicao(Cfg, forte, 5, ModoExpedicao.Avancar, 2, 3);
                Afirmar(r2.AndaresConcluidos == 3 && r2.AndaresRegistrados[2] == 4, "Avançar deveria chegar ao andar 4");
                var um = ExecutorSemTela.RodarAndar(Cfg, forte, 5, 1);
                Afirmar(um.AndaresConcluidos == 1 && um.InimigosMortos > 0 && um.Ossos > 0, "RodarAndar");
            });

            Console.WriteLine("- Ordens e feitiços do necromante");
            Teste("ordem 'SE vida < 50% ENTÃO Drenar Vida' só dispara quando a condição vale", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Minions.Clear();
                p.Ordens.Add(new Ordem(CondicaoOrdem.VidaAbaixoDe, 50f, AcaoNecromante.DrenarVida));
                var sim = Arena(1, p);
                Boneco(sim, 1e6f, 1e6f, new Vec2(6, 6));
                bool lancou = false;
                for (int t = 0; t < 50; t++) { sim.Tick(); EventoSim ev; if (Achar(sim, TipoEvento.FeiticoLancado, out ev)) lancou = true; }
                Afirmar(!lancou, "não deveria lançar com vida cheia");
                Afirmar(sim.Resultado.Ticks == 50 && sim.Necromante.Recarga > 0f, "rede de segurança: ataque básico");
                sim.Necromante.Vida = sim.Necromante.VidaMax * 0.3f;
                sim.Tick();
                EventoSim f;
                Afirmar(Achar(sim, TipoEvento.FeiticoLancado, out f) && f.Extra == (int)FeiticoId.DrenarVida, "deveria lançar Drenar Vida");
                Afirmar(sim.Necromante.Vida > sim.Necromante.VidaMax * 0.3f, "Drenar Vida deveria curar");
                Afirmar(sim.Necromante.Mana < sim.Necromante.ManaMax && sim.RecargaDoFeitico(FeiticoId.DrenarVida) > 0f, "custo de mana e recarga");
            });
            Teste("ordens são avaliadas de cima para baixo e limitadas aos espaços (3 iniciais, até 8)", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Ordens.Add(new Ordem(CondicaoOrdem.ChefePresente, 0f, AcaoNecromante.MaldicaoDaFraqueza)); // falsa
                p.Ordens.Add(new Ordem(CondicaoOrdem.InimigosPeloMenos, 1f, AcaoNecromante.LancaDeOssos));    // verdadeira
                p.Ordens.Add(new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.DrenarVida));                 // nunca chega (no 1º tick)
                p.Ordens.Add(new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.Sacrificio));                 // 4ª: fora dos 3 espaços
                var sim = Arena(1, p);
                Boneco(sim, 1e6f, 1e6f, new Vec2(8, 6));
                sim.Tick();
                EventoSim f;
                Afirmar(Achar(sim, TipoEvento.FeiticoLancado, out f) && f.Extra == (int)FeiticoId.LancaDeOssos, "primeira ordem válida deveria ser a Lança");
                for (int t = 0; t < 300; t++) { sim.Tick(); if (Achar(sim, TipoEvento.FeiticoLancado, out f)) Afirmar(f.Extra != (int)FeiticoId.Sacrificio, "4ª ordem não deveria existir com 3 espaços"); }
                Afirmar(ContarMinionsVivos(sim) == 3, "nenhum minion deveria ser sacrificado");
                Afirmar(Cfg.EspacosDeOrdemIniciais == 3 && Cfg.EspacosDeOrdemMaximos == 8, "espaços de ordem");
            });
            Teste("Lança de Ossos atravessa inimigos em linha", () =>
            {
                var p = Progressao.NovoJogo(Cfg); p.Minions.Clear();
                p.Ordens.Add(new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.LancaDeOssos));
                var sim = Arena(1, p);
                float y = sim.Necromante.Pos.Y;
                var a = Boneco(sim, 1e5f, 1e5f, new Vec2(6, y)); var b = Boneco(sim, 1e5f, 1e5f, new Vec2(9, y)); var c = Boneco(sim, 1e5f, 1e5f, new Vec2(12, y));
                var fora = Boneco(sim, 1e5f, 1e5f, new Vec2(9, y + 3f));
                sim.Tick();
                Afirmar(sim.Projeteis.Count == 1 && sim.Projeteis[0].Perfurante, "a lança deveria estar em voo");
                Afirmar(c.Vida == 1e5f, "o inimigo mais distante não deveria ser atingido no primeiro tick");
                sim.NecromanteAtivo = false;   // só a primeira lança
                for (int t = 0; t < 12 && sim.Projeteis.Count > 0; t++) sim.Tick();
                Afirmar(sim.Projeteis.Count == 0, "a lança deveria terminar o voo");
                Afirmar(a.Vida < 1e5f && b.Vida < 1e5f && c.Vida < 1e5f, "os três em linha deveriam ser atingidos");
                Afirmar(fora.Vida == 1e5f, "inimigo fora da linha não deveria ser atingido");
            });
            Teste("Explosão de Cadáver consome cadáveres e fere em área", () =>
            {
                var p = Progressao.NovoJogo(Cfg); p.Minions.Clear();
                p.Ordens.Add(new Ordem(CondicaoOrdem.CadaveresPeloMenos, 2f, AcaoNecromante.ExplosaoDeCadaver));
                var sim = Arena(1, p);
                sim.NecromanteAtivo = false;
                for (int i = 0; i < 2; i++) { var morto = Boneco(sim, 100f, 10f, new Vec2(10 + i * 0.5f, 6)); sim.CausarDano(null, morto, 20f, TipoDano.Fisico, false); }
                var perto = Boneco(sim, 1e5f, 1e5f, new Vec2(10.5f, 6.5f)); var longe = Boneco(sim, 1e5f, 1e5f, new Vec2(17, 2));
                Afirmar(sim.Cadaveres.Count == 2, "preparação");
                sim.NecromanteAtivo = true;
                sim.Tick();
                Afirmar(sim.Cadaveres.Count == 0 && Tem(sim, TipoEvento.CadaverConsumido), "cadáveres deveriam ser consumidos");
                Afirmar(perto.Vida < 1e5f && longe.Vida == 1e5f, "dano só na área");
            });
            Teste("Reerguer traz o minion morto de volta consumindo o cadáver dele", () =>
            {
                var sim = Arena(1, null, 1, false);
                var m = PrimeiroMinion(sim);
                sim.CausarDano(null, m, m.VidaMax * 0.45f, TipoDano.Fisico, false);
                sim.CausarDano(null, m, m.VidaMax * 0.45f, TipoDano.Fisico, false);
                sim.CausarDano(null, m, m.VidaMax * 0.45f, TipoDano.Fisico, false);
                Afirmar(!m.Viva && sim.Cadaveres.Count == 1 && sim.Cadaveres[0].DeMinion, "minion morto deveria virar cadáver");
                Afirmar(sim.CondicaoVerdadeira(new Ordem(CondicaoOrdem.MinionMortoDeCustoMinimo, 1f, AcaoNecromante.Reerguer)), "condição 'minion de custo >= 1 morreu'");
                Afirmar(!sim.CondicaoVerdadeira(new Ordem(CondicaoOrdem.MinionMortoDeCustoMinimo, 3f, AcaoNecromante.Reerguer)), "custo >= 3 deveria ser falso");
                sim.NecromanteAtivo = true;
                bool reergueu = false;
                for (int t = 0; t < 5; t++) { sim.Tick(); if (Tem(sim, TipoEvento.MinionReerguido)) reergueu = true; }
                Afirmar(reergueu && m.Viva && sim.Cadaveres.Count == 0, "Reerguer falhou");
                Perto(m.Vida, m.VidaMax * 0.5f, 1e-2f, "volta com 50% da vida");
                Afirmar(ContarMinionsVivos(sim) == 3, "3 minions vivos de novo");
                int naLista = 0; for (int i = 0; i < sim.Entidades.Count; i++) if (sim.Entidades[i] == m) naLista++;
                Afirmar(naLista == 1, "minion duplicado na lista de entidades");
            });
            Teste("Sacrifício destrói um minion e dá +50% de dano aos outros por 8 s", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Ordens.Add(new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.Sacrificio));
                var sim = Arena(1, p);
                var alvo = Boneco(sim, 1e7f, 1e7f, new Vec2(18, 6));
                sim.Tick();
                Afirmar(ContarMinionsVivos(sim) == 2 && sim.Cadaveres.Count == 0, "um minion destruído, sem cadáver");
                Perto(sim.SacrificioTempo, 8f, 0.11f, "duração");
                Entidade vivo = null;
                for (int i = 0; i < sim.Entidades.Count; i++) if (sim.Entidades[i].Faccao == Faccao.Minion && sim.Entidades[i].Viva) vivo = sim.Entidades[i];
                Perto(sim.CausarDano(vivo, alvo, 100f, TipoDano.Gelo, false), 150f, 1e-2f, "+50% de dano");
                for (int t = 0; t < 85; t++) sim.Tick();
                Afirmar(sim.SacrificioTempo <= 0f, "bônus deveria acabar");
                Perto(sim.CausarDano(vivo, alvo, 100f, TipoDano.Gelo, false), 100f, 1e-2f, "dano normal depois");
            });
            Teste("Maldição da Fraqueza é lançada com chefe presente e reduz o dano inimigo em 20%", () =>
            {
                var p = Progressao.NovoJogo(Cfg); p.Minions.Clear();
                var sim = Arena(1, p, 10);
                var chefe = Boneco(sim, 1e7f, 1e7f, new Vec2(9, 6), false, true);
                chefe.TempoHabilidade = 999f;
                bool lancou = false;
                for (int t = 0; t < 5 && !lancou; t++) { sim.Tick(); EventoSim f; if (Achar(sim, TipoEvento.FeiticoLancado, out f) && f.Extra == (int)FeiticoId.MaldicaoDaFraqueza) lancou = true; }
                Afirmar(lancou && chefe.FraquezaTempo > 7f, "maldição deveria ser aplicada ao chefe");
                var n = sim.Necromante; n.Armadura = 0f; for (int i = 0; i < 6; i++) n.Resistencias[i] = 0f; n.VidaMax = 1e6f; n.Vida = 1e6f;
                Perto(sim.CausarDano(chefe, n, 100f, TipoDano.Gelo, false), 80f, 1e-2f, "-20% de dano");
            });

            Console.WriteLine("- IA dos papéis");
            Teste("Tanque provoca o inimigo mais próximo do necromante; Suporte cura o aliado mais ferido", () =>
            {
                var p = ProgressoCom(EspecieId.Zumbi);
                var sim = Arena(1, p, 1, false);
                var tanque = PrimeiroMinion(sim);
                var pertoDoMestre = Boneco(sim, 1e6f, 1e6f, new Vec2(sim.Necromante.Pos.X + 1f, sim.Necromante.Pos.Y + 2f));
                var pertoDoTanque = Boneco(sim, 1e6f, 1e6f, new Vec2(tanque.Pos.X + 1.2f, tanque.Pos.Y));
                bool provocou = false;
                for (int t = 0; t < 100 && !provocou; t++) { sim.Tick(); provocou = pertoDoMestre.ProvocadoPor == tanque.Id; }
                Afirmar(provocou, "tanque deveria ir no inimigo mais próximo do necromante e provocá-lo");
                Afirmar(pertoDoTanque.Vida == 1e6f, "tanque não deveria ter atacado o inimigo mais próximo de si");

                var p2 = ProgressoCom(EspecieId.AbadessaCosturada);
                var sim2 = Arena(1, p2, 1, false);
                sim2.Necromante.Vida = sim2.Necromante.VidaMax * 0.5f;
                bool curou = false;
                for (int t = 0; t < 30 && !curou; t++) { sim2.Tick(); EventoSim c; curou = Achar(sim2, TipoEvento.Cura, out c) && c.Alvo == sim2.Necromante.Id; }
                Afirmar(curou, "Suporte deveria curar o necromante ferido");
            });
            Teste("Atirador escolhe o de menor vida; Conjurador acerta em área; posturas limitam o alvo", () =>
            {
                var sim = Arena(1, ProgressoCom(EspecieId.ArqueiroOssudo), 1, false);
                var arq = PrimeiroMinion(sim);
                var cheio = Boneco(sim, 1e5f, 1e5f, new Vec2(arq.Pos.X + 3f, arq.Pos.Y));
                var fraco = Boneco(sim, 1e5f, 500f, new Vec2(arq.Pos.X + 5f, arq.Pos.Y + 1f));
                for (int t = 0; t < 20; t++) sim.Tick();
                Afirmar(fraco.Vida < 500f && cheio.Vida == 1e5f, "Atirador deveria focar o de menor vida");

                var sim2 = Arena(1, ProgressoCom(EspecieId.AcolitoCaido), 1, false);
                var aco = PrimeiroMinion(sim2);
                var a = Boneco(sim2, 1e5f, 1e5f, new Vec2(aco.Pos.X + 4f, aco.Pos.Y)); var b = Boneco(sim2, 1e5f, 1e5f, new Vec2(aco.Pos.X + 4.5f, aco.Pos.Y + 0.8f));
                var c = Boneco(sim2, 1e5f, 1e5f, new Vec2(aco.Pos.X + 4f, aco.Pos.Y - 0.9f)); var sozinho = Boneco(sim2, 1e5f, 1e5f, new Vec2(aco.Pos.X + 1f, aco.Pos.Y + 4.5f));
                sim2.Tick(); sim2.Tick();
                Afirmar(a.Vida < 1e5f && b.Vida < 1e5f && c.Vida < 1e5f && sozinho.Vida == 1e5f, "Conjurador deveria mirar o grupo de 3");

                var pe = ProgressoCom(EspecieId.Esqueleto);
                pe.Minions[0].Grupo = 2; // grupo 2 = Escolta
                var sim3 = Arena(1, pe, 1, false);
                var esc = PrimeiroMinion(sim3);
                Boneco(sim3, 1e5f, 1e5f, new Vec2(18, 6));
                Vec2 pos0 = esc.Pos;
                for (int t = 0; t < 30; t++) sim3.Tick();
                Afirmar(esc.Postura == Postura.Escolta && Vec2.Dist(esc.Pos, pos0) < 0.01f, "Escolta não deveria perseguir inimigo distante");
                var pa = ProgressoCom(EspecieId.Esqueleto);
                var sim4 = Arena(1, pa, 1, false);
                var agr = PrimeiroMinion(sim4);
                Boneco(sim4, 1e5f, 1e5f, new Vec2(18, 6));
                for (int t = 0; t < 15; t++) sim4.Tick();
                Afirmar(agr.Postura == Postura.Agressiva && agr.Pos.X > pos0.X + 2f && agr.Animacao == EstadoAnimacao.Andando, "Agressiva deveria avançar: " + agr.Postura + " " + agr.Pos + " " + agr.Animacao);
            });

            Console.WriteLine("- Filactério e progresso");
            Teste("Filactério: 20 espaços, descarte automático vira Essência, espécie nova nunca é descartada", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.DissolverAbaixoDe = Qualidade.Comum;
                Afirmar(Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Fraca }, Cfg) == DestinoDaAlma.Dissolvida, "Fraca conhecida deveria dissolver");
                Afirmar(p.Essencia == Cfg.EssenciaPorQualidade[0] && p.Filacterio.Count == 0, "Essência da dissolução");
                Afirmar(Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Fraca }, Cfg) == DestinoDaAlma.Guardada, "espécie nova (mesmo Fraca) nunca é descartada");
                Afirmar(p.EspeciesConhecidas.Contains(EspecieId.Zumbi), "espécie registrada");
                for (int i = 0; i < 19; i++) Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Cfg);
                Afirmar(p.Filacterio.Count == 20, "deveria estar cheio");
                Afirmar(Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Cfg) == DestinoDaAlma.GuardadaDissolvendoOutra, "cheio: melhor que a pior substitui a pior");
                bool temZumbiFraco = false; for (int i = 0; i < p.Filacterio.Count; i++) if (p.Filacterio[i].Especie == EspecieId.Zumbi) temZumbiFraco = true;
                Afirmar(!temZumbiFraco && p.Filacterio.Count == 20, "a pior (Zumbi Fraca) deveria ter sido dissolvida");
                long ess = p.Essencia;
                Afirmar(Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Cfg) == DestinoDaAlma.Dissolvida, "cheio: alma igual à pior é dissolvida");
                Afirmar(p.Filacterio.Count == 20 && p.Essencia == ess + Cfg.EssenciaPorQualidade[1], "continua com 20 e ganha Essência");
                Afirmar(Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Ancestral }, Cfg) == DestinoDaAlma.GuardadaDissolvendoOutra, "cheio: Ancestral substitui uma Comum");
                Afirmar(Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Diabrete, Qualidade = Qualidade.Fraca }, Cfg) == DestinoDaAlma.GuardadaDissolvendoOutra, "cheio: espécie nova entra mesmo sendo a pior");
                Afirmar(p.Filacterio.Count == 20 && p.Filacterio[19].Especie == EspecieId.Diabrete, "Diabrete guardado");
                p.Essencia = 1000;
                var m = Progressao.ErguerMinion(p, 19, Cfg);
                Afirmar(p.Filacterio.Count == 19 && m.Alma.Especie == EspecieId.Diabrete && m.Ativo, "erguer minion a partir da alma");
            });
            Teste("progresso é um POCO serializável (ida e volta em JSON)", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Ordens.Add(new Ordem(CondicaoOrdem.CadaveresPeloMenos, 3f, AcaoNecromante.ExplosaoDeCadaver));
                p.Filacterio.Add(GeradorDeAlmas.Gerar(new Rng(1), Cfg, EspecieId.Diabrete, Qualidade.Profana));
                p.Restos[(int)CondicaoCadaver.Queimado] = 7; p.Ossos = 123; p.ChefesDerrotados.Add(1);
                var opcoes = new JsonSerializerOptions { IncludeFields = true };
                string json = JsonSerializer.Serialize(p, opcoes);
                var q = JsonSerializer.Deserialize<EstadoProgresso>(json, opcoes);
                Afirmar(JsonSerializer.Serialize(q, opcoes) == json, "ida e volta mudou o conteúdo");
                Afirmar(q.Minions.Count == 3 && q.Filacterio[0].Afixos.Count == 3 && q.Filacterio[0].Traco != TracoProfano.Nenhum && q.Ordens[0].Parametro == 3f && q.Restos[2] == 7, "campos perdidos");
                var a = ExecutorSemTela.RodarExpedicao(Cfg, p, 8, ModoExpedicao.Avancar, 1); var b = ExecutorSemTela.RodarExpedicao(Cfg, q, 8, ModoExpedicao.Avancar, 1);
                Afirmar(a.Ticks == b.Ticks && a.Ossos == b.Ossos, "progresso recarregado deveria simular igual");
            });
            Teste("a simulação não altera o progresso; progresso offline aplica resultados", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var opcoes = new JsonSerializerOptions { IncludeFields = true };
                string antes = JsonSerializer.Serialize(p, opcoes);
                ExecutorSemTela.RodarExpedicao(Cfg, p, 3, ModoExpedicao.Avancar, 1);
                Afirmar(JsonSerializer.Serialize(p, opcoes) == antes, "RodarExpedicao mexeu no progresso");
                var resumo = ExecutorSemTela.SimularOffline(Cfg, p, 1800f, ModoExpedicao.Avancar);
                Perto(resumo.SegundosSimulados, 1800f, 1f, "tempo simulado");
                Afirmar(resumo.Expedicoes >= 2 && p.Ossos >= resumo.Ossos && p.Ossos <= resumo.Ossos * 1.5 && p.Ossos > 0 && p.MaiorAndarConcluido == resumo.MaiorAndar && p.NivelNecromante > 1, "resultados não aplicados");
                Afirmar(p.Filacterio.Count + (int)p.Essencia >= 0 && p.ContadorDeSementes == (ulong)resumo.Expedicoes + 1, "contador de sementes");
            });

            Teste("masmorra: salas especiais aparecem, resolvem e o andar continua", () =>
            {
                var cfg = new ConfigJogo { ChanceDeSalaEspecial = 1f };
                int salas = 0, resolvidas = 0, baus = 0; bool explorou = false;
                for (ulong s = 1; s <= 12; s++)
                {
                    var p = Progressao.NovoJogo(cfg); p.PoderNecromantico = 6f;
                    var sim = new Simulacao(cfg, p, s, ModoExpedicao.Avancar, 2) { LimiteDeAndares = 2 };
                    int guarda = 0;
                    while (!sim.Encerrada && guarda++ < 20000)
                    {
                        sim.Tick();
                        if (sim.Fase == FaseExpedicao.Explorando) explorou = true;
                        foreach (var ev in sim.Eventos) { if (ev.Tipo == TipoEvento.SalaIniciada) salas++; if (ev.Tipo == TipoEvento.SalaResolvida) resolvidas++; }
                    }
                    baus += sim.Resultado.BausAbertos;
                    Afirmar(sim.Resultado.Motivo == MotivoFim.LimiteDeAndares || sim.Resultado.Motivo == MotivoFim.NecromanteMorreu, "expedição travou: " + sim.Resultado.Motivo + " fase " + sim.Fase);
                }
                Afirmar(explorou && salas >= 24 && resolvidas >= salas - 2 && baus > 0, "salas " + salas + " resolvidas " + resolvidas + " baús " + baus);
            });
            Teste("masmorra: nível secreto forçado solta a horda e paga o tesouro", () =>
            {
                var p = Progressao.NovoJogo(Cfg); p.PoderNecromantico = 40f;
                var cfgS = new ConfigJogo { ForcarSecreto = 1 };
                var sim = new Simulacao(cfgS, p, 5, ModoExpedicao.Avancar, 1) { LimiteDeAndares = 1, ForcarNivelSecreto = true };
                int guarda = 0; bool viu = false;
                while (!sim.Encerrada && guarda++ < 30000) { sim.Tick(); if (sim.EmNivelSecreto) viu = true; }
                bool pasto = sim.Resultado.MortesPorEspecie[(int)EspecieId.ResProfana] >= 15 && sim.Resultado.Itens.Count >= 2, camara = sim.Resultado.ElitesMortos >= 3 && sim.Resultado.Itens.Count >= 3;
                Afirmar(viu && sim.Resultado.NiveisSecretos == 1 && (pasto || camara), "secreto " + sim.Resultado.NiveisSecretos + " reses " + sim.Resultado.MortesPorEspecie[(int)EspecieId.ResProfana] + " itens " + sim.Resultado.Itens.Count);
            });
            Teste("masmorra: a Cripta dos Espelhos solta um reflexo de cada minion e paga o tesouro", () =>
            {
                var cfgE = new ConfigJogo { ForcarSecreto = 3 };
                var p = Progressao.NovoJogo(cfgE); p.PoderNecromantico = 40f;
                var sim = new Simulacao(cfgE, p, 5, ModoExpedicao.Avancar, 1) { LimiteDeAndares = 1, ForcarNivelSecreto = true };
                int guarda = 0; bool viu = false; int reflexos = 0, vivosNaEntrada = -1;
                while (!sim.Encerrada && guarda++ < 30000)
                {
                    bool antes = sim.EmNivelSecreto;
                    sim.Tick();
                    if (sim.EmNivelSecreto && !antes) { viu = true; Afirmar(sim.SecretoAtual == NivelSecreto.CriptaDosEspelhos, "deveria ser a Cripta dos Espelhos"); vivosNaEntrada = 0; foreach (var e in sim.Entidades) if (e.Viva && e.Faccao == Faccao.Minion) vivosNaEntrada++; }
                    if (sim.EmNivelSecreto) foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu) { foreach (var e in sim.Entidades) if (e.Id == ev.Alvo && e.Faccao == Faccao.Inimigo) reflexos++; }
                }
                Afirmar(viu && sim.Resultado.NiveisSecretos == 1, "a cripta não abriu");
                Afirmar(reflexos == Math.Max(1, vivosNaEntrada), "um reflexo por minion vivo: " + reflexos + " para " + vivosNaEntrada);
                Afirmar(sim.Resultado.Itens.Count >= 2, "o tesouro da cripta deveria dar ao menos 2 itens: " + sim.Resultado.Itens.Count);
            });
            Teste("masmorra: a sala do chefe tem ambiente e ele fere os dois lados", () =>
            {
                bool lava = false, dano = false;
                for (ulong s = 1; s <= 30 && !(lava && dano); s++)
                {
                    var p = Progressao.NovoJogo(Cfg); p.PoderNecromantico = 5f;
                    var sim = new Simulacao(Cfg, p, s, ModoExpedicao.Avancar, 10) { LimiteDeAndares = 1 };
                    Afirmar(sim.Ambiente != AmbienteDeChefe.Nenhum, "chefe sem ambiente");
                    if (sim.Ambiente != AmbienteDeChefe.Lava) continue;
                    lava = true; int guarda = 0;
                    while (!sim.Encerrada && guarda++ < 3000) { sim.Tick(); foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.Dano && ev.Origem == -1 && ev.TipoDano == TipoDano.Fogo) dano = true; }
                }
                Afirmar(lava && dano, "lava " + lava + " dano " + dano);
            });

            Teste("masmorra: o mapa tem uma célula por sala, sem sobreposição, e todas são visitadas", () =>
            {
                var cfg = new ConfigJogo { ChanceDeSalaEspecial = 1f, ChanceDeSalaVazia = 1f };
                for (ulong s = 1; s <= 20; s++)
                {
                    var p = Progressao.NovoJogo(cfg); p.PoderNecromantico = 8f;
                    var sim = new Simulacao(cfg, p, s, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 };
                    Afirmar(sim.Mapa.Count == sim.Salas.Count && sim.Salas.Count >= 5, "mapa " + sim.Mapa.Count + " salas " + sim.Salas.Count);
                    var vistos = new System.Collections.Generic.HashSet<long>();
                    for (int i = 0; i < sim.Mapa.Count; i++)
                    {
                        var c = sim.Mapa[i];
                        Afirmar(vistos.Add(c.X * 1000L + c.Y), "duas salas na mesma célula");
                        if (i > 0) Afirmar(c.Pai >= 0 && c.Pai < i && Math.Abs(c.X - sim.Mapa[c.Pai].X) + Math.Abs(c.Y - sim.Mapa[c.Pai].Y) == 1, "sala não é vizinha do pai");
                    }
                    int maior = 0, guarda = 0, total = sim.Salas.Count;
                    while (!sim.Encerrada && guarda++ < 20000) { sim.Tick(); if (sim.SalaAtual > maior) maior = sim.SalaAtual; }
                    Afirmar(sim.Resultado.Motivo != MotivoFim.LimiteDeAndares || maior >= total - 1, "andar concluído sem visitar tudo: " + maior + "/" + total);
                }
            });

            Teste("chefe do Ato 2: o Carcereiro de Almas surge no andar 20, acorrenta minions e chama Aparições", () =>
            {
                Afirmar(TabelaEspecies.ChefeDoAto(1) == EspecieId.AbadessaCosturada && TabelaEspecies.ChefeDoAto(2) == EspecieId.CarcereiroDeAlmas, "chefe por Ato");
                var p = Progressao.NovoJogo(Cfg); p.PoderNecromantico = 400f;
                var sim = new Simulacao(Cfg, p, 3, ModoExpedicao.Avancar, 20) { LimiteDeAndares = 1 };
                bool viu = false, grilhoes = false, aparicao = false; int guarda = 0;
                while (!sim.Encerrada && guarda++ < 6000)
                {
                    sim.Tick();
                    foreach (var ev in sim.Eventos)
                    {
                        if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.CarcereiroDeAlmas && ev.Critico) viu = true;
                        if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 12) grilhoes = true;
                        if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.Aparicao && viu) aparicao = true;
                    }
                    if (viu && grilhoes && aparicao) break;
                }
                Afirmar(viu && grilhoes && aparicao, "chefe " + viu + " grilhões " + grilhoes + " aparição " + aparicao);
            });

            Teste("rota do jogador: a sala escolhida vira a próxima, os inimigos acompanham e o andar ainda visita tudo", () =>
            {
                var cfg = new ConfigJogo { ChanceDeSalaEspecial = 1f, ChanceDeSalaVazia = 1f };
                int trocas = 0;
                for (ulong s = 1; s <= 12; s++)
                {
                    var p = Progressao.NovoJogo(cfg); p.PoderNecromantico = 8f;
                    var sim = new Simulacao(cfg, p, s, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 };
                    int total = sim.Salas.Count, alvo = total - 2;
                    var tipo = sim.Salas[alvo]; var cel = sim.Mapa[alvo];
                    Afirmar(!sim.ComandoIrPara(total - 1), "a última sala não pode ser adiantada");
                    if (sim.ComandoIrPara(alvo)) { trocas++; Afirmar(sim.Salas[1] == tipo && sim.Mapa[1].X == cel.X && sim.Mapa[1].Y == cel.Y && sim.Salas.Count == total, "a sala escolhida deveria ser a próxima"); }
                    // cada onda continua apontando para uma sala de combate, na ordem de visita
                    int ant = -1;
                    foreach (var plano in sim.PlanoDasOndas) { Afirmar(plano.Count > 0, "onda vazia"); int sl = plano[0].Sala; Afirmar(sl > ant && (sim.Salas[sl] == TipoSala.Combate || sim.Salas[sl] == TipoSala.Elite || sim.Salas[sl] == TipoSala.Chefe), "onda fora de ordem ou em sala errada: " + sl); ant = sl; }
                    int maior = 0, guarda = 0;
                    while (!sim.Encerrada && guarda++ < 20000) { sim.Tick(); if (sim.SalaAtual > maior) maior = sim.SalaAtual; }
                    Afirmar(sim.Resultado.Motivo != MotivoFim.LimiteDeAndares || maior >= total - 1, "andar concluído sem visitar tudo");
                }
                Afirmar(trocas >= 8, "trocas " + trocas);
            });

            Teste("cofre trancado: a chave fica numa sala de combate comum; com a chave o cofre paga, sem ela fica fechado", () =>
            {
                int achados = 0, abertos = 0, fechados = 0;
                for (ulong s = 1; s <= 400 && (abertos == 0 || fechados == 0 || achados < 3); s++)
                {
                    var p = Progressao.NovoJogo(Cfg); p.PoderNecromantico = 30f;
                    var sim = new Simulacao(Cfg, p, s, ModoExpedicao.Avancar, 4) { LimiteDeAndares = 1 };
                    int cofre = sim.Salas.IndexOf(TipoSala.Cofre);
                    if (cofre < 0) { Afirmar(sim.ChaveNaSala == -1, "chave sem cofre"); continue; }
                    achados++;
                    Afirmar(sim.ChaveNaSala >= 0 && sim.ChaveNaSala < sim.Salas.Count - 1 && sim.Salas[sim.ChaveNaSala] == TipoSala.Combate, "a chave deveria estar numa sala de combate comum");
                    bool chaveAntes = sim.ChaveNaSala < cofre, pagou = false; int guarda = 0;
                    while (!sim.Encerrada && guarda++ < 20000) { sim.Tick(); foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)TipoSala.Cofre && ev.Origem == 1) pagou = true; }
                    if (sim.Resultado.Motivo != MotivoFim.LimiteDeAndares) continue;
                    Afirmar(pagou == chaveAntes, "cofre " + (pagou ? "abriu" : "fechado") + " com a chave " + (chaveAntes ? "antes" : "depois"));
                    if (pagou) abertos++; else fechados++;
                }
                Afirmar(achados >= 3 && abertos > 0 && fechados > 0, "andares com cofre " + achados + ", abertos " + abertos + ", fechados " + fechados);
            });

            Teste("patrulha errante: surge numa sala de combate, muda de sala, entra na luta e deixa um item", () =>
            {
                var cfg = new ConfigJogo { ChanceDePatrulha = 1f, PatrulhaIntervalo = 2f };
                int surgiu = 0, mudou = 0, lutou = 0, pagou = 0;
                for (ulong s = 1; s <= 10; s++)
                {
                    var p = Progressao.NovoJogo(cfg); p.PoderNecromantico = 60f;
                    var sim = new Simulacao(cfg, p, s, ModoExpedicao.Avancar, 7) { LimiteDeAndares = 1 };
                    if (sim.PatrulhaSala < 0) continue;
                    surgiu++;
                    Afirmar(sim.Patrulha.Count > 0 && (sim.Salas[sim.PatrulhaSala] == TipoSala.Combate || sim.Salas[sim.PatrulhaSala] == TipoSala.Elite), "patrulha fora de sala de combate");
                    int ini = sim.PatrulhaSala, guarda = 0; bool m = false, l = false, i2 = false;
                    while (!sim.Encerrada && guarda++ < 20000)
                    {
                        sim.Tick();
                        if (sim.PatrulhaSala >= 0 && sim.PatrulhaSala != ini) m = true;
                        foreach (var ev in sim.Eventos) { if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 14) l = true; if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 15) i2 = true; }
                    }
                    if (m) mudou++; if (l) lutou++; if (i2) pagou++;
                    if (sim.Resultado.Motivo == MotivoFim.LimiteDeAndares) Afirmar(l && i2 && sim.Resultado.PatrulhasEnfrentadas == 1, "andar limpo sem enfrentar a patrulha");
                }
                Afirmar(surgiu >= 8 && mudou >= 3 && lutou >= 6 && pagou >= 6, "surgiu " + surgiu + " mudou " + mudou + " lutou " + lutou + " pagou " + pagou);
            });

            Teste("habilidades de espécie: o Esqueleto apara o primeiro golpe da sala; o Zumbi solta miasma ao cair", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var sim = new Simulacao(Cfg, p, 3, ModoExpedicao.Avancar, 1);
                bool aparou = false; int guarda = 0;
                while (!sim.Encerrada && guarda++ < 3000 && !aparou) { sim.Tick(); foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 16) aparou = true; }
                Afirmar(aparou, "nenhum esqueleto aparou um golpe");
                var semHab = new ConfigJogo { HabilidadesDeEspecie = false };
                var s2 = new Simulacao(semHab, Progressao.NovoJogo(semHab), 3, ModoExpedicao.Avancar, 1); bool aparou2 = false; guarda = 0;
                while (!s2.Encerrada && guarda++ < 1500) { s2.Tick(); foreach (var ev in s2.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 16) aparou2 = true; }
                Afirmar(!aparou2, "com as habilidades desligadas ninguém deveria aparar");
                var pz = ProgressoCom(EspecieId.Zumbi);
                var sz = new Simulacao(Cfg, pz, 4, ModoExpedicao.Avancar, 9); bool miasma = false; guarda = 0;
                while (!sz.Encerrada && guarda++ < 6000 && !miasma) { sz.Tick(); foreach (var ev in sz.Eventos) if (ev.Tipo == TipoEvento.Explosao && ev.TipoDano == TipoDano.Veneno) miasma = true; }
                Afirmar(miasma, "o zumbi deveria soltar miasma ao morrer");
            });

            Teste("habilidades de espécie, lote 2: o Carniçal se cura ao matar e o Acólito enfraquece o alvo", () =>
            {
                var sc = new Simulacao(Cfg, ProgressoCom(EspecieId.Carnical, EspecieId.Carnical), 3, ModoExpedicao.Avancar, 5); bool banquete = false; int guarda = 0;
                while (!sc.Encerrada && guarda++ < 6000 && !banquete) { sc.Tick(); foreach (var ev in sc.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 18 && ev.Valor > 0f) banquete = true; }
                Afirmar(banquete, "o carniçal deveria se curar ao matar");
                var sa = new Simulacao(Cfg, ProgressoCom(EspecieId.AcolitoCaido, EspecieId.Esqueleto), 2, ModoExpedicao.Avancar, 5); bool fraco = false; guarda = 0;
                while (!sa.Encerrada && guarda++ < 6000 && !fraco) { sa.Tick(); foreach (var e in sa.Entidades) if (e.Viva && e.Faccao == Faccao.Inimigo && e.FraquezaTempo > 0f) fraco = true; }
                Afirmar(fraco, "o acólito deveria deixar o alvo enfraquecido");
            });

            Teste("habilidades de espécie, lote 3: a Aparição deixa um golpe atravessá-la; com Colosso vivo os outros apanham menos", () =>
            {
                var sa = new Simulacao(Cfg, ProgressoCom(EspecieId.Aparicao, EspecieId.Aparicao), 3, ModoExpedicao.Avancar, 5); bool passou = false; int guarda = 0;
                while (!sa.Encerrada && guarda++ < 8000 && !passou) { sa.Tick(); foreach (var ev in sa.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 19) passou = true; }
                Afirmar(passou, "a aparição deveria ignorar um golpe");
                float Sofrido(ConfigJogo c) { float t = 0f; for (ulong sm = 1; sm <= 8; sm++) t += SofridoEm(c, sm); return t; }
                float SofridoEm(ConfigJogo c, ulong sm)
                {
                    var s = new Simulacao(c, ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto), sm, ModoExpedicao.Avancar, 7); float tot = 0f; int g = 0;
                    while (!s.Encerrada && g++ < 1500) { s.Tick(); foreach (var ev in s.Eventos) if (ev.Tipo == TipoEvento.Dano) { Entidade al = null; foreach (var e in s.Entidades) if (e.Id == ev.Alvo) al = e; if (al != null && al.Faccao == Faccao.Minion && al.Especie == EspecieId.Esqueleto) tot += ev.Valor; } }
                    return tot;
                }
                float com = Sofrido(new ConfigJogo { BaluarteReducao = 0.9f, ObjetosPorSala = 0 }), sem = Sofrido(new ConfigJogo { BaluarteReducao = 0f, ObjetosPorSala = 0 });
                Afirmar(com < sem, "o Baluarte deveria reduzir o dano sofrido pelos outros minions (" + com + " vs " + sem + ")");
            });

            Teste("Forja de Ossos: a sala existe no sorteio e dá couraça aos aliados", () =>
            {
                bool viu = false;
                for (int semente = 1; semente < 400 && !viu; semente++)
                {
                    var sim = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), (ulong)semente, ModoExpedicao.Avancar, 3);
                    sim.Tick();
                    if (!sim.Salas.Contains(TipoSala.ForjaDeOssos)) continue;
                    int guarda = 0;
                    while (!sim.Encerrada && guarda++ < 6000 && !viu)
                    {
                        sim.Tick();
                        foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)TipoSala.ForjaDeOssos)
                        { viu = true; Afirmar(ev.Valor >= Cfg.ForjaEscudo - 1e-4f, "valor da couraça"); Afirmar(sim.Necromante.Escudo > 0f, "o necromante deveria sair com couraça"); }
                    }
                }
                Afirmar(viu, "nenhuma Forja de Ossos resolvida em 400 sementes");
            });

            Teste("Ninho: sem toque as crias chocam e viram luta; com toque os ovos rendem Ossos", () =>
            {
                bool chocou = false, esmagou = false;
                for (int semente = 1; semente < 600 && !(chocou && esmagou); semente++)
                {
                    for (int modo = 0; modo < 2; modo++)
                    {
                        var sim = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), (ulong)semente, ModoExpedicao.Avancar, 3);
                        sim.Tick();
                        if (!sim.Salas.Contains(TipoSala.Ninho)) break;
                        int guarda = 0; bool fim = false;
                        while (!sim.Encerrada && guarda++ < 6000 && !fim)
                        {
                            if (modo == 1 && sim.Fase == FaseExpedicao.Explorando && sim.Salas[sim.SalaAtual] == TipoSala.Ninho) sim.ComandoInteragir();
                            sim.Tick();
                            foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)TipoSala.Ninho)
                            {
                                fim = true;
                                if (modo == 0) { Afirmar(ev.Origem == 2 && sim.Fase == FaseExpedicao.EmOnda, "sem toque o ninho deveria chocar e virar luta"); chocou = true; }
                                else { Afirmar(ev.Origem == 1 && ev.Valor > 0f, "com toque o ninho deveria render Ossos"); esmagou = true; }
                            }
                        }
                    }
                }
                Afirmar(chocou && esmagou, "ninho não exercitado nos dois modos: " + chocou + "/" + esmagou);
            });

            Teste("estilo Diablo: a horda multiplica os comuns (mais fracos, em matilhas) e aparecem pacotes de 3 Campeões", () =>
            {
                int Comuns(ConfigJogo c) { var s = new Simulacao(c, Progressao.NovoJogo(c), 11UL, ModoExpedicao.Avancar, 6); s.Tick(); int n = 0; foreach (var l in s.PlanoDasOndas) foreach (var ip in l) if (!ip.Elite && !ip.Chefe && !ip.Campeao) n++; return n; }
                int com = Comuns(new ConfigJogo { ChanceDeCampeoes = 0f }), sem = Comuns(new ConfigJogo { ChanceDeCampeoes = 0f, Horda = 1f });
                Afirmar(com >= sem * 1.25f, "a horda deveria ter pelo menos 25% mais inimigos: " + com + " vs " + sem);
                var cc = new ConfigJogo { ChanceDeCampeoes = 1f };
                var sim = new Simulacao(cc, Progressao.NovoJogo(cc), 11UL, ModoExpedicao.Avancar, 6); sim.Tick();
                int campeoes = 0; foreach (var ip in sim.PlanoDasOndas[0]) if (ip.Campeao) { campeoes++; Afirmar(ip.Especie == sim.PlanoDasOndas[0][0].Especie, "campeões da mesma espécie"); }
                Afirmar(campeoes == cc.CampeoesPorPacote, "pacote de " + cc.CampeoesPorPacote + " campeões: " + campeoes);
                bool viuCampeao = false, viuHorda = false; int guarda = 0;
                while (!sim.Encerrada && guarda++ < 600 && !(viuCampeao && viuHorda)) { sim.Tick(); foreach (var e in sim.Entidades) { if (e.Campeao) viuCampeao = true; if (e.DaHorda) viuHorda = true; } }
                Afirmar(viuCampeao && viuHorda, "campeões e horda deveriam surgir na sala");
            });

            Teste("estilo Diablo: o Único tem nome próprio, séquito da mesma espécie e sempre larga item", () =>
            {
                var cu = new ConfigJogo { ChanceDeUnico = 1f, ChanceDeCampeoes = 0f };
                var p = Progressao.NovoJogo(cu); p.PoderNecromantico = 60f;
                var sim = new Simulacao(cu, p, 21UL, ModoExpedicao.Avancar, 4) { LimiteDeAndares = 1 }; sim.Tick();
                Simulacao.InimigoPlanejado chefe = null; int sequito = 0;
                foreach (var l in sim.PlanoDasOndas) foreach (var ip in l) { if (ip.Unico) chefe = ip; }
                Afirmar(chefe != null && !string.IsNullOrEmpty(chefe.Nome) && chefe.Nome.Contains(", "), "o andar deveria ter um único com nome");
                foreach (var l in sim.PlanoDasOndas) foreach (var ip in l) if (ip.Sequito) { sequito++; Afirmar(ip.Especie == chefe.Especie && ip.Sala == chefe.Sala, "séquito da mesma espécie e na mesma sala"); }
                Afirmar(sequito == cu.SequitoDoUnico, "séquito de " + cu.SequitoDoUnico + ": " + sequito);
                bool viu = false, morreu = false; int id = -1, itensAntes = 0, guarda = 0;
                while (!sim.Encerrada && guarda++ < 30000 && !morreu)
                {
                    int antes = sim.Resultado.Itens.Count;
                    sim.Tick();
                    foreach (var e in sim.Entidades) if (e.Unico) { viu = true; id = e.Id; Afirmar(e.NomeProprio == chefe.Nome, "nome na entidade"); }
                    foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.Morte && ev.Alvo == id) { morreu = true; itensAntes = antes; }
                }
                Afirmar(viu && morreu, "o único deveria surgir e morrer: " + viu + "/" + morreu);
                Afirmar(sim.Resultado.Itens.Count > itensAntes, "o único deveria largar um item");
            });

            Teste("estilo Diablo: salas de combate têm objetos quebráveis; aliados os quebram ao passar e o barril explosivo fere", () =>
            {
                var c = new ConfigJogo { PortasFechadas = false, EmboscadaDeCorredor = false };
                var p = Progressao.NovoJogo(c); p.PoderNecromantico = 30f;
                var sim = new Simulacao(c, p, 31UL, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 }; sim.Tick();
                Afirmar(sim.Objetos.Count >= 3, "o andar deveria ter objetos: " + sim.Objetos.Count);
                foreach (var o in sim.Objetos) Afirmar(sim.Salas[o.Sala] == TipoSala.Combate || sim.Salas[o.Sala] == TipoSala.Elite, "objeto fora de sala de combate");
                int guarda = 0; bool viuEvento = false;
                while (!sim.Encerrada && guarda++ < 20000) { sim.Tick(); foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 21) viuEvento = true; }
                Afirmar(viuEvento && sim.Resultado.ObjetosQuebrados > 0, "os aliados deveriam quebrar objetos ao lutar: " + sim.Resultado.ObjetosQuebrados);
                // toque: só vale na sala atual e durante a luta
                var s2 = new Simulacao(c, Progressao.NovoJogo(c), 31UL, ModoExpedicao.Avancar, 3); s2.Tick();
                int alvo = -1; guarda = 0;
                while (alvo < 0 && guarda++ < 3000) { s2.Tick(); if (s2.Fase == FaseExpedicao.EmOnda) for (int i = 0; i < s2.Objetos.Count; i++) if (!s2.Objetos[i].Quebrado && s2.Objetos[i].Sala == s2.SalaAtual) { alvo = i; break; } }
                Afirmar(alvo >= 0, "nenhum objeto na sala atual para tocar");
                int outro = -1; for (int i = 0; i < s2.Objetos.Count; i++) if (s2.Objetos[i].Sala != s2.SalaAtual) outro = i;
                Afirmar(s2.ComandoQuebrar(alvo) && s2.Objetos[alvo].Quebrado, "o toque deveria quebrar o objeto");
                Afirmar(!s2.ComandoQuebrar(alvo), "não se quebra duas vezes");
                if (outro >= 0) Afirmar(!s2.ComandoQuebrar(outro), "objeto de outra sala não pode ser quebrado");
            });

            Teste("salas temáticas: a Biblioteca dá XP e o Fosso de Monstros é uma horda de uma espécie só que guarda um item", () =>
            {
                bool leu = false, fosso = false;
                for (int semente = 1; semente < 900 && !(leu && fosso); semente++)
                {
                    var p = Progressao.NovoJogo(Cfg); p.PoderNecromantico = 60f;
                    var sim = new Simulacao(Cfg, p, (ulong)semente, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 };
                    sim.Tick();
                    bool temB = sim.Salas.Contains(TipoSala.Biblioteca), temF = sim.Salas.Contains(TipoSala.FossoDeMonstros);
                    if (!(temB && !leu) && !(temF && !fosso)) continue;
                    int guarda = 0; bool noFosso = false; EspecieId especieDoFosso = EspecieId.Necromante; int nFosso = 0, itensAntes = 0;
                    while (!sim.Encerrada && guarda++ < 20000)
                    {
                        float xpAntes = sim.Resultado.Xp; int itens = sim.Resultado.Itens.Count;
                        sim.Tick();
                        foreach (var ev in sim.Eventos)
                        {
                            if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)TipoSala.Biblioteca) { leu = true; Afirmar(ev.Valor > 0f && sim.Resultado.Xp >= xpAntes + ev.Valor - 0.01f, "o tomo deveria dar XP"); }
                            if (ev.Tipo == TipoEvento.SalaIniciada && ev.Extra == (int)TipoSala.FossoDeMonstros) { noFosso = true; itensAntes = itens; }
                            if (noFosso && ev.Tipo == TipoEvento.EntidadeSurgiu) foreach (var e in sim.Entidades) if (e.Id == ev.Alvo && e.Faccao == Faccao.Inimigo) { if (nFosso == 0) especieDoFosso = e.Especie; Afirmar(e.Especie == especieDoFosso && e.DaHorda, "o fosso é de uma espécie só, em versão de horda"); nFosso++; }
                            if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)TipoSala.FossoDeMonstros) { fosso = true; noFosso = false; Afirmar(nFosso >= 6, "fosso lotado: " + nFosso); Afirmar(sim.Resultado.Itens.Count > itensAntes, "o fosso guarda um item"); }
                        }
                    }
                }
                Afirmar(leu && fosso, "salas não exercitadas: biblioteca " + leu + ", fosso " + fosso);
            });

            Teste("salas temáticas: a Sala de Tortura dá Sangue e o Arsenal dá uma arma ou armadura (Rara com o toque)", () =>
            {
                for (int toque = 0; toque < 2; toque++)
                {
                    foreach (var tipo in new[] { TipoSala.SalaDeTortura, TipoSala.Arsenal })
                    {
                        var c = new ConfigJogo { ForcarSalaEspecial = (int)tipo };
                        var p = Progressao.NovoJogo(c); p.PoderNecromantico = 60f;
                        var sim = new Simulacao(c, p, 9UL, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 }; sim.Tick();
                        Afirmar(sim.Salas.Contains(tipo), "a sala forçada deveria estar no andar: " + tipo);
                        bool viu = false; int guarda = 0;
                        while (!sim.Encerrada && guarda++ < 20000 && !viu)
                        {
                            long sangue = sim.Resultado.Sangue; int itens = sim.Resultado.Itens.Count;
                            if (toque == 1 && sim.Fase == FaseExpedicao.Explorando && sim.Salas[sim.SalaAtual] == tipo) sim.ComandoInteragir();
                            sim.Tick();
                            foreach (var ev in sim.Eventos) if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)tipo)
                            {
                                viu = true;
                                if (tipo == TipoSala.SalaDeTortura) Afirmar(sim.Resultado.Sangue == sangue + c.TorturaSangue + (toque == 1 ? c.TorturaSangueDoToque : 0), "sangue da sala de tortura");
                                else
                                {
                                    Afirmar(sim.Resultado.Itens.Count == itens + 1, "o arsenal dá um item");
                                    var it = sim.Resultado.Itens[sim.Resultado.Itens.Count - 1];
                                    Afirmar((int)it.Tipo <= (int)TipoItem.Botas && it.Raridade >= (toque == 1 ? RaridadeItem.Raro : RaridadeItem.Magico), "arma ou armadura, " + it.Tipo + " " + it.Raridade);
                                }
                            }
                        }
                        Afirmar(viu, "sala não resolvida: " + tipo);
                    }
                }
            });

            Teste("temas de andar: mudam a cada 4 andares e puxam o elenco para a família da casa", () =>
            {
                Afirmar(Temas.Do(1) == TemaDeAndar.Catedral && Temas.Do(4) == TemaDeAndar.Catedral && Temas.Do(5) == TemaDeAndar.Catacumbas && Temas.Do(9) == TemaDeAndar.Cavernas && Temas.Do(13) == TemaDeAndar.Inferno && Temas.Do(17) == TemaDeAndar.Catedral, "o tema deveria mudar a cada 4 andares e repetir depois do Inferno");
                float Bestas(ConfigJogo c)
                {
                    int b = 0, n = 0; var vistos = new System.Collections.Generic.HashSet<int>();
                    for (ulong sm = 1; sm <= 6; sm++)
                    {
                        var s = new Simulacao(c, ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto), sm, ModoExpedicao.Avancar, 9); int g = 0; vistos.Clear();
                        while (!s.Encerrada && g++ < 2500 && s.Andar == 9) { s.Tick(); foreach (var e in s.Entidades) if (e.Faccao == Faccao.Inimigo && vistos.Add(e.Id)) { n++; if (TabelaEspecies.Obter(e.Especie).Familia == Familia.Besta) b++; } }
                    }
                    return n > 0 ? (float)b / n : 0f;
                }
                float com = Bestas(new ConfigJogo { ObjetosPorSala = 0 }), sem = Bestas(new ConfigJogo { ObjetosPorSala = 0, TemasDeAndar = false });
                Afirmar(com > sem * 1.2f, "nas Cavernas (andar 9) deveria haver mais bestas com o tema ligado (" + com + " vs " + sem + ")");
            });

            Teste("Cavernas e Inferno: o andar é irregular (menos compacto), conexo e cabe em 5 salas de lado", () =>
            {
                float Compacto(int andar, out int lado, out bool conexo)
                {
                    float soma = 0f; int n = 0; lado = 0; conexo = true;
                    for (ulong sm = 1; sm <= 12; sm++)
                    {
                        var s = new Simulacao(Cfg, ProgressoCom(EspecieId.Esqueleto), sm, ModoExpedicao.Avancar, andar);
                        int x0 = 99, x1 = -99, y0 = 99, y1 = -99;
                        for (int i = 0; i < s.Mapa.Count; i++)
                        {
                            var c = s.Mapa[i]; x0 = Math.Min(x0, c.X); x1 = Math.Max(x1, c.X); y0 = Math.Min(y0, c.Y); y1 = Math.Max(y1, c.Y);
                            if (i > 0) { bool viz = false; for (int j = 0; j < i; j++) if (Math.Abs(s.Mapa[j].X - c.X) + Math.Abs(s.Mapa[j].Y - c.Y) == 1) viz = true; if (!viz) conexo = false; }
                            for (int j = 0; j < i; j++) if (s.Mapa[j].X == c.X && s.Mapa[j].Y == c.Y) conexo = false;
                        }
                        int w = x1 - x0 + 1, h = y1 - y0 + 1; lado = Math.Max(lado, Math.Max(w, h)); soma += (float)s.Mapa.Count / (w * h); n++;
                    }
                    return soma / n;
                }
                int ladoC, ladoF; bool okC, okF;
                float catedral = Compacto(3, out ladoC, out okC), caverna = Compacto(11, out ladoF, out okF);
                Afirmar(okC && okF, "todas as salas deveriam estar ligadas e sem sobreposição");
                Afirmar(caverna < catedral - 0.05f, "o andar das Cavernas deveria preencher menos o seu retângulo (" + caverna + " vs " + catedral + ")");
                Afirmar(ladoF <= Cfg.MapaFundoLadoMaximo, "o andar irregular não deveria passar de " + Cfg.MapaFundoLadoMaximo + " salas de lado (" + ladoF + ")");
            });

            Teste("missão do Açougueiro: sala no andar 2, abrir é escolha; vencê-lo dá o Cutelo e encerra a missão no save", () =>
            {
                var c = new ConfigJogo { ForcarAcougueiro = true, ObjetosPorSala = 0 };
                // fraco e sem resposta: o necromante não abre a porta
                var s1 = new Simulacao(c, ProgressoCom(EspecieId.Esqueleto), 3, ModoExpedicao.Avancar, 2); int g = 0; bool viu = false, lutou = false;
                Afirmar(s1.Salas.Contains(TipoSala.Acougue), "o andar 2 deveria ter a sala do Açougueiro");
                while (!s1.Encerrada && g++ < 6000 && s1.Andar == 2) { s1.Tick(); if (s1.BencaosOferecidas != null && s1.BencaosOferecidas[0] == 11) viu = true; foreach (var e in s1.Entidades) if (e.NomeProprio == "O Açougueiro") lutou = true; }
                Afirmar(viu && !lutou, "sem resposta e sem recorde, a porta deveria ficar fechada (viu " + viu + ", lutou " + lutou + ")");
                // forte e abrindo a porta: luta, Cutelo e missão vencida
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.DiaboDeGuerra); p.NivelNecromante = 40; foreach (var m in p.Minions) m.Nivel = 20;
                var s2 = new Simulacao(c, p, 5, ModoExpedicao.Avancar, 2); g = 0; lutou = false;
                while (!s2.Encerrada && g++ < 9000 && s2.Andar == 2) { s2.Tick(); if (s2.BencaosOferecidas != null) s2.ComandoEscolherBencao(0); foreach (var e in s2.Entidades) if (e.NomeProprio == "O Açougueiro") lutou = true; }
                Afirmar(lutou, "abrindo a porta, o Açougueiro deveria aparecer");
                Afirmar(s2.Resultado.AcougueiroMorto, "um exército forte deveria vencer o Açougueiro");
                bool cutelo = false; foreach (var it in s2.Resultado.Itens) if (it.Nome == "Cutelo do Açougueiro" && !it.NaoIdentificado) cutelo = true;
                Afirmar(cutelo, "o Açougueiro deveria largar o Cutelo, já identificado");
                Progressao.AplicarResultado(p, s2.Resultado, c);
                Afirmar(p.MissaoAcougueiro == 3, "a missão deveria ficar gravada como vencida");
                var s3 = new Simulacao(new ConfigJogo { ObjetosPorSala = 0 }, p, 7, ModoExpedicao.Avancar, 2);
                Afirmar(!s3.Salas.Contains(TipoSala.Acougue), "depois de vencido, o Açougueiro não deveria voltar");
            });

            Teste("missão do Rei Esqueleto: tumba no andar 3, ele ergue esqueletos enquanto vive; vencê-lo dá a Coroa e encerra a missão", () =>
            {
                var c = new ConfigJogo { ForcarReiEsqueleto = true, ObjetosPorSala = 0 };
                var s1 = new Simulacao(c, ProgressoCom(EspecieId.Esqueleto), 3, ModoExpedicao.Avancar, 3); int g = 0; bool viu = false, lutou = false;
                Afirmar(s1.Salas.Contains(TipoSala.TumbaDoRei), "o andar 3 deveria ter a tumba do Rei");
                while (!s1.Encerrada && g++ < 6000 && s1.Andar == 3) { s1.Tick(); if (s1.BencaosOferecidas != null && s1.BencaosOferecidas[0] == 12) viu = true; foreach (var e in s1.Entidades) if (e.NomeProprio == "Rei Esqueleto") lutou = true; }
                Afirmar(viu && !lutou, "sem resposta e sem recorde, a tumba deveria ficar fechada");
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.DiaboDeGuerra); p.NivelNecromante = 40; foreach (var m in p.Minions) m.Nivel = 20;
                var s2 = new Simulacao(c, p, 5, ModoExpedicao.Avancar, 3); g = 0; int erguidos = 0;
                while (!s2.Encerrada && g++ < 12000 && s2.Andar == 3) { s2.Tick(); if (s2.BencaosOferecidas != null) s2.ComandoEscolherBencao(0); foreach (var ev in s2.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 22) erguidos++; }
                Afirmar(erguidos >= 1, "o Rei deveria erguer esqueletos durante a luta");
                Afirmar(s2.Resultado.ReiEsqueletoMorto, "um exército forte deveria vencer o Rei Esqueleto");
                bool coroa = false; foreach (var it in s2.Resultado.Itens) if (it.Nome == "Coroa do Rei Morto" && it.Tipo == TipoItem.Elmo) coroa = true;
                Afirmar(coroa, "o Rei deveria largar a Coroa do Rei Morto");
                Progressao.AplicarResultado(p, s2.Resultado, c);
                Afirmar(p.MissaoReiEsqueleto == 3, "a missão deveria ficar gravada como vencida");
            });

            Teste("dificuldades: Pesadelo abre ao vencer o chefe do Ato 2; inimigos mais fortes, ouro e ossos maiores, itens de nível mais alto", () =>
            {
                var p = ProgressoCom(EspecieId.Esqueleto);
                Afirmar(!Dificuldades.Definir(p, 1) && p.Dificuldade == 0, "Pesadelo deveria começar trancado");
                var r = new ResultadoExpedicao(); r.ChefesDerrotados.Add(2);
                Dificuldades.Registrar(p, r, Cfg);
                Afirmar(p.DificuldadeLiberada == 1 && Dificuldades.Definir(p, 1) && !Dificuldades.Definir(p, 2), "vencer o chefe do Ato 2 no Normal deveria abrir só o Pesadelo");
                Dificuldades.Registrar(p, r, Cfg);
                Afirmar(p.DificuldadeLiberada == 1, "vencer de novo no Normal não deveria abrir o Inferno");
                p.AndarDeAncoragem = 20; p.Dificuldade = 0;
                Afirmar(Dificuldades.Definir(p, 1) && p.AndarDeAncoragem == 1, "o Pesadelo é uma descida nova: começa do andar 1");
                p.AndarDeAncoragem = 5;
                Afirmar(Dificuldades.Definir(p, 0) && p.AndarDeAncoragem == 20, "voltar ao Normal retoma a ancoragem do Normal");
                Afirmar(Dificuldades.Definir(p, 1) && p.AndarDeAncoragem == 5, "e voltar ao Pesadelo retoma a do Pesadelo");
                p.Dificuldade = 0; p.AndarDeAncoragem = 1;
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                var pn = ProgressoCom(EspecieId.Esqueleto); var pp = ProgressoCom(EspecieId.Esqueleto); pp.DificuldadeLiberada = 1; pp.Dificuldade = 1;
                float VidaDoPrimeiro(EstadoProgresso q) { var s = new Simulacao(c, q, 3, ModoExpedicao.Avancar, 1); int g = 0; while (g++ < 400) { s.Tick(); foreach (var e in s.Entidades) if (e.Faccao == Faccao.Inimigo && !e.Elite) return e.VidaMax; } return 0f; }
                float vn = VidaDoPrimeiro(pn), vp = VidaDoPrimeiro(pp);
                Perto(vp / vn, Cfg.DificuldadeVida[1] * Formulas.EscalaInimigo(1 + Cfg.DificuldadeAndaresExtras[1], Cfg) / Formulas.EscalaInimigo(1, Cfg), vp / vn * 0.02f, "vida dos inimigos no Pesadelo: andar 1 com a força do andar 21");
                Afirmar(vp > vn * 20f, "o Pesadelo deveria ser muito mais duro que o Normal no mesmo andar (" + vp / vn + "x)");
                var rr = new ResultadoExpedicao { Ouro = 100, Dificuldade = 1 }; long antes = pp.Ouro; pp.SemAutoReparo = true; pp.SemReporPocoes = true;
                Progressao.AplicarResultado(pp, rr, c);
                Afirmar(pp.Ouro - antes == (long)(100 * Cfg.DificuldadeOuro[1]), "o ouro do Pesadelo deveria vir multiplicado (" + (pp.Ouro - antes) + ")");
            });

            Teste("salas ligadas: a luta atrai a matilha de uma sala vizinha pela porta, e a sala esvaziada não trava a descida", () =>
            {
                int atraidas = 0, andares = 0;
                for (ulong sm = 1; sm <= 10; sm++)
                {
                    var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, ChanceDeAtrairVizinha = 1f };
                    var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 20;
                    var s = new Simulacao(c, p, sm, ModoExpedicao.Avancar, 2) { LimiteDeAndares = 2 }; int g = 0; bool naPorta = false;
                    while (!s.Encerrada && g++ < 30000)
                    {
                        s.Tick();
                        foreach (var ev in s.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 23) { atraidas++; if (ev.Pos.X < 1.5f || ev.Pos.X > c.LarguraArena - 1.5f || ev.Pos.Y < 1.5f || ev.Pos.Y > c.AlturaArena - 1.5f) naPorta = true; else Afirmar(false, "a matilha atraída deveria entrar por uma porta (borda da sala)"); }
                    }
                    Afirmar(s.Encerrada, "a descida não deveria travar com uma sala esvaziada (semente " + sm + ")");
                    andares += s.Resultado.MaiorAndarConcluido >= 3 ? 1 : 0;
                }
                Afirmar(atraidas >= 3, "com chance 100%, salas vizinhas deveriam ser atraídas (" + atraidas + ")");
                Afirmar(andares >= 8, "um exército forte deveria concluir os dois andares mesmo com salas atraídas (" + andares + "/10)");
            });

            Teste("salas ligadas: com a configuração padrão a atração acontece com frequência perceptível", () =>
            {
                int atraidas = 0, andares = 0;
                for (ulong sm = 1; sm <= 12; sm++)
                {
                    var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 20;
                    var s = new Simulacao(new ConfigJogo { MissoesDeCenario = false }, p, sm, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 3 }; int g = 0;
                    while (!s.Encerrada && g++ < 40000) s.Tick();
                    atraidas += s.Resultado.SalasAtraidas; andares += 3;
                }
                Console.WriteLine("        (salas atraídas: " + atraidas + " em " + andares + " andares)");
                Afirmar(atraidas * 3 >= andares, "deveria haver ao menos 1 sala atraída a cada 3 andares (" + atraidas + " em " + andares + ")");
            });

            Teste("livros de feitiço: cada livro sobe um nível, exige Magia, o que não dá para ler fica guardado; o nível deixa o feitiço mais forte e mais barato", () =>
            {
                var p = ProgressoCom(EspecieId.Esqueleto); p.NivelNecromante = 20; p.SemAutoAtributos = true;
                Afirmar(Grimorio.Nivel(p, FeiticoId.LancaDeOssos) == 1, "todo feitiço começa no nível 1");
                Afirmar(!Grimorio.Receber(p, FeiticoId.LancaDeOssos, Cfg) && Grimorio.Guardados(p, FeiticoId.LancaDeOssos) == 1, "sem Magia o livro fica guardado");
                Afirmar(Atributos.Gastar(p, Atributo.Magia, Cfg.LivroMagiaPorNivel, Cfg), "pontos de Magia");
                Afirmar(Grimorio.LerGuardados(p, Cfg) == 1 && Grimorio.Nivel(p, FeiticoId.LancaDeOssos) == 2 && Grimorio.Guardados(p, FeiticoId.LancaDeOssos) == 0, "com Magia o livro guardado é lido");
                Afirmar(!Grimorio.Receber(p, FeiticoId.LancaDeOssos, Cfg), "o nível 3 pede mais Magia");
                Afirmar(Grimorio.FatorDePotencia(3, Cfg) > Grimorio.FatorDePotencia(1, Cfg) && Grimorio.FatorDeMana(3, Cfg) < 1f, "nível maior: mais potência, menos mana");
                // na masmorra: a Biblioteca sempre dá um livro
                var c = new ConfigJogo { ForcarSalaEspecial = (int)TipoSala.Biblioteca, ObjetosPorSala = 0, MissoesDeCenario = false };
                var s = new Simulacao(c, ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos), 4, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 }; int g = 0;
                while (!s.Encerrada && g++ < 20000) s.Tick();
                Afirmar(s.Resultado.Livros.Count >= 1, "a Biblioteca deveria render um livro de feitiço");
                var q = ProgressoCom(EspecieId.Esqueleto); q.NivelNecromante = 30; int antes = 0; for (int i = 0; i < Grimorio.Quantos; i++) antes += Grimorio.Nivel(q, (FeiticoId)i) + Grimorio.Guardados(q, (FeiticoId)i);
                Progressao.AplicarResultado(q, s.Resultado, c); int depois = 0; for (int i = 0; i < Grimorio.Quantos; i++) depois += Grimorio.Nivel(q, (FeiticoId)i) + Grimorio.Guardados(q, (FeiticoId)i);
                Afirmar(depois == antes + s.Resultado.Livros.Count, "os livros achados deveriam entrar no grimório (lidos ou guardados)");
            });

            Teste("corredores com vida: portas emperradas atrasam a travessia (o toque abre na hora) e emboscadas de corredor caem em cima do grupo", () =>
            {
                long Ticks(bool tocar, out int portas, out int porToque)
                {
                    var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, ChanceDePortaFechada = 1f, EmboscadaDeCorredor = false, SalasLigadas = false, ChanceDeSalaEspecial = 0f };
                    var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 20;
                    var s = new Simulacao(c, p, 9, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 }; int g = 0;
                    while (!s.Encerrada && g++ < 30000) { s.Tick(); if (tocar && s.PortaFechada) s.ComandoAbrirPorta(); }
                    portas = s.Resultado.PortasArrombadas; porToque = s.Resultado.PortasAbertasPorToque;
                    return s.TickAtual;
                }
                int pa, pt, qa, qt; long semToque = Ticks(false, out pa, out pt), comToque = Ticks(true, out qa, out qt);
                Afirmar(pa >= 2 && pt == 0, "sem toque, as portas são arrombadas (" + pa + ")");
                Afirmar(qt >= 2 && qa == 0, "com toque, as portas abrem na hora (" + qt + ")");
                Afirmar(comToque < semToque, "abrir pelo toque deveria ser mais rápido (" + comToque + " vs " + semToque + " ticks)");
                var c2 = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, ChanceDeEmboscadaDeCorredor = 1f, PortasFechadas = false, SalasLigadas = false };
                var p2 = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p2.NivelNecromante = 30; foreach (var m in p2.Minions) m.Nivel = 20;
                var s2 = new Simulacao(c2, p2, 9, ModoExpedicao.Avancar, 4) { LimiteDeAndares = 1 }; int g2 = 0; bool perto = false;
                while (!s2.Encerrada && g2++ < 30000) { s2.Tick(); foreach (var ev in s2.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 27) perto = true; }
                Afirmar(s2.Resultado.EmboscadasDeCorredor >= 1 && perto, "com chance 100% deveria haver emboscada de corredor");
                Afirmar(s2.Encerrada, "a descida não deveria travar");
            });

            Teste("Golem: o necromante conjura um só por vez durante a luta, ele some com o tempo, e saves antigos ganham o 7º feitiço sem perder níveis", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                var p = ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 20;
                var semLivro = new Simulacao(c, p, 6, ModoExpedicao.Avancar, 3); int g0 = 0; while (!semLivro.Encerrada && g0++ < 3000) semLivro.Tick();
                Afirmar(semLivro.Resultado.GolensConjurados == 0, "sem o livro, o necromante não conhece o Golem");
                Afirmar(Grimorio.Receber(p, FeiticoId.Golem, c) && Grimorio.Nivel(p, FeiticoId.Golem) == 1, "o primeiro livro ensina o Golem (não pede Magia)");
                var s = new Simulacao(c, p, 6, ModoExpedicao.Avancar, 3); int g = 0, maxGolens = 0; bool viu = false, sumiu = false;
                while (!s.Encerrada && g++ < 6000)
                {
                    s.Tick(); int n = 0; foreach (var e in s.Entidades) if (e.Golem && e.Viva) n++;
                    if (n > maxGolens) maxGolens = n; if (n > 0) viu = true; else if (viu) sumiu = true;
                }
                Afirmar(viu && s.Resultado.GolensConjurados >= 1, "o Golem deveria ser conjurado na luta");
                Afirmar(maxGolens == 1, "só pode haver um Golem por vez (" + maxGolens + ")");
                Afirmar(sumiu, "o Golem deveria acabar (tempo ou morte)");
                var semGolem = new Simulacao(new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, GolemAtivo = false }, p, 6, ModoExpedicao.Avancar, 3); g = 0;
                while (!semGolem.Encerrada && g++ < 3000) semGolem.Tick();
                Afirmar(semGolem.Resultado.GolensConjurados == 0, "com o Golem desligado, nada de Golem");
                var antigo = ProgressoCom(EspecieId.Esqueleto); antigo.NiveisDeFeitico = new[] { 2, 0, 1, 0, 0, 3 }; antigo.LivrosGuardados = new[] { 0, 1, 0, 0, 0, 0 };
                Afirmar(Grimorio.Nivel(antigo, FeiticoId.Golem) == 0 && Grimorio.Nivel(antigo, FeiticoId.Sacrificio) == 4 && Grimorio.Nivel(antigo, FeiticoId.LancaDeOssos) == 3 && Grimorio.Guardados(antigo, FeiticoId.DrenarVida) == 1, "o save antigo mantém os níveis e ganha o Golem ainda por aprender");
            });

            Teste("despertar por proximidade: a frente marcha até os adormecidos, que acordam quando ela chega perto (e não todos de uma vez)", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, SalasLigadas = false, EmboscadaDeCorredor = false, PortasFechadas = false };
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 20;
                var s = new Simulacao(c, p, 12, ModoExpedicao.Avancar, 4) { LimiteDeAndares = 1 }; int g = 0; bool marchou = false; float xInicial = -1f, xMax = 0f;
                while (!s.Encerrada && g++ < 30000)
                {
                    s.Tick();
                    if (s.HaAlvoDeMarcha) marchou = true;
                    foreach (var e in s.Entidades) if (e.Faccao == Faccao.Minion && e.Viva && !e.Temporario) { if (xInicial < 0f) xInicial = e.Pos.X; if (e.Pos.X > xMax) xMax = e.Pos.X; }
                }
                Afirmar(s.Encerrada && s.Resultado.MaiorAndarConcluido >= 4, "a descida deveria concluir o andar");
                Afirmar(marchou && xMax > xInicial + 4f, "a linha de frente deveria marchar sala adentro (" + xInicial + " -> " + xMax + ")");
                Afirmar(s.Resultado.DespertaresPorProximidade >= 2, "inimigos deveriam acordar por proximidade (" + s.Resultado.DespertaresPorProximidade + ")");
                var c0 = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, SalasLigadas = false, EmboscadaDeCorredor = false, PortasFechadas = false, DespertarPorProximidade = false };
                var s0 = new Simulacao(c0, p, 12, ModoExpedicao.Avancar, 4) { LimiteDeAndares = 1 }; g = 0; while (!s0.Encerrada && g++ < 30000) s0.Tick();
                Afirmar(s0.Resultado.DespertaresPorProximidade == 0 && !s0.HaAlvoDeMarcha, "desligado, vale o despertar antigo por tempo");
            });

            Teste("comportamento por espécie: diabretes fogem ao ver um companheiro morrer, carniçais feridos devoram cadáveres, e a descida não trava", () =>
            {
                int fugas = 0, banquetes = 0, concluiu = 0;
                for (ulong sm = 1; sm <= 8; sm++)
                {
                    var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                    var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 18;
                    var s = new Simulacao(c, p, sm, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 3 }; int g = 0;
                    while (!s.Encerrada && g++ < 60000) s.Tick();
                    fugas += s.Resultado.Fugas; banquetes += s.Resultado.Banquetes; if (s.Encerrada) concluiu++;
                }
                Console.WriteLine("        (fugas: " + fugas + ", banquetes: " + banquetes + ")");
                Afirmar(concluiu == 8, "nenhuma descida deveria travar (" + concluiu + "/8)");
                Afirmar(fugas >= 3, "diabretes deveriam fugir ao ver companheiros morrerem (" + fugas + ")");
                var c0 = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, ComportamentoDeEspecie = false };
                var p0 = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p0.NivelNecromante = 30;
                var s0 = new Simulacao(c0, p0, 1, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; int g0 = 0; while (!s0.Encerrada && g0++ < 60000) s0.Tick();
                Afirmar(s0.Resultado.Fugas == 0 && s0.Resultado.Banquetes == 0, "desligado, ninguém foge nem come");
            });

            Teste("bestas cercam pelos flancos: a matilha abre para os lados em vez de vir em linha reta, e a descida não trava", () =>
            {
                int flancos = 0, concluiu = 0; float maiorDesvio = 0f;
                for (ulong sm = 1; sm <= 6; sm++)
                {
                    var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                    var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 18;
                    var s = new Simulacao(c, p, sm, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 3 }; int g = 0;
                    var antes = new System.Collections.Generic.Dictionary<int, Vec2>();
                    while (!s.Encerrada && g++ < 60000)
                    {
                        s.Tick();
                        foreach (var e in s.Entidades)
                        {
                            if (!e.Viva || e.Faccao != Faccao.Inimigo || e.Familia != Familia.Besta || !e.Flanqueando) continue;
                            Vec2 a; if (antes.TryGetValue(e.Id, out a)) { float dy = Math.Abs(e.Pos.Y - a.Y); if (dy > maiorDesvio) maiorDesvio = dy; } else antes[e.Id] = e.Pos;
                        }
                    }
                    flancos += s.Resultado.Flancos; if (s.Encerrada) concluiu++;
                }
                Console.WriteLine("        (bestas que flanquearam: " + flancos + ", maior desvio lateral: " + maiorDesvio.ToString("0.0") + ")");
                Afirmar(concluiu == 6, "nenhuma descida deveria travar (" + concluiu + "/6)");
                Afirmar(flancos >= 6, "bestas deveriam flanquear (" + flancos + ")");
                Afirmar(maiorDesvio >= 1.2f, "o caminho da besta deveria abrir para o lado (" + maiorDesvio + ")");
                var c0 = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, FlancoDasBestas = false };
                var p0 = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos); p0.NivelNecromante = 30;
                var s0 = new Simulacao(c0, p0, 1, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; int g0 = 0; while (!s0.Encerrada && g0++ < 60000) s0.Tick();
                Afirmar(s0.Resultado.Flancos == 0, "desligado, ninguém flanqueia");
            });

            Teste("imunidades como no Diablo 1: no Pesadelo a resistência sobe, a partir do limiar vira imunidade (nunca a físico), e o golpe imune não fere", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                var p = ProgressoCom(EspecieId.Esqueleto);
                var sn = new Simulacao(c, p, 1, ModoExpedicao.Avancar, 3);
                var zn = sn.CriarInimigo(EspecieId.Zumbi, new Vec2(5, 5));
                Afirmar(zn.Imune == 0 && Math.Abs(zn.Resistencias[(int)TipoDano.Veneno] - 0.5f) < 1e-4f, "no Normal nada muda");
                p.DificuldadeLiberada = 2; p.Dificuldade = 1;
                var sp = new Simulacao(c, p, 1, ModoExpedicao.Avancar, 3);
                var z = sp.CriarInimigo(EspecieId.Zumbi, new Vec2(5, 5)); z.Vida = z.VidaMax;
                Afirmar((z.Imune & (1 << (int)TipoDano.Veneno)) != 0, "Zumbi (50% a veneno) fica imune a veneno no Pesadelo");
                Afirmar((z.Imune & 1) == 0 && z.Resistencias[(int)TipoDano.Fogo] < 0f, "físico nunca é imune e a fraqueza a fogo continua");
                var esq = sp.CriarInimigo(EspecieId.Esqueleto, new Vec2(6, 5));
                Afirmar(esq.Imune == 0 && Math.Abs(esq.Resistencias[(int)TipoDano.Veneno] - 0.5f) < 1e-4f, "Esqueleto (30% a veneno) no Pesadelo só resiste mais: " + esq.Resistencias[(int)TipoDano.Veneno]);
                float v0 = z.Vida;
                Afirmar(sp.CausarDano(null, z, 50f, TipoDano.Veneno, false) == 0f && z.Vida == v0 && sp.Resultado.GolpesEmImunes == 1, "veneno não fere o imune");
                Afirmar(sp.CausarDano(null, z, 50f, TipoDano.Fogo, false) > 0f && z.Vida < v0, "fogo fere (e mais, pela fraqueza)");
                p.Dificuldade = 2;
                var si = new Simulacao(c, p, 1, ModoExpedicao.Avancar, 3);
                Afirmar((si.CriarInimigo(EspecieId.Esqueleto, new Vec2(5, 5)).Imune & (1 << (int)TipoDano.Veneno)) == 0 && (si.CriarInimigo(EspecieId.Aparicao, new Vec2(5, 6)).Imune & (1 << (int)TipoDano.Sombra)) != 0, "no Inferno mais espécies ficam imunes (Aparição a sombra)");
                var c0 = new ConfigJogo { ImunidadesDeInimigos = false };
                Afirmar(new Simulacao(c0, p, 1, ModoExpedicao.Avancar, 3).CriarInimigo(EspecieId.Zumbi, new Vec2(5, 5)).Imune == 0, "desligado, ninguém é imune");
            });

            Teste("Muralha de Fogo: só depois do livro, atravessa o caminho do grupo inimigo, queima com fogo quem está nela e apaga sozinha", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, PortasFechadas = false, EmboscadaDeCorredor = false, SalasLigadas = false };
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 18;
                Afirmar(Grimorio.Nivel(p, FeiticoId.MuralhaDeFogo) == 0, "sem livro, a Muralha não existe");
                var s0 = new Simulacao(c, p, 3, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; int g = 0; while (!s0.Encerrada && g++ < 60000) s0.Tick();
                Afirmar(s0.Resultado.MuralhasDeFogo == 0, "sem livro, ninguém conjura");
                p.NiveisDeFeitico[(int)FeiticoId.MuralhaDeFogo] = 1;
                var s = new Simulacao(c, p, 3, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; g = 0; int ev39 = 0; float danoDeFogo = 0f, maiorComprimento = 0f; bool apagou = false, acesa = false;
                while (!s.Encerrada && g++ < 60000)
                {
                    s.Tick();
                    if (s.Muralhas.Count > 0) { acesa = true; var mu = s.Muralhas[0]; float l = Vec2.Dist(mu.A, mu.B); if (l > maiorComprimento) maiorComprimento = l; } else if (acesa) apagou = true;
                    foreach (var ev in s.Eventos) { if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 39) ev39++; if (ev.Tipo == TipoEvento.Dano && ev.TipoDano == TipoDano.Fogo && ev.Origem == s.Necromante.Id) danoDeFogo += ev.Valor; }
                }
                Console.WriteLine("        (muralhas: " + s.Resultado.MuralhasDeFogo + ", dano de fogo: " + danoDeFogo.ToString("0") + ", comprimento: " + maiorComprimento.ToString("0.0") + ")");
                Afirmar(s.Encerrada && s.Resultado.MuralhasDeFogo >= 2 && ev39 == s.Resultado.MuralhasDeFogo, "com o livro, o necromante ergue a Muralha (" + s.Resultado.MuralhasDeFogo + ")");
                Afirmar(danoDeFogo > 0f && apagou, "a Muralha queima e depois apaga");
                Afirmar(maiorComprimento > 3f && maiorComprimento <= c.MuralhaComprimento + 0.01f, "comprimento dentro do configurado: " + maiorComprimento);
                Afirmar(Grimorio.SoPorLivro(FeiticoId.Golem) && Grimorio.SoPorLivro(FeiticoId.MuralhaDeFogo) && !Grimorio.SoPorLivro(FeiticoId.LancaDeOssos), "Golem e Muralha só por livro");
            });

            Teste("estresse no Pesadelo: descidas com poder alto, Muralha e Golem aprendidos não lançam exceção", () =>
            {
                var c = new ConfigJogo();
                for (ulong sm = 1; sm <= 40; sm++)
                {
                    var p = ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 40; p.PoderNecromantico = 9000f;
                    p.DificuldadeLiberada = 1; p.Dificuldade = 1; p.NiveisDeFeitico[(int)FeiticoId.MuralhaDeFogo] = (int)(sm % 2); p.NiveisDeFeitico[(int)FeiticoId.Golem] = (int)(sm % 3 == 0 ? 1 : 0);
                    var s = new Simulacao(c, p, sm * 31UL, ModoExpedicao.Avancar, 1 + (int)(sm % 9)) { LimiteDeAndares = 4, ReapresentarEventosDeComandos = true }; int g = 0;
                    try { while (!s.Encerrada && g++ < 40000) { s.Tick(); if (g % 50 == 0) { foreach (var e in s.Entidades) if (e.Viva && e.Faccao == Faccao.Inimigo) { s.ComandoMarcar(e.Id); break; } } } }
                    catch (Exception ex) { Afirmar(false, "semente " + sm + ", tick " + g + ": " + ex); }
                }
            });

            Teste("Súcubo: demônio atirador dos andares fundos; inimiga recua de quem chega perto; erguida, cura o necromante a cada acerto", () =>
            {
                var d = TabelaEspecies.Obter(EspecieId.Sucubo);
                Afirmar(TabelaEspecies.Total >= 15 && d != null && d.Familia == Familia.Demonio && d.Papel == Papel.Atirador && d.AtoMinimo == 2 && d.AndarMinimo == 2 && d.PesoOnda > 0f, "está na tabela, do andar 12 em diante (Ato 2, segundo andar)");
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                // aparece nas ondas dos andares fundos e não antes
                int cedo = 0, fundo = 0;
                for (ulong sm = 1; sm <= 6; sm++)
                {
                    var p1 = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p1.NivelNecromante = 60; p1.PoderNecromantico = 50f; foreach (var m in p1.Minions) m.Nivel = 20;
                    var a = new Simulacao(c, p1, sm, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; int g = 0; while (!a.Encerrada && g++ < 40000) { a.Tick(); foreach (var ev in a.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.Sucubo) cedo++; }
                    var b = new Simulacao(c, p1, sm, ModoExpedicao.Avancar, 13) { LimiteDeAndares = 2 }; g = 0; while (!b.Encerrada && g++ < 40000) { b.Tick(); foreach (var ev in b.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.Sucubo) fundo++; }
                }
                Console.WriteLine("        (súcubos vistas: " + cedo + " nos andares 6-7, " + fundo + " nos andares 13-14)");
                Afirmar(cedo == 0 && fundo >= 3, "só nos andares fundos: " + cedo + " / " + fundo);
                // erguida: Beijo de Sangue
                var p = ProgressoCom(EspecieId.Sucubo); p.NivelNecromante = 10;
                var s = new Simulacao(c, p, 5, ModoExpedicao.Avancar, 2);
                var suc = s.Entidades.Find(e => e.Faccao == Faccao.Minion && e.Especie == EspecieId.Sucubo);
                var alvo = s.CriarInimigo(EspecieId.Zumbi, new Vec2(8, 6));
                s.Necromante.Vida = s.Necromante.VidaMax * 0.5f; float v0 = s.Necromante.Vida;
                float dano = s.CausarDano(suc, alvo, 20f, TipoDano.Sombra, true);
                Afirmar(dano > 0f && Math.Abs(s.Necromante.Vida - v0 - dano * c.BeijoDeSangue) < 0.01f, "o acerto da Súcubo cura o necromante em " + c.BeijoDeSangue * 100 + "% do dano (" + (s.Necromante.Vida - v0) + " de " + dano + ")");
            });

            Teste("Relâmpago em Cadeia: só depois do livro, dano de raio que salta entre inimigos vizinhos perdendo força, sem acertar o mesmo duas vezes", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, PortasFechadas = false, EmboscadaDeCorredor = false, SalasLigadas = false };
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 18;
                var s0 = new Simulacao(c, p, 3, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; int g = 0; while (!s0.Encerrada && g++ < 60000) s0.Tick();
                Afirmar(Grimorio.Nivel(p, FeiticoId.RelampagoEmCadeia) == 0 && s0.Resultado.Relampagos == 0, "sem livro, ninguém conjura");
                p.NiveisDeFeitico[(int)FeiticoId.RelampagoEmCadeia] = 1;
                var s = new Simulacao(c, p, 3, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; g = 0; int ev41 = 0, maiorCadeia = 0, cadeia = 0; float danoDeRaio = 0f, primeiro = 0f, segundo = 0f; bool repetiu = false; var vistos = new System.Collections.Generic.HashSet<int>();
                while (!s.Encerrada && g++ < 60000)
                {
                    s.Tick();
                    foreach (var ev in s.Eventos)
                    {
                        if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 41) { ev41++; if (ev.Valor == 0f) { cadeia = 0; vistos.Clear(); } cadeia++; if (!vistos.Add(ev.Alvo)) repetiu = true; if (cadeia > maiorCadeia) maiorCadeia = cadeia; }
                        if (ev.Tipo == TipoEvento.Dano && ev.TipoDano == TipoDano.Raio && ev.Origem == s.Necromante.Id) { danoDeRaio += ev.Valor; if (!ev.Critico) { if (cadeia == 1 && primeiro == 0f) primeiro = ev.Valor; else if (cadeia == 2 && segundo == 0f && primeiro > 0f) segundo = ev.Valor; } }
                    }
                }
                Console.WriteLine("        (relâmpagos: " + s.Resultado.Relampagos + ", saltos: " + s.Resultado.SaltosDeRelampago + ", maior cadeia: " + maiorCadeia + ", dano de raio: " + danoDeRaio.ToString("0") + ")");
                Afirmar(s.Encerrada && s.Resultado.Relampagos >= 3 && ev41 == s.Resultado.Relampagos + s.Resultado.SaltosDeRelampago, "com o livro, o necromante lança o raio (" + s.Resultado.Relampagos + ")");
                Afirmar(s.Resultado.SaltosDeRelampago >= 2 && maiorCadeia >= 2 && maiorCadeia <= c.RelampagoSaltos + 1, "o raio salta, até " + (c.RelampagoSaltos + 1) + " alvos: " + maiorCadeia);
                Afirmar(danoDeRaio > 0f && !repetiu, "fere com raio e não repete alvo na mesma cadeia");
            });

            Teste("Cavaleiro Negro e Magistrado: só no Ato 2; o Cavaleiro tem couraça (inimigo e erguido); o Magistrado se teleporta de quem encosta e, erguido, volta para junto do necromante", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                var cav = TabelaEspecies.Obter(EspecieId.CavaleiroNegro); var mag = TabelaEspecies.Obter(EspecieId.Magistrado);
                Afirmar(TabelaEspecies.Total >= 17 && cav.AtoMinimo == 2 && mag.AtoMinimo == 2 && cav.Armadura >= 60f && cav.Resistencias[0] > 0f, "na tabela, do Ato 2 em diante, com armadura alta");
                int cedoC = 0, fundoC = 0, cedoM = 0, fundoM = 0, teles = 0, concluiu = 0;
                for (ulong sm = 1; sm <= 6; sm++)
                {
                    var p1 = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p1.NivelNecromante = 60; p1.PoderNecromantico = 50f; foreach (var m in p1.Minions) m.Nivel = 20;
                    var a = new Simulacao(c, p1, sm, ModoExpedicao.Avancar, 6) { LimiteDeAndares = 2 }; int g = 0; while (!a.Encerrada && g++ < 40000) { a.Tick(); foreach (var ev in a.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu) { if (ev.Especie == EspecieId.CavaleiroNegro) cedoC++; if (ev.Especie == EspecieId.Magistrado) cedoM++; } }
                    var b = new Simulacao(c, p1, sm, ModoExpedicao.Avancar, 16) { LimiteDeAndares = 2 }; g = 0; while (!b.Encerrada && g++ < 40000) { b.Tick(); foreach (var ev in b.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu) { if (ev.Especie == EspecieId.CavaleiroNegro) fundoC++; if (ev.Especie == EspecieId.Magistrado) fundoM++; } }
                    teles += b.Resultado.Teleportes; if (b.Encerrada) concluiu++;
                }
                Console.WriteLine("        (andares 16-17: " + fundoC + " cavaleiros, " + fundoM + " magistrados, " + teles + " teleportes)");
                Afirmar(cedoC == 0 && cedoM == 0 && fundoC >= 2 && fundoM >= 2, "só no Ato 2: " + cedoC + "/" + cedoM + " cedo, " + fundoC + "/" + fundoM + " fundo");
                Afirmar(teles >= 2 && concluiu == 6, "magistrados se teleportam e nenhuma descida trava (" + teles + ", " + concluiu + "/6)");
                // erguidos
                var p = ProgressoCom(EspecieId.CavaleiroNegro, EspecieId.Esqueleto, EspecieId.Magistrado); p.NivelNecromante = 10;
                var s = new Simulacao(c, p, 5, ModoExpedicao.Avancar, 2);
                var mc = s.Entidades.Find(e => e.Faccao == Faccao.Minion && e.Especie == EspecieId.CavaleiroNegro); var me = s.Entidades.Find(e => e.Faccao == Faccao.Minion && e.Especie == EspecieId.Esqueleto); var mm = s.Entidades.Find(e => e.Faccao == Faccao.Minion && e.Especie == EspecieId.Magistrado);
                mc.Armadura = 0f; me.Armadura = 0f; me.Aparou = true; for (int r = 0; r < 6; r++) { mc.Resistencias[r] = 0f; me.Resistencias[r] = 0f; }
                float dc = s.CausarDano(null, mc, 40f, TipoDano.Fisico, false), de = s.CausarDano(null, me, 40f, TipoDano.Fisico, false), df = s.CausarDano(null, mc, 40f, TipoDano.Fogo, false);
                Afirmar(Math.Abs(dc - de * (1f - c.CouracaDoCavaleiro)) < 0.01f && Math.Abs(df - de) < 0.01f, "Couraça: " + c.CouracaDoCavaleiro * 100 + "% a menos de dano físico (" + dc + " x " + de + "), fogo inteiro (" + df + ")");
                mm.Pos = new Vec2(12f, 6f); mm.Vida = mm.VidaMax * 0.55f; int antes = s.Resultado.Teleportes;
                s.CausarDano(null, mm, mm.VidaMax * 0.2f, TipoDano.Fogo, false);
                Afirmar(s.Resultado.Teleportes == antes + 1 && Vec2.Dist(mm.Pos, s.Necromante.Pos) < 2.5f, "Passo Sombrio: ferido, reaparece junto do necromante (" + Vec2.Dist(mm.Pos, s.Necromante.Pos) + ")");
            });

            Teste("Ato 3: o chefe do andar 30 é o Arcebispo Lázarus, que acende muralhas de fogo sob o grupo, se teleporta e chama Súcubos; a luta termina", () =>
            {
                Afirmar(TabelaEspecies.ChefeDoAto(1) == EspecieId.AbadessaCosturada && TabelaEspecies.ChefeDoAto(2) == EspecieId.CarcereiroDeAlmas && TabelaEspecies.ChefeDoAto(3) == EspecieId.Lazarus && TabelaEspecies.ChefeDoAto(4) == EspecieId.Diablo && TabelaEspecies.ChefeDoAto(5) == EspecieId.AbadessaCosturada, "chefes em ciclo de quatro atos");
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                int viu = 0, muralhas = 0, teles = 0, sucubos = 0, venceu = 0, acabou = 0; float fogoSofrido = 0f;
                for (ulong sm = 1; sm <= 4; sm++)
                {
                    var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 90; p.PoderNecromantico = 6000f; foreach (var m in p.Minions) m.Nivel = 20;
                    var s = new Simulacao(c, p, sm, ModoExpedicao.Avancar, 30) { LimiteDeAndares = 1 }; int g = 0; int telesAntes = 0; bool chefeVivo = false;
                    while (!s.Encerrada && g++ < 80000)
                    {
                        s.Tick();
                        foreach (var ev in s.Eventos)
                        {
                            if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.Lazarus) { viu++; chefeVivo = true; telesAntes = s.Resultado.Teleportes; }
                            if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.Sucubo && chefeVivo) sucubos++;
                            if (ev.Tipo == TipoEvento.Dano && ev.TipoDano == TipoDano.Fogo && ev.Alvo == s.Necromante.Id) fogoSofrido += ev.Valor;
                        }
                    }
                    muralhas += s.Resultado.MuralhasDeLazarus; if (chefeVivo) teles += s.Resultado.Teleportes - telesAntes;
                    if (s.Encerrada) acabou++; if (s.Resultado.ChefesDerrotados.Contains(3) || s.Resultado.MaiorAndarConcluido >= 30) venceu++;
                }
                Console.WriteLine("        (Lázarus: visto " + viu + "/4, venceu " + venceu + "/4, muralhas " + muralhas + ", teleportes " + teles + ", súcubos " + sucubos + ", fogo no necromante " + fogoSofrido.ToString("0") + ")");
                Afirmar(viu == 4 && acabou == 4, "o chefe do andar 30 é Lázarus e a luta termina (" + viu + ", " + acabou + ")");
                Afirmar(muralhas >= 4 && sucubos >= 2, "acende muralhas e chama Súcubos (" + muralhas + ", " + sucubos + ")");
                Afirmar(venceu >= 1, "um exército forte consegue vencê-lo (" + venceu + "/4)");
            });

            Teste("Ato 4 e fim da campanha: Diablo no andar 40 solta o Apocalipse; vencê-lo marca a campanha na dificuldade, dá um Único travado e pede o desfecho uma única vez", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false };
                int viu = 0, venceu = 0, apocalipses = 0, acabou = 0; ResultadoExpedicao vitoria = null;
                for (ulong sm = 1; sm <= 4; sm++)
                {
                    var p0 = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto); p0.NivelNecromante = 120; p0.PoderNecromantico = 60000f; foreach (var m in p0.Minions) m.Nivel = 20;
                    var s = new Simulacao(c, p0, sm, ModoExpedicao.Avancar, 40) { LimiteDeAndares = 1 }; int g = 0;
                    while (!s.Encerrada && g++ < 80000) { s.Tick(); foreach (var ev in s.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu && ev.Especie == EspecieId.Diablo) viu++; }
                    apocalipses += s.Resultado.Apocalipses; if (s.Encerrada) acabou++;
                    if (s.Resultado.ChefesVencidosNaDescida.Contains(4)) { venceu++; if (vitoria == null) vitoria = s.Resultado; }
                }
                Console.WriteLine("        (Diablo: visto " + viu + "/4, venceu " + venceu + "/4, apocalipses " + apocalipses + ")");
                Afirmar(viu == 4 && acabou == 4 && apocalipses >= 3, "o chefe do andar 40 é Diablo, solta o Apocalipse e a luta termina (" + viu + ", " + acabou + ", " + apocalipses + ")");
                Afirmar(venceu >= 1 && vitoria != null, "um exército forte o vence (" + venceu + "/4)");
                var p = Progressao.NovoJogo(c); int itens0 = p.Inventario.Count;
                Progressao.AplicarResultado(p, vitoria, c);
                Afirmar(p.CampanhaVencida == 1 && p.VitoriaPendente && p.VitoriaNaDificuldade == 0, "a campanha fica marcada no Normal e o desfecho fica pendente");
                Afirmar(p.Inventario.Exists(it => it.UnicoId > 0 && it.Bloqueado), "um item Único travado vem de prêmio");
                p.VitoriaPendente = false; int unicos = p.Inventario.FindAll(it => it.UnicoId > 0 && it.Bloqueado).Count;
                Progressao.AplicarResultado(p, vitoria, c);
                Afirmar(!p.VitoriaPendente && p.Inventario.FindAll(it => it.UnicoId > 0 && it.Bloqueado).Count == unicos, "vencer de novo na mesma dificuldade não repete o desfecho nem o prêmio");
                p.ChefesDerrotados.Add(1); Ascensao.Ascender(p, c);
                Afirmar(p.CampanhaVencida == 1, "o marco atravessa a Ascensão");
            });

            Teste("missão da Fonte Envenenada: sala a mais no andar da missão, luta contra os mortos da nascente, e limpa a fonte as poções ficam mais baratas até a Ascensão", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, ForcarFonte = true, PortasFechadas = false, EmboscadaDeCorredor = false, SalasLigadas = false };
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 30; foreach (var m in p.Minions) m.Nivel = 18;
                long antes = Pocoes.Preco(p, false, c);
                var s = new Simulacao(c, p, 7, ModoExpedicao.Avancar, c.FonteAndar) { LimiteDeAndares = 1 }; int g = 0; bool temSala = false, ev44 = false, resolvida = false;
                while (!s.Encerrada && g++ < 60000)
                {
                    s.Tick(); if (!temSala) temSala = s.Salas.Contains(TipoSala.FonteEnvenenada);
                    foreach (var ev in s.Eventos) { if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 44) ev44 = true; if (ev.Tipo == TipoEvento.SalaResolvida && ev.Extra == (int)TipoSala.FonteEnvenenada) resolvida = true; }
                }
                Afirmar(temSala && ev44 && resolvida && s.Resultado.FonteLimpa, "a sala existe no andar " + c.FonteAndar + ", a luta acontece e a fonte fica limpa (" + temSala + ", " + ev44 + ", " + resolvida + ")");
                Progressao.AplicarResultado(p, s.Resultado, c);
                Afirmar(p.MissaoFonte == 3 && Pocoes.Preco(p, false, c) < antes * (1.0 + c.PocaoPrecoPorAndar * p.MaiorAndarConcluido) * 0.85, "missão cumprida: poção mais barata (" + antes + " -> " + Pocoes.Preco(p, false, c) + ")");
                var s2 = new Simulacao(c, p, 8, ModoExpedicao.Avancar, c.FonteAndar) { LimiteDeAndares = 1 };
                Afirmar(!s2.Salas.Contains(TipoSala.FonteEnvenenada), "limpa, a sala não volta");
                p.ChefesDerrotados.Add(1); Ascensao.Ascender(p, c);
                Afirmar(p.MissaoFonte == 0, "a missão é sorteada de novo na vida seguinte");
                var c0 = new ConfigJogo { MissoesDeCenario = false };
                var s0 = new Simulacao(c0, ProgressoCom(EspecieId.Esqueleto), 7, ModoExpedicao.Avancar, c0.FonteAndar);
                Afirmar(!s0.Salas.Contains(TipoSala.FonteEnvenenada), "sem missões de cenário, nada de fonte");
            });

            Teste("missão da Câmara de Ossos: selo com escolha no andar da missão; rompido, esqueletos com dois de elite; vencida, dá um livro de feitiço que o jogador não tinha", () =>
            {
                var c = new ConfigJogo { ObjetosPorSala = 0, MissoesDeCenario = false, ForcarCamara = true, PortasFechadas = false, EmboscadaDeCorredor = false, SalasLigadas = false };
                var p = ProgressoCom(EspecieId.ColossoDeOssos, EspecieId.ColossoDeOssos, EspecieId.Esqueleto, EspecieId.Esqueleto); p.NivelNecromante = 40; p.PoderNecromantico = 300f; foreach (var m in p.Minions) m.Nivel = 18;
                // recusando: a sala fica, nada muda
                var s1 = new Simulacao(c, p, 7, ModoExpedicao.Avancar, c.CamaraAndar) { LimiteDeAndares = 1 }; int g = 0; bool ofereceu = false;
                while (!s1.Encerrada && g++ < 60000) { s1.Tick(); if (s1.BencaosOferecidas != null && s1.BencaosOferecidas[0] == 13) { ofereceu = true; s1.ComandoEscolherBencao(1); } }
                Afirmar(ofereceu && !s1.Resultado.CamaraVencida && s1.Salas.Contains(TipoSala.CamaraDeOssos), "o selo oferece a escolha e, recusado, nada acontece");
                // sem resposta e sem recorde: não abre sozinho
                var s2 = new Simulacao(c, p, 7, ModoExpedicao.Avancar, c.CamaraAndar) { LimiteDeAndares = 1 }; g = 0; while (!s2.Encerrada && g++ < 60000) s2.Tick();
                Afirmar(!s2.Resultado.CamaraVencida, "sem resposta e com recorde baixo, o necromante não rompe o selo");
                // rompendo
                var s3 = new Simulacao(c, p, 7, ModoExpedicao.Avancar, c.CamaraAndar) { LimiteDeAndares = 1 }; g = 0; int elites = 0, esqueletos = 0; bool dentro = false;
                while (!s3.Encerrada && g++ < 60000)
                {
                    s3.Tick(); if (s3.BencaosOferecidas != null && s3.BencaosOferecidas[0] == 13) { s3.ComandoEscolherBencao(0); dentro = true; }
                    if (dentro && !s3.Resultado.CamaraVencida) foreach (var ev in s3.Eventos) if (ev.Tipo == TipoEvento.EntidadeSurgiu && (ev.Especie == EspecieId.Esqueleto || ev.Especie == EspecieId.ArqueiroOssudo)) { esqueletos++; var en = s3.AcharEntidade(ev.Alvo); if (en != null && en.Elite) elites++; }
                }
                Afirmar(s3.Resultado.CamaraVencida && esqueletos >= c.CamaraInimigos && elites >= 2, "rompido o selo, a câmara é vencida (" + esqueletos + " esqueletos, " + elites + " de elite)");
                Afirmar(s3.Resultado.Livros.Count >= 1 && s3.Resultado.Livros.Exists(f => Grimorio.SoPorLivro((FeiticoId)f)), "o prêmio é um livro de feitiço que só existe por livro");
                Progressao.AplicarResultado(p, s3.Resultado, c);
                Afirmar(p.MissaoCamara == 3 && (Grimorio.Nivel(p, FeiticoId.Golem) + Grimorio.Nivel(p, FeiticoId.MuralhaDeFogo) + Grimorio.Nivel(p, FeiticoId.RelampagoEmCadeia) >= 1 || p.LivrosGuardados[(int)FeiticoId.Golem] + p.LivrosGuardados[(int)FeiticoId.MuralhaDeFogo] + p.LivrosGuardados[(int)FeiticoId.RelampagoEmCadeia] >= 1), "missão cumprida e o feitiço novo aprendido (ou guardado à espera de Magia)");
                Afirmar(!new Simulacao(c, p, 9, ModoExpedicao.Avancar, c.CamaraAndar) { LimiteDeAndares = 1 }.Salas.Contains(TipoSala.CamaraDeOssos), "vencida, a sala não volta");
            });

            Teste("comandos do jogador entre dois ticks não perdem eventos: quebrar um objeto pela tela aparece no tick seguinte", () =>
            {
                var c = new ConfigJogo { MissoesDeCenario = false, PortasFechadas = false, EmboscadaDeCorredor = false };
                var p = ProgressoCom(EspecieId.Esqueleto, EspecieId.Esqueleto); bool quebrou = false, viu = false;
                for (ulong sm = 1; sm <= 6 && !viu; sm++)
                {
                    var s = new Simulacao(c, p, sm, ModoExpedicao.Avancar, 3) { ReapresentarEventosDeComandos = true }; int g = 0;
                    while (!s.Encerrada && g++ < 3000 && !viu)
                    {
                        s.Tick();
                        if (quebrou) { foreach (var ev in s.Eventos) if (ev.Tipo == TipoEvento.EfeitoAplicado && ev.Extra == 21) viu = true; quebrou = false; }
                        if (!viu && s.Fase == FaseExpedicao.EmOnda) for (int i = 0; i < s.Objetos.Count; i++) if (s.ComandoQuebrar(i)) { quebrou = true; break; }
                    }
                }
                Afirmar(viu, "o evento de objeto quebrado por toque deveria chegar no tick seguinte");
            });

            Teste("santuários de efeito incerto: o tipo é sorteado, usar revela o efeito, recusar não faz nada, e o Livro guarda o que já se viu", () =>
            {
                Simulacao AteOSantuario(ConfigJogo c, EstadoProgresso p)
                {
                    c.MissoesDeCenario = false; var s = new Simulacao(c, p, 5UL, ModoExpedicao.Avancar, 3) { LimiteDeAndares = 1 }; int g = 0;
                    while (!s.Encerrada && g++ < 20000 && s.BencaosOferecidas == null) s.Tick();
                    Afirmar(s.BencaosOferecidas != null, "o santuário forçado deveria aparecer");
                    return s;
                }
                // Religioso: cura todo mundo; e passa a ser conhecido
                var c6 = new ConfigJogo { ForcarSalaEspecial = (int)TipoSala.Santuario, ForcarSantuario = (int)Bencao.Religioso };
                var p6 = Progressao.NovoJogo(c6); p6.PoderNecromantico = 60f;
                var s6 = AteOSantuario(c6, p6);
                Afirmar(s6.BencaosOferecidas[0] == (int)Bencao.Religioso && s6.BencaosOferecidas[1] == 0, "tipo forçado e ainda desconhecido");
                s6.Necromante.Vida = s6.Necromante.VidaMax * 0.3f; s6.ComandoEscolherBencao(0); s6.Tick();
                Afirmar(s6.BencaoAtual == Bencao.Religioso && s6.Necromante.Vida >= s6.Necromante.VidaMax - 0.01f, "o Santuário Religioso cura");
                Afirmar((s6.Resultado.SantuariosVistos & (1 << (int)Bencao.Religioso)) != 0 && s6.Resultado.SantuariosUsados == 1, "efeito anotado no resultado");
                Progressao.AplicarResultado(p6, s6.Resultado, c6);
                Afirmar((p6.SantuariosVistos & (1 << (int)Bencao.Religioso)) != 0, "o Livro dos Santuários guarda o efeito");
                var s6b = AteOSantuario(c6, p6); Afirmar(s6b.BencaosOferecidas[1] == 1, "da segunda vez o efeito já é conhecido");
                // recusar: nada muda
                s6b.Necromante.Vida = s6b.Necromante.VidaMax * 0.3f; s6b.ComandoEscolherBencao(1); s6b.Tick();
                Afirmar(s6b.BencaoAtual == Bencao.Nenhuma && s6b.Necromante.Vida < s6b.Necromante.VidaMax * 0.5f && s6b.Resultado.SantuariosUsados == 0, "recusar deixa tudo como estava");
                // sem resposta, o necromante usa sozinho (idle)
                var c9 = new ConfigJogo { ForcarSalaEspecial = (int)TipoSala.Santuario, ForcarSantuario = (int)Bencao.Tetrico };
                var p9 = Progressao.NovoJogo(c9); p9.PoderNecromantico = 60f; var s9 = AteOSantuario(c9, p9);
                long ouro = s9.Resultado.Ouro; float vida = s9.Necromante.Vida; int g9 = 0;
                while (s9.BencaosOferecidas != null && g9++ < 200) s9.Tick();
                Afirmar(s9.BencaoAtual == Bencao.Tetrico && s9.Resultado.Ouro > ouro && s9.Necromante.Vida < vida, "o Tétrico cobra vida e paga em ouro, mesmo sem toque");
                // Oculto: o equipamento não gasta nesta descida
                var c5 = new ConfigJogo { ForcarSalaEspecial = (int)TipoSala.Santuario, ForcarSantuario = (int)Bencao.Oculto };
                var p5 = Progressao.NovoJogo(c5); p5.PoderNecromantico = 60f; p5.SemAutoReparo = true;
                var anel = GeradorDeItens.GerarDoTipo(new Rng(3), c5, 1, RaridadeItem.Magico, TipoItem.Anel); Equipamento.Equipar(p5, anel);
                var s5 = AteOSantuario(c5, p5); s5.ComandoEscolherBencao(0); s5.Tick();
                Afirmar(s5.Resultado.SemDesgaste, "o Oculto marca a descida como sem desgaste");
                Progressao.AplicarResultado(p5, s5.Resultado, c5); Afirmar(anel.Desgaste == 0, "e o anel volta inteiro");
            });

            RodarLoot();

            Console.WriteLine();
            Console.WriteLine(_total - _falhas + " de " + _total + " testes passaram.");
            return _falhas;
        }
    }
}
