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
        /// <summary>Só para testes: o alvo que esta entidade escolheria agora.</summary>
        public Entidade AlvoMaisProximoParaTeste(Entidade e) { return HostilMaisProximo(e, e.Pos, float.MaxValue); }

        private Entidade HostilMaisProximo(Entidade e, Vec2 referencia, float filtro2)
        {
            if (MarcadoId >= 0 && e.Faccao == Faccao.Minion)
            {
                // alvo marcado pelo jogador tem prioridade sobre o mais próximo
                var marcado = Marcado();
                if (marcado != null && (filtro2 == float.MaxValue || Vec2.Dist2(marcado.Pos, Necromante.Pos) <= filtro2)) return marcado;
            }
            Entidade melhor = null;
            float melhorD = float.MaxValue;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (!o.Viva || !Hostil(e, o)) continue;
                if (o.Oculto && Vec2.Dist2(o.Pos, e.Pos) > Cfg.OcultoVistoA * Cfg.OcultoVistoA) continue;   // Oculto: ninguém o enxerga de longe
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

        /// <summary>
        /// Comportamentos próprios de espécie dos inimigos (Diablo 1). Devolve true se a unidade já agiu neste tick.
        /// Diabrete: foge por uns segundos quando vê um companheiro morrer (ver AoMorrerInimigoPerto) e depois volta.
        /// Carniçal: ferido, vai até um cadáver e o devora para se curar. Atirador: recua enquanto recarrega se chegam perto.
        /// </summary>
        private bool ComportamentoDeEspecie(Entidade e)
        {
            if (e.Chefe || e.Unico) return false;
            if (e.FugaTempo > 0f)
            {
                e.FugaTempo -= DT;
                var h = HostilMaisProximo(e, e.Pos, float.MaxValue);
                if (h == null) { e.FugaTempo = 0f; return false; }
                Vec2 fuga = (e.Pos - h.Pos).Normalizado;
                MoverPara(e, Limitar(e.Pos + fuga * 2f)); e.Animacao = EstadoAnimacao.Andando;
                return true;
            }
            if (e.Especie == EspecieId.Carnical && !e.Elite)
            {
                if (e.ComendoTempo > 0f)
                {
                    e.ComendoTempo -= DT; e.Animacao = EstadoAnimacao.Conjurando;
                    if (e.ComendoTempo <= 0f)
                    {
                        float cura = e.VidaMax * Cfg.BanqueteInimigoCura; e.Vida = Math.Min(e.VidaMax, e.Vida + cura); e.JaComeu = true;
                        Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 31, Alvo = e.Id, Origem = -1, Valor = cura, Pos = e.Pos });
                    }
                    return true;
                }
                if (!e.JaComeu && e.Vida < e.VidaMax * Cfg.BanqueteInimigoVida)
                {
                    int melhor = -1; float md = Cfg.BanqueteInimigoRaio * Cfg.BanqueteInimigoRaio;
                    for (int i = 0; i < Cadaveres.Count; i++) { float d2 = Vec2.Dist2(Cadaveres[i].Pos, e.Pos); if (d2 < md) { md = d2; melhor = i; } }
                    if (melhor >= 0)
                    {
                        if (md <= 0.6f * 0.6f) { Cadaveres.RemoveAt(melhor); e.ComendoTempo = Cfg.BanqueteInimigoTempo; Resultado.Banquetes++; Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 32, Alvo = e.Id, Origem = -1, Pos = e.Pos }); }
                        else { MoverPara(e, Cadaveres[melhor].Pos); e.Animacao = EstadoAnimacao.Andando; }
                        return true;
                    }
                }
            }
            if (e.Oculto && TickAtual * DT >= e.OcultoAte) Revelar(e);
            if (e.Especie == EspecieId.Carpideira && Cfg.HabilidadesDeEspecie && (e.TempoHabilidade -= DT) <= 0f)
            {
                e.TempoHabilidade = Cfg.CarpideiraIntervalo; int curados = 0; float r2 = Cfg.CarpideiraRaio * Cfg.CarpideiraRaio;
                for (int i = 0; i < Entidades.Count && curados < Cfg.CarpideiraAlvos; i++)
                {
                    var a = Entidades[i];
                    if (!a.Viva || a.Faccao != Faccao.Inimigo || a.Vida >= a.VidaMax || Vec2.Dist2(a.Pos, e.Pos) > r2) continue;
                    a.Vida = Math.Min(a.VidaMax, a.Vida + a.VidaMax * Cfg.CarpideiraCura); curados++;
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 58, Alvo = a.Id, Origem = e.Id, Pos = e.Pos, Dir = a.Pos - e.Pos });
                }
                if (curados > 0) { e.Animacao = EstadoAnimacao.Conjurando; e.TempoAcao = 0.6f; return true; }
            }
            if (e.Especie == EspecieId.ArautoDeCinzas && Cfg.HabilidadesDeEspecie && (e.TempoHabilidade -= DT) <= 0f)
            {
                e.TempoHabilidade = Cfg.ArautoIntervalo; int dados = 0; float r2 = Cfg.ArautoRaio * Cfg.ArautoRaio;
                for (int i = 0; i < Entidades.Count && dados < Cfg.ArautoAlvos; i++)
                {
                    var a = Entidades[i];
                    if (!a.Viva || a == e || a.Faccao != Faccao.Inimigo || a.Escudo > 0f || Vec2.Dist2(a.Pos, e.Pos) > r2) continue;
                    a.Escudo = a.VidaMax * Cfg.ArautoEscudo; dados++; Resultado.EscudosDeArauto++;
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 52, Alvo = a.Id, Origem = e.Id, Pos = e.Pos, Dir = a.Pos - e.Pos });
                }
                if (dados > 0) { e.Animacao = EstadoAnimacao.Conjurando; e.TempoAcao = 0.6f; return true; }
            }
            if (e.Especie == EspecieId.Magistrado && TickAtual * DT >= e.TeleporteAte)
            {
                var encostado = HostilMaisProximo(e, e.Pos, Cfg.TeleporteDist * Cfg.TeleporteDist);
                if (encostado != null)
                {
                    Vec2 fuga = (e.Pos - encostado.Pos).Normalizado; if (fuga.X == 0f && fuga.Y == 0f) fuga = new Vec2(1f, 0f);
                    var destino = Limitar(e.Pos + fuga * Cfg.TeleporteSalto);
                    if (Vec2.Dist2(destino, e.Pos) < 9f) destino = Limitar(e.Pos + new Vec2(-fuga.Y, fuga.X) * Cfg.TeleporteSalto);   // encurralado: salta de lado
                    if (Vec2.Dist2(destino, e.Pos) < 9f) destino = Limitar(e.Pos - new Vec2(-fuga.Y, fuga.X) * Cfg.TeleporteSalto);
                    if (Vec2.Dist2(destino, e.Pos) >= 9f) { Teleportar(e, destino); return true; }
                }
            }
            if (e.Papel == Papel.Atirador && e.Recarga > 0f && !e.Elite)
            {
                var perto = HostilMaisProximo(e, e.Pos, float.MaxValue);
                float dRecuo = e.Especie == EspecieId.Sucubo ? Cfg.SucuboRecuaDist : Cfg.AtiradorInimigoRecuaDist;
                if (perto != null && Vec2.Dist2(perto.Pos, e.Pos) < dRecuo * dRecuo)
                {
                    Vec2 fuga = (e.Pos - perto.Pos).Normalizado; var destino = Limitar(e.Pos + fuga * 1.0f);
                    if (Vec2.Dist2(destino, e.Pos) > 0.25f) { MoverPara(e, destino); return true; }   // encurralado na parede: fica e atira
                }
            }
            return false;
        }

        /// <summary>Um Oculto se mostra (atacou, foi ferido ou o tempo dele acabou).</summary>
        private void Revelar(Entidade e)
        {
            if (!e.Oculto) return;
            e.Oculto = false; Resultado.OcultosRevelados++;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 45, Alvo = e.Id, Origem = -1, Pos = e.Pos });
        }

        /// <summary>Teleporte do Magistrado: some de um ponto e aparece no outro (evento 42: Pos = de onde, Dir = deslocamento).</summary>
        private void Teleportar(Entidade e, Vec2 destino)
        {
            Vec2 de = e.Pos; e.Pos = destino; e.TeleporteAte = TickAtual * DT + Cfg.TeleporteRecarga; e.ProvocadoPor = -1; Resultado.Teleportes++;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 42, Alvo = e.Id, Origem = -1, Pos = de, Dir = destino - de });
        }

        /// <summary>Um inimigo morreu: os diabretes por perto perdem a coragem e fogem (como os Caídos do Diablo 1).</summary>
        private void AoMorrerInimigoPerto(Entidade morto)
        {
            float r2 = Cfg.FugaRaio * Cfg.FugaRaio; int n = 0;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (!o.Viva || o == morto || o.Faccao != Faccao.Inimigo || o.Especie != EspecieId.Diabrete || o.Elite || o.Unico || o.Campeao || o.FugasFeitas >= Cfg.FugaMaximo || o.FugaTempo > 0f) continue;
                if (Vec2.Dist2(o.Pos, morto.Pos) > r2) continue;
                o.FugaTempo = Cfg.FugaDuracao; o.FugasFeitas++; n++;
            }
            if (n > 0) { Resultado.Fugas += n; Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 30, Valor = n, Alvo = -1, Origem = -1, Pos = morto.Pos }); }
        }

        private void Ocioso(Entidade e)
        {
            // Sala com inimigos ainda adormecidos e ninguém acordado: a linha de frente marcha até eles (atiradores e conjuradores seguram o posto).
            if (e.Faccao == Faccao.Minion && Cfg.DespertarPorProximidade && Fase == FaseExpedicao.EmOnda && HaAlvoDeMarcha && !e.Temporario
                && e.Papel != Papel.Atirador && e.Papel != Papel.Conjurador && e.Papel != Papel.Suporte && e.Postura != Postura.Defensiva)
            { MoverPara(e, AlvoDeMarcha); e.Animacao = EstadoAnimacao.Andando; return; }
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
            if (e.Oculto) { if (!Bonus.Unico[11]) multiplicador *= Cfg.OcultoGolpe; Revelar(e); }   // o golpe que sai do escuro
            e.Recarga = 1f / (e.VelAtaque * (e.SedeTempo > 0f ? 1f + Cfg.SedeBonus : 1f));
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
            if (e.SedeTempo > 0f) e.SedeTempo -= DT;
            if (e.ProvocadoTempo > 0f) { e.ProvocadoTempo -= DT; if (e.ProvocadoTempo <= 0f) e.ProvocadoPor = -1; }
            if (e.TempoAcao > 0f) { e.TempoAcao -= DT; return; } // presa na animação de ataque/conjuração

            bool inimigo = e.Faccao == Faccao.Inimigo;
            if (inimigo && Cfg.ComportamentoDeEspecie && ComportamentoDeEspecie(e)) return;
            if (inimigo && (e.Elite || e.Chefe) && GolpeBrutal(e)) return;
            if (inimigo && e.Chefe && e.Especie == EspecieId.Lazarus && TickAtual * DT >= e.TeleporteAte)
            {
                var colado = HostilMaisProximo(e, e.Pos, Cfg.LazarusTeleporteDist * Cfg.LazarusTeleporteDist);
                if (colado != null)
                {
                    Vec2 fuga = (e.Pos - colado.Pos).Normalizado; if (fuga.X == 0f && fuga.Y == 0f) fuga = new Vec2(1f, 0f);
                    var destino = Limitar(e.Pos + fuga * Cfg.TeleporteSalto);
                    if (Vec2.Dist2(destino, e.Pos) < 9f) destino = Limitar(e.Pos + new Vec2(-fuga.Y, fuga.X) * Cfg.TeleporteSalto);
                    if (Vec2.Dist2(destino, e.Pos) < 9f) destino = Limitar(e.Pos - new Vec2(-fuga.Y, fuga.X) * Cfg.TeleporteSalto);
                    if (Vec2.Dist2(destino, e.Pos) >= 9f) { Teleportar(e, destino); return; }
                }
            }
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
                    default: // Lutador e Enxame: o mais próximo; minions concentram no mais ferido dali
                        alvo = HostilMaisProximo(e, e.Pos, filtro2);
                        if (!inimigo && alvo != null && alvo.Id != MarcadoId) alvo = MaisFeridoPerto(e, alvo);
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
                // Bestas não vêm em linha reta: a matilha abre para os lados e fecha o cerco pelo flanco do alvo.
                if (inimigo && Cfg.FlancoDasBestas && e.Familia == Familia.Besta && !e.Chefe && e.ProvocadoPor < 0 && d > Cfg.FlancoDistDireta)
                {
                    Vec2 dir = (alvo.Pos - e.Pos).Normalizado; float lado = (e.Id & 1) == 0 ? 1f : -1f;
                    var destino = Limitar(alvo.Pos + new Vec2(-dir.Y, dir.X) * (lado * Cfg.FlancoAbertura * Math.Min(1f, d / 4f)));
                    if (!e.Flanqueando)
                    {
                        e.Flanqueando = true; Resultado.Flancos++;
                        if (_ondaDoUltimoFlanco != Onda * 1000 + Andar) { _ondaDoUltimoFlanco = Onda * 1000 + Andar; Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 37, Alvo = e.Id, Origem = -1, Pos = e.Pos }); }
                    }
                    if (Vec2.Dist2(destino, e.Pos) > 0.09f) { MoverPara(e, destino); return; }
                }
                MoverPara(e, alvo.Pos);
            }
        }
        private int _ondaDoUltimoFlanco = -1;

        /// <summary>Foco: entre os hostis colados no alvo mais próximo, prefere o de menor vida (o exército termina o que começou).</summary>
        private Entidade MaisFeridoPerto(Entidade e, Entidade proximo)
        {
            Entidade melhor = proximo;
            float r2 = Cfg.RaioDeFoco * Cfg.RaioDeFoco;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var o = Entidades[i];
                if (o == proximo || !o.Viva || !Hostil(e, o) || Vec2.Dist2(o.Pos, proximo.Pos) > r2) continue;
                if (o.Vida < melhor.Vida) melhor = o;
            }
            return melhor;
        }

        /// <summary>
        /// Golpe Brutal de elites e chefes: param, avisam por GolpeBrutalAviso segundos e então ferem todos em volta.
        /// O jogador interrompe marcando o inimigo durante o aviso (ele fica atordoado). Retorna true se gastou o tick.
        /// </summary>
        private bool GolpeBrutal(Entidade e)
        {
            if (e.PreparandoGolpe > 0f)
            {
                e.Animacao = EstadoAnimacao.Conjurando;
                e.PreparandoGolpe -= DT;
                if (e.PreparandoGolpe > 0f) return true;
                float r2 = Cfg.GolpeBrutalRaio * Cfg.GolpeBrutalRaio;
                Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = e.Id, Alvo = -1, Pos = e.Pos, Valor = Cfg.GolpeBrutalRaio, TipoDano = e.TipoDano });
                int n = Entidades.Count;
                for (int i = 0; i < n; i++)
                {
                    var o = Entidades[i];
                    if (o.Viva && Hostil(e, o) && Vec2.Dist2(o.Pos, e.Pos) <= r2) CausarDano(e, o, e.Dano * Cfg.GolpeBrutalMult, e.TipoDano, true);
                }
                Resultado.GolpesBrutais++;
                e.TempoAcao = 0.4f;
                return true;
            }
            e.RecargaGolpe -= DT;
            if (e.RecargaGolpe > 0f) return false;
            var perto = HostilMaisProximo(e, e.Pos, float.MaxValue);
            if (perto == null || Vec2.Dist2(perto.Pos, e.Pos) > Cfg.GolpeBrutalRaio * Cfg.GolpeBrutalRaio) return false;
            e.RecargaGolpe = Cfg.GolpeBrutalRecarga;
            e.PreparandoGolpe = Cfg.GolpeBrutalAviso;
            e.Animacao = EstadoAnimacao.Conjurando;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 9, Alvo = e.Id, Origem = e.Id, Valor = Cfg.GolpeBrutalAviso, Pos = e.Pos });
            return true;
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

        /// <summary>Lázarus: acende uma muralha de fogo atravessada sobre a linha de frente e chama uma Súcubo (até o limite em campo).</summary>
        private void RitoDeLazarus(Entidade e)
        {
            e.TempoHabilidade = Cfg.LazarusIntervalo; e.TempoAcao = 1.0f; e.Animacao = EstadoAnimacao.Conjurando;
            var alvo = HostilMaisProximo(e, e.Pos, float.MaxValue);   // a linha de frente do exército, não o necromante lá atrás
            if (alvo != null)
            {
                Vec2 dir = (alvo.Pos - e.Pos).Normalizado; if (dir.X == 0f && dir.Y == 0f) dir = new Vec2(-1f, 0f);
                Vec2 perp = new Vec2(-dir.Y, dir.X) * (Cfg.LazarusMuralhaComprimento * 0.5f);
                var m = new MuralhaDeFogo { Id = _proximoId++, A = Limitar(alvo.Pos - perp), B = Limitar(alvo.Pos + perp), Tempo = Cfg.LazarusMuralhaDuracao, Raio = 0.9f, Dano = e.Dano * Cfg.LazarusMuralhaDano, Inimiga = true, DonoId = e.Id };
                Muralhas.Add(m); Resultado.MuralhasDeLazarus++;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 39, Alvo = m.Id, Origem = e.Id, Pos = m.A, Dir = m.B - m.A, Valor = m.Tempo, TipoDano = TipoDano.Fogo, Critico = true });
            }
            int servas = 0;
            for (int i = 0; i < Entidades.Count; i++) if (Entidades[i].Viva && Entidades[i].Faccao == Faccao.Inimigo && Entidades[i].Especie == EspecieId.Sucubo) servas++;
            if (servas < Cfg.LazarusMaximoDeSucubos) CriarInimigo(EspecieId.Sucubo, Limitar(new Vec2(e.Pos.X + Rng.Intervalo(-2f, 2f), e.Pos.Y + Rng.Intervalo(-2f, 2f))));
        }

        /// <summary>O Carcereiro de Almas: acorrenta os minions de mais vida e chama uma Aparição (até o limite em campo).</summary>
        private void Grilhoes(Entidade e)
        {
            e.TempoHabilidade = Cfg.GrilhoesIntervalo;
            e.TempoAcao = 1.0f;
            e.Animacao = EstadoAnimacao.Conjurando;
            for (int k = 0; k < Cfg.GrilhoesAlvos; k++)
            {
                Entidade alvo = null;
                for (int i = 0; i < _minions.Count; i++)
                {
                    var m = _minions[i];
                    if (m.Viva && m.TempoAcao < Cfg.GrilhoesDuracao * 0.5f && (alvo == null || m.Vida > alvo.Vida)) alvo = m;
                }
                if (alvo == null) break;
                alvo.TempoAcao = Cfg.GrilhoesDuracao; alvo.Animacao = EstadoAnimacao.Parado;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 12, Alvo = alvo.Id, Origem = e.Id, Valor = Cfg.GrilhoesDuracao, Pos = alvo.Pos });
            }
            int servas = 0;
            for (int i = 0; i < Entidades.Count; i++) if (Entidades[i].Viva && Entidades[i].Faccao == Faccao.Inimigo && Entidades[i].Especie == EspecieId.Aparicao) servas++;
            if (servas < Cfg.GrilhoesMaximoDeAparicoes)
                CriarInimigo(EspecieId.Aparicao, Limitar(new Vec2(e.Pos.X + Rng.Intervalo(-2f, 2f), e.Pos.Y + Rng.Intervalo(-2f, 2f))));
        }

        /// <summary>A Abadessa Costurada: periodicamente invoca uma poça de sangue que a cura. Retorna true se gastou o tick.</summary>
        private bool HabilidadeDoChefe(Entidade e)
        {
            e.TempoHabilidade -= DT;
            if (e.TempoHabilidade > 0f) return false;
            if (e.Especie == EspecieId.CarcereiroDeAlmas) { Grilhoes(e); return true; }
            if (e.Especie == EspecieId.Lazarus) { RitoDeLazarus(e); return true; }
            if (e.Especie == EspecieId.Diablo)
            {   // Apocalipse: um anel de fogo estoura em volta dele e fere todo aliado dentro do raio
                e.TempoHabilidade = Cfg.ApocalipseIntervalo; e.TempoAcao = 0.8f; e.Animacao = EstadoAnimacao.Conjurando; Resultado.Apocalipses++;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 43, Alvo = e.Id, Origem = e.Id, Pos = e.Pos, Valor = Cfg.ApocalipseRaio, TipoDano = TipoDano.Fogo });
                _explosoes.Add(new ExplosaoPendente { Pos = e.Pos, Dano = e.Dano * Cfg.ApocalipseDano, Raio = Cfg.ApocalipseRaio, AtingeInimigos = false, OrigemId = e.Id, Tipo = TipoDano.Fogo });
                return true;
            }
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
