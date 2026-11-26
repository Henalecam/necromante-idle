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

        // ---- Durabilidade e reparo (o Griswold do Diablo 1 conserta o que a masmorra gasta) ----
        public static int DurabilidadeMax(Item item, ConfigJogo cfg) { return cfg.DurabilidadeBase + cfg.DurabilidadePorRaridade * (int)item.Raridade; }
        public static int DurabilidadeAtual(Item item, ConfigJogo cfg) { return Math.Max(0, DurabilidadeMax(item, cfg) - item.Desgaste); }
        public static bool Quebrado(Item item, ConfigJogo cfg) { return cfg.Durabilidade && !Equipamento.Vazio(item) && item.Desgaste >= DurabilidadeMax(item, cfg); }

        /// <summary>Uma expedição gasta um pouco de tudo o que está equipado.</summary>
        public static void Desgastar(EstadoProgresso p, ConfigJogo cfg)
        {
            if (!cfg.Durabilidade || p.Equipados == null) return;
            for (int i = 0; i < p.Equipados.Length; i++)
            {
                var it = p.Equipados[i];
                if (Equipamento.Vazio(it)) continue;
                it.Desgaste = Math.Min(DurabilidadeMax(it, cfg), it.Desgaste + cfg.DesgastePorExpedicao);
            }
        }

        public static long CustoDeReparo(Item item, ConfigJogo cfg)
        {
            if (Equipamento.Vazio(item) || item.Desgaste <= 0) return 0;
            bool fechado = item.NaoIdentificado; item.NaoIdentificado = false;
            double cheio = PrecoDeVenda(item, cfg) * (double)cfg.ReparoFracao; item.NaoIdentificado = fechado;
            return Math.Max(1L, (long)Math.Ceiling(cheio * Math.Min(1.0, item.Desgaste / (double)DurabilidadeMax(item, cfg))));
        }

        public static bool Reparar(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            long c = CustoDeReparo(item, cfg);
            if (c <= 0 || p.Ouro < c) return false;
            p.Ouro -= c; item.Desgaste = 0; p.ItensReparados++;
            return true;
        }

        public static long CustoDeRepararTudo(EstadoProgresso p, ConfigJogo cfg)
        {
            long t = 0; if (p.Equipados != null) for (int i = 0; i < p.Equipados.Length; i++) t += CustoDeReparo(p.Equipados[i], cfg);
            return t;
        }

        /// <summary>Repara os equipados que o ouro alcançar, começando pelos mais gastos. Devolve quantos.</summary>
        public static int RepararTudo(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = 0; if (p.Equipados == null) return 0;
            var ordem = new List<Item>();
            for (int i = 0; i < p.Equipados.Length; i++) if (!Equipamento.Vazio(p.Equipados[i]) && p.Equipados[i].Desgaste > 0) ordem.Add(p.Equipados[i]);
            ordem.Sort((a, b) => (b.Desgaste * DurabilidadeMax(a, cfg)).CompareTo(a.Desgaste * DurabilidadeMax(b, cfg)));
            for (int i = 0; i < ordem.Count; i++) if (Reparar(p, ordem[i], cfg)) n++;
            return n;
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
