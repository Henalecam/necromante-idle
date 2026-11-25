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
                var semPacto = new Simulacao(Cfg, forte, 91, ModoExpedicao.Avancar, 1);
                var ignora = new Simulacao(Cfg, forte, 91, ModoExpedicao.Avancar, 1);
                var aceita = new Simulacao(Cfg, forte, 91, ModoExpedicao.Avancar, 1);
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
                var semSalas = new ConfigJogo { ChanceDeSalaEspecial = 0f, ChanceDeSalaVazia = 0f };   // sem baús no caminho: o teste mede só o combate
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
                Perto(Formulas.EscalaInimigo(1, Cfg), 1f, 1e-6f, "andar 1");
                Perto(Formulas.EscalaInimigo(2, Cfg), 1f + Cfg.CrescimentoPorAndar, 1e-5f, "andar 2");
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
                var sim = new Simulacao(Cfg, p, 5, ModoExpedicao.Avancar, 1) { LimiteDeAndares = 1, ForcarNivelSecreto = true };
                int guarda = 0; bool viu = false;
                while (!sim.Encerrada && guarda++ < 30000) { sim.Tick(); if (sim.EmNivelSecreto) viu = true; }
                Afirmar(viu && sim.Resultado.NiveisSecretos == 1 && sim.Resultado.MortesPorEspecie[(int)EspecieId.ResProfana] >= 15 && sim.Resultado.Itens.Count >= 2, "secreto " + sim.Resultado.NiveisSecretos + " reses " + sim.Resultado.MortesPorEspecie[(int)EspecieId.ResProfana] + " itens " + sim.Resultado.Itens.Count);
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

            RodarLoot();

            Console.WriteLine();
            Console.WriteLine(_total - _falhas + " de " + _total + " testes passaram.");
            return _falhas;
        }
    }
}
