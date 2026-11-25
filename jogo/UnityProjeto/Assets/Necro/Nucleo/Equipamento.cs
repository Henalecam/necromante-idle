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

        // ---- Minions ----
        public float PoderNecromantico;     // somado ao Poder Necromântico do progresso
        public float VidaDosMinions;        // vida máxima x (1 + valor)
        public float DanoDosMinions;        // "aumento" (aditivo) no dano
        public float VelAtaqueDosMinions;   // velocidade de ataque x (1 + valor)
        public float RouboDeVidaDosMinions;

        // ---- Lendários e conjuntos ----
        public bool CeifeiroFaminto;
        public bool MaeCarnica;
        public bool ReiSolitario;
        public bool LegiaoMuda;
        public bool UltimoSuspiro;
        public bool MareRubra;
        /// <summary>Peças equipadas de cada conjunto (índice = (int)ConjuntoId).</summary>
        public int[] PecasDeConjunto = new int[2];
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
            if (Vazio(item)) return false;
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

        /// <summary>Tira o item do espaço e o guarda no inventário. Devolve o item, ou null se o espaço estava vazio ou o inventário está cheio.</summary>
        public static Item Desequipar(EstadoProgresso p, EspacoEquipamento espaco, ConfigJogo cfg)
        {
            Item item = Obter(p, espaco);
            if (item == null || p.Inventario.Count >= cfg.CapacidadeInventario) return null;
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
            if (item == null || !p.Inventario.Remove(item)) return 0;
            Missoes.Registrar(p, AcaoDeMissao.Desmanchar, cfg);
            return Necropole.GanharEssencia(p, ValorEmEssencia(item, cfg), cfg);
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
            if (p.Inventario.Count < cfg.CapacidadeInventario)
            {
                p.Inventario.Add(item);
                return DestinoDoItem.Guardado;
            }
            int pior = 0;
            float pontosDoPior = Pontuacao(p.Inventario[0], cfg);
            for (int i = 1; i < p.Inventario.Count; i++)
            {
                float pontos = Pontuacao(p.Inventario[i], cfg);
                if (p.Inventario[i].Raridade < p.Inventario[pior].Raridade
                    || (p.Inventario[i].Raridade == p.Inventario[pior].Raridade && pontos < pontosDoPior)) { pior = i; pontosDoPior = pontos; }
            }
            if (p.Inventario.Count == 0 || item.Raridade < p.Inventario[pior].Raridade
                || (item.Raridade == p.Inventario[pior].Raridade && Pontuacao(item, cfg) <= pontosDoPior))
            {
                Necropole.GanharEssencia(p, ValorEmEssencia(item, cfg), cfg);
                return DestinoDoItem.Desmanchado;
            }
            Necropole.GanharEssencia(p, ValorEmEssencia(p.Inventario[pior], cfg), cfg);
            p.Inventario.RemoveAt(pior);
            p.Inventario.Add(item);
            return DestinoDoItem.GuardadoDesmanchandoOutro;
        }

        /// <summary>
        /// Função pura: soma os implícitos, afixos, efeitos lendários e bônus de conjunto dos 9 espaços equipados.
        /// Itens no inventário não contam.
        /// </summary>
        public static BonusEquipamento CalcularBonus(EstadoProgresso p, ConfigJogo cfg)
        {
            return CalcularBonus(p.Equipados, cfg);
        }

        public static BonusEquipamento CalcularBonus(IList<Item> equipados, ConfigJogo cfg)
        {
            var b = new BonusEquipamento();
            if (equipados == null) return b;
            for (int i = 0; i < equipados.Count; i++)
            {
                var item = equipados[i];
                if (Vazio(item)) continue;
                b.ItensEquipados++;
                if (item.Implicito != null && item.Implicito.Valor != 0f) b.Somar(item.Implicito.Afixo, item.Implicito.Valor);
                if (item.Afixos != null)
                    for (int k = 0; k < item.Afixos.Count; k++) b.Somar(item.Afixos[k].Afixo, item.Afixos[k].Valor);
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
                    if (Vazio(item)) continue;
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
