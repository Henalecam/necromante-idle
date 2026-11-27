using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Números base de uma espécie. A mesma espécie existe como inimigo (base x fatores de inimigo x escala do andar)
    /// e como minion erguido (base x Qualidade x Nível x Poder Necromântico).
    /// </summary>
    [Serializable]
    public class DadosEspecie
    {
        public EspecieId Id;
        public string Nome;
        public Familia Familia;
        public Porte Porte;
        public Papel Papel;
        public float Vida;
        public float Dano;
        public float VelAtaque;     // ataques por segundo
        public float Armadura;
        public float Alcance;
        public float VelMovimento;  // unidades por segundo
        public TipoDano TipoDano;
        public float[] Resistencias = new float[6]; // por TipoDano, 0..1
        public bool Chefe;
        // Geração de ondas
        public int AndarMinimo = 1; // primeiro andar do Ato em que aparece
        public int AtoMinimo = 1;   // primeiro Ato em que aparece (a Súcubo só do Ato 2 em diante)
        public float PesoOnda;      // 0 = nunca sorteado em ondas
        public float CustoOnda = 1f;
    }

    /// <summary>Tabela de espécies em código (Ato 1). Trocar por ScriptableObjects depois é só preencher esta lista.</summary>
    public static class TabelaEspecies
    {
        private static DadosEspecie[] _porId = CriarPadrao();

        public static DadosEspecie Obter(EspecieId id) { return _porId[(int)id]; }

        public static DadosEspecie[] Todas { get { return _porId; } }

        /// <summary>Substitui a tabela inteira (ex.: dados vindos de ScriptableObjects). Índice = (int)EspecieId.</summary>
        /// <summary>Quantas espécies existem (índices 0..Total-1; 0 é o necromante).</summary>
        public static int Total { get { return _porId.Length; } }

        public static void Substituir(DadosEspecie[] tabela) { _porId = tabela; }

        public static void RestaurarPadrao() { _porId = CriarPadrao(); }

        private static float[] Res(float fisico, float fogo, float gelo, float raio, float veneno, float sombra)
        {
            return new[] { fisico, fogo, gelo, raio, veneno, sombra };
        }

        private static DadosEspecie[] CriarPadrao()
        {
            var t = new DadosEspecie[19];
            t[(int)EspecieId.Necromante] = new DadosEspecie
            {
                Id = EspecieId.Necromante, Nome = "Necromante", Familia = Familia.Espirito, Porte = Porte.Medio, Papel = Papel.Conjurador,
                Vida = 200f, Dano = 10f, VelAtaque = 1.0f, Armadura = 10f, Alcance = 9f, VelMovimento = 2.6f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0f, 0f, 0f, 0f, 0f, 0.2f)
            };
            t[(int)EspecieId.Esqueleto] = new DadosEspecie
            {
                Id = EspecieId.Esqueleto, Nome = "Esqueleto", Familia = Familia.MortoVivo, Porte = Porte.Pequeno, Papel = Papel.Lutador,
                Vida = 60f, Dano = 8f, VelAtaque = 1.0f, Armadura = 10f, Alcance = 1.0f, VelMovimento = 2.2f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0f, 0f, 0.2f, 0f, 0.3f, 0f), AndarMinimo = 1, PesoOnda = 10f, CustoOnda = 1f
            };
            t[(int)EspecieId.ArqueiroOssudo] = new DadosEspecie
            {
                Id = EspecieId.ArqueiroOssudo, Nome = "Arqueiro Ossudo", Familia = Familia.MortoVivo, Porte = Porte.Pequeno, Papel = Papel.Atirador,
                Vida = 40f, Dano = 7f, VelAtaque = 0.8f, Armadura = 0f, Alcance = 6.0f, VelMovimento = 2.0f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0f, 0f, 0.2f, 0f, 0.3f, 0f), AndarMinimo = 3, PesoOnda = 5f, CustoOnda = 1f
            };
            t[(int)EspecieId.Zumbi] = new DadosEspecie
            {
                Id = EspecieId.Zumbi, Nome = "Zumbi", Familia = Familia.MortoVivo, Porte = Porte.Pequeno, Papel = Papel.Tanque,
                Vida = 110f, Dano = 5f, VelAtaque = 0.7f, Armadura = 30f, Alcance = 1.0f, VelMovimento = 1.4f, TipoDano = TipoDano.Veneno,
                Resistencias = Res(0f, -0.2f, 0f, 0f, 0.5f, 0f), AndarMinimo = 1, PesoOnda = 5f, CustoOnda = 1f
            };
            t[(int)EspecieId.CaoDeCripta] = new DadosEspecie
            {
                Id = EspecieId.CaoDeCripta, Nome = "Cão de Cripta", Familia = Familia.Besta, Porte = Porte.Pequeno, Papel = Papel.Lutador,
                Vida = 50f, Dano = 9f, VelAtaque = 1.3f, Armadura = 5f, Alcance = 1.0f, VelMovimento = 3.2f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0f, 0f, 0f, 0f, 0f, 0.1f), AndarMinimo = 2, PesoOnda = 5f, CustoOnda = 1f
            };
            t[(int)EspecieId.Diabrete] = new DadosEspecie
            {
                Id = EspecieId.Diabrete, Nome = "Diabrete", Familia = Familia.Demonio, Porte = Porte.Pequeno, Papel = Papel.Enxame,
                Vida = 24f, Dano = 3.5f, VelAtaque = 1.6f, Armadura = 0f, Alcance = 1.0f, VelMovimento = 3.6f, TipoDano = TipoDano.Fogo,
                Resistencias = Res(0f, 0.5f, -0.2f, 0f, 0f, 0f), AndarMinimo = 4, PesoOnda = 6f, CustoOnda = 0.5f
            };
            t[(int)EspecieId.AcolitoCaido] = new DadosEspecie
            {
                Id = EspecieId.AcolitoCaido, Nome = "Acólito Caído", Familia = Familia.Aberracao, Porte = Porte.Medio, Papel = Papel.Conjurador,
                Vida = 90f, Dano = 12f, VelAtaque = 0.45f, Armadura = 5f, Alcance = 6.0f, VelMovimento = 1.8f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0f, 0f, 0f, 0f, 0f, 0.4f), AndarMinimo = 5, PesoOnda = 3f, CustoOnda = 3f
            };
            t[(int)EspecieId.AbadessaCosturada] = new DadosEspecie
            {
                Id = EspecieId.AbadessaCosturada, Nome = "A Abadessa Costurada", Familia = Familia.Aberracao, Porte = Porte.Colossal, Papel = Papel.Suporte,
                Vida = 700f, Dano = 24f, VelAtaque = 0.7f, Armadura = 60f, Alcance = 1.8f, VelMovimento = 1.5f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0.1f, 0f, 0f, 0f, 0.3f, 0.3f), Chefe = true, AndarMinimo = 10, PesoOnda = 0f, CustoOnda = 10f
            };
            // ---- segunda leva: mais variedade de ondas ----
            t[(int)EspecieId.Carnical] = new DadosEspecie
            {
                Id = EspecieId.Carnical, Nome = "Carniçal", Familia = Familia.MortoVivo, Porte = Porte.Pequeno, Papel = Papel.Lutador,
                Vida = 45f, Dano = 7f, VelAtaque = 1.7f, Armadura = 0f, Alcance = 1.0f, VelMovimento = 3.8f, TipoDano = TipoDano.Veneno,
                Resistencias = Res(0f, -0.2f, 0f, 0f, 0.4f, 0f), AndarMinimo = 3, PesoOnda = 5f, CustoOnda = 1f
            };
            t[(int)EspecieId.DiaboDeGuerra] = new DadosEspecie
            {
                Id = EspecieId.DiaboDeGuerra, Nome = "Diabo de Guerra", Familia = Familia.Demonio, Porte = Porte.Grande, Papel = Papel.Tanque,
                Vida = 260f, Dano = 14f, VelAtaque = 0.6f, Armadura = 25f, Alcance = 1.3f, VelMovimento = 1.6f, TipoDano = TipoDano.Fogo,
                Resistencias = Res(0.1f, 0.5f, -0.2f, 0f, 0f, 0f), AndarMinimo = 6, PesoOnda = 2f, CustoOnda = 4f
            };
            t[(int)EspecieId.Aparicao] = new DadosEspecie
            {
                Id = EspecieId.Aparicao, Nome = "Aparição", Familia = Familia.Espirito, Porte = Porte.Medio, Papel = Papel.Atirador,
                Vida = 55f, Dano = 11f, VelAtaque = 0.7f, Armadura = 0f, Alcance = 7.0f, VelMovimento = 2.4f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0.3f, 0f, 0f, 0f, 0f, 0.5f), AndarMinimo = 7, PesoOnda = 3f, CustoOnda = 2f
            };
            t[(int)EspecieId.ColossoDeOssos] = new DadosEspecie
            {
                Id = EspecieId.ColossoDeOssos, Nome = "Colosso de Ossos", Familia = Familia.MortoVivo, Porte = Porte.Grande, Papel = Papel.Lutador,
                Vida = 220f, Dano = 18f, VelAtaque = 0.55f, Armadura = 20f, Alcance = 1.5f, VelMovimento = 1.5f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0.1f, 0f, 0.2f, 0f, 0.3f, 0f), AndarMinimo = 8, PesoOnda = 2f, CustoOnda = 4f
            };
            // Só aparece no nível secreto (PesoOnda 0): fraca, numerosa e furiosa.
            t[(int)EspecieId.ResProfana] = new DadosEspecie
            {
                Id = EspecieId.ResProfana, Nome = "Rês Profana", Familia = Familia.Besta, Porte = Porte.Medio, Papel = Papel.Lutador,
                Vida = 60f, Dano = 6f, VelAtaque = 0.9f, Armadura = 5f, Alcance = 1.1f, VelMovimento = 2.6f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0.1f, 0f, 0f, 0f, 0f, 0f), AndarMinimo = 99, PesoOnda = 0f, CustoOnda = 1f
            };
            // Chefe do Ato 2: um espectro que acorrenta os teus minions e chama Aparições para o lado dele.
            t[(int)EspecieId.CarcereiroDeAlmas] = new DadosEspecie
            {
                Id = EspecieId.CarcereiroDeAlmas, Nome = "O Carcereiro de Almas", Familia = Familia.Espirito, Porte = Porte.Colossal, Papel = Papel.Lutador,
                Vida = 640f, Dano = 26f, VelAtaque = 0.75f, Armadura = 40f, Alcance = 2.0f, VelMovimento = 1.7f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0.2f, 0f, 0f, 0f, 0.2f, 0.5f), Chefe = true, AndarMinimo = 99, PesoOnda = 0f, CustoOnda = 10f
            };
            // Súcubo (Diablo 1): atiradora demoníaca dos andares fundos. Dispara de longe e recua para manter a distância.
            t[(int)EspecieId.Sucubo] = new DadosEspecie
            {
                Id = EspecieId.Sucubo, Nome = "Súcubo", Familia = Familia.Demonio, Porte = Porte.Medio, Papel = Papel.Atirador,
                Vida = 70f, Dano = 13f, VelAtaque = 0.8f, Armadura = 0f, Alcance = 7.5f, VelMovimento = 2.8f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0f, 0.3f, 0f, -0.2f, 0f, 0.3f), AtoMinimo = 2, AndarMinimo = 2, PesoOnda = 3f, CustoOnda = 2f
            };
            // Cavaleiro Negro (Diablo 1): couraça pesada. Quase não sente golpe físico: pede feitiço (raio, fogo) ou paciência.
            t[(int)EspecieId.CavaleiroNegro] = new DadosEspecie
            {
                Id = EspecieId.CavaleiroNegro, Nome = "Cavaleiro Negro", Familia = Familia.MortoVivo, Porte = Porte.Medio, Papel = Papel.Lutador,
                Vida = 170f, Dano = 20f, VelAtaque = 0.5f, Armadura = 80f, Alcance = 1.3f, VelMovimento = 1.5f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0.3f, 0f, 0f, -0.2f, 0f, 0.2f), AtoMinimo = 2, AndarMinimo = 5, PesoOnda = 2f, CustoOnda = 4f
            };
            // Magistrado (Diablo 1): feiticeiro que some num clarão quando alguém encosta e reaparece longe.
            t[(int)EspecieId.Magistrado] = new DadosEspecie
            {
                Id = EspecieId.Magistrado, Nome = "Magistrado", Familia = Familia.Aberracao, Porte = Porte.Medio, Papel = Papel.Atirador,
                Vida = 60f, Dano = 12f, VelAtaque = 0.6f, Armadura = 0f, Alcance = 7.0f, VelMovimento = 2.0f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0f, -0.2f, 0f, 0f, 0f, 0.4f), AtoMinimo = 2, AndarMinimo = 3, PesoOnda = 2.5f, CustoOnda = 2f
            };
            // Chefe do Ato 3 (Diablo 1): o Arcebispo Lázarus. Acende muralhas de fogo sob o grupo, some quando o cercam e chama Súcubos.
            t[(int)EspecieId.Lazarus] = new DadosEspecie
            {
                Id = EspecieId.Lazarus, Nome = "Arcebispo Lázarus", Familia = Familia.Aberracao, Porte = Porte.Colossal, Papel = Papel.Atirador,
                Vida = 560f, Dano = 22f, VelAtaque = 0.7f, Armadura = 20f, Alcance = 8.0f, VelMovimento = 1.8f, TipoDano = TipoDano.Sombra,
                Resistencias = Res(0.1f, 0.5f, 0f, -0.1f, 0.2f, 0.4f), Chefe = true, AndarMinimo = 99, PesoOnda = 0f, CustoOnda = 10f
            };
            // Chefe do Ato 4 e fim da campanha (Diablo 1): o Senhor do Terror. Corpo a corpo brutal e o Apocalipse, um anel de fogo que fere todos em volta.
            t[(int)EspecieId.Diablo] = new DadosEspecie
            {
                Id = EspecieId.Diablo, Nome = "Diablo, o Senhor do Terror", Familia = Familia.Demonio, Porte = Porte.Colossal, Papel = Papel.Lutador,
                Vida = 900f, Dano = 30f, VelAtaque = 0.8f, Armadura = 50f, Alcance = 2.2f, VelMovimento = 1.9f, TipoDano = TipoDano.Fisico,
                Resistencias = Res(0.2f, 0.6f, -0.1f, 0.2f, 0.2f, 0.3f), Chefe = true, AndarMinimo = 99, PesoOnda = 0f, CustoOnda = 10f
            };
            return t;
        }

        /// <summary>Chefe de um Ato. Por enquanto só existe o conteúdo do Ato 1; os demais reutilizam a Abadessa.</summary>
        /// <summary>Abadessa no Ato 1, Carcereiro no 2, Lázarus no 3, Diablo no 4; daí em diante o ciclo se repete.</summary>
        public static EspecieId ChefeDoAto(int ato) { int k = ((ato < 1 ? 1 : ato) - 1) % 4; return k == 0 ? EspecieId.AbadessaCosturada : k == 1 ? EspecieId.CarcereiroDeAlmas : k == 2 ? EspecieId.Lazarus : EspecieId.Diablo; }
    }
}
