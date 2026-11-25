using System;

namespace Necro.Nucleo
{
    /// <summary>Construções da Necrópole (meta-progressão). A ordem é a do save: novas entram no fim.</summary>
    public enum Construcao { AltarDeErguimento, Cripta, TorreDasAlmas, Ossuario, MesaDeDissecacao }

    /// <summary>Um pacto de encruzilhada: vale até o fim da expedição e sempre cobra um preço.</summary>
    [Serializable]
    public class DadosPacto
    {
        public string Nome;
        public string Ganho;
        public string Preco;
        public float DanoMinion = 1f, DanoInimigo = 1f, VidaInimigo = 1f, Ossos = 1f, ChanceDeItem = 1f, RecargaDosPoderes = 1f;
        public float ChanceDeAlma;       // somado aos outros aumentos de chance de alma
        public bool CuraAgora;
    }

    [Serializable]
    public class DadosConstrucao
    {
        public Construcao Id;
        public string Nome;
        public string Efeito;
        public int AndarDeDesbloqueio;   // MaiorAndarConcluido necessário
        public int NivelMaximo;
        public long OssosBase;
        public long EssenciaBase;
    }

    /// <summary>
    /// Regras da Necrópole: custo de cada nível = base x Crescimento^nível (Ossos e Essência).
    /// Efeitos: Altar barateia erguer minions; Cripta dá Comando; Torre das Almas amplia o Filactério e a chance de alma;
    /// Ossuário rende mais Ossos por expedição; Mesa de Dissecação rende mais Essência ao desmanchar itens e dissolver almas.
    /// </summary>
    public static class Necropole
    {
        public const int TotalDeConstrucoes = 5;

        public static DadosConstrucao Dados(Construcao c, ConfigJogo cfg) { return cfg.Construcoes[(int)c]; }

        public static int Nivel(EstadoProgresso p, Construcao c)
        {
            return p.Construcoes != null && (int)c < p.Construcoes.Length ? p.Construcoes[(int)c] : 0;
        }

        public static bool Desbloqueada(EstadoProgresso p, Construcao c, ConfigJogo cfg)
        {
            return p.MaiorAndarConcluido >= Dados(c, cfg).AndarDeDesbloqueio;
        }

        public static void Custo(EstadoProgresso p, Construcao c, ConfigJogo cfg, out long ossos, out long essencia)
        {
            var d = Dados(c, cfg);
            double f = Math.Pow(cfg.CrescimentoCustoConstrucao, Nivel(p, c));
            ossos = (long)Math.Ceiling(d.OssosBase * f);
            essencia = (long)Math.Ceiling(d.EssenciaBase * f);
        }

        public static bool PodeMelhorar(EstadoProgresso p, Construcao c, ConfigJogo cfg)
        {
            if (!Desbloqueada(p, c, cfg) || Nivel(p, c) >= Dados(c, cfg).NivelMaximo) return false;
            long o, e; Custo(p, c, cfg, out o, out e);
            return p.Ossos >= o && p.Essencia >= e;
        }

        /// <summary>Sobe um nível pagando o custo. Retorna false (sem mudar nada) se não puder.</summary>
        public static bool Melhorar(EstadoProgresso p, Construcao c, ConfigJogo cfg)
        {
            if (!PodeMelhorar(p, c, cfg)) return false;
            long o, e; Custo(p, c, cfg, out o, out e);
            p.Ossos -= o; p.Essencia -= e;
            if (p.Construcoes == null || p.Construcoes.Length < TotalDeConstrucoes)
            {
                var novo = new int[TotalDeConstrucoes];
                if (p.Construcoes != null) Array.Copy(p.Construcoes, novo, p.Construcoes.Length);
                p.Construcoes = novo;
            }
            p.Construcoes[(int)c]++;
            Missoes.Registrar(p, AcaoDeMissao.Melhorar, cfg);
            return true;
        }

        // ---- efeitos ----

        /// <summary>Cripta: +1 de Comando a cada N níveis.</summary>
        public static int ComandoExtra(EstadoProgresso p, ConfigJogo cfg) { return Nivel(p, Construcao.Cripta) / cfg.NiveisDeCriptaPorComando; }

        /// <summary>Torre das Almas: espaços extras no Filactério.</summary>
        public static int CapacidadeDoFilacterio(EstadoProgresso p, ConfigJogo cfg)
        {
            return cfg.CapacidadeFilacterio + cfg.EspacosPorNivelDeTorre * Nivel(p, Construcao.TorreDasAlmas);
        }

        /// <summary>Torre das Almas: aumento (somado aos outros "aumentos") da chance de alma.</summary>
        public static float BonusChanceDeAlma(EstadoProgresso p, ConfigJogo cfg) { return cfg.ChanceDeAlmaPorNivelDeTorre * Nivel(p, Construcao.TorreDasAlmas); }

        /// <summary>Ossuário: multiplicador dos Ossos trazidos de cada expedição.</summary>
        public static float MultOssos(EstadoProgresso p, ConfigJogo cfg) { return 1f + cfg.OssosPorNivelDeOssuario * Nivel(p, Construcao.Ossuario); }

        /// <summary>Mesa de Dissecação: Essência de desmanchar itens e dissolver almas, já com o bônus. Soma ao estado e devolve o ganho.</summary>
        public static long GanharEssencia(EstadoProgresso p, long valor, ConfigJogo cfg)
        {
            long ganho = (long)Math.Round(valor * (1.0 + cfg.EssenciaPorNivelDeMesa * Nivel(p, Construcao.MesaDeDissecacao)));
            p.Essencia += ganho;
            return ganho;
        }

        /// <summary>Essência para erguer um minion: cresce com o porte e a qualidade; o Altar reduz até um piso.</summary>
        public static long CustoDeErguer(EstadoProgresso p, Alma alma, ConfigJogo cfg)
        {
            var d = TabelaEspecies.Obter(alma.Especie);
            float baseC = cfg.EssenciaParaErguerPorPorte[(int)d.Porte] * cfg.MultQualidade[(int)alma.Qualidade];
            float desconto = Math.Min(cfg.DescontoMaximoDoAltar, cfg.DescontoPorNivelDeAltar * Nivel(p, Construcao.AltarDeErguimento));
            return (long)Math.Ceiling(baseC * (1f - desconto));
        }
    }
}
