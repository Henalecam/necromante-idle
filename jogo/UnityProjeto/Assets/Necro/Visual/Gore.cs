using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necro.Visual
{
    public enum TipoMorte { Corte, Explosao, Fogo, Gelo, Raio, Veneno, Sombra }

    /// <summary>
    /// Efeitos de morte aplicados em tempo real sobre o sprite atual do monstro: funcionam para
    /// qualquer espécie sem arte extra. Coloque um GoreMestre na cena e chame GoreMestre.Matar(...).
    /// </summary>
    public class GoreMestre : MonoBehaviour
    {
        public static GoreMestre Instancia { get; private set; }

        [Header("Recursos")]
        public Shader shaderGore;                 // Necro/SpriteGore
        public Texture2D ruido;                   // gerado em código se vazio
        public ChaoDeSangue chao;
        [Header("Ajustes")]
        public float gravidade = 14f;
        [Range(0f, 1f)] public float intensidade = 1f;   // opção "intensidade do gore" das configurações
        public Color corSangue = new Color(0.33f, 0.01f, 0.02f, 1f);

        readonly List<Pedaco> _pedacos = new List<Pedaco>();
        Sprite _pixel;

        class Pedaco
        {
            public Transform t; public SpriteRenderer sr; public Vector2 vel; public float altura, velAltura, giro, vida;
            public Vector2 chaoPos; public bool sangra; public Color cor; public bool some;
        }

        void Awake()
        {
            Instancia = this;
            if (shaderGore == null) shaderGore = Shader.Find("Necro/SpriteGore");
            if (ruido == null) ruido = GerarRuido(64);
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
        }

        public static void Matar(SpriteRenderer alvo, TipoMorte tipo, Vector2 direcaoDoGolpe, bool overkill = false)
        {
            if (Instancia != null) Instancia.StartCoroutine(Instancia.Rotina(alvo, tipo, direcaoDoGolpe.normalized, overkill));
        }

        IEnumerator Rotina(SpriteRenderer alvo, TipoMorte tipo, Vector2 dir, bool overkill)
        {
            if (alvo == null || alvo.sprite == null) yield break;
            var anim = alvo.GetComponent<AnimadorSprite8>();
            if (anim != null) anim.enabled = false;       // congela no quadro atual
            if (dir == Vector2.zero) dir = Vector2.right;
            Vector2 pes = alvo.transform.position;

            if (overkill || tipo == TipoMorte.Explosao)
            {
                Jorrar(pes, dir, 60, 7f);
                Estilhacar(alvo, dir, 5, 4, 7f, default, true);
                alvo.enabled = false;
                yield break;
            }

            var mat = NovoMaterial(alvo);
            switch (tipo)
            {
                case TipoMorte.Corte:
                {
                    // duas metades separadas por uma linha perpendicular ao golpe
                    Vector2 n = new Vector2(-dir.y, dir.x);
                    if (n.y < 0) n = -n;
                    float altura = Random.Range(0.42f, 0.62f);
                    var cima = Clonar(alvo, mat); var baixo = alvo; baixo.sharedMaterial = mat;
                    var mb = new MaterialPropertyBlock();
                    baixo.GetPropertyBlock(mb); mb.SetVector("_CorteNormal", new Vector4(-n.x, -n.y, 1f - altura, 1f)); baixo.SetPropertyBlock(mb);
                    cima.GetPropertyBlock(mb); mb.SetVector("_CorteNormal", new Vector4(n.x, n.y, altura, 1f)); cima.SetPropertyBlock(mb);
                    Jorrar(pes + Vector2.up * 0.6f, dir, 40, 5f);
                    _pedacos.Add(new Pedaco { t = cima.transform, sr = cima, vel = dir * 2.2f + n * 0.5f, altura = 0.01f, velAltura = 4.5f,
                                              giro = Random.Range(-260f, 260f), vida = 6f, chaoPos = pes, sangra = true });
                    yield return Tombar(baixo, -dir, 0.45f);
                    break;
                }
                case TipoMorte.Fogo:
                case TipoMorte.Sombra:
                {
                    var mb = new MaterialPropertyBlock();
                    Color brasa = tipo == TipoMorte.Fogo ? new Color(1f, 0.45f, 0.05f) : new Color(0.5f, 0.2f, 1f);
                    for (float t = 0; t < 1f; t += Time.deltaTime / 1.1f)
                    {
                        alvo.GetPropertyBlock(mb);
                        mb.SetFloat("_Dissolver", t); mb.SetColor("_CorBrasa", brasa);
                        alvo.SetPropertyBlock(mb);
                        if (tipo == TipoMorte.Fogo && Random.value < 0.5f) Particula(pes + Random.insideUnitCircle * 0.3f + Vector2.up * Random.value, Vector2.up * 1.5f, new Color(0.1f, 0.1f, 0.1f, 0.6f), 0.8f, false);
                        yield return null;
                    }
                    if (chao != null) chao.Carimbar(pes, 0.7f, new Color(0.03f, 0.02f, 0.02f, 0.85f));
                    alvo.enabled = false;
                    break;
                }
                case TipoMorte.Gelo:
                {
                    var mb = new MaterialPropertyBlock();
                    for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
                    {
                        alvo.GetPropertyBlock(mb); mb.SetColor("_Tinta", new Color(0.55f, 0.8f, 1f, 0.75f * t)); alvo.SetPropertyBlock(mb);
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.25f);
                    Estilhacar(alvo, dir, 4, 4, 4.5f, new Color(0.55f, 0.8f, 1f, 0.75f), false);
                    alvo.enabled = false;
                    break;
                }
                case TipoMorte.Raio:
                {
                    var mb = new MaterialPropertyBlock();
                    for (int i = 0; i < 6; i++)
                    {
                        alvo.GetPropertyBlock(mb); mb.SetFloat("_Clarao", i % 2 == 0 ? 1f : 0f); alvo.SetPropertyBlock(mb);
                        yield return new WaitForSeconds(0.05f);
                    }
                    Jorrar(pes + Vector2.up * 0.5f, dir, 35, 6f);
                    Estilhacar(alvo, dir, 3, 3, 6f, new Color(0.05f, 0.04f, 0.04f, 0.7f), true);
                    alvo.enabled = false;
                    break;
                }
                case TipoMorte.Veneno:
                {
                    var mb = new MaterialPropertyBlock();
                    for (float t = 0; t < 1f; t += Time.deltaTime / 1.3f)
                    {
                        alvo.GetPropertyBlock(mb);
                        mb.SetFloat("_Derreter", t * t); mb.SetColor("_Tinta", new Color(0.3f, 0.55f, 0.1f, 0.6f * t));
                        alvo.SetPropertyBlock(mb);
                        yield return null;
                    }
                    if (chao != null) chao.Carimbar(pes, 0.9f, new Color(0.18f, 0.3f, 0.05f, 0.9f));
                    alvo.enabled = false;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ peças

        Material NovoMaterial(SpriteRenderer sr)
        {
            var m = new Material(shaderGore);
            m.SetTexture("_Ruido", ruido);
            sr.sharedMaterial = m;
            DefinirRect(sr);
            return m;
        }

        static void DefinirRect(SpriteRenderer sr)
        {
            var s = sr.sprite; var tr = s.textureRect; var tex = s.texture;
            var mb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mb);
            mb.SetVector("_RectSprite", new Vector4(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            sr.SetPropertyBlock(mb);
        }

        SpriteRenderer Clonar(SpriteRenderer original, Material mat)
        {
            var go = new GameObject("Pedaco");
            go.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            go.transform.localScale = original.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = original.sprite; sr.flipX = original.flipX; sr.color = original.color;
            sr.sortingLayerID = original.sortingLayerID; sr.sortingOrder = original.sortingOrder + 1;
            sr.sharedMaterial = mat;
            DefinirRect(sr);
            return sr;
        }

        void Estilhacar(SpriteRenderer alvo, Vector2 dir, int colunas, int linhas, float forca, Color tinta, bool sangra)
        {
            var mat = NovoMaterial(alvo);
            var b = alvo.sprite.bounds;
            for (int x = 0; x < colunas; x++)
                for (int y = 0; y < linhas; y++)
                {
                    var rec = new Vector4((float)x / colunas, (float)y / linhas, (x + 1f) / colunas, (y + 1f) / linhas);
                    if (!TemPixels(alvo.sprite, rec)) continue;
                    var p = Clonar(alvo, mat);
                    var mb = new MaterialPropertyBlock();
                    p.GetPropertyBlock(mb); mb.SetVector("_Recorte", rec); mb.SetColor("_Tinta", tinta); p.SetPropertyBlock(mb);
                    Vector2 centro = new Vector2(Mathf.Lerp(b.min.x, b.max.x, (x + 0.5f) / colunas), Mathf.Lerp(b.min.y, b.max.y, (y + 0.5f) / linhas));
                    Vector2 fuga = (centro - (Vector2)b.center).normalized + dir * 0.8f + Random.insideUnitCircle * 0.5f;
                    _pedacos.Add(new Pedaco { t = p.transform, sr = p, vel = fuga * forca * Random.Range(0.4f, 1f) * new Vector2(1f, 0.5f),
                                              altura = Mathf.Max(0.01f, centro.y - b.min.y), velAltura = Random.Range(2f, 6f) * forca / 5f,
                                              giro = Random.Range(-500f, 500f), vida = Random.Range(4f, 7f),
                                              chaoPos = alvo.transform.position, sangra = sangra });
                }
        }

        static bool TemPixels(Sprite s, Vector4 rec)
        {
            if (!s.texture.isReadable) return true;
            var r = s.textureRect;
            int x0 = (int)(r.x + rec.x * r.width), x1 = (int)(r.x + rec.z * r.width);
            int y0 = (int)(r.y + rec.y * r.height), y1 = (int)(r.y + rec.w * r.height);
            for (int y = y0; y < y1; y += 2)
                for (int x = x0; x < x1; x += 2)
                    if (s.texture.GetPixel(x, y).a > 0.5f) return true;
            return false;
        }

        IEnumerator Tombar(SpriteRenderer sr, Vector2 lado, float duracao)
        {
            float alvo = lado.x >= 0 ? -88f : 88f;
            for (float t = 0; t < 1f; t += Time.deltaTime / duracao)
            {
                sr.transform.rotation = Quaternion.Euler(0, 0, alvo * t * t);
                yield return null;
            }
            if (chao != null) chao.Carimbar(sr.transform.position, 0.8f, corSangue);
        }

        // ------------------------------------------------------------------ sangue

        public void Jorrar(Vector2 origem, Vector2 dir, int quantidade, float forca)
        {
            quantidade = Mathf.RoundToInt(quantidade * intensidade);
            for (int i = 0; i < quantidade; i++)
            {
                Vector2 v = (dir + Random.insideUnitCircle * 0.9f) * forca * Random.Range(0.2f, 1f);
                Particula(origem, v, corSangue * Random.Range(0.6f, 1.1f), Random.Range(0.6f, 1.4f), true);
            }
        }

        void Particula(Vector2 origem, Vector2 vel, Color cor, float vida, bool sangue)
        {
            var go = new GameObject("Gota");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _pixel; sr.color = cor; sr.sortingOrder = 30000;
            float tam = Random.Range(0.6f, 1.6f);
            go.transform.localScale = new Vector3(tam, tam, 1f);
            go.transform.position = origem;
            _pedacos.Add(new Pedaco { t = go.transform, sr = sr, vel = new Vector2(vel.x, vel.y * 0.5f), altura = sangue ? Random.Range(0.3f, 0.9f) : 0f,
                                      velAltura = sangue ? Mathf.Abs(vel.y) * 0.6f + Random.Range(0f, 3f) : 0f, giro = 0f, vida = vida,
                                      chaoPos = origem + Vector2.down * 0.5f, sangra = false, cor = cor, some = !sangue });
            if (!sangue) { _pedacos[_pedacos.Count - 1].altura = -1f; _pedacos[_pedacos.Count - 1].vel = vel; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _pedacos.Count - 1; i >= 0; i--)
            {
                var p = _pedacos[i];
                p.vida -= dt;
                if (p.altura < 0f)   // fumaça: só sobe e some
                {
                    p.t.position += (Vector3)(p.vel * dt);
                    var c = p.sr.color; c.a = Mathf.Clamp01(p.vida); p.sr.color = c;
                }
                else if (p.altura > 0f)   // no ar: parábola com "altura" separada da posição no chão
                {
                    p.velAltura -= gravidade * dt;
                    p.altura += p.velAltura * dt;
                    p.chaoPos += p.vel * dt;
                    p.t.Rotate(0, 0, p.giro * dt);
                    if (p.altura <= 0f)
                    {
                        p.altura = 0f;
                        if (chao != null && (p.sr.sprite == _pixel || p.sangra))
                            chao.Carimbar(p.chaoPos, p.sr.sprite == _pixel ? Random.Range(0.08f, 0.22f) : 0.35f, corSangue);
                        if (p.sr.sprite == _pixel) p.vida = 0f;
                    }
                    p.t.position = p.chaoPos + Vector2.up * p.altura;
                    p.sr.sortingOrder = -Mathf.RoundToInt(p.chaoPos.y * 100f) + 1;
                }
                else if (p.vida < 1f)     // parado no chão: some aos poucos
                {
                    var c = p.sr.color; c.a = Mathf.Clamp01(p.vida); p.sr.color = c;
                }
                if (p.vida <= 0f) { if (p.t != null) Destroy(p.t.gameObject); _pedacos.RemoveAt(i); }
            }
        }

        static Texture2D GerarRuido(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.6f + Mathf.PerlinNoise(x * 0.31f + 7f, y * 0.31f + 3f) * 0.4f;
                    t.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            t.Apply();
            return t;
        }
    }
}
