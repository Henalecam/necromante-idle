using System.Collections.Generic;

namespace Necro.Nucleo
{
    /// <summary>
    /// A mão do jogador no combate. Nada aqui é obrigatório: ausente, o necromante ceifa e conjura sozinho.
    /// Presente, o jogador rende mais: ceifa na hora (com chance de alma melhor) e dispara dois poderes de toque
    /// que não gastam mana e têm recarga própria. Os comandos entram numa fila e valem no tick seguinte, então a
    /// mesma sequência de comandos nos mesmos ticks dá sempre o mesmo resultado.
    /// </summary>
    public sealed partial class Simulacao
    {
        private readonly List<int> _toquesEmOrbes = new List<int>();
        private bool _pedidoDetonar, _pedidoFrenesi;

        /// <summary>Recarga restante (s) do Detonar.</summary>
        public float RecargaDetonar { get; private set; }
        /// <summary>Recarga restante (s) do Frenesi.</summary>
        public float RecargaFrenesi { get; private set; }
        /// <summary>Tempo restante (s) do Frenesi ativo.</summary>
        public float FrenesiTempo { get; private set; }

        /// <summary>Quantos cadáveres de inimigos o Detonar consumiria agora.</summary>
        public int CadaveresDetonaveis
        {
            get { int n = 0; for (int i = 0; i < Cadaveres.Count; i++) if (!Cadaveres[i].DeMinion) n++; return n; }
        }

        // ---- Marca do Ceifador: o jogador aponta um inimigo; os minions largam o que estão fazendo e vão nele ----
        private int _pedidoMarca = -1;
        /// <summary>Id do inimigo marcado, ou -1.</summary>
        public int MarcadoId { get; private set; } = -1;
        public float MarcaTempo { get; private set; }
        /// <summary>Toque num inimigo: ele fica marcado (um por vez). Minions o priorizam e todo dano aliado nele aumenta.</summary>
        public void ComandoMarcar(int entidadeId) { _pedidoMarca = entidadeId; }
        private Entidade Marcado()
        {
            if (MarcadoId < 0) return null;
            var m = AcharEntidade(MarcadoId);
            return m != null && m.Viva && m.Faccao == Faccao.Inimigo ? m : null;
        }

        // ---- Sopro Profano: toque num aliado ferido para remendá-lo ----
        private int _pedidoCura = -1;
        public float RecargaSopro { get; private set; }
        /// <summary>Toque num minion (ou no necromante): cura uma fração da vida. Tem recarga própria; vale no próximo tick.</summary>
        public void ComandoCurar(int entidadeId) { _pedidoCura = entidadeId; }

        /// <summary>Toque num orbe: ceifa imediata no próximo tick.</summary>
        public void ComandoCeifar(int orbeId) { if (!_toquesEmOrbes.Contains(orbeId)) _toquesEmOrbes.Add(orbeId); }
        /// <summary>Detonar: explode de uma vez os cadáveres de inimigos na arena.</summary>
        public void ComandoDetonar() { _pedidoDetonar = true; }
        /// <summary>Frenesi: os minions causam mais dano por alguns segundos.</summary>
        public void ComandoFrenesi() { _pedidoFrenesi = true; }

        // ---- Encruzilhadas: pactos para o resto da expedição ----
        private Rng _rngPactos;                       // fluxo próprio: sortear ofertas não desloca os sorteios do combate
        private int _pedidoPacto = -1;
        private float _pDanoMinion = 1f, _pDanoInimigo = 1f, _pVidaInimigo = 1f, _pOssos = 1f, _pAlma, _pItem = 1f, _pRecarga = 1f;

        /// <summary>Os três pactos em oferta (índices em ConfigJogo.Pactos), ou null se não há encruzilhada aberta.</summary>
        public int[] PactosOferecidos { get; private set; }
        /// <summary>Segundos até a oferta sumir.</summary>
        public float TempoDaOferta { get; private set; }
        /// <summary>Pactos aceitos nesta expedição, em ordem.</summary>
        public readonly List<int> PactosAtivos = new List<int>();

        /// <summary>Aceita o pacto da posição 0, 1 ou 2 da oferta. Vale no próximo tick; sem oferta aberta, não faz nada.</summary>
        public void ComandoPacto(int opcao) { _pedidoPacto = opcao; }

        /// <summary>Chamado ao concluir um andar: a cada N andares da expedição abre uma encruzilhada com três pactos.</summary>
        private void TalvezAbrirEncruzilhada()
        {
            if (Cfg.Pactos.Length < 3 || Andar < Cfg.EncruzilhadaAPartirDoAndar || Resultado.AndaresConcluidos % Cfg.AndaresPorEncruzilhada != 0) return;
            if (_rngPactos == null) _rngPactos = new Rng(_sementePactos);
            var o = new int[3];
            for (int i = 0; i < 3; i++)
            {
                bool repetido;
                do { o[i] = _rngPactos.Intervalo(0, Cfg.Pactos.Length); repetido = false; for (int k = 0; k < i; k++) if (o[k] == o[i]) repetido = true; } while (repetido);
            }
            PactosOferecidos = o; TempoDaOferta = Cfg.TempoDaEncruzilhada;
        }

        private void AceitarPacto(int opcao)
        {
            if (PactosOferecidos == null || opcao < 0 || opcao > 2 || !Necromante.Viva) return;
            int id = PactosOferecidos[opcao];
            var d = Cfg.Pactos[id];
            PactosOferecidos = null; TempoDaOferta = 0f;
            PactosAtivos.Add(id); Resultado.PactosAceitos++;
            _pDanoMinion *= d.DanoMinion; _pDanoInimigo *= d.DanoInimigo; _pVidaInimigo *= d.VidaInimigo;
            _pOssos *= d.Ossos; _pAlma += d.ChanceDeAlma; _pItem *= d.ChanceDeItem; _pRecarga *= d.RecargaDosPoderes;
            if (d.CuraAgora)
                for (int i = 0; i < Entidades.Count; i++)
                {
                    var e = Entidades[i];
                    if (e.Viva && e.Faccao != Faccao.Inimigo) e.Vida = e.VidaMax;
                }
            Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 7, Alvo = -1, Origem = Necromante.Id, Valor = id, Pos = Necromante.Pos });
        }

        private void AtualizarComandosDoJogador()
        {
            if (PactosOferecidos != null)
            {
                if (_pedidoPacto >= 0) AceitarPacto(_pedidoPacto);
                else { TempoDaOferta -= DT; if (TempoDaOferta <= 0f) PactosOferecidos = null; }
            }
            _pedidoPacto = -1;
            if (_pedidoMarca >= 0)
            {
                var alvoMarca = AcharEntidade(_pedidoMarca);
                if (alvoMarca != null && alvoMarca.Viva && alvoMarca.Faccao == Faccao.Inimigo && Necromante.Viva)
                {
                    MarcadoId = alvoMarca.Id; MarcaTempo = Cfg.MarcaDuracao; Resultado.MarcasFeitas++;
                    if (alvoMarca.PreparandoGolpe > 0f)
                    {
                        // marcar durante o aviso quebra o Golpe Brutal e atordoa
                        alvoMarca.PreparandoGolpe = 0f; alvoMarca.TempoAcao = Cfg.GolpeBrutalAtordoamento; alvoMarca.Animacao = EstadoAnimacao.Parado;
                        Resultado.GolpesInterrompidos++;
                        Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 10, Alvo = alvoMarca.Id, Origem = Necromante.Id, Valor = Cfg.GolpeBrutalAtordoamento, Pos = alvoMarca.Pos });
                    }
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 8, Alvo = alvoMarca.Id, Origem = Necromante.Id, Valor = Cfg.MarcaDuracao, Pos = alvoMarca.Pos });
                }
                _pedidoMarca = -1;
            }
            if (RecargaSopro > 0f) RecargaSopro -= DT * _pRecarga;
            if (_pedidoCura >= 0)
            {
                var ferido = AcharEntidade(_pedidoCura);
                if (RecargaSopro <= 0f && ferido != null && ferido.Viva && ferido.Faccao != Faccao.Inimigo && ferido.Vida < ferido.VidaMax && Necromante.Viva)
                {
                    RecargaSopro = Cfg.SoproRecarga; Resultado.SoprosUsados++;
                    Curar(Necromante, ferido, ferido.VidaMax * Cfg.SoproCura);
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 11, Alvo = ferido.Id, Origem = Necromante.Id, Valor = Cfg.SoproCura, Pos = ferido.Pos });
                }
                _pedidoCura = -1;
            }
            if (MarcadoId >= 0) { MarcaTempo -= DT; if (MarcaTempo <= 0f || Marcado() == null) MarcadoId = -1; }
            if (RecargaDetonar > 0f) RecargaDetonar -= DT * _pRecarga;
            if (RecargaFrenesi > 0f) RecargaFrenesi -= DT * _pRecarga;
            if (FrenesiTempo > 0f) FrenesiTempo -= DT;

            for (int t = 0; t < _toquesEmOrbes.Count; t++)
            {
                OrbeDeAlma o = null;
                for (int i = 0; i < Orbes.Count; i++) if (Orbes[i].Id == _toquesEmOrbes[t]) { o = Orbes[i]; break; }
                if (o == null || !Necromante.Viva) continue;
                if (_ceifaOrbe == o) { _ceifaOrbe = null; _ceifaTempo = 0f; _ceifaDano = 0f; }   // a canalização vira ceifa pronta
                bool melhorou = false;
                if (o.Alma.Qualidade < Qualidade.Ancestral && Rng.Chance(Cfg.ToqueChanceDeMelhorarAlma + Bonus.ToqueMelhoraAlma)) { o.Alma.Qualidade++; melhorou = true; }
                Resultado.CeifasPorToque++;
                EntregarAlma(o, melhorou);
            }
            _toquesEmOrbes.Clear();

            if (_pedidoDetonar) { _pedidoDetonar = false; Detonar(); }
            if (_pedidoFrenesi)
            {
                _pedidoFrenesi = false;
                if (RecargaFrenesi <= 0f && Necromante.Viva && HaMinionVivo())
                {
                    RecargaFrenesi = Cfg.FrenesiRecarga; FrenesiTempo = Cfg.FrenesiDuracao;
                    Emitir(new EventoSim { Tipo = TipoEvento.EfeitoAplicado, Extra = 6, Alvo = -1, Origem = Necromante.Id, Valor = Cfg.FrenesiDuracao, Pos = Necromante.Pos });
                }
            }
        }

        private void Detonar()
        {
            if (RecargaDetonar > 0f || !Necromante.Viva || CadaveresDetonaveis == 0) return;
            RecargaDetonar = Cfg.DetonarRecarga;
            var f = Fei(FeiticoId.ExplosaoDeCadaver);
            float r2 = f.Raio * f.Raio;
            int consumidos = 0;
            for (int i = Cadaveres.Count - 1; i >= 0 && consumidos < Cfg.DetonarMaxCorpos; i--)
            {
                var c = Cadaveres[i];
                if (c.DeMinion) continue;
                consumidos++;
                float dano = (Necromante.Dano * f.Potencia + c.VidaMax * Cfg.ExplosaoCadaverFracaoVida) * Cfg.DetonarPotencia;
                Cadaveres.RemoveAt(i);
                Emitir(new EventoSim { Tipo = TipoEvento.CadaverConsumido, Alvo = c.Id, Pos = c.Pos, Extra = (int)FeiticoId.ExplosaoDeCadaver, Origem = Necromante.Id });
                Emitir(new EventoSim { Tipo = TipoEvento.Explosao, Origem = Necromante.Id, Alvo = -1, Pos = c.Pos, Valor = f.Raio, TipoDano = TipoDano.Fisico });
                int total = Entidades.Count;
                for (int j = 0; j < total; j++)
                {
                    var e = Entidades[j];
                    if (e.Viva && e.Faccao == Faccao.Inimigo && Vec2.Dist2(e.Pos, c.Pos) <= r2) CausarDano(Necromante, e, dano, TipoDano.Fisico, true);
                }
            }
        }
    }
}
