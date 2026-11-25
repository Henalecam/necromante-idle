namespace Necro.Nucleo
{
    /// <summary>
    /// Gerador pseudoaleatório determinístico (xorshift64*). Mesma semente = mesma sequência,
    /// em qualquer plataforma (usa apenas aritmética inteira).
    /// </summary>
    public sealed class Rng
    {
        private ulong _estado;

        public Rng(ulong semente)
        {
            // Mistura a semente (splitmix64) para que sementes vizinhas (1, 2, 3...) não gerem sequências parecidas.
            ulong z = semente + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            _estado = z == 0UL ? 0x2545F4914F6CDD1DUL : z;
        }

        /// <summary>Estado interno (para salvar/restaurar e para o hash de determinismo).</summary>
        public ulong Estado { get { return _estado; } set { _estado = value == 0UL ? 1UL : value; } }

        public ulong ProximoULong()
        {
            ulong x = _estado;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _estado = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Float uniforme em [0, 1).</summary>
        public float Proximo01()
        {
            return (ProximoULong() >> 40) * (1f / 16777216f);
        }

        /// <summary>Inteiro uniforme em [min, maxExclusivo).</summary>
        public int Intervalo(int min, int maxExclusivo)
        {
            if (maxExclusivo <= min) return min;
            ulong faixa = (ulong)(maxExclusivo - min);
            return min + (int)((ProximoULong() >> 11) % faixa);
        }

        public float Intervalo(float min, float max) { return min + (max - min) * Proximo01(); }

        public bool Chance(float probabilidade) { return Proximo01() < probabilidade; }
    }
}
