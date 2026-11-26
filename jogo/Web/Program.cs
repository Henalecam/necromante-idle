// Ponte entre o núcleo do jogo (C# compilado para WebAssembly) e a página: o JavaScript chama estes métodos.
using System;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using Necro.Nucleo;

public static partial class Jogo
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static ConfigJogo _cfg;
    static EstadoProgresso _p;
    static Simulacao _sim;
    static readonly StringBuilder _sb = new StringBuilder(8192);

    public static void Main() { }

    /// <summary>Começa (ou continua) o jogo a partir de um save em JSON; vazio = jogo novo.</summary>
    [JSExport]
    public static void Iniciar(string save)
    {
        _cfg = new ConfigJogo();
        _p = null;
        if (!string.IsNullOrEmpty(save))
        {
            try { _p = JsonSerializer.Deserialize<EstadoProgresso>(save, Json); if (_p != null) Missoes.Migrar(_p); } catch { _p = null; }
        }
        if (_p == null || _p.Minions == null) _p = Progressao.NovoJogo(_cfg);
        if (_p.Equipados == null || _p.Equipados.Length != 9) _p.Equipados = new Item[9];
        if (_p.OferendasAutomaticas) Oferendas.OfertarTudo(_p, _cfg);
        NovaExpedicao();
    }

    static void NovaExpedicao()
    {
        _cfg.ForcarCofre = _forcarCofre; _forcarCofre = false;
        _sim = new Simulacao(_cfg, _p, 1003UL + _p.ContadorDeSementes++, ModoExpedicao.Avancar, Progressao.AndarDePartida(_p, _cfg));
        Oferendas.ConsumirRito(_p);
        if (_forcarSecreto) { _sim.ForcarNivelSecreto = true; _forcarSecreto = false; } else _cfg.ForcarSecreto = 0;
    }
    static bool _forcarSecreto, _forcarCofre;

    [JSExport]
    public static string Salvar() { return JsonSerializer.Serialize(_p, Json); }

    /// <summary>Avança N ticks (10 por segundo de jogo) e devolve o estado para desenhar, em linhas de texto.</summary>
    [JSExport]
    public static string Avancar(int ticks)
    {
        _sb.Clear();
        for (int t = 0; t < ticks; t++)
        {
            if (_sim.Encerrada)
            {
                var r = _sim.Resultado;
                Progressao.AplicarResultado(_p, r, _cfg);
                _sb.Append("F ").Append(r.AndarFinal).Append(' ').Append(r.Almas.Count).Append(' ').Append(r.Itens.Count).Append(' ').Append(r.Ossos).Append(' ').Append(r.InimigosMortos).Append('\n');
                NovaExpedicao();
                break;
            }
            _sim.Tick();
            foreach (var ev in _sim.Eventos)
                _sb.Append("V ").Append(ev.Tipo).Append(' ').Append(ev.Origem).Append(' ').Append(ev.Alvo).Append(' ')
                  .Append(ev.Valor.ToString("0.#", CI)).Append(' ').Append(ev.TipoDano).Append(' ').Append(ev.Critico ? 1 : 0).Append(' ')
                  .Append(ev.Condicao).Append(' ').Append(ev.Overkill ? 1 : 0).Append(' ').Append(ev.Execucao ? 1 : 0).Append(' ')
                  .Append(F(ev.Pos.X)).Append(' ').Append(F(ev.Pos.Y)).Append(' ').Append(F(ev.Dir.X)).Append(' ').Append(F(ev.Dir.Y)).Append(' ').Append(ev.Extra).Append('\n');
        }
        _sb.Append("T ").Append(_sim.Andar).Append(' ').Append(_sim.Necromante != null ? F(_sim.Necromante.Mana) : "0").Append(' ')
           .Append(_sim.Necromante != null ? F(_sim.Necromante.ManaMax) : "1").Append(' ')
           .Append(_sim.Resultado.Ossos).Append(' ').Append(_sim.Resultado.Almas.Count).Append(' ').Append(_sim.Resultado.Itens.Count).Append(' ')
           .Append(F(Math.Max(0f, _sim.RecargaDetonar))).Append(' ').Append(F(Math.Max(0f, _sim.RecargaFrenesi))).Append(' ')
           .Append(F(Math.Max(0f, _sim.FrenesiTempo))).Append(' ').Append(_sim.CadaveresDetonaveis).Append(' ').Append(_sim.MarcadoId).Append(' ').Append(_sim.NecromanteCorpoACorpo ? 1 : 0).Append(' ').Append(F(Math.Max(0f, _sim.RecargaSopro))).Append(' ').Append((int)_sim.FeiticoDoEspaco(0)).Append(' ').Append(F(_sim.RecargaDoFeiticoDeItem(0))).Append(' ').Append((int)_sim.FeiticoDoEspaco(1)).Append(' ').Append(F(_sim.RecargaDoFeiticoDeItem(1))).Append(' ').Append((int)_sim.PressagioAtual).Append('\n');
        _sb.Append("M ").Append(_sim.Fase).Append(' ').Append(_sim.SalaAtual).Append(' ').Append((int)_sim.Ambiente).Append(' ').Append((int)_sim.BencaoAtual).Append(' ')
           .Append(_sim.EmNivelSecreto ? (int)_sim.SecretoAtual : 0).Append(' ').Append(F(_sim.PosDoObjeto.X)).Append(' ').Append(F(_sim.PosDoObjeto.Y));
        foreach (var ts in _sim.Salas) _sb.Append(' ').Append((int)ts);
        _sb.Append('\n');
        if (_sim.BencaosOferecidas != null) _sb.Append("B ").Append(_sim.BencaosOferecidas[0]).Append(' ').Append(_sim.BencaosOferecidas[1]).Append(' ').Append(_sim.BencaosOferecidas[2]).Append(' ').Append(F(Math.Max(0f, _sim.TempoDaBencao))).Append('\n');
        foreach (var plano in _sim.PlanoDasOndas) foreach (var ip in plano) if (!ip.Desperto)
            _sb.Append("I ").Append(ip.Sala).Append(' ').Append(ip.Especie).Append(' ').Append(F(ip.Pos.X)).Append(' ').Append(F(ip.Pos.Y)).Append(' ').Append(ip.Elite ? 1 : 0).Append(' ').Append(ip.Chefe ? 1 : 0).Append(' ').Append(ip.Campeao ? 1 : 0).Append('\n');
        if (_sim.ChaveNaSala >= 0) _sb.Append("C ").Append(_sim.ChaveNaSala).Append(' ').Append(_sim.TemChave ? 1 : 0).Append('\n');
        if (_sim.PatrulhaSala >= 0) { _sb.Append("R ").Append(_sim.PatrulhaSala); foreach (var pe in _sim.Patrulha) _sb.Append(' ').Append(pe); _sb.Append('\n'); }
        _sb.Append('G'); foreach (var c in _sim.Mapa) _sb.Append(' ').Append(c.X).Append(' ').Append(c.Y).Append(' ').Append(c.Pai);
        _sb.Append('\n');
        if (_sim.PactosOferecidos != null)
            _sb.Append("K ").Append(_sim.PactosOferecidos[0]).Append(' ').Append(_sim.PactosOferecidos[1]).Append(' ').Append(_sim.PactosOferecidos[2]).Append(' ').Append(F(_sim.TempoDaOferta)).Append('\n');
        if (_sim.PactosAtivos.Count > 0) { _sb.Append('A'); foreach (int pa in _sim.PactosAtivos) _sb.Append(' ').Append(pa); _sb.Append('\n'); }
        foreach (var ob in _sim.Objetos) _sb.Append("Q ").Append(ob.Sala).Append(' ').Append((int)ob.Tipo).Append(' ').Append(F(ob.Pos.X)).Append(' ').Append(F(ob.Pos.Y)).Append(' ').Append(ob.Quebrado ? 1 : 0).Append('\n');
        foreach (var e in _sim.Entidades) if (e.Unico && e.Viva) _sb.Append("U ").Append(e.Id).Append(' ').Append(e.NomeProprio).Append('\n');
        foreach (var e in _sim.Entidades)
            _sb.Append("E ").Append(e.Id).Append(' ').Append(e.Especie).Append(' ').Append(e.Faccao == Faccao.Inimigo ? 1 : 0).Append(' ')
              .Append(F(e.Pos.X)).Append(' ').Append(F(e.Pos.Y)).Append(' ').Append(F(e.Olhar.X)).Append(' ').Append(F(e.Olhar.Y)).Append(' ')
              .Append(e.Animacao).Append(' ').Append(e.Viva ? 1 : 0).Append(' ').Append(F(e.Vida / Math.Max(1f, e.VidaMax))).Append(' ')
              .Append(e.Elite ? 1 : 0).Append(' ').Append(e.Chefe ? 1 : 0).Append(' ').Append(e == _sim.Necromante ? 1 : 0).Append(' ').Append(F(Math.Max(0f, e.PreparandoGolpe))).Append(' ').Append(e.Faccao == Faccao.Minion && e.IndiceMinion >= 0 && e.IndiceMinion < _p.Minions.Count ? (int)_p.Minions[e.IndiceMinion].Alma.Qualidade : -1).Append(' ').Append(F(Math.Min(1f, Math.Max(0f, e.Escudo) / Math.Max(1f, e.VidaMax)))).Append(' ').Append(e.Campeao ? 1 : 0).Append('\n');
        foreach (var o in _sim.Orbes) _sb.Append("O ").Append(o.Id).Append(' ').Append(F(o.Pos.X)).Append(' ').Append(F(o.Pos.Y)).Append('\n');
        foreach (var pj in _sim.Projeteis)
            _sb.Append("P ").Append(pj.Id).Append(' ').Append(F(pj.Pos.X)).Append(' ').Append(F(pj.Pos.Y)).Append(' ').Append(F(pj.Dir.X)).Append(' ').Append(F(pj.Dir.Y)).Append(' ').Append(pj.Perfurante ? 1 : 0).Append('\n');
        foreach (var pc in _sim.Pocas) _sb.Append("S ").Append(pc.Id).Append(' ').Append(F(pc.Pos.X)).Append(' ').Append(F(pc.Pos.Y)).Append(' ').Append(F(pc.Raio)).Append('\n');
        return _sb.ToString();
    }

    static string F(float v) { return v.ToString("0.##", CI); }

    /// <summary>Tudo o que os painéis precisam mostrar, em JSON.</summary>
    [JSExport]
    public static string Painel()
    {
        Progressao.AjustarAoComando(_p, _cfg);   // equipamento ou talentos podem ter baixado o Comando
        var sb = new StringBuilder(4096);
        sb.Append("{\"andar\":").Append(_sim.Andar).Append(",\"recorde\":").Append(_p.MaiorAndarConcluido).Append(",\"ossos\":").Append(_p.Ossos)
          .Append(",\"essencia\":").Append(_p.Essencia).Append(",\"sangue\":").Append(_p.Sangue).Append(",\"nivel\":").Append(_p.NivelNecromante).Append(",\"poder\":").Append(Poder.Total(_p, _cfg)).Append(",\"poderSugerido\":").Append(Poder.SugeridoParaOAndar(_sim.Andar, _cfg)).Append(",\"poderProximo\":").Append(Poder.SugeridoParaOAndar(_p.MaiorAndarConcluido + 1, _cfg)).Append(",\"sorte\":").Append(F(_p.Sorte)).Append(",\"poderNec\":").Append((int)Math.Round(_p.PoderNecromantico * 100f)).Append(",\"comandoBase\":").Append(_p.ComandoMaximo).Append(",\"cheats\":").Append(B(_p.UsouCheats)).Append(",\"cinzas\":").Append(_p.Cinzas).Append(",\"xp\":").Append(F(Math.Min(1f, _p.XpNecromante / Formulas.XpParaProximoNivelNecromante(_p.NivelNecromante))))
          .Append(",\"comando\":").Append(Progressao.ComandoEmUso(_p, _cfg)).Append(",\"comandoMax\":").Append(Progressao.ComandoMaximoEfetivo(_p, _cfg))
          .Append(",\"filacterioMax\":").Append(Necropole.CapacidadeDoFilacterio(_p, _cfg)).Append(",\"inventarioMax\":").Append(_cfg.CapacidadeInventario)
          .Append(",\"largura\":").Append(F(_cfg.LarguraArena)).Append(",\"altura\":").Append(F(_cfg.AlturaArena));
        var mi = Missoes.Atual(_p, _cfg);
        sb.Append(",\"abertos\":[").Append(B(Missoes.Aberto(_p, Sistema.Exercito, _cfg))).Append(',').Append(B(Missoes.Aberto(_p, Sistema.Necropole, _cfg))).Append(',').Append(B(Missoes.Aberto(_p, Sistema.Equipamento, _cfg))).Append(']');
        sb.Append(",\"missao\":");
        if (mi == null) sb.Append("null");
        else sb.Append("{\"n\":").Append(_p.MissaoAtual + 1).Append(",\"total\":").Append(_cfg.Missoes.Length).Append(",\"titulo\":").Append(S(mi.Titulo)).Append(",\"historia\":").Append(S(mi.Historia))
               .Append(",\"objetivo\":").Append(S(mi.Objetivo)).Append(",\"feito\":").Append(Missoes.Progresso(_p, _cfg, _sim.Resultado)).Append(",\"de\":").Append(mi.Quantidade)
               .Append(",\"pronta\":").Append(B(Missoes.Completa(_p, _cfg, _sim.Resultado)))
               .Append(",\"ossos\":").Append(mi.Ossos).Append(",\"essencia\":").Append(mi.Essencia).Append('}');
        sb.Append(",\"pactos\":[");
        for (int i = 0; i < _cfg.Pactos.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(_cfg.Pactos[i].Nome)).Append(",\"ganho\":").Append(S(_cfg.Pactos[i].Ganho)).Append(",\"preco\":").Append(S(_cfg.Pactos[i].Preco)).Append('}');
        }
        sb.Append(']');
        if (!_p.SemAutoAtributos) Atributos.AutoDistribuir(_p, _cfg);
        Grimorio.LerGuardados(_p, _cfg);   // Magia nova (pontos gastos agora) já lê o que estava guardado
        sb.Append(",\"grimorio\":[");
        for (int fi = 0; fi < Grimorio.Quantos; fi++)
        {
            var fd = _cfg.Feitico((FeiticoId)fi); int nv = Grimorio.Nivel(_p, (FeiticoId)fi);
            if (fi > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(fd.Nome)).Append(",\"nivel\":").Append(nv).Append(",\"max\":").Append(_cfg.LivroNivelMaximo).Append(",\"guardados\":").Append(Grimorio.Guardados(_p, (FeiticoId)fi))
              .Append(",\"magia\":").Append(Grimorio.MagiaExigida(nv + 1, _cfg)).Append(",\"mana\":").Append(F(fd.CustoMana * Grimorio.FatorDeMana(nv, _cfg))).Append(",\"pot\":").Append((int)Math.Round((Grimorio.FatorDePotencia(nv, _cfg) - 1f) * 100f)).Append('}');
        }
        sb.Append("],\"livrosNaDescida\":").Append(_sim.Resultado.Livros.Count);
        sb.Append(",\"atributos\":{\"v\":[").Append(Atributos.Valor(_p, Atributo.Forca)).Append(',').Append(Atributos.Valor(_p, Atributo.Magia)).Append(',').Append(Atributos.Valor(_p, Atributo.Destreza)).Append(',').Append(Atributos.Valor(_p, Atributo.Vitalidade))
          .Append("],\"livres\":").Append(Atributos.Livres(_p, _cfg)).Append(",\"auto\":").Append(B(!_p.SemAutoAtributos)).Append(",\"redistribuir\":").Append(Atributos.CustoDeRedistribuir(_p, _cfg))
          .Append(",\"porPonto\":[").Append(F(_cfg.ForcaDanoDosMinions * 100f)).Append(',').Append(F(_cfg.MagiaDanoDeFeiticos * 100f)).Append(',').Append(F(_cfg.MagiaMana)).Append(',').Append(F(_cfg.DestrezaCritico * 100f)).Append(',').Append(F(_cfg.DestrezaVelAtaque * 100f)).Append(',').Append(F(_cfg.VitalidadeVidaDosMinions * 100f)).Append(',').Append(F(_cfg.VitalidadeVidaDoNecromante * 100f)).Append("]}");
        sb.Append(",\"pontos\":").Append(Talentos.Disponiveis(_p, _cfg)).Append(",\"niveisPorPonto\":").Append(_cfg.NiveisPorPontoDeTalento)
          .Append(",\"redistribuir\":").Append(Talentos.CustoDeRedistribuir(_p, _cfg)).Append(",\"talentos\":[");
        for (int i = 0; i < _cfg.Talentos.Length; i++)
        {
            var t = _cfg.Talentos[i]; int pts = Talentos.Pontos(_p, i);
            bool pct = t.Efeito != EfeitoDeTalento.Comando && t.Efeito != EfeitoDeTalento.RegenMana;
            string agora = string.Format(CI, t.Texto, (t.PorPonto * pts * (pct ? 100f : 1f)).ToString("0.#", CI));
            string prox = string.Format(CI, t.Texto, (t.PorPonto * (pts + 1) * (pct ? 100f : 1f)).ToString("0.#", CI));
            if (i > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(t.Nome)).Append(",\"ramo\":").Append((int)t.Ramo).Append(",\"pts\":").Append(pts).Append(",\"max\":").Append(t.Maximo)
              .Append(",\"agora\":").Append(S(agora)).Append(",\"prox\":").Append(S(prox)).Append(",\"requer\":").Append(t.PontosNoRamo)
              .Append(",\"noRamo\":").Append(Talentos.GastosNoRamo(_p, t.Ramo, _cfg)).Append(",\"pode\":").Append(B(Talentos.PodeInvestir(_p, i, _cfg))).Append('}');
        }
        sb.Append(']');
        sb.Append(",\"ritoPreparado\":").Append((int)_p.RitoPreparado).Append(",\"ritoEmCurso\":").Append((int)_sim.RitoDaExpedicao).Append(",\"ritos\":[");
        for (int i = 0; i < _cfg.Ritos.Length; i++)
        {
            var rt = _cfg.Ritos[i]; if (i > 0) sb.Append(',');
            sb.Append("{\"id\":").Append((int)rt.Id).Append(",\"nome\":").Append(S(rt.Nome)).Append(",\"efeito\":").Append(S(rt.Efeito)).Append(",\"sangue\":").Append(rt.Sangue).Append('}');
        }
        sb.Append("],\"oferendas\":[");
        for (int i = 0; i < Oferendas.Tipos; i++)
        {
            var of = _cfg.Oferendas[i]; int nv = Oferendas.Nivel(_p, i); if (i > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(of.Nome)).Append(",\"texto\":").Append(S(string.Format(CI, of.Texto, (of.PorNivel * nv * 100f).ToString("0.#", CI))))
              .Append(",\"porNivel\":").Append(F(of.PorNivel * 100f)).Append(",\"nivel\":").Append(nv).Append(",\"max\":").Append(_cfg.NivelMaximoDaOferenda)
              .Append(",\"restos\":").Append(_p.Restos[i]).Append(",\"custo\":").Append(Oferendas.Custo(_p, i, _cfg)).Append(",\"pode\":").Append(B(Oferendas.Pode(_p, i, _cfg))).Append('}');
        }
        sb.Append(']');
        sb.Append(",\"ascensao\":{\"pode\":").Append(B(Ascensao.Pode(_p))).Append(",\"renderia\":").Append(Ascensao.CinzasDestaVida(_p)).Append(",\"cinzas\":").Append(_p.Cinzas)
          .Append(",\"vezes\":").Append(_p.Ascensoes).Append(",\"legados\":[");
        for (int i = 0; i < Ascensao.Legados; i++)
        {
            var lg = _cfg.Legados[i]; int nv = Ascensao.Nivel(_p, i); if (i > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(lg.Nome)).Append(",\"texto\":").Append(S(string.Format(CI, lg.Texto, (lg.PorNivel * nv * 100f).ToString("0.#", CI))))
              .Append(",\"porNivel\":").Append(F(lg.PorNivel * 100f)).Append(",\"nivel\":").Append(nv).Append(",\"max\":").Append(lg.Maximo)
              .Append(",\"custo\":").Append(Ascensao.Custo(_p, i)).Append(",\"pode\":").Append(B(Ascensao.PodeComprar(_p, i, _cfg))).Append('}');
        }
        sb.Append("]}");
        sb.Append(",\"construcoes\":[");
        for (int c = 0; c < Necropole.TotalDeConstrucoes; c++)
        {
            var id = (Construcao)c; var d = Necropole.Dados(id, _cfg); long o, e; Necropole.Custo(_p, id, _cfg, out o, out e);
            if (c > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(d.Nome)).Append(",\"efeito\":").Append(S(d.Efeito)).Append(",\"nivel\":").Append(Necropole.Nivel(_p, id)).Append(",\"max\":").Append(d.NivelMaximo)
              .Append(",\"ossos\":").Append(o).Append(",\"essencia\":").Append(e).Append(",\"aberta\":").Append(B(Necropole.Desbloqueada(_p, id, _cfg)))
              .Append(",\"andar\":").Append(d.AndarDeDesbloqueio).Append(",\"pode\":").Append(B(Necropole.PodeMelhorar(_p, id, _cfg))).Append('}');
        }
        sb.Append("],\"almas\":[");
        for (int i = 0; i < _p.Filacterio.Count; i++)
        {
            var a = _p.Filacterio[i]; if (i > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(TabelaEspecies.Obter(a.Especie).Nome)).Append(",\"q\":").Append((int)a.Qualidade).Append(",\"qn\":").Append(S(a.Qualidade.ToString()))
              .Append(",\"custo\":").Append(Necropole.CustoDeErguer(_p, a, _cfg)).Append(",\"comando\":").Append(Progressao.CustoDeComando(a.Especie, _cfg))
              .Append(",\"sprite\":").Append(S(a.Especie.ToString().ToLowerInvariant())).Append(",\"dons\":").Append(Dons(a)).Append(",\"tracos\":").Append(Tracos(a)).Append('}');
        }
        sb.Append("],\"fusoes\":[");
        bool pf = true;
        for (int e = 1; e < 16; e++)
            for (int q = 0; q < (int)Qualidade.Profana; q++)
            {
                int n = Fusao.Contar(_p, (EspecieId)e, (Qualidade)q);
                if (n < _cfg.AlmasPorFusao) continue;
                if (!pf) sb.Append(','); pf = false;
                sb.Append("{\"e\":").Append(e).Append(",\"q\":").Append(q).Append(",\"nome\":").Append(S(TabelaEspecies.Obter((EspecieId)e).Nome)).Append(",\"n\":").Append(n)
                  .Append(",\"custo\":").Append(Fusao.Custo((Qualidade)q, _cfg)).Append(",\"pode\":").Append(B(Fusao.Pode(_p, (EspecieId)e, (Qualidade)q, _cfg))).Append('}');
            }
        sb.Append("],\"bestiario\":[");
        for (int e = 1; e < TabelaEspecies.Total; e++)
        {
            var id = (EspecieId)e; if (e > 1) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(TabelaEspecies.Obter(id).Nome)).Append(",\"mortes\":").Append(Bestiario.Mortes(_p, id) + (e < _sim.Resultado.MortesPorEspecie.Length ? _sim.Resultado.MortesPorEspecie[e] : 0))
              .Append(",\"dominio\":").Append(Bestiario.Dominio(_p, id, _cfg)).Append(",\"max\":").Append(_cfg.MarcosDoBestiario.Length)
              .Append(",\"prox\":").Append(Bestiario.ProximoMarco(_p, id, _cfg)).Append(",\"bonus\":").Append((int)Math.Round(100 * _cfg.BonusPorDominio * Bestiario.Dominio(_p, id, _cfg)))
              .Append(",\"porNivel\":").Append((int)Math.Round(100 * _cfg.BonusPorDominio)).Append(",\"conhecida\":").Append(B(_p.EspeciesConhecidas.Contains(id))).Append('}');
        }
        sb.Append("],\"minions\":[");
        for (int i = 0; i < _p.Minions.Count; i++)
        {
            var m = _p.Minions[i]; if (i > 0) sb.Append(',');
            sb.Append("{\"nome\":").Append(S(TabelaEspecies.Obter(m.Alma.Especie).Nome)).Append(",\"q\":").Append((int)m.Alma.Qualidade).Append(",\"nivel\":").Append(m.Nivel)
              .Append(",\"ativo\":").Append(B(m.Ativo)).Append(",\"comando\":").Append(Progressao.CustoDeComando(m.Alma.Especie, _cfg))
              .Append(",\"tracos\":").Append(Tracos(m.Alma)).Append(",\"max\":").Append(Progressao.NivelMaximo(m, _cfg)).Append(",\"estagio\":").Append(m.Estagio)
              .Append(",\"xp\":").Append(F(Math.Min(1f, m.Xp / Formulas.XpParaProximoNivelMinion(m.Nivel))))
              .Append(",\"ficha\":").Append(Ficha(m))
              .Append(",\"frag\":").Append(Sacrificio.Fragmentos(m, _cfg)).Append(",\"imbuir\":").Append(m.Alma.Qualidade < Qualidade.Profana ? Sacrificio.CustoDeImbuir(m, _cfg) : -1)
              .Append(",\"evoluir\":").Append(m.Estagio < _cfg.EstagioMaximo && m.Nivel >= Progressao.NivelMaximo(m, _cfg) ? Progressao.CustoDeEvolucao(m) : -1).Append('}');
        }
        sb.Append("],\"equipados\":[");
        for (int e = 0; e < 9; e++) { if (e > 0) sb.Append(','); AppendItem(sb, Equipamento.Obter(_p, (EspacoEquipamento)e)); }
        sb.Append("],\"inventario\":[");
        for (int i = 0; i < _p.Inventario.Count; i++) { if (i > 0) sb.Append(','); AppendItem(sb, _p.Inventario[i]); }
        var gx = new int[_p.Inventario.Count]; var gy = new int[_p.Inventario.Count];
        Equipamento.Arrumar(_p.Inventario, _cfg, gx, gy);
        sb.Append("],\"grade\":{\"w\":").Append(_cfg.LarguraDoInventario).Append(",\"h\":").Append(_cfg.AlturaDoInventario).Append(",\"pos\":[");
        for (int i = 0; i < gx.Length; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(gx[i]).Append(',').Append(gy[i]).Append(']'); }
        var armaEq = Equipamento.Obter(_p, EspacoEquipamento.Arma);
        sb.Append("]},\"dif\":").Append(Dificuldades.Atual(_p)).Append(",\"difMax\":").Append(_p.DificuldadeLiberada).Append(",\"difAgora\":").Append(_sim.Resultado.Dificuldade).Append(",\"rei\":").Append(_sim.Resultado.ReiEsqueletoMorto ? 3 : _p.MissaoReiEsqueleto != 0 ? _p.MissaoReiEsqueleto : _sim.Resultado.ReiEsqueletoSorteio).Append(",\"acougueiro\":").Append(_sim.Resultado.AcougueiroMorto ? 3 : _p.MissaoAcougueiro != 0 ? _p.MissaoAcougueiro : _sim.Resultado.AcougueiroSorteio).Append(",\"santuarios\":").Append(_p.SantuariosVistos | _sim.Resultado.SantuariosVistos).Append(",\"autoMelhoria\":").Append(B(_p.AutoMelhoria)).Append(",\"autoReparo\":").Append(B(!_p.SemAutoReparo)).Append(",\"reparoTotal\":").Append(Ferreiro.CustoDeRepararTudo(_p, _cfg)).Append(",\"ouroTotal\":").Append(_p.Ouro).Append(",\"ouroDaExpedicao\":").Append(_sim.Resultado.Ouro).Append(",\"loja\":[");
        if (_p.Loja == null || (_p.Loja.Count == 0 && _p.LojaRenovacoes == 0)) Ferreiro.Renovar(_p, _cfg);
        for (int i = 0; i < _p.Loja.Count; i++) { if (i > 0) sb.Append(','); sb.Append("{\"preco\":").Append(Ferreiro.PrecoDeCompra(_p.Loja[i], _cfg)).Append(",\"pode\":").Append(B(Ferreiro.PodeComprar(_p, i, _cfg))).Append(",\"item\":"); AppendItem(sb, _p.Loja[i]); sb.Append('}'); }
        sb.Append("],\"fragmentos\":").Append(_p.FragmentosDeAlma).Append(",\"corpoACorpo\":").Append(B(!Equipamento.Vazio(armaEq) && armaEq.Tipo == TipoItem.Foice))
          .Append(",\"auto\":[").Append(B(Auto(0))).Append(',').Append(B(Auto(1))).Append(',').Append(B(Auto(2))).Append(',').Append(B(Auto(3))).Append(',').Append(B(Auto(4))).Append("],\"autoPenalidade\":").Append((int)Math.Round((_cfg.AutoFeiticoPenalidade - 1f) * 100f))
          .Append(",\"postura\":").Append((int)_p.PosturasDosGrupos[0])
          .Append(",\"regras\":[").Append((int)_p.DesmancharAbaixoDe).Append(',').Append((int)_p.DissolverAbaixoDe).Append(',').Append((int)_p.SacrificarAbaixoDe).Append("]}");
        return sb.ToString();
    }

    static void AppendItem(StringBuilder sb, Item it)
    {
        if (Equipamento.Vazio(it)) { sb.Append("null"); return; }
        int tw, th; Equipamento.Tamanho(it.Tipo, out tw, out th);
        if (it.NaoIdentificado)
        {
            // fechado: só o tipo, a raridade e o nível aparecem; nome e propriedades só depois de identificar
            sb.Append("{\"nome\":").Append(S("Não identificado")).Append(",\"trava\":").Append(B(it.Bloqueado)).Append(",\"ouro\":").Append(Ferreiro.PrecoDeVenda(it, _cfg)).Append(",\"poder\":0,\"tipo\":").Append((int)it.Tipo)
              .Append(",\"w\":").Append(tw).Append(",\"h\":").Append(th).Append(",\"r\":").Append((int)it.Raridade).Append(",\"nivel\":").Append(it.Nivel).Append(",\"ess\":").Append(Equipamento.ValorEmEssencia(it, _cfg))
              .Append(",\"dur\":").Append(Ferreiro.DurabilidadeAtual(it, _cfg)).Append(",\"durMax\":").Append(Ferreiro.DurabilidadeMax(it, _cfg)).Append(",\"reparo\":").Append(Ferreiro.CustoDeReparo(it, _cfg)).Append(",\"nid\":true,\"custoId\":").Append(Ferreiro.CustoDeIdentificar(it, _cfg)).Append(",\"afixos\":[]}");
            return;
        }
        sb.Append("{\"nome\":").Append(S(it.Melhoria > 0 ? it.Nome + " +" + it.Melhoria : it.Nome)).Append(",\"mel\":").Append(it.Melhoria).Append(",\"custoMel\":").Append(Ferreiro.CustoDeMelhorar(it, _cfg)).Append(",\"custoImb\":").Append(Ferreiro.CustoDeImbuir(it, _cfg)).Append(",\"trava\":").Append(B(it.Bloqueado)).Append(",\"ouro\":").Append(Ferreiro.PrecoDeVenda(it, _cfg)).Append(",\"poder\":").Append(Poder.DoItem(it, _cfg)).Append(",\"tipo\":").Append((int)it.Tipo).Append(",\"w\":").Append(tw).Append(",\"h\":").Append(th).Append(",\"r\":").Append((int)it.Raridade).Append(",\"nivel\":").Append(it.Nivel).Append(",\"ess\":").Append(Equipamento.ValorEmEssencia(it, _cfg)).Append(",\"dur\":").Append(Ferreiro.DurabilidadeAtual(it, _cfg)).Append(",\"durMax\":").Append(Ferreiro.DurabilidadeMax(it, _cfg)).Append(",\"reparo\":").Append(Ferreiro.CustoDeReparo(it, _cfg)).Append(",\"afixos\":[");
        bool primeiro = true;
        if (it.Implicito != null && it.Implicito.Valor != 0f) { sb.Append(S(TabelaItens.Descrever(it.Implicito))); primeiro = false; }
        for (int i = 0; i < it.Afixos.Count; i++) { if (!primeiro) sb.Append(','); sb.Append(S(TabelaItens.Descrever(it.Afixos[i]))); primeiro = false; }
        if (it.Melhoria > 0) { if (!primeiro) sb.Append(','); sb.Append(S("Melhoria +" + it.Melhoria + ": tudo acima vale " + (int)Math.Round((Ferreiro.FatorDeMelhoria(it, _cfg) - 1f) * 100f) + "% a mais")); primeiro = false; }
        if (it.Efeito != EfeitoLendario.Nenhum) { if (!primeiro) sb.Append(','); sb.Append(S("Lendário: " + it.Efeito)); }
        sb.Append(']');
        if (it.Conjunto != ConjuntoId.Nenhum)
        {
            var c = TabelaItens.Conjunto(it.Conjunto); var bq = Equipamento.CalcularBonus(_p, _cfg);
            int tem = (int)c.Id < bq.PecasDeConjunto.Length ? bq.PecasDeConjunto[(int)c.Id] : 0;
            sb.Append(",\"conj\":{\"nome\":").Append(S(c.Nome)).Append(",\"tem\":").Append(tem).Append(",\"pecas\":[");
            for (int i = 0; i < c.NomesDasPecas.Length; i++) { if (i > 0) sb.Append(','); sb.Append(S(c.NomesDasPecas[i])); }
            sb.Append("],\"b2\":[");
            for (int i = 0; i < c.BonusCom2.Length; i++) { if (i > 0) sb.Append(','); sb.Append(S(TabelaItens.Descrever(c.BonusCom2[i]))); }
            sb.Append("],\"b3\":[");
            for (int i = 0; i < c.BonusCom3.Length; i++) { if (i > 0) sb.Append(','); sb.Append(S(TabelaItens.Descrever(c.BonusCom3[i]))); }
            sb.Append("]}");
        }
        sb.Append('}');
    }

    static string S(string s)
    {
        var sb = new StringBuilder((s ?? "").Length + 2); sb.Append('"');
        foreach (char c in s ?? "")
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c < ' ') sb.Append(' ');
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
    static readonly string[] Papeis = { "Tanque: segura a linha de frente", "Lutador: corpo a corpo", "Atirador: ataca de longe, mira o mais ferido", "Conjurador: feitiços de longe", "Suporte: ajuda os aliados", "Enxame: fraco sozinho, forte em bando" };
    static readonly string[] Familias = { "Morto-vivo", "Demônio", "Besta", "Aberração", "Espírito" };
    static readonly string[] Portes = { "Pequeno", "Médio", "Grande", "Colossal" };

    /// <summary>Atributos reais do minion na próxima expedição (com qualidade, nível, Poder, equipamento, talentos e Bestiário).</summary>
    static string Ficha(MinionSalvo m)
    {
        var d = TabelaEspecies.Obter(m.Alma.Especie);
        float vida, dano, vel, arm; Poder.Atributos(_p, m, _cfg, out vida, out dano, out vel, out arm);
        var sb = new StringBuilder(256);
        sb.Append("{\"sprite\":").Append(S(m.Alma.Especie.ToString().ToLowerInvariant())).Append(",\"vida\":").Append(F(vida)).Append(",\"dano\":").Append(F(dano))
          .Append(",\"armadura\":").Append(F(arm)).Append(",\"vel\":").Append(F(vel)).Append(",\"dps\":").Append(F(dano * vel)).Append(",\"alcance\":").Append(F(d.Alcance))
          .Append(",\"mov\":").Append(F(d.VelMovimento)).Append(",\"tipoDano\":").Append(S(d.TipoDano.ToString())).Append(",\"papel\":").Append(S(Papeis[(int)d.Papel]))
          .Append(",\"familia\":").Append(S(Familias[(int)d.Familia])).Append(",\"porte\":").Append(S(Portes[(int)d.Porte]))
          .Append(",\"dominio\":").Append(Bestiario.Dominio(_p, m.Alma.Especie, _cfg)).Append(",\"qmult\":").Append(F(_cfg.MultQualidade[(int)m.Alma.Qualidade])).Append(",\"poder\":").Append(Poder.DeAtributos(vida, dano, vel, arm)).Append(",\"dons\":").Append(Dons(m.Alma)).Append('}');
        return sb.ToString();
    }

    static string Dons(Alma a) { return "[" + ((int)Math.Round(a.DomVida * 100f)) + "," + ((int)Math.Round(a.DomDano * 100f)) + "," + ((int)Math.Round(a.DomVelocidade * 100f)) + "]"; }
    static string Tracos(Alma a)
    {
        var sb = new StringBuilder("[");
        if (a.Afixos != null) for (int i = 0; i < a.Afixos.Count; i++) { if (i > 0) sb.Append(','); sb.Append((int)a.Afixos[i]); }
        if (a.Traco != TracoProfano.Nenhum) { if (sb.Length > 1) sb.Append(','); sb.Append(10 + (int)a.Traco); }
        return sb.Append(']').ToString();
    }
    static string B(bool b) { return b ? "true" : "false"; }

    [JSExport] public static bool Melhorar(int construcao) { return Necropole.Melhorar(_p, (Construcao)construcao, _cfg); }
    [JSExport] public static bool Erguer(int indice) { return indice >= 0 && indice < _p.Filacterio.Count && Progressao.ErguerMinion(_p, indice, _cfg) != null; }
    [JSExport]
    public static void AlternarMinion(int i)
    {
        if (i < 0 || i >= _p.Minions.Count) return;
        if (_p.Minions[i].Ativo) Progressao.Desativar(_p, i); else Progressao.Ativar(_p, i, _cfg);
    }
    // ---- mão do jogador: valem no próximo tick ----
    [JSExport] public static void Ceifar(int orbeId) { _sim.ComandoCeifar(orbeId); }
    [JSExport] public static void Pacto(int opcao) { _sim.ComandoPacto(opcao); }
    [JSExport] public static void Marcar(int entidadeId) { _sim.ComandoMarcar(entidadeId); }
    [JSExport] public static void Curar(int entidadeId) { _sim.ComandoCurar(entidadeId); }
    [JSExport] public static void EscolherBencao(int opcao) { _sim.ComandoEscolherBencao(opcao); }
    [JSExport] public static bool IrPara(int sala) { return _sim.ComandoIrPara(sala); }
    [JSExport] public static void Interagir() { _sim.ComandoInteragir(); }
    [JSExport] public static bool Quebrar(int indice) { return _sim.ComandoQuebrar(indice); }
    [JSExport] public static void Detonar() { _sim.ComandoDetonar(); }
    [JSExport] public static void Frenesi() { _sim.ComandoFrenesi(); }
    [JSExport] public static bool Fundir(int especie, int qualidade) { return Fusao.Fundir(_p, (EspecieId)especie, (Qualidade)qualidade, _cfg) != null; }
    [JSExport] public static bool Evoluir(int i) { return i >= 0 && i < _p.Minions.Count && Progressao.EvoluirEstagio(_p, i, _cfg); }
    [JSExport] public static bool Investir(int i) { return Talentos.Investir(_p, i, _cfg); }
    [JSExport] public static bool Redistribuir() { return Talentos.Redistribuir(_p, _cfg); }
    [JSExport] public static bool Sacrificar(int i) { return Sacrificio.Sacrificar(_p, i, _cfg) > 0; }
    [JSExport] public static bool Imbuir(int i) { return Sacrificio.Imbuir(_p, i, _cfg); }
    /// <summary>Regras automáticas: 0 = desmanchar itens abaixo da raridade; 1 = dissolver almas abaixo da qualidade; 2 = sacrificar minions da reserva abaixo da qualidade.</summary>
    [JSExport]
    public static void Regra(int qual, int valor)
    {
        valor = Math.Max(0, Math.Min(4, valor));
        if (qual == 0) _p.DesmancharAbaixoDe = (RaridadeItem)valor;
        else if (qual == 1) _p.DissolverAbaixoDe = (Qualidade)valor;
        else if (qual == 2) { _p.SacrificarAbaixoDe = (Qualidade)valor; Sacrificio.AplicarRegra(_p, _cfg); }
    }
    [JSExport] public static bool Ofertar(int tipo) { return Oferendas.Ofertar(_p, tipo, _cfg); }
    [JSExport] public static bool PrepararRito(int rito) { return Oferendas.PrepararRito(_p, (Rito)rito, _cfg); }
    [JSExport]
    public static void Travar(int indice, bool equipado)
    {
        Item it = equipado ? Equipamento.Obter(_p, (EspacoEquipamento)indice) : (indice >= 0 && indice < _p.Inventario.Count ? _p.Inventario[indice] : null);
        if (!Equipamento.Vazio(it)) it.Bloqueado = !it.Bloqueado;
    }
    /// <summary>Vende (desmancha) tudo o que não está travado, até a raridade dada. Devolve "itens Essência".</summary>
    [JSExport]
    public static string VenderTudo(int ateRaridade)
    {
        int n; long e = Equipamento.DesmancharTudo(_p, _cfg, (RaridadeItem)Math.Max(0, Math.Min(4, ateRaridade)), out n);
        return n + " " + e;
    }
    /// <summary>Postura do exército inteiro: 0 Agressiva, 1 Defensiva, 2 Escolta. Vale a partir da próxima expedição.</summary>
    [JSExport]
    public static void Postura(int valor)
    {
        valor = Math.Max(0, Math.Min(2, valor));
        for (int i = 0; i < _p.PosturasDosGrupos.Length; i++) _p.PosturasDosGrupos[i] = (Postura)valor;
    }
    [JSExport] public static bool ComprarLegado(int i) { return Ascensao.Comprar(_p, i, _cfg); }
    /// <summary>Ascende: zera a vida atual, soma as Cinzas e começa uma expedição nova. Devolve as Cinzas ganhas.</summary>
    [JSExport]
    public static int Ascender()
    {
        long c = Ascensao.Ascender(_p, _cfg);
        if (c > 0) NovaExpedicao();
        return (int)c;
    }
    static bool Auto(int i) { return _p.AutoFeiticos != null && i < _p.AutoFeiticos.Length && _p.AutoFeiticos[i]; }
    [JSExport] public static void FeiticoDeItem(int espaco) { _sim.ComandoFeiticoDeItem(espaco); }
    /// <summary>Liga ou desliga um feitiço automático (0 Detonar, 1 Frenesi, 2 Sopro, 3 Arma, 4 Mão secundária). Vale na próxima expedição.</summary>
    [JSExport]
    public static void AlternarAuto(int i)
    {
        if (_p.AutoFeiticos == null || _p.AutoFeiticos.Length < 5) { var n = new bool[5]; if (_p.AutoFeiticos != null) Array.Copy(_p.AutoFeiticos, n, Math.Min(5, _p.AutoFeiticos.Length)); _p.AutoFeiticos = n; }
        if (i >= 0 && i < 5) _p.AutoFeiticos[i] = !_p.AutoFeiticos[i];
    }
    /// <summary>Painel de testes. Altera o save direto; não faz parte do jogo normal.</summary>
    [JSExport]
    public static string Cheat(string nome, double valor)
    {
        _p.UsouCheats = true;
        long v = (long)Math.Max(0, Math.Min(1e15, valor));
        var rng = new Rng(_p.ContadorDeSementes++ * 7919UL + 13UL);
        switch (nome)
        {
            case "ossos": _p.Ossos = v; break;
            case "essencia": _p.Essencia = v; break;
            case "sangue": _p.Sangue = v; break;
            case "fragmentos": _p.FragmentosDeAlma = v; break;
            case "cinzas": _p.Cinzas = v; break;
            case "restos": for (int i = 0; i < _p.Restos.Length; i++) _p.Restos[i] += v; if (_p.OferendasAutomaticas) Oferendas.OfertarTudo(_p, _cfg); break;
            case "nivel": _p.NivelNecromante = (int)Math.Max(1, Math.Min(999, v)); _p.XpNecromante = 0f; break;
            case "sorte": _p.Sorte = (float)Math.Max(0, Math.Min(1000, valor)); break;
            case "poder": _p.PoderNecromantico = (float)Math.Max(0, Math.Min(100000, valor)) / 100f; break;
            case "comando": _p.ComandoMaximo = (int)Math.Max(1, Math.Min(60, v)); break;
            case "andar":
                _p.MaiorAndarConcluido = (int)Math.Max(0, Math.Min(200, v)); _p.AndarDeAncoragem = _p.MaiorAndarConcluido + 1;
                for (int ato = 1; ato <= _p.MaiorAndarConcluido / _cfg.AndaresPorAto; ato++) if (!_p.ChefesDerrotados.Contains(ato)) _p.ChefesDerrotados.Add(ato);
                break;
            case "itens":
                for (int i = 0; i < (int)Math.Min(30, v); i++)
                {
                    var it = GeradorDeItens.Gerar(rng, _cfg, Math.Max(1, _p.MaiorAndarConcluido), rng.Chance(0.25f) ? RaridadeItem.Lendario : RaridadeItem.Raro);
                    it.NaoIdentificado = _cfg.ItensNaoIdentificados;   // como cairiam na masmorra
                    if (Equipamento.Cabe(_p, it, _cfg)) _p.Inventario.Add(it);
                }
                break;
            case "almas":
                for (int i = 0; i < (int)Math.Min(30, v); i++)
                    _p.Filacterio.Add(GeradorDeAlmas.Gerar(rng, _cfg, (EspecieId)(1 + rng.Intervalo(0, TabelaEspecies.Total - 1)), (Qualidade)rng.Intervalo(1, 5)));
                break;
            case "missao": if (Missoes.Atual(_p, _cfg) != null) { _p.MissaoAtual++; _p.ProgressoMissao = 0; } break;
            case "conjunto":
                for (int c = 0; c < TabelaItens.Conjuntos.Length; c++) for (int i = 0; i < 3; i++)
                { var pc = GeradorDeItens.GerarPecaDeConjunto(rng, _cfg, Math.Max(1, _p.MaiorAndarConcluido), TabelaItens.Conjuntos[c].Id, i); if (Equipamento.Cabe(_p, pc, _cfg)) _p.Inventario.Add(pc); }
                break;
            case "sala": _cfg.ForcarSalaEspecial = (int)v; break;
            case "dificuldade": _p.DificuldadeLiberada = (int)Math.Max(0, Math.Min(2, v)); break;
            case "livros": for (int li = 0; li < (int)Math.Min(30, v); li++) Grimorio.Receber(_p, (FeiticoId)(li % 6), _cfg); break;
            case "rei": _cfg.ForcarReiEsqueleto = true; _p.MissaoReiEsqueleto = 1; break;
            case "acougueiro": _cfg.ForcarAcougueiro = true; _p.MissaoAcougueiro = 1; break;
            case "santuario": _cfg.ForcarSalaEspecial = 4; _cfg.ForcarSantuario = (int)v; break;
            case "secreto": _forcarSecreto = true; _cfg.ForcarSecreto = v >= 1 && v <= 3 ? (int)v : 0; break;
            case "cofre": _forcarCofre = true; break;
            case "reiniciar": break;
        }
        NovaExpedicao();    // o que foi mexido vale já
        return nome;
    }
    [JSExport] public static bool Resgatar() { return Missoes.Resgatar(_p, _cfg, _sim.Resultado); }
    [JSExport] public static int AutoEquipar() { return Equipamento.AutoEquipar(_p, _cfg); }
    [JSExport]
    public static void Equipar(int i)
    {
        if (i < 0 || i >= _p.Inventario.Count || _p.Inventario[i] == null || _p.Inventario[i].NaoIdentificado) return;
        var it = _p.Inventario[i]; _p.Inventario.RemoveAt(i);
        var saiu = Equipamento.Equipar(_p, it);
        if (!Equipamento.Vazio(saiu)) _p.Inventario.Add(saiu);
    }
    [JSExport] public static void Desequipar(int espaco) { Equipamento.Desequipar(_p, (EspacoEquipamento)espaco, _cfg); }
    [JSExport] public static bool IdentificarItem(int i) { return i >= 0 && i < _p.Inventario.Count && Ferreiro.Identificar(_p, _p.Inventario[i], _cfg); }
    [JSExport] public static int IdentificarTudo() { return Ferreiro.IdentificarTudo(_p, _cfg); }
    [JSExport] public static bool MelhorarEquipado(int espaco) { return espaco >= 0 && espaco < _p.Equipados.Length && Ferreiro.Melhorar(_p, _p.Equipados[espaco], _cfg); }
    [JSExport] public static int MelhorarTudo() { return Ferreiro.MelhorarTudo(_p, _cfg); }
    [JSExport] public static bool ImbuirEquipado(int espaco) { return espaco >= 0 && espaco < _p.Equipados.Length && Ferreiro.Imbuir(_p, _p.Equipados[espaco], _cfg); }
    [JSExport] public static void AlternarAutoMelhoria() { _p.AutoMelhoria = !_p.AutoMelhoria; }
    [JSExport] public static bool RepararEquipado(int espaco) { return espaco >= 0 && espaco < _p.Equipados.Length && Ferreiro.Reparar(_p, _p.Equipados[espaco], _cfg); }
    [JSExport] public static bool GastarAtributo(int qual, int pontos) { return qual >= 0 && qual < 4 && Atributos.Gastar(_p, (Atributo)qual, pontos, _cfg); }
    [JSExport] public static void AlternarAutoAtributos() { _p.SemAutoAtributos = !_p.SemAutoAtributos; }
    [JSExport] public static bool RedistribuirAtributos() { return Atributos.Redistribuir(_p, _cfg); }
    [JSExport] public static bool DefinirDificuldade(int d) { return Dificuldades.Definir(_p, d); }
    [JSExport] public static void AlternarAutoReparo() { _p.SemAutoReparo = !_p.SemAutoReparo; }
    [JSExport] public static int RepararTudo() { return Ferreiro.RepararTudo(_p, _cfg); }
    [JSExport] public static int VenderFechados() { int n = 0; for (int i = _p.Inventario.Count - 1; i >= 0; i--) if (_p.Inventario[i] != null && _p.Inventario[i].NaoIdentificado && Ferreiro.Vender(_p, _p.Inventario[i], _cfg) > 0) n++; return n; }
    [JSExport] public static void VenderItem(int i) { if (i >= 0 && i < _p.Inventario.Count) Ferreiro.Vender(_p, _p.Inventario[i], _cfg); }
    [JSExport] public static bool ComprarItem(int i) { return Ferreiro.Comprar(_p, i, _cfg); }
    [JSExport] public static void Desmanchar(int i) { if (i >= 0 && i < _p.Inventario.Count) Equipamento.Desmanchar(_p, _p.Inventario[i], _cfg); }
    /// <summary>Abandona a expedição atual e começa outra já com o exército e os itens novos.</summary>
    [JSExport] public static void Recomecar() { Progressao.AplicarResultado(_p, _sim.Resultado, _cfg); NovaExpedicao(); }
    [JSExport] public static void NovoJogo() { _p = Progressao.NovoJogo(_cfg); NovaExpedicao(); }
}
