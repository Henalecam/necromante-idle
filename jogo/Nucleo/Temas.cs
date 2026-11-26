namespace Necro.Nucleo
{
    /// <summary>Tema do andar, como no Diablo 1: muda a cada 4 andares e puxa o elenco para uma família.</summary>
    public enum TemaDeAndar { Catedral = 0, Catacumbas = 1, Cavernas = 2, Inferno = 3 }

    public static class Temas
    {
        public static readonly string[] Nomes = { "Catedral", "Catacumbas", "Cavernas", "Inferno" };

        public static TemaDeAndar Do(int andar) { return (TemaDeAndar)(((andar < 1 ? 1 : andar) - 1) / 4 % 4); }

        /// <summary>A família da casa em cada tema: mortos-vivos na Catedral, espíritos e aberrações nas Catacumbas, bestas nas Cavernas, demônios no Inferno.</summary>
        public static bool DaCasa(TemaDeAndar t, Familia f)
        {
            switch (t)
            {
                case TemaDeAndar.Catedral: return f == Familia.MortoVivo;
                case TemaDeAndar.Catacumbas: return f == Familia.Espirito || f == Familia.Aberracao;
                case TemaDeAndar.Cavernas: return f == Familia.Besta;
                default: return f == Familia.Demonio;
            }
        }

        /// <summary>Multiplicador do peso de sorteio de uma espécie neste andar.</summary>
        public static float Peso(ConfigJogo cfg, int andar, Familia f)
        {
            if (!cfg.TemasDeAndar) return 1f;
            return DaCasa(Do(andar), f) ? cfg.TemaAfinidade : cfg.TemaForaDeCasa;
        }
    }
}
