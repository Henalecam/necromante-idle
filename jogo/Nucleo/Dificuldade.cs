using System;

namespace Necro.Nucleo
{
    /// <summary>Dificuldades como no Diablo 1: Normal, Pesadelo e Inferno. Inimigos mais fortes, saque melhor.</summary>
    public static class Dificuldades
    {
        public static readonly string[] Nomes = { "Normal", "Pesadelo", "Inferno" };
        public const int Maxima = 2;

        public static int Atual(EstadoProgresso p) { return p.Dificuldade < 0 ? 0 : p.Dificuldade > Maxima ? Maxima : p.Dificuldade; }

        public static bool Pode(EstadoProgresso p, int d) { return d >= 0 && d <= Maxima && d <= p.DificuldadeLiberada; }

        /// <summary>Vale a partir da próxima expedição.</summary>
        public static bool Definir(EstadoProgresso p, int d)
        {
            if (!Pode(p, d)) return false;
            int atual = Atual(p);
            if (d == atual) return true;
            // cada dificuldade tem a sua própria descida (como no Diablo: jogo novo, do andar 1): guarda a ancoragem desta e retoma a da outra
            if (p.AncoragemPorDificuldade == null || p.AncoragemPorDificuldade.Length != Maxima + 1) p.AncoragemPorDificuldade = new int[Maxima + 1];
            p.AncoragemPorDificuldade[atual] = p.AndarDeAncoragem;
            p.AndarDeAncoragem = Math.Max(1, p.AncoragemPorDificuldade[d]);
            p.PortalNoAndar = 0;
            p.Dificuldade = d;
            return true;
        }

        /// <summary>Vencer o chefe do Ato 2 na dificuldade mais alta já liberada abre a seguinte.</summary>
        public static void Registrar(EstadoProgresso p, ResultadoExpedicao r, ConfigJogo cfg)
        {
            if (r.Dificuldade == p.DificuldadeLiberada && p.DificuldadeLiberada < Maxima && (r.ChefesVencidosNaDescida.Contains(cfg.DificuldadeAbreNoAto) || r.ChefesDerrotados.Contains(cfg.DificuldadeAbreNoAto))) p.DificuldadeLiberada++;
        }
    }
}
