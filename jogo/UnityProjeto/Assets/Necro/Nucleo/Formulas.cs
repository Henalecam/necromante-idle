using System;

namespace Necro.Nucleo
{
    /// <summary>Fórmulas puras do documento de design (testáveis isoladamente).</summary>
    public static class Formulas
    {
        /// <summary>Mitigação da armadura = Armadura / (Armadura + 100 + 20 x andar), com teto de 75%.</summary>
        public static float MitigacaoArmadura(float armadura, int andar, ConfigJogo cfg)
        {
            if (armadura <= 0f) return 0f;
            float m = armadura / (armadura + cfg.ArmaduraConstante + cfg.ArmaduraPorAndar * andar);
            return m > cfg.TetoMitigacao ? cfg.TetoMitigacao : m;
        }

        /// <summary>Resistência com teto de 75% (valores negativos = vulnerabilidade, sem piso).</summary>
        public static float ResistenciaEfetiva(float resistencia, ConfigJogo cfg)
        {
            return resistencia > cfg.TetoMitigacao ? cfg.TetoMitigacao : resistencia;
        }

        /// <summary>
        /// Mitigação total contra um tipo de dano: resistência do tipo e, para dano Físico, também a armadura
        /// (combinadas multiplicativamente). O total nunca passa do teto de 75%.
        /// </summary>
        public static float MitigacaoTotal(float armadura, float resistenciaDoTipo, TipoDano tipo, int andar, ConfigJogo cfg)
        {
            float res = ResistenciaEfetiva(resistenciaDoTipo, cfg);
            float total = res;
            if (tipo == TipoDano.Fisico)
            {
                float arm = MitigacaoArmadura(armadura, andar, cfg);
                total = 1f - (1f - arm) * (1f - res);
            }
            return total > cfg.TetoMitigacao ? cfg.TetoMitigacao : total;
        }

        /// <summary>Dano = Base x (1 + soma dos aumentos) x produto dos (1 + mais) x (1 - mitigação).</summary>
        public static float Dano(float danoBase, float somaAumentos, float produtoMais, float mitigacao)
        {
            return danoBase * (1f + somaAumentos) * produtoMais * (1f - mitigacao);
        }

        /// <summary>Escala composta de vida/dano dos inimigos: (1 + taxa)^(andar - 1), vezes o degrau de cada Ato já iniciado.</summary>
        public static float EscalaInimigo(int andar, ConfigJogo cfg)
        {
            float e = MathF.Pow(1f + cfg.CrescimentoPorAndar, andar - 1);
            int ato = (andar - 1) / cfg.AndaresPorAto;      // 0 no Ato 1
            if (ato > 0 && cfg.DegrauPorAto != 1f) e *= MathF.Pow(cfg.DegrauPorAto, ato);
            // segunda metade de cada Ato é mais íngreme (e alivia no primeiro andar do Ato seguinte)
            int pos = (andar - 1) % cfg.AndaresPorAto + 1;
            if (cfg.InclinacaoFimDeAto != 0f && pos > cfg.AndarDaInclinacao) e *= MathF.Pow(1f + cfg.InclinacaoFimDeAto, pos - cfg.AndarDaInclinacao);
            // os primeiros andares do jogo são um pouco mais leves: a primeira expedição precisa passar da âncora
            if (andar <= cfg.AlivioAteOAndar) e *= cfg.AlivioInicial;
            return e;
        }

        public static float FatorNivelMinion(int nivel, ConfigJogo cfg)
        {
            return 1f + cfg.MinionCrescimentoPorNivel * (nivel - 1);
        }

        public static float FatorNivelNecromante(int nivel, ConfigJogo cfg)
        {
            return 1f + cfg.NecromanteCrescimentoPorNivel * (nivel - 1);
        }

        /// <summary>Atributo de minion = Base da espécie x Qualidade x fator de Nível x (1 + Poder Necromântico).</summary>
        public static float AtributoMinion(float baseEspecie, Qualidade qualidade, int nivel, float poderNecromantico, ConfigJogo cfg)
        {
            return baseEspecie * cfg.MultQualidade[(int)qualidade] * FatorNivelMinion(nivel, cfg) * (1f + poderNecromantico);
        }

        /// <summary>Tipo de dano do golpe final -> condição do cadáver (Execução gera Intacto, tratado à parte).</summary>
        public static CondicaoCadaver CondicaoPorTipo(TipoDano tipo)
        {
            switch (tipo)
            {
                case TipoDano.Fogo: return CondicaoCadaver.Queimado;
                case TipoDano.Gelo: return CondicaoCadaver.Congelado;
                case TipoDano.Raio: return CondicaoCadaver.Eletrizado;
                case TipoDano.Veneno: return CondicaoCadaver.Putrefato;
                case TipoDano.Sombra: return CondicaoCadaver.Oco;
                default: return CondicaoCadaver.Mutilado;
            }
        }

        public static float XpParaProximoNivelMinion(int nivel) { return 30f * MathF.Pow(nivel, 1.5f); }

        public static float XpParaProximoNivelNecromante(int nivel) { return 60f * MathF.Pow(nivel, 1.6f); }

        public static int AtoDoAndar(int andar, ConfigJogo cfg) { return (andar - 1) / cfg.AndaresPorAto + 1; }

        public static int AndarNoAto(int andar, ConfigJogo cfg) { return (andar - 1) % cfg.AndaresPorAto + 1; }

        public static bool AndarDeChefe(int andar, ConfigJogo cfg) { return andar % cfg.AndaresPorAto == 0; }
    }
}
