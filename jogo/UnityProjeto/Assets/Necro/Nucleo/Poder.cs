using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Poder: um número só que resume a força, calculado dos atributos reais (os mesmos que a simulação usa).
    /// Serve para comparar minions, itens e o exército inteiro com o que o andar pede. Não entra em nenhuma regra de combate.
    /// </summary>
    public static class Poder
    {
        /// <summary>Atributos do minion na próxima expedição: qualidade, nível, Poder Necromântico, equipamento, talentos, oferendas, legados, Bestiário, dons e traços.</summary>
        public static void Atributos(EstadoProgresso p, MinionSalvo m, ConfigJogo cfg, out float vida, out float dano, out float velAtaque, out float armadura)
        {
            var d = TabelaEspecies.Obter(m.Alma.Especie);
            var b = Equipamento.CalcularBonus(p, cfg);
            float poder = p.PoderNecromantico + b.PoderNecromantico, dom = Bestiario.MultMinion(p, m.Alma.Especie, cfg);
            vida = Formulas.AtributoMinion(d.Vida, m.Alma.Qualidade, m.Nivel, poder, cfg) * dom * (1f + b.VidaDosMinions) * (1f + m.Alma.DomVida);
            dano = Formulas.AtributoMinion(d.Dano, m.Alma.Qualidade, m.Nivel, poder, cfg) * dom * (1f + b.DanoDosMinions) * (1f + m.Alma.DomDano);
            armadura = Formulas.AtributoMinion(d.Armadura, m.Alma.Qualidade, m.Nivel, poder, cfg);
            velAtaque = d.VelAtaque * (1f + b.VelAtaqueDosMinions) * (1f + m.Alma.DomVelocidade);
            if (m.Alma.Tem(Afixo.Frenetico)) velAtaque *= 1f + cfg.FreneticoBonus;
            if (m.Alma.Tem(Afixo.Petreo)) armadura += cfg.PetreoArmadura;
            if (m.Alma.Traco == TracoProfano.Colosso) vida *= 1f + cfg.ColossoVida;
            if (m.Alma.Traco == TracoProfano.Carniceiro) dano *= 1f + cfg.CarniceiroDano;
        }

        /// <summary>Poder de um corpo: raiz de (vida efetiva x dano por segundo), vezes 10.</summary>
        public static long DeAtributos(float vida, float dano, float velAtaque, float armadura)
        {
            double vidaEfetiva = vida * (1.0 + Math.Max(0f, armadura) / 100.0);
            return (long)Math.Round(Math.Sqrt(Math.Max(0.0, vidaEfetiva * dano * velAtaque)) * 10.0);
        }

        public static long DoMinion(EstadoProgresso p, MinionSalvo m, ConfigJogo cfg)
        {
            float v, d, vel, a; Atributos(p, m, cfg, out v, out d, out vel, out a);
            return DeAtributos(v, d, vel, a);
        }

        public static long DoNecromante(EstadoProgresso p, ConfigJogo cfg)
        {
            var d = TabelaEspecies.Obter(EspecieId.Necromante);
            var b = Equipamento.CalcularBonus(p, cfg);
            float f = Formulas.FatorNivelNecromante(p.NivelNecromante, cfg);
            float vida = d.Vida * f * (1f + b.VidaDoNecromantePercentual) + b.Vida;
            float dano = d.Dano * f * (1f + b.DanoDeFeiticos) * 2f;      // x2: os feitiços somam ao ataque básico
            return DeAtributos(vida, dano, d.VelAtaque, d.Armadura + b.Armadura);
        }

        /// <summary>Poder do item: a pontuação que o "equipar os melhores" já usa, em escala de leitura.</summary>
        public static long DoItem(Item item, ConfigJogo cfg) { return (long)Math.Round(Equipamento.Pontuacao(item, cfg) * 10f); }

        /// <summary>Necromante + minions em campo.</summary>
        public static long Total(EstadoProgresso p, ConfigJogo cfg)
        {
            long t = DoNecromante(p, cfg);
            for (int i = 0; i < p.Minions.Count; i++) if (p.Minions[i].Ativo) t += DoMinion(p, p.Minions[i], cfg);
            return t;
        }

        /// <summary>
        /// Poder sugerido para um andar: o Poder de um jogo novo (que dá conta do andar 1) crescendo como os inimigos crescem.
        /// É estimativa para orientar, não portão: quem joga com a mão passa com menos.
        /// </summary>
        public static long SugeridoParaOAndar(int andar, ConfigJogo cfg)
        {
            long basePoder = Total(Progressao.NovoJogo(cfg), cfg);
            // vida e dano dos inimigos crescem juntos; o Poder é raiz do produto, então acompanha a escala uma vez
            return (long)Math.Round(basePoder * Formulas.EscalaInimigo(Math.Max(1, andar), cfg) * cfg.FolgaDoPoderSugerido);
        }
    }
}
