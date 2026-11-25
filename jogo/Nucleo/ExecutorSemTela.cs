using System;

namespace Necro.Nucleo
{
    /// <summary>Resumo de uma sessão de progresso offline.</summary>
    [Serializable]
    public class ResumoOffline
    {
        public float SegundosSimulados;
        public int Expedicoes;
        public int AndaresConcluidos;
        public int MaiorAndar;
        public long Ossos;
        public long Sangue;
        public int AlmasCeifadas;
        public int ItensEncontrados;
    }

    /// <summary>
    /// Roda a simulação "sem tela", o mais rápido possível (sem eventos, sem esperar tempo real).
    /// Serve para progresso offline e para o relatório de balanceamento.
    /// </summary>
    public static class ExecutorSemTela
    {
        /// <summary>Teto de segurança: 2 horas de jogo por expedição.</summary>
        public const int TicksMaximosPadrao = 10 * 3600 * 2;

        /// <summary>Roda uma expedição inteira até o necromante morrer (ou bater num limite). Não altera o progresso.</summary>
        public static ResultadoExpedicao RodarExpedicao(ConfigJogo cfg, EstadoProgresso progresso, ulong semente, ModoExpedicao modo, int andarInicial,
            int limiteDeAndares = int.MaxValue, int ticksMaximos = TicksMaximosPadrao)
        {
            var sim = new Simulacao(cfg, progresso, semente, modo, andarInicial);
            sim.EmitirEventos = false;
            sim.LimiteDeAndares = limiteDeAndares;
            while (!sim.Encerrada)
            {
                sim.Tick();
                if (sim.TickAtual >= ticksMaximos) sim.EncerrarPorTempo();
            }
            return sim.Resultado;
        }

        /// <summary>Roda um único andar. Resultado.AndaresConcluidos == 1 significa vitória.</summary>
        public static ResultadoExpedicao RodarAndar(ConfigJogo cfg, EstadoProgresso progresso, ulong semente, int andar)
        {
            return RodarExpedicao(cfg, progresso, semente, ModoExpedicao.Cacar, andar, 1);
        }

        /// <summary>
        /// Progresso offline: encadeia expedições até gastar o tempo ausente, aplicando cada resultado ao progresso.
        /// Avançar parte sempre do último Ponto de Ancoragem; Caçar repete o andar pedido.
        /// </summary>
        public static ResumoOffline SimularOffline(ConfigJogo cfg, EstadoProgresso progresso, float segundosAusente, ModoExpedicao modo, int andarDeCaca = 1)
        {
            var resumo = new ResumoOffline();
            int ticksRestantes = (int)(segundosAusente * cfg.TicksPorSegundo);
            int guarda = 0;
            while (ticksRestantes > 0 && guarda++ < 100000)
            {
                int andar = modo == ModoExpedicao.Avancar ? progresso.AndarDeAncoragem : andarDeCaca;
                ulong semente = progresso.ContadorDeSementes++;
                var r = RodarExpedicao(cfg, progresso, semente, modo, andar, int.MaxValue, ticksRestantes);
                Progressao.AplicarResultado(progresso, r, cfg);
                ticksRestantes -= Math.Max(1, r.Ticks);
                resumo.Expedicoes++;
                resumo.AndaresConcluidos += r.AndaresConcluidos;
                if (r.MaiorAndarConcluido > resumo.MaiorAndar) resumo.MaiorAndar = r.MaiorAndarConcluido;
                resumo.Ossos += r.Ossos;
                resumo.Sangue += r.Sangue;
                resumo.AlmasCeifadas += r.Almas.Count;
                resumo.ItensEncontrados += r.Itens.Count;
                resumo.SegundosSimulados += r.Ticks / (float)cfg.TicksPorSegundo;
            }
            return resumo;
        }
    }
}
