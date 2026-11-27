using System;

namespace Necro.Nucleo
{
    [Serializable]
    public class DadosLegado
    {
        public string Nome;
        public string Texto;            // "{0}" = valor total em %
        public float PorNivel;
        public int Maximo;
    }

    /// <summary>
    /// Ascensão: depois de vencer a chefe do Ato 1, o necromante pode morrer de propósito e recomeçar. Perde a vida atual
    /// (níveis, recursos, construções, exército, talentos, altares) e leva Cinzas, que compram Legados permanentes.
    /// Atravessam a Ascensão: o Bestiário, as missões, as regras automáticas, os Legados e os itens TRAVADOS.
    /// </summary>
    public static class Ascensao
    {
        public const int Legados = 3;   // 0 Ossos, 1 Sangue (Poder dos minions), 2 Sombras (chance de alma)

        public static bool Pode(EstadoProgresso p) { return p.ChefesDerrotados.Contains(1); }

        /// <summary>Parte das Cinzas que vem da profundidade: (maior andar / 5) ao quadrado. Andar 10 = 4, 15 = 9, 20 = 16, 30 = 36.</summary>
        public static long CinzasDoAndar(EstadoProgresso p) { double a = p.MaiorAndarConcluido / 5.0; return (long)Math.Floor(a * a); }
        /// <summary>Parte que vem dos chefes de ato vencidos nesta vida.</summary>
        public static long CinzasDosChefes(EstadoProgresso p, ConfigJogo cfg) { return cfg.CinzasPorChefe * p.ChefesDerrotados.Count; }
        /// <summary>Multiplicador da dificuldade em que se está ao ascender.</summary>
        public static float FatorDaDificuldade(EstadoProgresso p, ConfigJogo cfg) { int d = Math.Max(0, Math.Min(cfg.CinzasPorDificuldade.Length - 1, p.Dificuldade)); return cfg.CinzasPorDificuldade[d]; }

        /// <summary>Cinzas que esta vida rende: (profundidade + chefes) x dificuldade.</summary>
        public static long CinzasDestaVida(EstadoProgresso p, ConfigJogo cfg)
        {
            return (long)Math.Floor((CinzasDoAndar(p) + CinzasDosChefes(p, cfg)) * (double)FatorDaDificuldade(p, cfg) + 1e-6);
        }

        public static int Nivel(EstadoProgresso p, int legado) { return p.Legados != null && legado < p.Legados.Length ? p.Legados[legado] : 0; }

        public static long Custo(EstadoProgresso p, int legado) { return Nivel(p, legado) + 1; }

        public static bool PodeComprar(EstadoProgresso p, int legado, ConfigJogo cfg)
        {
            return legado >= 0 && legado < Legados && Nivel(p, legado) < cfg.Legados[legado].Maximo && p.Cinzas >= Custo(p, legado);
        }

        public static bool Comprar(EstadoProgresso p, int legado, ConfigJogo cfg)
        {
            if (!PodeComprar(p, legado, cfg)) return false;
            p.Cinzas -= Custo(p, legado);
            if (p.Legados == null || p.Legados.Length < Legados) { var n = new int[Legados]; if (p.Legados != null) Array.Copy(p.Legados, n, p.Legados.Length); p.Legados = n; }
            p.Legados[legado]++;
            return true;
        }

        public static float Valor(EstadoProgresso p, int legado, ConfigJogo cfg) { return cfg.Legados[legado].PorNivel * Nivel(p, legado); }

        public static void Aplicar(EstadoProgresso p, ConfigJogo cfg, BonusEquipamento b)
        {
            if (p.Legados == null) return;
            b.PoderNecromantico += Valor(p, 1, cfg);
            b.ChanceDeAlma += Valor(p, 2, cfg);
        }

        /// <summary>Recomeça a vida. Devolve as Cinzas ganhas (0 se ainda não pode).</summary>
        public static long Ascender(EstadoProgresso p, ConfigJogo cfg)
        {
            if (!Pode(p)) return 0;
            long cinzas = CinzasDestaVida(p, cfg);
            var novo = Progressao.NovoJogo(cfg);
            // itens travados atravessam: os equipados travados voltam para o inventário
            var guardados = new System.Collections.Generic.List<Item>();
            for (int i = 0; i < p.Inventario.Count; i++) if (p.Inventario[i].Bloqueado) guardados.Add(p.Inventario[i]);
            if (p.Equipados != null) for (int i = 0; i < p.Equipados.Length; i++) if (!Equipamento.Vazio(p.Equipados[i]) && p.Equipados[i].Bloqueado) guardados.Add(p.Equipados[i]);

            p.NivelNecromante = novo.NivelNecromante; p.XpNecromante = 0f;
            p.MaiorAndarConcluido = 0; p.AndarDeAncoragem = novo.AndarDeAncoragem; p.ChefesDerrotados.Clear();
            p.Ossos = 0; p.Essencia = 0; p.Sangue = 0; p.FragmentosDeAlma = 0; p.Ouro = 0; if (p.Loja != null) p.Loja.Clear();
            for (int i = 0; i < p.Restos.Length; i++) p.Restos[i] = 0;
            p.Filacterio.Clear();
            p.Construcoes = new int[Necropole.TotalDeConstrucoes];
            p.Talentos = new int[cfg.Talentos.Length];
            Atributos.Zerar(p);
            p.Oferendas = new int[Oferendas.Tipos];
            p.RitoPreparado = Rito.Nenhum;
            // o que mais pertence à vida que acaba: livros lidos, cinto, portal, prateleira da Bruxa, missões (sorteadas de novo) e a descida de cada dificuldade
            p.NiveisDeFeitico = new int[Grimorio.Quantos]; p.LivrosGuardados = new int[Grimorio.Quantos];
            p.PocoesDeVida = 0; p.PocoesDeMana = 0; p.PergaminhosDePortal = 0; p.PortalNoAndar = 0; p.LivrosDaBruxa = null;
            p.MissaoAcougueiro = 0; p.MissaoReiEsqueleto = 0; p.MissaoFonte = 0; p.MissaoCamara = 0; p.MissaoCegos = 0;
            p.Dificuldade = 0; p.AncoragemPorDificuldade = null;
            p.Minions.Clear(); p.Minions.AddRange(novo.Minions);
            p.Inventario.Clear(); p.Equipados = new Item[9];
            for (int i = 0; i < guardados.Count; i++) if (Equipamento.Cabe(p, guardados[i], cfg)) p.Inventario.Add(guardados[i]);

            p.Cinzas += cinzas; p.CinzasGanhas += cinzas; p.Ascensoes++;
            Arvore.AplicarHeranca(p, cfg);   // o ramo Herança adianta a vida nova
            return cinzas;
        }
    }
}
