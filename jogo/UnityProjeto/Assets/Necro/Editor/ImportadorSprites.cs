using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Necro.Visual;

namespace Necro.EditorTools
{
    /// <summary>
    /// Lê os JSON gerados pelo pipeline do Blender (pasta Assets/Necro/Resources/Sprites/&lt;monstro&gt;/),
    /// fatia cada sprite sheet com o pivô nos pés e cria um asset DadosSprites por monstro.
    /// Menu: Necro > Importar sprites do pipeline.
    /// </summary>
    public static class ImportadorSprites
    {
        const string PastaSprites = "Assets/Necro/Resources/Sprites";

        [Serializable] class MetaAcao { public string nome; public string arquivo; public int frames; public float fps; public bool loop; }
        [Serializable]
        class Meta
        {
            public string nome;
            public int largura_frame;
            public int altura_frame;
            public int[] pivo_px;
            public int pixels_por_unidade_unity;
            public string[] direcoes;
            public MetaAcao[] acoes;
        }

        /// <summary>Na primeira vez que o projeto abre (nenhum DadosSprites ainda), importa tudo sozinho.</summary>
        [InitializeOnLoadMethod]
        static void ImportarNaPrimeiraVez()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.FindAssets("t:DadosSprites").Length == 0) ImportarTudo();
            };
        }

        [MenuItem("Necro/Importar sprites do pipeline")]
        public static void ImportarTudo()
        {
            if (!Directory.Exists(PastaSprites)) { Debug.LogWarning("Pasta não encontrada: " + PastaSprites); return; }
            int n = 0;
            foreach (var json in Directory.GetFiles(PastaSprites, "*.json", SearchOption.AllDirectories))
            {
                try { Importar(json.Replace('\\', '/')); n++; }
                catch (Exception e) { Debug.LogError("Falha ao importar " + json + ": " + e); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Necro: " + n + " monstro(s) importado(s).");
        }

        static void Importar(string caminhoJson)
        {
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(caminhoJson));
            if (meta == null || meta.acoes == null) return;   // não é um JSON do pipeline
            string pasta = Path.GetDirectoryName(caminhoJson).Replace('\\', '/');
            int nDir = meta.direcoes.Length;
            var pivo = new Vector2((float)meta.pivo_px[0] / meta.largura_frame, (float)meta.pivo_px[1] / meta.altura_frame);

            string caminhoDados = pasta + "/" + meta.nome + "_dados.asset";
            var dados = AssetDatabase.LoadAssetAtPath<DadosSprites>(caminhoDados);
            if (dados == null)
            {
                dados = ScriptableObject.CreateInstance<DadosSprites>();
                AssetDatabase.CreateAsset(dados, caminhoDados);
            }
            dados.nomeMonstro = meta.nome;
            dados.direcoes = nDir;
            dados.acoes.Clear();

            foreach (var a in meta.acoes)
            {
                string png = pasta + "/" + a.arquivo;
                var imp = (TextureImporter)AssetImporter.GetAtPath(png);
                if (imp == null) { Debug.LogWarning("Sprite sheet não encontrado: " + png); continue; }
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Multiple;
                imp.spritePixelsPerUnit = meta.pixels_por_unidade_unity;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.maxTextureSize = 8192;
                imp.isReadable = true;   // os efeitos de gore leem os pixels para fatiar o sprite

                int alturaTextura = nDir * meta.altura_frame;
                var fatias = new List<SpriteMetaData>();
                for (int d = 0; d < nDir; d++)
                    for (int f = 0; f < a.frames; f++)
                        fatias.Add(new SpriteMetaData
                        {
                            name = meta.nome + "_" + a.nome + "_" + meta.direcoes[d] + "_" + f,
                            rect = new Rect(f * meta.largura_frame, alturaTextura - (d + 1) * meta.altura_frame,
                                            meta.largura_frame, meta.altura_frame),
                            alignment = (int)SpriteAlignment.Custom,
                            pivot = pivo,
                        });
#pragma warning disable 0618   // API antiga de fatiamento: simples e ainda suportada
                imp.spritesheet = fatias.ToArray();
#pragma warning restore 0618
                imp.SaveAndReimport();

                var porNome = new Dictionary<string, Sprite>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(png))
                    if (o is Sprite s) porNome[s.name] = s;
                var acao = new DadosSprites.Acao { nome = a.nome, frames = a.frames, fps = a.fps, loop = a.loop,
                                                   sprites = new Sprite[nDir * a.frames] };
                for (int i = 0; i < fatias.Count; i++) porNome.TryGetValue(fatias[i].name, out acao.sprites[i]);
                dados.acoes.Add(acao);
            }
            EditorUtility.SetDirty(dados);
        }
    }
}
