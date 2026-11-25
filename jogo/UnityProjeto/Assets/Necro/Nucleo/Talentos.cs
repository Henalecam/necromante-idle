using System;

namespace Necro.Nucleo
{
    public enum RamoDeTalento { Ossos = 0, Sangue = 1, Sombras = 2 }

    /// <summary>O que um talento melhora (um efeito por talento; o valor é por ponto investido).</summary>
    public enum EfeitoDeTalento
    {
        VidaDosMinions, DanoDosMinions, VelAtaqueDosMinions, RouboDeVidaDosMinions, Comando,
        VidaDoNecromante, DanoDeFeiticos, RegenMana, ChanceCritico, ReducaoDeRecarga,
        VelocidadeDeCeifa, ChanceDeAlma, Ossos, ToqueMelhoraAlma, PoderNecromantico
    }

    [Serializable]
    public class DadosTalento
    {
        public string Nome;
        public string Texto;            // "{0}" vira o valor total com os pontos atuais
        public RamoDeTalento Ramo;
        public EfeitoDeTalento Efeito;
        public float PorPonto;
        public int Maximo = 5;
        public int PontosNoRamo;        // pré-requisito: pontos já gastos no mesmo ramo
    }

    /// <summary>
    /// Talentos do necromante: 1 ponto a cada 2 níveis, três ramos (Ossos = minions, Sangue = o próprio necromante,
    /// Sombras = almas e saque). Os nós mais fundos pedem pontos no mesmo ramo, então espalhar custa os melhores talentos.
    /// </summary>
    public static class Talentos
    {
        public static int Pontos(EstadoProgresso p, int indice) { return p.Talentos != null && indice < p.Talentos.Length ? p.Talentos[indice] : 0; }

        public static int Gastos(EstadoProgresso p)
        {
            int n = 0; if (p.Talentos != null) for (int i = 0; i < p.Talentos.Length; i++) n += p.Talentos[i];
            return n;
        }

        public static int GastosNoRamo(EstadoProgresso p, RamoDeTalento ramo, ConfigJogo cfg)
        {
            int n = 0;
            for (int i = 0; i < cfg.Talentos.Length; i++) if (cfg.Talentos[i].Ramo == ramo) n += Pontos(p, i);
            return n;
        }

        /// <summary>Pontos ganhos até aqui: um a cada N níveis do necromante.</summary>
        public static int Ganhos(EstadoProgresso p, ConfigJogo cfg) { return (p.NivelNecromante - 1) / cfg.NiveisPorPontoDeTalento; }

        public static int Disponiveis(EstadoProgresso p, ConfigJogo cfg) { return Math.Max(0, Ganhos(p, cfg) - Gastos(p)); }

        public static bool PodeInvestir(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (indice < 0 || indice >= cfg.Talentos.Length || Disponiveis(p, cfg) <= 0) return false;
            var d = cfg.Talentos[indice];
            return Pontos(p, indice) < d.Maximo && GastosNoRamo(p, d.Ramo, cfg) >= d.PontosNoRamo;
        }

        public static bool Investir(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (!PodeInvestir(p, indice, cfg)) return false;
            if (p.Talentos == null || p.Talentos.Length < cfg.Talentos.Length)
            {
                var novo = new int[cfg.Talentos.Length];
                if (p.Talentos != null) Array.Copy(p.Talentos, novo, p.Talentos.Length);
                p.Talentos = novo;
            }
            p.Talentos[indice]++;
            return true;
        }

        public static long CustoDeRedistribuir(EstadoProgresso p, ConfigJogo cfg) { return cfg.EssenciaPorPontoRedistribuido * Gastos(p); }

        /// <summary>Devolve todos os pontos, pagando Essência. Minions acima do Comando novo saem de campo.</summary>
        public static bool Redistribuir(EstadoProgresso p, ConfigJogo cfg)
        {
            long custo = CustoDeRedistribuir(p, cfg);
            if (Gastos(p) == 0 || p.Essencia < custo) return false;
            p.Essencia -= custo;
            for (int i = 0; i < p.Talentos.Length; i++) p.Talentos[i] = 0;
            for (int i = p.Minions.Count - 1; i >= 0 && Progressao.ComandoEmUso(p, cfg) > Progressao.ComandoMaximoEfetivo(p, cfg); i--)
                if (p.Minions[i].Ativo) Progressao.Desativar(p, i);
            return true;
        }

        /// <summary>Valor total de um efeito com os pontos atuais.</summary>
        public static float Total(EstadoProgresso p, EfeitoDeTalento efeito, ConfigJogo cfg)
        {
            float v = 0f;
            for (int i = 0; i < cfg.Talentos.Length; i++) if (cfg.Talentos[i].Efeito == efeito) v += cfg.Talentos[i].PorPonto * Pontos(p, i);
            return v;
        }

        /// <summary>Soma os talentos ao agregado de bônus que a simulação já usa para o equipamento.</summary>
        public static void Aplicar(EstadoProgresso p, ConfigJogo cfg, BonusEquipamento b)
        {
            if (p.Talentos == null) return;
            b.VidaDosMinions += Total(p, EfeitoDeTalento.VidaDosMinions, cfg);
            b.DanoDosMinions += Total(p, EfeitoDeTalento.DanoDosMinions, cfg);
            b.VelAtaqueDosMinions += Total(p, EfeitoDeTalento.VelAtaqueDosMinions, cfg);
            b.RouboDeVidaDosMinions += Total(p, EfeitoDeTalento.RouboDeVidaDosMinions, cfg);
            b.Comando += (int)Math.Round(Total(p, EfeitoDeTalento.Comando, cfg));
            b.VidaDoNecromantePercentual += Total(p, EfeitoDeTalento.VidaDoNecromante, cfg);
            b.DanoDeFeiticos += Total(p, EfeitoDeTalento.DanoDeFeiticos, cfg);
            b.RegenMana += Total(p, EfeitoDeTalento.RegenMana, cfg);
            b.ChanceCritico += Total(p, EfeitoDeTalento.ChanceCritico, cfg);
            b.ReducaoDeRecarga = Math.Min(cfg.TetoReducaoDeRecarga, b.ReducaoDeRecarga + Total(p, EfeitoDeTalento.ReducaoDeRecarga, cfg));
            b.VelocidadeDeCeifa += Total(p, EfeitoDeTalento.VelocidadeDeCeifa, cfg);
            b.ChanceDeAlma += Total(p, EfeitoDeTalento.ChanceDeAlma, cfg);
            b.ToqueMelhoraAlma += Total(p, EfeitoDeTalento.ToqueMelhoraAlma, cfg);
            b.PoderNecromantico += Total(p, EfeitoDeTalento.PoderNecromantico, cfg);
        }
    }
}
