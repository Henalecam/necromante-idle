using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>Os 9 espaços de equipamento do necromante (minions não usam equipamento).</summary>
    public enum EspacoEquipamento { Arma = 0, MaoSecundaria = 1, Elmo = 2, Peitoral = 3, Luvas = 4, Botas = 5, Amuleto = 6, Anel1 = 7, Anel2 = 8 }

    /// <summary>Tipo de item. Arma = Cajado ou Foice; Mão secundária = Grimório ou Relicário.</summary>
    public enum TipoItem { Cajado = 0, Foice = 1, Grimorio = 2, Relicario = 3, Elmo = 4, Peitoral = 5, Luvas = 6, Botas = 7, Amuleto = 8, Anel = 9 }

    public enum RaridadeItem { Comum = 0, Magico = 1, Raro = 2, Lendario = 3, Profano = 4 }

    /// <summary>Afixos de item. Os valores são frações (0.10 = 10%) ou planos, conforme a TabelaItens.</summary>
    public enum AfixoItem
    {
        Vida = 0, Armadura = 1, DanoDeFeiticos = 2, Mana = 3, RegenMana = 4,
        ResFisico = 5, ResFogo = 6, ResGelo = 7, ResRaio = 8, ResVeneno = 9, ResSombra = 10,
        ChanceCritico = 11, DanoCritico = 12, PoderNecromantico = 13, Comando = 14,
        VelocidadeDeCeifa = 15, ReducaoDeRecarga = 16, ChanceDeAlma = 17,
        VidaDosMinions = 18, DanoDosMinions = 19, VelAtaqueDosMinions = 20, RouboDeVidaDosMinions = 21
    }

    /// <summary>Efeito único dos Lendários (muda mecânica, não só números).</summary>
    public enum EfeitoLendario
    {
        Nenhum = 0,
        /// <summary>Foice do Ceifeiro Faminto: cada alma ceifada cura todos os minions em 5% da vida máxima.</summary>
        CeifeiroFaminto = 1,
        /// <summary>Grimório da Mãe Carniça: Explosão de Cadáver deixa um Zumbi temporário onde explodiu.</summary>
        MaeCarnica = 2,
        /// <summary>Anel do Rei Solitário: +100% de dano do necromante se tiver 0 minions vivos.</summary>
        ReiSolitario = 3,
        /// <summary>Coroa da Legião Muda: minions Pequenos custam 0,5 de Comando, mas têm -20% de vida.</summary>
        LegiaoMuda = 4,
        /// <summary>Manto do Último Suspiro: uma vez por andar, dano fatal deixa o necromante com 1 de vida e sacrifica o minion de maior vida.</summary>
        UltimoSuspiro = 5,
        /// <summary>Amuleto da Maré Rubra: overkills espalham 30% do dano excedente aos inimigos a até 2 unidades.</summary>
        MareRubra = 6
    }

    public enum ConjuntoId { Nenhum = 0, VestesDoCoveiro = 1, GarrasDoCarniceiro = 2, PregadorDeCinzas = 3 }

    /// <summary>Como a faixa de um afixo cresce com o nível do item.</summary>
    public enum EscalaAfixo
    {
        /// <summary>Composta, igual à dos inimigos: (1 + CrescimentoItemPorAndar)^(nível - 1).</summary>
        Plena,
        /// <summary>Linear: 1 + CrescimentoItemLinearPorAndar x (nível - 1).</summary>
        Linear,
        /// <summary>Não cresce (ex.: +Comando, que tem regra própria).</summary>
        Fixa
    }

    /// <summary>Um afixo já sorteado num item.</summary>
    [Serializable]
    public class AfixoRolado
    {
        public AfixoItem Afixo;
        public float Valor;

        public AfixoRolado() { }
        public AfixoRolado(AfixoItem afixo, float valor) { Afixo = afixo; Valor = valor; }
    }

    /// <summary>
    /// Um item de equipamento. POCO de save: campos públicos, sem lógica.
    /// Nivel == 0 significa "espaço vazio" (o JsonUtility da Unity não guarda null em vetores de classes).
    /// </summary>
    [Serializable]
    public class Item
    {
        public TipoItem Tipo;
        public RaridadeItem Raridade;
        /// <summary>Nível do item = andar em que caiu. Define as faixas dos afixos.</summary>
        public int Nivel;
        public string Nome = "";
        /// <summary>Atributo fixo do tipo de item (todo item tem; não conta como afixo). Valor 0 = nenhum.</summary>
        public AfixoRolado Implicito = new AfixoRolado();
        public List<AfixoRolado> Afixos = new List<AfixoRolado>();
        public EfeitoLendario Efeito;
        public ConjuntoId Conjunto;
        /// <summary>Travado pelo jogador: nunca é desmanchado (nem pelo "vender tudo", nem para abrir espaço).</summary>
        public bool Bloqueado;
        public int Desgaste;           // durabilidade gasta (0 = novo). Ao chegar na durabilidade máxima o item quebra e para de dar bônus até o reparo
        public int UnicoId;            // > 0: item Único da tabela (Nucleo/Unicos.cs)
        public int Melhoria;           // +0 a +5: melhorado no Ferreiro com ouro; cada nível aumenta o implícito e os afixos
        public bool NaoIdentificado;   // caiu sem identificar (Diablo 1): não equipa nem mostra as propriedades até pagar a identificação

        public bool Tem(AfixoItem a)
        {
            if (Afixos == null) return false;
            for (int i = 0; i < Afixos.Count; i++) if (Afixos[i].Afixo == a) return true;
            return false;
        }

        public override string ToString()
        {
            string s = Nome + " [" + Raridade + " nv " + Nivel + "]";
            if (Implicito != null && Implicito.Valor != 0f) s += " (" + TabelaItens.Descrever(Implicito) + ")";
            if (Afixos != null) for (int i = 0; i < Afixos.Count; i++) s += " " + TabelaItens.Descrever(Afixos[i]) + (i + 1 < Afixos.Count ? "," : "");
            if (Efeito != EfeitoLendario.Nenhum) s += " <" + Efeito + ">";
            if (Conjunto != ConjuntoId.Nenhum) s += " {" + TabelaItens.Conjunto(Conjunto).Nome + "}";
            return s;
        }
    }

    /// <summary>Dados de design de um afixo de item.</summary>
    [Serializable]
    public class DadosAfixoItem
    {
        public AfixoItem Id;
        public string Nome;
        public float Min;           // faixa no nível 1
        public float Max;
        public EscalaAfixo Escala;
        public float Teto;          // valor máximo por item (0 = sem teto)
        public bool Percentual;     // só para exibição
        public float PesoSorteio = 1f;
        public float PesoPontuacao = 1f;
        /// <summary>Adjetivo do nome gerado, nas formas "masculino|feminino|feminino plural".</summary>
        public string Prefixo;
        public string Sufixo;       // já com a preposição: "do Lamento"
    }

    /// <summary>Dados de um Lendário: nome fixo, tipo de item e efeito.</summary>
    [Serializable]
    public class DadosLendario
    {
        public EfeitoLendario Efeito;
        public string Nome;
        public TipoItem Tipo;
        public string Descricao;
        public float Pontuacao;     // valor do efeito para o auto-equipar, em "afixos equivalentes"
    }

    /// <summary>Dados de um conjunto Profano: peças e bônus com 2 e 3 peças equipadas.</summary>
    [Serializable]
    public class DadosConjunto
    {
        public ConjuntoId Id;
        public string Nome;
        public TipoItem[] Pecas;
        public string[] NomesDasPecas;
        public AfixoRolado[] BonusCom2;
        public AfixoRolado[] BonusCom3;
    }

    /// <summary>Tabelas de itens em código (como a TabelaEspecies): afixos, listas por espaço, lendários e conjuntos.</summary>
    public static class TabelaItens
    {
        public const int TotalDeEspacos = 9;
        public const int TotalDeTipos = 10;
        public const int TotalDeAfixos = 22;
        public const int TotalDeRaridades = 5;

        private static readonly string[] NomesDosTipos = { "Cajado", "Foice", "Grimório", "Relicário", "Elmo", "Peitoral", "Luvas", "Botas", "Amuleto", "Anel" };
        // 0 = masculino, 1 = feminino, 2 = feminino plural (concordância do adjetivo no nome gerado)
        private static readonly int[] GeneroDosTipos = { 0, 1, 0, 0, 0, 0, 2, 2, 0, 0 };
        private static readonly AfixoItem[] ImplicitoDoTipo =
        {
            AfixoItem.DanoDeFeiticos, AfixoItem.VelocidadeDeCeifa, AfixoItem.Mana, AfixoItem.PoderNecromantico, AfixoItem.Armadura,
            AfixoItem.Armadura, AfixoItem.Armadura, AfixoItem.Armadura, AfixoItem.RegenMana, AfixoItem.Vida
        };

        private static readonly DadosAfixoItem[] _afixos = CriarAfixos();
        private static readonly AfixoItem[][] _afixosPorTipo = CriarListasPorTipo();

        public static readonly DadosLendario[] Lendarios =
        {
            new DadosLendario { Efeito = EfeitoLendario.CeifeiroFaminto, Nome = "Foice do Ceifeiro Faminto", Tipo = TipoItem.Foice, Pontuacao = 2f,
                Descricao = "Cada alma ceifada cura todos os minions em 5% da vida máxima." },
            new DadosLendario { Efeito = EfeitoLendario.MaeCarnica, Nome = "Grimório da Mãe Carniça", Tipo = TipoItem.Grimorio, Pontuacao = 2.5f,
                Descricao = "Explosão de Cadáver deixa um Zumbi temporário onde explodiu." },
            new DadosLendario { Efeito = EfeitoLendario.ReiSolitario, Nome = "Anel do Rei Solitário", Tipo = TipoItem.Anel, Pontuacao = 0f,
                Descricao = "+100% de dano do necromante se ele tiver 0 minions vivos." },
            new DadosLendario { Efeito = EfeitoLendario.LegiaoMuda, Nome = "Coroa da Legião Muda", Tipo = TipoItem.Elmo, Pontuacao = 1f,
                Descricao = "Minions Pequenos custam 0,5 de Comando, mas têm -20% de vida." },
            new DadosLendario { Efeito = EfeitoLendario.UltimoSuspiro, Nome = "Manto do Último Suspiro", Tipo = TipoItem.Peitoral, Pontuacao = 3f,
                Descricao = "Uma vez por andar, ao sofrer dano fatal, o necromante fica com 1 de vida e sacrifica o minion de maior vida no lugar." },
            new DadosLendario { Efeito = EfeitoLendario.MareRubra, Nome = "Amuleto da Maré Rubra", Tipo = TipoItem.Amuleto, Pontuacao = 2f,
                Descricao = "Overkills espalham 30% do dano excedente aos inimigos a até 2 unidades." },
        };

        public static readonly DadosConjunto[] Conjuntos =
        {
            new DadosConjunto
            {
                Id = ConjuntoId.VestesDoCoveiro, Nome = "Vestes do Coveiro",
                Pecas = new[] { TipoItem.Elmo, TipoItem.Peitoral, TipoItem.Botas },
                NomesDasPecas = new[] { "Capuz do Coveiro", "Mortalha do Coveiro", "Botas do Coveiro" },
                BonusCom2 = new[] { new AfixoRolado(AfixoItem.PoderNecromantico, 0.15f) },
                BonusCom3 = new[] { new AfixoRolado(AfixoItem.Comando, 1f), new AfixoRolado(AfixoItem.VidaDosMinions, 0.20f) }
            },
            new DadosConjunto
            {
                Id = ConjuntoId.GarrasDoCarniceiro, Nome = "Garras do Carniceiro",
                Pecas = new[] { TipoItem.Luvas, TipoItem.Amuleto, TipoItem.Anel },
                NomesDasPecas = new[] { "Garras do Carniceiro", "Colar de Dentes do Carniceiro", "Sinete do Carniceiro" },
                BonusCom2 = new[] { new AfixoRolado(AfixoItem.DanoDosMinions, 0.12f) },
                BonusCom3 = new[] { new AfixoRolado(AfixoItem.VelAtaqueDosMinions, 0.08f), new AfixoRolado(AfixoItem.RouboDeVidaDosMinions, 0.03f) }
            },
            // conjunto original: o pregador que lia sermões aos mortos. 3 peças: cada feitiço lançado devolve vida ao necromante (Cfg.PregadorCura)
            new DadosConjunto
            {
                Id = ConjuntoId.PregadorDeCinzas, Nome = "Sermão do Pregador de Cinzas",
                Pecas = new[] { TipoItem.Cajado, TipoItem.Grimorio, TipoItem.Anel },
                NomesDasPecas = new[] { "Báculo do Pregador de Cinzas", "Missal do Pregador de Cinzas", "Aliança do Pregador de Cinzas" },
                BonusCom2 = new[] { new AfixoRolado(AfixoItem.DanoDeFeiticos, 0.15f) },
                BonusCom3 = new[] { new AfixoRolado(AfixoItem.RegenMana, 1.0f) }
            }
        };

        public static DadosAfixoItem Afixo(AfixoItem a) { return _afixos[(int)a]; }

        public static DadosAfixoItem[] TodosOsAfixos { get { return _afixos; } }

        /// <summary>Afixos que podem ser sorteados num tipo de item (o implícito do tipo fica de fora).</summary>
        public static AfixoItem[] AfixosPossiveis(TipoItem tipo) { return _afixosPorTipo[(int)tipo]; }

        public static AfixoItem Implicito(TipoItem tipo) { return ImplicitoDoTipo[(int)tipo]; }

        public static string NomeDoTipo(TipoItem tipo) { return NomesDosTipos[(int)tipo]; }

        /// <summary>Espaço em que um tipo de item é equipado (Anel devolve Anel1; também cabe em Anel2).</summary>
        public static EspacoEquipamento EspacoDoTipo(TipoItem tipo)
        {
            switch (tipo)
            {
                case TipoItem.Cajado: case TipoItem.Foice: return EspacoEquipamento.Arma;
                case TipoItem.Grimorio: case TipoItem.Relicario: return EspacoEquipamento.MaoSecundaria;
                case TipoItem.Elmo: return EspacoEquipamento.Elmo;
                case TipoItem.Peitoral: return EspacoEquipamento.Peitoral;
                case TipoItem.Luvas: return EspacoEquipamento.Luvas;
                case TipoItem.Botas: return EspacoEquipamento.Botas;
                case TipoItem.Amuleto: return EspacoEquipamento.Amuleto;
                default: return EspacoEquipamento.Anel1;
            }
        }

        public static DadosLendario Lendario(EfeitoLendario efeito)
        {
            for (int i = 0; i < Lendarios.Length; i++) if (Lendarios[i].Efeito == efeito) return Lendarios[i];
            return null;
        }

        public static DadosConjunto Conjunto(ConjuntoId id)
        {
            for (int i = 0; i < Conjuntos.Length; i++) if (Conjuntos[i].Id == id) return Conjuntos[i];
            return null;
        }

        /// <summary>Multiplicador das faixas de um afixo no nível dado (nível 1 = 1).</summary>
        public static float FatorDeNivel(EscalaAfixo escala, int nivel, ConfigJogo cfg)
        {
            if (nivel < 1) nivel = 1;
            switch (escala)
            {
                case EscalaAfixo.Plena: return MathF.Pow(1f + cfg.CrescimentoItemPorAndar, nivel - 1);
                case EscalaAfixo.Linear: return 1f + cfg.CrescimentoItemLinearPorAndar * (nivel - 1);
                default: return 1f;
            }
        }

        /// <summary>Faixa [min, max] de um afixo num item do nível dado, já com o teto por item.</summary>
        public static void Faixa(AfixoItem afixo, int nivel, ConfigJogo cfg, out float min, out float max)
        {
            var d = _afixos[(int)afixo];
            if (afixo == AfixoItem.Comando)
            {
                // +Comando é inteiro: +1, e mais 1 a cada NiveisPorComandoExtra níveis de item.
                min = max = 1 + (cfg.NiveisPorComandoExtra > 0 ? (nivel < 1 ? 0 : nivel) / cfg.NiveisPorComandoExtra : 0);
                return;
            }
            float f = FatorDeNivel(d.Escala, nivel, cfg);
            min = d.Min * f;
            max = d.Max * f;
            if (d.Teto > 0f)
            {
                if (max > d.Teto) max = d.Teto;
                if (min > d.Teto) min = d.Teto;
            }
        }

        /// <summary>Texto curto de um afixo sorteado, ex.: "+12% Dano de feitiços".</summary>
        public static string Descrever(AfixoRolado a)
        {
            var d = _afixos[(int)a.Afixo];
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (d.Percentual) return "+" + (a.Valor * 100f).ToString("0.#", ci) + "% " + d.Nome;
            return "+" + a.Valor.ToString(a.Valor < 10f && a.Afixo != AfixoItem.Comando ? "0.##" : "0", ci) + " " + d.Nome;
        }

        /// <summary>Adjetivo do afixo concordando com o tipo de item ("Sepulcral", "Vigorosa", "Blindadas").</summary>
        public static string PrefixoPara(AfixoItem afixo, TipoItem tipo)
        {
            string formas = _afixos[(int)afixo].Prefixo;
            int genero = GeneroDosTipos[(int)tipo];
            int ini = 0;
            for (int g = 0; g < genero; g++)
            {
                int barra = formas.IndexOf('|', ini);
                if (barra < 0) break;
                ini = barra + 1;
            }
            int fim = formas.IndexOf('|', ini);
            return fim < 0 ? formas.Substring(ini) : formas.Substring(ini, fim - ini);
        }

        private static DadosAfixoItem A(AfixoItem id, string nome, float min, float max, EscalaAfixo escala, float teto, bool percentual, string prefixo, string sufixo,
            float pesoSorteio = 1f, float pesoPontuacao = 1f)
        {
            return new DadosAfixoItem { Id = id, Nome = nome, Min = min, Max = max, Escala = escala, Teto = teto, Percentual = percentual, Prefixo = prefixo, Sufixo = sufixo,
                                        PesoSorteio = pesoSorteio, PesoPontuacao = pesoPontuacao };
        }

        private static DadosAfixoItem[] CriarAfixos()
        {
            var t = new DadosAfixoItem[TotalDeAfixos];
            // Planos e multiplicadores "principais" crescem como os inimigos (9% composto); taxas e chances crescem devagar e têm teto.
            t[(int)AfixoItem.Vida] = A(AfixoItem.Vida, "Vida", 12f, 24f, EscalaAfixo.Plena, 0f, false, "Vigoroso|Vigorosa|Vigorosas", "da Carne");
            t[(int)AfixoItem.Armadura] = A(AfixoItem.Armadura, "Armadura", 6f, 12f, EscalaAfixo.Linear, 0f, false, "Blindado|Blindada|Blindadas", "da Muralha", 1f, 0.75f);
            t[(int)AfixoItem.DanoDeFeiticos] = A(AfixoItem.DanoDeFeiticos, "Dano de feitiços", 0.08f, 0.16f, EscalaAfixo.Plena, 0f, true, "Sepulcral|Sepulcral|Sepulcrais", "da Ruína");
            t[(int)AfixoItem.Mana] = A(AfixoItem.Mana, "Mana", 8f, 16f, EscalaAfixo.Linear, 0f, false, "Arcano|Arcana|Arcanas", "do Abismo", 1f, 0.5f);
            t[(int)AfixoItem.RegenMana] = A(AfixoItem.RegenMana, "Regeneração de mana/s", 0.25f, 0.50f, EscalaAfixo.Linear, 0f, false, "Sereno|Serena|Serenas", "da Maré", 1f, 0.75f);
            t[(int)AfixoItem.ResFisico] = A(AfixoItem.ResFisico, "Resistência a Físico", 0.05f, 0.10f, EscalaAfixo.Linear, 0.30f, true, "Pétreo|Pétrea|Pétreas", "da Rocha", 0.35f, 0.6f);
            t[(int)AfixoItem.ResFogo] = A(AfixoItem.ResFogo, "Resistência a Fogo", 0.05f, 0.10f, EscalaAfixo.Linear, 0.30f, true, "Ignífugo|Ignífuga|Ignífugas", "das Cinzas", 0.35f, 0.4f);
            t[(int)AfixoItem.ResGelo] = A(AfixoItem.ResGelo, "Resistência a Gelo", 0.05f, 0.10f, EscalaAfixo.Linear, 0.30f, true, "Gélido|Gélida|Gélidas", "do Inverno", 0.35f, 0.4f);
            t[(int)AfixoItem.ResRaio] = A(AfixoItem.ResRaio, "Resistência a Raio", 0.05f, 0.10f, EscalaAfixo.Linear, 0.30f, true, "Isolado|Isolada|Isoladas", "da Tormenta", 0.35f, 0.4f);
            t[(int)AfixoItem.ResVeneno] = A(AfixoItem.ResVeneno, "Resistência a Veneno", 0.05f, 0.10f, EscalaAfixo.Linear, 0.30f, true, "Purificado|Purificada|Purificadas", "do Antídoto", 0.35f, 0.4f);
            t[(int)AfixoItem.ResSombra] = A(AfixoItem.ResSombra, "Resistência a Sombra", 0.05f, 0.10f, EscalaAfixo.Linear, 0.30f, true, "Velado|Velada|Veladas", "do Crepúsculo", 0.35f, 0.4f);
            t[(int)AfixoItem.ChanceCritico] = A(AfixoItem.ChanceCritico, "Chance de crítico", 0.02f, 0.04f, EscalaAfixo.Linear, 0.15f, true, "Certeiro|Certeira|Certeiras", "do Presságio", 1f, 0.75f);
            t[(int)AfixoItem.DanoCritico] = A(AfixoItem.DanoCritico, "Dano crítico", 0.10f, 0.20f, EscalaAfixo.Linear, 2.0f, true, "Cruel|Cruel|Cruéis", "do Carrasco", 1f, 0.5f);
            t[(int)AfixoItem.PoderNecromantico] = A(AfixoItem.PoderNecromantico, "Poder Necromântico", 0.03f, 0.06f, EscalaAfixo.Plena, 0f, true, "Profanado|Profanada|Profanadas", "dos Mortos", 1f, 2f);
            t[(int)AfixoItem.Comando] = A(AfixoItem.Comando, "Comando", 1f, 1f, EscalaAfixo.Fixa, 0f, false, "Soberano|Soberana|Soberanas", "da Legião", 0.25f, 4f);
            t[(int)AfixoItem.VelocidadeDeCeifa] = A(AfixoItem.VelocidadeDeCeifa, "Velocidade de ceifa", 0.08f, 0.15f, EscalaAfixo.Linear, 0.60f, true, "Ceifador|Ceifadora|Ceifadoras", "da Colheita", 1f, 0.4f);
            t[(int)AfixoItem.ReducaoDeRecarga] = A(AfixoItem.ReducaoDeRecarga, "Redução de recarga", 0.03f, 0.06f, EscalaAfixo.Linear, 0.15f, true, "Incansável|Incansável|Incansáveis", "da Vigília");
            t[(int)AfixoItem.ChanceDeAlma] = A(AfixoItem.ChanceDeAlma, "Chance de alma", 0.08f, 0.15f, EscalaAfixo.Linear, 1.0f, true, "Assombrado|Assombrada|Assombradas", "do Lamento", 1f, 0.5f);
            t[(int)AfixoItem.VidaDosMinions] = A(AfixoItem.VidaDosMinions, "Vida dos minions", 0.05f, 0.10f, EscalaAfixo.Linear, 0f, true, "Ossudo|Ossuda|Ossudas", "da Cripta", 1f, 1.5f);
            t[(int)AfixoItem.DanoDosMinions] = A(AfixoItem.DanoDosMinions, "Dano dos minions", 0.05f, 0.10f, EscalaAfixo.Linear, 0f, true, "Sanguinário|Sanguinária|Sanguinárias", "da Matança", 1f, 1.5f);
            t[(int)AfixoItem.VelAtaqueDosMinions] = A(AfixoItem.VelAtaqueDosMinions, "Velocidade de ataque dos minions", 0.03f, 0.06f, EscalaAfixo.Linear, 0.25f, true, "Frenético|Frenética|Frenéticas", "do Frenesi", 1f, 1.2f);
            t[(int)AfixoItem.RouboDeVidaDosMinions] = A(AfixoItem.RouboDeVidaDosMinions, "Roubo de vida dos minions", 0.01f, 0.02f, EscalaAfixo.Linear, 0.06f, true, "Faminto|Faminta|Famintas", "da Fome");
            return t;
        }

        private static AfixoItem[][] CriarListasPorTipo()
        {
            var t = new AfixoItem[TotalDeTipos][];
            // +Comando só existe em arma e amuleto.
            t[(int)TipoItem.Cajado] = new[]
            {
                AfixoItem.Mana, AfixoItem.RegenMana, AfixoItem.ChanceCritico, AfixoItem.DanoCritico, AfixoItem.ReducaoDeRecarga,
                AfixoItem.PoderNecromantico, AfixoItem.Comando, AfixoItem.DanoDosMinions
            };
            t[(int)TipoItem.Foice] = new[]
            {
                AfixoItem.DanoDeFeiticos, AfixoItem.ChanceCritico, AfixoItem.DanoCritico, AfixoItem.ChanceDeAlma, AfixoItem.PoderNecromantico,
                AfixoItem.Comando, AfixoItem.DanoDosMinions, AfixoItem.VelAtaqueDosMinions, AfixoItem.RouboDeVidaDosMinions
            };
            t[(int)TipoItem.Grimorio] = new[]
            {
                AfixoItem.RegenMana, AfixoItem.DanoDeFeiticos, AfixoItem.ReducaoDeRecarga, AfixoItem.DanoCritico, AfixoItem.PoderNecromantico,
                AfixoItem.VidaDosMinions, AfixoItem.DanoDosMinions
            };
            t[(int)TipoItem.Relicario] = new[]
            {
                AfixoItem.Vida, AfixoItem.ChanceDeAlma, AfixoItem.VidaDosMinions, AfixoItem.VelAtaqueDosMinions, AfixoItem.RouboDeVidaDosMinions,
                AfixoItem.ResSombra, AfixoItem.RegenMana
            };
            t[(int)TipoItem.Elmo] = new[]
            {
                AfixoItem.Vida, AfixoItem.Mana, AfixoItem.ReducaoDeRecarga, AfixoItem.VidaDosMinions,
                AfixoItem.ResFisico, AfixoItem.ResFogo, AfixoItem.ResGelo, AfixoItem.ResRaio, AfixoItem.ResVeneno, AfixoItem.ResSombra
            };
            t[(int)TipoItem.Peitoral] = new[]
            {
                AfixoItem.Vida, AfixoItem.Mana, AfixoItem.RegenMana, AfixoItem.VidaDosMinions,
                AfixoItem.ResFisico, AfixoItem.ResFogo, AfixoItem.ResGelo, AfixoItem.ResRaio, AfixoItem.ResVeneno, AfixoItem.ResSombra
            };
            t[(int)TipoItem.Luvas] = new[]
            {
                AfixoItem.Vida, AfixoItem.ChanceCritico, AfixoItem.DanoCritico, AfixoItem.VelocidadeDeCeifa, AfixoItem.DanoDeFeiticos,
                AfixoItem.VelAtaqueDosMinions, AfixoItem.DanoDosMinions
            };
            t[(int)TipoItem.Botas] = new[]
            {
                AfixoItem.Vida, AfixoItem.VelocidadeDeCeifa, AfixoItem.RegenMana, AfixoItem.VelAtaqueDosMinions,
                AfixoItem.ResFisico, AfixoItem.ResFogo, AfixoItem.ResGelo, AfixoItem.ResRaio, AfixoItem.ResVeneno, AfixoItem.ResSombra
            };
            t[(int)TipoItem.Amuleto] = new[]
            {
                AfixoItem.Vida, AfixoItem.Mana, AfixoItem.PoderNecromantico, AfixoItem.Comando, AfixoItem.ChanceDeAlma, AfixoItem.ChanceCritico,
                AfixoItem.DanoCritico, AfixoItem.DanoDeFeiticos, AfixoItem.RouboDeVidaDosMinions,
                AfixoItem.ResFisico, AfixoItem.ResFogo, AfixoItem.ResGelo, AfixoItem.ResRaio, AfixoItem.ResVeneno, AfixoItem.ResSombra
            };
            t[(int)TipoItem.Anel] = new[]
            {
                AfixoItem.Mana, AfixoItem.RegenMana, AfixoItem.ChanceCritico, AfixoItem.DanoCritico, AfixoItem.DanoDeFeiticos, AfixoItem.ReducaoDeRecarga,
                AfixoItem.VidaDosMinions, AfixoItem.DanoDosMinions, AfixoItem.ChanceDeAlma,
                AfixoItem.ResFisico, AfixoItem.ResFogo, AfixoItem.ResGelo, AfixoItem.ResRaio, AfixoItem.ResVeneno, AfixoItem.ResSombra
            };
            return t;
        }
    }

    /// <summary>Gera itens com o Rng determinístico (mesma semente = mesmos itens).</summary>
    public static class GeradorDeItens
    {
        /// <summary>Chance de um inimigo comum ou elite soltar um item (o chefe garante cfg.ItensDoChefe).</summary>
        public static float ChanceDeDrop(ConfigJogo cfg, bool elite) { return elite ? cfg.DropItemElite : cfg.DropItemComum; }

        /// <summary>Sorteia a raridade (70 / 22 / 7 / 0,9 / 0,1 %). Com raridade mínima, sorteia de novo só entre as permitidas.</summary>
        public static RaridadeItem SortearRaridade(Rng rng, ConfigJogo cfg, RaridadeItem minima = RaridadeItem.Comum)
        {
            float total = 0f;
            for (int i = (int)minima; i < cfg.ChanceRaridadeItem.Length; i++) total += cfg.ChanceRaridadeItem[i];
            float r = rng.Proximo01() * total;
            float acumulado = 0f;
            for (int i = (int)minima; i < cfg.ChanceRaridadeItem.Length; i++)
            {
                acumulado += cfg.ChanceRaridadeItem[i];
                if (r < acumulado) return (RaridadeItem)i;
            }
            return minima;
        }

        /// <summary>Tipo de item: espaço sorteado por igual entre os 9; Arma e Mão secundária têm duas variantes.</summary>
        public static TipoItem SortearTipo(Rng rng)
        {
            int espaco = rng.Intervalo(0, TabelaItens.TotalDeEspacos);
            switch ((EspacoEquipamento)espaco)
            {
                case EspacoEquipamento.Arma: return rng.Intervalo(0, 2) == 0 ? TipoItem.Cajado : TipoItem.Foice;
                case EspacoEquipamento.MaoSecundaria: return rng.Intervalo(0, 2) == 0 ? TipoItem.Grimorio : TipoItem.Relicario;
                case EspacoEquipamento.Elmo: return TipoItem.Elmo;
                case EspacoEquipamento.Peitoral: return TipoItem.Peitoral;
                case EspacoEquipamento.Luvas: return TipoItem.Luvas;
                case EspacoEquipamento.Botas: return TipoItem.Botas;
                case EspacoEquipamento.Amuleto: return TipoItem.Amuleto;
                default: return TipoItem.Anel;
            }
        }

        /// <summary>Item de raridade sorteada (respeitando a mínima), do nível dado (= andar em que caiu).</summary>
        public static Item Gerar(Rng rng, ConfigJogo cfg, int nivel, RaridadeItem raridadeMinima = RaridadeItem.Comum)
        {
            return GerarDeRaridade(rng, cfg, nivel, SortearRaridade(rng, cfg, raridadeMinima));
        }

        /// <summary>Item de raridade fixa. Lendário sorteia um dos efeitos; Profano sorteia uma peça de conjunto (o tipo vem daí).</summary>
        public static Item GerarDeRaridade(Rng rng, ConfigJogo cfg, int nivel, RaridadeItem raridade)
        {
            if (raridade == RaridadeItem.Lendario)
                return GerarLendario(rng, cfg, nivel, TabelaItens.Lendarios[rng.Intervalo(0, TabelaItens.Lendarios.Length)].Efeito);
            if (raridade == RaridadeItem.Profano)
            {
                var c = TabelaItens.Conjuntos[rng.Intervalo(0, TabelaItens.Conjuntos.Length)];
                return GerarPecaDeConjunto(rng, cfg, nivel, c.Id, rng.Intervalo(0, c.Pecas.Length));
            }
            return GerarDoTipo(rng, cfg, nivel, raridade, SortearTipo(rng));
        }

        /// <summary>Item Comum, Mágico ou Raro de um tipo específico (raridades maiores são rebaixadas para Raro).</summary>
        public static Item GerarDoTipo(Rng rng, ConfigJogo cfg, int nivel, RaridadeItem raridade, TipoItem tipo)
        {
            if (raridade > RaridadeItem.Raro) raridade = RaridadeItem.Raro;
            var item = Base(rng, cfg, nivel, raridade, tipo);
            item.Nome = MontarNome(item);
            return item;
        }

        /// <summary>Lendário específico: 4 afixos + o efeito único. O tipo do item é o do lendário.</summary>
        public static Item GerarLendario(Rng rng, ConfigJogo cfg, int nivel, EfeitoLendario efeito)
        {
            var d = TabelaItens.Lendario(efeito);
            var item = Base(rng, cfg, nivel, RaridadeItem.Lendario, d.Tipo);
            item.Efeito = efeito;
            item.Nome = d.Nome;
            return item;
        }

        /// <summary>Peça Profana de conjunto: 4 afixos + contagem para o bônus de conjunto.</summary>
        public static Item GerarPecaDeConjunto(Rng rng, ConfigJogo cfg, int nivel, ConjuntoId conjunto, int indiceDaPeca)
        {
            var c = TabelaItens.Conjunto(conjunto);
            var item = Base(rng, cfg, nivel, RaridadeItem.Profano, c.Pecas[indiceDaPeca]);
            item.Conjunto = conjunto;
            item.Nome = c.NomesDasPecas[indiceDaPeca];
            return item;
        }

        private static Item Base(Rng rng, ConfigJogo cfg, int nivel, RaridadeItem raridade, TipoItem tipo)
        {
            if (nivel < 1) nivel = 1;
            var item = new Item { Tipo = tipo, Raridade = raridade, Nivel = nivel };
            AfixoItem implicito = TabelaItens.Implicito(tipo);
            item.Implicito = new AfixoRolado(implicito, Rolar(rng, cfg, implicito, nivel));

            var possiveis = TabelaItens.AfixosPossiveis(tipo);
            int r = (int)raridade;
            int n = rng.Intervalo(cfg.AfixosItemMin[r], cfg.AfixosItemMax[r] + 1);
            if (n > possiveis.Length) n = possiveis.Length;
            // Sorteio por peso, sem repetir afixo no mesmo item.
            while (item.Afixos.Count < n)
            {
                float total = 0f;
                for (int i = 0; i < possiveis.Length; i++)
                    if (!item.Tem(possiveis[i])) total += TabelaItens.Afixo(possiveis[i]).PesoSorteio;
                float x = rng.Proximo01() * total;
                AfixoItem escolhido = possiveis[0];
                bool achou = false;
                for (int i = 0; i < possiveis.Length; i++)
                {
                    if (item.Tem(possiveis[i])) continue;
                    escolhido = possiveis[i];
                    achou = true;
                    x -= TabelaItens.Afixo(possiveis[i]).PesoSorteio;
                    if (x < 0f) break;
                }
                if (!achou) break;
                item.Afixos.Add(new AfixoRolado(escolhido, Rolar(rng, cfg, escolhido, nivel)));
            }
            return item;
        }

        /// <summary>Imbuir: acrescenta ao item um afixo que ele ainda não tem (sorteio por peso). Devolve false se não houver nenhum possível.</summary>
        public static bool AcrescentarAfixo(Rng rng, ConfigJogo cfg, Item item)
        {
            var possiveis = TabelaItens.AfixosPossiveis(item.Tipo);
            float total = 0f;
            for (int i = 0; i < possiveis.Length; i++) if (!item.Tem(possiveis[i])) total += TabelaItens.Afixo(possiveis[i]).PesoSorteio;
            if (total <= 0f) return false;
            float x = rng.Proximo01() * total; AfixoItem escolhido = possiveis[0];
            for (int i = 0; i < possiveis.Length; i++)
            {
                if (item.Tem(possiveis[i])) continue;
                escolhido = possiveis[i]; x -= TabelaItens.Afixo(possiveis[i]).PesoSorteio;
                if (x < 0f) break;
            }
            item.Afixos.Add(new AfixoRolado(escolhido, Rolar(rng, cfg, escolhido, item.Nivel)));
            return true;
        }

        private static float Rolar(Rng rng, ConfigJogo cfg, AfixoItem afixo, int nivel)
        {
            float min, max;
            TabelaItens.Faixa(afixo, nivel, cfg, out min, out max);
            return min + (max - min) * rng.Proximo01();
        }

        /// <summary>"Cajado" (Comum), "Cajado Sepulcral" (1 afixo) ou "Cajado Sepulcral do Lamento" (2+ afixos).</summary>
        public static string MontarNome(Item item)
        {
            string nome = TabelaItens.NomeDoTipo(item.Tipo);
            if (item.Afixos.Count >= 1) nome += " " + TabelaItens.PrefixoPara(item.Afixos[0].Afixo, item.Tipo);
            if (item.Afixos.Count >= 2) nome += " " + TabelaItens.Afixo(item.Afixos[1].Afixo).Sufixo;
            return nome;
        }
    }
}
