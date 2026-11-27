using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Livros de feitiço, como no Diablo 1: cada livro lido sobe um nível do feitiço. Ler exige Magia;
    /// o livro que ainda não dá para ler fica guardado e é lido sozinho quando a Magia chegar.
    /// </summary>
    public static class Grimorio
    {
        public const int Quantos = 7;

        private static void Garantir(EstadoProgresso p)
        {
            p.NiveisDeFeitico = Crescer(p.NiveisDeFeitico); p.LivrosGuardados = Crescer(p.LivrosGuardados);
        }

        /// <summary>Saves antigos têm menos feitiços: o vetor cresce sem perder os níveis já ganhos.</summary>
        private static int[] Crescer(int[] v)
        {
            if (v != null && v.Length == Quantos) return v;
            var n = new int[Quantos];
            if (v != null) for (int i = 0; i < v.Length && i < Quantos; i++) n[i] = v[i];
            return n;
        }

        /// <summary>Nível do feitiço: 1 de nascença + 1 por livro lido.</summary>
        public static int Nivel(EstadoProgresso p, FeiticoId id) { Garantir(p); return (id == FeiticoId.Golem ? 0 : 1) + p.NiveisDeFeitico[(int)id]; }   // o Golem não é de nascença: nível 0 até achar o primeiro livro
        public static int Guardados(EstadoProgresso p, FeiticoId id) { Garantir(p); return p.LivrosGuardados[(int)id]; }

        /// <summary>Magia necessária para ler o livro que leva o feitiço a este nível.</summary>
        public static int MagiaExigida(int nivelAlvo, ConfigJogo cfg) { return cfg.LivroMagiaPorNivel * Math.Max(0, nivelAlvo - 1); }

        public static float FatorDePotencia(int nivel, ConfigJogo cfg) { return 1f + cfg.LivroPotenciaPorNivel * Math.Max(0, nivel - 1); }
        public static float FatorDeMana(int nivel, ConfigJogo cfg) { return Math.Max(0.5f, 1f - cfg.LivroManaPorNivel * Math.Max(0, nivel - 1)); }

        /// <summary>Recebe um livro. true = lido agora (nível subiu); false = ficou guardado (falta Magia) ou virou ouro (nível máximo).</summary>
        public static bool Receber(EstadoProgresso p, FeiticoId id, ConfigJogo cfg)
        {
            Garantir(p);
            if (Nivel(p, id) + p.LivrosGuardados[(int)id] >= cfg.LivroNivelMaximo) { p.Ouro += cfg.LivroRepetidoOuro; return false; }
            p.LivrosGuardados[(int)id]++;
            return LerGuardados(p, cfg) > 0 && p.LivrosGuardados[(int)id] == 0;
        }

        /// <summary>Lê todos os livros guardados que a Magia atual permite. Devolve quantos.</summary>
        public static int LerGuardados(EstadoProgresso p, ConfigJogo cfg)
        {
            Garantir(p); int lidos = 0, magia = Atributos.Valor(p, Atributo.Magia);
            for (int i = 0; i < Quantos; i++)
                while (p.LivrosGuardados[i] > 0 && Nivel(p, (FeiticoId)i) < cfg.LivroNivelMaximo && magia >= MagiaExigida(Nivel(p, (FeiticoId)i) + 1, cfg))
                { p.LivrosGuardados[i]--; p.NiveisDeFeitico[i]++; p.LivrosLidos++; lidos++; }
            return lidos;
        }
    }
}
