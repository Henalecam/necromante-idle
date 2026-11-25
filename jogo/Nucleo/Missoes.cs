using System;

namespace Necro.Nucleo
{
    /// <summary>O que a missão pede. "Contadores" somam enquanto a missão está ativa; "estados" olham o progresso salvo.</summary>
    public enum TipoMissao
    {
        // estados
        AlcancarAndar, MinionsEmCampo, ItensEquipados, NivelDeConstrucao, DerrotarChefe,
        // contadores
        MatarInimigos, MatarEspecie, MatarElites, CeifarAlmas, ErguerMinions, MelhorarConstrucoes, DesmancharItens
    }

    /// <summary>Ações fora da expedição que contam para missões.</summary>
    public enum AcaoDeMissao { Erguer, Melhorar, Desmanchar }

    /// <summary>Sistemas da interface que as missões abrem (só a interface olha isto; a simulação não muda).</summary>
    public enum Sistema { Exercito, Necropole, Equipamento }

    [Serializable]
    public class DadosMissao
    {
        public string Titulo;
        public string Historia;       // uma ou duas frases de ambientação
        public string Objetivo;       // o que fazer, em uma linha
        public TipoMissao Tipo;
        public int Alvo;              // andar, espécie, construção, Ato ou qualidade mínima, conforme o Tipo
        public int Quantidade = 1;
        public long Ossos;
        public long Essencia;
    }

    /// <summary>
    /// Cadeia de missões: uma por vez, em ordem. Dá um objetivo sempre à vista, conta a história aos poucos
    /// e abre os sistemas da interface um a um. A recompensa só entra quando o jogador resgata.
    /// </summary>
    public static class Missoes
    {
        public static DadosMissao Atual(EstadoProgresso p, ConfigJogo cfg)
        {
            return p.MissaoAtual >= 0 && p.MissaoAtual < cfg.Missoes.Length ? cfg.Missoes[p.MissaoAtual] : null;
        }

        public static bool EhContador(TipoMissao t) { return t >= TipoMissao.MatarInimigos; }

        /// <summary>Quanto um resultado de expedição (inteiro ou parcial) soma na missão.</summary>
        public static long Contar(DadosMissao m, ResultadoExpedicao r)
        {
            if (m == null || r == null) return 0;
            switch (m.Tipo)
            {
                case TipoMissao.MatarInimigos: return r.InimigosMortos;
                case TipoMissao.MatarElites: return r.ElitesMortos;
                case TipoMissao.MatarEspecie: return m.Alvo >= 0 && m.Alvo < r.MortesPorEspecie.Length ? r.MortesPorEspecie[m.Alvo] : 0;
                case TipoMissao.CeifarAlmas:
                    int n = 0;
                    for (int i = 0; i < r.Almas.Count; i++) if ((int)r.Almas[i].Qualidade >= m.Alvo) n++;
                    return n;
                default: return 0;
            }
        }

        /// <summary>Progresso da missão atual. Passe o resultado parcial da expedição em curso para mostrar ao vivo.</summary>
        public static long Progresso(EstadoProgresso p, ConfigJogo cfg, ResultadoExpedicao emCurso = null)
        {
            var m = Atual(p, cfg);
            if (m == null) return 0;
            long v;
            switch (m.Tipo)
            {
                case TipoMissao.AlcancarAndar:
                    v = Math.Max(p.MaiorAndarConcluido, emCurso != null ? emCurso.MaiorAndarConcluido : 0) >= m.Alvo ? 1 : 0; break;
                case TipoMissao.MinionsEmCampo:
                    v = 0; for (int i = 0; i < p.Minions.Count; i++) if (p.Minions[i].Ativo) v++; break;
                case TipoMissao.ItensEquipados:
                    v = 0; for (int e = 0; e < 9; e++) if (!Equipamento.Vazio(Equipamento.Obter(p, (EspacoEquipamento)e))) v++; break;
                case TipoMissao.NivelDeConstrucao: v = Necropole.Nivel(p, (Construcao)m.Alvo); break;
                case TipoMissao.DerrotarChefe:
                    v = p.ChefesDerrotados.Contains(m.Alvo) || (emCurso != null && emCurso.ChefesDerrotados.Contains(m.Alvo)) ? 1 : 0; break;
                default: v = p.ProgressoMissao + Contar(m, emCurso); break;
            }
            return Math.Max(0, Math.Min(v, m.Quantidade));
        }

        public static bool Completa(EstadoProgresso p, ConfigJogo cfg, ResultadoExpedicao emCurso = null)
        {
            var m = Atual(p, cfg);
            return m != null && Progresso(p, cfg, emCurso) >= m.Quantidade;
        }

        /// <summary>Soma o que a expedição encerrada rendeu (chamado por Progressao.AplicarResultado).</summary>
        public static void RegistrarExpedicao(EstadoProgresso p, ResultadoExpedicao r, ConfigJogo cfg)
        {
            var m = Atual(p, cfg);
            if (m != null && EhContador(m.Tipo)) p.ProgressoMissao += Contar(m, r);
        }

        /// <summary>Soma uma ação da Necrópole (erguer, melhorar, desmanchar).</summary>
        public static void Registrar(EstadoProgresso p, AcaoDeMissao acao, ConfigJogo cfg, int quantidade = 1)
        {
            var m = Atual(p, cfg);
            if (m == null) return;
            if ((acao == AcaoDeMissao.Erguer && m.Tipo == TipoMissao.ErguerMinions) ||
                (acao == AcaoDeMissao.Melhorar && m.Tipo == TipoMissao.MelhorarConstrucoes) ||
                (acao == AcaoDeMissao.Desmanchar && m.Tipo == TipoMissao.DesmancharItens))
                p.ProgressoMissao += quantidade;
        }

        /// <summary>
        /// Entrega a recompensa e passa para a próxima missão. Retorna false se ainda não está completa.
        /// Com a expedição em curso (emCurso), vale também o que ela já rendeu: o andar ou chefe vencido entra
        /// no save na hora, e a missão seguinte só conta o que acontecer depois do resgate.
        /// </summary>
        public static bool Resgatar(EstadoProgresso p, ConfigJogo cfg, ResultadoExpedicao emCurso = null)
        {
            if (!Completa(p, cfg, emCurso)) return false;
            var m = Atual(p, cfg);
            if (emCurso != null)
            {
                if (emCurso.MaiorAndarConcluido > p.MaiorAndarConcluido) p.MaiorAndarConcluido = emCurso.MaiorAndarConcluido;
                for (int i = 0; i < emCurso.ChefesDerrotados.Count; i++)
                    if (!p.ChefesDerrotados.Contains(emCurso.ChefesDerrotados[i])) p.ChefesDerrotados.Add(emCurso.ChefesDerrotados[i]);
            }
            p.Ossos += m.Ossos; p.Essencia += m.Essencia;
            p.MissaoAtual++;
            // o que a expedição em curso já tinha antes do resgate não conta para a missão seguinte
            p.ProgressoMissao = emCurso != null ? -Contar(Atual(p, cfg), emCurso) : 0;
            return true;
        }

        /// <summary>Quantas missões é preciso ter resgatado para a interface mostrar cada sistema.</summary>
        public static bool Aberto(EstadoProgresso p, Sistema s, ConfigJogo cfg)
        {
            return p.MissaoAtual >= cfg.MissoesParaAbrir[(int)s];
        }
    }
}
