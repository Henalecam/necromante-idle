using System;

namespace Necro.Nucleo
{
    /// <summary>Um nó da Árvore de Ascensão.</summary>
    public sealed class NoDaArvore
    {
        public string Nome, Texto; public AfixoItem Afixo; public float Valor; public bool Final;
    }

    /// <summary>
    /// Árvore de Ascensão: cinco ramos de oito nós, comprados em ordem (cada nó pede o anterior). Os cinco primeiros custam
    /// Cinzas; os três últimos custam Cinzas e Selos do Abismo (ganhos vencendo chefes no Pesadelo e no Inferno).
    /// O último nó de cada ramo muda uma mecânica. Tudo aqui atravessa a Ascensão.
    /// </summary>
    public static class Arvore
    {
        public const int Ramos = 5, Nos = 8, Heranca = 4;
        public static readonly string[] NomesDosRamos = { "Invocador", "Ossos", "Sangue", "Pragas", "Herança" };
        public static readonly string[] LemasDosRamos = { "o exército e o Golem", "feitiços e armadura", "vida, roubo de vida e poções", "almas, críticos e maldições", "a vida seguinte começa adiantada" };

        private static NoDaArvore N(string nome, AfixoItem a, float v, string texto) { return new NoDaArvore { Nome = nome, Afixo = a, Valor = v, Texto = texto }; }
        private static NoDaArvore H(string nome, string texto) { return new NoDaArvore { Nome = nome, Texto = texto, Afixo = (AfixoItem)(-1) }; }
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
            new[] {   // Herança: não dá força, dá ritmo. Os valores estão em ConfigJogo (Heranca*)
                H("Lições Antigas", "+60% de experiência"),
                H("Saque Farto", "+60% de Ossos"),
                H("Mapa Decorado", "a vida começa no andar 3"),
                H("Guarda de Honra", "seus mortos renascem no nível 10"),
                H("Caminho Sabido", "a vida começa no andar 6"),
                H("Dízimo dos Mortos", "mais +50% de Ossos"),
                H("Memória do Grimório", "renasce com o primeiro livro de cada feitiço já lido"),
                F("Trono Herdado", "o necromante renasce no nível 10"),
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

        public static bool Comprar(EstadoProgresso p, int ramo, ConfigJogo cfg = null)
        {
            if (!PodeComprar(p, ramo)) return false;
            int no = p.ArvoreDeAscensao[ramo];
            p.Cinzas -= CustoEmCinzas(no); p.SelosDoAbismo -= CustoEmSelos(no); p.ArvoreDeAscensao[ramo]++;
            if (ramo == Heranca && cfg != null) AplicarHeranca(p, cfg);
            return true;
        }

        public static void Aplicar(EstadoProgresso p, ConfigJogo cfg, BonusEquipamento b)
        {
            if (!cfg.ArvoreDeAscensao) return;
            for (int r = 0; r < Ramos; r++)
            {
                int n = Comprados(p, r);
                for (int k = 0; k < n; k++) { var no = Tabela[r][k]; if (no.Final) b.ArvoreFinal[r] = true; else if ((int)no.Afixo >= 0) b.Somar(no.Afixo, no.Valor); }
            }
            if (b.ArvoreFinal[1]) b.MaeCarnica = true;
        }

        // ---- Herança: o ramo que mexe no ritmo da vida seguinte ----
        private static int H_(EstadoProgresso p, ConfigJogo cfg) { return cfg.ArvoreDeAscensao ? Comprados(p, Heranca) : 0; }
        /// <summary>Ossos a mais (fração somada aos outros multiplicadores).</summary>
        public static float Ossos(EstadoProgresso p, ConfigJogo cfg) { int n = H_(p, cfg); return (n >= 2 ? cfg.HerancaOssos1 : 0f) + (n >= 6 ? cfg.HerancaOssos2 : 0f); }
        /// <summary>Experiência a mais (fração).</summary>
        public static float Xp(EstadoProgresso p, ConfigJogo cfg) { return H_(p, cfg) >= 1 ? cfg.HerancaXp : 0f; }
        /// <summary>Andar em que a vida começa (1 sem o ramo).</summary>
        public static int AndarInicial(EstadoProgresso p, ConfigJogo cfg) { int n = H_(p, cfg); return n >= 5 ? cfg.HerancaAndar2 : n >= 3 ? cfg.HerancaAndar1 : 1; }
        public static int NivelDosMinions(EstadoProgresso p, ConfigJogo cfg) { return H_(p, cfg) >= 4 ? cfg.HerancaNivelDosMinions : 1; }

        /// <summary>
        /// Garante os pisos da Herança: ancoragem, nível dos mortos, livros e nível do necromante nunca ficam abaixo do que o ramo dá.
        /// Chamado ao ascender e ao comprar um nó (as Cinzas costumam ser gastas logo depois de renascer).
        /// </summary>
        public static void AplicarHeranca(EstadoProgresso p, ConfigJogo cfg)
        {
            int n = H_(p, cfg); if (n <= 0) return;
            int anc = AndarInicial(p, cfg) + cfg.AndaresDeRecuo;   // o Avançar parte da ancoragem menos o recuo
            if (AndarInicial(p, cfg) > 1 && p.AndarDeAncoragem < anc) p.AndarDeAncoragem = anc;
            int nm = NivelDosMinions(p, cfg);
            for (int i = 0; i < p.Minions.Count; i++) if (p.Minions[i].Nivel < nm) { p.Minions[i].Nivel = Math.Min(nm, Progressao.NivelMaximo(p.Minions[i], cfg)); p.Minions[i].Xp = 0f; }
            if (n >= 7) { if (p.NiveisDeFeitico == null || p.NiveisDeFeitico.Length < Grimorio.Quantos) { var nv = new int[Grimorio.Quantos]; if (p.NiveisDeFeitico != null) Array.Copy(p.NiveisDeFeitico, nv, p.NiveisDeFeitico.Length); p.NiveisDeFeitico = nv; } for (int f = 0; f < Grimorio.Quantos; f++) if (p.NiveisDeFeitico[f] < 1) p.NiveisDeFeitico[f] = 1; }
            if (n >= Nos && p.NivelNecromante < cfg.HerancaNivelDoNecromante) { p.NivelNecromante = cfg.HerancaNivelDoNecromante; p.XpNecromante = 0f; }
        }

        /// <summary>O que a vida seguinte já leva da Herança, em frases curtas (para a tela da Ascensão).</summary>
        public static System.Collections.Generic.List<string> Resumo(EstadoProgresso p, ConfigJogo cfg)
        {
            var l = new System.Collections.Generic.List<string>(); int n = H_(p, cfg);
            if (Ossos(p, cfg) > 0f) l.Add("+" + (int)Math.Round(Ossos(p, cfg) * 100f) + "% de Ossos");
            if (Xp(p, cfg) > 0f) l.Add("+" + (int)Math.Round(Xp(p, cfg) * 100f) + "% de experiência");
            if (AndarInicial(p, cfg) > 1) l.Add("começa no andar " + AndarInicial(p, cfg));
            if (NivelDosMinions(p, cfg) > 1) l.Add("mortos no nível " + NivelDosMinions(p, cfg));
            if (n >= 7) l.Add("primeiro livro de cada feitiço lido");
            if (n >= Nos) l.Add("necromante no nível " + cfg.HerancaNivelDoNecromante);
            return l;
        }

        /// <summary>Selos do Abismo: um por chefe vencido no Pesadelo, dois por chefe vencido no Inferno.</summary>
        public static void Registrar(EstadoProgresso p, ResultadoExpedicao r)
        {
            if (r.Dificuldade >= 1 && r.ChefesVencidosNaDescida != null) { int g = r.ChefesVencidosNaDescida.Count * r.Dificuldade; p.SelosDoAbismo += g; p.SelosGanhos += g; }
        }
    }
}
