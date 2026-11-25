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
            try { _p = JsonSerializer.Deserialize<EstadoProgresso>(save, Json); } catch { _p = null; }
        }
        if (_p == null || _p.Minions == null) _p = Progressao.NovoJogo(_cfg);
        if (_p.Equipados == null || _p.Equipados.Length != 9) _p.Equipados = new Item[9];
        NovaExpedicao();
    }

    static void NovaExpedicao()
    {
        _sim = new Simulacao(_cfg, _p, 1003UL + _p.ContadorDeSementes++, ModoExpedicao.Avancar, Progressao.AndarDePartida(_p, _cfg));
    }

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
           .Append(F(Math.Max(0f, _sim.FrenesiTempo))).Append(' ').Append(_sim.CadaveresDetonaveis).Append('\n');
        if (_sim.PactosOferecidos != null)
            _sb.Append("K ").Append(_sim.PactosOferecidos[0]).Append(' ').Append(_sim.PactosOferecidos[1]).Append(' ').Append(_sim.PactosOferecidos[2]).Append(' ').Append(F(_sim.TempoDaOferta)).Append('\n');
        if (_sim.PactosAtivos.Count > 0) { _sb.Append('A'); foreach (int pa in _sim.PactosAtivos) _sb.Append(' ').Append(pa); _sb.Append('\n'); }
        foreach (var e in _sim.Entidades)
            _sb.Append("E ").Append(e.Id).Append(' ').Append(e.Especie).Append(' ').Append(e.Faccao == Faccao.Inimigo ? 1 : 0).Append(' ')
              .Append(F(e.Pos.X)).Append(' ').Append(F(e.Pos.Y)).Append(' ').Append(F(e.Olhar.X)).Append(' ').Append(F(e.Olhar.Y)).Append(' ')
              .Append(e.Animacao).Append(' ').Append(e.Viva ? 1 : 0).Append(' ').Append(F(e.Vida / Math.Max(1f, e.VidaMax))).Append(' ')
              .Append(e.Elite ? 1 : 0).Append(' ').Append(e.Chefe ? 1 : 0).Append(' ').Append(e == _sim.Necromante ? 1 : 0).Append('\n');
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
        var sb = new StringBuilder(4096);
        sb.Append("{\"andar\":").Append(_sim.Andar).Append(",\"recorde\":").Append(_p.MaiorAndarConcluido).Append(",\"ossos\":").Append(_p.Ossos)
          .Append(",\"essencia\":").Append(_p.Essencia).Append(",\"sangue\":").Append(_p.Sangue).Append(",\"nivel\":").Append(_p.NivelNecromante)
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
              .Append(",\"tracos\":").Append(Tracos(a)).Append('}');
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
        for (int e = 1; e <= 7; e++)
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
              .Append(",\"evoluir\":").Append(m.Estagio < _cfg.EstagioMaximo && m.Nivel >= Progressao.NivelMaximo(m, _cfg) ? Progressao.CustoDeEvolucao(m) : -1).Append('}');
        }
        sb.Append("],\"equipados\":[");
        for (int e = 0; e < 9; e++) { if (e > 0) sb.Append(','); AppendItem(sb, Equipamento.Obter(_p, (EspacoEquipamento)e)); }
        sb.Append("],\"inventario\":[");
        for (int i = 0; i < _p.Inventario.Count; i++) { if (i > 0) sb.Append(','); AppendItem(sb, _p.Inventario[i]); }
        sb.Append("]}");
        return sb.ToString();
    }

    static void AppendItem(StringBuilder sb, Item it)
    {
        if (Equipamento.Vazio(it)) { sb.Append("null"); return; }
        sb.Append("{\"nome\":").Append(S(it.Nome)).Append(",\"r\":").Append((int)it.Raridade).Append(",\"nivel\":").Append(it.Nivel).Append(",\"ess\":").Append(Equipamento.ValorEmEssencia(it, _cfg)).Append(",\"afixos\":[");
        bool primeiro = true;
        if (it.Implicito != null && it.Implicito.Valor != 0f) { sb.Append(S(TabelaItens.Descrever(it.Implicito))); primeiro = false; }
        for (int i = 0; i < it.Afixos.Count; i++) { if (!primeiro) sb.Append(','); sb.Append(S(TabelaItens.Descrever(it.Afixos[i]))); primeiro = false; }
        if (it.Efeito != EfeitoLendario.Nenhum) { if (!primeiro) sb.Append(','); sb.Append(S("Lendário: " + it.Efeito)); }
        sb.Append("]}");
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
    [JSExport] public static void Detonar() { _sim.ComandoDetonar(); }
    [JSExport] public static void Frenesi() { _sim.ComandoFrenesi(); }
    [JSExport] public static bool Fundir(int especie, int qualidade) { return Fusao.Fundir(_p, (EspecieId)especie, (Qualidade)qualidade, _cfg) != null; }
    [JSExport] public static bool Evoluir(int i) { return i >= 0 && i < _p.Minions.Count && Progressao.EvoluirEstagio(_p, i, _cfg); }
    [JSExport] public static bool Resgatar() { return Missoes.Resgatar(_p, _cfg, _sim.Resultado); }
    [JSExport] public static int AutoEquipar() { return Equipamento.AutoEquipar(_p, _cfg); }
    [JSExport]
    public static void Equipar(int i)
    {
        if (i < 0 || i >= _p.Inventario.Count) return;
        var it = _p.Inventario[i]; _p.Inventario.RemoveAt(i);
        var saiu = Equipamento.Equipar(_p, it);
        if (!Equipamento.Vazio(saiu)) _p.Inventario.Add(saiu);
    }
    [JSExport] public static void Desequipar(int espaco) { Equipamento.Desequipar(_p, (EspacoEquipamento)espaco, _cfg); }
    [JSExport] public static void Desmanchar(int i) { if (i >= 0 && i < _p.Inventario.Count) Equipamento.Desmanchar(_p, _p.Inventario[i], _cfg); }
    /// <summary>Abandona a expedição atual e começa outra já com o exército e os itens novos.</summary>
    [JSExport] public static void Recomecar() { Progressao.AplicarResultado(_p, _sim.Resultado, _cfg); NovaExpedicao(); }
    [JSExport] public static void NovoJogo() { _p = Progressao.NovoJogo(_cfg); NovaExpedicao(); }
}
