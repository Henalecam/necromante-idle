using System;

namespace Necro.Nucleo
{
    /// <summary>A Bruxa (Adria, no Diablo 1): vende livros de feitiço por ouro. Estoque pequeno, que muda a cada descida.</summary>
    public static class Bruxa
    {
        public static void Renovar(EstadoProgresso p, ConfigJogo cfg)
        {
            var rng = new Rng(424243UL + (ulong)p.ContadorDeSementes * 17UL + (ulong)p.LojaRenovacoes * 7UL);
            p.LivrosDaBruxa = new int[cfg.BruxaLivros];
            for (int i = 0; i < p.LivrosDaBruxa.Length; i++)
            {
                int q = rng.Intervalo(0, Grimorio.Quantos);
                if (i > 0 && q == p.LivrosDaBruxa[0]) q = (q + 1 + rng.Intervalo(0, Grimorio.Quantos - 1)) % Grimorio.Quantos;   // dois livros diferentes
                p.LivrosDaBruxa[i] = q;
            }
        }

        /// <summary>Preço do livro na prateleira: sobe com o nível que o feitiço já tem e com o recorde. 0 = vendido ou prateleira vazia.</summary>
        public static long Preco(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (p.LivrosDaBruxa == null || indice < 0 || indice >= p.LivrosDaBruxa.Length || p.LivrosDaBruxa[indice] < 0) return 0;
            var id = (FeiticoId)p.LivrosDaBruxa[indice];
            int ja = Grimorio.Nivel(p, id) + Grimorio.Guardados(p, id);
            if (ja >= cfg.LivroNivelMaximo) return 0;
            double c = cfg.BruxaPrecoBase * (1.0 + cfg.BruxaPrecoPorNivel * ja) * (1.0 + cfg.BruxaPrecoPorAndar * p.MaiorAndarConcluido);
            return Math.Max(1, (long)Math.Round(c));
        }

        public static bool Comprar(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            long c = Preco(p, indice, cfg);
            if (c <= 0 || p.Ouro < c) return false;
            p.Ouro -= c;
            Grimorio.Receber(p, (FeiticoId)p.LivrosDaBruxa[indice], cfg);
            p.LivrosDaBruxa[indice] = -1; p.LivrosComprados++;
            return true;
        }

        public static long PrecoDoPergaminho(EstadoProgresso p, ConfigJogo cfg) { return Math.Max(1, (long)Math.Round(cfg.PergaminhoPreco * (1.0 + cfg.PergaminhoPrecoPorAndar * p.MaiorAndarConcluido))); }

        /// <summary>Pergaminho de Portal da Cidade: até o máximo que se pode carregar.</summary>
        public static bool ComprarPergaminho(EstadoProgresso p, ConfigJogo cfg)
        {
            long c = PrecoDoPergaminho(p, cfg);
            if (!cfg.PortalDaCidade || p.PergaminhosDePortal >= cfg.PergaminhosMaximo || p.Ouro < c) return false;
            p.Ouro -= c; p.PergaminhosDePortal++;
            return true;
        }

        /// <summary>Compra o que der, do mais barato ao mais caro. Devolve quantos.</summary>
        public static int ComprarTudo(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0;
            while (true)
            {
                int melhor = -1; long menor = long.MaxValue;
                for (int i = 0; p.LivrosDaBruxa != null && i < p.LivrosDaBruxa.Length; i++) { long c = Preco(p, i, cfg); if (c > 0 && c < menor) { menor = c; melhor = i; } }
                if (melhor < 0 || !Comprar(p, melhor, cfg)) break;
                n++;
            }
            return n;
        }
    }
}
