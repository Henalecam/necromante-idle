using System;

namespace Necro.Nucleo
{
    /// <summary>Os seis tipos de dano; cada um tem resistência própria.</summary>
    public enum TipoDano { Fisico = 0, Fogo = 1, Gelo = 2, Raio = 3, Veneno = 4, Sombra = 5 }

    /// <summary>Papel de combate, define a IA da unidade.</summary>
    public enum Papel { Tanque, Lutador, Atirador, Conjurador, Suporte, Enxame }

    /// <summary>Postura de um grupo de minions.</summary>
    public enum Postura { Agressiva, Defensiva, Escolta }

    /// <summary>Porte define o custo de Comando.</summary>
    public enum Porte { Pequeno = 0, Medio = 1, Grande = 2, Colossal = 3 }

    public enum Familia { MortoVivo = 0, Demonio = 1, Besta = 2, Aberracao = 3, Espirito = 4 }

    public enum Qualidade { Fraca = 0, Comum = 1, Vigorosa = 2, Ancestral = 3, Profana = 4 }

    public enum Afixo { Faminto = 0, Frenetico = 1, Petreo = 2, Rancoroso = 3, Leal = 4, Incandescente = 5 }

    /// <summary>Traço exclusivo de almas Profanas.</summary>
    public enum TracoProfano { Nenhum = 0, Colosso = 1, Carniceiro = 2, Agourento = 3 }

    /// <summary>Condição do cadáver, definida pelo tipo de dano do golpe final.</summary>
    public enum CondicaoCadaver { Intacto = 0, Mutilado = 1, Queimado = 2, Congelado = 3, Eletrizado = 4, Putrefato = 5, Oco = 6 }

    [Flags]
    public enum ModificadorElite { Nenhum = 0, Vampirico = 1, Explosivo = 2, Veloz = 4, EscudoDeOssos = 8 }

    /// <summary>Presságio: condição sorteada que vale por um andar.</summary>
    public enum Pressagio { Nenhum = 0, Horda = 1, Ossario = 2, AlmasInquietas = 3, SedeDeSangue = 4, CampeaoErrante = 5, Calmaria = 6 }

    public enum EstadoAnimacao { Parado, Andando, Atacando, Conjurando, Morrendo }

    public enum Faccao { Necromante, Minion, Inimigo }

    public enum EspecieId
    {
        Necromante = 0, Esqueleto = 1, ArqueiroOssudo = 2, Zumbi = 3, CaoDeCripta = 4,
        Diabrete = 5, AcolitoCaido = 6, AbadessaCosturada = 7,
        Carnical = 8, DiaboDeGuerra = 9, Aparicao = 10, ColossoDeOssos = 11,
        ResProfana = 12, CarcereiroDeAlmas = 13
    }

    public enum FeiticoId { LancaDeOssos = 0, DrenarVida = 1, ExplosaoDeCadaver = 2, Reerguer = 3, MaldicaoDaFraqueza = 4, Sacrificio = 5, Golem = 6, MuralhaDeFogo = 7 }

    public enum ModoExpedicao { Avancar, Cacar }

    public enum FaseExpedicao { Preparando, EntreOndas, EmOnda, EntreAndares, Encerrada, Explorando }

    public enum MotivoFim { EmAndamento, NecromanteMorreu, LimiteDeAndares, LimiteDeTempo, Retirada, Portal }

    /// <summary>Vetor 2D mínimo (sem dependência da Unity).</summary>
    [Serializable]
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y) { X = x; Y = y; }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator *(Vec2 a, float k) { return new Vec2(a.X * k, a.Y * k); }

        public float Comprimento { get { return MathF.Sqrt(X * X + Y * Y); } }

        public Vec2 Normalizado
        {
            get
            {
                float c = Comprimento;
                return c > 1e-6f ? new Vec2(X / c, Y / c) : new Vec2(0f, 0f);
            }
        }

        public static float Dist2(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public static float Dist(Vec2 a, Vec2 b) { return MathF.Sqrt(Dist2(a, b)); }

        public override string ToString() { return "(" + X.ToString("0.00") + "; " + Y.ToString("0.00") + ")"; }
    }
}
