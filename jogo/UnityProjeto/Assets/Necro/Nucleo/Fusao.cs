using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>
    /// Fusão de almas: três almas da mesma espécie e qualidade viram uma de qualidade acima, que herda afixos das três.
    /// É a troca de quantidade por qualidade: três corpos medianos ocupam três de Comando; um corpo melhor ocupa um.
    /// </summary>
    public static class Fusao
    {
        public static int Contar(EstadoProgresso p, EspecieId especie, Qualidade q)
        {
            int n = 0;
            for (int i = 0; i < p.Filacterio.Count; i++) if (p.Filacterio[i].Especie == especie && p.Filacterio[i].Qualidade == q) n++;
            return n;
        }

        public static long Custo(Qualidade q, ConfigJogo cfg) { return q < Qualidade.Profana ? cfg.EssenciaParaFundir[(int)q] : 0; }

        public static bool Pode(EstadoProgresso p, EspecieId especie, Qualidade q, ConfigJogo cfg)
        {
            return q < Qualidade.Profana && Contar(p, especie, q) >= cfg.AlmasPorFusao && p.Essencia >= Custo(q, cfg);
        }

        /// <summary>Funde e devolve a alma nova (já guardada no Filactério), ou null se não puder.</summary>
        public static Alma Fundir(EstadoProgresso p, EspecieId especie, Qualidade q, ConfigJogo cfg)
        {
            if (!Pode(p, especie, q, cfg)) return null;
            p.Essencia -= Custo(q, cfg);
            var heranca = new List<Afixo>();
            int tirar = cfg.AlmasPorFusao;
            for (int i = p.Filacterio.Count - 1; i >= 0 && tirar > 0; i--)
            {
                var a = p.Filacterio[i];
                if (a.Especie != especie || a.Qualidade != q) continue;
                if (a.Afixos != null) for (int k = 0; k < a.Afixos.Count; k++) if (!heranca.Contains(a.Afixos[k])) heranca.Add(a.Afixos[k]);
                p.Filacterio.RemoveAt(i); tirar--;
            }
            var rng = new Rng(p.ContadorDeSementes++ * 0x9E3779B97F4A7C15UL + 0xF05A0UL);
            var nova = new Alma { Especie = especie, Qualidade = q + 1 };
            int nq = (int)nova.Qualidade;
            // herda primeiro (em ordem sorteada) o que as três tinham; completa com sorteio se faltar para o mínimo da qualidade
            int alvo = heranca.Count < cfg.AfixosMin[nq] ? cfg.AfixosMin[nq] : (heranca.Count > cfg.AfixosMax[nq] ? cfg.AfixosMax[nq] : heranca.Count);
            while (nova.Afixos.Count < alvo && heranca.Count > 0)
            {
                int k = rng.Intervalo(0, heranca.Count);
                nova.Afixos.Add(heranca[k]); heranca.RemoveAt(k);
            }
            while (nova.Afixos.Count < alvo)
            {
                var a = (Afixo)rng.Intervalo(0, 6);
                if (!nova.Afixos.Contains(a)) nova.Afixos.Add(a);
            }
            if (nova.Qualidade == Qualidade.Profana) nova.Traco = (TracoProfano)(1 + rng.Intervalo(0, 3));
            p.Filacterio.Add(nova);
            p.FusoesFeitas++;
            return nova;
        }
    }
}
