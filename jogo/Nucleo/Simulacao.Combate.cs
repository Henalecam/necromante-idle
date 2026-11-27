using System;

namespace Necro.Nucleo
{
    public sealed partial class Simulacao
    {
        /// <summary>Mitigação total do alvo contra um tipo de dano no andar atual (com tetos).</summary>
        public float MitigacaoDe(Entidade alvo, TipoDano tipo)
        {
            return Formulas.MitigacaoTotal(alvo.Armadura, alvo.Resistencias[(int)tipo], tipo, Andar, Cfg);
        }

        /// <summary>
        /// Aplica dano seguindo a fórmula do design. "golpe" = ataque/feitiço de uma unidade (pode critar e executar).
        /// atacante pode ser null (dano de ambiente/explosão). Retorna o dano que entrou na vida do alvo.
        /// </summary>
        public float CausarDano(Entidade atacante, Entidade alvo, float danoBase, TipoDano tipo, bool golpe)
        {
            if (alvo == null || !alvo.Viva || danoBase <= 0f) return 0f;
            // Habilidade da espécie — Esqueleto erguido: apara o primeiro golpe que recebe em cada sala
            if (golpe && atacante != null && alvo.Faccao == Faccao.Minion && alvo.Especie == EspecieId.Esqueleto && !alvo.Aparou && Cfg.HabilidadesDeEspecie)
            {
                alvo.Aparou = true;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 16, Alvo = alvo.Id, Origem = atacante.Id, Pos = alvo.Pos });
                return 0f;
            }

            // Aparição erguida: incorpórea, um a cada tantos golpes simplesmente a atravessa
            if (golpe && atacante != null && alvo.Faccao == Faccao.Minion && alvo.Especie == EspecieId.Aparicao && Cfg.HabilidadesDeEspecie && ++alvo.GolpesSofridos >= Cfg.IncorporeaACada)
            {
                alvo.GolpesSofridos = 0;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 19, Alvo = alvo.Id, Origem = atacante.Id, Pos = alvo.Pos });
                return 0f;
            }

            float aumentos = 0f, mais = 1f, fracaoFogo = 0f;
            bool critico = false;
            if (atacante != null)
            {
                aumentos = atacante.AumentoDano;
                mais = atacante.MaisDano;
                if (atacante.FraquezaTempo > 0f) mais *= 1f - Fei(FeiticoId.MaldicaoDaFraqueza).Potencia;
                if (atacante.Faccao == Faccao.Minion && SacrificioTempo > 0f) mais *= 1f + Fei(FeiticoId.Sacrificio).Potencia;
                if (atacante.Faccao == Faccao.Minion && FrenesiTempo > 0f) mais *= 1f + Cfg.FrenesiBonusDeDano;
                if (atacante.Faccao == Faccao.Minion) mais *= _pDanoMinion;
                if (alvo.Id == MarcadoId && atacante.Faccao != Faccao.Inimigo) mais *= 1f + Cfg.MarcaBonusDeDano;
                if (atacante.Faccao == Faccao.Inimigo) mais *= _fatorFuria * _pDanoInimigo * _aDanoInimigo * _bProtecao;
                else mais *= _bDanoAliado;
                if (golpe && Cfg.HabilidadesDeEspecie && atacante.Faccao == Faccao.Minion)
                {
                    // Arqueiro Ossudo: a primeira flecha num alvo ainda inteiro entra mais fundo
                    if (atacante.Especie == EspecieId.ArqueiroOssudo && alvo.Vida >= alvo.VidaMax * Cfg.OlhoMortoLimiar) mais *= 1f + Cfg.OlhoMortoBonus;
                    // Diabrete: cada outro diabrete vivo ao lado o deixa mais atrevido
                    else if (atacante.Especie == EspecieId.Diabrete)
                    {
                        int outros = 0;
                        for (int i = 0; i < _minions.Count; i++) { var o = _minions[i]; if (o != atacante && o.Viva && o.Especie == EspecieId.Diabrete) outros++; }
                        if (outros > Cfg.MatilhaMaximo) outros = Cfg.MatilhaMaximo;
                        mais *= 1f + outros * Cfg.MatilhaBonus;
                    }
                    // Diabo de Guerra: ferido, bate mais forte
                    else if (atacante.Especie == EspecieId.DiaboDeGuerra && atacante.Vida < atacante.VidaMax * Cfg.FuriaDeGuerraLimiar) mais *= 1f + Cfg.FuriaDeGuerraBonus;
                    // Rês Profana: a primeira chifrada de cada sala é uma investida
                    else if (atacante.Especie == EspecieId.ResProfana && !atacante.Aparou) { atacante.Aparou = true; mais *= Cfg.InvestidaMult; }
                    // Acólito Caído: o golpe deixa um agouro que enfraquece quem não é chefe
                    else if (atacante.Especie == EspecieId.AcolitoCaido && !alvo.Chefe && alvo.FraquezaTempo < Cfg.AgouroDuracao) alvo.FraquezaTempo = Cfg.AgouroDuracao;
                }
                // Anel do Rei Solitário: +100% de dano do necromante enquanto não houver nenhum minion vivo.
                if (Bonus.ReiSolitario && atacante == Necromante && !HaMinionVivo()) mais *= 1f + Cfg.ReiSolitarioBonus;
                if (golpe)
                {
                    critico = Rng.Chance(atacante.ChanceCritico);
                    if (critico) mais *= atacante.MultCritico;
                    if (tipo != TipoDano.Fogo) fracaoFogo = atacante.FracaoFogo;
                }
            }

            // Dano = Base x (1 + aumentos) x mais x (1 - mitigação). Incandescente divide a base em duas parcelas.
            float dano = Formulas.Dano(danoBase * (1f - fracaoFogo), aumentos, mais, MitigacaoDe(alvo, tipo));
            TipoDano tipoFinal = tipo;
            if (fracaoFogo > 0f)
            {
                dano += Formulas.Dano(danoBase * fracaoFogo, aumentos, mais, MitigacaoDe(alvo, TipoDano.Fogo));
                if (Rng.Chance(fracaoFogo)) tipoFinal = TipoDano.Fogo; // define a condição do cadáver se for o golpe final
            }
            // Colosso de Ossos erguido: enquanto estiver de pé, os outros minions apanham menos
            if (Cfg.HabilidadesDeEspecie && alvo.Faccao == Faccao.Minion && alvo.Especie != EspecieId.ColossoDeOssos)
                for (int i = 0; i < _minions.Count; i++) if (_minions[i].Viva && _minions[i].Especie == EspecieId.ColossoDeOssos) { dano *= 1f - Cfg.BaluarteReducao; break; }
            if (alvo.Leal && Vec2.Dist2(alvo.Pos, Necromante.Pos) <= Cfg.LealRaio * Cfg.LealRaio) dano *= 1f - Cfg.LealReducao;

            // Execução: elites/chefes abaixo de 10% de vida, 25% por golpe do necromante ou de minion Grande+.
            bool execucao = false;
            if (golpe && atacante != null && (alvo.Elite || alvo.Chefe) && alvo.Vida < alvo.VidaMax * Cfg.LimiarExecucao
                && (atacante.Faccao == Faccao.Necromante || (atacante.Faccao == Faccao.Minion && atacante.Porte >= Porte.Grande))
                && Rng.Chance(Cfg.ChanceExecucao))
            {
                execucao = true;
                alvo.Escudo = 0f;
                dano = alvo.Vida;
            }

            // Escudo de Ossos absorve primeiro.
            float absorvido = 0f;
            if (alvo.Escudo > 0f)
            {
                absorvido = dano < alvo.Escudo ? dano : alvo.Escudo;
                alvo.Escudo -= absorvido;
                dano -= absorvido;
            }

            // Manto do Último Suspiro: uma vez por andar, o dano fatal deixa o necromante com 1 de vida
            // e o minion de maior vida é sacrificado no lugar (precisa haver um minion vivo).
            Entidade sacrificado = null;
            if (alvo == Necromante && Bonus.UltimoSuspiro && !_suspiroUsado && dano >= alvo.Vida)
            {
                sacrificado = MinionDeMaiorVida();
                if (sacrificado != null)
                {
                    _suspiroUsado = true;
                    dano = alvo.Vida > 1f ? alvo.Vida - 1f : 0f;
                }
            }

            alvo.Vida -= dano;
            if (sacrificado != null) alvo.Vida = 1f;
            Emitir(new EventoSim
            {
                Tipo = TipoEvento.Dano, Origem = atacante != null ? atacante.Id : -1, Alvo = alvo.Id, Valor = dano,
                TipoDano = tipoFinal, Critico = critico, Execucao = execucao, Pos = alvo.Pos, Extra = (int)absorvido
            });
            if (sacrificado != null)
            {
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 5, Alvo = alvo.Id, Origem = sacrificado.Id, Pos = alvo.Pos });
                DestruirMinionSemCadaver(sacrificado, -1);
            }

            // Canalização da ceifa: interrompe se o necromante sofrer mais de 15% da vida máxima.
            if (alvo == Necromante && _ceifaOrbe != null)
            {
                _ceifaDano += dano;
                if (_ceifaDano > Necromante.VidaMax * Cfg.LimiteDanoCeifa) InterromperCeifa();
            }

            // Roubo de vida (Faminto / Vampírico).
            if (atacante != null && atacante.Viva && atacante.RouboDeVida > 0f && dano > 0f) Curar(atacante, atacante, dano * atacante.RouboDeVida);

            if (alvo.Vida <= 0f) Matar(alvo, atacante, tipoFinal, dano, execucao);
            return dano;
        }

        private void Curar(Entidade curador, Entidade alvo, float valor)
        {
            if (!alvo.Viva || valor <= 0f) return;
            if (Ambiente == AmbienteDeChefe.NevoaPutrida) valor *= 1f - Cfg.NevoaReducaoDeCura;
            float antes = alvo.Vida;
            alvo.Vida = Math.Min(alvo.VidaMax, alvo.Vida + valor);
            float real = alvo.Vida - antes;
            if (real > 0.01f) Emitir(new EventoSim { Tipo = TipoEvento.Cura, Origem = curador != null ? curador.Id : -1, Alvo = alvo.Id, Valor = real, Pos = alvo.Pos });
        }

        /// <summary>Minion permanente vivo de maior vida atual (alvo do Manto do Último Suspiro). Temporários não contam.</summary>
        private Entidade MinionDeMaiorVida()
        {
            Entidade melhor = null;
            for (int i = 0; i < _minions.Count; i++)
                if (_minions[i].Viva && (melhor == null || _minions[i].Vida > melhor.Vida)) melhor = _minions[i];
            return melhor;
        }

        /// <summary>Destrói um minion sem deixar cadáver (Sacrifício e Manto do Último Suspiro). Rancoroso ainda explode.</summary>
        private void DestruirMinionSemCadaver(Entidade vitima, int origemId)
        {
            vitima.Viva = false;
            vitima.Vida = 0f;
            vitima.Animacao = EstadoAnimacao.Morrendo;
            vitima.TempoMorrendo = Cfg.TempoMorrendo;
            Resultado.MinionsMortos++;
            Emitir(new EventoSim { Tipo = TipoEvento.Morte, Alvo = vitima.Id, Origem = origemId, Condicao = CondicaoCadaver.Intacto, Pos = vitima.Pos, Especie = vitima.Especie, Extra = 1 });
            if (vitima.Rancoroso)
                _explosoes.Add(new ExplosaoPendente { Pos = vitima.Pos, Dano = vitima.Dano * Cfg.RancorosoMult, Raio = Cfg.ExplosaoAoMorrerRaio, AtingeInimigos = true, OrigemId = vitima.Id, Tipo = TipoDano.Fogo });
        }

        private void Matar(Entidade alvo, Entidade matador, TipoDano tipo, float danoFinal, bool execucao)
        {
            float excedente = -alvo.Vida; // quanto o golpe final passou da vida que restava
            alvo.Viva = false;
            alvo.Vida = 0f;
            alvo.Animacao = EstadoAnimacao.Morrendo;
            alvo.TempoMorrendo = Cfg.TempoMorrendo;

            // Overkill: golpe final > 50% da vida máxima = o corpo explode, sem cadáver, ossos extras.
            bool overkill = !execucao && danoFinal > alvo.VidaMax * Cfg.LimiarOverkill;
            CondicaoCadaver condicao = execucao ? CondicaoCadaver.Intacto : Formulas.CondicaoPorTipo(tipo);
            Vec2 dir = matador != null ? (alvo.Pos - matador.Pos).Normalizado : new Vec2(0f, 0f);

            Emitir(new EventoSim
            {
                Tipo = TipoEvento.Morte, Alvo = alvo.Id, Origem = matador != null ? matador.Id : -1, Condicao = condicao,
                Overkill = overkill, Execucao = execucao, Dir = dir, Pos = alvo.Pos, Especie = alvo.Especie, TipoDano = tipo, Valor = danoFinal
            });

            if (Cfg.ComportamentoDeEspecie && alvo.Faccao == Faccao.Inimigo) AoMorrerInimigoPerto(alvo);
            if (Cfg.HabilidadesDeEspecie)
            {
                // Zumbi erguido: ao cair, solta um miasma que envenena os inimigos em volta
                if (alvo.Faccao == Faccao.Minion && alvo.Especie == EspecieId.Zumbi && !alvo.Temporario)
                    _explosoes.Add(new ExplosaoPendente { Pos = alvo.Pos, Dano = alvo.Dano * Cfg.MiasmaMult, Raio = Cfg.MiasmaRaio, AtingeInimigos = true, OrigemId = alvo.Id, Tipo = TipoDano.Veneno });
                // Cão de Cripta erguido: o cheiro de sangue o deixa mais rápido por alguns segundos
                if (matador != null && matador.Viva && matador.Faccao == Faccao.Minion && matador.Especie == EspecieId.CaoDeCripta && alvo.Faccao == Faccao.Inimigo)
                {
                    matador.SedeTempo = Cfg.SedeDuracao;
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 17, Alvo = matador.Id, Origem = -1, Valor = Cfg.SedeDuracao, Pos = matador.Pos });
                }
            }
            // Carniçal erguido: devora o que mata e recupera parte da vida
            if (Cfg.HabilidadesDeEspecie && matador != null && matador.Viva && matador.Faccao == Faccao.Minion && matador.Especie == EspecieId.Carnical && alvo.Faccao == Faccao.Inimigo && matador.Vida < matador.VidaMax)
            {
                float cura = Math.Min(matador.VidaMax - matador.Vida, matador.VidaMax * Cfg.BanqueteCura);
                matador.Vida += cura;
                Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 18, Alvo = matador.Id, Origem = -1, Valor = cura, Pos = matador.Pos });
            }
            if (overkill) Resultado.Overkills++;
            else if (alvo != Necromante && !alvo.Temporario) CriarCadaver(alvo, condicao);

            if (alvo.Faccao == Faccao.Inimigo)
            {
                InimigosVivos--;
                Resultado.InimigosMortos++;
                if ((int)alvo.Especie < Resultado.MortesPorEspecie.Length) Resultado.MortesPorEspecie[(int)alvo.Especie]++;
                if (alvo.Elite) Resultado.ElitesMortos++;
                if (execucao) Resultado.Execucoes++;

                // Ossos: todo inimigo morto; extras se explodiu.
                float ossos = Cfg.OssosPorPorte[(int)alvo.Porte] * (1f + 0.1f * (Andar - 1));
                if (alvo.Elite) ossos *= 3f;
                float premio = alvo.DaHorda ? 1f / Cfg.Horda : alvo.Campeao ? Cfg.CampeaoPremio : 1f;   // horda rende menos por cabeça; campeão rende mais
                ossos *= premio;
                if (overkill) ossos *= 1f + Cfg.OssosExtrasOverkill;
                ossos *= _pOssos * _aOssos * _bOssos * (_emSecreto ? Cfg.SecretoOssos : 1f);
                // a fração que sobra de cada morte é guardada: uma horda de inimigos fracos não arredonda tudo para cima
                _restoDeOssos += ossos; int qOssos = (int)_restoDeOssos; _restoDeOssos -= qOssos;
                Resultado.Ossos += qOssos;
                if (qOssos > 0) Emitir(new EventoSim { Tipo = TipoEvento.RecursoGanho, Extra = 0, Valor = qOssos, Pos = alvo.Pos, Alvo = alvo.Id, Origem = -1 });

                // Ouro: todo inimigo larga um pouco; elites e chefes, bem mais
                _restoDeOuro += Cfg.OuroPorPorte[(int)alvo.Porte] * (1f + 0.1f * (Andar - 1)) * premio * (alvo.Chefe ? Cfg.OuroChefe : alvo.Elite ? Cfg.OuroElite : 1f);
                { long qo = (long)_restoDeOuro; _restoDeOuro -= qo; Resultado.Ouro += qo; }
                // livros de feitiço: o chefe sempre larga um; Únicos com frequência; elites e Campeões de vez em quando
                if (Cfg.LivrosDeFeitico && (alvo.Chefe || (alvo.Unico && _rngItens.Chance(Cfg.ChanceDeLivroDeUnico)) || (!alvo.Unico && (alvo.Elite || alvo.Campeao) && _rngItens.Chance(Cfg.ChanceDeLivroDeElite)))) SoltarLivro(alvo.Pos);

                // Sangue: elites, chefes e execuções.
                int sangue = (alvo.Chefe ? Cfg.SangueChefe : alvo.Elite ? Cfg.SangueElite : 0) + (execucao ? Cfg.SangueExecucao : 0);
                if (sangue > 0)
                {
                    Resultado.Sangue += sangue;
                    Emitir(new EventoSim { Tipo = TipoEvento.RecursoGanho, Extra = 1, Valor = sangue, Pos = alvo.Pos, Alvo = alvo.Id, Origem = -1 });
                }

                float xp = Cfg.XpPorPorte[(int)alvo.Porte] * Formulas.EscalaInimigo(Andar, Cfg);
                if (alvo.Elite) xp *= 4f;
                xp *= premio;
                Resultado.Xp += xp;

                // Alma
                int ato = Formulas.AtoDoAndar(Andar, Cfg);
                bool jaDerrotado = alvo.Chefe && _chefesDerrotados.Contains(ato);
                if (Rng.Chance(GeradorDeAlmas.ChanceDeDrop(Cfg, alvo.Elite, alvo.Chefe, jaDerrotado) * premio * (1f + Bonus.ChanceDeAlma + _bonusAlmaNecropole + _pAlma + _aAlma + _bAlma + _sorte * Cfg.SorteMaisDropsPorPonto)))
                {
                    var almaNova = GeradorDeAlmas.Gerar(Rng, Cfg, alvo.Especie);
                    if (_sorte > 0f && Rng.Chance(_sorte * Cfg.SorteChanceDeMelhorPorPonto))
                    {
                        // sorte: sorteia outra e fica com a de maior qualidade
                        var outra = GeradorDeAlmas.Gerar(Rng, Cfg, alvo.Especie);
                        if (outra.Qualidade > almaNova.Qualidade) almaNova = outra;
                    }
                    CriarOrbe(alvo.Pos, almaNova, alvo.Id);
                }
                // Itens: sorteados num fluxo próprio de Rng e entregues direto no resultado.
                SoltarItens(alvo, jaDerrotado);
                if (alvo.Chefe && !jaDerrotado)
                {
                    _chefesDerrotados.Add(ato);
                    Resultado.ChefesDerrotados.Add(ato);
                }

                if ((alvo.Modificadores & ModificadorElite.Explosivo) != 0)
                    _explosoes.Add(new ExplosaoPendente { Pos = alvo.Pos, Dano = alvo.Dano * Cfg.ExplosaoAoMorrerMult, Raio = Cfg.ExplosaoAoMorrerRaio, AtingeInimigos = false, OrigemId = alvo.Id, Tipo = TipoDano.Fogo });

                // Amuleto da Maré Rubra: o overkill espalha parte do dano excedente nos inimigos em volta (pode encadear).
                if (overkill && Bonus.MareRubra && excedente > 0f)
                    _explosoes.Add(new ExplosaoPendente { Pos = alvo.Pos, Dano = excedente * Cfg.MareRubraFracao, Raio = Cfg.MareRubraRaio, AtingeInimigos = true, OrigemId = alvo.Id, Tipo = TipoDano.Fisico });
            }
            else if (alvo.Faccao == Faccao.Minion)
            {
                if (!alvo.Temporario) Resultado.MinionsMortos++;
                if (alvo.Rancoroso)
                    _explosoes.Add(new ExplosaoPendente { Pos = alvo.Pos, Dano = alvo.Dano * Cfg.RancorosoMult, Raio = Cfg.ExplosaoAoMorrerRaio, AtingeInimigos = true, OrigemId = alvo.Id, Tipo = TipoDano.Fogo });
            }
        }

        private void CriarCadaver(Entidade e, CondicaoCadaver condicao)
        {
            // Limite de cadáveres na arena: remove o mais antigo (de preferência de inimigo).
            if (Cadaveres.Count >= Cfg.LimiteDeCadaveres)
            {
                int idx = 0;
                for (int i = 0; i < Cadaveres.Count; i++) if (!Cadaveres[i].DeMinion) { idx = i; break; }
                Emitir(new EventoSim { Tipo = TipoEvento.CadaverRemovido, Alvo = Cadaveres[idx].Id, Pos = Cadaveres[idx].Pos, Origem = -1 });
                Cadaveres.RemoveAt(idx);
            }
            var c = new Cadaver
            {
                Id = _proximoId++, EntidadeId = e.Id, Especie = e.Especie, Pos = e.Pos, Condicao = condicao, VidaMax = e.VidaMax,
                DeMinion = e.Faccao == Faccao.Minion, Custo = e.Custo, Minion = e.Faccao == Faccao.Minion ? e : null
            };
            Cadaveres.Add(c);
            Emitir(new EventoSim { Tipo = TipoEvento.CadaverCriado, Alvo = c.Id, Origem = e.Id, Condicao = condicao, Pos = e.Pos, Especie = e.Especie });
        }

        /// <summary>Resolve explosões de morte (Rancoroso / elite Explosivo / Maré Rubra), inclusive em cadeia.</summary>
        private void ProcessarExplosoes()
        {
            int guarda = 0;
            while (_explosoes.Count > 0 && guarda++ < 256)
            {
                var x = _explosoes[0];
                _explosoes.RemoveAt(0);
                Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = x.OrigemId, Alvo = -1, Pos = x.Pos, Valor = x.Raio, TipoDano = x.Tipo });
                float r2 = x.Raio * x.Raio;
                int n = Entidades.Count;
                for (int i = 0; i < n; i++)
                {
                    var e = Entidades[i];
                    if (!e.Viva || (e.Faccao == Faccao.Inimigo) != x.AtingeInimigos) continue;
                    if (Vec2.Dist2(e.Pos, x.Pos) <= r2) CausarDano(null, e, x.Dano, x.Tipo, false);
                }
            }
        }
    }
}
