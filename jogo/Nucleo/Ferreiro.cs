using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>
    /// O Ferreiro da cidade, como o Griswold do Diablo 1: compra o que você traz da masmorra e vende um punhado de itens
    /// que muda a cada expedição. Tudo em Ouro.
    /// </summary>
    public static class Ferreiro
    {
        public static long PrecoDeVenda(Item item, ConfigJogo cfg)
        {
            if (Equipamento.Vazio(item)) return 0;
            double v = cfg.OuroVendaPorRaridade[Math.Min((int)item.Raridade, cfg.OuroVendaPorRaridade.Length - 1)] * (1.0 + cfg.OuroPorNivelDeItem * (Math.Max(1, item.Nivel) - 1));
            if (item.NaoIdentificado) v *= cfg.VendaNaoIdentificado;
            return Math.Max(1L, (long)Math.Round(v));
        }

        // ---- Identificação (o Cain do Diablo 1 cobra em ouro para revelar o que um item mágico faz) ----
        public static long CustoDeIdentificar(Item item, ConfigJogo cfg)
        {
            if (Equipamento.Vazio(item) || !item.NaoIdentificado) return 0;
            double v = cfg.OuroParaIdentificar[Math.Min((int)item.Raridade, cfg.OuroParaIdentificar.Length - 1)] * (1.0 + 0.1 * (Math.Max(1, item.Nivel) - 1));
            return Math.Max(1L, (long)Math.Round(v));
        }

        public static bool Identificar(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            if (item == null || !item.NaoIdentificado) return false;
            long c = CustoDeIdentificar(item, cfg);
            if (p.Ouro < c) return false;
            p.Ouro -= c; item.NaoIdentificado = false; p.ItensIdentificados++;
            return true;
        }

        /// <summary>Identifica o que o ouro alcançar, do mais raro para o mais comum. Devolve quantos.</summary>
        public static int IdentificarTudo(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0;
            for (int r = (int)RaridadeItem.Profano; r >= 0; r--)
                for (int i = 0; i < p.Inventario.Count; i++)
                    if (p.Inventario[i] != null && p.Inventario[i].NaoIdentificado && (int)p.Inventario[i].Raridade == r && Identificar(p, p.Inventario[i], cfg)) n++;
            return n;
        }

        public static long PrecoDeCompra(Item item, ConfigJogo cfg) { return (long)Math.Round(PrecoDeVenda(item, cfg) * (double)cfg.LojaMargem); }

        /// <summary>Vende um item do inventário (não vale para travados nem equipados). Devolve o Ouro ganho.</summary>
        public static long Vender(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            if (item == null || item.Bloqueado || !p.Inventario.Remove(item)) return 0;
            long v = PrecoDeVenda(item, cfg);
            p.Ouro += v; p.ItensVendidos++;
            return v;
        }

        /// <summary>Troca o estoque: itens do nível do andar mais fundo já vencido, ao menos Mágicos, o último ao menos Raro.</summary>
        public static void Renovar(EstadoProgresso p, ConfigJogo cfg)
        {
            if (p.Loja == null) p.Loja = new List<Item>();
            p.Loja.Clear();
            var rng = new Rng(77771UL + (ulong)p.ContadorDeSementes * 31UL + (ulong)p.LojaRenovacoes++);
            int nivel = Math.Max(1, p.MaiorAndarConcluido);
            for (int i = 0; i < cfg.LojaItens; i++)
                p.Loja.Add(GeradorDeItens.Gerar(rng, cfg, nivel, i == cfg.LojaItens - 1 ? RaridadeItem.Raro : RaridadeItem.Magico));
        }

        public static bool PodeComprar(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            return p.Loja != null && indice >= 0 && indice < p.Loja.Count && p.Ouro >= PrecoDeCompra(p.Loja[indice], cfg) && Equipamento.Cabe(p, p.Loja[indice], cfg);
        }

        public static bool Comprar(EstadoProgresso p, int indice, ConfigJogo cfg)
        {
            if (!PodeComprar(p, indice, cfg)) return false;
            var it = p.Loja[indice];
            p.Ouro -= PrecoDeCompra(it, cfg);
            p.Loja.RemoveAt(indice);
            p.Inventario.Add(it); p.ItensComprados++;
            return true;
        }
    }
}
