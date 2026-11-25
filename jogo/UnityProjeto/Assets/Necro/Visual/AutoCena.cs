using UnityEngine;

namespace Necro.Visual
{
    /// <summary>
    /// Monta a cena sozinho ao apertar Play numa cena vazia: câmera, chão da cripta, sangue, gore e a arena.
    /// Se a cena já tiver um VisualArena, não faz nada.
    /// </summary>
    public static class AutoCena
    {
        const float PPU = 32f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Montar()
        {
            if (Object.FindObjectOfType<VisualArena>() != null) return;

            // chão + parede do fundo (imagem renderizada no Blender)
            var tex = Resources.Load<Texture2D>("Arena/arena_chao");
            float alturaMundo = 19.4f, centroY = 2.7f;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                // a arena de 20x12 fica centrada na parte de baixo da imagem (abaixo da faixa da parede)
                float topoPx = 171f, chaoPx = tex.height - topoPx;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, (chaoPx * 0.5f) / tex.height), PPU);
                var chao = new GameObject("Cripta").AddComponent<SpriteRenderer>();
                chao.sprite = sp; chao.sortingOrder = -32000;
                alturaMundo = tex.height / PPU;
                centroY = (tex.height * 0.5f - chaoPx * 0.5f) / PPU;
            }

            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Camera").AddComponent<Camera>(); cam.gameObject.tag = "MainCamera"; }
            cam.orthographic = true;
            cam.orthographicSize = alturaMundo * 0.5f;
            cam.transform.position = new Vector3(0f, centroY, -10f);
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;

            var sangue = new GameObject("ChaoDeSangue").AddComponent<ChaoDeSangue>();
            sangue.tamanhoMundo = new Vector2(42f, 14f * 1.0f);
            var gore = new GameObject("GoreMestre").AddComponent<GoreMestre>();
            gore.chao = sangue;
            var arena = new GameObject("Arena").AddComponent<VisualArena>();
            arena.gameObject.AddComponent<Interface>();
        }
    }
}
