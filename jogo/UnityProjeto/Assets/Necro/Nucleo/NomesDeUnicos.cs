namespace Necro.Nucleo
{
    /// <summary>Nomes próprios dos monstros únicos, no espírito dos do Diablo: um nome composto e uma alcunha.</summary>
    public static class NomesDeUnicos
    {
        static readonly string[] A = { "Racha", "Rói", "Sangra", "Mói", "Quebra", "Fura", "Esfola", "Torce", "Lasca", "Suga", "Rasga", "Esmaga" };
        static readonly string[] B = { "Ossos", "Crânios", "Tripas", "Almas", "Dentes", "Costelas", "Carne", "Olhos", "Medula", "Espinhas", "Unhas", "Vértebras" };
        static readonly string[] C = { "o Faminto", "o Sem-Sono", "o Imundo", "o Esfolado", "o Profanador", "o Coveiro", "o Caolho", "o Insaciável", "o Podre", "o Cruel", "o Antigo", "o Maldito", "o Carniceiro", "o Rastejante" };

        public static string Sortear(Rng rng)
        {
            return A[rng.Intervalo(0, A.Length)] + "-" + B[rng.Intervalo(0, B.Length)] + ", " + C[rng.Intervalo(0, C.Length)];
        }
    }
}
