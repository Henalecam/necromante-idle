using System;

namespace Necro.Nucleo
{
    public sealed partial class Simulacao
    {
        private static bool Hostil(Entidade e, Entidade outro)
        {
            return (e.Faccao == Faccao.Inimigo) != (outro.Faccao == Faccao.Inimigo);
        }

        /// <summary>Hostil vivo mais próximo de "referencia". filtro2 = raio² em volta do necromante (posturas).</summary>
        private Entidade HostilMaisProximo(Entidade e, Vec2 referencia, float filtro2)
        {
            Entidade melhor = null;
            float melhorD = float.MaxValue;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (!o.Viva || !Hostil(e, o)) continue;
                if (filtro2 < float.MaxValue && Vec2.Dist2(o.Pos, Necromante.Pos) > filtro2) continue;
                float d = Vec2.Dist2(o.Pos, referencia);
                if (d < melhorD) { melhorD = d; melhor = o; }
            }
            return melhor;
        }

        /// <summary>Hostil de menor vida dentro do alcance (Atirador).</summary>
        private Entidade HostilDeMenorVida(Entidade e, float alcance, float filtro2)
        {
            Entidade melhor = null;
            float a2 = alcance * alcance;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (!o.Viva || !Hostil(e, o)) continue;
                if (Vec2.Dist2(o.Pos, e.Pos) > a2) continue;
                if (filtro2 < float.MaxValue && Vec2.Dist2(o.Pos, Necromante.Pos) > filtro2) continue;
                if (melhor == null || o.Vida < melhor.Vida) melhor = o;
            }
            return melhor;
        }

        /// <summary>Hostil dentro do alcance com mais hostis em volta (Conjurador mira "onde há mais inimigos").</summary>
        private Entidade MelhorCentroDeArea(Entidade e, float alcance, float raio, float filtro2)
        {
            Entidade melhor = null;
            int melhorN = 0;
            float a2 = alcance * alcance, r2 = raio * raio;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (!o.Viva || !Hostil(e, o)) continue;
                if (Vec2.Dist2(o.Pos, e.Pos) > a2) continue;
                if (filtro2 < float.MaxValue && Vec2.Dist2(o.Pos, Necromante.Pos) > filtro2) continue;
                int n = 0;
                for (int j = 0; j < Entidades.Count; j++)
                {
                    var v = Entidades[j];
                    if (v.Viva && Hostil(e, v) && Vec2.Dist2(v.Pos, o.Pos) <= r2) n++;
                }
                if (n > melhorN) { melhorN = n; melhor = o; }
            }
            return melhor;
        }

        /// <summary>Aliado vivo e ferido com a menor fração de vida (Suporte).</summary>
        private Entidade AliadoMaisFerido(Entidade e)
        {
            Entidade melhor = null;
            float melhorF = 0.999f;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (!o.Viva || o == e || Hostil(e, o)) continue;
                float f = o.Vida / o.VidaMax;
                if (f < melhorF) { melhorF = f; melhor = o; }
            }
            return melhor;
        }

        private void MoverPara(Entidade e, Vec2 destino)
        {
            Vec2 delta = destino - e.Pos;
            float d = delta.Comprimento;
            if (d < 0.05f) { e.Animacao = EstadoAnimacao.Parado; return; }
            float passo = e.VelMovimento * DT;
            Vec2 dir = new Vec2(delta.X / d, delta.Y / d);
            e.Pos = Limitar(passo >= d ? destino : e.Pos + dir * passo);
            e.Olhar = dir;
            e.Animacao = EstadoAnimacao.Andando;
        }

        private void Ocioso(Entidade e)
        {
            // Minions voltam ao posto na formação; inimigos ficam parados.
            if (e.Faccao == Faccao.Minion && Vec2.Dist2(e.Pos, e.Posto) > 0.04f) MoverPara(e, e.Posto);
            else e.Animacao = EstadoAnimacao.Parado;
        }

        private void Olhar(Entidade e, Vec2 ponto)
        {
            Vec2 d = (ponto - e.Pos).Normalizado;
            if (d.X != 0f || d.Y != 0f) e.Olhar = d;
        }

        private void Atacar(Entidade e, Entidade alvo, float multiplicador)
        {
            e.Recarga = 1f / e.VelAtaque;
            e.TempoAcao = Math.Min(0.3f, 0.5f / e.VelAtaque);
            e.Animacao = EstadoAnimacao.Atacando;
            Olhar(e, alvo.Pos);
            Emitir(new EventoSim { Tipo = TipoEvento.Ataque, Origem = e.Id, Alvo = alvo.Id, Pos = e.Pos, Dir = e.Olhar, TipoDano = e.TipoDano });
            if (e.Papel == Papel.Atirador)
            {
                // Atirador: o dano viaja numa flecha e só é aplicado quando ela chega.
                var pr = new Projetil { Id = _proxProjetil++, Origem = e, Alvo = alvo, EspecieOrigem = e.Especie, Pos = e.Pos, Destino = alvo.Pos,
                                        Dir = e.Olhar, Dano = e.Dano * multiplicador, TipoDano = e.TipoDano };
                Projeteis.Add(pr);
                Emitir(new EventoSim { Tipo = TipoEvento.ProjetilLancado, Origem = e.Id, Alvo = pr.Id, Pos = e.Pos, Dir = e.Olhar, Especie = e.Especie, TipoDano = e.TipoDano });
                return;
            }
            CausarDano(e, alvo, e.Dano * multiplicador, e.TipoDano, true);

            // Tanque (minion): o golpe provoca o alvo.
            if (e.Papel == Papel.Tanque && e.Faccao == Faccao.Minion && alvo.Viva && e.Viva)
            {
                if (alvo.ProvocadoPor != e.Id) Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 2, Alvo = alvo.Id, Origem = e.Id, Valor = Cfg.DuracaoProvocacao });
                alvo.ProvocadoPor = e.Id;
                alvo.ProvocadoTempo = Cfg.DuracaoProvocacao;
            }
        }

        private void AtualizarProjeteis()
        {
            float passo = Cfg.VelProjetil * DT;
            for (int i = Projeteis.Count - 1; i >= 0; i--)
            {
                var pr = Projeteis[i];
                if (pr.Perfurante)
                {
                    float avanco = Math.Min(pr.Velocidade * DT, pr.Restante);
                    Vec2 ini = pr.Pos;
                    int total = Entidades.Count;
                    for (int k = 0; k < total; k++)
                    {
                        var e = Entidades[k];
                        if (!e.Viva || e.Faccao != Faccao.Inimigo || pr.Atingidos.Contains(e.Id)) continue;
                        Vec2 rel = e.Pos - ini;
                        float t = rel.X * pr.Dir.X + rel.Y * pr.Dir.Y;
                        if (t < -pr.Raio || t > avanco) continue;
                        float qx = rel.X - pr.Dir.X * t, qy = rel.Y - pr.Dir.Y * t;
                        if (qx * qx + qy * qy > pr.Raio * pr.Raio) continue;
                        pr.Atingidos.Add(e.Id);
                        CausarDano(pr.Origem, e, pr.Dano, pr.TipoDano, true);
                    }
                    pr.Pos = new Vec2 { X = ini.X + pr.Dir.X * avanco, Y = ini.Y + pr.Dir.Y * avanco };
                    pr.Restante -= avanco;
                    if (pr.Restante <= 0.0001f)
                    {
                        Emitir(new EventoSim { Tipo = TipoEvento.ProjetilTerminou, Alvo = pr.Id, Origem = -1, Pos = pr.Pos, Dir = pr.Dir, TipoDano = pr.TipoDano, Extra = 1 });
                        Projeteis.RemoveAt(i);
                    }
                    continue;
                }
                bool alvoVivo = pr.Alvo != null && pr.Alvo.Viva;
                if (alvoVivo) pr.Destino = pr.Alvo.Pos;
                Vec2 d = pr.Destino - pr.Pos;
                float dist = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y);
                if (dist <= passo)
                {
                    pr.Pos = pr.Destino;
                    Emitir(new EventoSim { Tipo = TipoEvento.ProjetilTerminou, Alvo = pr.Id, Origem = alvoVivo ? pr.Alvo.Id : -1, Pos = pr.Pos, Dir = pr.Dir, TipoDano = pr.TipoDano });
                    if (alvoVivo) CausarDano(pr.Origem, pr.Alvo, pr.Dano, pr.TipoDano, true);
                    Projeteis.RemoveAt(i);
                }
                else
                {
                    pr.Dir = new Vec2 { X = d.X / dist, Y = d.Y / dist };
                    pr.Pos = new Vec2 { X = pr.Pos.X + pr.Dir.X * passo, Y = pr.Pos.Y + pr.Dir.Y * passo };
                }
            }
        }

        /// <summary>IA de minions e inimigos, por Papel.</summary>
        private void AtualizarUnidade(Entidade e)
        {
            if (e.Temporario)
            {
                e.TempoDeVida -= DT;
                if (e.TempoDeVida <= 1e-3f) { ExpirarTemporario(e); return; } // tolerância ao acúmulo de float, como nos orbes
            }
            if (e.Recarga > 0f) e.Recarga -= DT;
            if (e.FraquezaTempo > 0f) e.FraquezaTempo -= DT;
            if (e.ProvocadoTempo > 0f) { e.ProvocadoTempo -= DT; if (e.ProvocadoTempo <= 0f) e.ProvocadoPor = -1; }
            if (e.TempoAcao > 0f) { e.TempoAcao -= DT; return; } // presa na animação de ataque/conjuração

            bool inimigo = e.Faccao == Faccao.Inimigo;
            if (inimigo && e.Chefe && HabilidadeDoChefe(e)) return;

            // Posturas (só minions): limitam quais inimigos contam como alvo.
            float filtro2 = float.MaxValue;
            if (!inimigo)
            {
                if (InimigosVivos == 0 && e.Papel != Papel.Suporte) { Ocioso(e); return; }
                if (e.Postura == Postura.Defensiva) filtro2 = Cfg.RaioDefensivo * Cfg.RaioDefensivo;
                else if (e.Postura == Postura.Escolta) filtro2 = Cfg.RaioEscolta * Cfg.RaioEscolta;
            }

            Entidade alvo = null;
            if (inimigo && e.ProvocadoPor >= 0)
            {
                var t = AcharEntidade(e.ProvocadoPor);
                if (t != null && t.Viva) alvo = t; else e.ProvocadoPor = -1;
            }

            if (alvo == null)
            {
                switch (e.Papel)
                {
                    case Papel.Suporte:
                        if (!(inimigo && e.Chefe)) { AtualizarSuporte(e, filtro2); return; }
                        alvo = HostilMaisProximo(e, e.Pos, filtro2);
                        break;
                    case Papel.Conjurador:
                        AtualizarConjurador(e, filtro2);
                        return;
                    case Papel.Atirador:
                        // Fica atrás e escolhe o de menor vida ao alcance; se não há ninguém ao alcance, aproxima do mais próximo.
                        alvo = HostilDeMenorVida(e, e.Alcance, filtro2);
                        if (alvo == null) alvo = HostilMaisProximo(e, e.Pos, filtro2);
                        break;
                    case Papel.Tanque:
                        // Minion tanque intercepta o inimigo mais próximo do necromante.
                        alvo = HostilMaisProximo(e, inimigo ? e.Pos : Necromante.Pos, filtro2);
                        break;
                    default: // Lutador e Enxame: o mais próximo
                        alvo = HostilMaisProximo(e, e.Pos, filtro2);
                        break;
                }
            }

            if (alvo == null) { Ocioso(e); return; }

            float d = Vec2.Dist(e.Pos, alvo.Pos);

            // Atirador aliado recua enquanto recarrega se algo chegar perto demais.
            if (!inimigo && e.Papel == Papel.Atirador && e.Recarga > 0f)
            {
                var perto = HostilMaisProximo(e, e.Pos, float.MaxValue);
                if (perto != null && Vec2.Dist2(perto.Pos, e.Pos) < 1.8f * 1.8f)
                {
                    Vec2 fuga = (e.Pos - perto.Pos).Normalizado;
                    MoverPara(e, e.Pos + fuga * 1.0f);
                    return;
                }
            }

            if (d <= e.Alcance + 0.05f)
            {
                if (e.Recarga <= 0f) Atacar(e, alvo, 1f);
                else { e.Animacao = EstadoAnimacao.Parado; Olhar(e, alvo.Pos); }
            }
            else
            {
                MoverPara(e, alvo.Pos);
            }
        }

        /// <summary>Conjurador: dano em área centrado onde há mais inimigos.</summary>
        private void AtualizarConjurador(Entidade e, float filtro2)
        {
            Entidade centro = MelhorCentroDeArea(e, e.Alcance, Cfg.RaioAreaConjurador, filtro2);
            if (centro == null)
            {
                var prox = HostilMaisProximo(e, e.Pos, filtro2);
                if (prox == null) Ocioso(e); else MoverPara(e, prox.Pos);
                return;
            }
            Olhar(e, centro.Pos);
            if (e.Recarga > 0f) { e.Animacao = EstadoAnimacao.Parado; return; }

            e.Recarga = 1f / e.VelAtaque;
            e.TempoAcao = Cfg.TempoDeConjuracao;
            e.Animacao = EstadoAnimacao.Conjurando;
            Vec2 ponto = centro.Pos;
            float r2 = Cfg.RaioAreaConjurador * Cfg.RaioAreaConjurador;
            Emitir(new EventoSim { Tipo = TipoEvento.Ataque, Origem = e.Id, Alvo = centro.Id, Pos = e.Pos, Dir = e.Olhar, TipoDano = e.TipoDano });
            Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = e.Id, Alvo = -1, Pos = ponto, Valor = Cfg.RaioAreaConjurador, TipoDano = e.TipoDano });
            int n = Entidades.Count;
            for (int i = 0; i < n; i++)
            {
                var o = Entidades[i];
                if (o.Viva && Hostil(e, o) && Vec2.Dist2(o.Pos, ponto) <= r2) CausarDano(e, o, e.Dano, e.TipoDano, true);
            }
        }

        /// <summary>Suporte: cura o aliado de menor vida; sem feridos, ataca fraco o que estiver ao alcance.</summary>
        private void AtualizarSuporte(Entidade e, float filtro2)
        {
            Entidade ferido = AliadoMaisFerido(e);
            if (ferido != null)
            {
                if (Vec2.Dist(e.Pos, ferido.Pos) > e.Alcance + 3f) { MoverPara(e, ferido.Pos); return; }
                Olhar(e, ferido.Pos);
                if (e.Recarga > 0f) { e.Animacao = EstadoAnimacao.Parado; return; }
                e.Recarga = 1f / e.VelAtaque;
                e.TempoAcao = Cfg.TempoDeConjuracao;
                e.Animacao = EstadoAnimacao.Conjurando;
                Curar(e, ferido, e.Dano);
                return;
            }
            Entidade alvo = HostilMaisProximo(e, e.Pos, filtro2);
            if (alvo != null && Vec2.Dist(e.Pos, alvo.Pos) <= e.Alcance + 0.05f)
            {
                if (e.Recarga <= 0f) Atacar(e, alvo, 0.5f);
                else e.Animacao = EstadoAnimacao.Parado;
                return;
            }
            Ocioso(e);
        }

        /// <summary>A Abadessa Costurada: periodicamente invoca uma poça de sangue que a cura. Retorna true se gastou o tick.</summary>
        private bool HabilidadeDoChefe(Entidade e)
        {
            e.TempoHabilidade -= DT;
            if (e.TempoHabilidade > 0f) return false;
            e.TempoHabilidade = Cfg.PocaIntervalo;
            e.TempoAcao = 1.0f;
            e.Animacao = EstadoAnimacao.Conjurando;
            var pos = Limitar(new Vec2(e.Pos.X + Rng.Intervalo(-1.2f, 1.2f), e.Pos.Y + Rng.Intervalo(-1.2f, 1.2f)));
            var p = new PocaDeSangue { Id = _proximoId++, DonoId = e.Id, Pos = pos, Raio = Cfg.PocaRaio, TempoRestante = Cfg.PocaDuracao };
            Pocas.Add(p);
            Emitir(new EventoSim { Tipo = TipoEvento.PocaCriada, Alvo = p.Id, Origem = e.Id, Pos = pos, Valor = p.Raio });
            return true;
        }
    }
}
