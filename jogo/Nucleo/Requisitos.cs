using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Requisito de atributo dos itens, como no Diablo 1: cada peça pede um mínimo de Força, Magia ou Destreza para ser equipada.
    /// O requisito não fica gravado no item: sai do tipo, do nível e da raridade, então vale também para itens de saves antigos
    /// (o que já está equipado continua equipado; só não dá para vestir de novo sem o atributo).
    /// </summary>
    public static class Requisitos
    {
        public static Atributo AtributoDe(TipoItem t)
        {
            switch (t)
            {
                case TipoItem.Foice: case TipoItem.Elmo: case TipoItem.Peitoral: return Atributo.Forca;
                case TipoItem.Luvas: case TipoItem.Botas: case TipoItem.Anel: return Atributo.Destreza;
                default: return Atributo.Magia;   // cajado, grimório, relicário, amuleto
            }
        }

        /// <summary>Quanto do atributo o item pede (0 = qualquer um usa).</summary>
        public static int Valor(Item item, ConfigJogo cfg)
        {
            if (!cfg.RequisitoDeAtributo || Equipamento.Vazio(item)) return 0;
            float v = (item.Nivel - cfg.RequisitoNivelLivre) * cfg.RequisitoPorNivel * (1f + cfg.RequisitoPorRaridade * (int)item.Raridade);
            return v <= 0f ? 0 : (int)Math.Round(v);
        }

        public static bool Atende(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            int v = Valor(item, cfg);
            return v <= 0 || Atributos.Valor(p, AtributoDe(item.Tipo)) >= v;
        }

        /// <summary>Quantos pontos faltam para poder equipar (0 = já pode).</summary>
        public static int Falta(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            return Math.Max(0, Valor(item, cfg) - Atributos.Valor(p, AtributoDe(item.Tipo)));
        }
    }
}
