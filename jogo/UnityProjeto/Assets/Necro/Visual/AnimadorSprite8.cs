using System;
using UnityEngine;

namespace Necro.Visual
{
    /// <summary>Toca as animações de 8 direções de um DadosSprites num SpriteRenderer.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AnimadorSprite8 : MonoBehaviour
    {
        public DadosSprites dados;
        public string acaoInicial = "parado";
        public float velocidade = 1f;

        public event Action<string> AoTerminar;

        SpriteRenderer _sr;
        DadosSprites.Acao _acao;
        int _direcao;
        float _tempo;
        bool _terminou;

        public string AcaoAtual => _acao != null ? _acao.nome : null;
        public int Direcao => _direcao;
        public Sprite SpriteAtual => _sr.sprite;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (dados != null) Tocar(acaoInicial, true);
        }

        public void Tocar(string nome, bool reiniciar = false)
        {
            if (dados == null) return;
            if (!reiniciar && _acao != null && _acao.nome == nome) return;
            _acao = dados.Buscar(nome);
            _tempo = 0f;
            _terminou = false;
            Atualizar();
        }

        public void Olhar(Vector2 direcaoNoMundo)
        {
            if (direcaoNoMundo.sqrMagnitude < 1e-6f || dados == null) return;
            _direcao = DadosSprites.DirecaoDe(direcaoNoMundo, dados.direcoes);
        }

        void Update()
        {
            if (_acao == null) return;
            _tempo += Time.deltaTime * velocidade;
            Atualizar();
        }

        void Atualizar()
        {
            int frame = Mathf.FloorToInt(_tempo * _acao.fps);
            if (_acao.loop) frame %= _acao.frames;
            else if (frame >= _acao.frames)
            {
                frame = _acao.frames - 1;
                if (!_terminou) { _terminou = true; AoTerminar?.Invoke(_acao.nome); }
            }
            _sr.sprite = _acao.Obter(_direcao, frame);
            // ordenação isométrica: quem está mais "embaixo" na tela fica na frente
            _sr.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f);
        }
    }
}
