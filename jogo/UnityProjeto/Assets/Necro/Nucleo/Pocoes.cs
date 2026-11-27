using System;

namespace Necro.Nucleo
{
    /// <summary>Poções e cinto (Diablo 1): compradas na Curandeira com ouro, levadas no cinto e bebidas sozinhas pela regra do jogador.</summary>
    public static class Pocoes
    {
        /// <summary>Poções de cada tipo que cabem no cinto (o ramo Sangue da Árvore aumenta).</summary>
        public static int Capacidade(EstadoProgresso p, ConfigJogo cfg) { return cfg.CintoPorTipo + (cfg.ArvoreDeAscensao && Arvore.Final(p, 2) ? cfg.CintoDoCarniceiro : 0); }

        public static long Preco(EstadoProgresso p, bool mana, ConfigJogo cfg)
        {
            double b = mana ? cfg.PocaoManaPreco : cfg.PocaoVidaPreco;
            return Math.Max(1, (long)Math.Round(b * (1.0 + cfg.PocaoPrecoPorAndar * p.MaiorAndarConcluido)));
        }

        public static bool Comprar(EstadoProgresso p, bool mana, ConfigJogo cfg)
        {
            long c = Preco(p, mana, cfg);
            if (p.Ouro < c) return false;
            if (mana) { if (p.PocoesDeMana >= Capacidade(p, cfg)) return false; p.PocoesDeMana++; }
            else { if (p.PocoesDeVida >= Capacidade(p, cfg)) return false; p.PocoesDeVida++; }
            p.Ouro -= c; p.PocoesCompradas++;
            return true;
        }

        /// <summary>Enche o cinto com o ouro que houver, alternando vida e mana. Devolve quantas comprou.</summary>
        public static int Repor(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0; bool comprou = true;
            while (comprou) { comprou = false; if (Comprar(p, false, cfg)) { n++; comprou = true; } if (Comprar(p, true, cfg)) { n++; comprou = true; } }
            return n;
        }

        /// <summary>Fim da descida: tira do cinto o que foi bebido, guarda o que foi achado e repõe se a reposição automática estiver ligada.</summary>
        public static void AoVoltar(EstadoProgresso p, ResultadoExpedicao r, ConfigJogo cfg)
        {
            p.PocoesDeVida = Math.Max(0, Math.Min(Capacidade(p, cfg), p.PocoesDeVida - r.PocoesDeVidaUsadas + r.PocoesDeVidaAchadas));
            p.PocoesDeMana = Math.Max(0, Math.Min(Capacidade(p, cfg), p.PocoesDeMana - r.PocoesDeManaUsadas + r.PocoesDeManaAchadas));
            if (!p.SemReporPocoes) Repor(p, cfg);
        }
    }
}
