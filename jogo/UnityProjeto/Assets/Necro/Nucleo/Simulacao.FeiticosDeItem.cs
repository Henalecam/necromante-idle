using System;

namespace Necro.Nucleo
{
    /// <summary>
    /// Feitiços vinculados ao equipamento: a Arma e a Mão secundária dão, cada uma, um feitiço próprio do tipo do item,
    /// mais forte conforme a raridade. Trocar de arma troca o feitiço. Como os poderes de toque, não gastam mana.
    /// Cada um (e também o Detonar, o Frenesi e o Sopro) pode ficar no automático: dispara sozinho quando faz sentido,
    /// com recarga maior (ConfigJogo.AutoFeiticoPenalidade), para a mão do jogador continuar valendo mais.
    /// </summary>
    public enum FeiticoDeItem { Nenhum = 0, RaioSombrio = 1, Ceifada = 2, ChuvaDeOssos = 3, EscudoDeAlmas = 4 }

    public sealed partial class Simulacao
    {
        /// <summary>Índices de auto-feitiço em EstadoProgresso.AutoFeiticos.</summary>
        public const int AutoDetonar = 0, AutoFrenesi = 1, AutoSopro = 2, AutoArma = 3, AutoMao = 4;

        private readonly FeiticoDeItem[] _feiticoDoEspaco = new FeiticoDeItem[2];
        private readonly float[] _potenciaDoEspaco = new float[2];
        private readonly float[] _recargaDeItem = new float[2];
        private readonly bool[] _pedidoDeItem = new bool[2];
        private bool[] _auto = new bool[5];

        /// <summary>Feitiço dado pelo item do espaço (0 = Arma, 1 = Mão secundária).</summary>
        public FeiticoDeItem FeiticoDoEspaco(int espaco) { return _feiticoDoEspaco[espaco]; }
        public float RecargaDoFeiticoDeItem(int espaco) { return _recargaDeItem[espaco] > 0f ? _recargaDeItem[espaco] : 0f; }
        /// <summary>Dispara o feitiço do item (0 = Arma, 1 = Mão secundária). Vale no próximo tick.</summary>
        public void ComandoFeiticoDeItem(int espaco) { if (espaco == 0 || espaco == 1) _pedidoDeItem[espaco] = true; }

        public static FeiticoDeItem FeiticoDoTipo(TipoItem tipo)
        {
            switch (tipo)
            {
                case TipoItem.Cajado: return FeiticoDeItem.RaioSombrio;
                case TipoItem.Foice: return FeiticoDeItem.Ceifada;
                case TipoItem.Grimorio: return FeiticoDeItem.ChuvaDeOssos;
                case TipoItem.Relicario: return FeiticoDeItem.EscudoDeAlmas;
                default: return FeiticoDeItem.Nenhum;
            }
        }

        public static float RecargaBase(FeiticoDeItem f, ConfigJogo cfg)
        {
            switch (f)
            {
                case FeiticoDeItem.RaioSombrio: return cfg.RaioSombrioRecarga;
                case FeiticoDeItem.Ceifada: return cfg.CeifadaRecarga;
                case FeiticoDeItem.ChuvaDeOssos: return cfg.ChuvaDeOssosRecarga;
                case FeiticoDeItem.EscudoDeAlmas: return cfg.EscudoDeAlmasRecarga;
                default: return 0f;
            }
        }

        private void PrepararFeiticosDeItem(EstadoProgresso p)
        {
            for (int e = 0; e < 2; e++)
            {
                var it = Equipamento.Obter(p, e == 0 ? EspacoEquipamento.Arma : EspacoEquipamento.MaoSecundaria);
                if (Equipamento.Vazio(it)) continue;
                _feiticoDoEspaco[e] = FeiticoDoTipo(it.Tipo);
                _potenciaDoEspaco[e] = 1f + Cfg.FeiticoDeItemPorRaridade * (int)it.Raridade;
            }
            if (p.AutoFeiticos != null) for (int i = 0; i < _auto.Length && i < p.AutoFeiticos.Length; i++) _auto[i] = p.AutoFeiticos[i];
        }

        private void AtualizarFeiticosDeItem()
        {
            if (!Necromante.Viva) return;
            // automáticos dos poderes de toque: pedem sozinhos quando vale a pena; a recarga sai maior
            if (_auto[AutoDetonar] && RecargaDetonar <= 0f && CadaveresDetonaveis >= 3 && InimigosVivos >= 2) { Detonar(); if (RecargaDetonar > 0f) RecargaDetonar *= Cfg.AutoFeiticoPenalidade; }
            if (_auto[AutoFrenesi] && RecargaFrenesi <= 0f && InimigosVivos >= 3 && HaMinionVivo())
            {
                RecargaFrenesi = Cfg.FrenesiRecarga * Cfg.AutoFeiticoPenalidade; FrenesiTempo = Cfg.FrenesiDuracao;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 6, Alvo = -1, Origem = Necromante.Id, Valor = Cfg.FrenesiDuracao, Pos = Necromante.Pos });
            }
            if (_auto[AutoSopro] && RecargaSopro <= 0f)
            {
                Entidade pior = null;
                for (int i = 0; i < Entidades.Count; i++)
                {
                    var a = Entidades[i];
                    if (a.Viva && a.Faccao != Faccao.Inimigo && a.Vida < a.VidaMax * 0.4f && (pior == null || a.Vida / a.VidaMax < pior.Vida / pior.VidaMax)) pior = a;
                }
                if (pior != null)
                {
                    RecargaSopro = Cfg.SoproRecarga * Cfg.AutoFeiticoPenalidade; Resultado.SoprosUsados++;
                    Curar(Necromante, pior, pior.VidaMax * Cfg.SoproCura);
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 11, Alvo = pior.Id, Origem = Necromante.Id, Valor = Cfg.SoproCura, Pos = pior.Pos });
                }
            }
            for (int e = 0; e < 2; e++)
            {
                if (_recargaDeItem[e] > 0f) _recargaDeItem[e] -= DT * _pRecarga;
                bool manual = _pedidoDeItem[e]; _pedidoDeItem[e] = false;
                var f = _feiticoDoEspaco[e];
                if (f == FeiticoDeItem.Nenhum || _recargaDeItem[e] > 0f) continue;
                bool auto = !manual && _auto[e == 0 ? AutoArma : AutoMao];
                if (!manual && !auto) continue;
                if (LancarFeiticoDeItem(f, _potenciaDoEspaco[e], auto))
                {
                    _recargaDeItem[e] = RecargaBase(f, Cfg) * (auto ? Cfg.AutoFeiticoPenalidade : 1f);
                    Resultado.FeiticosDeItem++;
                }
            }
        }

        /// <summary>Executa o feitiço. Devolve false (sem gastar a recarga) se não havia o que fazer.</summary>
        private bool LancarFeiticoDeItem(FeiticoDeItem f, float potencia, bool auto)
        {
            var n = Necromante;
            switch (f)
            {
                case FeiticoDeItem.RaioSombrio:
                {
                    // um alvo só: o marcado; senão, o inimigo com mais vida
                    Entidade alvo = Marcado();
                    if (alvo == null)
                        for (int i = 0; i < Entidades.Count; i++)
                        {
                            var o = Entidades[i];
                            if (o.Viva && o.Faccao == Faccao.Inimigo && (alvo == null || o.Vida > alvo.Vida)) alvo = o;
                        }
                    if (alvo == null) return false;
                    Emitir(new EventoSim { Tipo = TipoEvento.FeiticoLancado, Origem = n.Id, Alvo = alvo.Id, Extra = 100 + (int)f, Pos = alvo.Pos, Dir = (alvo.Pos - n.Pos).Normalizado, Valor = 0f });
                    CausarDano(n, alvo, n.Dano * Cfg.RaioSombrioPotencia * potencia, TipoDano.Sombra, true);
                    return true;
                }
                case FeiticoDeItem.Ceifada:
                {
                    float r2 = Cfg.CeifadaRaio * Cfg.CeifadaRaio; int acertos = 0, total = Entidades.Count;
                    for (int i = 0; i < total; i++)
                    {
                        var o = Entidades[i];
                        if (o.Viva && o.Faccao == Faccao.Inimigo && Vec2.Dist2(o.Pos, n.Pos) <= r2) acertos++;
                    }
                    if (acertos == 0 || (auto && acertos < 2 && InimigosVivos > 1)) return false;
                    Emitir(new EventoSim { Tipo = TipoEvento.FeiticoLancado, Origem = n.Id, Alvo = -1, Extra = 100 + (int)f, Pos = n.Pos, Dir = n.Olhar, Valor = Cfg.CeifadaRaio });
                    for (int i = 0; i < total; i++)
                    {
                        var o = Entidades[i];
                        if (o.Viva && o.Faccao == Faccao.Inimigo && Vec2.Dist2(o.Pos, n.Pos) <= r2) CausarDano(n, o, n.Dano * Cfg.CeifadaPotencia * potencia, TipoDano.Fisico, true);
                    }
                    return true;
                }
                case FeiticoDeItem.ChuvaDeOssos:
                {
                    // cai onde há mais inimigos juntos (ou no marcado)
                    Entidade centro = Marcado(); int melhor = 0; float r2 = Cfg.ChuvaDeOssosRaio * Cfg.ChuvaDeOssosRaio;
                    if (centro == null)
                        for (int i = 0; i < Entidades.Count; i++)
                        {
                            var c = Entidades[i];
                            if (!c.Viva || c.Faccao != Faccao.Inimigo) continue;
                            int k = 0;
                            for (int j = 0; j < Entidades.Count; j++) { var o = Entidades[j]; if (o.Viva && o.Faccao == Faccao.Inimigo && Vec2.Dist2(o.Pos, c.Pos) <= r2) k++; }
                            if (k > melhor) { melhor = k; centro = c; }
                        }
                    if (centro == null || (auto && melhor < 3 && InimigosVivos >= 3)) return false;
                    Vec2 ponto = centro.Pos;
                    Emitir(new EventoSim { Tipo = TipoEvento.FeiticoLancado, Origem = n.Id, Alvo = -1, Extra = 100 + (int)f, Pos = ponto, Dir = n.Olhar, Valor = Cfg.ChuvaDeOssosRaio });
                    Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = n.Id, Alvo = -1, Pos = ponto, Valor = Cfg.ChuvaDeOssosRaio, TipoDano = TipoDano.Fisico });
                    int total = Entidades.Count;
                    for (int i = 0; i < total; i++)
                    {
                        var o = Entidades[i];
                        if (o.Viva && o.Faccao == Faccao.Inimigo && Vec2.Dist2(o.Pos, ponto) <= r2) CausarDano(n, o, n.Dano * Cfg.ChuvaDeOssosPotencia * potencia, TipoDano.Fisico, true);
                    }
                    return true;
                }
                case FeiticoDeItem.EscudoDeAlmas:
                {
                    if (InimigosVivos == 0) return false;
                    Emitir(new EventoSim { Tipo = TipoEvento.FeiticoLancado, Origem = n.Id, Alvo = -1, Extra = 100 + (int)f, Pos = n.Pos, Dir = n.Olhar, Valor = 0f });
                    for (int i = 0; i < Entidades.Count; i++)
                    {
                        var a = Entidades[i];
                        if (!a.Viva || a.Faccao == Faccao.Inimigo) continue;
                        float escudo = a.VidaMax * Cfg.EscudoDeAlmasFracao * potencia;
                        if (a.Escudo < escudo) a.Escudo = escudo;
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
