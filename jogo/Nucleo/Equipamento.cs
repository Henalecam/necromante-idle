using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>
    /// Soma de tudo o que o equipamento dá. Calculado por Equipamento.CalcularBonus (função pura) e lido pela Simulacao
    /// ao criar o necromante e os minions. Sem itens equipados, todos os campos valem zero/false e a simulação não muda.
    /// </summary>
    [Serializable]
    public class BonusEquipamento
    {
        // ---- Necromante ----
        public float Vida;                  // plano
        public float Armadura;              // plano
        public float DanoDeFeiticos;        // "aumento" (aditivo) no dano do necromante
        public float Mana;                  // plano
        public float RegenMana;             // plano, por segundo
        public float[] Resistencias = new float[6]; // por TipoDano, somadas às da espécie
        public float ChanceCritico;
        public float DanoCritico;           // somado ao multiplicador de crítico
        public float VelocidadeDeCeifa;     // tempo de ceifa = base / (1 + valor)
        public float ReducaoDeRecarga;      // já limitada a cfg.TetoReducaoDeRecarga
        public float ChanceDeAlma;          // chance de orbe x (1 + valor)
        public int Comando;
        public float VidaDoNecromantePercentual;   // talentos: vida base x (1 + valor)
        public float ToqueMelhoraAlma;             // talentos: somado à chance de a ceifa por toque melhorar a alma

        // ---- Minions ----
        public float PoderNecromantico;     // somado ao Poder Necromântico do progresso
        public float VidaDosMinions;        // vida máxima x (1 + valor)
        public float DanoDosMinions;        // "aumento" (aditivo) no dano
        public float VelAtaqueDosMinions;   // velocidade de ataque x (1 + valor)
        public float RouboDeVidaDosMinions;

        // ---- Lendários e conjuntos ----
        public bool CeifeiroFaminto;
        public bool MaeCarnica;
        public readonly bool[] ArvoreFinal = new bool[5];   // nós finais da Árvore de Ascensão (Invocador, Ossos, Sangue, Pragas)
        public readonly bool[] Unico = new bool[20];   // efeitos próprios dos itens Únicos vestidos (índice = UnicoId)
        public bool ReiSolitario;
        public bool LegiaoMuda;
        public bool UltimoSuspiro;
        public bool MareRubra;
        /// <summary>Peças equipadas de cada conjunto (índice = (int)ConjuntoId).</summary>
        public int[] PecasDeConjunto = new int[4];
        public int ItensQuebrados;
        public int ItensEquipados;

        public bool Tem(EfeitoLendario efeito)
        {
            switch (efeito)
            {
                case EfeitoLendario.CeifeiroFaminto: return CeifeiroFaminto;
                case EfeitoLendario.MaeCarnica: return MaeCarnica;
                case EfeitoLendario.ReiSolitario: return ReiSolitario;
                case EfeitoLendario.LegiaoMuda: return LegiaoMuda;
                case EfeitoLendario.UltimoSuspiro: return UltimoSuspiro;
                case EfeitoLendario.MareRubra: return MareRubra;
            }
            return false;
        }

        /// <summary>Soma um afixo (de item ou de bônus de conjunto) ao total.</summary>
        public void Somar(AfixoItem afixo, float valor)
        {
            switch (afixo)
            {
                case AfixoItem.Vida: Vida += valor; break;
                case AfixoItem.Armadura: Armadura += valor; break;
                case AfixoItem.DanoDeFeiticos: DanoDeFeiticos += valor; break;
                case AfixoItem.Mana: Mana += valor; break;
                case AfixoItem.RegenMana: RegenMana += valor; break;
                case AfixoItem.ResFisico: Resistencias[(int)TipoDano.Fisico] += valor; break;
                case AfixoItem.ResFogo: Resistencias[(int)TipoDano.Fogo] += valor; break;
                case AfixoItem.ResGelo: Resistencias[(int)TipoDano.Gelo] += valor; break;
                case AfixoItem.ResRaio: Resistencias[(int)TipoDano.Raio] += valor; break;
                case AfixoItem.ResVeneno: Resistencias[(int)TipoDano.Veneno] += valor; break;
                case AfixoItem.ResSombra: Resistencias[(int)TipoDano.Sombra] += valor; break;
                case AfixoItem.ChanceCritico: ChanceCritico += valor; break;
                case AfixoItem.DanoCritico: DanoCritico += valor; break;
                case AfixoItem.PoderNecromantico: PoderNecromantico += valor; break;
                case AfixoItem.Comando: Comando += (int)(valor + 0.5f); break;
                case AfixoItem.VelocidadeDeCeifa: VelocidadeDeCeifa += valor; break;
                case AfixoItem.ReducaoDeRecarga: ReducaoDeRecarga += valor; break;
                case AfixoItem.ChanceDeAlma: ChanceDeAlma += valor; break;
                case AfixoItem.VidaDosMinions: VidaDosMinions += valor; break;
                case AfixoItem.DanoDosMinions: DanoDosMinions += valor; break;
                case AfixoItem.VelAtaqueDosMinions: VelAtaqueDosMinions += valor; break;
                case AfixoItem.RouboDeVidaDosMinions: RouboDeVidaDosMinions += valor; break;
            }
        }
    }

    public enum DestinoDoItem { Guardado, Desmanchado, GuardadoDesmanchandoOutro }

    /// <summary>Regras de inventário e equipamento que mexem no EstadoProgresso (fora da simulação de combate).</summary>
    public static class Equipamento
    {
        /// <summary>Um espaço conta como vazio se for null ou tiver Nivel 0 (como o JsonUtility recarrega um null).</summary>
        public static bool Vazio(Item item) { return item == null || item.Nivel <= 0; }

        /// <summary>Item equipado no espaço, ou null se estiver vazio.</summary>
        public static Item Obter(EstadoProgresso p, EspacoEquipamento espaco)
        {
            int i = (int)espaco;
            if (p.Equipados == null || i < 0 || i >= p.Equipados.Length) return null;
            return Vazio(p.Equipados[i]) ? null : p.Equipados[i];
        }

        public static bool PodeEquipar(Item item, EspacoEquipamento espaco)
        {
            if (Vazio(item) || item.NaoIdentificado) return false;
            var e = TabelaItens.EspacoDoTipo(item.Tipo);
            if (e == EspacoEquipamento.Anel1) return espaco == EspacoEquipamento.Anel1 || espaco == EspacoEquipamento.Anel2;
            return e == espaco;
        }

        /// <summary>Espaço em que Equipar(p, item) põe o item. Anel: o primeiro espaço de anel vazio; se os dois estiverem ocupados, Anel1.</summary>
        public static EspacoEquipamento EspacoPadrao(EstadoProgresso p, Item item)
        {
            var e = TabelaItens.EspacoDoTipo(item.Tipo);
            if (e == EspacoEquipamento.Anel1 && Obter(p, EspacoEquipamento.Anel1) != null && Obter(p, EspacoEquipamento.Anel2) == null)
                return EspacoEquipamento.Anel2;
            return e;
        }

        /// <summary>
        /// Equipa o item no espaço padrão do tipo dele e devolve o item que saiu (null se o espaço estava vazio).
        /// O item sai do inventário (se estava lá) e o que saiu do espaço vai para o inventário: troca de 1 por 1.
        /// </summary>
        public static Item Equipar(EstadoProgresso p, Item item)
        {
            if (Vazio(item)) throw new ArgumentException("item vazio");
            return Equipar(p, item, EspacoPadrao(p, item));
        }

        /// <summary>Equipa num espaço específico (para escolher entre Anel1 e Anel2). Lança ArgumentException se o tipo não couber no espaço.</summary>
        public static Item Equipar(EstadoProgresso p, Item item, EspacoEquipamento espaco)
        {
            if (!PodeEquipar(item, espaco)) throw new ArgumentException("o item não cabe no espaço " + espaco);
            GarantirEspacos(p);
            Item anterior = Obter(p, espaco);
            if (ReferenceEquals(anterior, item)) return null; // já estava equipado ali
            // Se o item estava em outro espaço (anel trocando de dedo), tira de lá.
            for (int i = 0; i < p.Equipados.Length; i++) if (ReferenceEquals(p.Equipados[i], item)) p.Equipados[i] = null;
            p.Inventario.Remove(item);
            p.Equipados[(int)espaco] = item;
            if (anterior != null) p.Inventario.Add(anterior);
            return anterior;
        }

        // ---------------------------------------------------------------------
        //  Inventário em grade (estilo Diablo): cada item ocupa largura x altura
        // ---------------------------------------------------------------------

        /// <summary>Tamanho do item na grade do inventário, em espaços.</summary>
        public static void Tamanho(TipoItem tipo, out int largura, out int altura)
        {
            switch (tipo)
            {
                case TipoItem.Cajado: largura = 1; altura = 3; break;
                case TipoItem.Foice: case TipoItem.Peitoral: largura = 2; altura = 3; break;
                case TipoItem.Relicario: largura = 1; altura = 2; break;
                case TipoItem.Amuleto: case TipoItem.Anel: largura = 1; altura = 1; break;
                default: largura = 2; altura = 2; break;   // Grimório, Elmo, Luvas, Botas
            }
        }

        /// <summary>
        /// Arruma os itens na grade (maiores primeiro, depois na ordem do inventário) e devolve a posição de cada um
        /// em xs/ys (-1 = não coube). "extra" é um item a mais, testado junto. Retorna true se tudo coube.
        /// </summary>
        public static bool Arrumar(IList<Item> itens, ConfigJogo cfg, int[] xs, int[] ys, Item extra = null)
        {
            int W = cfg.LarguraDoInventario, H = cfg.AlturaDoInventario, n = itens.Count + (extra != null ? 1 : 0);
            var ocupado = new bool[W * H];
            var ordem = new int[n];
            for (int i = 0; i < n; i++) ordem[i] = i;
            // ordenação estável por área decrescente (inserção: n é pequeno)
            for (int i = 1; i < n; i++)
            {
                int k = ordem[i], j = i - 1; int ak = Area(k < itens.Count ? itens[k] : extra);
                while (j >= 0 && Area(ordem[j] < itens.Count ? itens[ordem[j]] : extra) < ak) { ordem[j + 1] = ordem[j]; j--; }
                ordem[j + 1] = k;
            }
            bool tudo = true;
            for (int o = 0; o < n; o++)
            {
                int idx = ordem[o]; var it = idx < itens.Count ? itens[idx] : extra;
                int w, h; Tamanho(it.Tipo, out w, out h);
                int px = -1, py = -1;
                for (int y = 0; y + h <= H && px < 0; y++)
                    for (int x = 0; x + w <= W && px < 0; x++)
                    {
                        bool livre = true;
                        for (int dy = 0; dy < h && livre; dy++) for (int dx = 0; dx < w; dx++) if (ocupado[(y + dy) * W + x + dx]) { livre = false; break; }
                        if (livre) { px = x; py = y; }
                    }
                if (px >= 0) for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) ocupado[(py + dy) * W + px + dx] = true;
                else tudo = false;
                if (xs != null && idx < xs.Length) { xs[idx] = px; ys[idx] = py; }
            }
            return tudo;
        }

        private static int Area(Item it) { int w, h; Tamanho(it.Tipo, out w, out h); return w * h; }

        /// <summary>O item cabe no inventário? (há espaço na grade e o limite de itens não foi atingido)</summary>
        public static bool Cabe(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            return p.Inventario.Count < cfg.CapacidadeInventario && Arrumar(p.Inventario, cfg, null, null, item);
        }

        /// <summary>Tira o item do espaço e o guarda no inventário. Devolve o item, ou null se o espaço estava vazio ou o inventário está cheio.</summary>
        public static Item Desequipar(EstadoProgresso p, EspacoEquipamento espaco, ConfigJogo cfg)
        {
            Item item = Obter(p, espaco);
            if (item == null || !Cabe(p, item, cfg)) return null;
            p.Equipados[(int)espaco] = null;
            p.Inventario.Add(item);
            return item;
        }

        /// <summary>Essência que o item rende ao ser desmanchado: base da raridade x (1 + EssenciaItemPorNivel x (nível - 1)).</summary>
        public static long ValorEmEssencia(Item item, ConfigJogo cfg)
        {
            if (Vazio(item)) return 0;
            float v = cfg.EssenciaPorRaridadeItem[(int)item.Raridade] * (1f + cfg.EssenciaItemPorNivel * (item.Nivel - 1));
            long q = (long)(v + 0.5f);
            return q < 1 ? 1 : q;
        }

        /// <summary>Desmancha um item do inventário em Essência. Devolve a Essência ganha (0 se o item não estava no inventário; itens equipados precisam ser desequipados antes).</summary>
        public static long Desmanchar(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            if (item == null || item.Bloqueado || !p.Inventario.Remove(item)) return 0;
            Missoes.Registrar(p, AcaoDeMissao.Desmanchar, cfg);
            return Necropole.GanharEssencia(p, ValorEmEssencia(item, cfg), cfg);
        }

        /// <summary>
        /// "Vender tudo": desmancha de uma vez todo item do inventário que não esteja travado e cuja raridade seja no máximo
        /// "ate". Devolve a Essência ganha; "quantos" recebe o número de itens desmanchados.
        /// </summary>
        public static long DesmancharTudo(EstadoProgresso p, ConfigJogo cfg, RaridadeItem ate, out int quantos)
        {
            long total = 0; quantos = 0;
            for (int i = p.Inventario.Count - 1; i >= 0; i--)
            {
                var it = p.Inventario[i];
                if (it.Bloqueado || it.Raridade > ate) continue;
                total += Desmanchar(p, it, cfg); quantos++;
            }
            return total;
        }

        /// <summary>Quanto o "vender tudo" renderia agora (sem o bônus da Mesa de Dissecação).</summary>
        public static long ValorDeTudo(EstadoProgresso p, ConfigJogo cfg, RaridadeItem ate, out int quantos)
        {
            long total = 0; quantos = 0;
            for (int i = 0; i < p.Inventario.Count; i++)
                if (!p.Inventario[i].Bloqueado && p.Inventario[i].Raridade <= ate) { total += ValorEmEssencia(p.Inventario[i], cfg); quantos++; }
            return total;
        }

        /// <summary>
        /// Guarda um item recém-obtido aplicando o descarte automático:
        /// 1) raridade abaixo de p.DesmancharAbaixoDe -> desmancha; 2) há espaço -> guarda;
        /// 3) inventário cheio -> desmancha o pior (o novo, ou o pior guardado: menor raridade, depois menor pontuação).
        /// </summary>
        public static DestinoDoItem GuardarItem(EstadoProgresso p, Item item, ConfigJogo cfg)
        {
            if (item.Raridade < p.DesmancharAbaixoDe)
            {
                Necropole.GanharEssencia(p, ValorEmEssencia(item, cfg), cfg);
                return DestinoDoItem.Desmanchado;
            }
            if (Cabe(p, item, cfg))
            {
                p.Inventario.Add(item);
                return DestinoDoItem.Guardado;
            }
            // Cheio: abre espaço desmanchando os piores (menor raridade, depois menor pontuação), mas só os que são
            // piores que o item novo. Se nem assim couber, é o novo que vira Essência e nada mais muda.
            var removidos = new List<Item>(); var posicoes = new List<int>();
            float pontosDoNovo = Pontuacao(item, cfg);
            bool coube = false;
            while (!coube)
            {
                int pior = -1; float pontosDoPior = 0f;
                for (int i = 0; i < p.Inventario.Count; i++)
                {
                    var c = p.Inventario[i]; float pontos = Pontuacao(c, cfg);
                    if (c.Bloqueado) continue;       // travado nunca sai para abrir espaço
                    bool piorQueONovo = c.Raridade < item.Raridade || (c.Raridade == item.Raridade && pontos < pontosDoNovo);
                    if (!piorQueONovo) continue;
                    if (pior < 0 || c.Raridade < p.Inventario[pior].Raridade || (c.Raridade == p.Inventario[pior].Raridade && pontos < pontosDoPior)) { pior = i; pontosDoPior = pontos; }
                }
                if (pior < 0) break;
                removidos.Add(p.Inventario[pior]); posicoes.Add(pior);
                p.Inventario.RemoveAt(pior);
                coube = Cabe(p, item, cfg);
            }
            if (!coube)
            {
                for (int k = removidos.Count - 1; k >= 0; k--) p.Inventario.Insert(posicoes[k], removidos[k]);
                Necropole.GanharEssencia(p, ValorEmEssencia(item, cfg), cfg);
                return DestinoDoItem.Desmanchado;
            }
            for (int k = 0; k < removidos.Count; k++) Necropole.GanharEssencia(p, ValorEmEssencia(removidos[k], cfg), cfg);
            p.Inventario.Add(item);
            return DestinoDoItem.GuardadoDesmanchandoOutro;
        }

        /// <summary>
        /// Função pura: soma os implícitos, afixos, efeitos lendários e bônus de conjunto dos 9 espaços equipados.
        /// Itens no inventário não contam.
        /// </summary>
        public static BonusEquipamento CalcularBonus(EstadoProgresso p, ConfigJogo cfg)
        {
            var b = CalcularBonus(p.Equipados, cfg);
            Talentos.Aplicar(p, cfg, b);     // talentos somam no mesmo agregado
            Oferendas.Aplicar(p, cfg, b);    // oferendas também
            Ascensao.Aplicar(p, cfg, b);     // e os Legados da Ascensão
            Arvore.Aplicar(p, cfg, b);       // e a Árvore de Ascensão
            Atributos.Aplicar(p, cfg, b);    // e os atributos do necromante
            return b;
        }

        public static BonusEquipamento CalcularBonus(IList<Item> equipados, ConfigJogo cfg)
        {
            var b = new BonusEquipamento();
            if (equipados == null) return b;
            for (int i = 0; i < equipados.Count; i++)
            {
                var item = equipados[i];
                if (Vazio(item)) continue;
                if (Ferreiro.Quebrado(item, cfg)) { b.ItensQuebrados++; continue; }   // quebrado: ocupa o espaço mas não dá nada
                b.ItensEquipados++;
                if (item.UnicoId > 0 && item.UnicoId < b.Unico.Length && !item.NaoIdentificado && cfg.EfeitosDeUnicos) b.Unico[item.UnicoId] = true;
                float mel = Ferreiro.FatorDeMelhoria(item, cfg);   // +N do Ferreiro: tudo o que o item dá cresce junto
                if (item.Implicito != null && item.Implicito.Valor != 0f) b.Somar(item.Implicito.Afixo, item.Implicito.Valor * mel);
                if (item.Afixos != null)
                    for (int k = 0; k < item.Afixos.Count; k++) b.Somar(item.Afixos[k].Afixo, item.Afixos[k].Valor * mel);
                switch (item.Efeito)
                {
                    case EfeitoLendario.CeifeiroFaminto: b.CeifeiroFaminto = true; break;
                    case EfeitoLendario.MaeCarnica: b.MaeCarnica = true; break;
                    case EfeitoLendario.ReiSolitario: b.ReiSolitario = true; break; // dois anéis iguais não acumulam
                    case EfeitoLendario.LegiaoMuda: b.LegiaoMuda = true; break;
                    case EfeitoLendario.UltimoSuspiro: b.UltimoSuspiro = true; break;
                    case EfeitoLendario.MareRubra: b.MareRubra = true; break;
                }
                int c = (int)item.Conjunto;
                if (c > 0 && c < b.PecasDeConjunto.Length) b.PecasDeConjunto[c]++;
            }
            // Bônus de conjunto (2 e 3 peças).
            for (int i = 0; i < TabelaItens.Conjuntos.Length; i++)
            {
                var c = TabelaItens.Conjuntos[i];
                int pecas = (int)c.Id < b.PecasDeConjunto.Length ? b.PecasDeConjunto[(int)c.Id] : 0;
                if (pecas >= 2) for (int k = 0; k < c.BonusCom2.Length; k++) b.Somar(c.BonusCom2[k].Afixo, c.BonusCom2[k].Valor);
                if (pecas >= 3) for (int k = 0; k < c.BonusCom3.Length; k++) b.Somar(c.BonusCom3[k].Afixo, c.BonusCom3[k].Valor);
            }
            if (b.ReducaoDeRecarga > cfg.TetoReducaoDeRecarga) b.ReducaoDeRecarga = cfg.TetoReducaoDeRecarga;
            return b;
        }

        /// <summary>
        /// Pontuação simples para comparar itens do mesmo espaço (auto-equipar): cada afixo vale
        /// peso x (valor / máximo do afixo no nível 1); lendários e peças de conjunto somam um valor fixo pelo efeito.
        /// Não entende sinergias (ex.: Anel do Rei Solitário vale só pelos afixos).
        /// </summary>
        public static float Pontuacao(Item item, ConfigJogo cfg)
        {
            if (Vazio(item)) return 0f;
            float s = 0f;
            if (item.Implicito != null && item.Implicito.Valor != 0f) s += PontosDoAfixo(item.Implicito);
            if (item.Afixos != null) for (int i = 0; i < item.Afixos.Count; i++) s += PontosDoAfixo(item.Afixos[i]);
            s *= Ferreiro.FatorDeMelhoria(item, cfg);
            float fatorNivel = TabelaItens.FatorDeNivel(EscalaAfixo.Linear, item.Nivel, cfg);
            if (item.Efeito != EfeitoLendario.Nenhum)
            {
                var d = TabelaItens.Lendario(item.Efeito);
                if (d != null) s += d.Pontuacao * fatorNivel;
            }
            if (item.Conjunto != ConjuntoId.Nenhum) s += 1.5f * fatorNivel;
            return s;
        }

        private static float PontosDoAfixo(AfixoRolado a)
        {
            var d = TabelaItens.Afixo(a.Afixo);
            return d.PesoPontuacao * a.Valor / d.Max;
        }

        /// <summary>Soma da pontuação dos itens equipados.</summary>
        public static float PontuacaoEquipada(EstadoProgresso p, ConfigJogo cfg)
        {
            float s = 0f;
            if (p.Equipados == null) return 0f;
            for (int i = 0; i < p.Equipados.Length; i++) s += Pontuacao(p.Equipados[i], cfg);
            return s;
        }

        /// <summary>
        /// Equipa, do inventário, todo item com pontuação maior que a do que está no espaço dele (anéis: troca o pior dos dois).
        /// Devolve quantas trocas fez. Os itens que saem voltam para o inventário.
        /// </summary>
        public static int AutoEquipar(EstadoProgresso p, ConfigJogo cfg)
        {
            GarantirEspacos(p);
            int trocas = 0;
            bool mudou = true;
            int guarda = 0;
            while (mudou && guarda++ < 1000)
            {
                mudou = false;
                for (int i = 0; i < p.Inventario.Count; i++)
                {
                    var item = p.Inventario[i];
                    if (Vazio(item) || item.NaoIdentificado || !Requisitos.Atende(p, item, cfg)) continue;
                    var espaco = TabelaItens.EspacoDoTipo(item.Tipo);
                    if (espaco == EspacoEquipamento.Anel1
                        && Pontuacao(Obter(p, EspacoEquipamento.Anel2), cfg) < Pontuacao(Obter(p, EspacoEquipamento.Anel1), cfg))
                        espaco = EspacoEquipamento.Anel2;
                    if (Pontuacao(item, cfg) > Pontuacao(Obter(p, espaco), cfg))
                    {
                        Equipar(p, item, espaco);
                        trocas++;
                        mudou = true;
                        break; // o inventário mudou: recomeça a varredura
                    }
                }
            }
            return trocas;
        }

        /// <summary>Saves antigos (ou vindos do JsonUtility) podem trazer o vetor de espaços nulo ou curto.</summary>
        private static void GarantirEspacos(EstadoProgresso p)
        {
            if (p.Equipados != null && p.Equipados.Length == TabelaItens.TotalDeEspacos) { if (p.Inventario == null) p.Inventario = new List<Item>(); return; }
            var novo = new Item[TabelaItens.TotalDeEspacos];
            if (p.Equipados != null) for (int i = 0; i < p.Equipados.Length && i < novo.Length; i++) novo[i] = p.Equipados[i];
            p.Equipados = novo;
            if (p.Inventario == null) p.Inventario = new List<Item>();
        }
    }
}
