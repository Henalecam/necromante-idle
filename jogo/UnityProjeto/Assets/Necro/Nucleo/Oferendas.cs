using System;

namespace Necro.Nucleo
{
    /// <summary>Rito de Sangue: gasta Sangue para mudar a PRÓXIMA expedição. Um por vez; some ao partir.</summary>
    public enum Rito { Nenhum = 0, Pressa = 1, Colheita = 2, Banquete = 3 }

    [Serializable]
    public class DadosRito
    {
        public Rito Id;
        public string Nome;
        public string Efeito;
        public long Sangue;
    }

    [Serializable]
    public class DadosOferenda
    {
        public string Nome;
        public string Texto;               // "{0}" = valor total em %
        public EfeitoDeTalento Efeito;     // reaproveita os efeitos dos talentos
        public float PorNivel;
    }

    /// <summary>
    /// Oferendas: cada tipo de Resto de cadáver (o jeito como o inimigo morreu) alimenta um altar próprio, com bônus permanente.
    /// Liga o estilo de matar ao que se ganha: quem queima muito junta Restos Queimados, que fortalecem os feitiços.
    /// Ritos de Sangue: o Sangue de elites, chefes e execuções compra uma vantagem para a próxima expedição.
    /// </summary>
    public static class Oferendas
    {
        public const int Tipos = 7;   // um por CondicaoCadaver

        public static int Nivel(EstadoProgresso p, int tipo) { return p.Oferendas != null && tipo < p.Oferendas.Length ? p.Oferendas[tipo] : 0; }

        public static long Custo(EstadoProgresso p, int tipo, ConfigJogo cfg)
        {
            return (long)Math.Ceiling(cfg.RestosPorOferenda * Math.Pow(cfg.CrescimentoDaOferenda, Nivel(p, tipo)));
        }

        public static bool Pode(EstadoProgresso p, int tipo, ConfigJogo cfg)
        {
            return tipo >= 0 && tipo < Tipos && Nivel(p, tipo) < cfg.NivelMaximoDaOferenda && p.Restos[tipo] >= Custo(p, tipo, cfg);
        }

        public static bool Ofertar(EstadoProgresso p, int tipo, ConfigJogo cfg)
        {
            if (!Pode(p, tipo, cfg)) return false;
            p.Restos[tipo] -= Custo(p, tipo, cfg);
            if (p.Oferendas == null || p.Oferendas.Length < Tipos) { var n = new int[Tipos]; if (p.Oferendas != null) Array.Copy(p.Oferendas, n, p.Oferendas.Length); p.Oferendas = n; }
            p.Oferendas[tipo]++;
            Missoes.Registrar(p, AcaoDeMissao.Ofertar, cfg);
            return true;
        }

        /// <summary>Oferta tudo o que os Restos guardados pagam (cada Resto só serve a um altar, então não há o que escolher). Devolve quantos níveis subiram.</summary>
        public static int OfertarTudo(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0;
            for (int t = 0; t < Tipos; t++) while (Ofertar(p, t, cfg)) n++;
            return n;
        }

        public static float Total(EstadoProgresso p, EfeitoDeTalento efeito, ConfigJogo cfg)
        {
            float v = 0f;
            for (int i = 0; i < Tipos && i < cfg.Oferendas.Length; i++) if (cfg.Oferendas[i].Efeito == efeito) v += cfg.Oferendas[i].PorNivel * Nivel(p, i);
            return v;
        }

        public static void Aplicar(EstadoProgresso p, ConfigJogo cfg, BonusEquipamento b)
        {
            if (p.Oferendas == null) return;
            b.VidaDosMinions += Total(p, EfeitoDeTalento.VidaDosMinions, cfg);
            b.DanoDosMinions += Total(p, EfeitoDeTalento.DanoDosMinions, cfg);
            b.VelAtaqueDosMinions += Total(p, EfeitoDeTalento.VelAtaqueDosMinions, cfg);
            b.DanoDeFeiticos += Total(p, EfeitoDeTalento.DanoDeFeiticos, cfg);
            b.VidaDoNecromantePercentual += Total(p, EfeitoDeTalento.VidaDoNecromante, cfg);
            b.ChanceDeAlma += Total(p, EfeitoDeTalento.ChanceDeAlma, cfg);
        }

        // ---- Ritos de Sangue ----

        public static DadosRito DadosDoRito(Rito r, ConfigJogo cfg)
        {
            for (int i = 0; i < cfg.Ritos.Length; i++) if (cfg.Ritos[i].Id == r) return cfg.Ritos[i];
            return null;
        }

        /// <summary>Prepara um rito para a próxima expedição (substitui o anterior sem devolver o Sangue).</summary>
        public static bool PrepararRito(EstadoProgresso p, Rito r, ConfigJogo cfg)
        {
            var d = DadosDoRito(r, cfg);
            if (d == null || p.RitoPreparado == r || p.Sangue < d.Sangue) return false;
            p.Sangue -= d.Sangue; p.RitoPreparado = r;
            return true;
        }

        /// <summary>Chamado por quem cria a Simulacao, logo depois de criá-la: o rito preparado foi gasto.</summary>
        public static void ConsumirRito(EstadoProgresso p) { p.RitoPreparado = Rito.Nenhum; }
    }
}
