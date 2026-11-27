using System;

namespace Necro.Nucleo
{
    public sealed partial class Simulacao
    {
        /// <summary>Prioridade padrão, usada quando o jogador não configurou nenhuma Ordem.</summary>
        public static readonly Ordem[] OrdensPadrao =
        {
            new Ordem(CondicaoOrdem.OrbeDeAlmaPresente, 0f, AcaoNecromante.CeifarAlma),
            new Ordem(CondicaoOrdem.MinionMortoDeCustoMinimo, 1f, AcaoNecromante.Reerguer),
            new Ordem(CondicaoOrdem.VidaAbaixoDe, 50f, AcaoNecromante.DrenarVida),
            new Ordem(CondicaoOrdem.ChefePresente, 0f, AcaoNecromante.MaldicaoDaFraqueza),
            new Ordem(CondicaoOrdem.ElitePresente, 0f, AcaoNecromante.MaldicaoDaFraqueza),
            new Ordem(CondicaoOrdem.CadaveresPeloMenos, 3f, AcaoNecromante.ExplosaoDeCadaver),
            new Ordem(CondicaoOrdem.ManaAcimaDe, 50f, AcaoNecromante.LancaDeOssos),
            new Ordem(CondicaoOrdem.Sempre, 0f, AcaoNecromante.AtaqueBasico),
        };

        /// <summary>Recarga restante (s) de um feitiço, para a interface.</summary>
        public float RecargaDoFeitico(FeiticoId id) { return _recargaFeitico[(int)id] > 0f ? _recargaFeitico[(int)id] : 0f; }

        private void AtualizarNecromante()
        {
            var n = Necromante;
            n.Mana = Math.Min(n.ManaMax, n.Mana + _regenMana * DT);
            n.Vida = Math.Min(n.VidaMax, n.Vida + Cfg.RegenVidaPorSegundo * DT);
            for (int i = 0; i < _recargaFeitico.Length; i++) if (_recargaFeitico[i] > 0f) _recargaFeitico[i] -= DT;
            if (n.Recarga > 0f) n.Recarga -= DT;
            if (n.FraquezaTempo > 0f) n.FraquezaTempo -= DT;
            if (_ceifaEspera > 0f) _ceifaEspera -= DT;
            if (Cfg.PocoesNoCinto) BeberPocoes();
            // Único 2, Coroa do Rei Morto: durante a luta, um morto se ergue de tempos em tempos para lutar por você
            if (Bonus.Unico[2] && InimigosVivos > 0) { _coroaTempo += DT; if (_coroaTempo >= Cfg.CoroaIntervalo) { _coroaTempo = 0f; CriarZumbiTemporario(new Vec2(n.Pos.X + 1.5f, n.Pos.Y), Cfg.CoroaDuracao); Resultado.EfeitosDeUnicos++; } }

            // Sala especial: caminha até o objeto (baú, santuário...) se não houver alma para ceifar.
            if (Fase == FaseExpedicao.Explorando && !CaminhandoAteASala && _ceifaOrbe == null && Orbes.Count == 0)
            {
                var alvoSala = new Vec2(PosDoObjeto.X - 1.1f, PosDoObjeto.Y);
                if (!_temPosto) { _postoNecro = n.Pos; _temPosto = true; }   // depois da sala ele volta para trás do exército
                if (Vec2.Dist2(n.Pos, alvoSala) > 0.04f) { MoverPara(n, alvoSala); n.Animacao = EstadoAnimacao.Andando; }
                else { n.Animacao = EstadoAnimacao.Conjurando; n.Olhar = new Vec2(1f, 0f); }
                return;
            }

            // Canalizando a ceifa: não faz mais nada até concluir, ser interrompido ou o orbe sumir.
            if (_ceifaOrbe != null)
            {
                n.Animacao = EstadoAnimacao.Conjurando;
                if (Cfg.RaioAtracaoDaCeifa > 0f)
                {
                    // variante experimental: a alma sendo arrancada atrai os inimigos próximos para o necromante
                    float r2 = Cfg.RaioAtracaoDaCeifa * Cfg.RaioAtracaoDaCeifa;
                    for (int i = 0; i < Entidades.Count; i++)
                    {
                        var e = Entidades[i];
                        if (!e.Viva || e.Faccao != Faccao.Inimigo || Vec2.Dist2(e.Pos, n.Pos) > r2) continue;
                        e.ProvocadoPor = n.Id; e.ProvocadoTempo = 0.5f;
                    }
                }
                _ceifaTempo += DT;
                if (_ceifaTempo >= _tempoDeCeifa - DT * 0.5f) ConcluirCeifa();
                return;
            }
            if (n.TempoAcao > 0f) { n.TempoAcao -= DT; return; }
            if (LancarGolem()) return;
            if (LancarMuralha()) return;
            if (LancarRelampago()) return;   // o Golem vem antes das ordens: é o escudo do exército

            // Ordens "SE condição ENTÃO ação", de cima para baixo; a primeira que puder agir encerra o tick.
            bool agiu = false;
            if (_ordens.Count > 0)
            {
                for (int i = 0; i < _ordens.Count && !agiu; i++)
                    if (CondicaoVerdadeira(_ordens[i])) agiu = TentarAcao(_ordens[i].Acao);
                // Rede de segurança quando nenhuma ordem do jogador dispara: ceifar e ataque básico.
                if (!agiu) agiu = TentarAcao(AcaoNecromante.CeifarAlma) || TentarAcao(AcaoNecromante.AtaqueBasico);
            }
            else
            {
                for (int i = 0; i < OrdensPadrao.Length && !agiu; i++)
                    if (CondicaoVerdadeira(OrdensPadrao[i])) agiu = TentarAcao(OrdensPadrao[i].Acao);
            }
            if (NecromanteCorpoACorpo && !agiu)
            {
                // de foice: sem nada melhor a fazer, avança no inimigo mais próximo; sem inimigos, volta para perto do exército
                var perto = HostilMaisProximo(n, n.Pos, float.MaxValue);
                Vec2 destino = perto != null ? perto.Pos : new Vec2(Cfg.NecromanteX + 2f, Cfg.AlturaArena * 0.5f);
                Vec2 dd = destino - n.Pos;
                if (dd.X * dd.X + dd.Y * dd.Y > (perto != null ? n.Alcance * n.Alcance * 0.6f : 0.25f)) { MoverPara(n, destino); n.Animacao = EstadoAnimacao.Andando; agiu = true; }
            }
            if (!NecromanteCorpoACorpo && _temPosto && Orbes.Count == 0)
            {
                Vec2 volta = _postoNecro - n.Pos;
                if (volta.X * volta.X + volta.Y * volta.Y > 0.09f && !agiu) { MoverPara(n, _postoNecro); n.Animacao = EstadoAnimacao.Andando; agiu = true; }
            }
            if (!agiu) n.Animacao = EstadoAnimacao.Parado;
        }

        /// <summary>Avalia a condição de uma Ordem contra o estado atual da arena.</summary>
        public bool CondicaoVerdadeira(Ordem o)
        {
            var n = Necromante;
            switch (o.Condicao)
            {
                case CondicaoOrdem.Sempre: return true;
                case CondicaoOrdem.VidaAbaixoDe: return n.Vida < n.VidaMax * o.Parametro * 0.01f;
                case CondicaoOrdem.ManaAcimaDe: return n.Mana > n.ManaMax * o.Parametro * 0.01f;
                case CondicaoOrdem.CadaveresPeloMenos: return Cadaveres.Count >= (int)o.Parametro;
                case CondicaoOrdem.InimigosPeloMenos: return InimigosVivos >= (int)o.Parametro;
                case CondicaoOrdem.OrbeDeAlmaPresente: return Orbes.Count > 0;
                case CondicaoOrdem.ChefePresente:
                    for (int i = 0; i < Entidades.Count; i++) if (Entidades[i].Viva && Entidades[i].Chefe) return true;
                    return false;
                case CondicaoOrdem.ElitePresente:
                    for (int i = 0; i < Entidades.Count; i++) if (Entidades[i].Viva && Entidades[i].Elite) return true;
                    return false;
                case CondicaoOrdem.MinionMortoDeCustoMinimo:
                    for (int i = 0; i < Cadaveres.Count; i++) if (Cadaveres[i].DeMinion && Cadaveres[i].Custo >= (int)o.Parametro) return true;
                    return false;
                case CondicaoOrdem.MinionComVidaAbaixoDe:
                    for (int i = 0; i < _minions.Count; i++)
                        if (_minions[i].Viva && _minions[i].Vida < _minions[i].VidaMax * o.Parametro * 0.01f) return true;
                    return false;
            }
            return false;
        }

        private bool TentarAcao(AcaoNecromante acao)
        {
            switch (acao)
            {
                case AcaoNecromante.CeifarAlma: return IniciarCeifa();
                case AcaoNecromante.AtaqueBasico: return AtaqueBasico();
                case AcaoNecromante.LancaDeOssos: return LancarLancaDeOssos();
                case AcaoNecromante.DrenarVida: return LancarDrenarVida();
                case AcaoNecromante.ExplosaoDeCadaver: return LancarExplosaoDeCadaver();
                case AcaoNecromante.Reerguer: return LancarReerguer();
                case AcaoNecromante.MaldicaoDaFraqueza: return LancarMaldicao();
                case AcaoNecromante.Sacrificio: return LancarSacrificio();
            }
            return false;
        }

        // ---------------------------------------------------------------------
        //  Ceifa de almas
        // ---------------------------------------------------------------------

        private Vec2 _postoNecro;
        private bool _temPosto;

        private bool IniciarCeifa()
        {
            if (Orbes.Count == 0 || _ceifaEspera > 0f) return false;
            // Prioriza o orbe que vai sumir primeiro.
            OrbeDeAlma alvo = Orbes[0];
            for (int i = 1; i < Orbes.Count; i++) if (Orbes[i].TempoRestante < alvo.TempoRestante) alvo = Orbes[i];
            if (Cfg.AlcanceCeifa > 0f)
            {
                // variante experimental: precisa chegar perto do orbe antes de canalizar
                Vec2 d = alvo.Pos - Necromante.Pos;
                if (d.X * d.X + d.Y * d.Y > Cfg.AlcanceCeifa * Cfg.AlcanceCeifa)
                {
                    if (!_temPosto) { _postoNecro = Necromante.Pos; _temPosto = true; }
                    MoverPara(Necromante, alvo.Pos);
                    Necromante.Animacao = EstadoAnimacao.Andando;
                    return true;
                }
            }
            _ceifaOrbe = alvo;
            _ceifaTempo = 0f;
            _ceifaDano = 0f;
            Necromante.Animacao = EstadoAnimacao.Conjurando;
            Olhar(Necromante, alvo.Pos);
            Emitir(new EventoSim { Tipo = TipoEvento.CeifaIniciada, Alvo = alvo.Id, Origem = Necromante.Id, Pos = alvo.Pos });
            return true;
        }

        private void InterromperCeifa()
        {
            Resultado.CeifasInterrompidas++;
            Emitir(new EventoSim { Tipo = TipoEvento.CeifaInterrompida, Alvo = _ceifaOrbe.Id, Origem = Necromante.Id, Valor = _ceifaDano, Pos = _ceifaOrbe.Pos });
            _ceifaOrbe = null;
            _ceifaTempo = 0f;
            _ceifaDano = 0f;
            _ceifaEspera = Cfg.EsperaAposInterrupcao; // tenta de novo logo depois, enquanto o orbe existir
        }

        private void ConcluirCeifa()
        {
            var o = _ceifaOrbe;
            _ceifaOrbe = null;
            _ceifaTempo = 0f;
            EntregarAlma(o, false);
        }

        /// <summary>A alma do orbe entra no resultado da expedição (ceifa canalizada ou por toque; Critico = melhorada pelo toque).</summary>
        private void EntregarAlma(OrbeDeAlma o, bool melhorada)
        {
            Orbes.Remove(o);
            Resultado.Almas.Add(o.Alma);
            // Único 5, Anel dos Mil Sussurros: cada alma ceifada cura os minions
            if (Bonus.Unico[5]) { for (int i = 0; i < _minions.Count; i++) if (_minions[i].Viva) _minions[i].Vida = Math.Min(_minions[i].VidaMax, _minions[i].Vida + _minions[i].VidaMax * Cfg.AnelCura); Resultado.EfeitosDeUnicos++; }
            Emitir(new EventoSim { Tipo = TipoEvento.AlmaCeifada, Alvo = o.Id, Origem = Necromante.Id, Pos = o.Pos, Especie = o.Alma.Especie, Extra = (int)o.Alma.Qualidade, Critico = melhorada });
            // Foice do Ceifeiro Faminto: cada alma ceifada cura todos os minions vivos.
            if (Bonus.CeifeiroFaminto)
            {
                for (int i = 0; i < Entidades.Count; i++)
                {
                    var m = Entidades[i];
                    if (m.Viva && m.Faccao == Faccao.Minion) Curar(Necromante, m, m.VidaMax * Cfg.CeifeiroFamintoCura);
                }
            }
        }

        // ---------------------------------------------------------------------
        //  Ataque básico e feitiços
        // ---------------------------------------------------------------------

        private Entidade InimigoMaisProximoNoAlcance(float alcance)
        {
            var alvo = HostilMaisProximo(Necromante, Necromante.Pos, float.MaxValue);
            if (alvo == null || Vec2.Dist2(alvo.Pos, Necromante.Pos) > alcance * alcance) return null;
            return alvo;
        }

        private bool AtaqueBasico()
        {
            var n = Necromante;
            if (n.Recarga > 0f) return false;
            var alvo = InimigoMaisProximoNoAlcance(n.Alcance);
            if (alvo == null) return false;
            n.Recarga = 1f / n.VelAtaque;
            n.TempoAcao = 0.2f;
            n.Animacao = EstadoAnimacao.Atacando;
            Olhar(n, alvo.Pos);
            Emitir(new EventoSim { Tipo = TipoEvento.Ataque, Origem = n.Id, Alvo = alvo.Id, Pos = n.Pos, Dir = n.Olhar, TipoDano = n.TipoDano });
            CausarDano(n, alvo, n.Dano, n.TipoDano, true);
            if (NecromanteCorpoACorpo)
            {
                // a foice corta em arco: fere também os outros inimigos ao alcance (até FoiceAlvosExtras)
                int extras = 0, total = Entidades.Count; float a2 = n.Alcance * n.Alcance;
                for (int i = 0; i < total && extras < Cfg.FoiceAlvosExtras; i++)
                {
                    var o = Entidades[i];
                    if (o == alvo || !o.Viva || o.Faccao != Faccao.Inimigo || Vec2.Dist2(o.Pos, n.Pos) > a2) continue;
                    CausarDano(n, o, n.Dano, n.TipoDano, true); extras++;
                }
            }
            return true;
        }

        private bool PodeLancar(FeiticoId id)
        {
            return _recargaFeitico[(int)id] <= 0f && Necromante.Mana >= Fei(id).CustoMana;
        }

        private void PagarFeitico(FeiticoId id, Entidade alvo, Vec2 ponto, float valor)
        {
            var f = Fei(id);
            var n = Necromante;
            n.Mana -= f.CustoMana;
            _recargaFeitico[(int)id] = f.Recarga * _fatorRecarga;
            // Único 7, Grimório de Pele Humana: às vezes o feitiço não entra em recarga (o Golem não conta)
            if (Bonus.Unico[7] && id != FeiticoId.Golem && id != FeiticoId.MuralhaDeFogo && id != FeiticoId.RelampagoEmCadeia && Rng.Chance(Cfg.GrimorioSemRecarga)) { _recargaFeitico[(int)id] = 0f; Resultado.EfeitosDeUnicos++; }
            n.TempoAcao = Cfg.TempoDeConjuracao;
            n.Animacao = EstadoAnimacao.Conjurando;
            Olhar(n, ponto);
            Emitir(new EventoSim { Tipo = TipoEvento.FeiticoLancado, Origem = n.Id, Alvo = alvo != null ? alvo.Id : -1, Extra = (int)id, Pos = ponto, Dir = n.Olhar, Valor = valor });
        }

        /// <summary>Lança de Ossos: dano Físico em linha, atravessando todos os inimigos no caminho.</summary>
        private bool LancarLancaDeOssos()
        {
            if (!PodeLancar(FeiticoId.LancaDeOssos)) return false;
            var f = Fei(FeiticoId.LancaDeOssos);
            var alvo = InimigoMaisProximoNoAlcance(f.Alcance);
            if (alvo == null) return false;
            var n = Necromante;
            Vec2 dir = (alvo.Pos - n.Pos).Normalizado;
            PagarFeitico(FeiticoId.LancaDeOssos, alvo, alvo.Pos, f.Alcance);
            // a lança é um projétil perfurante: atravessa a linha e fere cada inimigo quando passa por ele
            var pr = new Projetil { Id = _proxProjetil++, Origem = n, Alvo = null, EspecieOrigem = n.Especie, Pos = n.Pos, Destino = n.Pos + dir * f.Alcance,
                                    Dir = dir, Dano = n.Dano * f.Potencia, TipoDano = TipoDano.Fisico, Perfurante = true, Restante = f.Alcance,
                                    Raio = f.Raio, Velocidade = Cfg.VelLancaDeOssos };
            Projeteis.Add(pr);
            Emitir(new EventoSim { Tipo = TipoEvento.ProjetilLancado, Origem = n.Id, Alvo = pr.Id, Pos = n.Pos, Dir = dir, Especie = n.Especie, TipoDano = TipoDano.Fisico, Extra = 1, Valor = f.Alcance });
            return true;
        }

        /// <summary>Drenar Vida: dano de Sombra em um alvo; o necromante recupera o dano causado.</summary>
        private bool LancarDrenarVida()
        {
            if (!PodeLancar(FeiticoId.DrenarVida)) return false;
            var f = Fei(FeiticoId.DrenarVida);
            var alvo = InimigoMaisProximoNoAlcance(f.Alcance);
            if (alvo == null) return false;
            PagarFeitico(FeiticoId.DrenarVida, alvo, alvo.Pos, 0f);
            float dano = CausarDano(Necromante, alvo, Necromante.Dano * f.Potencia, TipoDano.Sombra, true);
            Curar(Necromante, Necromante, dano);
            return true;
        }

        /// <summary>Explosão de Cadáver: consome até 3 cadáveres de inimigos próximos entre si e fere os inimigos em volta.</summary>
        private bool LancarExplosaoDeCadaver()
        {
            if (!PodeLancar(FeiticoId.ExplosaoDeCadaver)) return false;
            var f = Fei(FeiticoId.ExplosaoDeCadaver);
            float r2 = f.Raio * f.Raio, a2 = f.Alcance * f.Alcance;
            Cadaver melhor = null;
            int melhorN = 0;
            for (int i = 0; i < Cadaveres.Count; i++)
            {
                var c = Cadaveres[i];
                if (c.DeMinion || Vec2.Dist2(c.Pos, Necromante.Pos) > a2) continue; // cadáveres de minions ficam para o Reerguer
                int nIni = 0;
                for (int j = 0; j < Entidades.Count; j++)
                {
                    var e = Entidades[j];
                    if (e.Viva && e.Faccao == Faccao.Inimigo && Vec2.Dist2(e.Pos, c.Pos) <= r2) nIni++;
                }
                if (nIni > melhorN) { melhorN = nIni; melhor = c; }
            }
            if (melhor == null) return false;

            Vec2 centro = melhor.Pos;
            PagarFeitico(FeiticoId.ExplosaoDeCadaver, null, centro, f.Raio);
            float dano = 0f;
            int consumidos = 0;
            for (int i = Cadaveres.Count - 1; i >= 0 && consumidos < Cfg.ExplosaoCadaverMaxCorpos; i--)
            {
                var c = Cadaveres[i];
                if (c.DeMinion || (c != melhor && Vec2.Dist2(c.Pos, centro) > 4f)) continue;
                dano += Necromante.Dano * f.Potencia + c.VidaMax * Cfg.ExplosaoCadaverFracaoVida;
                consumidos++;
                Emitir(new EventoSim { Tipo = TipoEvento.CadaverConsumido, Alvo = c.Id, Pos = c.Pos, Extra = (int)FeiticoId.ExplosaoDeCadaver, Origem = Necromante.Id });
                Cadaveres.RemoveAt(i);
            }
            Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = Necromante.Id, Alvo = -1, Pos = centro, Valor = f.Raio, TipoDano = TipoDano.Fisico });
            int total = Entidades.Count;
            for (int i = 0; i < total; i++)
            {
                var e = Entidades[i];
                if (e.Viva && e.Faccao == Faccao.Inimigo && Vec2.Dist2(e.Pos, centro) <= r2) CausarDano(Necromante, e, dano, TipoDano.Fisico, true);
            }
            // Grimório da Mãe Carniça: a explosão deixa um Zumbi temporário no lugar.
            if (Bonus.MaeCarnica) CriarZumbiTemporario(centro, Cfg.MaeCarnicaDuracao);
            return true;
        }

        /// <summary>Reerguer: traz de volta o minion morto de maior custo, consumindo o cadáver dele.</summary>
        private bool LancarReerguer()
        {
            if (!PodeLancar(FeiticoId.Reerguer)) return false;
            int idx = -1;
            for (int i = 0; i < Cadaveres.Count; i++)
                if (Cadaveres[i].DeMinion && Cadaveres[i].Minion != null && !Cadaveres[i].Minion.Viva && (idx < 0 || Cadaveres[i].Custo > Cadaveres[idx].Custo)) idx = i;
            if (idx < 0) return false;
            var c = Cadaveres[idx];
            var f = Fei(FeiticoId.Reerguer);
            PagarFeitico(FeiticoId.Reerguer, c.Minion, c.Pos, 0f);
            Cadaveres.RemoveAt(idx);
            Emitir(new EventoSim { Tipo = TipoEvento.CadaverConsumido, Alvo = c.Id, Pos = c.Pos, Extra = (int)FeiticoId.Reerguer, Origem = Necromante.Id });
            Reviver(c.Minion, c.Pos, f.Potencia);
            Emitir(new EventoSim { Tipo = TipoEvento.MinionReerguido, Alvo = c.Minion.Id, Origem = Necromante.Id, Pos = c.Pos, Especie = c.Especie });
            return true;
        }

        /// <summary>Maldição da Fraqueza: -20% de dano dos inimigos na área, por 8 s. Prioriza chefe/elite.</summary>
        private bool LancarMaldicao()
        {
            if (!PodeLancar(FeiticoId.MaldicaoDaFraqueza)) return false;
            var f = Fei(FeiticoId.MaldicaoDaFraqueza);
            float a2 = f.Alcance * f.Alcance;
            Entidade alvo = null;
            float melhorD = float.MaxValue;
            bool melhorEspecial = false;
            for (int i = 0; i < Entidades.Count; i++)
            {
                var e = Entidades[i];
                if (!e.Viva || e.Faccao != Faccao.Inimigo || e.FraquezaTempo > 0f) continue;
                float d = Vec2.Dist2(e.Pos, Necromante.Pos);
                if (d > a2) continue;
                bool especial = e.Chefe || e.Elite;
                if (alvo == null || (especial && !melhorEspecial) || (especial == melhorEspecial && d < melhorD)) { alvo = e; melhorD = d; melhorEspecial = especial; }
            }
            if (alvo == null) return false;
            Vec2 centro = alvo.Pos;
            PagarFeitico(FeiticoId.MaldicaoDaFraqueza, alvo, centro, f.Raio);
            for (int i = 0; i < Entidades.Count; i++)
            {
                var e = Entidades[i];
                if (!e.Viva || e.Faccao != Faccao.Inimigo || Vec2.Dist2(e.Pos, centro) > f.Raio * f.Raio) continue;
                e.FraquezaTempo = f.Duracao;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 1, Alvo = e.Id, Origem = Necromante.Id, Valor = f.Duracao, Pos = e.Pos });
            }
            return true;
        }

        /// <summary>Sacrifício: destrói o minion mais barato (sem deixar cadáver); os outros causam +50% de dano por 8 s.</summary>
        private bool LancarSacrificio()
        {
            if (!PodeLancar(FeiticoId.Sacrificio) || SacrificioTempo > 0f || InimigosVivos == 0) return false;
            Entidade vitima = null;
            int vivos = 0;
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                if (!m.Viva) continue;
                vivos++;
                if (vitima == null || m.Custo < vitima.Custo || (m.Custo == vitima.Custo && m.Vida < vitima.Vida)) vitima = m;
            }
            if (vivos < 2) return false;
            var f = Fei(FeiticoId.Sacrificio);
            PagarFeitico(FeiticoId.Sacrificio, vitima, vitima.Pos, 0f);
            DestruirMinionSemCadaver(vitima, Necromante.Id);
            SacrificioTempo = f.Duracao;
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 3, Alvo = -1, Origem = Necromante.Id, Valor = f.Duracao });
            return true;
        }
    }
}
