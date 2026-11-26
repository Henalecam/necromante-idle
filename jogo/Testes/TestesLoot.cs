using System;
using System.Collections.Generic;
using System.Text.Json;
using Necro.Nucleo;

namespace Necro.Testes
{
    /// <summary>Testes do sistema de loot: geração, inventário, bônus na simulação, lendários, determinismo e save.</summary>
    public static partial class Testes
    {
        // ---- Utilitários de loot ----

        /// <summary>Item "de laboratório": só os afixos pedidos, sem implícito (valores exatos nos testes).</summary>
        private static Item ItemCom(TipoItem tipo, params (AfixoItem afixo, float valor)[] afixos)
        {
            var item = new Item { Tipo = tipo, Raridade = RaridadeItem.Raro, Nivel = 1, Nome = "Item de teste" };
            for (int i = 0; i < afixos.Length; i++) item.Afixos.Add(new AfixoRolado(afixos[i].afixo, afixos[i].valor));
            return item;
        }

        /// <summary>Lendário "puro": só o efeito, sem afixos.</summary>
        private static Item Lendario(EfeitoLendario efeito)
        {
            var d = TabelaItens.Lendario(efeito);
            return new Item { Tipo = d.Tipo, Raridade = RaridadeItem.Lendario, Nivel = 1, Nome = d.Nome, Efeito = efeito };
        }

        private static EstadoProgresso Equipado(EstadoProgresso p, params Item[] itens)
        {
            for (int i = 0; i < itens.Length; i++) Equipamento.Equipar(p, itens[i]);
            return p;
        }

        private static EstadoProgresso SoEsqueletos(int quantos)
        {
            var p = new EstadoProgresso { ComandoMaximo = Cfg.ComandoInicial };
            for (int i = 0; i < quantos; i++)
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Ativo = true });
            return p;
        }

        private static int ContarEventos(Simulacao sim, TipoEvento tipo)
        {
            int n = 0;
            for (int i = 0; i < sim.Eventos.Count; i++) if (sim.Eventos[i].Tipo == tipo) n++;
            return n;
        }

        private static void RodarLoot()
        {
            var json = new JsonSerializerOptions { IncludeFields = true };

            Console.WriteLine("- Loot: geração de itens");
            Teste("Necrópole: custo cresce 1,15x por nível, paga em Ossos e Essência, respeita desbloqueio e nível máximo", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                Afirmar(!Necropole.Melhorar(p, Construcao.Cripta, Cfg), "sem Ossos não deveria melhorar");
                p.Ossos = 100000; p.Essencia = 100000;
                long o0, e0, o1, e1;
                Necropole.Custo(p, Construcao.Cripta, Cfg, out o0, out e0);
                Afirmar(Necropole.Melhorar(p, Construcao.Cripta, Cfg), "deveria melhorar");
                Afirmar(p.Ossos == 100000 - o0 && Necropole.Nivel(p, Construcao.Cripta) == 1, "pagou e subiu de nível");
                Necropole.Custo(p, Construcao.Cripta, Cfg, out o1, out e1);
                Perto(o1 / (float)o0, Cfg.CrescimentoCustoConstrucao, 0.02f, "crescimento do custo");
                Afirmar(!Necropole.Melhorar(p, Construcao.TorreDasAlmas, Cfg), "Torre só abre no andar 5");
                p.MaiorAndarConcluido = 5;
                Afirmar(Necropole.Melhorar(p, Construcao.TorreDasAlmas, Cfg), "Torre aberta no andar 5");
                p.Ossos = long.MaxValue / 4; p.Essencia = long.MaxValue / 4;
                int max = Necropole.Dados(Construcao.AltarDeErguimento, Cfg).NivelMaximo;
                for (int i = 0; i < max + 5; i++) Necropole.Melhorar(p, Construcao.AltarDeErguimento, Cfg);
                Afirmar(Necropole.Nivel(p, Construcao.AltarDeErguimento) == max, "não passa do nível máximo");
            });
            Teste("Necrópole: Cripta dá Comando, Torre amplia o Filactério e a chance de alma, Altar barateia erguer", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                int comando0 = Progressao.ComandoMaximoEfetivo(p, Cfg);
                p.Construcoes[(int)Construcao.Cripta] = 2 * Cfg.NiveisDeCriptaPorComando;
                Afirmar(Progressao.ComandoMaximoEfetivo(p, Cfg) == comando0 + 2, "+2 de Comando");
                p.Construcoes[(int)Construcao.TorreDasAlmas] = 3;
                Afirmar(Necropole.CapacidadeDoFilacterio(p, Cfg) == Cfg.CapacidadeFilacterio + 3 * Cfg.EspacosPorNivelDeTorre, "espaços do Filactério");
                for (int i = 0; i < Cfg.CapacidadeFilacterio + 6; i++)
                    Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Cfg);
                Afirmar(p.Filacterio.Count == Cfg.CapacidadeFilacterio + 6, "o Filactério maior guarda mais almas");
                Perto(Necropole.BonusChanceDeAlma(p, Cfg), 3 * Cfg.ChanceDeAlmaPorNivelDeTorre, 1e-5f, "chance de alma");
                var alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum };
                long semAltar = Necropole.CustoDeErguer(p, alma, Cfg);
                p.Construcoes[(int)Construcao.AltarDeErguimento] = 10;
                Afirmar(Necropole.CustoDeErguer(p, alma, Cfg) < semAltar, "Altar reduz o custo de erguer");
                p.Essencia = 0;
                Afirmar(Progressao.ErguerMinion(p, 0, Cfg) == null && p.Filacterio.Count == Cfg.CapacidadeFilacterio + 6, "sem Essência não ergue e a alma fica");
                p.Essencia = 1000;
                Afirmar(Progressao.ErguerMinion(p, 0, Cfg) != null && p.Essencia < 1000, "com Essência ergue e paga");
            });
            Teste("Ossuário rende mais Ossos; Mesa de Dissecação rende mais Essência; save antigo (3 construções) segue válido", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Construcoes = new int[3];                       // save de antes das construções novas
                Afirmar(Necropole.Nivel(p, Construcao.Ossuario) == 0 && Necropole.MultOssos(p, Cfg) == 1f, "save antigo: nível 0, sem bônus");
                p.MaiorAndarConcluido = 20; p.Ossos = 1000000; p.Essencia = 1000000;
                for (int i = 0; i < 10; i++) Afirmar(Necropole.Melhorar(p, Construcao.Ossuario, Cfg), "melhora o Ossuário");
                for (int i = 0; i < 5; i++) Afirmar(Necropole.Melhorar(p, Construcao.MesaDeDissecacao, Cfg), "melhora a Mesa");
                Afirmar(p.Construcoes.Length == Necropole.TotalDeConstrucoes, "o save cresce para as construções novas");
                p.Ossos = 0; p.Essencia = 0;
                var r = new ResultadoExpedicao { Ossos = 1000 };
                Progressao.AplicarResultado(p, r, Cfg);
                Afirmar(p.Ossos == (long)Math.Round(1000 * (1.0 + 10 * Cfg.OssosPorNivelDeOssuario)), "Ossos com bônus do Ossuário: " + p.Ossos);
                Afirmar(Necropole.GanharEssencia(p, 100, Cfg) == (long)Math.Round(100 * (1.0 + 5 * Cfg.EssenciaPorNivelDeMesa)) && p.Essencia > 100, "Essência com bônus da Mesa");
            });
            Teste("missões: contam, completam, pagam ao resgatar e abrem sistemas em ordem", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                Afirmar(Missoes.Atual(p, Cfg).Tipo == TipoMissao.AlcancarAndar && !Missoes.Completa(p, Cfg), "começa na missão 1, incompleta");
                Afirmar(!Missoes.Resgatar(p, Cfg) && p.MissaoAtual == 0, "não resgata incompleta");
                Afirmar(!Missoes.Aberto(p, Sistema.Exercito, Cfg), "Exército fechado no começo");
                var r = new ResultadoExpedicao { MaiorAndarConcluido = 2 };
                Afirmar(Missoes.Completa(p, Cfg, r), "o resultado em curso já mostra a missão completa");
                Afirmar(!Missoes.Resgatar(p, Cfg), "sem o resultado em curso ainda não resgata");
                r.Almas.Add(new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Fraca });
                long ossos = p.Ossos;
                Afirmar(Missoes.Resgatar(p, Cfg, r) && p.Ossos == ossos + Cfg.Missoes[0].Ossos && p.MissaoAtual == 1 && p.MaiorAndarConcluido == 2, "resgata ao vivo, paga e grava o andar");
                Progressao.AplicarResultado(p, r, Cfg);
                Afirmar(Missoes.Progresso(p, Cfg) == 0, "alma ceifada antes do resgate não conta para a missão seguinte");
                Afirmar(Missoes.Aberto(p, Sistema.Exercito, Cfg) && !Missoes.Aberto(p, Sistema.Necropole, Cfg), "abre o Exército, não a Necrópole");
                var r2 = new ResultadoExpedicao();
                for (int i = 0; i < 2; i++) r2.Almas.Add(new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Fraca });
                Afirmar(Missoes.Progresso(p, Cfg, r2) == 2, "conta almas ao vivo");
                Progressao.AplicarResultado(p, r2, Cfg); Progressao.AplicarResultado(p, r2, Cfg);
                Afirmar(Missoes.Progresso(p, Cfg) == 3 && Missoes.Completa(p, Cfg), "progresso satura na quantidade pedida");
                Afirmar(Missoes.Resgatar(p, Cfg) && p.ProgressoMissao == 0, "contador zera na missão seguinte");
                p.Essencia = 1000; Progressao.GuardarAlma(p, new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Comum }, Cfg);
                Afirmar(Progressao.ErguerMinion(p, p.Filacterio.Count - 1, Cfg) != null && Missoes.Completa(p, Cfg), "erguer conta para a missão de erguer");
                p.MissaoAtual = Cfg.Missoes.Length;
                Afirmar(Missoes.Atual(p, Cfg) == null && !Missoes.Resgatar(p, Cfg), "fim da cadeia não quebra");
            });
            Teste("fusão: três almas iguais viram uma de qualidade acima, herdam afixos, custam Essência", () =>
            {
                var p = Progressao.NovoJogo(Cfg); p.Filacterio.Clear(); p.Essencia = 0;
                p.Filacterio.Add(new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Comum, Afixos = { Afixo.Petreo } });
                p.Filacterio.Add(new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Comum, Afixos = { Afixo.Faminto } });
                p.Filacterio.Add(new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum });
                Afirmar(!Fusao.Pode(p, EspecieId.Zumbi, Qualidade.Comum, Cfg), "duas não bastam");
                p.Filacterio.Add(new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Comum, Afixos = { Afixo.Petreo } });
                Afirmar(!Fusao.Pode(p, EspecieId.Zumbi, Qualidade.Comum, Cfg) && Fusao.Fundir(p, EspecieId.Zumbi, Qualidade.Comum, Cfg) == null, "sem Essência não funde");
                p.Essencia = 100;
                var nova = Fusao.Fundir(p, EspecieId.Zumbi, Qualidade.Comum, Cfg);
                Afirmar(nova != null && nova.Qualidade == Qualidade.Vigorosa && nova.Especie == EspecieId.Zumbi, "qualidade sobe um degrau");
                Afirmar(p.Essencia == 100 - Cfg.EssenciaParaFundir[(int)Qualidade.Comum], "paga a Essência");
                Afirmar(p.Filacterio.Count == 2 && p.Filacterio.Contains(nova), "saem três, entra uma; a de outra espécie fica");
                int nq = (int)Qualidade.Vigorosa;
                Afirmar(nova.Afixos.Count >= Cfg.AfixosMin[nq] && nova.Afixos.Count <= Cfg.AfixosMax[nq], "número de afixos dentro da faixa da qualidade nova");
                foreach (var a in nova.Afixos) Afirmar(a == Afixo.Petreo || a == Afixo.Faminto, "afixo herdado das três: " + a);
                for (int i = 0; i < 3; i++) p.Filacterio.Add(new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Profana });
                Afirmar(!Fusao.Pode(p, EspecieId.Zumbi, Qualidade.Profana, Cfg), "Profana é o teto");
                for (int i = 0; i < 3; i++) p.Filacterio.Add(new Alma { Especie = EspecieId.Diabrete, Qualidade = Qualidade.Ancestral });
                p.Essencia = 1000;
                var prof = Fusao.Fundir(p, EspecieId.Diabrete, Qualidade.Ancestral, Cfg);
                Afirmar(prof != null && prof.Qualidade == Qualidade.Profana && prof.Traco != TracoProfano.Nenhum, "três Ancestrais dão uma Profana com traço");
            });
            Teste("bestiário: mortes acumulam, marcos dão Domínio e o Domínio fortalece os minions da espécie", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                Afirmar(Bestiario.Dominio(p, EspecieId.Esqueleto, Cfg) == 0 && Bestiario.MultMinion(p, EspecieId.Esqueleto, Cfg) == 1f, "começa sem Domínio");
                var r = new ResultadoExpedicao(); r.MortesPorEspecie[(int)EspecieId.Esqueleto] = (int)Cfg.MarcosDoBestiario[0] - 1;
                Progressao.AplicarResultado(p, r, Cfg);
                Afirmar(Bestiario.Dominio(p, EspecieId.Esqueleto, Cfg) == 0 && Bestiario.ProximoMarco(p, EspecieId.Esqueleto, Cfg) == Cfg.MarcosDoBestiario[0], "falta uma morte");
                r = new ResultadoExpedicao(); r.MortesPorEspecie[(int)EspecieId.Esqueleto] = 1;
                Progressao.AplicarResultado(p, r, Cfg);
                Afirmar(Bestiario.Dominio(p, EspecieId.Esqueleto, Cfg) == 1 && Bestiario.Dominio(p, EspecieId.Zumbi, Cfg) == 0, "Domínio 1 só para a espécie caçada");
                p.MortesPorEspecie[(int)EspecieId.Esqueleto] = 999999;
                Afirmar(Bestiario.Dominio(p, EspecieId.Esqueleto, Cfg) == Cfg.MarcosDoBestiario.Length && Bestiario.ProximoMarco(p, EspecieId.Esqueleto, Cfg) == 0, "teto de Domínio");
                var semDominio = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 5, ModoExpedicao.Avancar, 1);
                var comDominio = new Simulacao(Cfg, p, 5, ModoExpedicao.Avancar, 1);
                float v0 = 0, v1 = 0;
                foreach (var e in semDominio.Entidades) if (e.Faccao == Faccao.Minion) { v0 = e.VidaMax; break; }
                foreach (var e in comDominio.Entidades) if (e.Faccao == Faccao.Minion) { v1 = e.VidaMax; break; }
                Perto(v1 / v0, 1f + Cfg.BonusPorDominio * Cfg.MarcosDoBestiario.Length, 0.01f, "vida do esqueleto com Domínio máximo");
                p.MortesPorEspecie = null;
                Afirmar(Bestiario.Mortes(p, EspecieId.Esqueleto) == 0, "save antigo sem bestiário não quebra");
            });
            Teste("talentos: 1 ponto a cada 2 níveis, pré-requisito no ramo, efeito na simulação e redistribuição paga", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                Afirmar(Talentos.Disponiveis(p, Cfg) == 0 && !Talentos.Investir(p, 0, Cfg), "nível 1 não tem ponto");
                p.NivelNecromante = 1 + 5 * Cfg.NiveisPorPontoDeTalento;
                Afirmar(Talentos.Disponiveis(p, Cfg) == 5, "5 pontos");
                Afirmar(!Talentos.PodeInvestir(p, 2, Cfg), "Marcha Fúnebre pede 4 pontos em Ossos");
                for (int i = 0; i < 4; i++) Afirmar(Talentos.Investir(p, 0, Cfg), "investe em Ossatura");
                Afirmar(Talentos.Investir(p, 2, Cfg) && Talentos.Disponiveis(p, Cfg) == 0 && !Talentos.Investir(p, 1, Cfg), "abre o nó seguinte e acaba o saldo");
                var b = Equipamento.CalcularBonus(p, Cfg);
                Perto(b.VidaDosMinions, 4 * Cfg.Talentos[0].PorPonto, 1e-5f, "bônus de vida dos minions");
                var sem = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 5, ModoExpedicao.Avancar, 1);
                var p2 = Progressao.NovoJogo(Cfg); p2.NivelNecromante = 1; p2.Talentos[0] = 4;
                var com = new Simulacao(Cfg, p2, 5, ModoExpedicao.Avancar, 1);
                float v0 = 0, v1 = 0;
                foreach (var e in sem.Entidades) if (e.Faccao == Faccao.Minion) { v0 = e.VidaMax; break; }
                foreach (var e in com.Entidades) if (e.Faccao == Faccao.Minion) { v1 = e.VidaMax; break; }
                Perto(v1 / v0, 1f + 4 * Cfg.Talentos[0].PorPonto, 0.005f, "minion nasce com mais vida");
                p.NivelNecromante = 1 + 39 * Cfg.NiveisPorPontoDeTalento; for (int i = 0; i < 5; i++) Talentos.Investir(p, 1, Cfg);
                for (int i = 0; i < 5; i++) Talentos.Investir(p, 3, Cfg);
                Afirmar(Talentos.Investir(p, 4, Cfg) && Talentos.Investir(p, 4, Cfg) && !Talentos.Investir(p, 4, Cfg), "Senhor da Legião tem máximo 2");
                Afirmar(Progressao.ComandoMaximoEfetivo(p, Cfg) == p.ComandoMaximo + 2, "o Comando sobe com o talento");
                p.Essencia = 0; Afirmar(!Talentos.Redistribuir(p, Cfg), "sem Essência não redistribui");
                long custo = Talentos.CustoDeRedistribuir(p, Cfg); p.Essencia = custo;
                Afirmar(Talentos.Redistribuir(p, Cfg) && p.Essencia == 0 && Talentos.Gastos(p) == 0 && Talentos.Disponiveis(p, Cfg) == 39, "redistribui, paga e devolve os pontos");
                p.Talentos = null; Afirmar(Talentos.Gastos(p) == 0 && Equipamento.CalcularBonus(p, Cfg) != null, "save antigo sem talentos não quebra");
            });
            Teste("inventário em grade: cada item ocupa o seu tamanho, nada se sobrepõe, e cheio é quando não cabe", () =>
            {
                var p = Progressao.NovoJogo(Cfg); var rng = new Rng(31);
                int total = Cfg.LarguraDoInventario * Cfg.AlturaDoInventario;
                int cabem = (Cfg.LarguraDoInventario / 2) * (Cfg.AlturaDoInventario / 3), guardados = 0;
                for (int i = 0; i < cabem + 5; i++)
                    if (Equipamento.GuardarItem(p, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Raro, TipoItem.Peitoral), Cfg) == DestinoDoItem.Guardado) guardados++;
                Afirmar(guardados == cabem && p.Inventario.Count == cabem, "peitorais 2x3: cabem " + cabem + ", guardou " + guardados);
                var anel = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Raro, TipoItem.Anel);
                Afirmar(Equipamento.Cabe(p, anel, Cfg), "sobraram duas fileiras: o anel 1x1 cabe");
                p.Inventario.RemoveAt(0);
                p.Inventario.Add(GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Raro, TipoItem.Cajado));
                p.Inventario.Add(GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Raro, TipoItem.Elmo));
                var xs = new int[p.Inventario.Count]; var ys = new int[p.Inventario.Count];
                Afirmar(Equipamento.Arrumar(p.Inventario, Cfg, xs, ys), "tudo coube depois de trocar um peitoral por cajado + elmo");
                var usado = new bool[total];
                for (int i = 0; i < p.Inventario.Count; i++)
                {
                    int w, h; Equipamento.Tamanho(p.Inventario[i].Tipo, out w, out h);
                    Afirmar(xs[i] >= 0 && xs[i] + w <= Cfg.LarguraDoInventario && ys[i] + h <= Cfg.AlturaDoInventario, "dentro da grade");
                    for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++)
                    {
                        int c = (ys[i] + dy) * Cfg.LarguraDoInventario + xs[i] + dx;
                        Afirmar(!usado[c], "dois itens no mesmo espaço"); usado[c] = true;
                    }
                }
            });
            Teste("sacrifício: minion vira Fragmentos, a regra automática só toca na reserva, e Imbuir sobe a qualidade", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                int n0 = p.Minions.Count;
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Fraca }, Nivel = 11, Ativo = false });
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Vigorosa }, Ativo = false });
                long esperado = (long)Math.Round(Cfg.FragmentosPorQualidade[0] * (1.0 + Cfg.FragmentosPorNivel * 10));
                Afirmar(Sacrificio.Fragmentos(p.Minions[n0], Cfg) == esperado, "Fragmentos crescem com o nível");
                p.SacrificarAbaixoDe = Qualidade.Vigorosa;
                Progressao.AplicarResultado(p, new ResultadoExpedicao(), Cfg);
                Afirmar(p.Minions.Count == n0 + 1 && p.FragmentosDeAlma == esperado, "a regra sacrificou só o Fraco da reserva; os Comuns em campo ficaram");
                foreach (var m in p.Minions) Afirmar(m.Ativo || m.Alma.Qualidade >= Qualidade.Vigorosa, "ninguém em campo foi tocado");
                Afirmar(!Sacrificio.PodeImbuir(p, 0, Cfg), "sem Fragmentos suficientes não imbui");
                p.FragmentosDeAlma = Cfg.FragmentosParaImbuir[(int)Qualidade.Comum];
                int nivel = p.Minions[0].Nivel;
                Afirmar(Sacrificio.Imbuir(p, 0, Cfg) && p.Minions[0].Alma.Qualidade == Qualidade.Vigorosa && p.FragmentosDeAlma == 0 && p.Minions[0].Nivel == nivel, "imbui: sobe um degrau, paga e mantém o nível");
                p.Minions[0].Alma.Qualidade = Qualidade.Ancestral; p.FragmentosDeAlma = 100000;
                Afirmar(Sacrificio.Imbuir(p, 0, Cfg) && p.Minions[0].Alma.Traco != TracoProfano.Nenhum && !Sacrificio.Imbuir(p, 0, Cfg), "Profana ganha traço e é o teto");
                Afirmar(Sacrificio.Sacrificar(p, 99, Cfg) == 0, "índice inválido não faz nada");
            });
            Teste("oferendas gastam Restos por bônus permanente; ritos gastam Sangue e valem por uma expedição", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var rr = new ResultadoExpedicao(); rr.Restos[0] = Cfg.RestosPorOferenda;
                Progressao.AplicarResultado(p, rr, Cfg);
                Afirmar(Oferendas.Nivel(p, 0) == 1 && p.Restos[0] == 0, "oferenda automática ao fim da expedição");
                p.OferendasAutomaticas = false;
                Afirmar(!Oferendas.Ofertar(p, 1, Cfg), "sem Restos não oferta");
                p.Restos[1] = Cfg.RestosPorOferenda;
                long c0 = Oferendas.Custo(p, 1, Cfg);
                Afirmar(Oferendas.Ofertar(p, 1, Cfg) && p.Restos[1] == 0 && Oferendas.Nivel(p, 1) == 1 && Oferendas.Custo(p, 1, Cfg) > c0, "oferta, paga e o custo cresce");
                Perto(Equipamento.CalcularBonus(p, Cfg).DanoDosMinions, Cfg.Oferendas[1].PorNivel, 1e-5f, "bônus de dano dos minions");
                p.Restos[6] = 100000; for (int i = 0; i < 99; i++) Oferendas.Ofertar(p, 6, Cfg);
                p.Restos[6] = long.MaxValue / 4; for (int i = 0; i < 99; i++) Oferendas.Ofertar(p, 6, Cfg);
                Afirmar(Oferendas.Nivel(p, 6) == Cfg.NivelMaximoDaOferenda, "teto de nível");
                p.Ossos = 0; Progressao.AplicarResultado(p, new ResultadoExpedicao { Ossos = 1000 }, Cfg);
                Afirmar(p.Ossos == (long)Math.Round(1000 * (1.0 + Cfg.Oferendas[6].PorNivel * Cfg.NivelMaximoDaOferenda)), "Ossos com a oferenda: " + p.Ossos);

                p.AndarDeAncoragem = 6; p.Sangue = 0;
                Afirmar(!Oferendas.PrepararRito(p, Rito.Pressa, Cfg), "sem Sangue não prepara");
                p.Sangue = 100; int semRito = Progressao.AndarDePartida(p, Cfg);
                Afirmar(Oferendas.PrepararRito(p, Rito.Pressa, Cfg) && p.Sangue == 100 - Oferendas.DadosDoRito(Rito.Pressa, Cfg).Sangue, "prepara e paga");
                Afirmar(Progressao.AndarDePartida(p, Cfg) == semRito + Cfg.AndaresDeRecuo, "Pressa: parte sem recuar");
                var res = ExecutorSemTela.RodarExpedicao(Cfg, p, 3, ModoExpedicao.Avancar, Progressao.AndarDePartida(p, Cfg), 1);
                Afirmar(p.RitoPreparado == Rito.Nenhum && Progressao.AndarDePartida(p, Cfg) == semRito, "o rito some ao partir");
                Oferendas.PrepararRito(p, Rito.Banquete, Cfg);
                var comRito = new Simulacao(Cfg, p, 9, ModoExpedicao.Avancar, 1); Oferendas.ConsumirRito(p);
                var semR = new Simulacao(Cfg, p, 9, ModoExpedicao.Avancar, 1);
                for (int t = 0; t < 600; t++) { comRito.Tick(); semR.Tick(); }
                Afirmar(comRito.RitoDaExpedicao == Rito.Banquete && comRito.Resultado.Ossos > semR.Resultado.Ossos * 1.5, "Banquete: " + comRito.Resultado.Ossos + " contra " + semR.Resultado.Ossos);
                p.Oferendas = null; Afirmar(Oferendas.Nivel(p, 0) == 0 && Equipamento.CalcularBonus(p, Cfg) != null, "save antigo não quebra");
            });
            Teste("item travado nunca é desmanchado; vender tudo respeita a trava e o teto de raridade", () =>
            {
                var p = Progressao.NovoJogo(Cfg); var rng = new Rng(77);
                var comum = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Anel);
                var magico = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Magico, TipoItem.Anel);
                var raro = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Raro, TipoItem.Elmo);
                var travado = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Botas); travado.Bloqueado = true;
                p.Inventario.AddRange(new[] { comum, magico, raro, travado });
                Afirmar(Equipamento.Desmanchar(p, travado, Cfg) == 0 && p.Inventario.Contains(travado), "travado não desmancha");
                int n; long previsto = Equipamento.ValorDeTudo(p, Cfg, RaridadeItem.Magico, out n);
                Afirmar(n == 2 && previsto == Equipamento.ValorEmEssencia(comum, Cfg) + Equipamento.ValorEmEssencia(magico, Cfg), "prévia conta só Comum e Mágico destravados");
                long ganho = Equipamento.DesmancharTudo(p, Cfg, RaridadeItem.Magico, out n);
                Afirmar(n == 2 && ganho == previsto && p.Essencia == previsto, "vendeu os dois e ganhou o previsto");
                Afirmar(p.Inventario.Count == 2 && p.Inventario.Contains(raro) && p.Inventario.Contains(travado), "o Raro e o travado ficaram");
                // inventário cheio só de travados: o item novo é que vira Essência
                p.Inventario.Clear();
                while (true) { var b = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Peitoral); b.Bloqueado = true; if (!Equipamento.Cabe(p, b, Cfg)) break; p.Inventario.Add(b); }
                int antes = p.Inventario.Count;
                var lend = GeradorDeItens.GerarDoTipo(rng, Cfg, 5, RaridadeItem.Raro, TipoItem.Peitoral);
                Afirmar(Equipamento.GuardarItem(p, lend, Cfg) == DestinoDoItem.Desmanchado && p.Inventario.Count == antes, "travados não saem nem para um item melhor");
            });
            Teste("ascensão: só depois da chefe do Ato 1; zera a vida, rende Cinzas, mantém bestiário, missões e itens travados", () =>
            {
                var p = Progressao.NovoJogo(Cfg); var rng = new Rng(4);
                Afirmar(!Ascensao.Pode(p) && Ascensao.Ascender(p, Cfg) == 0, "antes da chefe não ascende");
                p.ChefesDerrotados.Add(1); p.MaiorAndarConcluido = 15; p.AndarDeAncoragem = 16; p.NivelNecromante = 40; p.Ossos = 9999; p.Essencia = 500;
                p.Construcoes[1] = 12; p.Talentos[0] = 5; p.MissaoAtual = 9; p.MortesPorEspecie[1] = 700; p.Oferendas[0] = 3;
                var travado = GeradorDeItens.GerarDoTipo(rng, Cfg, 9, RaridadeItem.Raro, TipoItem.Elmo); travado.Bloqueado = true;
                var solto = GeradorDeItens.GerarDoTipo(rng, Cfg, 9, RaridadeItem.Raro, TipoItem.Anel);
                Equipamento.Equipar(p, travado); p.Inventario.Add(solto);
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Profana }, Nivel = 30, Ativo = true });
                long esperado = Ascensao.CinzasDestaVida(p);
                Afirmar(esperado == 9 && Ascensao.Ascender(p, Cfg) == 9 && p.Cinzas == 9 && p.Ascensoes == 1, "andar 15 rende 9 Cinzas");
                var novo = Progressao.NovoJogo(Cfg);
                Afirmar(p.NivelNecromante == 1 && p.Ossos == 0 && p.Essencia == 0 && p.MaiorAndarConcluido == 0 && p.ChefesDerrotados.Count == 0 && p.AndarDeAncoragem == novo.AndarDeAncoragem, "a vida recomeça");
                Afirmar(p.Construcoes[1] == 0 && p.Talentos[0] == 0 && p.Oferendas[0] == 0 && p.Minions.Count == novo.Minions.Count, "construções, talentos, altares e exército zerados");
                Afirmar(p.MissaoAtual == 9 && p.MortesPorEspecie[1] == 700, "missões e bestiário ficam");
                Afirmar(p.Inventario.Count == 1 && p.Inventario[0] == travado && Equipamento.Obter(p, EspacoEquipamento.Elmo) == null, "só o item travado atravessa, de volta ao inventário");
                Afirmar(!Ascensao.Comprar(p, 1, Cfg) == false && Ascensao.Nivel(p, 1) == 1 && p.Cinzas == 8, "primeiro nível do Legado custa 1");
                Afirmar(Ascensao.Comprar(p, 1, Cfg) && p.Cinzas == 6, "o segundo custa 2");
                Perto(Equipamento.CalcularBonus(p, Cfg).PoderNecromantico, 2 * Cfg.Legados[1].PorNivel, 1e-5f, "Legado de Sangue vira Poder Necromântico");
                p.Cinzas = 0; Afirmar(!Ascensao.Comprar(p, 0, Cfg), "sem Cinzas não compra");
                var sim = new Simulacao(Cfg, p, 3, ModoExpedicao.Avancar, Progressao.AndarDePartida(p, Cfg));
                for (int t = 0; t < 300; t++) sim.Tick();
                Afirmar(sim.Andar >= 1 && sim.Necromante != null, "a vida nova joga normalmente");
                p.Legados = null; Afirmar(Ascensao.Nivel(p, 0) == 0 && Equipamento.CalcularBonus(p, Cfg) != null, "save antigo não quebra");
            });
            Teste("Comando que cai tira de campo os últimos ativos, e a simulação concorda com a Necrópole", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Construcoes[(int)Construcao.Cripta] = 2 * Cfg.NiveisDeCriptaPorComando;
                for (int i = 0; i < 2; i++) { p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum } }); }
                while (Progressao.ComandoEmUso(p, Cfg) < Progressao.ComandoMaximoEfetivo(p, Cfg) && p.Minions.Count < 60)
                { p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum } }); Progressao.Ativar(p, p.Minions.Count - 1, Cfg); }
                p.Construcoes[(int)Construcao.Cripta] = 0;     // o teto cai 2
                Afirmar(Progressao.ComandoEmUso(p, Cfg) > Progressao.ComandoMaximoEfetivo(p, Cfg), "ficou acima do teto");
                Afirmar(Progressao.AjustarAoComando(p, Cfg) == 2 && Progressao.ComandoEmUso(p, Cfg) == Progressao.ComandoMaximoEfetivo(p, Cfg), "saem exatamente os 2 últimos");
                var s = new Simulacao(Cfg, p, 4, ModoExpedicao.Avancar, 1); int campo = 0, ativos = 0;
                foreach (var e in s.Entidades) if (e.Faccao == Faccao.Minion) campo++;
                foreach (var m in p.Minions) if (m.Ativo) ativos++;
                Afirmar(campo == ativos, "em campo " + campo + " de " + ativos);
            });
            Teste("dons das almas variam dentro da faixa, valem na simulação e entram no Poder; a fusão herda o melhor", () =>
            {
                var rng = new Rng(123); float minV = 9, maxV = -9; int diferentes = 0; float ultimo = 99;
                for (int i = 0; i < 400; i++)
                {
                    var a = GeradorDeAlmas.Gerar(rng, Cfg, EspecieId.Esqueleto, Qualidade.Vigorosa);
                    float faixa = Cfg.DomBase + Cfg.DomPorQualidade * 2;
                    Afirmar(a.DomVida <= faixa + 1e-5f && a.DomVida >= -faixa * Cfg.DomParteNegativa - 1e-5f && Math.Abs(a.DomVelocidade) <= faixa, "dom fora da faixa");
                    if (a.DomVida < minV) minV = a.DomVida; if (a.DomVida > maxV) maxV = a.DomVida;
                    if (a.DomVida != ultimo) diferentes++; ultimo = a.DomVida;
                }
                Afirmar(diferentes > 390 && maxV > 0.10f && minV < -0.05f, "os dons variam de verdade: " + minV + " a " + maxV);
                var p = Progressao.NovoJogo(Cfg);
                long antes = Poder.DoMinion(p, p.Minions[0], Cfg);
                p.Minions[0].Alma.DomVida = 0.5f; p.Minions[0].Alma.DomDano = 0.5f;
                long depois = Poder.DoMinion(p, p.Minions[0], Cfg);
                Perto(depois / (float)antes, 1.5f, 0.02f, "Poder acompanha os dons (raiz de 1,5 x 1,5)");
                var s = new Simulacao(Cfg, p, 4, ModoExpedicao.Avancar, 1); var s0 = new Simulacao(Cfg, Progressao.NovoJogo(Cfg), 4, ModoExpedicao.Avancar, 1);
                float v = 0, v0 = 0;
                foreach (var e in s.Entidades) if (e.Faccao == Faccao.Minion) { v = e.VidaMax; break; }
                foreach (var e in s0.Entidades) if (e.Faccao == Faccao.Minion) { v0 = e.VidaMax; break; }
                Perto(v / v0, 1.5f, 0.01f, "o dom de vida vale na simulação");
                Afirmar(Poder.Total(p, Cfg) > Poder.Total(Progressao.NovoJogo(Cfg), Cfg) && Poder.SugeridoParaOAndar(10, Cfg) > Poder.SugeridoParaOAndar(1, Cfg) * 3, "Poder total e sugerido crescem");
                var pf = Progressao.NovoJogo(Cfg); pf.Filacterio.Clear(); pf.Essencia = 1000;
                float[] dv = { 0.01f, 0.09f, -0.02f };
                for (int i = 0; i < 3; i++) pf.Filacterio.Add(new Alma { Especie = EspecieId.Zumbi, Qualidade = Qualidade.Comum, DomVida = dv[i], DomDano = -dv[i] });
                var nova = Fusao.Fundir(pf, EspecieId.Zumbi, Qualidade.Comum, Cfg);
                Perto(nova.DomVida, 0.09f, 1e-5f, "fusão herda o melhor dom de vida"); Perto(nova.DomDano, 0.02f, 1e-5f, "e o melhor de dano");
            });
            Teste("presságios: andares sorteiam condições; fluxo próprio de sorteio; o Ossário rende mais Ossos", () =>
            {
                var vistos = new System.Collections.Generic.HashSet<Pressagio>(); int andares = 0, com = 0;
                for (ulong sem = 1; sem <= 30; sem++)
                {
                    var forte = Progressao.NovoJogo(Cfg); forte.PoderNecromantico = 6f;
                    var s = new Simulacao(Cfg, forte, sem, ModoExpedicao.Avancar, Cfg.PressagioAPartirDoAndar);
                    var comPressagio = new System.Collections.Generic.HashSet<int>(); var todos = new System.Collections.Generic.HashSet<int>();
                    for (int t = 0; t < 5000 && !s.Encerrada; t++)
                    {
                        s.Tick();
                        if (Formulas.AndarDeChefe(s.Andar, Cfg)) { Afirmar(s.PressagioAtual == Pressagio.Nenhum || s.Fase != FaseExpedicao.EmOnda, "andar de chefe não tem presságio"); continue; }
                        if (s.InimigosVivos > 0) { todos.Add(s.Andar); if (s.PressagioAtual != Pressagio.Nenhum) { comPressagio.Add(s.Andar); vistos.Add(s.PressagioAtual); } }
                    }
                    andares += todos.Count; com += comPressagio.Count;
                }
                float fr = com / (float)andares;
                Afirmar(vistos.Count >= 5 && fr > Cfg.ChanceDePressagio * 0.6f && fr < Cfg.ChanceDePressagio * 1.4f, "presságios em " + (fr * 100).ToString("0") + "% dos andares, " + vistos.Count + " tipos vistos");
            });
            Teste("sorte: mais itens e almas, e melhores; sem sorte nada muda", () =>
            {
                var semObjetos = new ConfigJogo { ObjetosPorSala = 0 };   // urnas largam itens sem sorte: fora da conta
                Func<float, ResultadoExpedicao> rodar = sorte =>
                {
                    var p = Progressao.NovoJogo(Cfg); p.PoderNecromantico = 5f; p.Sorte = sorte;
                    var total = new ResultadoExpedicao();
                    for (ulong sem = 1; sem <= 30; sem++)
                    {
                        var r = ExecutorSemTela.RodarExpedicao(semObjetos, p, sem, ModoExpedicao.Avancar, 1, 8);
                        total.Itens.AddRange(r.Itens); total.Almas.AddRange(r.Almas); total.OrbesCaidos += r.OrbesCaidos;
                    }
                    return total;
                };
                var sem0 = rodar(0f); var com = rodar(300f);
                double mi0 = 0, mi1 = 0, ma0 = 0, ma1 = 0;
                foreach (var it in sem0.Itens) mi0 += (int)it.Raridade; foreach (var it in com.Itens) mi1 += (int)it.Raridade;
                foreach (var a in sem0.Almas) ma0 += (int)a.Qualidade; foreach (var a in com.Almas) ma1 += (int)a.Qualidade;
                Afirmar(com.Itens.Count > sem0.Itens.Count * 1.5 && com.OrbesCaidos > sem0.OrbesCaidos * 1.3, "mais quedas: itens " + sem0.Itens.Count + " -> " + com.Itens.Count + ", orbes " + sem0.OrbesCaidos + " -> " + com.OrbesCaidos);
                Afirmar(mi1 / com.Itens.Count > mi0 / sem0.Itens.Count * 1.15 && ma1 / com.Almas.Count > ma0 / sem0.Almas.Count * 1.08, "raridade média " + (mi0 / sem0.Itens.Count) + " -> " + (mi1 / com.Itens.Count) + "; qualidade média " + (ma0 / sem0.Almas.Count) + " -> " + (ma1 / com.Almas.Count) + " (" + sem0.Almas.Count + "/" + com.Almas.Count + " almas)");
            });
            Teste("distribuição de raridade 70 / 22 / 7 / 0,9 / 0,1 % (400 mil sorteios)", () =>
            {
                var rng = new Rng(2024);
                var c = new int[5];
                int n = 400000;
                for (int i = 0; i < n; i++) c[(int)GeradorDeItens.SortearRaridade(rng, Cfg)]++;
                Perto(c[0] / (float)n, 0.70f, 0.005f, "Comum");
                Perto(c[1] / (float)n, 0.22f, 0.005f, "Mágico");
                Perto(c[2] / (float)n, 0.07f, 0.003f, "Raro");
                Perto(c[3] / (float)n, 0.009f, 0.001f, "Lendário");
                Perto(c[4] / (float)n, 0.001f, 0.0004f, "Profano");
                // Com raridade mínima Raro nunca sai abaixo, e as proporções entre as permitidas se mantêm.
                var m = new int[5];
                for (int i = 0; i < 100000; i++) m[(int)GeradorDeItens.SortearRaridade(rng, Cfg, RaridadeItem.Raro)]++;
                Afirmar(m[0] == 0 && m[1] == 0, "raridade mínima Raro não respeitada");
                Perto(m[2] / 100000f, 0.07f / 0.08f, 0.01f, "Raro entre Raro+");
            });
            Teste("nº de afixos por raridade (0 / 1–2 / 3–4 / 4+efeito / 4+conjunto), sem repetição e só da lista do espaço", () =>
            {
                var rng = new Rng(11);
                var vistos = new bool[5, 5];
                var tiposVistos = new bool[TabelaItens.TotalDeTipos];
                for (int i = 0; i < 60000; i++)
                {
                    var raridade = (RaridadeItem)(i % 5);
                    var item = GeradorDeItens.GerarDeRaridade(rng, Cfg, 1 + i % 30, raridade);
                    int n = item.Afixos.Count;
                    Afirmar(item.Raridade == raridade && item.Nivel == 1 + i % 30, "raridade/nível errados");
                    Afirmar(n >= Cfg.AfixosItemMin[(int)raridade] && n <= Cfg.AfixosItemMax[(int)raridade], raridade + " com " + n + " afixos");
                    vistos[(int)raridade, n] = true;
                    tiposVistos[(int)item.Tipo] = true;
                    var possiveis = TabelaItens.AfixosPossiveis(item.Tipo);
                    for (int a = 0; a < n; a++)
                    {
                        var af = item.Afixos[a].Afixo;
                        Afirmar(Array.IndexOf(possiveis, af) >= 0, af + " não pertence à lista de " + item.Tipo);
                        Afirmar(af != item.Implicito.Afixo, "afixo repete o implícito em " + item);
                        for (int b = a + 1; b < n; b++) Afirmar(item.Afixos[b].Afixo != af, "afixo repetido em " + item);
                        if (af == AfixoItem.Comando)
                            Afirmar(item.Tipo == TipoItem.Cajado || item.Tipo == TipoItem.Foice || item.Tipo == TipoItem.Amuleto, "+Comando fora de arma/amuleto: " + item);
                        float min, max;
                        TabelaItens.Faixa(af, item.Nivel, Cfg, out min, out max);
                        Afirmar(item.Afixos[a].Valor >= min - 1e-4f && item.Afixos[a].Valor <= max + 1e-4f, "valor fora da faixa em " + item);
                    }
                    Afirmar(item.Implicito.Afixo == TabelaItens.Implicito(item.Tipo) && item.Implicito.Valor > 0f, "implícito do tipo");
                    Afirmar(!string.IsNullOrEmpty(item.Nome), "item sem nome");
                    Afirmar((item.Efeito != EfeitoLendario.Nenhum) == (raridade == RaridadeItem.Lendario), "efeito único só (e sempre) em Lendário");
                    Afirmar((item.Conjunto != ConjuntoId.Nenhum) == (raridade == RaridadeItem.Profano), "conjunto só (e sempre) em Profano");
                    if (raridade == RaridadeItem.Lendario) Afirmar(item.Tipo == TabelaItens.Lendario(item.Efeito).Tipo && item.Nome == TabelaItens.Lendario(item.Efeito).Nome, "tipo/nome do lendário");
                    if (raridade == RaridadeItem.Profano) Afirmar(Array.IndexOf(TabelaItens.Conjunto(item.Conjunto).Pecas, item.Tipo) >= 0, "peça fora do conjunto");
                }
                Afirmar(vistos[0, 0] && vistos[1, 1] && vistos[1, 2] && vistos[2, 3] && vistos[2, 4] && vistos[3, 4] && vistos[4, 4], "nem todas as contagens apareceram");
                for (int t = 0; t < tiposVistos.Length; t++) Afirmar(tiposVistos[t], "tipo nunca gerado: " + (TipoItem)t);
                // +Comando existe mesmo (é raro, mas aparece).
                int comComando = 0;
                for (int i = 0; i < 4000; i++) if (GeradorDeItens.GerarDoTipo(rng, Cfg, 5, RaridadeItem.Raro, TipoItem.Amuleto).Tem(AfixoItem.Comando)) comComando++;
                Afirmar(comComando > 20 && comComando < 1200, "+Comando deveria ser raro, mas possível: " + comComando + "/4000");
                Afirmar(TabelaItens.Conjuntos.Length == 2 && TabelaItens.Conjuntos[0].Pecas.Length == 3 && TabelaItens.Conjuntos[1].Pecas.Length == 3 && TabelaItens.Lendarios.Length == 6, "2 conjuntos de 3 peças e 6 lendários");
                {   // Garras do Carniceiro: 2 peças = +12% dano dos minions; 3 peças = +8% velocidade e +3% roubo de vida
                    var r2 = new Rng(77); var g = new List<Item>();
                    for (int i = 0; i < 3; i++) { var it = GeradorDeItens.GerarPecaDeConjunto(r2, Cfg, 1, ConjuntoId.GarrasDoCarniceiro, i); it.Afixos.Clear(); it.Implicito = new AfixoRolado(); g.Add(it); }
                    var b1 = Equipamento.CalcularBonus(new List<Item> { g[0] }, Cfg); var b2 = Equipamento.CalcularBonus(new List<Item> { g[0], g[1] }, Cfg); var b3 = Equipamento.CalcularBonus(g, Cfg);
                    Afirmar(b1.DanoDosMinions == 0f && Math.Abs(b2.DanoDosMinions - 0.12f) < 1e-4f && b2.VelAtaqueDosMinions == 0f, "2 peças das Garras: só o dano");
                    Afirmar(Math.Abs(b3.VelAtaqueDosMinions - 0.08f) < 1e-4f && Math.Abs(b3.RouboDeVidaDosMinions - 0.03f) < 1e-4f, "3 peças das Garras: velocidade e roubo de vida");
                }
            });
            Teste("faixas dos afixos crescem com o nível do item (composto, Config.CrescimentoItemPorAndar; taxas crescem devagar e têm teto)", () =>
            {
                float min1, max1, min10, max10, min40, max40;
                TabelaItens.Faixa(AfixoItem.Vida, 1, Cfg, out min1, out max1);
                TabelaItens.Faixa(AfixoItem.Vida, 10, Cfg, out min10, out max10);
                Perto(min10 / min1, MathF.Pow(1f + Cfg.CrescimentoItemPorAndar, 9), 1e-3f, "Vida do item cresce pelo crescimento composto do item (de propósito abaixo do dos inimigos)");
                Perto(max10 / max1, MathF.Pow(1f + Cfg.CrescimentoItemPorAndar, 9), 1e-3f, "crescimento composto do item");
                for (int a = 0; a < TabelaItens.TotalDeAfixos; a++)
                {
                    TabelaItens.Faixa((AfixoItem)a, 1, Cfg, out min1, out max1);
                    TabelaItens.Faixa((AfixoItem)a, 10, Cfg, out min10, out max10);
                    TabelaItens.Faixa((AfixoItem)a, 40, Cfg, out min40, out max40);
                    Afirmar(min1 > 0f && max1 >= min1 && max10 >= max1 && max40 >= max10 && min40 >= min10 && min10 >= min1, "faixa não cresce: " + (AfixoItem)a);
                    if ((AfixoItem)a != AfixoItem.Comando) Afirmar(max10 > max1, "nível 10 deveria ser melhor que nível 1: " + (AfixoItem)a);
                    float teto = TabelaItens.Afixo((AfixoItem)a).Teto;
                    if (teto > 0f) Afirmar(max40 <= teto + 1e-6f, "teto por item estourado: " + (AfixoItem)a);
                }
                TabelaItens.Faixa(AfixoItem.Comando, 1, Cfg, out min1, out max1);
                TabelaItens.Faixa(AfixoItem.Comando, 30, Cfg, out min40, out max40);
                Afirmar(min1 == 1f && max1 == 1f && max40 == 2f, "+Comando: 1, e 2 a partir do nível 30");
                // Na prática: a média de vida dos itens do andar 12 é ~2,58x a do andar 1.
                var rng = new Rng(3);
                double s1 = 0, s12 = 0;
                for (int i = 0; i < 4000; i++)
                {
                    s1 += GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Anel).Implicito.Valor;
                    s12 += GeradorDeItens.GerarDoTipo(rng, Cfg, 12, RaridadeItem.Comum, TipoItem.Anel).Implicito.Valor;
                }
                Perto((float)(s12 / s1), MathF.Pow(1f + Cfg.CrescimentoItemPorAndar, 11), 0.12f, "média rolada no andar 12 / andar 1");
            });
            Teste("nomes em português: tipo + adjetivo concordando + sufixo", () =>
            {
                Afirmar(GeradorDeItens.MontarNome(ItemCom(TipoItem.Cajado, (AfixoItem.DanoDeFeiticos, 0.1f), (AfixoItem.ChanceDeAlma, 0.1f))) == "Cajado Sepulcral do Lamento", "Cajado Sepulcral do Lamento");
                Afirmar(GeradorDeItens.MontarNome(ItemCom(TipoItem.Luvas, (AfixoItem.Vida, 10f))) == "Luvas Vigorosas", "Luvas Vigorosas");
                Afirmar(GeradorDeItens.MontarNome(ItemCom(TipoItem.Foice, (AfixoItem.PoderNecromantico, 0.1f), (AfixoItem.DanoDosMinions, 0.1f), (AfixoItem.Comando, 1f))) == "Foice Profanada da Matança", "Foice Profanada da Matança");
                Afirmar(GeradorDeItens.MontarNome(ItemCom(TipoItem.Botas, (AfixoItem.ResGelo, 0.1f), (AfixoItem.VelocidadeDeCeifa, 0.1f))) == "Botas Gélidas da Colheita", "Botas Gélidas da Colheita");
                Afirmar(GeradorDeItens.MontarNome(ItemCom(TipoItem.Anel)) == "Anel", "Comum leva só o nome do tipo");
                var rng = new Rng(5);
                var magico = GeradorDeItens.GerarDoTipo(rng, Cfg, 3, RaridadeItem.Magico, TipoItem.Grimorio);
                Afirmar(magico.Nome.StartsWith("Grimório ") && magico.Nome == GeradorDeItens.MontarNome(magico), "nome gerado: " + magico.Nome);
            });

            Console.WriteLine("- Loot: inventário e equipamento");
            Teste("Equipar devolve o item que saiu; 2 anéis; Desequipar; tipo errado no espaço é recusado", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var rng = new Rng(8);
                var cajado = GeradorDeItens.GerarDoTipo(rng, Cfg, 2, RaridadeItem.Magico, TipoItem.Cajado);
                var foice = GeradorDeItens.GerarDoTipo(rng, Cfg, 2, RaridadeItem.Raro, TipoItem.Foice);
                var anelA = GeradorDeItens.GerarDoTipo(rng, Cfg, 2, RaridadeItem.Raro, TipoItem.Anel);
                var anelB = GeradorDeItens.GerarDoTipo(rng, Cfg, 2, RaridadeItem.Raro, TipoItem.Anel);
                var anelC = GeradorDeItens.GerarDoTipo(rng, Cfg, 2, RaridadeItem.Raro, TipoItem.Anel);
                p.Inventario.Add(cajado); p.Inventario.Add(foice); p.Inventario.Add(anelA); p.Inventario.Add(anelB); p.Inventario.Add(anelC);

                Afirmar(Equipamento.Equipar(p, cajado) == null, "espaço vazio: não sai nada");
                Afirmar(Equipamento.Obter(p, EspacoEquipamento.Arma) == cajado && p.Inventario.Count == 4 && !p.Inventario.Contains(cajado), "cajado equipado e fora do inventário");
                Afirmar(Equipamento.Equipar(p, foice) == cajado, "a foice ocupa o mesmo espaço e devolve o cajado");
                Afirmar(Equipamento.Obter(p, EspacoEquipamento.Arma) == foice && p.Inventario.Contains(cajado) && p.Inventario.Count == 4, "troca de 1 por 1");
                Afirmar(Equipamento.Equipar(p, anelA) == null && Equipamento.Equipar(p, anelB) == null, "dois anéis cabem");
                Afirmar(Equipamento.Obter(p, EspacoEquipamento.Anel1) == anelA && Equipamento.Obter(p, EspacoEquipamento.Anel2) == anelB, "Anel1 e Anel2");
                Afirmar(Equipamento.Equipar(p, anelC, EspacoEquipamento.Anel2) == anelB && p.Inventario.Contains(anelB), "terceiro anel troca o do espaço escolhido");
                bool recusou = false;
                try { Equipamento.Equipar(p, cajado, EspacoEquipamento.Elmo); } catch (ArgumentException) { recusou = true; }
                Afirmar(recusou && !Equipamento.PodeEquipar(cajado, EspacoEquipamento.Elmo) && Equipamento.PodeEquipar(anelB, EspacoEquipamento.Anel2), "cajado no elmo deveria ser recusado");

                int antes = p.Inventario.Count;
                Afirmar(Equipamento.Desequipar(p, EspacoEquipamento.Arma, Cfg) == foice, "Desequipar devolve o item");
                Afirmar(Equipamento.Obter(p, EspacoEquipamento.Arma) == null && p.Inventario.Count == antes + 1 && p.Inventario.Contains(foice), "foice voltou ao inventário");
                Afirmar(Equipamento.Desequipar(p, EspacoEquipamento.Arma, Cfg) == null, "espaço vazio devolve null");
                while (p.Inventario.Count < Cfg.CapacidadeInventario) p.Inventario.Add(GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Botas));
                Afirmar(Equipamento.Desequipar(p, EspacoEquipamento.Anel1, Cfg) == null && Equipamento.Obter(p, EspacoEquipamento.Anel1) == anelA, "inventário cheio: não desequipa");
            });
            Teste("Desmanchar dá Essência por raridade e nível; inventário de 40 com descarte automático", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var rng = new Rng(21);
                var raro1 = GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Raro, TipoItem.Elmo);
                var raro11 = GeradorDeItens.GerarDoTipo(rng, Cfg, 11, RaridadeItem.Raro, TipoItem.Elmo);
                var lend = GeradorDeItens.GerarLendario(rng, Cfg, 1, EfeitoLendario.MareRubra);
                Afirmar(Equipamento.ValorEmEssencia(raro1, Cfg) == 10 && Equipamento.ValorEmEssencia(raro11, Cfg) == 20 && Equipamento.ValorEmEssencia(lend, Cfg) == 40, "tabela de Essência (10, 20 no nível 11, 40)");
                p.Inventario.Add(raro1); p.Inventario.Add(raro11);
                Afirmar(Equipamento.Desmanchar(p, raro11, Cfg) == 20 && p.Essencia == 20 && p.Inventario.Count == 1, "desmanchar soma Essência e tira do inventário");
                Afirmar(Equipamento.Desmanchar(p, raro11, Cfg) == 0 && p.Essencia == 20, "item que não está no inventário não rende nada");
                Equipamento.Equipar(p, raro1);
                Afirmar(Equipamento.Desmanchar(p, raro1, Cfg) == 0 && Equipamento.Obter(p, EspacoEquipamento.Elmo) == raro1, "item equipado não é desmanchado");

                // Regra "desmanchar Comuns ao pegar".
                p.DesmancharAbaixoDe = RaridadeItem.Magico;
                long ess = p.Essencia;
                Afirmar(Equipamento.GuardarItem(p, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Anel), Cfg) == DestinoDoItem.Desmanchado, "Comum deveria ser desmanchado");
                Afirmar(p.Essencia == ess + 1 && p.Inventario.Count == 0, "Essência do Comum");
                for (int i = 0; i < Cfg.CapacidadeInventario; i++)
                    Afirmar(Equipamento.GuardarItem(p, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Magico, TipoItem.Anel), Cfg) == DestinoDoItem.Guardado, "deveria guardar até encher");
                Afirmar(p.Inventario.Count == 40, "limite de 40");
                ess = p.Essencia;
                Afirmar(Equipamento.GuardarItem(p, lend, Cfg) == DestinoDoItem.GuardadoDesmanchandoOutro, "cheio: Lendário entra no lugar do pior");
                Afirmar(p.Inventario.Count == 40 && p.Inventario.Contains(lend) && p.Essencia == ess + 3, "pior Mágico virou 3 de Essência");
                p.DesmancharAbaixoDe = RaridadeItem.Comum;
                ess = p.Essencia;
                Afirmar(Equipamento.GuardarItem(p, GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Anel), Cfg) == DestinoDoItem.Desmanchado, "cheio: item pior que todos é desmanchado");
                Afirmar(p.Inventario.Count == 40 && p.Essencia == ess + 1, "continua com 40");
            });
            Teste("CalcularBonus é pura e soma só o que está equipado (afixos, implícitos, conjunto 2/3 peças, teto de recarga)", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var vazio = Equipamento.CalcularBonus(p, Cfg);
                Afirmar(vazio.ItensEquipados == 0 && vazio.Vida == 0f && vazio.PoderNecromantico == 0f && vazio.Comando == 0 && !vazio.Tem(EfeitoLendario.MareRubra), "sem itens = tudo zero");
                var anel = ItemCom(TipoItem.Anel, (AfixoItem.DanoDeFeiticos, 0.25f), (AfixoItem.ResFogo, 0.10f));
                anel.Implicito = new AfixoRolado(AfixoItem.Vida, 30f);
                p.Inventario.Add(anel);
                Afirmar(Equipamento.CalcularBonus(p, Cfg).Vida == 0f, "item no inventário não conta");
                Equipado(p, anel, ItemCom(TipoItem.Anel, (AfixoItem.DanoDeFeiticos, 0.15f), (AfixoItem.ReducaoDeRecarga, 0.4f)), ItemCom(TipoItem.Grimorio, (AfixoItem.ReducaoDeRecarga, 0.4f)),
                    Lendario(EfeitoLendario.MareRubra));
                string antes = JsonSerializer.Serialize(p, json);
                var b = Equipamento.CalcularBonus(p, Cfg);
                Afirmar(JsonSerializer.Serialize(p, json) == antes, "CalcularBonus alterou o progresso");
                Perto(b.Vida, 30f, 1e-5f, "implícito"); Perto(b.DanoDeFeiticos, 0.40f, 1e-5f, "soma de aumentos");
                Perto(b.Resistencias[(int)TipoDano.Fogo], 0.10f, 1e-6f, "resistência por tipo");
                Perto(b.ReducaoDeRecarga, Cfg.TetoReducaoDeRecarga, 1e-6f, "teto de 50% de redução de recarga");
                Afirmar(b.ItensEquipados == 4 && b.MareRubra && b.Tem(EfeitoLendario.MareRubra) && !b.CeifeiroFaminto, "efeitos lendários");

                // Conjunto Vestes do Coveiro: 2 peças = +15% Poder; 3 peças = +1 Comando e +20% vida dos minions.
                var q = Progressao.NovoJogo(Cfg);
                var rng = new Rng(4);
                Item[] pecas = new Item[3];
                for (int i = 0; i < 3; i++)
                {
                    pecas[i] = GeradorDeItens.GerarPecaDeConjunto(rng, Cfg, 1, ConjuntoId.VestesDoCoveiro, i);
                    pecas[i].Afixos.Clear(); pecas[i].Implicito.Valor = 0f;
                }
                Equipamento.Equipar(q, pecas[0]);
                Afirmar(Equipamento.CalcularBonus(q, Cfg).PoderNecromantico == 0f, "1 peça não dá bônus");
                Equipamento.Equipar(q, pecas[1]);
                var b2 = Equipamento.CalcularBonus(q, Cfg);
                Perto(b2.PoderNecromantico, 0.15f, 1e-6f, "2 peças"); Afirmar(b2.Comando == 0, "bônus de 3 peças ainda não");
                Equipamento.Equipar(q, pecas[2]);
                var b3 = Equipamento.CalcularBonus(q, Cfg);
                Afirmar(b3.Comando == 1 && b3.PecasDeConjunto[(int)ConjuntoId.VestesDoCoveiro] == 3, "3 peças: +1 Comando");
                Perto(b3.VidaDosMinions, 0.20f, 1e-6f, "3 peças: vida dos minions"); Perto(b3.PoderNecromantico, 0.15f, 1e-6f, "bônus de 2 continua");
                Afirmar(new Simulacao(Cfg, q, 1, ModoExpedicao.Cacar, 1, false).Bonus.Comando == 1, "a simulação enxerga o bônus de conjunto");
            });
            Teste("pontuação e auto-equipar: troca só por item melhor, e o que sai volta ao inventário", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var fraco = ItemCom(TipoItem.Elmo, (AfixoItem.Vida, 12f));
                var forte = ItemCom(TipoItem.Elmo, (AfixoItem.Vida, 24f), (AfixoItem.VidaDosMinions, 0.10f));
                Afirmar(Equipamento.Pontuacao(forte, Cfg) > Equipamento.Pontuacao(fraco, Cfg) && Equipamento.Pontuacao(null, Cfg) == 0f, "pontuação");
                Perto(Equipamento.Pontuacao(fraco, Cfg), 0.5f, 1e-5f, "12 de vida = metade do máximo do nível 1");
                var rng = new Rng(6);
                Afirmar(Equipamento.Pontuacao(GeradorDeItens.GerarDoTipo(rng, Cfg, 20, RaridadeItem.Comum, TipoItem.Anel), Cfg) > Equipamento.Pontuacao(GeradorDeItens.GerarDoTipo(rng, Cfg, 1, RaridadeItem.Comum, TipoItem.Anel), Cfg),
                    "item de nível maior pontua mais");
                var anel1 = ItemCom(TipoItem.Anel, (AfixoItem.Mana, 8f)); var anel2 = ItemCom(TipoItem.Anel, (AfixoItem.Mana, 12f)); var anel3 = ItemCom(TipoItem.Anel, (AfixoItem.Mana, 16f));
                p.Inventario.Add(fraco); p.Inventario.Add(anel1); p.Inventario.Add(anel2);
                Afirmar(Equipamento.AutoEquipar(p, Cfg) == 3 && p.Inventario.Count == 0, "equipa o que há em espaços vazios (os 2 anéis)");
                p.Inventario.Add(forte); p.Inventario.Add(anel3);
                Afirmar(Equipamento.AutoEquipar(p, Cfg) == 2, "duas trocas por itens melhores");
                Afirmar(Equipamento.Obter(p, EspacoEquipamento.Elmo) == forte && p.Inventario.Contains(fraco), "elmo melhor equipado; o antigo no inventário");
                Afirmar(p.Inventario.Contains(anel1) && !p.Inventario.Contains(anel2) && !p.Inventario.Contains(anel3), "o anel trocado é o pior dos dois");
                Afirmar(Equipamento.AutoEquipar(p, Cfg) == 0, "nada mais a trocar");
                Afirmar(Equipamento.PontuacaoEquipada(p, Cfg) > 0f, "pontuação total");
            });

            Console.WriteLine("- Loot: bônus aplicados na simulação");
            Teste("necromante: +vida, +mana, +armadura, resistência, crítico e dano de feitiços (aumento aditivo)", () =>
            {
                var p = Equipado(Progressao.NovoJogo(Cfg),
                    ItemCom(TipoItem.Peitoral, (AfixoItem.Vida, 50f), (AfixoItem.Mana, 30f), (AfixoItem.Armadura, 20f), (AfixoItem.ResFogo, 0.20f)),
                    ItemCom(TipoItem.Cajado, (AfixoItem.DanoDeFeiticos, 0.30f), (AfixoItem.ChanceCritico, 0.10f), (AfixoItem.DanoCritico, 0.50f)),
                    ItemCom(TipoItem.Anel, (AfixoItem.DanoDeFeiticos, 0.20f)));
                var sem = Arena(1, null, 1, false).Necromante;
                var sim = Arena(1, p, 1, false);
                var n = sim.Necromante;
                Perto(n.VidaMax, sem.VidaMax + 50f, 1e-3f, "vida máxima"); Perto(n.Vida, n.VidaMax, 1e-3f, "começa com a vida cheia");
                Perto(n.ManaMax, sem.ManaMax + 30f, 1e-3f, "mana"); Perto(n.Mana, n.ManaMax, 1e-3f, "mana cheia");
                Perto(n.Armadura, sem.Armadura + 20f, 1e-4f, "armadura");
                Perto(n.Resistencias[(int)TipoDano.Fogo], sem.Resistencias[(int)TipoDano.Fogo] + 0.20f, 1e-6f, "resistência a Fogo");
                Perto(n.ChanceCritico, Cfg.CriticoBase + 0.10f, 1e-6f, "chance de crítico"); Perto(n.MultCritico, Cfg.CriticoMultiplicador + 0.50f, 1e-6f, "dano crítico");
                var alvo = Boneco(sim, 1e6f, 1e6f, new Vec2(10, 6));
                // Dano = Base x (1 + Σ aumentos): 100 x (1 + 0,30 + 0,20) = 150.
                Perto(sim.CausarDano(n, alvo, 100f, TipoDano.Gelo, false), 150f, 1e-2f, "dano de feitiços soma nos aumentos");
                Perto(sim.CausarDano(PrimeiroMinion(sim), alvo, 100f, TipoDano.Gelo, false), 100f, 1e-2f, "não vale para os minions");
                // Resistência do equipamento mitiga de verdade.
                float vida = n.Vida;
                sim.CausarDano(null, n, 100f, TipoDano.Fogo, false);
                Perto(vida - n.Vida, 80f, 1e-2f, "20% de resistência a Fogo");
            });
            Teste("minions: Poder Necromântico, vida, dano, velocidade de ataque e roubo de vida do equipamento", () =>
            {
                var semItens = PrimeiroMinion(Arena(1, null, 1, false));
                var p = Equipado(Progressao.NovoJogo(Cfg), ItemCom(TipoItem.Relicario, (AfixoItem.PoderNecromantico, 0.50f)));
                var m = PrimeiroMinion(Arena(1, p, 1, false));
                Perto(m.VidaMax, semItens.VidaMax * 1.5f, 1e-3f, "Poder: vida"); Perto(m.Dano, semItens.Dano * 1.5f, 1e-4f, "Poder: dano"); Perto(m.Armadura, semItens.Armadura * 1.5f, 1e-4f, "Poder: armadura");
                // Soma com o Poder do progresso: (1 + 0,3 + 0,5).
                p.PoderNecromantico = 0.30f;
                Perto(PrimeiroMinion(Arena(1, p, 1, false)).VidaMax, semItens.VidaMax * 1.8f, 1e-3f, "Poder do progresso + do equipamento");

                var q = Equipado(Progressao.NovoJogo(Cfg),
                    ItemCom(TipoItem.Luvas, (AfixoItem.VidaDosMinions, 0.20f), (AfixoItem.DanoDosMinions, 0.50f), (AfixoItem.VelAtaqueDosMinions, 0.10f)),
                    ItemCom(TipoItem.Foice, (AfixoItem.RouboDeVidaDosMinions, 0.03f)));
                var sim = Arena(1, q, 1, false);
                var mq = PrimeiroMinion(sim);
                Perto(mq.VidaMax, semItens.VidaMax * 1.2f, 1e-3f, "vida dos minions"); Perto(mq.Vida, mq.VidaMax, 1e-3f, "vida cheia");
                Perto(mq.VelAtaque, semItens.VelAtaque * 1.1f, 1e-5f, "velocidade de ataque"); Perto(mq.RouboDeVida, 0.03f, 1e-6f, "roubo de vida");
                Perto(mq.Dano, semItens.Dano, 1e-5f, "dano base não muda: o bônus é um 'aumento'");
                var alvo = Boneco(sim, 1e6f, 1e6f, new Vec2(10, 6));
                mq.Vida = mq.VidaMax * 0.5f;
                Perto(sim.CausarDano(mq, alvo, 100f, TipoDano.Gelo, false), 150f, 1e-2f, "dano dos minions: 100 x (1 + 0,5)");
                Perto(mq.Vida, mq.VidaMax * 0.5f + 4.5f, 1e-2f, "roubou 3% de 150");
                Perto(sim.CausarDano(sim.Necromante, alvo, 100f, TipoDano.Gelo, false), 100f, 1e-2f, "não vale para o necromante");
            });
            Teste("+Comando põe mais minions em campo e libera a ativação", () =>
            {
                var p = SoEsqueletos(12);
                Afirmar(ContarMinionsVivos(Arena(1, p, 1, false)) == 10, "sem item: 10 de Comando");
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum } });
                p.Minions[10].Ativo = false; p.Minions[11].Ativo = false;
                Afirmar(!Progressao.PodeAtivar(p, 10, Cfg), "sem item não cabe o 11º");
                Equipado(p, ItemCom(TipoItem.Cajado, (AfixoItem.Comando, 2f)));
                Afirmar(Progressao.ComandoMaximoEfetivo(p, Cfg) == 12, "Comando efetivo = 10 + 2");
                Afirmar(Progressao.Ativar(p, 10, Cfg) && Progressao.Ativar(p, 11, Cfg) && !Progressao.Ativar(p, 12, Cfg), "cabem 12, não 13");
                Afirmar(ContarMinionsVivos(Arena(1, p, 1, false)) == 12, "com +2 de Comando: 12 em campo");
            });
            Teste("redução de recarga, regeneração de mana, velocidade de ceifa e +chance de alma", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                p.Ordens.Add(new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.LancaDeOssos));
                Equipado(p, ItemCom(TipoItem.Grimorio, (AfixoItem.ReducaoDeRecarga, 0.10f), (AfixoItem.RegenMana, 2.5f)));
                var sim = Arena(1, p);
                Boneco(sim, 1e6f, 1e6f, new Vec2(10, 6));
                sim.Tick();
                Afirmar(Tem(sim, TipoEvento.FeiticoLancado), "deveria lançar a Lança de Ossos");
                Perto(sim.RecargaDoFeitico(FeiticoId.LancaDeOssos), Cfg.Feitico(FeiticoId.LancaDeOssos).Recarga * 0.9f, 1e-4f, "recarga 10% menor");
                float mana = sim.Necromante.Mana;
                sim.Tick();
                Perto(sim.Necromante.Mana - mana, (Cfg.RegenManaPorSegundo + 2.5f) * Cfg.DT, 1e-4f, "regeneração de mana somada");

                // Velocidade de ceifa +50%: 1,5 s / 1,5 = 1,0 s = 10 ticks (sem item: 15).
                var q = Equipado(Progressao.NovoJogo(Cfg), ItemCom(TipoItem.Foice, (AfixoItem.VelocidadeDeCeifa, 0.50f)));
                var sim2 = Arena(7, q);
                sim2.CriarOrbe(new Vec2(12, 6), GeradorDeAlmas.Gerar(sim2.Rng, Cfg, EspecieId.Zumbi));
                sim2.Tick();
                int inicio = sim2.TickAtual, fim = -1;
                for (int t = 0; t < 40 && fim < 0; t++) { sim2.Tick(); if (Tem(sim2, TipoEvento.AlmaCeifada)) fim = sim2.TickAtual; }
                Afirmar(fim - inicio == 10, "ceifa deveria levar 10 ticks, levou " + (fim - inicio));

                // +chance de alma multiplica a chance de drop: 2% x (1 + 49) = 100%.
                var r = Equipado(Progressao.NovoJogo(Cfg), ItemCom(TipoItem.Amuleto, (AfixoItem.ChanceDeAlma, 49f)));
                var sim3 = Arena(77, r, 1, false);
                for (int i = 0; i < 50; i++)
                {
                    var e = Boneco(sim3, 100f, 10f, new Vec2(10, 6));
                    sim3.CausarDano(null, e, 20f, TipoDano.Fisico, false);
                }
                Afirmar(sim3.Orbes.Count == 50, "todo inimigo deveria soltar alma: " + sim3.Orbes.Count + "/50");
            });

            Console.WriteLine("- Loot: drop de itens");
            Teste("chance de item: comum 3%, elite 35% (medido na simulação); nível = andar; evento ItemCaiu", () =>
            {
                var sim = Arena(77, null, 7, false);
                int n = 30000;
                for (int i = 0; i < n; i++)
                {
                    var e = Boneco(sim, 100f, 10f, new Vec2(10, 6));
                    sim.CausarDano(null, e, 20f, TipoDano.Fisico, false);
                    sim.Orbes.Clear(); sim.Cadaveres.Clear(); sim.Entidades.Remove(e); sim.Eventos.Clear();
                }
                Perto(sim.Resultado.Itens.Count / (float)n, 0.03f, 0.004f, "drop de inimigo comum");
                for (int i = 0; i < sim.Resultado.Itens.Count; i++) Afirmar(sim.Resultado.Itens[i].Nivel == 7, "nível do item = andar em que caiu");
                int antes = sim.Resultado.Itens.Count; n = 12000;
                int eventos = 0;
                for (int i = 0; i < n; i++)
                {
                    var e = Boneco(sim, 100f, 10f, new Vec2(11, 5), true);
                    sim.CausarDano(null, e, 20f, TipoDano.Fisico, false);
                    EventoSim ev;
                    if (Achar(sim, TipoEvento.ItemCaiu, out ev))
                    {
                        eventos++;
                        var item = sim.Resultado.Itens[ev.Alvo];
                        Afirmar(ev.Alvo == sim.Resultado.Itens.Count - 1 && ev.Extra == (int)item.Raridade && ev.Origem == e.Id && (int)ev.Valor == 7, "dados do evento ItemCaiu");
                        Perto(ev.Pos.X, 11f, 1e-4f, "posição X do evento"); Perto(ev.Pos.Y, 5f, 1e-4f, "posição Y do evento");
                    }
                    sim.Orbes.Clear(); sim.Cadaveres.Clear(); sim.Entidades.Remove(e); sim.Eventos.Clear();
                }
                Perto((sim.Resultado.Itens.Count - antes) / (float)n, 0.35f, 0.015f, "drop de elite");
                Afirmar(eventos == sim.Resultado.Itens.Count - antes, "um evento por item");
                Afirmar(sim.Orbes.Count == 0, "item não vira orbe: cai direto no resultado");
            });
            Teste("chefe garante 2 itens; na primeira vitória um deles é no mínimo Raro", () =>
            {
                for (ulong s = 1; s <= 40; s++)
                {
                    var sim = Arena(s, null, 10, false);
                    var chefe = Boneco(sim, 100f, 10f, new Vec2(10, 6), false, true);
                    sim.CausarDano(null, chefe, 20f, TipoDano.Fisico, false);
                    Afirmar(sim.Resultado.Itens.Count == 2 && ContarEventos(sim, TipoEvento.ItemCaiu) == 2, "chefe deveria soltar 2 itens");
                    Afirmar(sim.Resultado.Itens[0].Raridade >= RaridadeItem.Raro, "primeira vitória: raridade mínima Raro (semente " + s + ")");
                    Afirmar(sim.Resultado.Itens[0].Nivel == 10 && sim.Resultado.Itens[1].Nivel == 10, "nível = andar do chefe");
                }
                // Chefe já derrotado (no progresso): continua garantindo 2, mas sem o mínimo de Raro.
                var p = Progressao.NovoJogo(Cfg); p.ChefesDerrotados.Add(1);
                var sim2 = Arena(5, p, 10, false);
                int n = 3000, raros = 0;
                for (int i = 0; i < n; i++)
                {
                    var chefe = Boneco(sim2, 100f, 10f, new Vec2(10, 6), false, true);
                    sim2.CausarDano(null, chefe, 20f, TipoDano.Fisico, false);
                    if (sim2.Resultado.Itens[i * 2].Raridade >= RaridadeItem.Raro) raros++;
                    sim2.Orbes.Clear(); sim2.Cadaveres.Clear(); sim2.Entidades.Remove(chefe); sim2.Eventos.Clear();
                }
                Afirmar(sim2.Resultado.Itens.Count == 2 * n, "sempre 2 itens");
                Perto(raros / (float)n, 0.08f, 0.02f, "repetição: raridade normal (8% Raro ou melhor)");
            });
            Teste("itens do resultado vão para o inventário, mas não são equipados sozinhos", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var resumo = ExecutorSemTela.SimularOffline(Cfg, p, 3600f, ModoExpedicao.Avancar);
                Afirmar(resumo.ItensEncontrados > 0, "1 hora deveria render itens");
                Afirmar(p.Inventario.Count > 0 && p.Inventario.Count <= Math.Min(resumo.ItensEncontrados, Cfg.CapacidadeInventario) && Equipamento.Arrumar(p.Inventario, Cfg, null, null), "inventário: " + p.Inventario.Count + " de " + resumo.ItensEncontrados + ", tudo dentro da grade");
                Afirmar(Equipamento.CalcularBonus(p, Cfg).ItensEquipados == 0, "nada é equipado automaticamente");
            });

            Console.WriteLine("- Loot: lendários");
            Teste("Foice do Ceifeiro Faminto: cada alma ceifada cura todos os minions em 5% da vida máxima", () =>
            {
                for (int caso = 0; caso < 2; caso++)
                {
                    var p = Progressao.NovoJogo(Cfg);
                    if (caso == 1) Equipado(p, Lendario(EfeitoLendario.CeifeiroFaminto));
                    var sim = Arena(7, p);
                    for (int i = 0; i < sim.Entidades.Count; i++) if (sim.Entidades[i].Faccao == Faccao.Minion) sim.Entidades[i].Vida = sim.Entidades[i].VidaMax * 0.5f;
                    sim.CriarOrbe(new Vec2(12, 6), GeradorDeAlmas.Gerar(sim.Rng, Cfg, EspecieId.Zumbi));
                    int curas = 0;
                    for (int t = 0; t < 30; t++) { sim.Tick(); curas += ContarEventos(sim, TipoEvento.Cura); }
                    Afirmar(sim.Resultado.Almas.Count == 1, "a alma deveria ter sido ceifada");
                    for (int i = 0; i < sim.Entidades.Count; i++)
                    {
                        var m = sim.Entidades[i];
                        if (m.Faccao == Faccao.Minion) Perto(m.Vida, m.VidaMax * (caso == 1 ? 0.55f : 0.50f), 1e-2f, caso == 1 ? "curado em 5%" : "sem a foice não cura");
                    }
                    Afirmar(curas == (caso == 1 ? 3 : 0), "eventos de cura: " + curas);
                }
            });
            Teste("Grimório da Mãe Carniça: Explosão de Cadáver deixa um Zumbi temporário (10 s) onde explodiu", () =>
            {
                var p = Progressao.NovoJogo(Cfg); p.Minions.Clear();
                p.Ordens.Add(new Ordem(CondicaoOrdem.CadaveresPeloMenos, 2f, AcaoNecromante.ExplosaoDeCadaver));
                Equipado(p, Lendario(EfeitoLendario.MaeCarnica));
                var sim = Arena(1, p);
                sim.NecromanteAtivo = false;
                for (int i = 0; i < 2; i++) { var morto = Boneco(sim, 100f, 10f, new Vec2(10 + i * 0.5f, 6)); sim.CausarDano(null, morto, 20f, TipoDano.Fisico, false); }
                var perto = Boneco(sim, 1e6f, 1e6f, new Vec2(10.5f, 6.5f));
                sim.NecromanteAtivo = true;
                sim.Tick();
                EventoSim explosao;
                Afirmar(Achar(sim, TipoEvento.Explosao, out explosao), "deveria ter explodido");
                var zumbi = PrimeiroMinion(sim);
                Afirmar(zumbi != null && zumbi.Viva && zumbi.Temporario && zumbi.Especie == EspecieId.Zumbi && ContarMinionsVivos(sim) == 1, "um Zumbi temporário deveria surgir");
                Afirmar(Vec2.Dist(zumbi.Pos, explosao.Pos) < 0.5f, "o Zumbi nasce onde explodiu: " + zumbi.Pos + " vs " + explosao.Pos);
                Afirmar(Tem(sim, TipoEvento.EntidadeSurgiu) && zumbi.IndiceMinion == -1, "evento de surgimento; não é um minion do progresso");
                Perto(zumbi.VidaMax, TabelaEspecies.Obter(EspecieId.Zumbi).Vida, 1e-3f, "Zumbi Comum no nível do necromante (1)");
                float vidaAntes = perto.Vida;
                for (int t = 0; t < 94; t++) sim.Tick();
                Afirmar(zumbi.Viva && perto.Vida < vidaAntes, "aos 9,5 s ainda vivo e lutando");
                for (int t = 0; t < 10; t++) sim.Tick();
                Afirmar(!zumbi.Viva && ContarMinionsVivos(sim) == 0, "aos 10,5 s já deveria ter sumido");
                for (int i = 0; i < sim.Cadaveres.Count; i++) Afirmar(!sim.Cadaveres[i].DeMinion, "temporário não deixa cadáver");
                Afirmar(sim.Resultado.MinionsMortos == 0, "temporário não conta como minion perdido");
                // Morto em combate também não deixa cadáver (não pode voltar por Reerguer).
                var z2 = sim.CriarZumbiTemporario(new Vec2(5, 5), 10f);
                int cadaveres = sim.Cadaveres.Count;
                for (int i = 0; i < 4 && z2.Viva; i++) sim.CausarDano(null, z2, z2.VidaMax * 0.4f, TipoDano.Sombra, false);
                Afirmar(!z2.Viva && sim.Cadaveres.Count == cadaveres && sim.Resultado.MinionsMortos == 0, "zumbi temporário morto não vira cadáver");
                // Sem o grimório, a mesma explosão não cria nada (teste antigo de Explosão de Cadáver cobre o resto).
                Afirmar(!Arena(1, Progressao.NovoJogo(Cfg)).Bonus.MaeCarnica, "sem item, sem efeito");
            });
            Teste("Anel do Rei Solitário: +100% de dano do necromante só com 0 minions vivos", () =>
            {
                var p = Equipado(Progressao.NovoJogo(Cfg), Lendario(EfeitoLendario.ReiSolitario));
                var sim = Arena(1, p, 1, false);
                var alvo = Boneco(sim, 1e7f, 1e7f, new Vec2(18, 6));
                Perto(sim.CausarDano(sim.Necromante, alvo, 100f, TipoDano.Gelo, false), 100f, 1e-2f, "com minions vivos: dano normal");
                for (int i = 0; i < sim.Entidades.Count; i++)
                {
                    var m = sim.Entidades[i];
                    if (m.Faccao != Faccao.Minion) continue;
                    if (ContarMinionsVivos(sim) == 1) Perto(sim.CausarDano(sim.Necromante, alvo, 100f, TipoDano.Gelo, false), 100f, 1e-2f, "com 1 minion vivo ainda é normal");
                    while (m.Viva) sim.CausarDano(null, m, m.VidaMax * 0.4f, TipoDano.Sombra, false);
                }
                Afirmar(ContarMinionsVivos(sim) == 0, "preparação: todos mortos");
                Perto(sim.CausarDano(sim.Necromante, alvo, 100f, TipoDano.Gelo, false), 200f, 1e-2f, "sozinho: +100% ('mais')");
                Perto(sim.CausarDano(null, alvo, 100f, TipoDano.Gelo, false), 100f, 1e-2f, "só vale para o dano do necromante");
                // Dois anéis iguais não acumulam; sem o anel não há bônus.
                var doisAneis = new EstadoProgresso { ComandoMaximo = 10 };
                Equipado(doisAneis, Lendario(EfeitoLendario.ReiSolitario), Lendario(EfeitoLendario.ReiSolitario));
                var sim2 = Arena(1, doisAneis, 1, false);
                Perto(sim2.CausarDano(sim2.Necromante, Boneco(sim2, 1e7f, 1e7f, new Vec2(18, 6)), 100f, TipoDano.Gelo, false), 200f, 1e-2f, "dois anéis: continua +100%");
                var sim3 = Arena(1, new EstadoProgresso { ComandoMaximo = 10 }, 1, false);
                Perto(sim3.CausarDano(sim3.Necromante, Boneco(sim3, 1e7f, 1e7f, new Vec2(18, 6)), 100f, TipoDano.Gelo, false), 100f, 1e-2f, "sem anel e sem minions: normal");
                // Na prática: a Lança de Ossos do necromante solitário dobra.
                var solo = new EstadoProgresso { ComandoMaximo = 10 };
                solo.Ordens.Add(new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.LancaDeOssos));
                float[] danos = new float[2];
                for (int caso = 0; caso < 2; caso++)
                {
                    if (caso == 1) Equipado(solo, Lendario(EfeitoLendario.ReiSolitario));
                    var s4 = Arena(3, solo);
                    s4.Necromante.ChanceCritico = 0f;
                    var b = Boneco(s4, 1e6f, 1e6f, new Vec2(8, 6));
                    for (int t = 0; t < 10; t++) s4.Tick();
                    danos[caso] = 1e6f - b.Vida;
                }
                Afirmar(danos[0] > 0f, "a lança deveria acertar");
                Perto(danos[1], danos[0] * 2f, 0.05f, "Lança de Ossos em dobro");
            });
            Teste("Coroa da Legião Muda: Pequenos custam 0,5 de Comando (total arredondado para cima) e têm -20% de vida", () =>
            {
                var p = SoEsqueletos(21);
                Afirmar(ContarMinionsVivos(Arena(1, p, 1, false)) == 10 && Progressao.ComandoEmUso(p, Cfg) == 21, "sem a coroa: 1 de Comando cada");
                Equipado(p, Lendario(EfeitoLendario.LegiaoMuda));
                var sim = Arena(1, p, 1, false);
                Afirmar(ContarMinionsVivos(sim) == 20, "com a coroa cabem 20 Pequenos em 10 de Comando: " + ContarMinionsVivos(sim));
                // 20 Mortos-Vivos também ganham o bônus de família (+25% de vida), que multiplica com a penalidade da coroa.
                Perto(PrimeiroMinion(sim).VidaMax, TabelaEspecies.Obter(EspecieId.Esqueleto).Vida * (1f + Cfg.FamiliaBonus5) * 0.8f, 1e-3f, "-20% de vida");
                Perto(PrimeiroMinion(sim).VidaMax, PrimeiroMinion(Arena(1, SoEsqueletos(10), 1, false)).VidaMax * 0.8f, 1e-3f, "-20% em relação ao mesmo exército sem a coroa");
                Perto(PrimeiroMinion(sim).Dano, TabelaEspecies.Obter(EspecieId.Esqueleto).Dano, 1e-4f, "dano não muda");
                Afirmar(Progressao.ComandoEmUso(p, Cfg) == 11, "21 x 0,5 = 10,5 -> 11");
                var q = Equipado(SoEsqueletos(19), Lendario(EfeitoLendario.LegiaoMuda));
                Afirmar(Progressao.ComandoEmUso(q, Cfg) == 10, "19 x 0,5 = 9,5 -> 10 (arredonda para cima)");
                q.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum } });
                q.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum } });
                Afirmar(Progressao.Ativar(q, 19, Cfg) && !Progressao.Ativar(q, 20, Cfg), "o 20º cabe (10,0); o 21º não (10,5 -> 11)");
                // Médio continua custando 3 e com a vida inteira: Acólito (3) + 14 Pequenos (7) = 10.
                var misto = new EstadoProgresso { ComandoMaximo = 10 };
                misto.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.AcolitoCaido, Qualidade = Qualidade.Comum }, Ativo = true });
                for (int i = 0; i < 16; i++) misto.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Ativo = true });
                Equipado(misto, Lendario(EfeitoLendario.LegiaoMuda));
                var sim2 = Arena(1, misto, 1, false);
                Afirmar(ContarMinionsVivos(sim2) == 15, "1 Médio + 14 Pequenos: " + ContarMinionsVivos(sim2));
                Perto(PrimeiroMinion(sim2).VidaMax, TabelaEspecies.Obter(EspecieId.AcolitoCaido).Vida, 1e-3f, "Médio não perde vida");
            });
            Teste("Manto do Último Suspiro: uma vez por andar, dano fatal deixa 1 de vida e sacrifica o minion de maior vida", () =>
            {
                var p = Equipado(Progressao.NovoJogo(Cfg), Lendario(EfeitoLendario.UltimoSuspiro));
                var sim = Arena(1, p, 1, false);
                var n = sim.Necromante;
                Entidade maior = null; int k = 0;
                for (int i = 0; i < sim.Entidades.Count; i++)
                {
                    var m = sim.Entidades[i];
                    if (m.Faccao != Faccao.Minion) continue;
                    if (k == 1) maior = m; else m.Vida = m.VidaMax * 0.6f; // o do meio é o de maior vida
                    k++;
                }
                Afirmar(sim.UltimoSuspiroDisponivel, "disponível no começo");
                sim.CausarDano(null, n, n.Vida * 0.5f, TipoDano.Sombra, false);
                Afirmar(sim.UltimoSuspiroDisponivel && ContarMinionsVivos(sim) == 3, "dano não fatal não gasta o manto");
                sim.CausarDano(null, n, 100000f, TipoDano.Sombra, false);
                Afirmar(n.Viva && n.Vida == 1f, "deveria ficar com 1 de vida, ficou com " + n.Vida);
                Afirmar(!maior.Viva && ContarMinionsVivos(sim) == 2, "o minion de maior vida deveria ser sacrificado");
                Afirmar(sim.Cadaveres.Count == 0 && sim.Resultado.MinionsMortos == 1, "sacrifício não deixa cadáver e conta como minion perdido");
                bool anunciou = false;
                for (int i = 0; i < sim.Eventos.Count; i++) if (sim.Eventos[i].Tipo == TipoEvento.EfeitoAplicado && sim.Eventos[i].Extra == 5 && sim.Eventos[i].Origem == maior.Id) anunciou = true;
                Afirmar(anunciou && !sim.UltimoSuspiroDisponivel, "evento do efeito; gasto neste andar");
                sim.Tick();
                Afirmar(!sim.Encerrada, "a expedição continua");
                sim.CausarDano(null, n, 100000f, TipoDano.Sombra, false);
                Afirmar(!n.Viva && ContarMinionsVivos(sim) == 2, "o segundo golpe fatal no mesmo andar mata");

                // Sem minion vivo para sacrificar, o manto não salva.
                var solo = Equipado(new EstadoProgresso { ComandoMaximo = 10 }, Lendario(EfeitoLendario.UltimoSuspiro));
                var sim2 = Arena(1, solo, 1, false);
                sim2.CausarDano(null, sim2.Necromante, 100000f, TipoDano.Sombra, false);
                Afirmar(!sim2.Necromante.Viva, "sem minions não há quem sacrificar");
                // Sem o manto, morre normalmente.
                var sim3 = Arena(1, null, 1, false);
                sim3.CausarDano(null, sim3.Necromante, 100000f, TipoDano.Sombra, false);
                Afirmar(!sim3.Necromante.Viva && ContarMinionsVivos(sim3) == 3, "sem o manto: morte normal");

                // "Uma vez por andar": volta a ficar disponível quando o próximo andar começa.
                var forte = SoEsqueletos(8);
                for (int i = 0; i < forte.Minions.Count; i++) forte.Minions[i].Nivel = 20;
                Equipado(forte, Lendario(EfeitoLendario.UltimoSuspiro));
                var sim4 = new Simulacao(Cfg, forte, 9, ModoExpedicao.Avancar, 1);
                sim4.CausarDano(null, sim4.Necromante, 100000f, TipoDano.Sombra, false);
                Afirmar(sim4.Necromante.Viva && !sim4.UltimoSuspiroDisponivel, "gasto no andar 1");
                for (int t = 0; t < 3000 && sim4.Andar < 2; t++) { sim4.Tick(); sim4.Necromante.Vida = sim4.Necromante.VidaMax; }
                for (int t = 0; t < 60; t++) sim4.Tick();
                Afirmar(sim4.Andar >= 2 && sim4.UltimoSuspiroDisponivel, "deveria recarregar no andar seguinte (andar " + sim4.Andar + ")");
            });
            Teste("Amuleto da Maré Rubra: overkill espalha 30% do dano excedente aos inimigos a até 2 unidades", () =>
            {
                for (int caso = 0; caso < 2; caso++)
                {
                    var p = new EstadoProgresso { ComandoMaximo = 10 };
                    if (caso == 1) Equipado(p, Lendario(EfeitoLendario.MareRubra));
                    var sim = Arena(1, p, 1, false);
                    var alvo = Boneco(sim, 100f, 100f, new Vec2(10, 6));
                    var perto = Boneco(sim, 1e4f, 1e4f, new Vec2(11, 6));
                    var longe = Boneco(sim, 1e4f, 1e4f, new Vec2(13.5f, 6));
                    // 300 de dano em 100 de vida: overkill com 200 de excedente -> 60 nos vizinhos.
                    sim.CausarDano(null, alvo, 300f, TipoDano.Gelo, false);
                    Afirmar(!alvo.Viva && sim.Resultado.Overkills == 1, "preparação: overkill");
                    sim.Tick();
                    Perto(1e4f - perto.Vida, caso == 1 ? 60f : 0f, 1e-2f, caso == 1 ? "30% de 200 no inimigo a 1 unidade" : "sem o amuleto nada espalha");
                    Perto(longe.Vida, 1e4f, 1e-3f, "inimigo a mais de 2 unidades não sofre");
                    if (caso == 1) Afirmar(Tem(sim, TipoEvento.Explosao), "evento de explosão para a tela");
                    // Morte sem overkill não espalha.
                    var fraco = Boneco(sim, 100f, 10f, perto.Pos + new Vec2(0.5f, 0f));
                    float antes = perto.Vida;
                    sim.CausarDano(null, fraco, 30f, TipoDano.Gelo, false);
                    sim.Tick();
                    Perto(perto.Vida, antes, 1e-3f, "sem overkill, sem maré");
                }
                // Em cadeia: o respingo que mata com overkill espalha de novo; vale para overkill de minions também.
                var q = Equipado(Progressao.NovoJogo(Cfg), Lendario(EfeitoLendario.MareRubra));
                var s2 = Arena(1, q, 1, false);
                var a = Boneco(s2, 100f, 100f, new Vec2(10, 6));
                var b = Boneco(s2, 100f, 100f, new Vec2(11, 6));
                var c = Boneco(s2, 1e4f, 1e4f, new Vec2(12.5f, 6));
                s2.CausarDano(PrimeiroMinion(s2), a, 1100f, TipoDano.Gelo, false); // excedente 1000 -> 300 em b (overkill, excedente 200) -> 60 em c
                s2.Tick();
                Afirmar(!b.Viva && s2.Resultado.Overkills == 2, "b deveria morrer de overkill pelo respingo");
                Perto(1e4f - c.Vida, 60f, 1e-2f, "c só recebe o respingo de b (está longe de a)");
            });

            Console.WriteLine("- Loot: determinismo e save");
            Teste("mesma semente + mesmo equipamento -> mesmo hash e mesmos itens caídos; itens não equipados não mudam o combate", () =>
            {
                Func<EstadoProgresso> montar = () =>
                {
                    var p = Progressao.NovoJogo(Cfg);
                    var rng = new Rng(99);
                    for (int i = 0; i < 30; i++) p.Inventario.Add(GeradorDeItens.Gerar(rng, Cfg, 3, RaridadeItem.Magico));
                    p.Inventario.Add(GeradorDeItens.GerarLendario(rng, Cfg, 3, EfeitoLendario.MaeCarnica));
                    p.Inventario.Add(GeradorDeItens.GerarLendario(rng, Cfg, 3, EfeitoLendario.MareRubra));
                    p.Inventario.Add(GeradorDeItens.GerarLendario(rng, Cfg, 3, EfeitoLendario.CeifeiroFaminto));
                    Equipamento.AutoEquipar(p, Cfg);
                    return p;
                };
                var pa = montar(); var pb = montar();
                Afirmar(JsonSerializer.Serialize(pa, json) == JsonSerializer.Serialize(pb, json), "geração de itens não é determinística");
                Afirmar(Equipamento.CalcularBonus(pa, Cfg).ItensEquipados == 9, "os 9 espaços deveriam estar ocupados");
                var a = new Simulacao(Cfg, pa, 42, ModoExpedicao.Avancar, 1);
                var b = new Simulacao(Cfg, pb, 42, ModoExpedicao.Avancar, 1);
                b.EmitirEventos = false;
                for (int t = 0; t < 20000 && !a.Encerrada; t++)
                {
                    a.Tick(); b.Tick();
                    if (t % 50 == 0) Afirmar(a.HashDoEstado() == b.HashDoEstado(), "hash divergiu no tick " + t);
                }
                Afirmar(a.Encerrada && b.Encerrada && a.HashDoEstado() == b.HashDoEstado(), "hash final divergiu");
                Afirmar(a.Resultado.Itens.Count > 0, "a expedição deveria render itens");
                Afirmar(JsonSerializer.Serialize(a.Resultado.Itens, json) == JsonSerializer.Serialize(b.Resultado.Itens, json), "itens caídos divergiram");
                var c = ExecutorSemTela.RodarExpedicao(Cfg, montar(), 43, ModoExpedicao.Avancar, 1);
                Afirmar(JsonSerializer.Serialize(c.Itens, json) != JsonSerializer.Serialize(a.Resultado.Itens, json), "sementes diferentes deveriam dar itens diferentes");

                // Itens só entram quando equipados: inventário cheio de itens não altera um único tick do combate.
                var semItens = Progressao.NovoJogo(Cfg);
                var soInventario = Progressao.NovoJogo(Cfg);
                var r2 = new Rng(5);
                for (int i = 0; i < 40; i++) soInventario.Inventario.Add(GeradorDeItens.Gerar(r2, Cfg, 9, RaridadeItem.Raro));
                var x = new Simulacao(Cfg, semItens, 1234, ModoExpedicao.Avancar, 1);
                var y = new Simulacao(Cfg, soInventario, 1234, ModoExpedicao.Avancar, 1);
                while (!x.Encerrada) { x.Tick(); y.Tick(); if (x.TickAtual % 100 == 0) Afirmar(x.HashDoEstado() == y.HashDoEstado(), "inventário mudou o combate no tick " + x.TickAtual); }
                Afirmar(y.Encerrada && x.HashDoEstado() == y.HashDoEstado(), "hash final com/sem inventário");
                // E equipar de fato muda o resultado (o teste acima não é vazio).
                Afirmar(a.Resultado.Ticks != ExecutorSemTela.RodarExpedicao(Cfg, Progressao.NovoJogo(Cfg), 42, ModoExpedicao.Avancar, 1).Ticks, "equipamento deveria mudar a expedição");
            });
            Teste("EstadoProgresso com itens: ida e volta em JSON (equipados, inventário, lendário, conjunto)", () =>
            {
                var p = Progressao.NovoJogo(Cfg);
                var rng = new Rng(77);
                for (int i = 0; i < 25; i++) p.Inventario.Add(GeradorDeItens.Gerar(rng, Cfg, 1 + i, RaridadeItem.Magico));
                Equipamento.AutoEquipar(p, Cfg);
                Equipamento.Equipar(p, GeradorDeItens.GerarLendario(rng, Cfg, 6, EfeitoLendario.UltimoSuspiro));
                Equipamento.Equipar(p, GeradorDeItens.GerarPecaDeConjunto(rng, Cfg, 6, ConjuntoId.VestesDoCoveiro, 0));
                Equipamento.Desequipar(p, EspacoEquipamento.Luvas, Cfg); // deixa um espaço vazio
                p.DesmancharAbaixoDe = RaridadeItem.Magico;
                string texto = JsonSerializer.Serialize(p, json);
                var q = JsonSerializer.Deserialize<EstadoProgresso>(texto, json);
                Afirmar(JsonSerializer.Serialize(q, json) == texto, "ida e volta mudou o conteúdo");
                Afirmar(q.Inventario.Count == p.Inventario.Count && q.Equipados.Length == 9 && q.DesmancharAbaixoDe == RaridadeItem.Magico, "campos de inventário perdidos");
                var manto = Equipamento.Obter(q, EspacoEquipamento.Peitoral); var capuz = Equipamento.Obter(q, EspacoEquipamento.Elmo);
                Afirmar(manto != null && manto.Efeito == EfeitoLendario.UltimoSuspiro && manto.Afixos.Count == 4 && manto.Nome == "Manto do Último Suspiro", "lendário perdido");
                Afirmar(capuz != null && capuz.Conjunto == ConjuntoId.VestesDoCoveiro && capuz.Nome == "Capuz do Coveiro" && Equipamento.Obter(q, EspacoEquipamento.Luvas) == null, "peça de conjunto / espaço vazio");
                Afirmar(JsonSerializer.Serialize(Equipamento.CalcularBonus(p, Cfg), json) == JsonSerializer.Serialize(Equipamento.CalcularBonus(q, Cfg), json), "bônus diferente depois de recarregar");
                var ra = ExecutorSemTela.RodarExpedicao(Cfg, p, 8, ModoExpedicao.Avancar, 1); var rb = ExecutorSemTela.RodarExpedicao(Cfg, q, 8, ModoExpedicao.Avancar, 1);
                Afirmar(ra.Ticks == rb.Ticks && ra.Ossos == rb.Ossos && JsonSerializer.Serialize(ra.Itens, json) == JsonSerializer.Serialize(rb.Itens, json), "progresso recarregado deveria simular igual");
                // O JsonUtility da Unity recarrega null como objeto "zerado": Nivel 0 conta como espaço vazio.
                q.Equipados[(int)EspacoEquipamento.Luvas] = new Item();
                Afirmar(Equipamento.Obter(q, EspacoEquipamento.Luvas) == null, "item de nível 0 = vazio");
                Afirmar(JsonSerializer.Serialize(Equipamento.CalcularBonus(q, Cfg), json) == JsonSerializer.Serialize(Equipamento.CalcularBonus(p, Cfg), json), "espaço 'zerado' não deveria dar bônus");
                Afirmar(Equipamento.Equipar(q, ItemCom(TipoItem.Luvas, (AfixoItem.Vida, 5f))) == null, "equipar sobre espaço 'zerado' não devolve nada");
                // Save antigo (sem os campos novos) carrega com inventário vazio.
                var antigo = JsonSerializer.Deserialize<EstadoProgresso>("{\"Versao\":1,\"NivelNecromante\":3}", json);
                Afirmar(antigo.Inventario != null && antigo.Inventario.Count == 0 && antigo.Equipados.Length == 9 && Equipamento.CalcularBonus(antigo, Cfg).ItensEquipados == 0, "save antigo");
            });
        }
    }
}
