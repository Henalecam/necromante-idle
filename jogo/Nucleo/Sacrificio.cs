using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Sacrifício de minions: o minion some e vira Fragmentos de Alma. Os Fragmentos servem para Imbuir um minion,
    /// subindo a qualidade da alma dele. É o caminho para "alimentar o favorito com os fracos".
    /// A regra automática (EstadoProgresso.SacrificarAbaixoDe) só toca em minions na reserva.
    /// </summary>
    public static class Sacrificio
    {
        public static long Fragmentos(MinionSalvo m, ConfigJogo cfg)
        {
            double v = cfg.FragmentosPorQualidade[(int)m.Alma.Qualidade] * (1.0 + cfg.FragmentosPorNivel * (m.Nivel - 1));
            return Math.Max(1, (long)Math.Round(v));
        }

        /// <summary>Sacrifica o minion e devolve os Fragmentos ganhos.</summary>
        public static long Sacrificar(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (indice < 0 || indice >= p.Minions.Count) return 0;
            long f = Fragmentos(p.Minions[indice], cfg);
            p.Minions.RemoveAt(indice);
            p.FragmentosDeAlma += f; p.MinionsSacrificados++;
            Missoes.Registrar(p, AcaoDeMissao.Sacrificar, cfg);
            return f;
        }

        /// <summary>Aplica a regra automática: sacrifica minions da reserva com qualidade abaixo da regra. Devolve quantos.</summary>
        public static int AplicarRegra(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0;
            for (int i = p.Minions.Count - 1; i >= 0; i--)
                if (!p.Minions[i].Ativo && p.Minions[i].Alma.Qualidade < p.SacrificarAbaixoDe) { Sacrificar(p, i, cfg); n++; }
            return n;
        }

        public static long CustoDeImbuir(MinionSalvo m, ConfigJogo cfg)
        {
            return m.Alma.Qualidade < Qualidade.Profana ? cfg.FragmentosParaImbuir[(int)m.Alma.Qualidade] : 0;
        }

        public static bool PodeImbuir(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (indice < 0 || indice >= p.Minions.Count) return false;
            var m = p.Minions[indice];
            return m.Alma.Qualidade < Qualidade.Profana && p.FragmentosDeAlma >= CustoDeImbuir(m, cfg);
        }

        /// <summary>Gasta Fragmentos para subir a qualidade da alma do minion em um degrau (ele mantém nível, estágio e traços).</summary>
        public static bool Imbuir(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (!PodeImbuir(p, indice, cfg)) return false;
            var m = p.Minions[indice];
            p.FragmentosDeAlma -= CustoDeImbuir(m, cfg);
            m.Alma.Qualidade++;
            if (m.Alma.Qualidade == Qualidade.Profana && m.Alma.Traco == TracoProfano.Nenhum)
                m.Alma.Traco = (TracoProfano)(1 + new Rng(p.ContadorDeSementes++ * 0x9E3779B97F4A7C15UL + 0x1B01UL).Intervalo(0, 3));
            return true;
        }
    }
}
