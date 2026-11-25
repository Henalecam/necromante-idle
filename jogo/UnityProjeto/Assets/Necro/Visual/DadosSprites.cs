using System;
using System.Collections.Generic;
using UnityEngine;

namespace Necro.Visual
{
    /// <summary>Sprites de um monstro, gerados pelo pipeline do Blender e fatiados pelo ImportadorSprites.</summary>
    [CreateAssetMenu(menuName = "Necro/Dados de Sprites")]
    public class DadosSprites : ScriptableObject
    {
        [Serializable]
        public class Acao
        {
            public string nome;
            public int frames;
            public float fps = 8f;
            public bool loop = true;
            /// <summary>Ordem: direção * frames + frame. Direções: S, SE, E, NE, N, NW, W, SW.</summary>
            public Sprite[] sprites;

            public Sprite Obter(int direcao, int frame) => sprites[direcao * frames + Mathf.Clamp(frame, 0, frames - 1)];
        }

        public string nomeMonstro;
        public int direcoes = 8;
        public List<Acao> acoes = new List<Acao>();

        public Acao Buscar(string nome)
        {
            for (int i = 0; i < acoes.Count; i++)
                if (acoes[i].nome == nome) return acoes[i];
            return acoes.Count > 0 ? acoes[0] : null;
        }

        /// <summary>Converte um vetor de movimento no mundo (x direita, y cima na tela) no índice de direção.</summary>
        public static int DirecaoDe(Vector2 v, int direcoes = 8)
        {
            if (v.sqrMagnitude < 1e-6f) return 0;
            // 0 = S (para baixo na tela), girando no sentido anti-horário: SE, E, NE, N...
            float ang = Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;   // S=0, E=90, N=180, W=-90
            if (ang < 0) ang += 360f;
            return Mathf.RoundToInt(ang / (360f / direcoes)) % direcoes;
        }
    }
}
