using System;
using System.Globalization;
using System.IO;
using Necro.Nucleo;

namespace Necro.Testes
{
    public static class Program
    {
        /// <summary>
        /// dotnet run --project Testes                 -> testes + relatório de balanceamento
        /// dotnet run --project Testes -- --so-testes  -> só os testes
        /// dotnet run --project Testes -- --n 2000     -> nº de expedições por cenário no relatório
        /// </summary>
        /// <summary>Narra uma expedição do início de jogo, onda a onda (diagnóstico de balanceamento).</summary>
        private static void Depurar(ulong semente)
        {
            var cfg = new ConfigJogo();
            var sim = new Simulacao(cfg, Progressao.NovoJogo(cfg), semente, ModoExpedicao.Avancar, 1);
            while (!sim.Encerrada)
            {
                sim.Tick();
                for (int i = 0; i < sim.Eventos.Count; i++)
                {
                    var ev = sim.Eventos[i];
                    string t = (ev.Tick / 10f).ToString("0.0").PadLeft(6) + "s ";
                    switch (ev.Tipo)
                    {
                        case TipoEvento.OndaIniciada:
                            int vivos = 0; float vida = 0;
                            foreach (var e in sim.Entidades) if (e.Viva && e.Faccao == Faccao.Minion) { vivos++; vida += e.Vida; }
                            Console.WriteLine(t + "Andar " + sim.Andar + " onda " + ev.Extra + "/" + ev.Valor + (ev.Critico ? " (elite/chefe)" : "") + " | necro " + sim.Necromante.Vida.ToString("0") + "/" + sim.Necromante.VidaMax.ToString("0")
                                + " mana " + sim.Necromante.Mana.ToString("0") + " | minions vivos " + vivos + " (vida " + vida.ToString("0") + ")");
                            break;
                        case TipoEvento.EntidadeSurgiu: Console.WriteLine(t + "  surge " + ev.Especie + (ev.Extra != 0 ? " ELITE " + (ModificadorElite)ev.Extra : "") + (ev.Critico ? " CHEFE" : "")); break;
                        case TipoEvento.Morte: Console.WriteLine(t + "  morre " + ev.Especie + " #" + ev.Alvo + " " + ev.Condicao + (ev.Overkill ? " OVERKILL" : "") + (ev.Execucao ? " EXECUCAO" : "")); break;
                        case TipoEvento.FeiticoLancado: Console.WriteLine(t + "  feitiço " + (FeiticoId)ev.Extra); break;
                        case TipoEvento.AlmaCaiu: case TipoEvento.AlmaCeifada: case TipoEvento.CeifaInterrompida: case TipoEvento.AndarConcluido: case TipoEvento.ExpedicaoEncerrada:
                            Console.WriteLine(t + ev.Tipo + " " + ev.Extra + " " + ev.Valor.ToString("0.0")); break;
                    }
                }
            }
        }

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            bool soTestes = false;
            int n = 400;
            string saida = "RELATORIO_BALANCEAMENTO.md";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--so-testes") soTestes = true;
                else if (args[i] == "--n" && i + 1 < args.Length) n = int.Parse(args[++i]);
                else if (args[i] == "--saida" && i + 1 < args.Length) saida = args[++i];
                else if (args[i] == "--cfg" && i + 1 < args.Length) { Campanha.Ajustes = args[i + 1]; i++; }
                else if (args[i] == "--ativo") Campanha.JogadorAtivo = true;
                else if (args[i] == "--sem-fusao") Campanha.UsarFusao = false;
                else if (args[i] == "--sem-talentos") Campanha.UsarTalentos = false;
                else if (args[i] == "--sem-sacrificio") Campanha.UsarSacrificio = false;
                else if (args[i] == "--sem-oferendas") Campanha.UsarOferendas = false;
                else if (args[i] == "--calibrar") { Console.WriteLine(Campanha.Calibrar(6, 14.0)); return 0; }
                else if (args[i] == "--diagnostico") { Console.WriteLine(Campanha.Diagnostico(6, 8.0)); return 0; }
                else if (args[i] == "--campanha" || args[i] == "--metas")
                {
                    int nc = i + 1 < args.Length ? int.Parse(args[i + 1]) : 12;
                    double hs = i + 2 < args.Length ? double.Parse(args[i + 2]) : 12.0;
                    bool ok; string rel = Campanha.Relatorio(nc, hs, out ok);
                    Console.WriteLine(rel);
                    System.IO.File.WriteAllText("METAS_BALANCEAMENTO.md", rel);
                    return args[i] == "--metas" && !ok ? 1 : 0;
                }
                else if (args[i] == "--experimentos") { int ne = i + 1 < args.Length ? int.Parse(args[i + 1]) : 200; Console.WriteLine(Experimentos.Rodar(ne)); return 0; }
                else if (args[i] == "--depurar" && i + 1 < args.Length) { Depurar(ulong.Parse(args[++i])); return 0; }
            }

            Console.WriteLine("=== TESTES DO NÚCLEO ===");
            int falhas = Testes.RodarTodos();
            Console.WriteLine();
            if (falhas > 0)
            {
                Console.WriteLine("FALHOU: " + falhas + " teste(s).");
                return 1;
            }
            Console.WriteLine("Todos os testes passaram.");
            if (soTestes) return 0;

            Console.WriteLine();
            Console.WriteLine("=== RELATÓRIO DE BALANCEAMENTO ===");
            string relatorio = Balanceamento.Gerar(new ConfigJogo(), n);
            Console.WriteLine(relatorio);
            // Se rodar de dentro de Testes/, grava na pasta de cima (raiz do projeto).
            if (!Path.IsPathRooted(saida) && !Directory.Exists("Nucleo") && Directory.Exists(Path.Combine("..", "Nucleo")))
                saida = Path.Combine("..", saida);
            File.WriteAllText(saida, relatorio);
            Console.WriteLine("Relatório gravado em: " + Path.GetFullPath(saida));
            return 0;
        }
    }
}
