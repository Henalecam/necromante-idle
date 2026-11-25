using UnityEngine;
using Necro.Nucleo;

namespace Necro.Visual
{
    /// <summary>
    /// Interface provisória em IMGUI (sem arte): barra superior, vida/mana do necromante, e dois painéis —
    /// Necrópole (tecla N: construções e erguer almas) e Equipamento (tecla E: itens e auto-equipar).
    /// As compras valem a partir da próxima expedição, porque a simulação lê o progresso ao começar.
    /// </summary>
    [RequireComponent(typeof(VisualArena))]
    public class Interface : MonoBehaviour
    {
        VisualArena _arena;
        bool _necropole, _equipamento;
        Texture2D _branco;
        Vector2 _rolagem;

        static readonly string[] NomesDosEspacos = { "Arma", "Mão secundária", "Elmo", "Peitoral", "Luvas", "Botas", "Amuleto", "Anel 1", "Anel 2" };
        static readonly Color[] CorDaRaridade = { new Color(0.8f, 0.8f, 0.8f), new Color(0.45f, 0.6f, 1f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.55f, 0.15f), new Color(0.75f, 0.3f, 1f) };
        static readonly Color[] CorDaQualidade = { new Color(0.6f, 0.6f, 0.6f), Color.white, new Color(0.45f, 1f, 0.5f), new Color(0.5f, 0.7f, 1f), new Color(0.85f, 0.35f, 1f) };

        void Awake()
        {
            _arena = GetComponent<VisualArena>();
            _branco = new Texture2D(1, 1);
            _branco.SetPixel(0, 0, Color.white); _branco.Apply();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) _arena.velocidade = 1f;
            if (Input.GetKeyDown(KeyCode.Alpha2)) _arena.velocidade = 2f;
            if (Input.GetKeyDown(KeyCode.Alpha3)) _arena.velocidade = 4f;
            if (Input.GetKeyDown(KeyCode.N)) { _necropole = !_necropole; _equipamento = false; }
            if (Input.GetKeyDown(KeyCode.E)) { _equipamento = !_equipamento; _necropole = false; }
        }

        void Barra(Rect r, float fracao, Color cor, string texto)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(r, _branco);
            GUI.color = cor; GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(fracao), r.height - 2), _branco);
            GUI.color = Color.white; GUI.Label(new Rect(r.x + 6, r.y, r.width, r.height), texto);
        }

        void OnGUI()
        {
            var sim = _arena.Sim; var p = _arena.Progresso; var cfg = _arena.Config;
            if (sim == null || p == null) return;

            // barra superior
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(0, 0, Screen.width, 26), _branco); GUI.color = Color.white;
            GUI.Label(new Rect(10, 3, 900, 22), "Andar " + sim.Andar + "  (recorde " + p.MaiorAndarConcluido + ")     Ossos " + p.Ossos + "     Essência " + p.Essencia +
                      "     Sangue " + p.Sangue + "     Almas " + p.Filacterio.Count + "/" + Necropole.CapacidadeDoFilacterio(p, cfg) +
                      "     Comando " + Progressao.ComandoEmUso(p, cfg) + "/" + Progressao.ComandoMaximoEfetivo(p, cfg));
            GUI.Label(new Rect(Screen.width - 330, 3, 330, 22), "Velocidade " + _arena.velocidade + "x [1 2 3]   Necrópole [N]   Itens [E]");

            // vida e mana do necromante
            var n = sim.Necromante;
            if (n != null)
            {
                Barra(new Rect(10, Screen.height - 54, 260, 20), n.Vida / Mathf.Max(1f, n.VidaMax), new Color(0.6f, 0.05f, 0.05f), "Vida " + Mathf.RoundToInt(n.Vida) + " / " + Mathf.RoundToInt(n.VidaMax));
                Barra(new Rect(10, Screen.height - 30, 260, 20), n.Mana / Mathf.Max(1f, n.ManaMax), new Color(0.1f, 0.25f, 0.7f), "Mana " + Mathf.RoundToInt(n.Mana) + " / " + Mathf.RoundToInt(n.ManaMax));
            }

            if (_necropole) PainelNecropole(p, cfg);
            if (_equipamento) PainelEquipamento(p, cfg);
        }

        Rect Painel(string titulo)
        {
            var r = new Rect(Screen.width / 2 - 330, 50, 660, Screen.height - 130);
            GUI.color = new Color(0.05f, 0.03f, 0.05f, 0.92f); GUI.DrawTexture(r, _branco); GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 14, r.y + 8, 500, 22), titulo + "   (as mudanças valem na próxima expedição)");
            return r;
        }

        void PainelNecropole(EstadoProgresso p, ConfigJogo cfg)
        {
            var r = Painel("NECRÓPOLE");
            float y = r.y + 40;
            for (int c = 0; c < Necropole.TotalDeConstrucoes; c++)
            {
                var id = (Construcao)c; var d = Necropole.Dados(id, cfg);
                long ossos, ess; Necropole.Custo(p, id, cfg, out ossos, out ess);
                bool aberta = Necropole.Desbloqueada(p, id, cfg);
                GUI.Label(new Rect(r.x + 14, y, 420, 22), d.Nome + "  — nível " + Necropole.Nivel(p, id) + "/" + d.NivelMaximo);
                GUI.Label(new Rect(r.x + 14, y + 18, 420, 22), aberta ? d.Efeito : "Abre ao concluir o andar " + d.AndarDeDesbloqueio);
                GUI.enabled = Necropole.PodeMelhorar(p, id, cfg);
                if (GUI.Button(new Rect(r.x + 440, y + 2, 200, 34), "Melhorar: " + ossos + " Ossos" + (ess > 0 ? " + " + ess + " Ess." : "")))
                { Necropole.Melhorar(p, id, cfg); _arena.Salvar(); }
                GUI.enabled = true;
                y += 48;
            }

            y += 6;
            GUI.Label(new Rect(r.x + 14, y, 600, 22), "FILACTÉRIO — almas prontas para erguer");
            y += 24;
            int linhas = Mathf.Min(p.Filacterio.Count, Mathf.FloorToInt((r.yMax - y - 120) / 26f));
            for (int i = 0; i < linhas; i++)
            {
                var a = p.Filacterio[i];
                long custo = Necropole.CustoDeErguer(p, a, cfg);
                GUI.color = CorDaQualidade[(int)a.Qualidade];
                GUI.Label(new Rect(r.x + 14, y, 420, 22), TabelaEspecies.Obter(a.Especie).Nome + " — " + a.Qualidade + "   (Comando " + Progressao.CustoDeComando(a.Especie, cfg) + ")");
                GUI.color = Color.white;
                GUI.enabled = p.Essencia >= custo;
                if (GUI.Button(new Rect(r.x + 440, y, 200, 22), "Erguer: " + custo + " Essência")) { Progressao.ErguerMinion(p, i, cfg); _arena.Salvar(); break; }
                GUI.enabled = true;
                y += 26;
            }
            if (p.Filacterio.Count > linhas) GUI.Label(new Rect(r.x + 14, y, 400, 22), "... e mais " + (p.Filacterio.Count - linhas));

            // exército: ativar / desativar
            y = r.yMax - 104;
            GUI.Label(new Rect(r.x + 14, y, 600, 22), "EXÉRCITO — clique para ativar ou desativar");
            float x = r.x + 14; y += 24;
            for (int i = 0; i < p.Minions.Count && y < r.yMax - 24; i++)
            {
                var m = p.Minions[i];
                GUI.color = m.Ativo ? new Color(0.5f, 1f, 0.6f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUI.Button(new Rect(x, y, 154, 22), TabelaEspecies.Obter(m.Alma.Especie).Nome + " nv " + m.Nivel))
                {
                    if (m.Ativo) Progressao.Desativar(p, i); else Progressao.Ativar(p, i, cfg);
                    _arena.Salvar();
                }
                x += 158; if (x > r.xMax - 160) { x = r.x + 14; y += 25; }
            }
            GUI.color = Color.white;
        }

        void PainelEquipamento(EstadoProgresso p, ConfigJogo cfg)
        {
            var r = Painel("EQUIPAMENTO");
            if (GUI.Button(new Rect(r.xMax - 174, r.y + 6, 160, 24), "Auto-equipar")) { Equipamento.AutoEquipar(p, cfg); _arena.Salvar(); }
            float y = r.y + 40;
            for (int e = 0; e < 9; e++)
            {
                var it = Equipamento.Obter(p, (EspacoEquipamento)e);
                GUI.Label(new Rect(r.x + 14, y, 130, 22), NomesDosEspacos[e]);
                if (!Equipamento.Vazio(it))
                {
                    GUI.color = CorDaRaridade[(int)it.Raridade];
                    GUI.Label(new Rect(r.x + 150, y, 380, 22), it.Nome + "  (nível " + it.Nivel + ")");
                    GUI.color = Color.white;
                    if (GUI.Button(new Rect(r.xMax - 114, y, 100, 20), "Tirar")) { Equipamento.Desequipar(p, (EspacoEquipamento)e, cfg); _arena.Salvar(); }
                }
                else GUI.Label(new Rect(r.x + 150, y, 380, 22), "—");
                y += 24;
            }
            y += 8;
            GUI.Label(new Rect(r.x + 14, y, 600, 22), "INVENTÁRIO  " + p.Inventario.Count + "/" + cfg.CapacidadeInventario);
            y += 24;
            int max = Mathf.FloorToInt((r.yMax - y - 10) / 24f);
            for (int i = 0; i < p.Inventario.Count && i < max; i++)
            {
                var it = p.Inventario[i];
                GUI.color = CorDaRaridade[(int)it.Raridade];
                GUI.Label(new Rect(r.x + 14, y, 400, 22), it.Nome + "  (nível " + it.Nivel + ", " + it.Afixos.Count + " afixos)");
                GUI.color = Color.white;
                if (GUI.Button(new Rect(r.xMax - 224, y, 100, 20), "Equipar"))
                {
                    p.Inventario.RemoveAt(i);
                    var saiu = Equipamento.Equipar(p, it);
                    if (!Equipamento.Vazio(saiu)) p.Inventario.Add(saiu);
                    _arena.Salvar(); break;
                }
                if (GUI.Button(new Rect(r.xMax - 114, y, 100, 20), "+" + Equipamento.ValorEmEssencia(it, cfg) + " Ess.")) { Equipamento.Desmanchar(p, it, cfg); _arena.Salvar(); break; }
                y += 24;
            }
        }
    }
}
