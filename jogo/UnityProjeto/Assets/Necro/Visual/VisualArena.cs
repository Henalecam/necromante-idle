using System.Collections.Generic;
using UnityEngine;
using Necro.Nucleo;

namespace Necro.Visual
{
    /// <summary>
    /// Ponte entre a simulação (C# puro) e a tela: roda a simulação em passo fixo de 10 ticks/s,
    /// cria/move os sprites das entidades e transforma os eventos em gore.
    /// Monte uma cena com: Camera ortográfica, este componente, um GoreMestre e um ChaoDeSangue.
    /// Os DadosSprites ficam em Resources/Sprites/&lt;especie&gt;/&lt;especie&gt;_dados (criados pelo ImportadorSprites).
    /// O componente AutoCena monta tudo isso sozinho numa cena vazia.
    /// </summary>
    public class VisualArena : MonoBehaviour
    {
        [Header("Simulação")]
        public ulong semente = 1003;
        public int andarInicial = 1;
        [Range(0.25f, 8f)] public float velocidade = 1f;       // 1x, 2x, 4x
        [Header("Escala (unidades da simulação -> mundo da Unity)")]
        public float escalaX = 1.75f;       // 56 px por unidade / 32 px por unidade da Unity
        public float escalaY = 0.875f;      // metade: vista isométrica
        [Header("Sprites")]
        public List<DadosSprites> sprites = new List<DadosSprites>();
        public Sprite spriteOrbe;

        public Simulacao Sim { get; private set; }
        public EstadoProgresso Progresso { get; private set; }

        ConfigJogo _cfg;
        float _acumulado;
        readonly Dictionary<int, AnimadorSprite8> _vistos = new Dictionary<int, AnimadorSprite8>();
        readonly Dictionary<int, Vector2> _alvos = new Dictionary<int, Vector2>();
        readonly Dictionary<int, GameObject> _orbes = new Dictionary<int, GameObject>();
        readonly Dictionary<int, GameObject> _projeteis = new Dictionary<int, GameObject>();
        Sprite _spriteFlecha;
        readonly Dictionary<int, int> _cadaverParaEntidade = new Dictionary<int, int>();
        readonly Dictionary<string, DadosSprites> _porNome = new Dictionary<string, DadosSprites>();
        readonly List<int> _remover = new List<int>();

        void Start()
        {
            Application.targetFrameRate = 60;
            foreach (var d in sprites) if (d != null) _porNome[d.nomeMonstro] = d;
            if (spriteOrbe == null) spriteOrbe = CriarOrbe();
            _cfg = new ConfigJogo();
            Progresso = CarregarOuCriar();
            NovaExpedicao();
        }

        EstadoProgresso CarregarOuCriar()
        {
            string json = PlayerPrefs.GetString("necro_progresso", "");
            if (!string.IsNullOrEmpty(json))
            {
                var salvo = JsonUtility.FromJson<EstadoProgresso>(json);
                if (salvo != null && salvo.Minions != null && salvo.Minions.Count > 0) return salvo;
            }
            var p = new EstadoProgresso();
            for (int i = 0; i < 3; i++)
                p.Minions.Add(new MinionSalvo { Alma = new Alma { Especie = EspecieId.Esqueleto, Qualidade = Qualidade.Comum }, Nivel = 1, Ativo = true });
            return p;
        }

        public void NovaExpedicao()
        {
            foreach (var kv in _vistos) if (kv.Value != null) Destroy(kv.Value.gameObject);
            foreach (var kv in _orbes) if (kv.Value != null) Destroy(kv.Value);
            foreach (var kv in _projeteis) if (kv.Value != null) Destroy(kv.Value);
            _projeteis.Clear();
            _vistos.Clear(); _alvos.Clear(); _orbes.Clear(); _cadaverParaEntidade.Clear();
            if (GoreMestre.Instancia != null && GoreMestre.Instancia.chao != null) GoreMestre.Instancia.chao.Limpar();
            Sim = new Simulacao(_cfg, Progresso, semente + Progresso.ContadorDeSementes++, ModoExpedicao.Avancar,
                                Mathf.Max(andarInicial, Progressao.AndarDePartida(Progresso, _cfg)));
        }

        public Vector2 ParaMundo(Vec2 p)
        {
            return new Vector2((p.X - _cfg.LarguraArena * 0.5f) * escalaX, (p.Y - _cfg.AlturaArena * 0.5f) * escalaY);
        }

        void Update()
        {
            if (Sim == null) return;
            if (Sim.Encerrada)
            {
                Progressao.AplicarResultado(Progresso, Sim.Resultado, _cfg);
                PlayerPrefs.SetString("necro_progresso", JsonUtility.ToJson(Progresso));
                PlayerPrefs.Save();
                NovaExpedicao();
                return;
            }
            _acumulado += Time.deltaTime * velocidade;
            float dt = _cfg.DT;
            int passos = 0;
            while (_acumulado >= dt && passos < 40)
            {
                _acumulado -= dt; passos++;
                Sim.Tick();
                ProcessarEventos();      // a lista de eventos é limpa a cada tick
            }
            Sincronizar(dt);
        }

        void ProcessarEventos()
        {
            var evs = Sim.Eventos;
            for (int i = 0; i < evs.Count; i++)
            {
                var ev = evs[i];
                switch (ev.Tipo)
                {
                    case TipoEvento.Dano:
                        if (GoreMestre.Instancia != null && ev.Valor > 0f)
                            GoreMestre.Instancia.Jorrar(ParaMundo(ev.Pos) + Vector2.up * 0.9f, Random.insideUnitCircle, ev.Critico ? 10 : 4, 3f);
                        break;
                    case TipoEvento.Morte:
                        AnimadorSprite8 a;
                        if (_vistos.TryGetValue(ev.Alvo, out a) && a != null)
                        {
                            var dir = new Vector2(ev.Dir.X, ev.Dir.Y * 0.5f);
                            if (ev.Overkill) GoreMestre.Matar(a.GetComponent<SpriteRenderer>(), TipoMorte.Explosao, dir, true);
                            else if (ev.TipoDano == TipoDano.Fisico) { a.Tocar("morrer", true); if (GoreMestre.Instancia != null) GoreMestre.Instancia.Jorrar(ParaMundo(ev.Pos) + Vector2.up, dir, 30, 5f); }
                            else GoreMestre.Matar(a.GetComponent<SpriteRenderer>(), MorteDe(ev.TipoDano), dir);
                        }
                        break;
                    case TipoEvento.CadaverCriado:
                        _cadaverParaEntidade[ev.Alvo] = ev.Origem;
                        break;
                    case TipoEvento.CadaverConsumido:
                    case TipoEvento.CadaverRemovido:
                        int ent;
                        if (_cadaverParaEntidade.TryGetValue(ev.Alvo, out ent))
                        {
                            AnimadorSprite8 c;
                            if (_vistos.TryGetValue(ent, out c) && c != null)
                            {
                                if (ev.Tipo == TipoEvento.CadaverConsumido) GoreMestre.Matar(c.GetComponent<SpriteRenderer>(), TipoMorte.Explosao, Vector2.up, true);
                                Destroy(c.gameObject, ev.Tipo == TipoEvento.CadaverConsumido ? 0.05f : 0f);
                            }
                            _vistos.Remove(ent); _alvos.Remove(ent); _cadaverParaEntidade.Remove(ev.Alvo);
                        }
                        break;
                    case TipoEvento.AndarConcluido:
                        if (GoreMestre.Instancia != null && GoreMestre.Instancia.chao != null) GoreMestre.Instancia.chao.Limpar();
                        break;
                }
            }
        }

        static Sprite CriarOrbe()
        {
            const int n = 24;
            var t = new Texture2D(n, n) { filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - 11.5f) * (x - 11.5f) + (y - 11.5f) * (y - 11.5f)) / 11.5f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, d < 0.45f ? 1f : Mathf.Clamp01(1f - d) * 0.8f));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f);
        }

        static TipoMorte MorteDe(TipoDano t)
        {
            switch (t)
            {
                case TipoDano.Fogo: return TipoMorte.Fogo;
                case TipoDano.Gelo: return TipoMorte.Gelo;
                case TipoDano.Raio: return TipoMorte.Raio;
                case TipoDano.Veneno: return TipoMorte.Veneno;
                case TipoDano.Sombra: return TipoMorte.Sombra;
                default: return TipoMorte.Corte;
            }
        }

        void Sincronizar(float dt)
        {
            var ents = Sim.Entidades;
            for (int i = 0; i < ents.Count; i++)
            {
                var e = ents[i];
                AnimadorSprite8 a;
                if (!_vistos.TryGetValue(e.Id, out a))
                {
                    if (!e.Viva) continue;
                    a = Criar(e);
                    _vistos[e.Id] = a;
                }
                if (a == null || !a.enabled) continue;      // desativado = o gore assumiu o sprite
                _alvos[e.Id] = ParaMundo(e.Pos);
                a.Olhar(new Vector2(e.Olhar.X, e.Olhar.Y));
                if (e.Viva) a.Tocar(NomeDaAcao(e.Animacao, a.dados));
            }
            // interpola a posição entre ticks para o movimento ficar suave
            float k = Mathf.Clamp01(Time.deltaTime * 14f * Mathf.Max(1f, velocidade));
            foreach (var kv in _alvos)
            {
                AnimadorSprite8 a;
                if (_vistos.TryGetValue(kv.Key, out a) && a != null && a.enabled)
                    a.transform.position = Vector2.Lerp(a.transform.position, kv.Value, k);
            }
            // orbes de alma
            var orbes = Sim.Orbes;
            _remover.Clear();
            foreach (var kv in _orbes) _remover.Add(kv.Key);
            for (int i = 0; i < orbes.Count; i++)
            {
                _remover.Remove(orbes[i].Id);
                GameObject go;
                if (!_orbes.TryGetValue(orbes[i].Id, out go))
                {
                    go = new GameObject("Orbe");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = spriteOrbe; sr.color = new Color(0.45f, 1f, 0.65f, 1f); sr.sortingOrder = 29000;
                    _orbes[orbes[i].Id] = go;
                }
                go.transform.position = ParaMundo(orbes[i].Pos) + Vector2.up * (0.9f + 0.15f * Mathf.Sin(Time.time * 4f));
            }
            for (int i = 0; i < _remover.Count; i++) { Destroy(_orbes[_remover[i]]); _orbes.Remove(_remover[i]); }

            // flechas em voo
            var projs = Sim.Projeteis;
            _remover.Clear();
            foreach (var kv in _projeteis) _remover.Add(kv.Key);
            for (int i = 0; i < projs.Count; i++)
            {
                _remover.Remove(projs[i].Id);
                GameObject go;
                if (!_projeteis.TryGetValue(projs[i].Id, out go))
                {
                    if (_spriteFlecha == null)
                    {
                        var t = new Texture2D(14, 2) { filterMode = FilterMode.Point };
                        for (int x = 0; x < 14; x++) for (int y = 0; y < 2; y++) t.SetPixel(x, y, x > 10 ? new Color(0.6f, 0.6f, 0.65f, 1f) : new Color(0.88f, 0.82f, 0.66f, 1f));
                        t.Apply();
                        _spriteFlecha = Sprite.Create(t, new Rect(0, 0, 14, 2), new Vector2(0.5f, 0.5f), 32f);
                    }
                    go = new GameObject("Flecha");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _spriteFlecha; sr.sortingOrder = 29500;
                    if (projs[i].Perfurante) { go.transform.localScale = new Vector3(3.5f, 2f, 1f); sr.color = new Color(0.8f, 1f, 0.85f, 1f); }   // Lança de Ossos
                    _projeteis[projs[i].Id] = go;
                }
                go.transform.position = ParaMundo(projs[i].Pos) + Vector2.up * 1.6f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(projs[i].Dir.Y * 0.5f, projs[i].Dir.X) * Mathf.Rad2Deg);
            }
            for (int i = 0; i < _remover.Count; i++) { Destroy(_projeteis[_remover[i]]); _projeteis.Remove(_remover[i]); }
        }

        AnimadorSprite8 Criar(Entidade e)
        {
            var go = new GameObject(e.Especie + "_" + e.Id);
            go.transform.SetParent(transform);
            go.transform.position = ParaMundo(e.Pos);
            var sr = go.AddComponent<SpriteRenderer>();
            var a = go.AddComponent<AnimadorSprite8>();
            a.dados = Dados(e.Especie, e.Elite);
            a.Tocar("parado", true);
            return a;
        }

        DadosSprites Dados(EspecieId especie, bool elite)
        {
            string nome = especie.ToString().ToLowerInvariant();
            if (elite)
            {
                // elites usam o sprite "<especie>_elite" quando existir; senão, o sprite comum
                var de = Dados(nome + "_elite");
                if (de != null) return de;
            }
            return Dados(nome);
        }

        DadosSprites Dados(string nome)
        {
            DadosSprites d;
            if (_porNome.TryGetValue(nome, out d)) return d;
            d = Resources.Load<DadosSprites>("Sprites/" + nome + "/" + nome + "_dados");
            _porNome[nome] = d;
            return d;
        }

        static string NomeDaAcao(EstadoAnimacao s, DadosSprites d)
        {
            switch (s)
            {
                case EstadoAnimacao.Andando: return "andar";
                case EstadoAnimacao.Atacando: return Tem(d, "atacar") ? "atacar" : "conjurar";
                case EstadoAnimacao.Conjurando: return Tem(d, "conjurar") ? "conjurar" : "atacar";
                case EstadoAnimacao.Morrendo: return "morrer";
                default: return "parado";
            }
        }

        static bool Tem(DadosSprites d, string nome)
        {
            if (d == null) return false;
            for (int i = 0; i < d.acoes.Count; i++) if (d.acoes[i].nome == nome) return true;
            return false;
        }

        public ConfigJogo Config { get { return _cfg; } }

        /// <summary>Salva o progresso (a Interface chama depois de comprar, erguer ou equipar).</summary>
        public void Salvar()
        {
            PlayerPrefs.SetString("necro_progresso", JsonUtility.ToJson(Progresso));
            PlayerPrefs.Save();
        }
    }
}
