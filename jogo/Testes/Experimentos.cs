using System;
using System.Collections.Generic;
using System.Text;
using Necro.Nucleo;

namespace Necro.Testes
{
    /// <summary>
    /// Compara variantes de regra no cenário de início de jogo (necromante nível 1 + 3 Esqueletos),
    /// para apoiar decisões de design com números. Uso: dotnet run --project Testes -- --experimentos [n]
    /// </summary>
    public static class Experimentos
    {
        private sealed class Variante { public string Nome; public Action<ConfigJogo> Ajuste; }

        public static string Rodar(int n)
        {
            var variantes = new List<Variante>
            {
                new Variante { Nome = "Atual (ceifa à distância, overkill a 50%)", Ajuste = c => { } },
                new Variante { Nome = "Ceifa de perto (alcance 2)", Ajuste = c => c.AlcanceCeifa = 2f },
                new Variante { Nome = "Ceifa de perto (alcance 4)", Ajuste = c => c.AlcanceCeifa = 4f },
                new Variante { Nome = "Ceifa de perto (2) + almas atraem inimigos (raio 4)", Ajuste = c => { c.AlcanceCeifa = 2f; c.RaioAtracaoDaCeifa = 4f; } },
                new Variante { Nome = "Ceifa de perto (2) + almas atraem inimigos (raio 7)", Ajuste = c => { c.AlcanceCeifa = 2f; c.RaioAtracaoDaCeifa = 7f; } },
                new Variante { Nome = "Idem (raio 7) + interrompe com 8% de dano", Ajuste = c => { c.AlcanceCeifa = 2f; c.RaioAtracaoDaCeifa = 7f; c.LimiteDanoCeifa = 0.08f; } },
                new Variante { Nome = "Overkill a 100%", Ajuste = c => c.LimiarOverkill = 1.0f },
                new Variante { Nome = "Overkill a 150%", Ajuste = c => c.LimiarOverkill = 1.5f },
                new Variante { Nome = "Ceifa de perto (4) + overkill a 100%", Ajuste = c => { c.AlcanceCeifa = 4f; c.LimiarOverkill = 1.0f; } },
            };
            var sb = new StringBuilder();
            sb.AppendLine("| Variante | Andar final (mediana) | s por andar | Almas/h | Orbes perdidos | Ceifas interrompidas/h | Overkills |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var v in variantes)
            {
                var cfg = new ConfigJogo();
                v.Ajuste(cfg);
                var finais = new List<int>();
                long ticks = 0, ticksAndares = 0; int andares = 0, almas = 0, caidos = 0, perdidos = 0, interr = 0, mortos = 0, over = 0;
                for (int i = 0; i < n; i++)
                {
                    var p = Progressao.NovoJogo(cfg);
                    var r = ExecutorSemTela.RodarExpedicao(cfg, p, 5000UL + (ulong)i, ModoExpedicao.Avancar, 1);
                    finais.Add(r.AndarFinal);
                    ticks += r.Ticks; almas += r.Almas.Count; caidos += r.OrbesCaidos; perdidos += r.OrbesPerdidos; interr += r.CeifasInterrompidas;
                    mortos += r.InimigosMortos; over += r.Overkills;
                    for (int k = 0; k < r.TicksPorAndar.Count; k++) { ticksAndares += r.TicksPorAndar[k]; andares++; }
                }
                finais.Sort();
                double horas = ticks / (double)cfg.TicksPorSegundo / 3600.0;
                sb.Append("| ").Append(v.Nome).Append(" | ").Append(finais[finais.Count / 2]).Append(" | ")
                  .Append((ticksAndares / (double)Math.Max(1, andares) / cfg.TicksPorSegundo).ToString("0.0")).Append(" | ")
                  .Append((almas / horas).ToString("0.0")).Append(" | ")
                  .Append((100.0 * perdidos / Math.Max(1, caidos)).ToString("0.0")).Append("% | ")
                  .Append((interr / horas).ToString("0.0")).Append(" | ")
                  .Append((100.0 * over / Math.Max(1, mortos)).ToString("0")).AppendLine("% das mortes |");
            }
            return sb.ToString();
        }
    }
}
