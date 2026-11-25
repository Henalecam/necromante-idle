using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Necro.Nucleo;

namespace Necro.Testes
{
    /// <summary>Simula muitas expedições sem tela e monta o relatório em Markdown.</summary>
    public static class Balanceamento
    {
        private sealed class Agregado
        {
            public int Expedicoes;
            public long Ticks;
            public int[] MortesNoAndar = new int[64];     // andar em que o necromante morreu
            public long[] TicksNoAndar = new long[64];    // soma dos ticks dos andares concluídos
            public int[] ConclusoesDoAndar = new int[64];
            public long Almas, OrbesCaidos, OrbesPerdidos, Interrupcoes, Ossos, Sangue, Restos, Inimigos, Elites, MinionsMortos, Overkills, Execucoes, Andares;
            public int[] AlmasPorQualidade = new int[5];
            public int[] ItensPorRaridade = new int[5];
            public int ChefesVencidos;
            public int NaoMorreu;
            public List<int> AndaresFinais = new List<int>();
            public double XpTotal;
        }

        private static Agregado Rodar(ConfigJogo cfg, EstadoProgresso p, int n, ModoExpedicao modo, int andar, ulong sementeBase, int ticksMax)
        {
            var a = new Agregado();
            for (int i = 0; i < n; i++)
            {
                var r = ExecutorSemTela.RodarExpedicao(cfg, p, sementeBase + (ulong)i, modo, andar, int.MaxValue, ticksMax);
                a.Expedicoes++;
                a.Ticks += r.Ticks;
                if (r.Motivo == MotivoFim.NecromanteMorreu) a.MortesNoAndar[Math.Min(63, r.AndarFinal)]++; else a.NaoMorreu++;
                a.AndaresFinais.Add(r.AndarFinal);
                for (int k = 0; k < r.AndaresRegistrados.Count; k++)
                {
                    int f = Math.Min(63, r.AndaresRegistrados[k]);
                    a.TicksNoAndar[f] += r.TicksPorAndar[k];
                    a.ConclusoesDoAndar[f]++;
                }
                a.Andares += r.AndaresConcluidos;
                a.Almas += r.Almas.Count;
                for (int k = 0; k < r.Almas.Count; k++) a.AlmasPorQualidade[(int)r.Almas[k].Qualidade]++;
                for (int k = 0; k < r.Itens.Count; k++) a.ItensPorRaridade[(int)r.Itens[k].Raridade]++;
                a.OrbesCaidos += r.OrbesCaidos; a.OrbesPerdidos += r.OrbesPerdidos; a.Interrupcoes += r.CeifasInterrompidas;
                a.Ossos += r.Ossos; a.Sangue += r.Sangue;
                for (int k = 0; k < r.Restos.Length; k++) a.Restos += r.Restos[k];
                a.Inimigos += r.InimigosMortos; a.Elites += r.ElitesMortos; a.MinionsMortos += r.MinionsMortos;
                a.Overkills += r.Overkills; a.Execucoes += r.Execucoes;
                a.ChefesVencidos += r.ChefesDerrotados.Count;
                a.XpTotal += r.Xp;
            }
            return a;
        }

        private static EstadoProgresso ExercitoMelhorado(ConfigJogo cfg, int nivelMinions, int nivelNecro, Qualidade q)
        {
            var p = new EstadoProgresso { ComandoMaximo = cfg.ComandoInicial, NivelNecromante = nivelNecro };
            EspecieId[] lista =
            {
                EspecieId.Zumbi, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.CaoDeCripta,
                EspecieId.ArqueiroOssudo, EspecieId.ArqueiroOssudo, EspecieId.AcolitoCaido
            }; // custo total: 7 pequenos + 1 médio = 10
            for (int i = 0; i < lista.Length; i++)
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = lista[i], Qualidade = q }, Nivel = nivelMinions, Ativo = true });
            return p;
        }

        private static string F(double v, string fmt = "0.0") { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }

        private static void Cenario(StringBuilder sb, ConfigJogo cfg, string titulo, string descricao, Agregado a, bool histograma)
        {
            double horas = a.Ticks / (double)cfg.TicksPorSegundo / 3600.0;
            a.AndaresFinais.Sort();
            double media = 0; for (int i = 0; i < a.AndaresFinais.Count; i++) media += a.AndaresFinais[i];
            media /= Math.Max(1, a.AndaresFinais.Count);
            int mediana = a.AndaresFinais[a.AndaresFinais.Count / 2];
            int p10 = a.AndaresFinais[a.AndaresFinais.Count / 10];
            int p90 = a.AndaresFinais[a.AndaresFinais.Count * 9 / 10];

            sb.AppendLine("## " + titulo);
            sb.AppendLine();
            sb.AppendLine(descricao);
            sb.AppendLine();
            sb.AppendLine("| Métrica | Valor |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Expedições simuladas | " + a.Expedicoes + " |");
            sb.AppendLine("| Andar em que a expedição termina (média / mediana / p10–p90) | " + F(media) + " / " + mediana + " / " + p10 + "–" + p90 + " |");
            sb.AppendLine("| Duração média da expedição | " + F(a.Ticks / (double)cfg.TicksPorSegundo / a.Expedicoes / 60.0) + " min |");
            sb.AppendLine("| Tempo médio por andar concluído | " + F(a.Andares > 0 ? SomaTicks(a) / (double)cfg.TicksPorSegundo / a.Andares : 0) + " s |");
            sb.AppendLine("| Almas ceifadas por hora | " + F(a.Almas / horas, "0.00") + " |");
            sb.AppendLine("| Orbes caídos / perdidos / ceifas interrompidas (por hora) | " + F(a.OrbesCaidos / horas, "0.00") + " / " + F(a.OrbesPerdidos / horas, "0.00") + " / " + F(a.Interrupcoes / horas, "0.00") + " |");
            sb.AppendLine("| Itens caídos por hora (Comum / Mágico / Raro / Lendário / Profano) | " + F(a.ItensPorRaridade[0] / horas, "0.00") + " / " + F(a.ItensPorRaridade[1] / horas, "0.00") + " / "
                + F(a.ItensPorRaridade[2] / horas, "0.00") + " / " + F(a.ItensPorRaridade[3] / horas, "0.000") + " / " + F(a.ItensPorRaridade[4] / horas, "0.000") + " |");
            sb.AppendLine("| Ossos / Sangue / Restos por hora | " + F(a.Ossos / horas, "0") + " / " + F(a.Sangue / horas) + " / " + F(a.Restos / horas, "0") + " |");
            sb.AppendLine("| XP por hora | " + F(a.XpTotal / horas, "0") + " |");
            sb.AppendLine("| Inimigos mortos por expedição (elites) | " + F(a.Inimigos / (double)a.Expedicoes) + " (" + F(a.Elites / (double)a.Expedicoes, "0.00") + ") |");
            sb.AppendLine("| Minions perdidos por expedição | " + F(a.MinionsMortos / (double)a.Expedicoes, "0.00") + " |");
            sb.AppendLine("| Overkills / Execuções por expedição | " + F(a.Overkills / (double)a.Expedicoes, "0.00") + " / " + F(a.Execucoes / (double)a.Expedicoes, "0.00") + " |");
            sb.AppendLine("| Expedições que venceram um chefe | " + F(100.0 * a.ChefesVencidos / a.Expedicoes) + "% |");
            if (a.NaoMorreu > 0) sb.AppendLine("| Expedições cortadas pelo limite de tempo (necromante vivo) | " + a.NaoMorreu + " |");
            sb.AppendLine();

            if (!histograma) return;
            sb.AppendLine("| Andar | Expedições que chegam | Morrem aqui | % das mortes | Tempo médio p/ concluir |");
            sb.AppendLine("|---|---|---|---|---|");
            int chegam = a.Expedicoes;
            for (int f = 1; f < 64; f++)
            {
                if (chegam <= 0) break;
                if (f < FirstFloor(a)) continue;
                double t = a.ConclusoesDoAndar[f] > 0 ? a.TicksNoAndar[f] / (double)cfg.TicksPorSegundo / a.ConclusoesDoAndar[f] : 0;
                sb.AppendLine("| " + f + " | " + chegam + " | " + a.MortesNoAndar[f] + " | " + F(100.0 * a.MortesNoAndar[f] / a.Expedicoes) + "% | " + (t > 0 ? F(t) + " s" : "—") + " |");
                chegam = a.ConclusoesDoAndar[f];
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Cenário de loot: "campanhas" de N expedições seguidas a partir do novo jogo. Entre uma expedição e a seguinte só os itens
        /// são mantidos (guardados e auto-equipados); XP, almas e ancoragem são descartados para isolar o efeito do equipamento.
        /// Cada expedição tem um controle: a mesma semente rodada sem nenhum item.
        /// </summary>
        private static string CenarioLoot(ConfigJogo cfg, int campanhas, int expedicoes)
        {
            var andares = new List<int>[expedicoes];
            var controle = new List<int>[expedicoes];
            var somaEquipados = new double[expedicoes];
            var somaPontos = new double[expedicoes];
            var ticksAndar = new long[expedicoes];
            var nAndares = new long[expedicoes];
            var chefes = new int[expedicoes];
            for (int e = 0; e < expedicoes; e++) { andares[e] = new List<int>(); controle[e] = new List<int>(); }
            var itens = new long[5];
            long ticks = 0, ticksControle = 0, desmanchados = 0, lendariosEquipados = 0;
            var efeitos = new int[7];

            for (int c = 0; c < campanhas; c++)
            {
                var p = Progressao.NovoJogo(cfg);
                var semItens = Progressao.NovoJogo(cfg);
                for (int e = 0; e < expedicoes; e++)
                {
                    ulong semente = 6000UL + (ulong)c * 100UL + (ulong)e;
                    somaEquipados[e] += Equipamento.CalcularBonus(p, cfg).ItensEquipados;
                    somaPontos[e] += Equipamento.PontuacaoEquipada(p, cfg);
                    var r = ExecutorSemTela.RodarExpedicao(cfg, p, semente, ModoExpedicao.Avancar, 1);
                    var r0 = ExecutorSemTela.RodarExpedicao(cfg, semItens, semente, ModoExpedicao.Avancar, 1);
                    andares[e].Add(r.AndarFinal);
                    controle[e].Add(r0.AndarFinal);
                    ticks += r.Ticks; ticksControle += r0.Ticks;
                    for (int k = 0; k < r.TicksPorAndar.Count; k++) { ticksAndar[e] += r.TicksPorAndar[k]; nAndares[e]++; }
                    if (r.ChefesDerrotados.Count > 0) chefes[e]++;
                    for (int k = 0; k < r.Itens.Count; k++)
                    {
                        itens[(int)r.Itens[k].Raridade]++;
                        if (Equipamento.GuardarItem(p, r.Itens[k], cfg) != DestinoDoItem.Guardado) desmanchados++;
                    }
                    Equipamento.AutoEquipar(p, cfg);
                }
                for (int i = 0; i < p.Equipados.Length; i++)
                    if (!Equipamento.Vazio(p.Equipados[i]) && p.Equipados[i].Efeito != EfeitoLendario.Nenhum) { lendariosEquipados++; efeitos[(int)p.Equipados[i].Efeito]++; }
            }

            var sb = new StringBuilder();
            double horas = ticks / (double)cfg.TicksPorSegundo / 3600.0;
            sb.AppendLine("## Cenário 6 — Loot com auto-equipar ao longo de " + expedicoes + " expedições seguidas");
            sb.AppendLine();
            sb.AppendLine(campanhas + " campanhas. Cada campanha começa no novo jogo (necromante nível 1 + 3 Esqueletos) e faz " + expedicoes
                + " expedições no modo Avançar a partir do andar 1. Depois de cada expedição os itens caídos vão para o inventário e `Equipamento.AutoEquipar` troca o que tiver pontuação maior. "
                + "**Só os itens passam de uma expedição para a outra** (XP, almas e ancoragem são descartados), para isolar o efeito do loot. "
                + "O controle é a mesma semente rodada sem nenhum item (idêntico ao cenário 1).");
            sb.AppendLine();
            sb.AppendLine("| Expedição nº | Andar final com loot (média / mediana / p10–p90) | Andar final sem itens (média / mediana) | Itens equipados (média) | Pontuação do equipamento (média) | Tempo médio por andar | Venceu o chefe |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            for (int e = 0; e < expedicoes; e++)
            {
                andares[e].Sort(); controle[e].Sort();
                double m = 0, m0 = 0;
                for (int i = 0; i < campanhas; i++) { m += andares[e][i]; m0 += controle[e][i]; }
                sb.AppendLine("| " + (e + 1) + " | " + F(m / campanhas) + " / " + andares[e][campanhas / 2] + " / " + andares[e][campanhas / 10] + "–" + andares[e][campanhas * 9 / 10]
                    + " | " + F(m0 / campanhas) + " / " + controle[e][campanhas / 2] + " | " + F(somaEquipados[e] / campanhas) + " | " + F(somaPontos[e] / campanhas)
                    + " | " + F(nAndares[e] > 0 ? ticksAndar[e] / (double)cfg.TicksPorSegundo / nAndares[e] : 0) + " s | " + F(100.0 * chefes[e] / campanhas, "0") + "% |");
            }
            sb.AppendLine();
            {
                double ini = 0, fim = 0, c0 = 0; int ult = expedicoes - 1;
                for (int i = 0; i < campanhas; i++) { ini += andares[0][i]; fim += andares[ult][i]; c0 += controle[ult][i]; }
                sb.AppendLine("**Leitura:**");
                sb.AppendLine();
                sb.AppendLine("- Na 1ª expedição (sem nada equipado) o andar final médio é " + F(ini / campanhas) + "; na " + expedicoes + "ª, com o que caiu nas anteriores, é " + F(fim / campanhas)
                    + " (controle sem itens na mesma semente: " + F(c0 / campanhas) + "). Ganho do loot: **+" + F(fim / campanhas - c0 / campanhas) + " andares**, mediana "
                    + controle[ult][campanhas / 2] + " → " + andares[ult][campanhas / 2] + ".");
                sb.AppendLine("- Tempo de jogo por campanha: " + F(ticks / (double)cfg.TicksPorSegundo / campanhas / 60.0) + " min com loot (" + F(ticksControle / (double)cfg.TicksPorSegundo / campanhas / 60.0) + " min no controle).");
                sb.AppendLine("- Itens que não couberam ou perderam a vaga no inventário de " + cfg.CapacidadeInventario + " (desmanchados em Essência): " + F(desmanchados / (double)campanhas) + " por campanha.");
                string ef = "";
                for (int i = 1; i < efeitos.Length; i++) if (efeitos[i] > 0) ef += (ef.Length > 0 ? ", " : "") + TabelaItens.Lendario((EfeitoLendario)i).Nome + " ×" + efeitos[i];
                sb.AppendLine("- Lendários equipados ao fim das " + campanhas + " campanhas: " + lendariosEquipados + (ef.Length > 0 ? " (" + ef + ")" : "") + ".");
                sb.AppendLine();
            }
            sb.AppendLine("| Raridade | Itens caídos | Por hora de jogo | Por campanha | Observado | Design |");
            sb.AppendLine("|---|---|---|---|---|---|");
            long total = 0; for (int i = 0; i < 5; i++) total += itens[i];
            for (int i = 0; i < 5; i++)
                sb.AppendLine("| " + (RaridadeItem)i + " | " + itens[i] + " | " + F(itens[i] / horas, i >= 3 ? "0.000" : "0.00") + " | " + F(itens[i] / (double)campanhas, "0.00") + " | "
                    + F(total > 0 ? 100.0 * itens[i] / total : 0, "0.00") + "% | " + F(cfg.ChanceRaridadeItem[i] * 100, "0.0") + "% |");
            sb.AppendLine("| **Total** | " + total + " | " + F(total / horas, "0.00") + " | " + F(total / (double)campanhas, "0.00") + " | | |");
            sb.AppendLine();
            sb.AppendLine("Chances de drop usadas: inimigo comum " + F(cfg.DropItemComum * 100) + "%, elite " + F(cfg.DropItemElite * 100) + "%, chefe garante " + cfg.ItensDoChefe
                + ". Faixas dos afixos: " + F(cfg.CrescimentoItemPorAndar * 100) + "% composto por nível de item (escala plena) ou +" + F(cfg.CrescimentoItemLinearPorAndar * 100) + "% linear (taxas e chances).");
            sb.AppendLine();
            return sb.ToString();
        }

        private static int FirstFloor(Agregado a)
        {
            for (int f = 1; f < 64; f++) if (a.ConclusoesDoAndar[f] > 0 || a.MortesNoAndar[f] > 0) return f;
            return 1;
        }

        private static long SomaTicks(Agregado a)
        {
            long s = 0; for (int i = 0; i < a.TicksNoAndar.Length; i++) s += a.TicksNoAndar[i];
            return s;
        }

        public static string Gerar(ConfigJogo cfg, int n)
        {
            var sb = new StringBuilder();
            var relogio = Stopwatch.StartNew();
            long ticksTotais = 0;

            var novo = Progressao.NovoJogo(cfg);
            var a1 = Rodar(cfg, novo, n, ModoExpedicao.Avancar, 1, 1000, ExecutorSemTela.TicksMaximosPadrao);
            var novoAnc = Progressao.NovoJogo(cfg);
            var a2 = Rodar(cfg, novoAnc, n, ModoExpedicao.Avancar, 6, 2000, ExecutorSemTela.TicksMaximosPadrao);
            var melhor = ExercitoMelhorado(cfg, 8, 5, Qualidade.Comum);
            var a3 = Rodar(cfg, melhor, n, ModoExpedicao.Avancar, 1, 3000, ExecutorSemTela.TicksMaximosPadrao);
            var forte = ExercitoMelhorado(cfg, 20, 12, Qualidade.Vigorosa);
            forte.PoderNecromantico = 0.3f;
            var a4 = Rodar(cfg, forte, n, ModoExpedicao.Avancar, 6, 4000, ExecutorSemTela.TicksMaximosPadrao);
            int nCaca = Math.Max(10, n / 20);
            var a5 = Rodar(cfg, novo, nCaca, ModoExpedicao.Cacar, 3, 5000, 36000);
            ticksTotais = a1.Ticks + a2.Ticks + a3.Ticks + a4.Ticks + a5.Ticks;
            relogio.Stop();
            int campanhas = Math.Max(20, n / 5);
            string cenarioLoot = CenarioLoot(cfg, campanhas, 20);

            sb.AppendLine("# Relatório de balanceamento — Núcleo do Necromante");
            sb.AppendLine();
            sb.AppendLine("Gerado por `dotnet run --project Testes` (determinístico: mesmas sementes, mesmos números). " + n + " expedições por cenário.");
            sb.AppendLine();
            sb.AppendLine("Alvos de design: passar dos primeiros andares e empacar por volta do andar 6–10 sem melhorias; 30–60 s por andar.");
            sb.AppendLine();
            sb.AppendLine("Parâmetros globais usados: crescimento por andar " + F(cfg.CrescimentoPorAndar * 100) + "% composto; inimigo comum = base da espécie × "
                + F(cfg.FatorVidaInimigo, "0.00") + " (vida) / × " + F(cfg.FatorDanoInimigo, "0.00") + " (dano); elite × " + F(cfg.EliteMultVida) + " vida / × " + F(cfg.EliteMultDano)
                + " dano; chefe × " + F(cfg.ChefeMultVida) + " vida / × " + F(cfg.ChefeMultDano) + " dano; orçamento de onda " + F(cfg.OrcamentoBaseOnda) + " + " + F(cfg.OrcamentoPorAndar, "0.00")
                + "/andar + " + F(cfg.OrcamentoPorOnda) + "/onda; cura entre andares " + F(cfg.CuraEntreAndares * 100, "0") + "%.");
            sb.AppendLine();

            sb.AppendLine("## Números base das espécies (Ato 1)");
            sb.AppendLine();
            sb.AppendLine("| Espécie | Família | Porte | Papel | Vida | Dano | Vel. ataque | Armadura | Alcance | Vel. mov. | Tipo | 1º andar | Peso | Custo de onda |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var todas = TabelaEspecies.Todas;
            for (int i = 0; i < todas.Length; i++)
            {
                var d = todas[i];
                sb.AppendLine("| " + d.Nome + " | " + d.Familia + " | " + d.Porte + " | " + d.Papel + " | " + F(d.Vida, "0") + " | " + F(d.Dano) + " | " + F(d.VelAtaque, "0.00") + " | " + F(d.Armadura, "0")
                    + " | " + F(d.Alcance) + " | " + F(d.VelMovimento) + " | " + d.TipoDano + " | " + (d.PesoOnda > 0 || d.Chefe ? d.AndarMinimo.ToString() : "—") + " | " + F(d.PesoOnda, "0") + " | " + F(d.CustoOnda) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("Os valores são os do minion nível 1 de qualidade Comum; o inimigo usa os mesmos números × fatores de inimigo × 1,09^(andar−1).");
            sb.AppendLine();

            Cenario(sb, cfg, "Cenário 1 — Início de jogo (alvo principal)",
                "Necromante nível 1 + 3 Esqueletos comuns nível 1, ordens padrão, modo Avançar a partir do andar 1, sem nenhuma melhoria entre expedições.", a1, true);
            // Leitura automática do cenário 1 contra os alvos de design.
            {
                int passamDo4 = a1.ConclusoesDoAndar[4], mortes6a10 = 0, mortesAntesDo5 = 0, mortes5 = a1.MortesNoAndar[5];
                for (int f = 1; f <= 4; f++) mortesAntesDo5 += a1.MortesNoAndar[f];
                for (int f = 6; f <= 10; f++) mortes6a10 += a1.MortesNoAndar[f];
                sb.AppendLine("**Leitura contra os alvos:**");
                sb.AppendLine();
                sb.AppendLine("- Passam dos 4 primeiros andares: " + F(100.0 * passamDo4 / a1.Expedicoes) + "% das expedições (mortes nos andares 1–4: " + F(100.0 * mortesAntesDo5 / a1.Expedicoes) + "%).");
                sb.AppendLine("- Empacam (morrem) no andar 5: " + F(100.0 * mortes5 / a1.Expedicoes) + "%; entre os andares 6 e 10: " + F(100.0 * mortes6a10 / a1.Expedicoes) + "%.");
                string dentro = "", fora = "";
                for (int f = 1; f < 64; f++)
                {
                    if (a1.ConclusoesDoAndar[f] < Math.Max(5, a1.Expedicoes / 20)) continue; // ignora andares com amostra pequena
                    double t = a1.TicksNoAndar[f] / (double)cfg.TicksPorSegundo / a1.ConclusoesDoAndar[f];
                    if (t >= 30.0 && t <= 60.0) dentro += (dentro.Length > 0 ? ", " : "") + f; else fora += (fora.Length > 0 ? ", " : "") + f + " (" + F(t) + " s)";
                }
                sb.AppendLine("- Andares com tempo médio dentro do alvo de 30–60 s: " + (dentro.Length > 0 ? dentro : "nenhum") + ". Fora do alvo: " + (fora.Length > 0 ? fora : "nenhum") + ".");
                sb.AppendLine("- O tempo por andar não inclui a pausa de " + F(cfg.PausaEntreAndares) + " s entre andares.");
                sb.AppendLine();
            }
            Cenario(sb, cfg, "Cenário 2 — Mesmo exército inicial, partindo da Ancoragem do andar 6",
                "Mostra o que acontece depois de destravar o primeiro Ponto de Ancoragem sem ter melhorado nada.", a2, true);
            Cenario(sb, cfg, "Cenário 3 — Exército de Comando cheio (10), nível 8",
                "Necromante nível 5; Zumbi, 3 Esqueletos, Cão de Cripta, 2 Arqueiros Ossudos e Acólito Caído, todos Comuns nível 8. A partir do andar 1.", a3, true);
            Cenario(sb, cfg, "Cenário 4 — Exército avançado, partindo do andar 6",
                "Necromante nível 12, Poder Necromântico +30%; mesma composição do cenário 3 em qualidade Vigorosa nível 20.", a4, true);
            Cenario(sb, cfg, "Cenário 5 — Modo Caçar no andar 3 (1 hora de jogo por execução)",
                "Exército inicial repetindo o andar 3 por 1 hora simulada (" + nCaca + " execuções). No modo Caçar o exército é restaurado a cada repetição.", a5, false);

            sb.Append(cenarioLoot);

            sb.AppendLine("## Raridade dos itens caídos (cenários 1 a 5 somados)");
            sb.AppendLine();
            long totalItens = 0; var qi = new long[5];
            foreach (var a in new[] { a1, a2, a3, a4, a5 }) for (int i = 0; i < 5; i++) { qi[i] += a.ItensPorRaridade[i]; totalItens += a.ItensPorRaridade[i]; }
            sb.AppendLine("| Raridade | Quantidade | Observado | Design |");
            sb.AppendLine("|---|---|---|---|");
            for (int i = 0; i < 5; i++)
                sb.AppendLine("| " + (RaridadeItem)i + " | " + qi[i] + " | " + F(totalItens > 0 ? 100.0 * qi[i] / totalItens : 0, "0.00") + "% | " + F(cfg.ChanceRaridadeItem[i] * 100, "0.0") + "% |");
            sb.AppendLine();
            sb.AppendLine("O observado fica um pouco acima do design nas raridades altas porque o chefe garante um item no mínimo Raro na primeira vitória.");
            sb.AppendLine();

            sb.AppendLine("## Qualidade das almas ceifadas (todos os cenários somados)");
            sb.AppendLine();
            long totalAlmas = 0; var q = new long[5];
            foreach (var a in new[] { a1, a2, a3, a4, a5 }) for (int i = 0; i < 5; i++) { q[i] += a.AlmasPorQualidade[i]; totalAlmas += a.AlmasPorQualidade[i]; }
            sb.AppendLine("| Qualidade | Quantidade | Observado | Design |");
            sb.AppendLine("|---|---|---|---|");
            for (int i = 0; i < 5; i++)
                sb.AppendLine("| " + (Qualidade)i + " | " + q[i] + " | " + F(totalAlmas > 0 ? 100.0 * q[i] / totalAlmas : 0) + "% | " + F(cfg.ChanceQualidade[i] * 100, "0") + "% |");
            sb.AppendLine();

            sb.AppendLine("## Desempenho do executor sem tela");
            sb.AppendLine();
            double seg = relogio.Elapsed.TotalSeconds;
            sb.AppendLine("- " + ticksTotais + " ticks simulados em " + F(seg, "0.00") + " s de CPU (" + F(ticksTotais / seg / 1000.0, "0") + " mil ticks/s, ou ~"
                + F(ticksTotais / (double)cfg.TicksPorSegundo / seg, "0") + "× o tempo real) neste computador, build Debug, uma thread.");
            sb.AppendLine("- Esse número varia por máquina; o restante do relatório não.");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
