using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Livros de feitiço, como no Diablo 1: cada livro lido sobe um nível do feitiço. Ler exige Magia;
    /// o livro que ainda não dá para ler fica guardado e é lido sozinho quando a Magia chegar.
    /// </summary>
    public static class Grimorio
    {
        public const int Quantos = 6;

        private static void Garantir(EstadoProgresso p)
        {
            if (p.NiveisDeFeitico == null || p.NiveisDeFeitico.Length != Quantos) p.NiveisDeFeitico = new int[Quantos];
            if (p.LivrosGuardados == null || p.LivrosGuardados.Length != Quantos) p.LivrosGuardados = new int[Quantos];
        }

        /// <summary>Nível do feitiço: 1 de nascença + 1 por livro lido.</summary>
        public static int Nivel(EstadoProgresso p, FeiticoId id) { Garantir(p); return 1 + p.NiveisDeFeitico[(int)id]; }
        public static int Guardados(EstadoProgresso p, FeiticoId id) { Garantir(p); return p.LivrosGuardados[(int)id]; }

        /// <summary>Magia necessária para ler o livro que leva o feitiço a este nível.</summary>
        public static int MagiaExigida(int nivelAlvo, ConfigJogo cfg) { return cfg.LivroMagiaPorNivel * Math.Max(0, nivelAlvo - 1); }

        public static float FatorDePotencia(int nivel, ConfigJogo cfg) { return 1f + cfg.LivroPotenciaPorNivel * (nivel - 1); }
        public static float FatorDeMana(int nivel, ConfigJogo cfg) { return Math.Max(0.5f, 1f - cfg.LivroManaPorNivel * (nivel - 1)); }

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
