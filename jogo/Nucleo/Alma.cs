using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>Uma alma ceifada: espécie + qualidade + afixos (+ traço, se Profana).</summary>
    [Serializable]
    public class Alma
    {
        public EspecieId Especie;
        public Qualidade Qualidade;
        public List<Afixo> Afixos = new List<Afixo>();
        public TracoProfano Traco;
        /// <summary>Dons: variação aleatória de nascença, em fração (0.10 = +10%). Cada alma é um pouco diferente.</summary>
        public float DomVida, DomDano, DomVelocidade;

        public bool Tem(Afixo a) { return Afixos != null && Afixos.Contains(a); }

        public override string ToString()
        {
            string s = TabelaEspecies.Obter(Especie).Nome + " [" + Qualidade + "]";
            if (Afixos != null) for (int i = 0; i < Afixos.Count; i++) s += " " + Afixos[i];
            if (Traco != TracoProfano.Nenhum) s += " <" + Traco + ">";
            return s;
        }
    }

    public static class GeradorDeAlmas
    {
        private const int TotalAfixos = 6;
        private const int TotalTracos = 3;

        /// <summary>Chance de um inimigo morto soltar um orbe de alma.</summary>
        public static float ChanceDeDrop(ConfigJogo cfg, bool elite, bool chefe, bool chefeJaDerrotado)
        {
            if (chefe) return chefeJaDerrotado ? cfg.DropAlmaChefeRepetida : cfg.DropAlmaChefePrimeira;
            return elite ? cfg.DropAlmaElite : cfg.DropAlmaComum;
        }

        public static Qualidade SortearQualidade(Rng rng, ConfigJogo cfg)
        {
            float r = rng.Proximo01();
            float acumulado = 0f;
            for (int i = 0; i < cfg.ChanceQualidade.Length; i++)
            {
                acumulado += cfg.ChanceQualidade[i];
                if (r < acumulado) return (Qualidade)i;
            }
            return Qualidade.Fraca;
        }

        public static Alma Gerar(Rng rng, ConfigJogo cfg, EspecieId especie)
        {
            return Gerar(rng, cfg, especie, SortearQualidade(rng, cfg));
        }

        public static Alma Gerar(Rng rng, ConfigJogo cfg, EspecieId especie, Qualidade qualidade)
        {
            var alma = new Alma { Especie = especie, Qualidade = qualidade };
            int q = (int)qualidade;
            int n = rng.Intervalo(cfg.AfixosMin[q], cfg.AfixosMax[q] + 1);
            while (alma.Afixos.Count < n)
            {
                var a = (Afixo)rng.Intervalo(0, TotalAfixos);
                if (!alma.Afixos.Contains(a)) alma.Afixos.Add(a);
            }
            if (qualidade == Qualidade.Profana) alma.Traco = (TracoProfano)(1 + rng.Intervalo(0, TotalTracos));
            // dons: três sorteios independentes; a faixa cresce com a qualidade e pende para cima
            float faixa = cfg.DomBase + cfg.DomPorQualidade * q;
            alma.DomVida = rng.Intervalo(-faixa * cfg.DomParteNegativa, faixa);
            alma.DomDano = rng.Intervalo(-faixa * cfg.DomParteNegativa, faixa);
            alma.DomVelocidade = rng.Intervalo(-faixa * cfg.DomParteNegativa, faixa) * 0.6f;
            return alma;
        }
    }
}
