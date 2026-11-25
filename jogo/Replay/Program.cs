// Exporta uma expedição real da simulação, tick a tick, para um arquivo de texto simples.
// O script python `necro-pipeline/ferramentas/replay_gif.py` desenha esse replay com os sprites.
// Uso: dotnet run --project Replay -- <saida.txt> [semente] [ticksMax] [andarInicial] [nivelDosMinions]
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Necro.Nucleo;

static class Program
{
    static void Main(string[] a)
    {
        string saida = a.Length > 0 ? a[0] : "replay.txt";
        ulong semente = a.Length > 1 ? ulong.Parse(a[1]) : 1003UL;
        int ticksMax = a.Length > 2 ? int.Parse(a[2]) : 900;
        int andar = a.Length > 3 ? int.Parse(a[3]) : 1;
        int nivel = a.Length > 4 ? int.Parse(a[4]) : 3;          // nível dos minions (use ~14 para ver o chefe)
        var ci = CultureInfo.InvariantCulture;

        var cfg = new ConfigJogo();
        var p = new EstadoProgresso();
        EspecieId[] exercito = { EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Esqueleto, EspecieId.Zumbi, EspecieId.Zumbi, EspecieId.Diabrete, EspecieId.Diabrete };
        foreach (var e in exercito)
            p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = e, Qualidade = Qualidade.Comum }, Nivel = nivel, Ativo = true });

        var sim = new Simulacao(cfg, p, semente, ModoExpedicao.Avancar, andar);
        var sb = new StringBuilder();
        sb.Append("ARENA ").Append(cfg.LarguraArena.ToString(ci)).Append(' ').Append(cfg.AlturaArena.ToString(ci)).Append(' ').Append(cfg.TicksPorSegundo).Append('\n');
        for (int t = 0; t < ticksMax && !sim.Encerrada; t++)
        {
            sim.Tick();
            sb.Append("T ").Append(t).Append(' ').Append(sim.Andar).Append('\n');
            foreach (var e in sim.Entidades)
                sb.Append("E ").Append(e.Id).Append(' ').Append(e.Especie).Append(' ').Append(e.Faccao).Append(' ')
                  .Append(e.Pos.X.ToString("0.###", ci)).Append(' ').Append(e.Pos.Y.ToString("0.###", ci)).Append(' ')
                  .Append(e.Olhar.X.ToString("0.###", ci)).Append(' ').Append(e.Olhar.Y.ToString("0.###", ci)).Append(' ')
                  .Append(e.Animacao).Append(' ').Append(e.Viva ? 1 : 0).Append(' ')
                  .Append((e.Vida / Math.Max(1f, e.VidaMax)).ToString("0.###", ci)).Append(' ').Append(e.Elite ? 1 : 0).Append(e.Chefe ? 1 : 0).Append('\n');
            foreach (var o in sim.Orbes)
                sb.Append("O ").Append(o.Id).Append(' ').Append(o.Pos.X.ToString("0.###", ci)).Append(' ').Append(o.Pos.Y.ToString("0.###", ci)).Append('\n');
            foreach (var pj in sim.Projeteis)
                sb.Append("P ").Append(pj.Id).Append(' ').Append(pj.Pos.X.ToString("0.###", ci)).Append(' ').Append(pj.Pos.Y.ToString("0.###", ci)).Append(' ')
                  .Append(pj.Dir.X.ToString("0.###", ci)).Append(' ').Append(pj.Dir.Y.ToString("0.###", ci)).Append(' ').Append(pj.Perfurante ? 1 : 0).Append('\n');
            foreach (var c in sim.Cadaveres)
                sb.Append("C ").Append(c.Id).Append(' ').Append(c.Especie).Append(' ').Append(c.Pos.X.ToString("0.###", ci)).Append(' ').Append(c.Pos.Y.ToString("0.###", ci)).Append(' ').Append(c.Condicao).Append('\n');
            foreach (var ev in sim.Eventos)
                sb.Append("V ").Append(ev.Tipo).Append(' ').Append(ev.Origem).Append(' ').Append(ev.Alvo).Append(' ')
                  .Append(ev.Valor.ToString("0.##", ci)).Append(' ').Append(ev.TipoDano).Append(' ').Append(ev.Critico ? 1 : 0).Append(' ')
                  .Append(ev.Condicao).Append(' ').Append(ev.Overkill ? 1 : 0).Append(' ').Append(ev.Execucao ? 1 : 0).Append(' ')
                  .Append(ev.Pos.X.ToString("0.###", ci)).Append(' ').Append(ev.Pos.Y.ToString("0.###", ci)).Append(' ')
                  .Append(ev.Dir.X.ToString("0.###", ci)).Append(' ').Append(ev.Dir.Y.ToString("0.###", ci)).Append(' ').Append(ev.Extra).Append('\n');
        }
        File.WriteAllText(saida, sb.ToString());
        Console.WriteLine("Replay salvo em " + saida + " (andar final " + sim.Andar + ")");
    }
}
