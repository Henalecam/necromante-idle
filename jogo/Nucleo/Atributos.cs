using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Os quatro atributos do Diablo 1, com cinco pontos por nível, adaptados a um necromante que luta pelo exército:
    /// Força (dano dos minions), Magia (feitiços e mana), Destreza (crítico e velocidade de ataque dos minions) e
    /// Vitalidade (vida do necromante e dos minions).
    /// </summary>
    public enum Atributo { Forca = 0, Magia = 1, Destreza = 2, Vitalidade = 3 }

    public static class Atributos
    {
        public const int Quantos = 4;

        static void Garantir(EstadoProgresso p) { if (p.Atributos == null || p.Atributos.Length != Quantos) { var n = new int[Quantos]; if (p.Atributos != null) Array.Copy(p.Atributos, n, Math.Min(Quantos, p.Atributos.Length)); p.Atributos = n; } }

        public static int Totais(EstadoProgresso p, ConfigJogo cfg) { return cfg.PontosDeAtributoPorNivel * Math.Max(0, p.NivelNecromante - 1); }
        public static int Gastos(EstadoProgresso p) { Garantir(p); int s = 0; for (int i = 0; i < Quantos; i++) s += p.Atributos[i]; return s; }
        public static int Livres(EstadoProgresso p, ConfigJogo cfg) { return Math.Max(0, Totais(p, cfg) - Gastos(p)); }
        public static int Valor(EstadoProgresso p, Atributo a) { Garantir(p); return p.Atributos[(int)a]; }

        public static bool Gastar(EstadoProgresso p, Atributo a, int pontos, ConfigJogo cfg)
        {
            if (pontos <= 0 || Livres(p, cfg) < pontos) return false;
            p.Atributos[(int)a] += pontos;
            return true;
        }

        /// <summary>Distribui os pontos livres por igual, sempre no atributo mais baixo (o jogo é idle: ninguém precisa voltar só para isso).</summary>
        public static int AutoDistribuir(EstadoProgresso p, ConfigJogo cfg)
        {
            Garantir(p);
            int n = 0, livres = Livres(p, cfg);
            // primeiro o que destrava o melhor item guardado que ainda não dá para vestir (se os pontos livres bastarem)
            int guarda = 0;
            while (livres > 0 && cfg.RequisitoDeAtributo && guarda++ < 12)
            {
                Item melhor = null; float ganho = 0f;
                for (int i = 0; i < p.Inventario.Count; i++)
                {
                    var it = p.Inventario[i];
                    if (Equipamento.Vazio(it) || it.NaoIdentificado) continue;
                    int falta = Requisitos.Falta(p, it, cfg); if (falta <= 0 || falta > livres) continue;
                    var esp = TabelaItens.EspacoDoTipo(it.Tipo); float atual = Equipamento.Pontuacao(Equipamento.Obter(p, esp), cfg);
                    if (esp == EspacoEquipamento.Anel1) atual = Math.Min(atual, Equipamento.Pontuacao(Equipamento.Obter(p, EspacoEquipamento.Anel2), cfg));
                    float g = Equipamento.Pontuacao(it, cfg) - atual;
                    if (g > ganho) { ganho = g; melhor = it; }
                }
                if (melhor == null) break;
                int f = Requisitos.Falta(p, melhor, cfg); p.Atributos[(int)Requisitos.AtributoDe(melhor.Tipo)] += f; livres -= f; n += f;
            }
            while (livres-- > 0)
            {
                int menor = 0; for (int i = 1; i < Quantos; i++) if (p.Atributos[i] < p.Atributos[menor]) menor = i;
                p.Atributos[menor]++; n++;
            }
            return n;
        }

        public static void Zerar(EstadoProgresso p) { p.Atributos = new int[Quantos]; }

        /// <summary>Devolve todos os pontos (custa Essência, como redistribuir talentos).</summary>
        public static long CustoDeRedistribuir(EstadoProgresso p, ConfigJogo cfg) { return cfg.EssenciaPorPontoDeAtributoRedistribuido * Gastos(p); }
        public static bool Redistribuir(EstadoProgresso p, ConfigJogo cfg)
        {
            long c = CustoDeRedistribuir(p, cfg);
            if (Gastos(p) == 0 || p.Essencia < c) return false;
            p.Essencia -= c; Zerar(p); p.SemAutoAtributos = true;   // quem redistribui quer escolher à mão
            return true;
        }

        public static void Aplicar(EstadoProgresso p, ConfigJogo cfg, BonusEquipamento b)
        {
            Garantir(p);
            int f = p.Atributos[0], m = p.Atributos[1], d = p.Atributos[2], v = p.Atributos[3];
            b.DanoDosMinions += f * cfg.ForcaDanoDosMinions;
            b.DanoDeFeiticos += m * cfg.MagiaDanoDeFeiticos; b.Mana += m * cfg.MagiaMana;
            b.ChanceCritico += d * cfg.DestrezaCritico; b.VelAtaqueDosMinions += d * cfg.DestrezaVelAtaque;
            b.VidaDosMinions += v * cfg.VitalidadeVidaDosMinions; b.VidaDoNecromantePercentual += v * cfg.VitalidadeVidaDoNecromante;
        }
    }
}
