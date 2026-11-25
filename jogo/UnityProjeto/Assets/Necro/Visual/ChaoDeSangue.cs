using UnityEngine;

namespace Necro.Visual
{
    /// <summary>
    /// Sangue que fica: uma RenderTexture cobrindo a arena, onde cada gota "carimba" uma mancha.
    /// O acúmulo é permanente durante o andar e custa uma única textura. Chame Limpar() ao trocar de andar.
    /// </summary>
    public class ChaoDeSangue : MonoBehaviour
    {
        public Vector2 tamanhoMundo = new Vector2(24f, 14f);
        public int pixelsPorUnidade = 32;
        public Texture2D[] manchas;      // opcional: formas de mancha; se vazio, gera manchas irregulares

        RenderTexture _rt;
        Material _matCarimbo;
        Texture2D[] _geradas;

        void Awake()
        {
            int w = Mathf.RoundToInt(tamanhoMundo.x * pixelsPorUnidade), h = Mathf.RoundToInt(tamanhoMundo.y * pixelsPorUnidade);
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _rt.Create();
            Limpar();
            _matCarimbo = new Material(Shader.Find("Sprites/Default"));
            if (manchas == null || manchas.Length == 0) { _geradas = new Texture2D[6]; for (int i = 0; i < 6; i++) _geradas[i] = GerarMancha(32, i * 17 + 3); }

            // mostra a RenderTexture num quad do tamanho da arena, logo acima do chão
            var mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.AddComponent<MeshRenderer>();
            var m = new Mesh();
            float hx = tamanhoMundo.x / 2f, hy = tamanhoMundo.y / 2f;
            m.vertices = new[] { new Vector3(-hx, -hy), new Vector3(hx, -hy), new Vector3(hx, hy), new Vector3(-hx, hy) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mf.sharedMesh = m;
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = _rt };
            mr.sharedMaterial = mat;
            mr.sortingOrder = -30000;
        }

        public void Limpar()
        {
            var ativo = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = ativo;
        }

        /// <summary>Carimba uma mancha na posição do mundo, com o raio em unidades.</summary>
        public void Carimbar(Vector2 posMundo, float raio, Color cor)
        {
            Vector2 local = (Vector2)transform.InverseTransformPoint(posMundo) + tamanhoMundo / 2f;
            float px = local.x * pixelsPorUnidade, py = local.y * pixelsPorUnidade, r = raio * pixelsPorUnidade;
            var lista = (manchas != null && manchas.Length > 0) ? manchas : _geradas;
            var tex = lista[Random.Range(0, lista.Length)];

            var ativo = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _rt.width, 0, _rt.height);
            _matCarimbo.mainTexture = tex;
            _matCarimbo.color = cor;
            _matCarimbo.SetPass(0);
            // manchas achatadas na vertical, por causa da vista isométrica
            Graphics.DrawTexture(new Rect(px - r, py - r * 0.5f, r * 2f, r), tex, _matCarimbo);
            GL.PopMatrix();
            RenderTexture.active = ativo;
        }

        static Texture2D GerarMancha(int n, int semente)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float ang = Mathf.Atan2(dy, dx);
                    float borda = 0.55f + 0.35f * Mathf.PerlinNoise(semente + Mathf.Cos(ang) * 1.5f, semente + Mathf.Sin(ang) * 1.5f);
                    float a = Mathf.Sqrt(dx * dx + dy * dy) < borda ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        void OnDestroy() { if (_rt != null) _rt.Release(); }
    }
}
