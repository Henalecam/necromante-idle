using System;
using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>Objetos quebráveis das salas, no espírito do Diablo 1: barris (alguns explodem), sarcófagos (tesouro ou um morto que se levanta) e urnas.</summary>
    public enum TipoObjeto { Barril = 0, BarrilExplosivo = 1, Sarcofago = 2, Urna = 3 }

    public sealed class ObjetoDeSala
    {
        public int Sala; public TipoObjeto Tipo; public Vec2 Pos; public bool Quebrado;
    }

    public sealed partial class Simulacao
    {
        /// <summary>Todos os objetos do andar, sorteados quando ele começa (o mapa inteiro já os mostra).</summary>
        public readonly List<ObjetoDeSala> Objetos = new List<ObjetoDeSala>(32);

        private void PlanejarObjetos()
        {
            Objetos.Clear();
            if (Cfg.ObjetosPorSala <= 0) return;
            for (int w = 0; w < PlanoDasOndas.Count; w++)
            {
                var plano = PlanoDasOndas[w];
                if (plano.Count == 0 || plano[0].Chefe) continue;   // a arena do chefe fica limpa
                int sala = _salaDaOnda[w];
                int n = _rngMasmorra.Intervalo(Math.Max(1, Cfg.ObjetosPorSala - 2), Cfg.ObjetosPorSala + 1);
                for (int k = 0; k < n; k++)
                {
                    // perto de onde os bandos esperam: é ali que a luta passa
                    var perto = plano[_rngMasmorra.Intervalo(0, plano.Count)].Pos;
                    var pos = new Vec2(perto.X + _rngMasmorra.Intervalo(-2.4f, 2.4f), perto.Y + _rngMasmorra.Intervalo(-2.4f, 2.4f));
                    if (pos.X < 4f) pos.X = 4f; if (pos.X > Cfg.LarguraArena - 1f) pos.X = Cfg.LarguraArena - 1f;
                    if (pos.Y < 1f) pos.Y = 1f; if (pos.Y > Cfg.AlturaArena - 1f) pos.Y = Cfg.AlturaArena - 1f;
                    float r = _rngMasmorra.Proximo01();
                    var tipo = r < 0.45f ? TipoObjeto.Barril : r < 0.60f ? TipoObjeto.BarrilExplosivo : r < 0.80f ? TipoObjeto.Urna : TipoObjeto.Sarcofago;
                    Objetos.Add(new ObjetoDeSala { Sala = sala, Tipo = tipo, Pos = pos });
                }
            }
        }

        /// <summary>O jogador toca num objeto da sala atual para quebrá-lo (só durante a luta).</summary>
        public bool ComandoQuebrar(int indice)
        {
            if (indice < 0 || indice >= Objetos.Count || Fase != FaseExpedicao.EmOnda) return false;
            var o = Objetos[indice];
            if (o.Quebrado || o.Sala != SalaAtual || _emSecreto || _emEmboscada) return false;
            Quebrar(indice, true);
            Resultado.InteracoesPorToque++;
            return true;
        }

        private void AtualizarObjetos()
        {
            if (Objetos.Count == 0 || Fase != FaseExpedicao.EmOnda || _emSecreto || _emEmboscada || TickAtual % 3 != 0) return;
            float r2 = Cfg.ObjetoRaioDeQuebra * Cfg.ObjetoRaioDeQuebra;
            for (int i = 0; i < Objetos.Count; i++)
            {
                var o = Objetos[i];
                if (o.Quebrado || o.Sala != SalaAtual) continue;
                for (int j = 0; j < Entidades.Count; j++)
                {
                    var e = Entidades[j];
                    if (!e.Viva || e.Faccao == Faccao.Inimigo) continue;
                    if (Vec2.Dist2(e.Pos, o.Pos) <= r2) { Quebrar(i, false); break; }
                }
            }
        }

        private void Quebrar(int indice, bool toque)
        {
            var o = Objetos[indice];
            o.Quebrado = true;
            float valor = 0f; int sub = 0;
            float escala = (1f + 0.1f * (Andar - 1)) * _pOssos;
            switch (o.Tipo)
            {
                case TipoObjeto.Barril:
                    if (toque || _rngMasmorra.Chance(Cfg.BarrilChanceDeOssos)) { long q = Math.Max(1L, (long)(Cfg.BarrilOssos * escala + 0.5f)); Resultado.Ossos += q; valor = q; }
                    if (Cfg.PocoesNoCinto && _rngItens.Chance(Cfg.ChanceDePocaoNoBarril))
                    {
                        bool mana = _rngItens.Chance(0.5f);
                        if (mana) Resultado.PocoesDeManaAchadas++; else Resultado.PocoesDeVidaAchadas++;
                        Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 35, Valor = mana ? 1 : 0, Alvo = -1, Origem = -1, Pos = o.Pos });
                    }
                    break;
                case TipoObjeto.BarrilExplosivo:
                    // fere quem estiver perto, dos dois lados (como os barris do Diablo), mas dói bem mais no inimigo
                    for (int j = 0; j < Entidades.Count; j++)
                    {
                        var e = Entidades[j];
                        if (!e.Viva || Vec2.Dist2(e.Pos, o.Pos) > Cfg.BarrilExplosivoRaio * Cfg.BarrilExplosivoRaio) continue;
                        float dano = e.VidaMax * (e.Faccao == Faccao.Inimigo ? (e.Chefe || e.Elite ? Cfg.BarrilExplosivoDanoElite : Cfg.BarrilExplosivoDano) : Cfg.BarrilExplosivoDanoAliado);
                        CausarDano(null, e, dano, TipoDano.Fogo, false);
                    }
                    Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Pos = o.Pos, Valor = Cfg.BarrilExplosivoRaio, TipoDano = TipoDano.Fogo, Alvo = -1, Origem = -1 });
                    break;
                case TipoObjeto.Urna:
                    { long q = Math.Max(1L, (long)(Cfg.UrnaOssos * escala + 0.5f)); Resultado.Ossos += q; valor = q; }
                    if (_rngItens.Chance(Cfg.UrnaChanceDeItem * (toque ? 2f : 1f))) { SoltarItemEm(o.Pos, RaridadeItem.Comum); sub = 1; }
                    break;
                case TipoObjeto.Sarcofago:
                    if (_rngMasmorra.Chance(Cfg.SarcofagoChanceDeMorto))
                    {
                        // o morto se levanta e entra na luta
                        CriarInimigo(EspecieId.Esqueleto, o.Pos);
                        sub = 2;
                    }
                    else { long q = Math.Max(1L, (long)(Cfg.SarcofagoOssos * escala + 0.5f)); Resultado.Ossos += q; valor = q; }
                    break;
            }
            Resultado.Ouro += (long)((o.Tipo == TipoObjeto.Barril ? Cfg.OuroBarril : o.Tipo == TipoObjeto.Urna ? Cfg.OuroUrna : o.Tipo == TipoObjeto.Sarcofago && sub != 2 ? Cfg.OuroSarcofago : 0f) * (1f + 0.1f * (Andar - 1)) + 0.5f);
            Resultado.ObjetosQuebrados++;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 21, Valor = valor, Origem = indice, Alvo = sub, Pos = o.Pos, Critico = toque });
        }
    }
}
