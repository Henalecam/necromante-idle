using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>Um item Único (Diablo 1): nome fixo, tipo fixo e afixos fixos, sempre no topo da faixa e ainda mais fortes.</summary>
    public sealed class DadosUnico
    {
        public int Id; public string Nome; public TipoItem Tipo; public AfixoItem[] Afixos; public int DificuldadeMinima; public int AndarMinimo; public bool SoDeMissao; public string Lenda; public string Efeito;
    }

    public static class Unicos
    {
        public static readonly DadosUnico[] Todos =
        {
            new DadosUnico { Id = 1, Efeito = "Golpes dos minions matam na hora inimigos comuns abaixo de 30% de vida.", Nome = "Cutelo do Açougueiro", Tipo = TipoItem.Foice, Afixos = new[] { AfixoItem.DanoDosMinions, AfixoItem.ChanceCritico, AfixoItem.RouboDeVidaDosMinions }, SoDeMissao = true, Lenda = "Largado pelo Açougueiro, no andar 2." },
            new DadosUnico { Id = 2, Efeito = "Na luta, um morto se ergue a cada 20 s e luta por você por 12 s.", Nome = "Coroa do Rei Morto", Tipo = TipoItem.Elmo, Afixos = new[] { AfixoItem.Comando, AfixoItem.VidaDosMinions, AfixoItem.ResSombra }, SoDeMissao = true, Lenda = "Tirada do Rei Esqueleto, no andar 3." },
            new DadosUnico { Id = 3, Efeito = "A Lança de Ossos custa 40% menos mana.", Nome = "Cajado do Coveiro Cego", Tipo = TipoItem.Cajado, Afixos = new[] { AfixoItem.DanoDeFeiticos, AfixoItem.Mana, AfixoItem.ReducaoDeRecarga }, AndarMinimo = 3, Lenda = "Qualquer dificuldade, do andar 3 em diante." },
            new DadosUnico { Id = 4, Efeito = "Quem fere o necromante recebe metade do dano de volta.", Nome = "Mortalha da Abadessa", Tipo = TipoItem.Peitoral, Afixos = new[] { AfixoItem.Vida, AfixoItem.Armadura, AfixoItem.ResVeneno }, AndarMinimo = 5, Lenda = "Qualquer dificuldade, do andar 5 em diante." },
            new DadosUnico { Id = 5, Efeito = "Cada alma ceifada cura os minions em 6% da vida.", Nome = "Anel dos Mil Sussurros", Tipo = TipoItem.Anel, Afixos = new[] { AfixoItem.ChanceDeAlma, AfixoItem.VelocidadeDeCeifa, AfixoItem.PoderNecromantico }, AndarMinimo = 6, Lenda = "Qualquer dificuldade, do andar 6 em diante." },
            new DadosUnico { Id = 6, Efeito = "O minion que mata recupera 8% da vida.", Nome = "Garras do Primeiro Carniçal", Tipo = TipoItem.Luvas, Afixos = new[] { AfixoItem.VelAtaqueDosMinions, AfixoItem.DanoCritico, AfixoItem.DanoDosMinions }, AndarMinimo = 8, Lenda = "Qualquer dificuldade, do andar 8 em diante." },
            new DadosUnico { Id = 7, Efeito = "Cada feitiço tem 25% de chance de não entrar em recarga.", Nome = "Grimório de Pele Humana", Tipo = TipoItem.Grimorio, Afixos = new[] { AfixoItem.DanoDeFeiticos, AfixoItem.RegenMana, AfixoItem.ChanceCritico, AfixoItem.PoderNecromantico }, DificuldadeMinima = 1, Lenda = "Só no Pesadelo ou no Inferno." },
            new DadosUnico { Id = 8, Efeito = "Poções de vida e de mana rendem 60% a mais.", Nome = "Botas do Peregrino Enforcado", Tipo = TipoItem.Botas, Afixos = new[] { AfixoItem.Vida, AfixoItem.ResFogo, AfixoItem.ResGelo, AfixoItem.ResRaio }, DificuldadeMinima = 1, Lenda = "Só no Pesadelo ou no Inferno." },
            new DadosUnico { Id = 9, Efeito = "O Golem tem 60% a mais de vida e dura o dobro.", Nome = "Amuleto do Senhor do Terror", Tipo = TipoItem.Amuleto, Afixos = new[] { AfixoItem.Comando, AfixoItem.DanoDosMinions, AfixoItem.VidaDosMinions, AfixoItem.PoderNecromantico }, DificuldadeMinima = 2, Lenda = "Só no Inferno." },
            new DadosUnico { Id = 10, Efeito = "Matar um elite, Campeão, Único ou chefe cura todos os minions em 25%.", Nome = "Relicário das Almas Presas", Tipo = TipoItem.Relicario, Afixos = new[] { AfixoItem.ChanceDeAlma, AfixoItem.Mana, AfixoItem.RouboDeVidaDosMinions, AfixoItem.Armadura }, DificuldadeMinima = 2, Lenda = "Só no Inferno." },
            new DadosUnico { Id = 11, Efeito = "Os Ocultos perdem o golpe de emboscada contra o seu exército.", Nome = "Olho Óptico", Tipo = TipoItem.Amuleto, Afixos = new[] { AfixoItem.ChanceCritico, AfixoItem.ResRaio, AfixoItem.Mana }, SoDeMissao = true, Lenda = "Achado nos Salões dos Cegos, no andar 9." },
            new DadosUnico { Id = 12, Efeito = "Os minions causam +30% de dano a elites, campeões, Únicos e chefes.", Nome = "Estandarte Sangrento", Tipo = TipoItem.Relicario, Afixos = new[] { AfixoItem.DanoDosMinions, AfixoItem.VidaDosMinions, AfixoItem.Comando }, SoDeMissao = true, Lenda = "Tomado de Vargoth, na Fortaleza Rubra do andar 13." },
        };

        public static DadosUnico Obter(int id) { for (int i = 0; i < Todos.Length; i++) if (Todos[i].Id == id) return Todos[i]; return null; }

        /// <summary>Cria o Único: cada afixo no topo da faixa do nível, multiplicado pela força de Único.</summary>
        public static Item Gerar(Rng rng, ConfigJogo cfg, int nivel, DadosUnico d)
        {
            var item = GeradorDeItens.GerarDoTipo(rng, cfg, nivel, RaridadeItem.Raro, d.Tipo);
            item.Afixos.Clear();
            for (int i = 0; i < d.Afixos.Length; i++)
            {
                float min, max; TabelaItens.Faixa(d.Afixos[i], item.Nivel, cfg, out min, out max);
                item.Afixos.Add(new AfixoRolado(d.Afixos[i], d.Afixos[i] == AfixoItem.Comando ? max : max * cfg.UnicoForcaDosAfixos));
            }
            item.Nome = d.Nome; item.UnicoId = d.Id; item.Bloqueado = true;
            return item;
        }

        /// <summary>Sorteia um Único que possa cair neste andar e nesta dificuldade (nunca os de missão). null = nenhum disponível.</summary>
        public static DadosUnico Sortear(Rng rng, int andar, int dificuldade)
        {
            int n = 0;
            for (int i = 0; i < Todos.Length; i++) if (!Todos[i].SoDeMissao && Todos[i].DificuldadeMinima <= dificuldade && Todos[i].AndarMinimo <= andar) n++;
            if (n == 0) return null;
            int qual = rng.Intervalo(0, n);
            for (int i = 0; i < Todos.Length; i++) if (!Todos[i].SoDeMissao && Todos[i].DificuldadeMinima <= dificuldade && Todos[i].AndarMinimo <= andar && qual-- == 0) return Todos[i];
            return null;
        }

        public static void Registrar(EstadoProgresso p, List<Item> itens)
        {
            if (itens == null) return;
            for (int i = 0; i < itens.Count; i++) if (itens[i] != null && itens[i].UnicoId > 0 && itens[i].UnicoId < 31) p.UnicosAchados |= 1 << itens[i].UnicoId;
        }
    }
}
