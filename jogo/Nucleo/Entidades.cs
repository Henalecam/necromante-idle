namespace Necro.Nucleo
{
    /// <summary>Qualquer unidade viva na arena: necromante, minion ou inimigo. Estado legível pela camada visual.</summary>
    public sealed class Entidade
    {
        // ---- Identidade ----
        public int Id;
        public Faccao Faccao;
        public EspecieId Especie;
        public Papel Papel;
        public Porte Porte;
        public Familia Familia;
        public bool Elite;
        public bool Chefe;
        public ModificadorElite Modificadores;

        // ---- Estado visual ----
        public Vec2 Pos;
        public Vec2 Olhar;
        public EstadoAnimacao Animacao;
        public bool Viva = true;

        // ---- Atributos ----
        public float Vida;
        public float VidaMax;
        public float Mana;
        public float ManaMax;
        public float Dano;
        public float VelAtaque;
        public float Armadura;
        public float Alcance;
        public float VelMovimento;
        public TipoDano TipoDano;
        public readonly float[] Resistencias = new float[6];
        public bool Oculto; public float OcultoAte;   // Salões dos Cegos: invisível até atacar, ser ferido ou o tempo acabar
        public float TeleporteAte;   // Magistrado: instante (s de simulação) em que pode se teleportar de novo
        public int Imune;            // bits por TipoDano: dano desse tipo não entra (inimigos no Pesadelo e no Inferno)
        public float ImuneAviso;     // intervalo entre dois avisos de "Imune" do mesmo inimigo
        public float ChanceCritico;
        public float MultCritico;
        public float AumentoDano;       // soma dos "aumentos" (aditivos)
        public float MaisDano = 1f;     // produto dos "mais" permanentes (família, traço)
        public float RouboDeVida;
        public float FracaoFogo;        // Incandescente
        public bool Rancoroso;
        public bool Leal;
        public float Escudo;            // Escudo de Ossos restante

        // ---- Estado de combate ----
        public float Recarga;           // s até o próximo ataque
        public float TempoAcao;         // s presa na animação atual
        public float TempoMorrendo;
        public float FugaTempo, ComendoTempo; public int FugasFeitas, CadaverAlvo = -1; public bool JaComeu, Flanqueando;   // comportamento de espécie dos inimigos: fuga do diabrete, banquete do carniçal
        public bool Golem;                                         // o Golem conjurado pelo necromante (um só por vez)
        public bool Unico; public string NomeProprio;              // monstro único: nome próprio e séquito
        public bool DaHorda, Campeao;                              // inimigo comum de horda (mais fraco, rende menos) / campeão de pacote
        public bool Aparou; public int GolpesSofridos; public long UltimoLamento = -1;             // Esqueleto (minion): já aparou um golpe nesta sala
        public float SedeTempo;         // Cão de Cripta (minion): ataca mais rápido depois de matar
        public float FraquezaTempo;     // Maldição da Fraqueza
        public int ProvocadoPor = -1;
        public float ProvocadoTempo;
        public float TempoHabilidade;   // recarga da habilidade especial (chefe)
        public float PreparandoGolpe;   // > 0: elite/chefe armando o Golpe Brutal (aviso para o jogador interromper)
        public float RecargaGolpe = 5f; // s até poder armar o próximo

        // ---- Minion ----
        public Postura Postura;
        public Vec2 Posto;              // posição na formação
        public int IndiceMinion = -1;   // índice em EstadoProgresso.Minions
        public int Custo;
        /// <summary>Minion temporário (Grimório da Mãe Carniça): some quando TempoDeVida zera, não deixa cadáver e não volta por Reerguer.</summary>
        public bool Temporario;
        public float TempoDeVida;

        internal bool NaLista;
    }

    /// <summary>Cadáver na arena (recurso para feitiços e fonte de Restos).</summary>
    public sealed class Cadaver
    {
        public int Id;
        public int EntidadeId;
        public EspecieId Especie;
        public Vec2 Pos;
        public CondicaoCadaver Condicao;
        public float VidaMax;
        public bool DeMinion;
        public int Custo;
        internal Entidade Minion; // referência para Reerguer
    }

    /// <summary>Orbe de alma aguardando a ceifa.</summary>
    public sealed class OrbeDeAlma
    {
        public int Id;
        public Vec2 Pos;
        public Alma Alma;
        public float TempoRestante;
    }

    /// <summary>Poça de sangue invocada pela Abadessa; cura o chefe enquanto ele estiver dentro do raio.</summary>
    public sealed class PocaDeSangue
    {
        public int Id;
        public int DonoId;
        public Vec2 Pos;
        public float Raio;
        public float TempoRestante;
    }

    /// <summary>Projétil em voo (flecha de Atirador). Persegue o alvo; se o alvo morrer, segue até o último ponto e some.</summary>
    public sealed class Projetil
    {
        public int Id;
        public Entidade Origem;
        public Entidade Alvo;
        public EspecieId EspecieOrigem;
        public Vec2 Pos;
        public Vec2 Destino;
        public Vec2 Dir;
        public float Dano;
        public TipoDano TipoDano;
        // Perfurante (Lança de Ossos): voa em linha reta por "Restante" unidades e fere uma vez cada inimigo a até "Raio".
        public bool Perfurante;
        public float Restante;
        public float Raio;
        public float Velocidade;
        public readonly System.Collections.Generic.List<int> Atingidos = new System.Collections.Generic.List<int>(8);
    }
}
