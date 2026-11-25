namespace Necro.Nucleo
{
    /// <summary>
    /// Bestiário: quanto mais o jogador mata de uma espécie, melhor ele a conhece. Cada marco de mortes dá um nível de
    /// Domínio, e os minions erguidos daquela espécie ficam mais fortes. Dá motivo para caçar uma espécie e para apostar nela.
    /// </summary>
    public static class Bestiario
    {
        public static long Mortes(EstadoProgresso p, EspecieId e)
        {
            return p.MortesPorEspecie != null && (int)e < p.MortesPorEspecie.Length ? p.MortesPorEspecie[(int)e] : 0;
        }

        /// <summary>Nível de Domínio: quantos marcos de mortes já foram alcançados.</summary>
        public static int Dominio(EstadoProgresso p, EspecieId e, ConfigJogo cfg)
        {
            long m = Mortes(p, e); int n = 0;
            while (n < cfg.MarcosDoBestiario.Length && m >= cfg.MarcosDoBestiario[n]) n++;
            return n;
        }

        /// <summary>Mortes do próximo marco, ou 0 se o Domínio já está no máximo.</summary>
        public static long ProximoMarco(EstadoProgresso p, EspecieId e, ConfigJogo cfg)
        {
            int d = Dominio(p, e, cfg);
            return d < cfg.MarcosDoBestiario.Length ? cfg.MarcosDoBestiario[d] : 0;
        }

        /// <summary>Multiplicador de vida e dano dos minions dessa espécie.</summary>
        public static float MultMinion(EstadoProgresso p, EspecieId e, ConfigJogo cfg) { return 1f + cfg.BonusPorDominio * Dominio(p, e, cfg); }

        public static void Registrar(EstadoProgresso p, ResultadoExpedicao r)
        {
            if (p.MortesPorEspecie == null || p.MortesPorEspecie.Length < r.MortesPorEspecie.Length) p.MortesPorEspecie = new long[r.MortesPorEspecie.Length];
            for (int i = 0; i < r.MortesPorEspecie.Length; i++) p.MortesPorEspecie[i] += r.MortesPorEspecie[i];
        }
    }
}
