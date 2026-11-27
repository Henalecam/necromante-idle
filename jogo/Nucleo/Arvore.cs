using System;

namespace Necro.Nucleo
{
    /// <summary>Um nó da Árvore de Ascensão.</summary>
    public sealed class NoDaArvore
    {
        public string Nome, Texto; public AfixoItem Afixo; public float Valor; public bool Final;
    }

    /// <summary>
    /// Árvore de Ascensão: quatro ramos de oito nós, comprados em ordem (cada nó pede o anterior). Os cinco primeiros custam
    /// Cinzas; os três últimos custam Cinzas e Selos do Abismo (ganhos vencendo chefes no Pesadelo e no Inferno).
    /// O último nó de cada ramo muda uma mecânica. Tudo aqui atravessa a Ascensão.
    /// </summary>
    public static class Arvore
    {
        public const int Ramos = 4, Nos = 8;
        public static readonly string[] NomesDosRamos = { "Invocador", "Ossos", "Sangue", "Pragas" };
        public static readonly string[] LemasDosRamos = { "o exército e o Golem", "feitiços e armadura", "vida, roubo de vida e poções", "almas, críticos e maldições" };

        private static NoDaArvore N(string nome, AfixoItem a, float v, string texto) { return new NoDaArvore { Nome = nome, Afixo = a, Valor = v, Texto = texto }; }
        private static NoDaArvore F(string nome, string texto) { return new NoDaArvore { Nome = nome, Texto = texto, Final = true }; }

        public static readonly NoDaArvore[][] Tabela =
        {
            new[] {   // Invocador
                N("Ossos Firmes", AfixoItem.VidaDosMinions, 0.08f, "+8% de vida dos minions"),
                N("Garras Afiadas", AfixoItem.DanoDosMinions, 0.08f, "+8% de dano dos minions"),
                N("Passo Marcado", AfixoItem.VelAtaqueDosMinions, 0.065f, "+6,5% de velocidade de ataque dos minions"),
                N("Voz de Comando", AfixoItem.Comando, 1f, "+1 de Comando"),
                N("Carne Reforçada", AfixoItem.VidaDosMinions, 0.13f, "+13% de vida dos minions"),
                N("Fúria dos Mortos", AfixoItem.DanoDosMinions, 0.13f, "+13% de dano dos minions"),
                N("Segunda Voz", AfixoItem.Comando, 1f, "+1 de Comando"),
                F("Senhor do Golem", "você já renasce sabendo o Golem, e ele dura o dobro"),
            },
            new[] {   // Ossos
                N("Lasca de Osso", AfixoItem.DanoDeFeiticos, 0.095f, "+9,5% de dano de feitiços"),
                N("Couraça de Costelas", AfixoItem.Armadura, 16f, "+16 de armadura"),
                N("Mente Fria", AfixoItem.Mana, 20f, "+20 de mana"),
                N("Conjuração Ligeira", AfixoItem.ReducaoDeRecarga, 0.065f, "+6,5% de redução de recarga"),
                N("Estilhaço", AfixoItem.DanoDeFeiticos, 0.16f, "+16% de dano de feitiços"),
                N("Fonte Escura", AfixoItem.RegenMana, 0.8f, "+0,8 de regeneração de mana por segundo"),
                N("Mão Rápida", AfixoItem.ReducaoDeRecarga, 0.095f, "+9,5% de redução de recarga"),
                F("Mãe Carniça", "toda Explosão de Cadáver deixa um morto temporário onde explodiu"),
            },
            new[] {   // Sangue
                N("Sangue Grosso", AfixoItem.Vida, 48f, "+48 de vida do necromante"),
                N("Sede", AfixoItem.RouboDeVidaDosMinions, 0.015f, "+1,5% de roubo de vida dos minions"),
                N("Golpe Cruel", AfixoItem.DanoCritico, 0.16f, "+16% de dano crítico"),
                N("Pele Curtida", AfixoItem.ResFisico, 0.08f, "+8% de resistência a físico"),
                N("Coração Negro", AfixoItem.Vida, 96f, "+96 de vida do necromante"),
                N("Banquete", AfixoItem.RouboDeVidaDosMinions, 0.025f, "+2,5% de roubo de vida dos minions"),
                N("Carnificina", AfixoItem.DanoCritico, 0.24f, "+24% de dano crítico"),
                F("Cinto do Carniceiro", "o cinto leva 2 poções a mais de cada tipo"),
            },
            new[] {   // Pragas
                N("Olho Clínico", AfixoItem.ChanceCritico, 0.025f, "+2,5% de chance de crítico"),
                N("Colheita", AfixoItem.ChanceDeAlma, 0.065f, "+6,5% de chance de alma"),
                N("Foice Leve", AfixoItem.VelocidadeDeCeifa, 0.095f, "+9,5% de velocidade de ceifa"),
                N("Sombra Amiga", AfixoItem.ResSombra, 0.095f, "+9,5% de resistência a sombra"),
                N("Golpe Certeiro", AfixoItem.ChanceCritico, 0.04f, "+4% de chance de crítico"),
                N("Safra Farta", AfixoItem.ChanceDeAlma, 0.095f, "+9,5% de chance de alma"),
                N("Veneno Lento", AfixoItem.ResVeneno, 0.13f, "+13% de resistência a veneno"),
                F("Praga Maior", "a Maldição da Fraqueza fica 50% mais forte"),
            },
        };

        private static void Garantir(EstadoProgresso p) { if (p.ArvoreDeAscensao == null || p.ArvoreDeAscensao.Length != Ramos) { var n = new int[Ramos]; if (p.ArvoreDeAscensao != null) Array.Copy(p.ArvoreDeAscensao, n, Math.Min(Ramos, p.ArvoreDeAscensao.Length)); p.ArvoreDeAscensao = n; } }

        /// <summary>Quantos nós do ramo já foram comprados (0 a 8).</summary>
        public static int Comprados(EstadoProgresso p, int ramo) { Garantir(p); return ramo >= 0 && ramo < Ramos ? p.ArvoreDeAscensao[ramo] : 0; }

        /// <summary>O ramo está completo: o nó final vale.</summary>
        public static bool Final(EstadoProgresso p, int ramo) { return Comprados(p, ramo) >= Nos; }

        public static long CustoEmCinzas(int no) { return no < 5 ? 2 + no : 8 + 2 * (no - 5); }           // nó 0..7: 2, 3, 4, 5, 6, 8, 10, 12 (ramo inteiro: 50)
        public static int CustoEmSelos(int no) { return no < 5 ? 0 : no - 4; }                              // 0 nos cinco primeiros; depois 1, 2, 3

        public static bool PodeComprar(EstadoProgresso p, int ramo)
        {
            int no = Comprados(p, ramo);
            return ramo >= 0 && ramo < Ramos && no < Nos && p.Cinzas >= CustoEmCinzas(no) && p.SelosDoAbismo >= CustoEmSelos(no);
        }

        public static bool Comprar(EstadoProgresso p, int ramo)
        {
            if (!PodeComprar(p, ramo)) return false;
            int no = p.ArvoreDeAscensao[ramo];
            p.Cinzas -= CustoEmCinzas(no); p.SelosDoAbismo -= CustoEmSelos(no); p.ArvoreDeAscensao[ramo]++;
            return true;
        }

        public static void Aplicar(EstadoProgresso p, ConfigJogo cfg, BonusEquipamento b)
        {
            if (!cfg.ArvoreDeAscensao) return;
            for (int r = 0; r < Ramos; r++)
            {
                int n = Comprados(p, r);
                for (int k = 0; k < n; k++) { var no = Tabela[r][k]; if (no.Final) b.ArvoreFinal[r] = true; else b.Somar(no.Afixo, no.Valor); }
            }
            if (b.ArvoreFinal[1]) b.MaeCarnica = true;
        }

        /// <summary>Selos do Abismo: um por chefe vencido no Pesadelo, dois por chefe vencido no Inferno.</summary>
        public static void Registrar(EstadoProgresso p, ResultadoExpedicao r)
        {
            if (r.Dificuldade >= 1 && r.ChefesVencidosNaDescida != null) { int g = r.ChefesVencidosNaDescida.Count * r.Dificuldade; p.SelosDoAbismo += g; p.SelosGanhos += g; }
        }
    }
}
