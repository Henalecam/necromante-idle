// Desenho e interface da versão de navegador. As regras do jogo rodam no núcleo C# (WebAssembly): aqui só se desenha.
const ESPECIES = ['esqueleto', 'zumbi', 'diabrete', 'necromante', 'necromante_foice', 'arqueiroossudo', 'acolitocaido', 'abadessacosturada', 'caodecripta', 'esqueleto_elite', 'carnical', 'diabodeguerra', 'aparicao', 'colossodeossos', 'resprofana', 'carcereirodealmas'];
const ACAO = { Parado: 'parado', Andando: 'andar', Atacando: 'atacar', Conjurando: 'conjurar', Morrendo: 'morrer' };
const TPS = 10, CHAVE = 'necromante-idle-save-v1';
const $ = (id) => document.getElementById(id);
const tela = $('tela'), ctx = tela.getContext('2d');

function imagem(src) {
  return new Promise((ok, erro) => { const i = new Image(); i.onload = () => ok(i); i.onerror = () => erro(new Error('imagem ' + src)); i.src = src; });
}
async function json(src) { return (await fetch(src)).json(); }

const monstros = {};
// ---- vista isométrica (losango 2:1, como nos ARPGs clássicos): x da simulação desce para a direita, y sobe para a direita ----
const K = 40, PAREDE = 150, OX = 40 + 2 * K, OY = PAREDE + 20 + 7 * K, CW = OX + 34 * K + 40, CH = OY + 11 * K + 40, VISTA_H = 640;
const P = (x, y) => [OX + (x + y) * K, OY + (x - y) * K / 2];
// o andar inteiro é um só mundo: cada sala fica na sua célula do mapa, ligada às vizinhas por corredores
const PASSO_X = 29, PASSO_Y = 21;                      // distância entre salas vizinhas, em unidades da simulação
const W = (x, y) => [(x + y) * K, (x - y) * K / 2];
const offDaSala = (i) => (mapa[i] ? [mapa[i][0] * PASSO_X, mapa[i][1] * PASSO_Y] : [0, 0]);
let so = [0, 0];                                       // deslocamento da sala atual no mundo
let arena, chao, sangue, sctx;

async function carregarArte() {
  arena = { px_por_unidade_x: K * Math.SQRT2, px_por_unidade_y: K * Math.SQRT1_2 };
  chao = document.createElement('canvas'); chao.width = CW; chao.height = CH;
  sangue = document.createElement('canvas'); sangue.width = CW; sangue.height = CH; sctx = sangue.getContext('2d');
  construirSala();
  await Promise.all(ESPECIES.map(async (n) => {
    // conjunto de sprites ausente (ainda não renderizado) não derruba o jogo: quem usa cai no sprite padrão
    let meta; try { const r = await fetch(`arte/${n}/${n}.json`); if (!r.ok) return; meta = await r.json(); } catch (e) { return; }
    const acoes = {};
    await Promise.all(meta.acoes.map(async (a) => { acoes[a.nome] = { ...a, img: await imagem(`arte/${n}/${a.arquivo.replace('.png', '.webp')}`) }; }));
    monstros[n] = { meta, acoes, W: meta.largura_frame, H: meta.altura_frame, pivo: meta.pivo_px };
  }));
}

// espécies ainda sem modelo próprio: usam um sprite existente, com outra escala e cor, até o render delas ficar pronto
const PROVISORIO = { diablo: ['diabrete', 3.1, '150,14,10', 0.42], lazarus: ['acolitocaido', 1.9, '120,20,20', 0.5], cavaleironegro: ['esqueleto_elite', 1.28, '14,14,26', 0.6], magistrado: ['acolitocaido', 1.1, '150,60,220', 0.45], sucubo: ['acolitocaido', 1.05, '215,40,95', 0.45], carnical: ['zumbi', 0.92, '110,150,175', 0.38], diabodeguerra: ['diabrete', 1.9, '50,8,8', 0.32], aparicao: ['acolitocaido', 1.0, '110,190,255', 0.5], colossodeossos: ['esqueleto', 1.75, '80,70,50', 0.28], resprofana: ['caodecripta', 1.55, '235,225,215', 0.5], carcereirodealmas: ['aparicao', 2.1, '46,12,78', 0.8] };
// tom fixo por cima do sprite próprio (a Aparição sai clara demais do render: ganha um azul espectral)
const TINTA_FIXA = { aparicao: ['aparicao', 1, '70,130,200', 0.38] };
function visual(especie) { const n = especie.toLowerCase(); return !monstros[n] && PROVISORIO[n] ? PROVISORIO[n] : TINTA_FIXA[n] || null; }
function monstro(especie, elite) {
  let n = especie.toLowerCase();
  if (!monstros[n] && PROVISORIO[n]) n = PROVISORIO[n][0];
  return (elite && monstros[n + '_elite']) || monstros[n] || monstros.esqueleto;
}
function direcao(dx, dy) { return Math.round(((Math.atan2(dx + dy, dx - dy) * 180 / Math.PI + 360) % 360) / 45) % 8; }
function nomeDoChefe(especie) { return especie === 'AbadessaCosturada' ? 'A Abadessa Costurada' : especie === 'CarcereiroDeAlmas' ? 'O Carcereiro de Almas' : especie === 'Lazarus' ? 'Arcebispo Lázarus' : especie === 'Diablo' ? 'Diablo, o Senhor do Terror' : especie.replace(/([a-z])([A-Z])/g, '$1 $2'); }
function escHtml(t) { return String(t).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }
function naTela(x, y) { return W(x + so[0], y + so[1]); }
const rnd = (a, b) => a + Math.random() * (b - a);
// ---- a sala: chão de lajes, duas paredes ao fundo, portas conforme o mapa ----
// tema do andar, como no Diablo 1: muda a cada 4 andares [nome, multiplicador da pedra, fogo, fogo forte, frase]
const TEMAS_ANDAR = [['Catedral', [1, 1, 1], '255,150,60', '255,170,70', 'pedra lavrada e mortos-vivos'], ['Catacumbas', [0.7, 0.8, 1.08], '130,165,255', '170,200,255', 'frio de túmulo: espíritos e aberrações'], ['Cavernas', [1.2, 0.92, 0.6], '255,120,30', '255,160,50', 'terra crua e brasas: território das bestas'], ['Inferno', [1.3, 0.52, 0.48], '255,50,30', '255,90,50', 'pedra em sangue: os demônios mandam aqui']];
const temaDoAndar = (a) => { const z = Math.max(1, a) - 1; return z % 40 >= 36 ? 3 : Math.floor(z / 4) % 4; };
const TEMA = { 2: [70, 42, 42], 3: [70, 60, 44], 4: [48, 56, 70], 5: [46, 62, 54], 7: [66, 44, 48], 8: [76, 50, 66], 9: [64, 58, 46], 10: [70, 38, 40], 11: [50, 48, 50] };
// grade levadiça na porta de saída (paredes do fundo): fechada enquanto houver inimigo na sala, sobe quando ela é limpa
let gradeAberta = 1, gradeSala = -1;
function pintarGrade(ox, oy, dt) {
  if (saidaAtual !== 'o' && saidaAtual !== 'n') return;
  if (gradeSala !== masm.sala + '|' + andar) { gradeSala = masm.sala + '|' + andar; gradeAberta = 0; }
  let fechada = false; for (const s of estado.values()) if (s.inimigo && s.viva) { fechada = true; break; }
  if (!fechada) for (const e of aEspera) if (+e[0] === masm.sala) { fechada = true; break; }
  const antes = gradeAberta; gradeAberta = Math.max(0, Math.min(1, gradeAberta + (fechada ? -3 : 1.6) * dt));
  if (antes < 1 && gradeAberta >= 1 && !silencio) som('porta');
  const p0 = saidaAtual === 'o' ? P(-1, -1) : P(-1, 13), p1 = saidaAtual === 'o' ? P(-1, 13) : P(21, 13);
  const em = (t, h) => [ox + p0[0] + (p1[0] - p0[0]) * t, oy + p0[1] + (p1[1] - p0[1]) * t - h];
  const sobe = gradeAberta * 96;   // quanto a grade já subiu
  ctx.save(); ctx.beginPath(); for (const [t, h] of [[0.42, 0], [0.58, 0], [0.58, 104], [0.5, 124], [0.42, 104]]) { const q = em(t, h); ctx.lineTo(q[0], q[1]); } ctx.closePath(); ctx.clip();
  ctx.strokeStyle = '#6a6258'; ctx.lineWidth = 3; ctx.lineCap = 'butt';
  for (let k = 0; k <= 5; k++) { const t = 0.43 + k * 0.028, a0 = em(t, sobe), a1 = em(t, 130 + sobe); ctx.beginPath(); ctx.moveTo(a0[0], a0[1]); ctx.lineTo(a1[0], a1[1]); ctx.stroke(); const pt = em(t, sobe - 5); ctx.fillStyle = '#8a8070'; ctx.beginPath(); ctx.moveTo(a0[0] - 2.5, a0[1]); ctx.lineTo(a0[0] + 2.5, a0[1]); ctx.lineTo(pt[0], pt[1]); ctx.closePath(); ctx.fill(); }
  ctx.lineWidth = 2.5; for (const h of [30, 62, 94]) { const a0 = em(0.42, h + sobe), a1 = em(0.58, h + sobe); ctx.beginPath(); ctx.moveTo(a0[0], a0[1]); ctx.lineTo(a1[0], a1[1]); ctx.stroke(); }
  ctx.restore();
}
function construirSala(idx = masm.sala, alvo = chao, escala = 1) {
  if (!alvo) return;
  const c = alvo.getContext('2d'), tipo = masm.tipos[idx] || 0, ato = Math.floor((Math.max(1, andar) - 1) / 10);
  let sem = (andar * 131 + idx * 17 + 7) >>> 0; const r01 = () => { sem = (sem + 0x6D2B79F5) >>> 0; let t = sem; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
  // Ato 2 em diante: as criptas alagadas. Pedra esverdeada, limo, fogo-fátuo no lugar das tochas e raízes nas paredes
  const tm = temaDoAndar(andar), alagado = ato >= 1 && !tm, fogo = tm ? TEMAS_ANDAR[tm][2] : alagado ? '90,255,170' : '255,150,60', fogoForte = tm ? TEMAS_ANDAR[tm][3] : alagado ? '120,255,190' : '255,170,70';
  let base = TEMA[tipo] || [58, 53, 50]; if (alagado) base = TEMA[tipo] ? TEMA[tipo].map((v, i) => Math.round(v * [0.7, 0.95, 0.9][i])) : [38, 56, 52];
  if (tm) base = base.map((v, i) => Math.round(v * TEMAS_ANDAR[tm][1][i]));   // tema do andar por cima da cor da sala
  base = base.map((v) => Math.round(v * 1.3));
  c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, alvo.width, alvo.height); c.setTransform(escala, 0, 0, escala, 0, 0);
  const poli = (pts, cor, borda) => { c.beginPath(); pts.forEach(([x, y], i) => (i ? c.lineTo(x, y) : c.moveTo(x, y))); c.closePath(); if (cor) { c.fillStyle = cor; c.fill(); } if (borda) { c.strokeStyle = borda; c.stroke(); } };
  // portas: vizinhos no mapa (pai e filhos). leste = borda da frente à direita; oeste = parede esquerda; norte = parede direita; sul = borda da frente à esquerda
  const eu = mapa[idx], portas = { l: false, o: false, n: false, s: false };
  if (eu) mapa.forEach((m, i) => { if (i === idx) return; const liga = Math.abs(m[0] - eu[0]) + Math.abs(m[1] - eu[1]) === 1; if (!liga) return; const dx = m[0] - eu[0], dy = m[1] - eu[1]; if (dx === 1) portas.l = true; else if (dx === -1) portas.o = true; else if (dy === 1) portas.n = true; else if (dy === -1) portas.s = true; });
  // por qual porta o grupo sai: para a próxima sala se for vizinha; senão volta pelo caminho de onde veio
  let saida = ''; const prox = mapa[idx + 1];
  if (eu) { const cam0 = prox ? caminhoNoMapa(idx, idx + 1) : [], alvo = cam0.length > 1 ? mapa[cam0[1]] : null; if (alvo) { const dx = alvo[0] - eu[0], dy = alvo[1] - eu[1]; saida = dx === 1 ? 'l' : dx === -1 ? 'o' : dy === 1 ? 'n' : dy === -1 ? 's' : ''; } }
  if (idx !== masm.sala) saida = ''; else saidaAtual = saida;   // só a sala em que o grupo está mostra a saída acesa
  const ultima = idx >= masm.tipos.length - 1;
  // espessura da laje nas duas bordas da frente
  const A = P(-1, -1), B = P(21, -1), C = P(21, 13), D = P(-1, 13), esp = 26;
  poli([A, B, [B[0], B[1] + esp], [A[0], A[1] + esp]], '#141012'); poli([B, C, [C[0], C[1] + esp], [B[0], B[1] + esp]], '#0e0b0d');
  // soleiras das portas da frente
  c.lineWidth = 1;
  if (portas.l) poli([P(21, 4.6), P(22.6, 4.6), P(22.6, 7.4), P(21, 7.4)], `rgb(${base.map((v) => v + 16).join(',')})`, '#000');
  if (portas.s) poli([P(8.6, -1), P(8.6, -2.6), P(11.4, -2.6), P(11.4, -1)], `rgb(${base.map((v) => v + 16).join(',')})`, '#000');
  // lajes
  for (let i = -1; i < 21; i += 2) for (let j = -1; j < 13; j += 2) {
    const v = (r01() - 0.5) * 16, luz = 1 - 0.018 * (Math.abs(i - 9) + Math.abs(j - 5)), cor = base.map((b) => Math.max(0, Math.round((b + v) * luz)));
    poli([P(i, j), P(i + 2, j), P(i + 2, j + 2), P(i, j + 2)], `rgb(${cor.join(',')})`, 'rgba(0,0,0,0.55)');
    if (r01() < 0.22) { const [x, y] = P(i + 0.4 + r01() * 1.2, j + 0.4 + r01() * 1.2); c.strokeStyle = 'rgba(0,0,0,0.5)'; c.beginPath(); c.moveTo(x, y); c.lineTo(x + (r01() - 0.5) * 50, y + (r01() - 0.5) * 22); c.lineTo(x + (r01() - 0.5) * 70, y + (r01() - 0.5) * 30); c.stroke(); }
    if (r01() < 0.10) { const [x, y] = P(i + 1, j + 1); c.fillStyle = 'rgba(0,0,0,0.28)'; c.beginPath(); c.ellipse(x, y, 30 + r01() * 30, 12 + r01() * 12, 0, 0, 7); c.fill(); }
  }
  // ---- decoração do chão conforme a sala ----
  if (alagado) for (let q = 0; q < 7; q++) { const [px, py] = P(1 + r01() * 18, 1 + r01() * 10); c.fillStyle = 'rgba(20,60,60,0.45)'; c.beginPath(); c.ellipse(px, py, 40 + r01() * 70, 16 + r01() * 26, 0, 0, 7); c.fill(); c.strokeStyle = 'rgba(120,220,200,0.18)'; c.stroke(); }
  const elipse = (x, y, rx, ry, cor) => { const [px, py] = P(x, y); c.fillStyle = cor; c.beginPath(); c.ellipse(px, py, rx, ry, 0, 0, 7); c.fill(); };
  const anel = (x, y, r, cor, larg) => { const [px, py] = P(x, y); c.strokeStyle = cor; c.lineWidth = larg; c.beginPath(); c.ellipse(px, py, r * K * Math.SQRT2, r * K * Math.SQRT1_2, 0, 0, 7); c.stroke(); c.lineWidth = 1; };
  const tapete = (x0, y0, x1, y1, cor, borda) => { poli([P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1)], cor, null); c.lineWidth = 3; poli([P(x0 + 0.3, y0 + 0.3), P(x1 - 0.3, y0 + 0.3), P(x1 - 0.3, y1 - 0.3), P(x0 + 0.3, y1 - 0.3)], null, borda); c.lineWidth = 1; };
  const entulho = (n) => { for (let q = 0; q < n; q++) { const x = r01() * 20, y = r01() * 12, [px, py] = P(x, y); for (let z = 0; z < 5; z++) { c.fillStyle = r01() < 0.4 ? '#b8ae94' : '#3a3436'; c.beginPath(); c.ellipse(px + (r01() - 0.5) * 34, py + (r01() - 0.5) * 16, 2 + r01() * 5, 1.5 + r01() * 3, r01() * 3, 0, 7); c.fill(); } } };
  // ---- decoração do tema do andar: fica junto às bordas para não cobrir a luta ----
  const naBorda = () => { const q = r01(); return q < 0.3 ? [0.6 + r01() * 18.8, 10.6 + r01() * 1.1] : q < 0.6 ? [0.5 + r01() * 1.2, 0.8 + r01() * 10.4] : q < 0.8 ? [18.4 + r01() * 1.2, 0.8 + r01() * 10.4] : [0.6 + r01() * 18.8, 0.3 + r01() * 1.1]; };
  if (tm === 1) {              // Catacumbas: lápides no chão e ossos amontoados
    for (let q = 0; q < 4; q++) { const [x, y] = naBorda(); poli([P(x - 0.9, y - 0.45), P(x + 0.9, y - 0.45), P(x + 0.9, y + 0.45), P(x - 0.9, y + 0.45)], 'rgba(18,22,34,0.85)', 'rgba(150,170,210,0.35)'); const [px, py] = P(x, y); c.strokeStyle = 'rgba(150,170,210,0.4)'; c.beginPath(); c.moveTo(px - 16, py); c.lineTo(px + 16, py); c.moveTo(px - 5, py - 6); c.lineTo(px - 5, py + 6); c.stroke(); }
    for (let q = 0; q < 6; q++) { const [x, y] = naBorda(), [px, py] = P(x, y); for (let z = 0; z < 7; z++) { c.fillStyle = r01() < 0.7 ? '#b9b3a0' : '#7d7868'; c.beginPath(); c.ellipse(px + (r01() - 0.5) * 30, py + (r01() - 0.5) * 13, 3 + r01() * 6, 1.4 + r01() * 2, r01() * 3, 0, 7); c.fill(); } c.fillStyle = '#cfc9b6'; c.beginPath(); c.arc(px, py - 5, 5, 0, 7); c.fill(); c.fillStyle = '#15121a'; c.fillRect(px - 3, py - 6, 2, 2); c.fillRect(px + 1, py - 6, 2, 2); }
  } else if (tm === 2) {       // Cavernas: estalagmites e veios de brasa
    for (let q = 0; q < 5; q++) { const x = 1 + r01() * 18, y = 1 + r01() * 10, [px, py] = P(x, y); c.strokeStyle = 'rgba(255,120,30,0.5)'; c.lineWidth = 2; c.beginPath(); c.moveTo(px, py); let ax = px, ay = py; for (let z = 0; z < 4; z++) { ax += (r01() - 0.5) * 70; ay += (r01() - 0.5) * 30; c.lineTo(ax, ay); } c.stroke(); c.lineWidth = 1; }
    for (let q = 0; q < 9; q++) { const [x, y] = naBorda(), [px, py] = P(x, y), h = 26 + r01() * 34, w = 9 + r01() * 8; c.fillStyle = 'rgba(0,0,0,0.35)'; c.beginPath(); c.ellipse(px + 6, py + 2, w * 1.3, w * 0.5, 0, 0, 7); c.fill(); poli([[px - w, py], [px, py - h], [px + w, py]], '#4a3a26', '#1a130c'); poli([[px - w * 0.25, py - 2], [px, py - h], [px + w, py]], '#6a5434', null); }
  } else if (tm === 3) {       // Inferno: fendas de lava, poças de sangue e estacas
    for (let q = 0; q < 6; q++) { const x = 1 + r01() * 18, y = 1 + r01() * 10, [px, py] = P(x, y); const pts = [[px, py]]; let ax = px, ay = py; for (let z = 0; z < 5; z++) { ax += (r01() - 0.5) * 90; ay += (r01() - 0.5) * 36; pts.push([ax, ay]); } for (const [lw, cor] of [[7, 'rgba(255,40,10,0.22)'], [3, 'rgba(255,110,30,0.75)'], [1, 'rgba(255,220,120,0.9)']]) { c.strokeStyle = cor; c.lineWidth = lw; c.beginPath(); pts.forEach(([u, v], i) => (i ? c.lineTo(u, v) : c.moveTo(u, v))); c.stroke(); } c.lineWidth = 1; }
    for (let q = 0; q < 4; q++) { const [x, y] = naBorda(); elipse(x, y, 26 + r01() * 22, 10 + r01() * 8, 'rgba(110,8,14,0.6)'); }
    for (let q = 0; q < 5; q++) { const [x, y] = naBorda(), [px, py] = P(x, y), h = 46 + r01() * 22; c.fillStyle = 'rgba(0,0,0,0.35)'; c.beginPath(); c.ellipse(px + 5, py + 1, 10, 4, 0, 0, 7); c.fill(); c.strokeStyle = '#2a1a14'; c.lineWidth = 4; c.beginPath(); c.moveTo(px, py); c.lineTo(px, py - h); c.stroke(); c.lineWidth = 1; c.fillStyle = '#c9c0aa'; c.beginPath(); c.arc(px, py - h + 8, 6, 0, 7); c.fill(); c.fillStyle = '#1a0e0e'; c.fillRect(px - 4, py - h + 6, 2, 3); c.fillRect(px + 1, py - h + 6, 2, 3); }
  }
  if (tipo === 2) {            // covil do chefe: selo de sangue no centro
    anel(10, 6, 4.2, 'rgba(160,20,32,0.55)', 5); anel(10, 6, 3.2, 'rgba(160,20,32,0.35)', 2);
    c.strokeStyle = 'rgba(160,20,32,0.5)'; c.lineWidth = 3; c.beginPath(); for (let q = 0; q < 5; q++) { const a = (q * 144 - 90) * Math.PI / 180, [px, py] = P(10 + Math.cos(a) * 4.2, 6 + Math.sin(a) * 4.2); q ? c.lineTo(px, py) : c.moveTo(px, py); } c.closePath(); c.stroke(); c.lineWidth = 1;
    entulho(8);
  } else if (tipo === 4) { anel(10, 6, 2.6, 'rgba(110,200,255,0.5)', 4); anel(10, 6, 1.9, 'rgba(110,200,255,0.3)', 2); }
  else if (tipo === 3) { tapete(6, 3.5, 14, 8.5, 'rgba(70,24,28,0.75)', 'rgba(217,178,95,0.6)'); for (let q = 0; q < 26; q++) elipse(7 + r01() * 6, 4 + r01() * 4, 3, 1.6, 'rgba(255,216,74,0.8)'); }
  else if (tipo === 9) tapete(5, 3, 15, 9, 'rgba(60,44,70,0.7)', 'rgba(217,178,95,0.5)');
  else if (tipo === 7 || tipo === 10) { for (let q = 0; q < 7; q++) elipse(6 + r01() * 8, 3 + r01() * 6, 26 + r01() * 40, 10 + r01() * 14, 'rgba(110,8,16,0.35)'); }
  else if (tipo === 5) { anel(10, 6, 2.4, 'rgba(120,255,170,0.35)', 3); }
  else if (tipo === 11) { entulho(14); for (let q = 0; q < 3; q++) { const [px, py] = P(1 + r01() * 2, 9 + r01() * 3); c.strokeStyle = 'rgba(220,220,220,0.18)'; for (let z = 0; z < 6; z++) { c.beginPath(); c.moveTo(px, py); c.lineTo(px + Math.cos(z) * 46, py + Math.sin(z) * 24); c.stroke(); } } }
  else { entulho(4); if (r01() < 0.5) tapete(3, 4.5, 17, 7.5, 'rgba(60,20,24,0.5)', 'rgba(120,90,50,0.4)'); if (r01() < 0.6) for (let q = 0; q < 2; q++) { const gx = 4 + r01() * 12, gy = 2 + r01() * 8; poli([P(gx, gy), P(gx + 1.4, gy), P(gx + 1.4, gy + 1.4), P(gx, gy + 1.4)], '#0a0809', '#3a3436'); c.strokeStyle = '#3a3436'; for (let z = 1; z < 5; z++) { const a = P(gx + z * 0.28, gy), b2 = P(gx + z * 0.28, gy + 1.4); c.beginPath(); c.moveTo(a[0], a[1]); c.lineTo(b2[0], b2[1]); c.stroke(); } } }
  // escada por onde o grupo chegou, na entrada da primeira sala: degraus que sobem para fora
  if (idx === 0) {
    for (let q = 0; q < 5; q++) { const v = 52 + q * 14; poli([P(1.6 - q * 0.5, 4.7), P(2.1 - q * 0.5, 4.7), P(2.1 - q * 0.5, 7.3), P(1.6 - q * 0.5, 7.3)], `rgb(${v},${v - 4},${v - 6})`, '#000'); }
    poli([P(-0.4, 4.7), P(2.1, 4.7), P(2.1, 7.3), P(-0.4, 7.3)], null, 'rgba(217,178,95,0.55)');
  }
  // escada para o próximo andar, no canto do fundo da última sala
  if (ultima) {
    for (let q = 0; q < 6; q++) { const v = 60 - q * 9; poli([P(17 + q * 0.45, 9.2), P(17.45 + q * 0.45, 9.2), P(17.45 + q * 0.45, 12), P(17 + q * 0.45, 12)], `rgb(${v},${v - 4},${v - 6})`, '#000'); }
    poli([P(17, 9.2), P(19.7, 9.2), P(19.7, 12), P(17, 12)], null, 'rgba(217,178,95,0.8)');
    const [gx, gy] = P(18.3, 10.6); c.globalCompositeOperation = 'lighter'; const gl = c.createRadialGradient(gx, gy, 0, gx, gy, 120); gl.addColorStop(0, 'rgba(120,255,170,0.22)'); gl.addColorStop(1, 'rgba(120,255,170,0)'); c.fillStyle = gl; c.beginPath(); c.ellipse(gx, gy, 120, 60, 0, 0, 7); c.fill(); c.globalCompositeOperation = 'source-over';
  }
  // brilho na soleira da saída (portas da frente)
  const brilhoSaida = (x, y) => { const [gx, gy] = P(x, y); c.globalCompositeOperation = 'lighter'; const gl = c.createRadialGradient(gx, gy, 0, gx, gy, 150); gl.addColorStop(0, 'rgba(255,216,74,0.30)'); gl.addColorStop(1, 'rgba(255,216,74,0)'); c.fillStyle = gl; c.beginPath(); c.ellipse(gx, gy, 150, 75, 0, 0, 7); c.fill(); c.globalCompositeOperation = 'source-over'; };
  if (saida === 'l') brilhoSaida(21, 6); else if (saida === 's') brilhoSaida(10, -1); else if (saida === 'o') brilhoSaida(-0.6, 6); else if (saida === 'n') brilhoSaida(10, 12.6);
  // paredes do fundo (esquerda: x = -1; direita: y = 13)
  const parede = (p0, p1, escura, porta) => {
    const g = c.createLinearGradient(0, Math.min(p0[1], p1[1]) - PAREDE, 0, Math.max(p0[1], p1[1]));
    const tom = base.map((b) => Math.round(b * (escura ? 0.62 : 0.8)));
    g.addColorStop(0, `rgb(${tom.map((v) => Math.round(v * 0.45)).join(',')})`); g.addColorStop(1, `rgb(${tom.join(',')})`);
    poli([p0, p1, [p1[0], p1[1] - PAREDE], [p0[0], p0[1] - PAREDE]], g, '#000');
    c.save(); c.beginPath(); c.moveTo(p0[0], p0[1]); c.lineTo(p1[0], p1[1]); c.lineTo(p1[0], p1[1] - PAREDE); c.lineTo(p0[0], p0[1] - PAREDE); c.closePath(); c.clip();
    c.strokeStyle = 'rgba(0,0,0,0.45)'; c.lineWidth = 1;
    for (let h = 22; h < PAREDE; h += 22) { c.beginPath(); c.moveTo(p0[0], p0[1] - h); c.lineTo(p1[0], p1[1] - h); c.stroke(); }
    const n = 22; for (let q = 0; q <= n; q++) for (let h = 0; h < PAREDE; h += 22) { const t = (q + ((h / 22) % 2) * 0.5) / n; if (t > 1) continue; const x = p0[0] + (p1[0] - p0[0]) * t, y = p0[1] + (p1[1] - p0[1]) * t; c.beginPath(); c.moveTo(x, y - h); c.lineTo(x, y - h - 22); c.stroke(); }
    const em = (t, h) => [p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - h];
    if (alagado) {   // raízes e limo escorrendo pela parede
      for (let q = 0; q < 9; q++) { const t = r01(), [x, y] = em(t, PAREDE); c.strokeStyle = `rgba(${20 + r01() * 20 | 0},${50 + r01() * 30 | 0},${30 + r01() * 20 | 0},0.8)`; c.lineWidth = 2 + r01() * 4; c.beginPath(); c.moveTo(x, y); let yy = y, xx = x; const alt = 40 + r01() * 100; while (yy < y + alt) { xx += (r01() - 0.5) * 16; yy += 12; c.lineTo(xx, yy); } c.stroke(); }
      c.lineWidth = 1;
    }
    // pilares de pedra e estandartes
    for (const t of [0.04, 0.34, 0.66, 0.96]) { const [x, y] = em(t, 0); c.fillStyle = escura ? '#1c1618' : '#2a2224'; c.fillRect(x - 11, y - PAREDE, 22, PAREDE + 6); c.fillStyle = 'rgba(255,255,255,0.05)'; c.fillRect(x - 11, y - PAREDE, 5, PAREDE + 6); c.fillStyle = '#120e10'; c.fillRect(x - 14, y - 8, 28, 14); }
    if (!porta || true) for (const t of porta ? [0.19, 0.81] : [0.19, 0.5, 0.81]) { const [x, y] = em(t, 122), corB = tipo === 2 ? '#5a0c14' : tipo === 4 ? '#1c3a5a' : tipo === 8 ? '#5a1c44' : '#3a1418'; c.fillStyle = corB; c.beginPath(); c.moveTo(x - 13, y); c.lineTo(x + 13, y); c.lineTo(x + 13, y + 52); c.lineTo(x, y + 64); c.lineTo(x - 13, y + 52); c.closePath(); c.fill(); c.strokeStyle = 'rgba(217,178,95,0.5)'; c.stroke(); }
    if (porta) {   // arco escuro com moldura
      const a0 = em(0.42, 0), a1 = em(0.58, 0), a2 = em(0.58, 104), a3 = em(0.5, 124), a4 = em(0.42, 104);
      poli([a0, a1, a2, a3, a4], '#020102', '#8a744a'); c.lineWidth = 3; c.strokeStyle = porta === 2 ? '#ffd84a' : '#5c4c30'; c.stroke(); c.lineWidth = 1;
      for (const [t0, t1] of [[0.42, 0.452], [0.58, 0.548]]) {
        poli([em(t0, 0), em(t1, 0), em(t1, 94), em(t0, 102)], '#3a2416', '#140b07');
        c.strokeStyle = '#6a6a70'; c.lineWidth = 2;
        for (const hh of [24, 70]) { const [x0, y0] = em(t0, hh), [x1, y1] = em(t1, hh); c.beginPath(); c.moveTo(x0, y0); c.lineTo(x1, y1); c.stroke(); }
        c.lineWidth = 1;
      }
      if (porta === 2) { c.globalCompositeOperation = 'lighter'; const [gx, gy] = em(0.5, 50), gl = c.createRadialGradient(gx, gy, 0, gx, gy, 110); gl.addColorStop(0, 'rgba(255,216,74,0.35)'); gl.addColorStop(1, 'rgba(255,216,74,0)'); c.fillStyle = gl; c.beginPath(); c.arc(gx, gy, 110, 0, 7); c.fill(); c.globalCompositeOperation = 'source-over'; }
    }
    for (const t of porta ? [0.3, 0.7] : [0.25, 0.5, 0.75]) {   // tochas
      const [x, y] = em(t, 86); c.fillStyle = '#2a2018'; c.fillRect(x - 2, y, 4, 18);
      c.globalCompositeOperation = 'lighter'; const gl = c.createRadialGradient(x, y - 4, 0, x, y - 4, 90); gl.addColorStop(0, `rgba(${fogoForte},0.55)`); gl.addColorStop(0.25, `rgba(${fogo},0.18)`); gl.addColorStop(1, `rgba(${fogo},0)`); c.fillStyle = gl; c.beginPath(); c.arc(x, y - 4, 90, 0, 7); c.fill(); c.globalCompositeOperation = 'source-over';
    }
    c.restore();
  };
  parede(A, D, false, portas.o ? (saida === 'o' ? 2 : 1) : 0); parede(D, C, true, portas.n ? (saida === 'n' ? 2 : 1) : 0);
  // luz das tochas no chão e escuridão nas bordas
  c.globalCompositeOperation = 'lighter';
  for (const [x, y] of [P(-0.5, 3), P(-0.5, 9), P(5, 12.5), P(15, 12.5)]) { const gl = c.createRadialGradient(x, y, 0, x, y, 200); gl.addColorStop(0, `rgba(${fogo},0.16)`); gl.addColorStop(1, `rgba(${fogo},0)`); c.fillStyle = gl; c.beginPath(); c.ellipse(x, y, 200, 110, 0, 0, 7); c.fill(); }
  c.globalCompositeOperation = 'source-over';
}
function gauss() { return (Math.random() + Math.random() + Math.random() - 1.5) * 1.2; }

// marcas no chão conforme a morte: o jeito de matar fica escrito na arena
function marcaDeCondicao(x, y, cond, grande) {
  const [px, py] = P(x, y), r = grande ? 46 : 28;
  if (cond === 'Queimado') {
    const g = sctx.createRadialGradient(px, py, 2, px, py, r); g.addColorStop(0, 'rgba(8,6,5,0.85)'); g.addColorStop(0.6, 'rgba(20,12,8,0.5)'); g.addColorStop(1, 'rgba(20,12,8,0)');
    sctx.save(); sctx.translate(px, py); sctx.scale(1, 0.5); sctx.translate(-px, -py); sctx.fillStyle = g; sctx.beginPath(); sctx.arc(px, py, r, 0, 7); sctx.fill(); sctx.restore();
    for (let i = 0; i < 16; i++) particulas.push([x + rnd(-0.4, 0.4), y + rnd(-0.3, 0.3), 0.2, rnd(-0.3, 0.3), rnd(-0.2, 0.2), rnd(3, 6.5), '#ff8a2a', rnd(0.6, 1.3)]);
  } else if (cond === 'Congelado') {
    for (let i = 0; i < 9; i++) { const ox = gauss() * r * 0.5, oy = gauss() * r * 0.22; sctx.fillStyle = `rgba(170,215,255,${rnd(0.12, 0.3)})`; sctx.beginPath(); sctx.ellipse(px + ox, py + oy, rnd(5, 14), rnd(2, 6), rnd(0, 3), 0, 7); sctx.fill(); }
    for (let i = 0; i < 10; i++) particulas.push([x, y, 0.7, rnd(-2.5, 2.5), rnd(-1.5, 1.5), rnd(1, 4), '#cfe8ff', rnd(0.4, 0.8)]);
  } else if (cond === 'Putrefato') {
    mancha(x, y, grande ? 30 : 20, 9, [52, 84, 20]);
    for (let i = 0; i < 6; i++) particulas.push([x + rnd(-0.3, 0.3), y, 0.2, rnd(-0.2, 0.2), 0, rnd(2, 3.5), '#7ab030', rnd(0.8, 1.4)]);
  } else if (cond === 'Eletrizado') {
    for (let i = 0; i < 5; i++) raios.push([x, y, x + rnd(-1.2, 1.2), y + rnd(-0.8, 0.8), 0.18, 0, 1]);
    sctx.fillStyle = 'rgba(30,30,50,0.45)'; sctx.beginPath(); sctx.ellipse(px, py, r * 0.6, r * 0.25, 0, 0, 7); sctx.fill();
  } else if (cond === 'Oco') {
    for (let i = 0; i < 14; i++) particulas.push([x + rnd(-0.3, 0.3), y + rnd(-0.2, 0.2), 0.5, rnd(-0.4, 0.4), rnd(-0.3, 0.3), rnd(2.5, 5), '#9a6aff', rnd(0.7, 1.3)]);
  }
}
function pilhaDeOssos(x, y) {
  const [px, py] = P(x, y);
  for (let i = 0; i < 6; i++) {
    const a = rnd(0, 3.14), l = rnd(5, 11), ox = rnd(-12, 12), oy = rnd(-5, 5);
    sctx.strokeStyle = `rgba(${190 + rnd(-20, 20) | 0},${175 + rnd(-20, 20) | 0},140,0.75)`; sctx.lineWidth = 2.2; sctx.lineCap = 'round';
    sctx.beginPath(); sctx.moveTo(px + ox - Math.cos(a) * l, py + oy - Math.sin(a) * l * 0.5); sctx.lineTo(px + ox + Math.cos(a) * l, py + oy + Math.sin(a) * l * 0.5); sctx.stroke();
  }
}
// poeira e brasas no ar, e névoa rasteira: a masmorra respira
const poeira = Array.from({ length: 46 }, () => ({ x: Math.random(), y: Math.random(), z: Math.random(), v: 0.4 + Math.random(), brasa: Math.random() < 0.3 }));
function ambiente(dt, larg, alt) {
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  // tom do andar: quanto mais fundo, mais doentio; andar de chefe pulsa em vermelho
  const noAto = ((andar - 1) % 10) + 1, ato = Math.floor((andar - 1) / 10);
  let tom = null;
  if (noAto === 10) tom = `rgba(140,10,20,${0.10 + 0.05 * Math.sin(tempoSim * 3)})`;
  else if (ato >= 1) tom = `rgba(20,90,80,${0.05 + 0.012 * noAto})`;
  else if (noAto > 5) tom = `rgba(120,30,10,${0.02 * (noAto - 5)})`;
  if (masm.secreto) tom = `rgba(255,120,190,${0.10 + 0.04 * Math.sin(tempoSim * 4)})`;
  else if (masm.amb === 1) tom = `rgba(255,90,10,${0.13 + 0.05 * Math.sin(tempoSim * 2)})`;
  else if (masm.amb === 2) tom = `rgba(70,140,30,${0.16 + 0.04 * Math.sin(tempoSim * 1.3)})`;
  else if (masm.amb === 3) tom = 'rgba(120,180,255,0.14)';
  else if (masm.amb === 4) tom = 'rgba(90,80,70,0.14)';
  if (tom) { ctx.fillStyle = tom; ctx.fillRect(0, 0, larg, alt); }
  if (transicao > 0) { ctx.fillStyle = `rgba(0,0,0,${transicao * transicao})`; ctx.fillRect(0, 0, larg, alt); }
  if (descida) { const fd = Math.max(0, Math.min(1, ((tempoSim - descida.t0) / descida.T - 0.6) / 0.4)); if (fd > 0) { ctx.fillStyle = `rgba(0,0,0,${fd * fd})`; ctx.fillRect(0, 0, larg, alt); } }
  { const avE = $('aviso'); if (avE.classList.contains('alto') !== !!tituloAndar) avE.classList.toggle('alto', !!tituloAndar); }
  if (tituloAndar) {
    const ft = (performance.now() - tituloAndar.t) / 2600;
    const bpT = $('b-porta'), eT = $('encruzilhada'), bT = $('bencaos');
    if (ft >= 1 || (bpT && !bpT.hidden) || (eT && !eT.hidden && !eT.classList.contains('min')) || (bT && !bT.hidden)) tituloAndar = null;
    else {
      const af = ft < 0.15 ? ft / 0.15 : ft > 0.7 ? (1 - ft) / 0.3 : 1, tam = Math.max(22, Math.min(46, larg / 14));
      ctx.save(); ctx.globalAlpha = af; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.font = `${tam}px Georgia, serif`; ctx.fillStyle = '#000'; ctx.fillText(tituloAndar.a, larg / 2 + 2, alt * 0.5 + 2); ctx.fillStyle = '#d9b25f'; ctx.fillText(tituloAndar.a, larg / 2, alt * 0.5);
      ctx.font = `${Math.round(tam * 0.48)}px Georgia, serif`; ctx.fillStyle = '#000'; ctx.fillText(tituloAndar.b, larg / 2 + 1, alt * 0.5 + tam * 0.95 + 1); ctx.fillStyle = '#cfc4b4'; ctx.fillText(tituloAndar.b, larg / 2, alt * 0.5 + tam * 0.95);
      ctx.restore();
    }
  }
  ctx.globalCompositeOperation = 'lighter';
  for (const p of poeira) {
    p.y -= dt * 0.02 * p.v * (p.brasa ? 2.2 : 1); p.x += dt * 0.008 * Math.sin(tempoSim * 0.7 + p.z * 9);
    if (p.y < -0.02) { p.y = 1.02; p.x = Math.random(); }
    const a = (p.brasa ? 0.55 : 0.16) * (0.6 + 0.4 * Math.sin(tempoSim * 2 + p.z * 20)), r = (p.brasa ? 1.4 : 2.2) * (larg / 900 + 0.6);
    ctx.fillStyle = p.brasa ? (ato >= 1 ? `rgba(120,255,190,${a})` : `rgba(255,140,50,${a})`) : `rgba(200,190,170,${a})`; ctx.beginPath(); ctx.arc(p.x * larg, p.y * alt, r, 0, 7); ctx.fill();
  }
  ctx.globalCompositeOperation = 'source-over';
  for (let i = 0; i < 3; i++) {
    const fx = ((tempoSim * (0.012 + i * 0.006) + i * 0.37) % 1.4 - 0.2) * larg, fy = alt * (0.72 + i * 0.09);
    const g = ctx.createRadialGradient(fx, fy, 0, fx, fy, larg * 0.45); g.addColorStop(0, 'rgba(150,160,150,0.07)'); g.addColorStop(1, 'rgba(150,160,150,0)');
    ctx.save(); ctx.translate(fx, fy); ctx.scale(1, 0.22); ctx.translate(-fx, -fy); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(fx, fy, larg * 0.45, 0, 7); ctx.fill(); ctx.restore();
  }
}
function mancha(x, y, raio, n, cor = [70, 4, 8]) {
  const [px, py] = P(x, y);
  for (let i = 0; i < n; i++) {
    const ox = gauss() * raio * 0.6, oy = gauss() * raio * 0.3, r = rnd(raio * 0.15, raio * 0.6), k = rnd(0.6, 1.1);
    sctx.fillStyle = `rgba(${cor[0] * k | 0},${cor[1] * k | 0},${cor[2] * k | 0},${rnd(0.6, 0.9)})`;
    sctx.beginPath(); sctx.ellipse(px + ox, py + oy, r, r * 0.5, 0, 0, 7); sctx.fill();
  }
}

// ---- estado do desenho ----
const estado = new Map();      // id -> entidade viva
const mortos = new Map();      // id -> entidade caída (cadáver à vista)
const cadaverDe = new Map();   // id do cadáver -> id da entidade
let particulas = [], raios = [], numeros = [], orbes = [], projeteis = [], pocas = [];
let tempoSim = 0, andar = 0, mana = 0, necro = null, tremor = 0, saque = [0, 0, 0], poderes = [0, 0, 0, 0], oferta = null, ativos = [], pactos = [], ofertaMostrada = '', marcado = -1, almasVoando = [], ritoEmCurso = 0, clarao = 0, deFoice = false, dicaGolpe = false, sopro = 0, dicaSopro = false, itens = [[0, 0], [0, 0]], autosAnterior = '', pressagio = 0;
const PRESSAGIOS = [null, ['Horda', 'mais inimigos, mais Ossos'], ['Ossário', 'muito mais Ossos'], ['Almas inquietas', 'chance de alma dobrada'], ['Sede de sangue', 'inimigos batem mais forte, mais Ossos'], ['Campeão errante', 'um elite a mais'], ['Calmaria', 'menos inimigos']];
const COR_MORTE = { Queimado: '20,14,12', Congelado: '120,170,210', Putrefato: '60,90,25', Eletrizado: '40,40,60', Oco: '50,30,80' };
const tinta = document.createElement('canvas'), tctx = tinta.getContext('2d');

function limparArena(tudo) {
  mortos.clear(); cadaverDe.clear(); particulas = [];
  if (tudo) { sctx.clearRect(0, 0, sangue.width, sangue.height); return; }
  // andar novo na mesma expedição: o chão guarda metade do que aconteceu
  sctx.save(); sctx.globalCompositeOperation = 'destination-out'; sctx.fillStyle = 'rgba(0,0,0,0.5)'; sctx.fillRect(0, 0, sangue.width, sangue.height); sctx.restore();
}

// ---- som: tudo sintetizado no navegador (sem arquivos); começa no primeiro toque do jogador ----
let actx = null, somLigado = true, ultimoSom = {}; try { somLigado = localStorage.getItem('necromante-idle-save-v1-som') !== '0'; } catch (e) { /* padrão: ligado */ }
function som(tipo) {
  if (!actx || !somLigado || silencio || actx.state !== 'running') return;
  const agora = actx.currentTime, min = { golpe: 0.05, morte: 0.07, osso: 0.1 }[tipo] || 0.12;
  if (agora - (ultimoSom[tipo] || 0) < min) return; ultimoSom[tipo] = agora;
  const tom = (f0, f1, dur, vol, forma = 'sine', atraso = 0) => { const o = actx.createOscillator(), g = actx.createGain(), t0 = agora + atraso; o.type = forma; o.frequency.setValueAtTime(f0, t0); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t0 + dur); g.gain.setValueAtTime(vol, t0); g.gain.exponentialRampToValueAtTime(0.0008, t0 + dur); o.connect(g).connect(actx.destination); o.start(t0); o.stop(t0 + dur + 0.02); };
  const chiado = (dur, vol, freq, atraso = 0) => { const n = Math.floor(actx.sampleRate * dur), buf = actx.createBuffer(1, n, actx.sampleRate), d = buf.getChannelData(0); for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / n); const src = actx.createBufferSource(), f = actx.createBiquadFilter(), g = actx.createGain(); src.buffer = buf; f.type = 'lowpass'; f.frequency.value = freq; g.gain.value = vol; src.connect(f).connect(g).connect(actx.destination); src.start(agora + atraso); };
  switch (tipo) {
    case 'golpe': chiado(0.06, 0.10, 1400 + Math.random() * 800); break;
    case 'critico': chiado(0.09, 0.2, 2600); tom(220, 110, 0.12, 0.08, 'square'); break;
    case 'morte': chiado(0.16, 0.16, 520); tom(130, 50, 0.2, 0.10, 'sawtooth'); break;
    case 'explosao': chiado(0.4, 0.3, 300); tom(90, 30, 0.4, 0.2); break;
    case 'alma': tom(520, 1040, 0.25, 0.07); tom(780, 1560, 0.3, 0.04, 'sine', 0.05); break;
    case 'bau': [523, 659, 784, 1047].forEach((f, i) => tom(f, f, 0.18, 0.07, 'triangle', i * 0.07)); break;
    case 'bencao': [392, 494, 587].forEach((f, i) => tom(f, f * 1.01, 0.5, 0.05, 'sine', i * 0.1)); break;
    case 'porta': chiado(0.3, 0.08, 220); tom(70, 55, 0.35, 0.10); break;
    case 'chefe': tom(55, 41, 1.2, 0.22, 'sawtooth'); tom(82, 62, 1.2, 0.12, 'sawtooth', 0.05); chiado(0.8, 0.12, 180); break;
    case 'perigo': tom(880, 660, 0.12, 0.05, 'square'); break;
    case 'secreto': [262, 330, 392, 523, 659].forEach((f, i) => tom(f, f, 0.3, 0.07, 'triangle', i * 0.09)); break;
    case 'fim': tom(196, 98, 0.9, 0.14, 'triangle'); tom(147, 73, 1.1, 0.10, 'triangle', 0.15); break;
    case 'feitico': tom(300, 900, 0.18, 0.06, 'sawtooth'); chiado(0.15, 0.06, 3000); break;
    case 'toque': tom(660, 880, 0.08, 0.05, 'triangle'); break;
  }
}
function ligarSom() { if (actx) { if (actx.state === 'suspended') actx.resume(); return; } try { actx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { actx = null; } }
document.addEventListener('pointerdown', ligarSom, { passive: true });
function aviso(texto) {
  if (silencio) return;
  const a = $('aviso'); a.textContent = texto; a.style.opacity = 1; aviso.ate = performance.now() + 2300;
  clearTimeout(aviso.t); aviso.t = setTimeout(() => { a.style.opacity = 0; }, 2200);
}

function ler(texto) {
  chaveCofre = [-1, false]; patrulha = null;
  const vivos = new Set(); orbes = []; projeteis = []; pocas = []; oferta = null; ativos = []; bencaos = null; aEspera = []; quebraveis = [];
  for (const ln of texto.split('\n')) {
    if (!ln) continue;
    const p = ln.split(' ');
    switch (p[0]) {
      case 'T': {
        const a = +p[1];
        if (a !== andar) { if (andar) limparArena(); andar = a; if (a === 1 || temaDoAndar(a) !== temaDoAndar(a - 1)) { const t = TEMAS_ANDAR[temaDoAndar(a)]; aviso(t[0] + ' · ' + t[4]); anotar(`Você entra em <b>${t[0]}</b>: ${t[4]}.`); } }
        mana = +p[2] / Math.max(1, +p[3]); manaAbs = [+p[2] || 0, +p[3] || 0]; saque = [+p[4] || 0, +p[5] || 0, +p[6] || 0];
        poderes = [+p[7] || 0, +p[8] || 0, +p[9] || 0, +p[10] || 0]; marcado = p[11] === undefined ? -1 : +p[11]; deFoice = p[12] === '1'; sopro = +p[13] || 0; itens = [[+p[14] || 0, +p[15] || 0], [+p[16] || 0, +p[17] || 0]];
        { const pr = +p[18] || 0; if (pr !== pressagio) { pressagio = pr; if (pr && !silencio) aviso('Presságio: ' + PRESSAGIOS[pr][0] + ' · ' + PRESSAGIOS[pr][1]); } }
        break;
      }
      case 'E': {
        const id = +p[1]; vivos.add(id);
        let s = estado.get(id);
        const x = +p[4], y = +p[5];
        if (!s) { s = { id, rx: x, ry: y, dir: 0, anim: null, t: tempoSim }; estado.set(id, s); }
        if (p[8] !== s.anim) { s.anim = p[8]; s.t = tempoSim; }
        const dx = +p[6], dy = +p[7];
        if (Math.abs(dx) + Math.abs(dy) > 1e-6) s.dir = direcao(dx, dy);
        s.especie = p[2]; s.inimigo = p[3] === '1'; s.x = x; s.y = y; s.viva = p[9] === '1'; s.vida = +p[10];
        s.elite = p[11] === '1'; s.chefe = p[12] === '1'; s.necro = p[13] === '1';
        s.golpe = +p[14] || 0; s.qual = p[15] === undefined ? -1 : +p[15]; s.escudo = +p[16] || 0; s.campeao = p[17] === '1';
        if (s.golpe > 0 && !dicaGolpe && !silencio) { dicaGolpe = true; aviso('Toque no inimigo que brilha para interromper o golpe'); }
        if (s.necro) necro = s;
        break;
      }
      case 'O': orbes.push([+p[2], +p[3], +p[1]]); if (!dica && !silencio) { dica = true; aviso('Toque nos orbes verdes para ceifar na hora'); } break;
      case 'P': projeteis.push([+p[2], +p[3], +p[4], +p[5], p[6] === '1']); break;
      case 'S': pocas.push([+p[2], +p[3], +p[4]]); break;
      case 'K': oferta = [+p[1], +p[2], +p[3], +p[4]]; break;
      case 'A': ativos = p.slice(1).map(Number); break;
      case 'M': lerMasmorra(p); break;
      case 'G': lerMapa(p); break;
      case 'R': patrulha = { sala: +p[1], especies: p.slice(2) }; break;
      case 'C': chaveCofre = [+p[1], p[2] === '1']; break;
      case 'Q': quebraveis.push([+p[1], +p[2], +p[3], +p[4], p[5] === '1']); break;
      case 'U': nomesUnicos.set(+p[1], p.slice(2).join(' ')); break;
      case 'I': aEspera.push([+p[1], p[2], +p[3], +p[4], p[5] === '1', p[6] === '1', p[7] === '1']); break;
      case 'B': bencaos = [+p[1], +p[2], +p[3], +p[4]]; break;
      case 'V': evento(p); break;
      case 'F': fimDeExpedicao(p); break;
    }
  }
  for (const id of estado.keys()) if (!vivos.has(id)) estado.delete(id);
}

// ---- estatísticas de combate: tudo sai dos eventos da simulação ----
const NOME_ESPECIE = { Necromante: 'Necromante', Esqueleto: 'Esqueleto', ArqueiroOssudo: 'Arqueiro Ossudo', Zumbi: 'Zumbi', CaoDeCripta: 'Cão de Cripta', Diabrete: 'Diabrete', AcolitoCaido: 'Acólito Caído',
  AbadessaCosturada: 'Abadessa Costurada', Carnical: 'Carniçal', DiaboDeGuerra: 'Diabo de Guerra', Aparicao: 'Aparição', ColossoDeOssos: 'Colosso de Ossos', ResProfana: 'Rês Profana', CarcereiroDeAlmas: 'Carcereiro de Almas', Sucubo: 'Súcubo', CavaleiroNegro: 'Cavaleiro Negro', Magistrado: 'Magistrado', Lazarus: 'Arcebispo Lázarus', Diablo: 'Diablo' };
const novaEstat = () => ({ aliados: new Map(), inimigos: new Map(), inicio: tempoSim, fim: 0, dano: 0, sofrido: 0, cura: 0, abates: 0, maiorGolpe: 0, criticos: 0, golpes: 0 });
let estat = null, estatAnterior = null;
function linhaAliado(s) {
  let l = estat.aliados.get(s.id);
  if (!l) { l = { nome: NOME_ESPECIE[s.especie] || s.especie, q: s.necro ? -1 : s.qual, necro: s.necro, dano: 0, sofrido: 0, cura: 0, curado: 0, abates: 0, maior: 0, morreu: 0 }; estat.aliados.set(s.id, l); }
  return l;
}
function linhaInimigo(especie) {
  let l = estat.inimigos.get(especie);
  if (!l) { l = { nome: NOME_ESPECIE[especie] || especie, dano: 0, sofrido: 0, mortos: 0 }; estat.inimigos.set(especie, l); }
  return l;
}
function registrar(v) {
  if (!estat) estat = novaEstat();
  const tipo = v[1], origem = estado.get(+v[2]), alvo = estado.get(+v[3]), valor = +v[4] || 0;
  if (tipo === 'Dano' && valor > 0) {
    if (origem && !origem.inimigo) { const l = linhaAliado(origem); l.dano += valor; if (valor > l.maior) l.maior = valor; estat.dano += valor; estat.golpes++; if (v[6] === '1') estat.criticos++; if (valor > estat.maiorGolpe) estat.maiorGolpe = valor; }
    if (alvo && !alvo.inimigo) { linhaAliado(alvo).sofrido += valor; estat.sofrido += valor; if (origem && origem.inimigo) linhaInimigo(origem.especie).dano += valor; }
    if (alvo && alvo.inimigo) linhaInimigo(alvo.especie).sofrido += valor;
  } else if (tipo === 'Cura' && valor > 0) {
    if (origem && !origem.inimigo) linhaAliado(origem).cura += valor;
    if (alvo && !alvo.inimigo) { linhaAliado(alvo).curado += valor; estat.cura += valor; }
  } else if (tipo === 'Morte') {
    if (alvo && alvo.inimigo) { linhaInimigo(alvo.especie).mortos++; estat.abates++; if (origem && !origem.inimigo) linhaAliado(origem).abates++; }
    else if (alvo) linhaAliado(alvo).morreu++;
  }
}
function estatHtml(e, titulo) {
  if (!e || !e.aliados.size) return `<h3>${titulo}</h3><p class="nota">Ainda sem dados.</p>`;
  const dur = Math.max(1, (e.fim || tempoSim) - e.inicio), al = [...e.aliados.values()].sort((a, b) => b.dano - a.dano), maxD = Math.max(1, ...al.map((l) => l.dano)), maxS = Math.max(1, ...al.map((l) => l.sofrido));
  let h = `<h3>${titulo}</h3><div class="est"><span>Dano total <b style="color:#fff;font-weight:normal">${fmt(Math.round(e.dano))}</b> · ${fmt(Math.round(e.dano / dur))} por segundo</span><span>${Math.floor(dur / 60)}m${String(Math.floor(dur % 60)).padStart(2, '0')}s</span>` +
    `<small>Dano tankado ${fmt(Math.round(e.sofrido))} · cura ${fmt(Math.round(e.cura))} · ${e.abates} abates · maior golpe ${fmt(Math.round(e.maiorGolpe))} · ${e.golpes ? Math.round(100 * e.criticos / e.golpes) : 0}% críticos</small></div>`;
  h += '<p class="nota">Por aliado: a barra vermelha é o dano causado; a azul, o dano tankado.</p>';
  al.forEach((l) => {
    h += `<div class="est"><span class="${l.necro ? '' : 'q' + Math.max(0, l.q)}" ${l.necro ? 'style="color:var(--alma)"' : ''}>${esc(l.nome)}${l.morreu ? ` <span class="nota">· caiu ${l.morreu}×</span>` : ''}</span><span>${fmt(Math.round(l.dano))} <span class="nota">(${Math.round(100 * l.dano / Math.max(1, e.dano))}%)</span></span>` +
      `<div class="b"><i style="width:${100 * l.dano / maxD}%;background:#c8323c"></i></div><div class="b"><i style="width:${100 * l.sofrido / maxS}%;background:#4a8fd0"></i></div>` +
      `<small>${fmt(Math.round(l.dano / dur))}/s · tankou ${fmt(Math.round(l.sofrido))} · ${l.abates} abates · maior golpe ${fmt(Math.round(l.maior))}${l.cura ? ' · curou ' + fmt(Math.round(l.cura)) : ''}${l.curado ? ' · recebeu ' + fmt(Math.round(l.curado)) + ' de cura' : ''}</small></div>`;
  });
  const ini = [...e.inimigos.values()].sort((a, b) => b.dano - a.dano);
  if (ini.length) {
    h += '<p class="nota">Por tipo de inimigo: quanto dano fizeram no seu exército.</p>';
    const mx = Math.max(1, ...ini.map((l) => l.dano));
    ini.forEach((l) => { h += `<div class="est"><span>${esc(l.nome)}</span><span>${fmt(Math.round(l.dano))}</span><div class="b"><i style="width:${100 * l.dano / mx}%;background:#d9b25f"></i></div><small>${l.mortos} mortos · sofreram ${fmt(Math.round(l.sofrido))} de dano</small></div>`; });
  }
  return h;
}
function evento(v) {
  if (!silencio) registrar(v);
  if (silencio && v[1] !== 'CadaverCriado' && v[1] !== 'CadaverConsumido' && v[1] !== 'CadaverRemovido' && v[1] !== 'Morte') return;
  const tipo = v[1], x = +v[10], y = +v[11], dx = +v[12], dy = +v[13];
  if (tipo === 'SalaIniciada' || tipo === 'SalaResolvida' || tipo === 'AmbienteDefinido' || tipo === 'PerigoAnunciado') { eventoDeSala(tipo, v, x, y); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '16') { const g = estado.get(+v[3]); if (g) { g.flash = 0.18; numeros.push({ x: g.x, y: g.y, txt: 'Aparou!', cor: '#cfe6ff', t: 1, amigo: true }); for (let i = 0; i < 6; i++) particulas.push([g.x, g.y, 0.9, rnd(-2, 2), rnd(-1.5, 1.5), rnd(1, 3), '#e8f2ff', 0.35]); } som('golpe'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '17') { const g = estado.get(+v[3]); if (g) { g.sede = tempoSim + (+v[4] || 4); numeros.push({ x: g.x, y: g.y, txt: 'Sede!', cor: '#ff8a6a', t: 1, amigo: true }); } return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '25') {   // porta emperrada: botão por alguns segundos para o jogador abrir na hora
    aviso('Porta emperrada · o grupo está arrombando'); som('golpe');
    portaPendente = { t: tempoSim, dur: +v[4] || 3 }; if (viag && Math.abs(tempoSim - viag.t0) < 0.02) viag.dur = portaPendente.dur;
    let bp = $('b-porta'); if (!bp) { bp = document.createElement('button'); bp.id = 'b-porta'; bp.textContent = '🚪 Abrir a porta já'; bp.style.cssText = 'position:absolute;right:8px;bottom:128px;z-index:6;padding:10px 16px;font-size:15px;border:1px solid var(--ouro);background:#2a1418;color:#ffe9b0;border-radius:8px;box-shadow:0 0 18px #000'; bp.addEventListener('click', () => { if (J && J.AbrirPorta()) aviso('Porta aberta'); bp.hidden = true; }); tela.parentElement.appendChild(bp); }
    bp.hidden = false; clearTimeout(bp.t); bp.t = setTimeout(() => { bp.hidden = true; }, Math.max(600, (+v[4] || 3) * 1000 / Math.max(1, velocidade || 1)));
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '26') { const bp = $('b-porta'); if (bp) bp.hidden = true; abrirPortaNaViagem(); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '27') { const n = Math.round(+v[4] || 0); aviso(`Emboscada no corredor! ${n} inimigos em cima do grupo`); anotar(`<b style="color:#ff8a6a">Emboscada no corredor:</b> ${n} inimigos caíram sobre o grupo antes de ele formar.`); som('chefe'); tremor = Math.max(tremor, 0.35); return; }
  if (tipo === 'EfeitoAplicado' && (v[14] === '30' || v[14] === '31' || v[14] === '32')) {   // comportamento de espécie: avisa só na primeira vez de cada um por descida
    const k = v[14]; vistosDeEspecie = vistosDeEspecie || {};
    const g = estado.get(+v[3]), qx = g ? g.x : +v[10], qy = g ? g.y : +v[11];
    numeros.push({ x: qx, y: qy, txt: k === '30' ? 'Fogem!' : k === '32' ? 'Devora o cadáver' : '+' + Math.round(+v[4] || 0), cor: k === '30' ? '#ffd27a' : '#c8ff9a', t: 1, amigo: false });
    if (!vistosDeEspecie[k] && k !== '31') { vistosDeEspecie[k] = 1; aviso(k === '30' ? 'Diabretes fogem quando um deles morre · voltam logo' : 'Carniçais feridos devoram cadáveres para se curar'); }
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '47') { aviso('Os Salões dos Cegos · eles só aparecem quando já estão em cima de você'); anotar('Você entrou nos <b>Salões dos Cegos</b>: os Ocultos atacam do escuro.'); som('chefe'); ocultos.clear(); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '46') { ocultos.add(+v[3]); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '45') { if (ocultos.delete(+v[3])) { const g = estado.get(+v[3]); if (g) { numeros.push({ x: g.x, y: g.y, txt: 'Oculto!', cor: '#c9a0ff', t: 1, amigo: false }); g.flash = 0.2; } } return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '44') { aviso('A Fonte Envenenada · os mortos apodrecem a água da vila'); anotar('Você achou a <b>Fonte Envenenada</b>: limpe a sala para a água voltar a correr.'); som('chefe'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '43') {
    tremor = Math.max(tremor, 0.6); clarao = Math.max(clarao, 0.45); som('chefe'); apocFx.push({ p: [+v[10] + so[0], +v[11] + so[1]], r: +v[4] || 4.5, t: performance.now() }); if (apocFx.length > 4) apocFx.shift();
    vistosDeEspecie = vistosDeEspecie || {}; if (!vistosDeEspecie['43']) { vistosDeEspecie['43'] = 1; aviso('Apocalipse! · o fogo de Diablo fere todos em volta dele'); }
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '42') {
    const ox2 = +v[10], oy2 = +v[11], dx2 = ox2 + +v[12], dy2 = oy2 + +v[13], g = estado.get(+v[3]);
    teleFx.push({ p: [ox2 + so[0], oy2 + so[1]], t: performance.now() }, { p: [dx2 + so[0], dy2 + so[1]], t: performance.now() + 90 }); if (teleFx.length > 12) teleFx.splice(0, teleFx.length - 12);
    if (g) { g.rx = dx2; g.ry = dy2; g.x = dx2; g.y = dy2; }
    som('bencao'); vistosDeEspecie = vistosDeEspecie || {}; if (!vistosDeEspecie['42']) { vistosDeEspecie['42'] = 1; aviso('O Magistrado se teleporta quando alguém encosta'); }
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '41') {
    const semente = Math.random() * 1000; raiosFx.push({ a: [+v[10] + so[0], +v[11] + so[1]], b: [+v[10] + +v[12] + so[0], +v[11] + +v[13] + so[1]], t: performance.now() + (+v[4] || 0) * 55, s: semente });
    if (raiosFx.length > 24) raiosFx.shift();
    if (+v[4] === 0) { som('critico'); clarao = Math.max(clarao, 0.3); vistosDeEspecie = vistosDeEspecie || {}; if (!vistosDeEspecie['41']) { vistosDeEspecie['41'] = 1; aviso('Relâmpago em Cadeia · salta de um inimigo para o outro'); } }
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '39') {
    const inimigaM = v[6] === '1' || v[6] === 'True' || v[6] === 'true'; muralhasFx.push({ a: [+v[10], +v[11]], d: [+v[12], +v[13]], t0: tempoSim, dur: +v[4] || 6, so: [so[0], so[1]], ini: inimigaM }); if (muralhasFx.length > 6) muralhasFx.shift(); som(inimigaM ? 'perigo' : 'bencao'); clarao = Math.max(clarao, 0.25);
    vistosDeEspecie = vistosDeEspecie || {}; const kM = inimigaM ? '39i' : '39'; if (!vistosDeEspecie[kM]) { vistosDeEspecie[kM] = 1; aviso(inimigaM ? 'Lázarus acende uma muralha de fogo sob o grupo!' : 'Muralha de Fogo · queima quem atravessa'); }
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '40') { muralhasFx = []; return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '38') {
    const g = estado.get(+v[3]), TDI = { Fisico: 'físico', Fogo: 'fogo', Gelo: 'gelo', Raio: 'raio', Veneno: 'veneno', Sombra: 'sombra' }; numeros.push({ x: g ? g.x : +v[10], y: g ? g.y : +v[11], txt: 'Imune', cor: '#b0b8c8', t: 1, amigo: false });
    vistosDeEspecie = vistosDeEspecie || {}; if (!vistosDeEspecie['38']) { vistosDeEspecie['38'] = 1; aviso('Imune a ' + (TDI[v[5]] || 'esse dano') + ' · veja as imunidades no Bestiário'); }
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '37') {
    const g = estado.get(+v[3]); numeros.push({ x: g ? g.x : +v[10], y: g ? g.y : +v[11], txt: 'Cercam!', cor: '#ffd27a', t: 1, amigo: false });
    vistosDeEspecie = vistosDeEspecie || {}; if (!vistosDeEspecie['37']) { vistosDeEspecie['37'] = 1; aviso('Bestas não vêm de frente · a matilha cerca pelos flancos'); }
    return;
  }
  if (tipo === 'EfeitoAplicado' && (v[14] === '33' || v[14] === '34')) { const g = estado.get(+v[3]); if (g) numeros.push({ x: g.x, y: g.y, txt: v[14] === '33' ? 'Poção de vida' : 'Poção de mana', cor: v[14] === '33' ? '#ff8a8a' : '#8ab8ff', t: 1, amigo: true }); som('bencao'); if (cintoHud) cintoHud[v[14] === '33' ? 0 : 1] = Math.max(0, cintoHud[v[14] === '33' ? 0 : 1] - 1); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '35') { numeros.push({ x: +v[10], y: +v[11], txt: +v[4] ? '+1 poção de mana' : '+1 poção de vida', cor: '#e8dcb0', t: 1, amigo: true }); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '36') { portalFx = { x: +v[10] + 1.2, y: +v[11], t: performance.now(), so: [so[0], so[1]] }; const an = Math.round(+v[4] || 0); aviso(`Portal da Cidade · você volta ao andar ${an}`); anotar(`<b style="color:#7ab0ff">Portal da Cidade aberto no andar ${an}.</b> Todo o saque veio com você; a próxima descida começa lá.`); som('bencao'); clarao = Math.max(clarao, 0.5); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '29') { aviso('O Golem de Ossos se ergue'); som('chefe'); tremor = Math.max(tremor, 0.3); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '28') { aviso('★ Um item Único caiu!'); anotar('<b style="color:#ffcf5a">★ Um item Único caiu.</b> Identifique-o no Ferreiro para ver qual é.'); som('chefe'); clarao = Math.max(clarao, 0.5); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '24') { const nf = ['Lança de Ossos', 'Drenar Vida', 'Explosão de Cadáver', 'Reerguer', 'Maldição da Fraqueza', 'Sacrifício', 'Golem de Ossos'][Math.round(+v[4] || 0)] || 'feitiço'; aviso('Livro de feitiço · ' + nf); anotar(`Você achou um <b style="color:#9fd0ff">Livro de ${nf}</b>. Será lido ao voltar (se a Magia permitir).`); som('bencao'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '23') { window.__atraidas = (window.__atraidas || 0) + 1; const n = Math.round(+v[4] || 0); aviso(`O barulho atraiu a sala vizinha! +${n} inimigos pela porta`); anotar(`A luta ecoou: <b>${n} inimigos</b> da sala ao lado vieram pela porta. Aquela sala ficou vazia.`); som('chefe'); tremor = Math.max(tremor, 0.25); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '22') { aviso('O Rei ergue mais esqueletos!'); som('chefe'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '21') {   // objeto quebrado: v[2] = índice, v[3] = 1 item / 2 morto se levanta, v[4] = Ossos
    const q = quebraveis[+v[2]], qx = q ? q[2] : 10, qy = q ? q[3] : 6, tq = q ? q[1] : 0, val = +v[4] || 0;
    if (q) q[4] = true;
    for (let i = 0; i < 9; i++) particulas.push([qx, qy, 0.4, rnd(-2.5, 2.5), rnd(-1.5, 1.5), rnd(2, 6), tq === 2 ? '#8a8880' : tq === 3 ? '#b08a5a' : '#6a4a2a', rnd(0.3, 0.7)]);
    if (tq === 1) { tremor = Math.max(tremor, 0.3); for (let i = 0; i < 16; i++) particulas.push([qx, qy, 0.5, rnd(-4, 4), rnd(-2.5, 2.5), rnd(3, 9), i % 2 ? '#ff9a40' : '#ffd070', rnd(0.3, 0.7)]); som('explosao'); } else som('golpe');
    if (val > 0) numeros.push({ x: qx, y: qy, txt: '+' + Math.round(val), cor: '#e8dcb0', t: 1, amigo: true });
    if (v[3] === '2') { aviso('O morto se levanta!'); numeros.push({ x: qx, y: qy, txt: 'Morto-vivo!', cor: '#ff8a6a', t: 1, amigo: true }); }
    else if (v[3] === '1') numeros.push({ x: qx, y: qy - 0.6, txt: 'Item!', cor: '#9ad0ff', t: 1, amigo: true });
    return;
  }
  if (tipo === 'EfeitoAplicado' && v[14] === '20') { unicoAnunciar = +v[3]; tremor = 0.25; som('secreto'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '19') { const g = estado.get(+v[3]); if (g) numeros.push({ x: g.x, y: g.y, txt: 'Atravessou', cor: '#b8c8ff', t: 1, amigo: true }); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '18') { const g = estado.get(+v[3]); if (g) { numeros.push({ x: g.x, y: g.y, txt: '+' + Math.round(+v[4]), cor: '#7fe08a', t: 1, amigo: true }); for (let i = 0; i < 5; i++) particulas.push([g.x, g.y, 0.6, rnd(-1, 1), rnd(-1, 1), rnd(1.5, 3.5), '#7fe08a', 0.5]); } return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '11') { const g = estado.get(+v[3]); if (g) g.gelo = tempoSim + (+v[4] || 1.8); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '14') { aviso('A patrulha entrou na luta!'); som('chefe'); tremor = 0.25; anotar('A <b style="color:#ff7a5a">patrulha errante</b> cruzou com o grupo e entrou na luta.'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '15') { aviso('Patrulha vencida: ela carregava um item'); som('bau'); anotar('Patrulha vencida: o que ela saqueou agora é seu.'); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '13') { aviso('Chave do cofre encontrada!'); som('bau'); anotar('<b style="color:#ffd84a">Chave do cofre</b> tomada dos inimigos.'); for (let i = 0; i < 20; i++) particulas.push([x, y, 1, rnd(-2, 2), rnd(-1.5, 1.5), rnd(3, 7), '#ffd84a', rnd(0.6, 1.1)]); return; }
  if (tipo === 'EfeitoAplicado' && v[14] === '12') { const g = estado.get(+v[3]); if (g) g.preso = tempoSim + (+v[4] || 2); som('perigo'); if (!evento.avisouGrilhoes) { evento.avisouGrilhoes = true; aviso('Grilhões! Seus minions mais fortes ficam presos'); } return; }
  if (tipo === 'ItemCaiu' && +v[14] >= 2) anotar(`Caiu um item <span class="r${v[14]}">${RARIDADES[+v[14]]}</span>.`);
  if (tipo === 'AndarConcluido' && !silencio) { descida = { t0: tempoSim, T: 2.4 }; som('porta'); }
  if (tipo === 'AndarConcluido') anotar(`Andar ${v[14]} concluído em ${Math.round(+v[4])} s.`);
  if (tipo === 'EntidadeSurgiu' && +v[14] > 0 && v[6] !== '1') { batizar(+v[3], +v[14], v[15] || ''); return; }
  if (tipo === 'Morte' && campeoes.has(+v[3])) { const cp = campeoes.get(+v[3]); campeoes.delete(+v[3]); if (!silencio) { aviso(cp.nome + ' tombou'); anotar(`<b style="color:#ffd84a">${cp.nome}</b> tombou.`); } }
  if (tipo === 'Dano' && +v[4] > 0) {
    som(v[6] === '1' ? 'critico' : 'golpe');
    if (Math.random() < 0.6) mancha(x + rnd(-0.3, 0.3), y + rnd(-0.2, 0.2), 5, 2);
    for (let i = 0; i < 3; i++) particulas.push([x, y, 0.9, rnd(-1.5, 1.5), rnd(-1, 1), rnd(1, 3.5), '#78080c', 0.6]);
    const alvo = estado.get(+v[3]);
    if (alvo) alvo.flash = 0.12;
    if (alvo && (v[6] === '1' || +v[4] >= 1)) numeros.push({ x, y, v: Math.round(+v[4]), crit: v[6] === '1', t: 0.8, amigo: alvo && !alvo.inimigo });
  } else if (tipo === 'Morte') {
    const id = +v[3], s = estado.get(id); if (!s) return;
    const over = v[8] === '1'; som(over ? 'explosao' : 'morte');
    mortos.set(id, { ...s, t: tempoSim, over, cond: v[7] });
    mancha(x, y, over ? 26 : 16, over ? 14 : 8);
    marcaDeCondicao(x, y, v[7], over || s.chefe);
    if (over || s.chefe) tremor = s.chefe ? 0.5 : 0.18;
    const f = over ? 5 : 2.5;
    for (let i = 0, k = over ? 46 : 14; i < k; i++)
      particulas.push([x, y, 0.8, dx * f * Math.random() + rnd(-f, f) * 0.6, dy * f * Math.random() + rnd(-f, f) * 0.4, rnd(1.5, over ? 6.5 : 4),
        over ? ['#82080c', '#5a0408', '#c8bea0'][Math.random() * 3 | 0] : '#78080c', rnd(0.5, 1.1)]);
  } else if (tipo === 'CadaverCriado') cadaverDe.set(+v[3], +v[2]);
  else if (tipo === 'CadaverConsumido' || tipo === 'CadaverRemovido') {
    const id = cadaverDe.get(+v[3]); cadaverDe.delete(+v[3]);
    if (mortos.has(id)) {
      pilhaDeOssos(x, y);
      if (tipo === 'CadaverConsumido') {
        mancha(x, y, 22, 12);
        for (let i = 0; i < 30; i++) particulas.push([x, y, 0.4, rnd(-4, 4), rnd(-3, 3), rnd(2, 6), Math.random() < 0.5 ? '#82080c' : '#c8bea0', rnd(0.5, 1)]);
      }
      mortos.delete(id);
    }
  } else if (tipo === 'FeiticoLancado' && +v[14] >= 100) {
    som('feitico');
    const f = +v[14] - 100; clarao = Math.max(clarao, 0.25);
    if (f === 1) { const n = necro; if (n) raios.push([n.x, n.y, x, y, 0.4]); for (let i = 0; i < 26; i++) particulas.push([x, y, 1, rnd(-3, 3), rnd(-2, 2), rnd(2, 7), '#9a6aff', rnd(0.5, 1)]); }
    else if (f === 2) { raios.push([x, y, x, y, 0.35, +v[4] || 3]); for (let i = 0; i < 30; i++) { const a = rnd(0, 6.28); particulas.push([x + Math.cos(a) * 2, y + Math.sin(a) * 1.4, 0.8, Math.cos(a) * 3, Math.sin(a) * 2, rnd(1, 3), '#c8bea0', rnd(0.3, 0.6)]); } }
    else if (f === 3) { for (let i = 0; i < 46; i++) particulas.push([x + rnd(-3, 3), y + rnd(-2, 2), rnd(5, 9), 0, 0, rnd(-9, -4), '#c8bea0', rnd(0.6, 1.1)]); tremor = Math.max(tremor, 0.22); }
    else if (f === 4) { for (const s of estado.values()) if (s.viva && !s.inimigo) for (let i = 0; i < 6; i++) particulas.push([s.x + rnd(-0.4, 0.4), s.y, 0.2, 0, 0, rnd(2, 5), '#7fd8ff', rnd(0.5, 0.9)]); }
  } else if (tipo === 'FeiticoLancado') {
    const n = estado.get(+v[2]);
    if (n && v[14] !== '0') raios.push([n.x, n.y, x, y, 0.4]);
  } else if (tipo === 'Explosao') {
    som('explosao');
    raios.push([x, y, x, y, 0.35, +v[4] || 1.5]);
    marcaDeCondicao(x, y, 'Queimado', true); clarao = Math.max(clarao, 0.5);
    tremor = Math.max(tremor, 0.15);
  } else if (tipo === 'EfeitoAplicado' && v[14] === '10') {
    aviso('Golpe interrompido!'); clarao = Math.max(clarao, 0.35);
    for (let i = 0; i < 18; i++) particulas.push([x, y, 1.2, rnd(-3, 3), rnd(-2, 2), rnd(2, 5), '#ffd84a', rnd(0.4, 0.8)]);
  } else if (tipo === 'AlmaCeifada') {
    som('alma');
    if (necro) almasVoando.push({ x, y, t: 0 });
    if (v[6] === '1') { aviso('Alma melhorada pelo toque!'); for (let i = 0; i < 24; i++) particulas.push([x, y, 1, rnd(-3, 3), rnd(-2, 2), rnd(3, 7), '#78ffaa', rnd(0.6, 1.1)]); }
    for (let i = 0; i < 14; i++) particulas.push([x, y, 1, rnd(-1.5, 1.5), rnd(-1, 1), rnd(2, 5), '#78ffaa', rnd(0.4, 0.8)]);
  }
}

function fimDeExpedicao(p) {
  if (estat) { estat.fim = tempoSim; estatAnterior = estat; } estat = null;
  som('fim');
  if (!silencio) {
    const r = $('resumo');
    r.innerHTML = `<div class="t">Expedição encerrada · andar ${p[1]}</div><div class="g"><div><b>${fmt(+p[4])}</b><span>Ossos</span></div><div><b>${p[2]}</b><span>almas</span></div><div><b>${p[3]}</b><span>itens</span></div><div><b>${p[5]}</b><span>mortes</span></div></div>` + (explor.salas ? `<div class="nota" style="color:var(--texto)">Salas especiais: ${explor.salas} · baús: ${explor.baus} · com a sua mão: ${explor.toques}${explor.secretos ? ' · <span style="color:#ff9ad0">nível secreto limpo</span>' : ''}</div>` : '') + `<div class="nota">As almas foram para o Filactério e os itens para o Inventário. Toque para fechar.</div>`;
    r.hidden = false; clearTimeout(fimDeExpedicao.t); fimDeExpedicao.t = setTimeout(() => { r.hidden = true; }, 7000);
  }
  explor = { salas: 0, baus: 0, toques: 0, secretos: 0 };
  limparArena(true); andar = 0; if (!silencio) { salvar(); atualizarPainel(true); }
}

function quadro(m, acao, dir, tempo) {
  let a = m.acoes[acao];
  if (!a) a = (acao === 'conjurar' && m.acoes.atacar) || (acao === 'atacar' && m.acoes.conjurar) || m.acoes.parado;
  let f = Math.floor(tempo * a.fps);
  f = a.loop ? f % a.frames : Math.min(f, a.frames - 1);
  return [a.img, f * m.W, dir * m.H];
}

let saidaAtual = '', viagem = [1, 0];
// ---- descer e chegar: o grupo anda até a escada e some por ela; no andar novo, entra pela porta ----
const ESCADA = [18.3, 10.6]; let descida = null, chegada = null, tituloAndar = null;
function cenaDoGrupo(s) {
  if (descida) {
    const f = Math.max(0, Math.min(1, (tempoSim - descida.t0) / descida.T * 1.25 - 0.25 * ((s.id * 7) % 10) / 10)), k = f * f * (3 - 2 * f);
    return [s.rx + (ESCADA[0] - s.rx) * k, s.ry + (ESCADA[1] - s.ry) * k, f > 0.93 ? 0 : 1, ESCADA[0] - s.rx, ESCADA[1] - s.ry];
  }
  if (chegada) {
    const f0 = (tempoSim - chegada.t0) / chegada.T; if (f0 >= 1.3) { chegada = null; return null; }
    const f = Math.max(0, Math.min(1, f0 * 1.3 - 0.3 * ((s.id * 7) % 10) / 10)), k = f * f * (3 - 2 * f);
    return [-1.5 + (s.rx + 1.5) * k, 6 + (s.ry - 6) * k, f <= 0.02 ? 0 : 1, 1, 0, f >= 1];
  }
  return null;
}
// ---- o andar como um mundo contínuo: canvases das salas, viagem do grupo pelos corredores, zoom ----
let reordenado = false, camSalto = false, camForcar = false;
let unicoAnunciar = -1;
let salasCv = [], mapaSujo = true, assar = -1, viag = null, escAtual = 1, zoomModo = 2, zoomLivre = 0, pan = [0, 0];   // zoomModo: 0 = perto, 1 = médio, 2 = o andar inteiro
try { const z = localStorage.getItem('necromante-idle-save-v1-zoom'); if (z !== null) zoomModo = +z; } catch (e) { /* padrão */ }
// Luz e névoa (Diablo 1): o traçado do andar inteiro aparece, mas o que há em cada sala só se revela quando o grupo entra nela ou numa vizinha.
const vistas = new Set(); let vistasAndar = -1;
function atualizarVistas() {
  if (vistasAndar !== andar) { vistasAndar = andar; vistas.clear(); }
  if (!mapa.length || !mapa[masm.sala]) return;
  for (let i = 0; i < mapa.length; i++) if (i <= masm.sala || vizinhas(i, masm.sala)) vistas.add(mapa[i][0] + ',' + mapa[i][1]);
}
const revelada = (i) => i <= masm.sala || !mapa[i] || masm.secreto || vistas.has(mapa[i][0] + ',' + mapa[i][1]) || i === mapa.length - 1 && (masm.tipos[i] === 2);   // o covil do chefe sempre se conhece
const vizinhas = (a, b) => mapa[a] && mapa[b] && Math.abs(mapa[a][0] - mapa[b][0]) + Math.abs(mapa[a][1] - mapa[b][1]) === 1;
function caminhoNoMapa(a, b) {      // salas entre a e b: vizinhas na grade têm sempre um corredor; caminho mais curto
  if (!mapa[a] || !mapa[b]) return [a, b];
  const ant = new Array(mapa.length).fill(-2), fila = [a]; ant[a] = -1;
  while (fila.length) { const u = fila.shift(); if (u === b) break; for (let v = 0; v < mapa.length; v++) if (ant[v] === -2 && vizinhas(u, v)) { ant[v] = u; fila.push(v); } }
  if (ant[b] === -2) return [a, b];
  const cam = []; for (let u = b; u !== -1; u = ant[u]) cam.unshift(u); return cam;
}
function iniciarViagem(de, para) {
  if (!mapa[de] || !mapa[para]) { viag = null; return; }
  const cam = caminhoNoMapa(de, para), pts = [];
  const centro = (i, x, y) => { const o = offDaSala(i); return [o[0] + x, o[1] + y]; };
  pts.push(centro(de, 5, 6));
  for (let k = 0; k + 1 < cam.length; k++) {
    const a = cam[k], b = cam[k + 1];
    if (mapa[a][1] !== mapa[b][1]) { pts.push(centro(a, 10, 6)); pts.push(centro(b, 10, 6)); }   // corredor vertical: passa pelo eixo da porta
    else pts.push(centro(b, 5, 6));
  }
  pts.push(centro(para, 5, 6));
  let total = 0; const seg = []; for (let k = 0; k + 1 < pts.length; k++) { const d = Math.hypot(pts[k + 1][0] - pts[k][0], pts[k + 1][1] - pts[k][1]); seg.push(d); total += d; }
  const passos = cam.length - 1, T = Math.min(2.9, 0.8 + 0.7 * passos) * 0.95;   // igual a Config.ViagemBase/PorPassagem/Maxima
  viag = { pts, seg, total, t0: tempoSim, T, fim: centro(para, 5, 6), dur: 0, ate: 0 };
  if (cam.length > 1) { const dxp = mapa[cam[1]][0] - mapa[cam[0]][0]; viag.ate = Math.min(total * 0.8, dxp === 1 ? 15.3 : dxp === -1 ? 5.3 : 11.3); }
  if (portaPendente && Math.abs(tempoSim - portaPendente.t) < 0.35) { viag.dur = portaPendente.dur; portaPendente = null; }
}
let portaPendente = null;
// porta emperrada: o grupo anda até a porta, para encostado nela batendo pelo tempo do arrombamento e só então segue viagem
function distanciaNaViagem(e) {
  const sm = (x) => { const u = Math.max(0, Math.min(1, x)); return u * u * (3 - 2 * u); };
  if (!viag.dur) return viag.total * sm(e / viag.T);
  const T1 = viag.T * Math.max(0.2, viag.ate / viag.total);
  if (e < T1) return viag.ate * sm(e / T1);
  if (e < T1 + viag.dur) return viag.ate - 0.18 * Math.abs(Math.sin((e - T1) * 7));
  return viag.ate + (viag.total - viag.ate) * sm((e - T1 - viag.dur) / Math.max(0.2, viag.T - T1));
}
function abrirPortaNaViagem() { if (!viag || !viag.dur) return; const T1 = viag.T * Math.max(0.2, viag.ate / viag.total), e = tempoSim - viag.t0; viag.dur = Math.max(0.01, Math.min(viag.dur, e - T1)); }
window.__naPorta = () => { if (!viag || !viag.dur) return 0; const T1 = viag.T * Math.max(0.2, viag.ate / viag.total), e = tempoSim - viag.t0; return e >= T1 && e < T1 + viag.dur ? 1 : 0; };
// deslocamento (em unidades da simulação) dos aliados em relação à posição final, e para onde olham
function deltaDaViagem(atraso = 0, ox = 0, oy = 0) {
  if (!viag) return null;
  const dec = tempoSim - viag.t0, t0 = dec / (viag.T + viag.dur); if (t0 >= 1 || viag.total < 0.01) { if (!atraso) viag = null; return null; }
  // cada um parte um pouco depois do anterior e se espreme para caber no corredor: o grupo passa em fila, não em bloco
  const t = Math.max(0, Math.min(1, t0 * (1 + atraso) - atraso)), aperto = 1 - 0.72 * Math.sin(Math.PI * t);
  if (atraso || ox || oy) {
    let d2 = viag.dur ? distanciaNaViagem(Math.max(0, dec - atraso * 0.5)) : viag.total * (t * t * (3 - 2 * t)), k2 = 0; while (k2 < viag.seg.length - 1 && d2 > viag.seg[k2]) { d2 -= viag.seg[k2]; k2++; }
    const a2 = viag.pts[k2], b2 = viag.pts[k2 + 1], f2 = viag.seg[k2] > 0 ? d2 / viag.seg[k2] : 1;
    return [a2[0] + (b2[0] - a2[0]) * f2 - viag.fim[0] + ox * (aperto - 1), a2[1] + (b2[1] - a2[1]) * f2 - viag.fim[1] + oy * (aperto - 1), b2[0] - a2[0], b2[1] - a2[1]];
  }
  let d = viag.dur ? distanciaNaViagem(dec) : viag.total * (t * t * (3 - 2 * t)), k = 0; while (k < viag.seg.length - 1 && d > viag.seg[k]) { d -= viag.seg[k]; k++; }
  const a = viag.pts[k], b = viag.pts[k + 1], f = viag.seg[k] > 0 ? d / viag.seg[k] : 1;
  return [a[0] + (b[0] - a[0]) * f - viag.fim[0], a[1] + (b[1] - a[1]) * f - viag.fim[1], b[0] - a[0], b[1] - a[1]];
}
function reconstruirMundo(manterSangue) {
  salasCv = mapa.map((m, i) => { const cv = document.createElement('canvas'); cv.width = CW / 2; cv.height = CH / 2; construirSala(i, cv, 0.5); return cv; });
  construirSala(); if (!manterSangue) sctx.clearRect(0, 0, CW, CH);
}
function limitesDoMundo() {
  let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
  mapa.forEach((m, i) => { const o = offDaSala(i); for (const [x, y] of [[-1, -1], [21, -1], [21, 13], [-1, 13]]) { const [px, py] = W(o[0] + x, o[1] + y); x0 = Math.min(x0, px); x1 = Math.max(x1, px); y0 = Math.min(y0, py - PAREDE); y1 = Math.max(y1, py + 30); } });
  return mapa.length ? [x0, y0, x1, y1] : [0, 0, CW, CH];
}
// ---- campeões: todo elite ganha nome próprio e epíteto conforme os modificadores ----
const campeoes = new Map();
const NOME_A = ['Gor', 'Vel', 'Mor', 'Kaz', 'Ul', 'Thar', 'Bel', 'Nox', 'Sar', 'Dru', 'Hex', 'Ruk', 'Zul', 'Ael', 'Org'], NOME_B = ['vax', 'drin', 'goth', 'mund', 'rak', 'thas', 'zeth', 'gan', 'vor', 'lith', 'mor', 'kul', 'nar', 'dûm', 'iel'];
const EPITETOS = [[1, 'o Sedento'], [2, 'o Volátil'], [4, 'o Veloz'], [8, 'o Couraçado']];
function batizar(id, mods, especie) {
  const a = NOME_A[(id * 7 + mods * 3) % NOME_A.length], b = NOME_B[(id * 13 + mods) % NOME_B.length], ep = EPITETOS.filter(([m]) => mods & m).map(([, e]) => e);
  const nome = a + b + (ep.length ? ', ' + ep[(id + mods) % ep.length] : ', o Maldito');
  campeoes.set(id, { nome, especie }); if (campeoes.size > 40) campeoes.delete(campeoes.keys().next().value);
  if (!silencio) { aviso('Campeão: ' + nome); anotar(`Surge o campeão <b style="color:#ffd84a">${nome}</b>.`); som('perigo'); }
}
// ---- lições de toque: uma por vez, só quando a chance aparece; somem quando o jogador faz ----
const LICOES = [['orbe', 'Toque no orbe verde para ceifar a alma na hora', () => orbes.length > 0], ['sala', 'Toque no objeto da sala: rende mais (e desarma armadilhas)', () => !!objeto && !objeto.aberto && masm.fase === 'Explorando'],
  ['golpe', 'Toque no inimigo com o círculo vermelho para interromper o golpe', () => [...estado.values()].some((e) => e.viva && e.inimigo && e.golpe > 0)],
  ['cura', 'Toque no retrato de um aliado ferido para curá-lo', () => sopro <= 0 && [...estado.values()].some((e) => e.viva && !e.inimigo && e.vida < 0.5)],
  ['marca', 'Toque num inimigo para marcar o alvo dos seus minions', () => [...estado.values()].filter((e) => e.viva && e.inimigo).length >= 3],
  ['detonar', 'Detonar explode os cadáveres no chão', () => poderes[0] <= 0 && poderes[3] >= 3], ['mapa', 'Use o botão ⤢ para aproximar ou ver o andar inteiro', () => mapa.length >= 4],
  ['rota', 'Toque no símbolo de uma sala para o grupo ir a ela primeiro', () => !!licoes.mapa && mapa.length - masm.sala >= 4]];
let licoes = {}; try { licoes = JSON.parse(localStorage.getItem('necromante-idle-save-v1-licoes') || '{}'); } catch (e) { /* começa do zero */ }
function aprendeu(k) { if (licoes[k]) return; licoes[k] = 1; try { localStorage.setItem('necromante-idle-save-v1-licoes', JSON.stringify(licoes)); } catch (e) { /* tudo bem */ } }
// ---- ficha do inimigo marcado: a barra de descrição do Diablo 1 ----
const FAMILIAS = ['Morto-vivo', 'Demônio', 'Besta', 'Aberração', 'Espírito'], TIPOS_DANO = ['físico', 'fogo', 'gelo', 'raio', 'veneno', 'sombra'], MODS_ELITE = [[1, 'Vampírico'], [2, 'Explosivo'], [4, 'Veloz'], [8, 'Escudo de Ossos']];
let fichaTxt = '', fichaEm = 0;
function atualizarFicha() {
  const c = $('ficha'); if (!c || !J || !J.FichaDoMarcado) return;
  const agora = performance.now(); if (agora - fichaEm < 200) return; fichaEm = agora;
  let h = '';
  if (marcado >= 0) {
    let f = null; try { const bruto = J.FichaDoMarcado(); f = bruto ? JSON.parse(bruto) : null; } catch (e) { f = null; }
    if (f) {
      const imu = [], rst = [], frc = []; f.res.forEach((v, t) => { if (v >= 100) imu.push(TIPOS_DANO[t]); else if (v > 0) rst.push(TIPOS_DANO[t] + ' ' + v + '%'); else if (v < 0) frc.push(TIPOS_DANO[t]); });
      const mods = MODS_ELITE.filter(([bit]) => f.mods & bit).map(([, n]) => n);
      const cls = f.tipo === 'Único' ? 'unico' : f.tipo === 'Campeão' ? 'campeao' : f.tipo === 'Chefe' ? 'chefe' : '';
      h = `<b class="${cls}">${esc(f.nome)}${f.tipo ? ' · ' + f.tipo : ''}</b>` +
        `${FAMILIAS[f.familia] || ''}${f.nome !== f.especie ? ' · ' + esc(f.especie) : ''} · vida ${fmt(f.vida)} de ${fmt(f.max)} · ${fmt(f.mortes)} mortos${mods.length ? ' · ' + mods.join(', ') : ''}<br>` +
        ((imu.length ? `<span class="im">Imune a ${imu.join(', ')}</span>` : '') + (rst.length ? (imu.length ? ' · ' : '') + 'resiste a ' + rst.join(', ') : '') + (frc.length ? (imu.length || rst.length ? ' · ' : '') + `<span class="fr">fraco a ${frc.join(', ')}</span>` : '') || 'sem resistências');
    }
  }
  if (h) { let chefeVivo = false; for (const eC of estado.values()) if (eC.viva && eC.chefe && eC.inimigo) { chefeVivo = true; break; } c.style.top = chefeVivo ? '104px' : ''; }
  if (h !== fichaTxt) { fichaTxt = h; c.innerHTML = h; c.hidden = !h; }
}
function atualizarLicao() {
  const l = !silencio && velocidade > 0 ? LICOES.find(([k, , cond]) => !licoes[k] && cond()) : null, c = $('licao'), tx = l ? l[1] : '';
  if (c.textContent !== tx) { c.textContent = tx; c.hidden = !tx; }
}
let camX = 0, camY = 0, salaSuja = false, mapa = [], dica = true, dicaMarca = true;
// ---- masmorra: salas, objeto da sala, ambiente do chefe ----
const SALAS = [['⚔', 'Combate'], ['☠', 'Elite'], ['♛', 'Chefe'], ['▣', 'Baú'], ['✦', 'Santuário'], ['☥', 'Almas presas'], ['‼', 'Emboscada'], ['♨', 'Fonte'], ['?', 'Nível secreto'], ['$', 'Mercador'], ['†', 'Altar de sangue'], ['·', 'Câmara vazia'], ['▲', 'Armadilha'], ['▩', 'Cofre trancado'], ['⚒', 'Forja de ossos'], ['◍', 'Ninho'], ['▤', 'Biblioteca'], ['☣', 'Fosso de monstros'], ['⛓', 'Sala de tortura'], ['⚔', 'Arsenal'], ['🜏', 'Porta do Açougueiro'], ['♚', 'Tumba do Rei'], ['♒', 'Fonte envenenada'], ['☗', 'Câmara de Ossos'], ['◉', 'Salões dos Cegos']];
let chaveCofre = [-1, false];
let patRota = [], patAlvo = -1, patGeo = '';
let patrulha = null, patPos = null;   // patrulha errante: {sala, especies}; patPos = onde ela está desenhada no mundo (anda até a sala nova)   // [sala de combate que guarda a chave do cofre, já foi pega]
const AMBIENTES = [null, ['Lava', 'erupções queimam quem estiver em cima, dos dois lados'], ['Névoa pútrida', 'todos perdem vida aos poucos e as curas rendem menos'], ['Geada', 'de tempos em tempos, unidades dos dois lados congelam'], ['Desabamento', 'pedras caem do teto sobre os dois lados, pequenas e frequentes']];
const DICA_SALA = { 21: 'missão: o Rei ergue esqueletos enquanto vive', 20: 'missão: abrir é escolha sua. Ele é forte e rápido', 19: 'uma arma ou armadura (Rara se você tocar)', 18: 'Sangue dos supliciados', 17: 'horda de uma espécie só, guardando um item', 16: 'um tomo: experiência para o necromante', 15: 'ovos: toque para esmagar antes que choquem', 14: 'couraça de ossos para todos (25% da vida)', 13: 'só abre com a chave: passe antes pela sala que a guarda', 3: 'Ossos e talvez um item', 4: 'santuário de efeito incerto: pode ajudar ou cobrar um preço', 5: 'uma alma de graça', 7: 'cura 20% de todos', 9: 'troca Ossos por um item', 10: 'vida por +80% de almas no andar', 12: 'fere o grupo; toque para desarmar' };
const NOME_SECRETO = ['', 'O Pasto Profano', 'A Câmara do Tesouro', 'A Cripta dos Espelhos'];
// Santuários de efeito incerto (Diablo 1): [nome, frase enigmática, efeito revelado depois de usado]
const SANTUARIOS = [null,
  ['Santuário Misterioso', 'Uns minguam para que um cresça.', 'seus aliados batem muito mais forte, mas apanham um pouco mais'],
  ['Santuário Sombrio', 'Quem se guarda pouco fere.', 'seus aliados apanham bem menos, mas batem um pouco menos'],
  ['Santuário Estranho', 'A lâmina justa é ligeira.', 'seus aliados causam mais dano'],
  ['Santuário Reluzente', 'À luz, o oculto se mostra.', 'identifica os itens já achados nesta descida e faz cair mais itens'],
  ['Santuário Oculto', 'O que se gasta aqui, ali se poupa.', 'o equipamento não perde durabilidade nesta descida'],
  ['Santuário Religioso', 'A fé fecha as feridas.', 'cura todos os aliados por completo'],
  ['Santuário Mágico', 'Um véu se ergue entre ti e o golpe.', 'um escudo envolve todos os aliados'],
  ['Santuário Ornado', 'Saber custa o que o chão dá.', 'muita experiência para o necromante, mas menos Ossos'],
  ['Santuário Tétrico', 'Onde a carne cede, o ouro brota.', 'tira parte da vida de todos, e paga em ouro'],
  ['Santuário Profano', 'As almas ouvem; os vivos também.', 'caem mais almas, mas os inimigos ferem mais']];
const BENCAOS = SANTUARIOS.map((x) => (x ? x[0] + ': ' + x[2] : ''));
let santuariosVistos = 0;
let quebraveis = [];   // objetos quebráveis do andar: [sala, tipo, x, y, quebrado]
const nomesUnicos = new Map();   // id da entidade -> nome próprio do monstro único
let manaAbs = [0, 0], cintoMax = 3;
let vistosDeEspecie = null, cintoHud = null, portalFx = null, muralhasFx = [], raiosFx = [], teleFx = [], apocFx = [];
const ocultos = new Set();
let bencaos = null, bencaosMostradas = '', aEspera = [];   // aEspera: inimigos já postos nas salas do andar, ainda adormecidos
let masm = { fase: '', sala: 0, amb: 0, bencao: 0, secreto: false, ox: 0, oy: 0, tipos: [] }, salasChave = '', transicao = 0, objeto = null, perigos = [], diario = [], dicaSala = true;   // as lições de toque (LICOES) já ensinam isto
let explor = { salas: 0, baus: 0, toques: 0, secretos: 0 };
function anotar(t) { diario.unshift([andar, t]); if (diario.length > 40) diario.pop(); }
function lerMasmorra(p) {
  const antes = masm.sala, faseAntes = masm.fase, secAntes = masm.secreto;
  masm = { fase: p[1], sala: +p[2], amb: +p[3], bencao: +p[4], secreto: +p[5] > 0, qualSecreto: +p[5], ox: +p[6], oy: +p[7], tipos: p.slice(8).map(Number) };
  if (masm.fase === 'EmOnda' && faseAntes !== 'EmOnda' && viag && viag.dur) abrirPortaNaViagem();
  if (masm.sala !== antes) {
    objeto = null;
    if (masm.sala === 0 || antes > masm.sala) { mapaSujo = true; viag = null; descida = null; chegada = silencio ? null : { t0: tempoSim, T: 1.1 }; if (!silencio) { transicao = 1; tituloAndar = { t: performance.now(), a: 'Andar ' + andar, b: TEMAS_ANDAR[temaDoAndar(andar)][0] }; } }     // andar novo: tudo de novo, com um escurecer só aqui
    else { assar = antes; salaSuja = true; if (!silencio) { som('porta'); iniciarViagem(antes, masm.sala); } }
  }
  if (masm.fase === 'Explorando' && faseAntes !== 'Explorando' && masm.tipos[masm.sala] !== 11) { objeto = { tipo: masm.tipos[masm.sala], x: masm.ox, y: masm.oy, t: 0, aberto: 0, tocado: false }; if (!dicaSala && !silencio) { dicaSala = true; aviso('Toque no objeto da sala para render mais'); } }
  if (masm.secreto && !secAntes && !silencio) { som('secreto'); aviso('NÍVEL SECRETO · ' + NOME_SECRETO[masm.qualSecreto]); clarao = 0.8; tremor = 0.4; if (masm.qualSecreto === 3) anotar('<b style="color:#b8c8ff">Cripta dos Espelhos</b>: cada minion seu encara o próprio reflexo.'); }
  const t = masm.tipos[masm.sala], rot = (t === undefined ? '' : `Sala ${masm.sala + 1}/${masm.tipos.length} · ${SALAS[t][1]}`) + (masm.amb ? ' · ' + AMBIENTES[masm.amb][0] : '') + (masm.bencao ? ' · ' + BENCAOS[masm.bencao].split(':')[0] : '');
  if (rot !== lerMasmorra.rot) { lerMasmorra.rot = rot; $('t-sala').textContent = rot; }
}
let mapaChave = '';
function lerMapa(p) {
  const m = []; for (let i = 1; i + 2 < p.length; i += 3) m.push([+p[i], +p[i + 1], +p[i + 2]]);
  const geo = p.join(' ') + '|' + masm.tipos.join(',') + '|' + andar, chave = geo + '|' + masm.sala;
  if (geo !== lerMapa.geo) {
    const cels = m.map((c) => c[0] + ',' + c[1]).sort().join(';') + '|' + andar;
    reordenado = cels === lerMapa.cels && !!mapa[masm.sala] && !!m[masm.sala] && mapa[masm.sala][0] === m[masm.sala][0] && mapa[masm.sala][1] === m[masm.sala][1];   // só a ordem de visita mudou (rota do jogador)
    lerMapa.cels = cels; lerMapa.geo = geo; mapa = m; mapaSujo = true;
  }
  if (chave === mapaChave) return;
  mapaChave = chave; mapa = m;
  atualizarVistas();
  $('salas').innerHTML = mapaSvg(13, false);
  if (!$('mapa-grande').hidden) $('mapa-corpo').innerHTML = mapaGrandeHtml();
}
// o mapa do andar: salas numa grade, ligadas por corredores; visitadas apagadas, a atual em destaque
function mapaSvg(c, rotulos) {
  if (!mapa.length) return '';
  const xs = mapa.map((m) => m[0]), ys = mapa.map((m) => m[1]), x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
  const passo = c * 1.55, W = (x1 - x0) * passo + c + 6, H = (y1 - y0) * passo + c + 6, pos = (m) => [3 + (m[0] - x0) * passo, 3 + (y1 - m[1]) * passo];
  let h = `<svg viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" xmlns="http://www.w3.org/2000/svg">`;
  for (let i = 0; i < mapa.length; i++) for (let j2 = 0; j2 < i; j2++) { if (!vizinhas(i, j2)) continue; const [ax, ay] = pos(mapa[i]), [bx, by] = pos(mapa[j2]); h += `<line x1="${ax + c / 2}" y1="${ay + c / 2}" x2="${bx + c / 2}" y2="${by + c / 2}" stroke="${i <= masm.sala && j2 <= masm.sala ? '#8a744a' : '#4a3a3c'}" stroke-width="${Math.max(2, c * 0.22)}"/>`; }
  mapa.forEach((m, i) => {
    const [x, y] = pos(m), real = masm.tipos[i] || 0, atual = i === masm.sala, feita = i < masm.sala;
    // névoa: só se conhece o que já foi visitado, a sala seguinte e o covil do chefe
    const t = revelada(i) ? real : -1;   // o traçado inteiro aparece; o conteúdo só depois de visto
    const cor = t === 2 ? '#ff5060' : t === 8 ? '#ff9ad0' : t >= 3 && t !== 11 ? '#d9b25f' : '#b8a890';
    h += `<rect x="${x}" y="${y}" width="${c}" height="${c}" rx="${c * 0.18}" fill="${atual ? '#5a1a20' : feita ? '#241a1d' : '#120c0f'}" stroke="${atual ? '#ffd84a' : feita ? '#5c4c30' : '#4a3a3c'}" stroke-width="${atual ? 2 : 1}"><title>${t < 0 ? 'Sala desconhecida' : SALAS[t][1]}</title></rect>`;
    h += `<text x="${x + c / 2}" y="${y + c * 0.72}" text-anchor="middle" font-size="${c * 0.62}" fill="${cor}" opacity="${feita ? 0.4 : 1}">${t < 0 ? '?' : SALAS[t][0]}</text>`;
    if (rotulos) h += `<text x="${x + c / 2}" y="${y + c + 11}" text-anchor="middle" font-size="10" fill="${atual ? '#fff' : '#8d7c6c'}">${t < 0 ? '' : SALAS[t][1].split(' ')[0]}</text>`;
  });
  return h + '</svg>';
}
function mapaGrandeHtml() {
  const feitas = masm.sala, total = masm.tipos.length;
  return `<h3>Mapa do andar ${andar}</h3><div style="overflow:auto;text-align:center;padding:6px 0 14px">${mapaSvg(40, true)}</div>` +
    `<p class="nota">${feitas} de ${total} salas exploradas. O grupo percorre a masmorra inteira antes de descer.</p>` +
    '<p class="nota">' + [0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 11, 8].map((t) => `<span style="white-space:nowrap;margin-right:10px">${SALAS[t][0]} ${SALAS[t][1]}</span>`).join(' ') + '</p>';
}
function eventoDeSala(tipo, v, x, y) {
  const ts = +v[14];
  if (tipo === 'AmbienteDefinido') { som('chefe'); const a = AMBIENTES[ts]; if (a) { aviso('Sala do chefe · ' + a[0]); anotar(`Sala do chefe: <b>${a[0]}</b>, ${a[1]}.`); } return; }
  if (tipo === 'PerigoAnunciado') { som('perigo'); perigos.push({ x, y, r: +v[4], t: 0, dur: ts / TPS, pedra: v[5] === 'Fisico' }); return; }
  if (tipo === 'SalaResolvida' && ts === 11) { if (+v[4] > 0) aviso('Ossos esquecidos: +' + fmt(Math.round(+v[4]))); return; }
  if (tipo === 'SalaIniciada' && ts === 17) { aviso('Fosso de monstros!'); tremor = 0.35; som('secreto'); anotar('<b style="color:#c8e06a">Fosso de monstros</b>: a sala está entupida deles, e guardam alguma coisa.'); return; }
  if (tipo === 'SalaIniciada') { if (ts === 6) { aviso('Emboscada!'); tremor = 0.25; anotar('Emboscada: inimigos por todos os lados.'); } else if (ts === 8) anotar(`<b style="color:#ff9ad0">Nível secreto: ${NOME_SECRETO[+v[2]] || 'desconhecido'}.</b>`); return; }
  // SalaResolvida
  const sub = +v[2], toque = v[6] === '1', val = +v[4];
  if (!(ts === 3 && sub === 1)) { explor.salas++; if (ts === 3) explor.baus++; if (ts === 8) explor.secretos++; if (toque && ts !== 8 && ts !== 6) explor.toques++; }
  if (objeto) { objeto.aberto = 0.01; }
  som(ts === 13 ? (sub === 1 ? 'bau' : 'porta') : ts === 12 ? (sub === 1 ? 'bau' : 'critico') : ts === 3 || ts === 9 ? 'bau' : ts === 8 ? 'secreto' : ts === 11 ? 'toque' : 'bencao');
  const brilho = (cor, n) => { for (let i = 0; i < n; i++) particulas.push([x, y, 0.6, rnd(-2.5, 2.5), rnd(-1.5, 1.5), rnd(3, 8), cor, rnd(0.6, 1.2)]); };
  if (ts === 3) {
    if (sub === 1) { aviso('Era um mímico!'); tremor = 0.3; anotar('O baú era um <b>mímico</b>. Vencê-lo paga em dobro.'); brilho('#a01420', 30); }
    else { aviso((sub === 2 ? 'Baú dourado! ' : 'Baú: ') + '+' + fmt(Math.round(val)) + ' Ossos'); anotar(`${sub === 2 ? '<b style="color:#ffd84a">Baú dourado</b>' : 'Baú'}: +${fmt(Math.round(val))} Ossos${sub === 2 ? ', item Raro e Sangue' : ''}${toque ? ' (aberto por você)' : ''}.`); brilho('#ffd84a', sub === 2 ? 60 : 26); if (sub === 2) clarao = 0.5; }
  } else if (ts === 4) { const sv = SANTUARIOS[sub] || ['Santuário', '', '']; if (val < 0) { aviso(sv[0] + ' · deixado para trás'); anotar(`${sv[0]}: você mandou o grupo seguir sem tocar nele.`); } else { const novo = !(santuariosVistos & (1 << sub)); santuariosVistos |= 1 << sub; aviso(sv[0] + ' · ' + sv[2]); anotar(`<b style="color:#7fd8ff">${sv[0]}</b> — “${sv[1]}” — ${sv[2]}.${novo ? ' <b>Efeito anotado no Livro dos Santuários.</b>' : ''}`); brilho('#7fd8ff', 34); } }
  else if (ts === 5) { aviso('Alma libertada: ' + QUALIDADES[sub]); anotar(`Almas presas: uma alma <span class="q${sub}">${QUALIDADES[sub]}</span> se soltou${toque ? ' (melhorada pelo seu toque?)' : ''}.`); brilho('#78ffaa', 30); }
  else if (ts === 15) { if (sub === 2) { aviso('Os ovos chocaram!'); tremor = 0.3; anotar('<b style="color:#c8e06a">Ninho</b>: ninguém esmagou os ovos e as crias chocaram.'); brilho('#c8e06a', 26); } else { aviso('Ninho esmagado · +' + fmt(Math.round(val)) + ' Ossos'); anotar(`Ninho esmagado por você: +${fmt(Math.round(val))} Ossos.`); brilho('#e8e0a0', 30); for (let i = 0; i < 8; i++) mancha(x + rnd(-0.8, 0.8), y + rnd(-0.5, 0.5), 6, 3); } }
  else if (ts === 14) { aviso('Forja de ossos: couraça para todos'); anotar(`Forja de ossos: cada aliado sai com uma couraça de ${Math.round(val * 100)}% da vida${toque ? ' (reforçada pelo seu toque)' : ''}.`); brilho('#e8e0c8', 34); for (const g of estado.values()) if (g.viva && !g.inimigo) g.flash = 0.25; }
  else if (ts === 7) { aviso('Fonte de sangue: todos curados'); anotar(`Fonte: aliados recuperam ${Math.round(val * 100)}% da vida.`); brilho('#ff5060', 30); }
  else if (ts === 9) { if (sub === 1) { aviso('Mercador: item comprado por ' + fmt(Math.round(val)) + ' Ossos'); anotar(`Mercador de ossos: um item por ${fmt(Math.round(val))} Ossos${toque ? ' (com seu desconto)' : ''}.`); brilho('#ffd84a', 24); } else { aviso('Mercador: Ossos insuficientes'); anotar('Mercador de ossos: faltaram Ossos nesta expedição.'); } }
  else if (ts === 10) { aviso(toque ? 'Altar de sangue: você pagou o preço por eles' : 'Altar de sangue: vida por almas'); anotar(`Altar de sangue: +${Math.round(val * 100)}% de chance de alma neste andar${toque ? ', sem custo graças ao seu toque' : ', ao custo de 15% da vida de todos'}.`); brilho('#a01420', 40); }
  else if (ts === 13) { if (sub === 1) { aviso('Cofre aberto! +' + fmt(Math.round(val)) + ' Ossos e um item'); anotar(`<b style="color:#ffd84a">Cofre aberto</b> com a chave: +${fmt(Math.round(val))} Ossos e um item.`); brilho('#ffd84a', 60); clarao = 0.5; } else { if (objeto) objeto.aberto = 0; aviso('Cofre trancado: a chave estava em outra sala'); anotar('O cofre estava trancado. A chave ficava com os inimigos de outra sala: toque nela antes para mudar a rota.'); } }
  else if (ts === 12) { if (sub === 1) { aviso('Armadilha desarmada · +' + fmt(Math.round(val)) + ' Ossos'); anotar(`Armadilha desarmada por você: +${fmt(Math.round(val))} Ossos.`); } else { aviso('Armadilha! Todos sangram'); tremor = 0.25; anotar(`Armadilha de espinhos: todos perderam ${Math.round(val * 100)}% da vida. Tocando nela antes, você a desarma.`); } }
  else if (ts === 18) { aviso('Sala de tortura · +' + Math.round(val) + ' Sangue'); anotar(`Sala de tortura: o necromante colheu ${Math.round(val)} de Sangue${toque ? ' (um a mais pelo seu toque)' : ''}.`); brilho('#c8323c', 30); for (let i = 0; i < 6; i++) mancha(x + rnd(-1, 1), y + rnd(-0.6, 0.6), 7, 3); }
  else if (ts === 24) { aviso('Os Salões dos Cegos caíram · o Olho Óptico é seu'); anotar('<b style="color:#ffcf5a">Missão cumprida: os Salões dos Cegos.</b> O amuleto Olho Óptico foi para o inventário.'); brilho('#c9a0ff', 50); clarao = 0.4; ocultos.clear(); }
  else if (ts === 23) { if (val < 0) { aviso('A Câmara de Ossos ficou selada'); anotar('Você deixou a Câmara de Ossos selada. Ela continua no mesmo andar.'); } else { aviso('A Câmara de Ossos caiu · o livro é seu'); anotar('<b style="color:#ffcf5a">Missão cumprida: a Câmara de Ossos.</b> O livro que ela guardava é seu.'); brilho('#e6e0d0', 50); clarao = 0.4; } }
  else if (ts === 22) { aviso('A fonte corre limpa de novo · poções mais baratas'); anotar('<b style="color:#ffcf5a">Missão cumprida: a Fonte Envenenada está limpa.</b> A Curandeira agradece com poções mais baratas até o fim desta vida.'); brilho('#7fd8ff', 50); clarao = 0.4; }
  else if (ts === 21) { if (val < 0) { aviso('A tumba do Rei ficou fechada'); anotar('Você deixou a tumba do Rei Esqueleto fechada. Ele espera no andar 3.'); } else { aviso('O Rei Esqueleto caiu · Coroa do Rei Morto'); anotar('<b style="color:#ffcf5a">Missão cumprida: o Rei Esqueleto voltou ao pó.</b> A Coroa do Rei Morto é sua.'); som('chefe'); clarao = 0.6; } }
  else if (ts === 20) { if (val < 0) { aviso('A porta do Açougueiro ficou fechada'); anotar('Você deixou a porta do Açougueiro fechada. Ele espera no andar 2.'); } else { aviso('O Açougueiro caiu · Cutelo do Açougueiro'); anotar('<b style="color:#ffcf5a">Missão cumprida: o Açougueiro está morto.</b> O Cutelo do Açougueiro é seu.'); som('chefe'); clarao = 0.6; } }
  else if (ts === 19) { aviso('Arsenal · ' + TIPOS[sub] + ' ' + RARIDADES[Math.round(val)]); anotar(`Arsenal: nos suportes havia ${TIPOS[sub] ? TIPOS[sub].toLowerCase() : 'um item'} (${RARIDADES[Math.round(val)]})${toque ? ', escolhido por você' : ''}.`); brilho('#c8d0e0', 30); }
  else if (ts === 17) { aviso('Fosso limpo · item e +' + fmt(Math.round(val)) + ' Ossos'); anotar(`Fosso de monstros limpo: um item (ao menos Mágico) e +${fmt(Math.round(val))} Ossos.`); brilho('#c8e06a', 30); }
  else if (ts === 16) { aviso('Tomo lido · +' + fmt(Math.round(val)) + ' de experiência'); anotar(`Biblioteca: o necromante leu um tomo${toque ? ' escolhido por você' : ''}: +${fmt(Math.round(val))} de experiência.`); brilho('#b8a0ff', 30); }
  else if (ts === 6) { aviso('Emboscada vencida · +' + fmt(Math.round(val)) + ' Ossos'); anotar(`Emboscada vencida: +${fmt(Math.round(val))} Ossos${toque ? ' e um item' : ''}.`); }
  else if (ts === 8) { aviso((NOME_SECRETO[sub] || 'Nível secreto') + ': tesouro tomado!'); anotar(`${NOME_SECRETO[sub] || 'Nível secreto'} limpo: +${fmt(Math.round(val))} Ossos, ${sub === 2 ? '3 itens (com chance de Lendário)' : '2 itens (um Raro)'} e Sangue.`); clarao = 0.6; brilho('#ffd84a', 80); }
}
// ---- chefe do Ato 3: o Profeta Velado, design próprio — vestes cinza-azuladas, capuz fundo, máscara de bronze lisa e um livro aberto flutuando ----
function desenharLazarus(px, py, u, s, alfa) {
  const t = performance.now() / 1000, anim = String(s.anim || ''), conj = /Conjur/.test(anim), ataca = /Atac/.test(anim), anda = /And/.test(anim);
  const flut = Math.sin(t * 1.6) * 3, balanco = anda ? Math.sin(t * 6) * 3 : 0, lado = necro && necro.x > s.x ? 1 : -1;
  const P2 = (x, y) => [px + x * u * lado, py + y * u];
  const forma = (pts, cor, borda) => { ctx.beginPath(); pts.forEach(([x, y], k) => { const [qx, qy] = P2(x, y); if (k) ctx.lineTo(qx, qy); else ctx.moveTo(qx, qy); }); ctx.closePath(); if (cor) { ctx.fillStyle = cor; ctx.fill(); } if (borda) { ctx.strokeStyle = borda; ctx.stroke(); } };
  const curva = (a, c, b, cor, larg) => { const [ax, ay] = P2(a[0], a[1]), [cx, cy] = P2(c[0], c[1]), [bx, by] = P2(b[0], b[1]); ctx.strokeStyle = cor; ctx.lineWidth = larg * u; ctx.beginPath(); ctx.moveTo(ax, ay); ctx.quadraticCurveTo(cx, cy, bx, by); ctx.stroke(); };
  const brilho = (x, y, r, c0, c1) => { const [gx, gy] = P2(x, y), g = ctx.createRadialGradient(gx, gy, 0, gx, gy, r * u); g.addColorStop(0, c0); g.addColorStop(1, c1); const ant = ctx.globalCompositeOperation; ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = g; ctx.beginPath(); ctx.arc(gx, gy, r * u, 0, 7); ctx.fill(); ctx.globalCompositeOperation = ant; };
  ctx.save(); ctx.globalAlpha = alfa; ctx.lineJoin = 'round'; ctx.lineCap = 'round'; ctx.lineWidth = 2;
  if (alfa >= 1) { ctx.fillStyle = 'rgba(0,0,0,0.45)'; ctx.beginPath(); ctx.ellipse(px, py, 40 * u, 15 * u, 0, 0, 7); ctx.fill(); }
  const traco = '#0c1016';
  const gv = ctx.createLinearGradient(px, py - 160 * u, px, py); gv.addColorStop(0, '#5a6a7a'); gv.addColorStop(1, '#1e2630');
  // vestes em faixas rasgadas que não tocam o chão: ele flutua
  forma([[-20, -146 + flut], [20, -146 + flut], [34, -70 + flut], [44 + balanco, -14 + flut], [30, -22 + flut], [22, -8 + flut], [10, -20 + flut], [0, -6 + flut], [-12, -20 + flut], [-24, -8 + flut], [-32, -22 + flut], [-44 + balanco, -14 + flut], [-34, -70 + flut]], gv, traco);
  ctx.strokeStyle = 'rgba(10,14,20,0.6)'; ctx.lineWidth = 1.5 * u; for (const fx of [-16, 0, 16]) { const [a1, b1] = P2(fx * 0.6, -130 + flut), [a2, b2] = P2(fx * 1.6 + balanco, -18 + flut); ctx.beginPath(); ctx.moveTo(a1, b1); ctx.lineTo(a2, b2); ctx.stroke(); }
  for (let k = 0; k < 6; k++) { const [cx, cy] = P2(-8 + k * 3.5, -124 + k * 9 + flut); ctx.fillStyle = '#3a4048'; ctx.beginPath(); ctx.arc(cx, cy, 2.2 * u, 0, 7); ctx.fill(); }   // corrente de contas de ferro
  for (const l of [-1, 1]) {                                                                                                                         // braços magros em mangas longas
    const om = [l * 18, -140 + flut], mao = conj ? [l * 30, -196 + flut] : ataca ? [l * 46, -120 + flut] : [l * 24, -100 + flut], cot = [(om[0] + mao[0]) / 2 + l * 10, (om[1] + mao[1]) / 2 + 8];
    curva(om, cot, mao, '#2c3440', 12); curva(om, cot, mao, '#4a5868', 5);
    const [hx, hy] = P2(mao[0], mao[1]); ctx.fillStyle = '#8a8478'; ctx.beginPath(); ctx.arc(hx, hy, 4 * u, 0, 7); ctx.fill();
  }
  forma([[-17, -142 + flut], [-20, -170 + flut], [-12, -192 + flut], [4, -198 + flut], [16, -186 + flut], [20, -164 + flut], [17, -142 + flut]], '#3a4654', traco);   // capuz fundo
  forma([[-11, -150 + flut], [-12, -170 + flut], [-6, -182 + flut], [6, -182 + flut], [12, -170 + flut], [11, -150 + flut], [0, -144 + flut]], '#0a0c10', null); // sombra do capuz
  const gm = ctx.createLinearGradient(px - 9 * u, py - 178 * u, px + 9 * u, py - 150 * u); gm.addColorStop(0, '#d8a860'); gm.addColorStop(1, '#7a5420');
  forma([[-8, -152 + flut], [-9, -168 + flut], [-4, -176 + flut], [4, -176 + flut], [9, -168 + flut], [8, -152 + flut], [0, -147 + flut]], gm, '#3a2408');   // máscara de bronze lisa, sem rosto
  ctx.strokeStyle = 'rgba(40,24,8,0.8)'; ctx.lineWidth = 1.2 * u; for (const yy of [-168, -160]) { const [a1, b1] = P2(-6, yy + flut), [a2, b2] = P2(6, yy + flut); ctx.beginPath(); ctx.moveTo(a1, b1); ctx.lineTo(a2, b2); ctx.stroke(); }
  // o livro aberto que flutua diante dele e acende ao conjurar
  const lx = 30, ly = (conj ? -214 : -150) + Math.sin(t * 2.3) * 4;
  forma([[lx - 16, ly + 2], [lx, ly + 6], [lx, ly - 6], [lx - 15, ly - 10]], '#e8dcc0', '#3a2a18'); forma([[lx + 16, ly + 2], [lx, ly + 6], [lx, ly - 6], [lx + 15, ly - 10]], '#ddd0b0', '#3a2a18');
  ctx.strokeStyle = 'rgba(60,40,20,0.6)'; ctx.lineWidth = 1 * u; for (const k of [-1, 1]) for (const d of [3, 7, 11]) { const [a1, b1] = P2(lx + k * d, ly - 6 + d * 0.3), [a2, b2] = P2(lx + k * (d + 3), ly - 4 + d * 0.3); ctx.beginPath(); ctx.moveTo(a1, b1); ctx.lineTo(a2, b2); ctx.stroke(); }
  brilho(lx, ly - 4, conj ? 40 : 16, `rgba(170,255,170,${(conj ? 0.85 : 0.35) * alfa})`, 'rgba(40,160,60,0)');
  for (let k = 0; k < 3; k++) { const an = t * 1.3 + k * 2.1; brilho(lx + Math.cos(an) * 22, ly - 6 + Math.sin(an) * 10, 4, `rgba(200,255,190,${0.7 * alfa})`, 'rgba(60,200,80,0)'); }   // letras soltas que orbitam o livro
  ctx.restore(); ctx.lineWidth = 1;
}
// ---- chefe do Ato 4: o Colosso de Cinzas, design próprio — esqueleto gigante de ossos negros com brasas entre as costelas, coroa de chamas e um anel de crânios em órbita ----
function desenharDiablo(px, py, u, s, alfa) {
  const t = performance.now() / 1000, anim = String(s.anim || ''), conj = /Conjur/.test(anim), ataca = /Atac/.test(anim), anda = /And/.test(anim);
  const resp = Math.sin(t * 2) * 2, passo = anda ? Math.sin(t * 7) : 0, lado = necro && necro.x > s.x ? 1 : -1;
  const P2 = (x, y) => [px + x * u * lado, py + y * u];
  const osso = (a, b, larg, cor) => { const [ax, ay] = P2(a[0], a[1]), [bx, by] = P2(b[0], b[1]); ctx.strokeStyle = cor || '#2a2420'; ctx.lineWidth = larg * u; ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke(); ctx.strokeStyle = '#6a5a4a'; ctx.lineWidth = Math.max(1, larg * 0.3) * u; ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke(); };
  const brasa = (x, y, r, a) => { const [gx, gy] = P2(x, y), g = ctx.createRadialGradient(gx, gy, 0, gx, gy, r * u); g.addColorStop(0, `rgba(255,220,120,${a})`); g.addColorStop(0.45, `rgba(255,110,20,${a * 0.6})`); g.addColorStop(1, 'rgba(160,30,0,0)'); const ant = ctx.globalCompositeOperation; ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = g; ctx.beginPath(); ctx.arc(gx, gy, r * u, 0, 7); ctx.fill(); ctx.globalCompositeOperation = ant; };
  ctx.save(); ctx.globalAlpha = alfa; ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  if (alfa >= 1) { ctx.fillStyle = 'rgba(0,0,0,0.5)'; ctx.beginPath(); ctx.ellipse(px, py, 58 * u, 22 * u, 0, 0, 7); ctx.fill(); brasa(0, -4, 70, 0.25 + 0.08 * Math.sin(t * 3)); }
  // pernas e pés de ossos, juntas acesas
  for (const l of [-1, 1]) { const jx = l * 18 + l * passo * 4, jy = -48; osso([l * 12, -92], [jx, jy], 11); osso([jx, jy], [l * 20 - l * passo * 3, -4], 9); osso([l * 20 - l * passo * 3, -4], [l * 36, -2], 8); brasa(jx, jy, 9, 0.6); }
  osso([-24, -92], [24, -92], 12);                                                                                                                    // bacia
  osso([0, -92], [0, -170 + resp], 9);                                                                                                                // coluna
  for (let k = 0; k < 5; k++) { const y = -158 + resp + k * 12, w = 30 - k * 3; for (const l of [-1, 1]) { const [a1, b1] = P2(0, y), [c1, d1] = P2(l * w * 1.1, y - 4), [a2, b2] = P2(l * w, y + 10); ctx.strokeStyle = '#2a2420'; ctx.lineWidth = 6 * u; ctx.beginPath(); ctx.moveTo(a1, b1); ctx.quadraticCurveTo(c1, d1, a2, b2); ctx.stroke(); ctx.strokeStyle = '#6a5a4a'; ctx.lineWidth = 2 * u; ctx.stroke(); } }   // costelas
  brasa(0, -128 + resp, conj ? 46 : 30, 0.85 + 0.15 * Math.sin(t * 4));                                                                            // o fogo que mora dentro do peito
  osso([-40, -166 + resp], [40, -166 + resp], 10);                                                                                                    // clavículas
  for (const l of [-1, 1]) {                                                                                                                          // braços longos demais, até os joelhos
    const om = [l * 40, -166 + resp], cot = conj ? [l * 66, -206] : ataca && l === 1 ? [l * 78, -150] : [l * 58, -124], mao = conj ? [l * 52, -244 + 4 * Math.sin(t * 14)] : ataca && l === 1 ? [l * 108, -134] : [l * 54, -60 + resp];
    osso(om, cot, 10); osso(cot, mao, 8); brasa(cot[0], cot[1], 8, 0.55);
    for (const dq of [-6, 0, 6]) osso(mao, [mao[0] + l * 3 + dq, mao[1] + (conj ? -16 : 16)], 3, '#3a322a');
  }
  // crânio alongado, sem chifres, órbitas em brasa
  const [cx, cy] = P2(0, -190 + resp); ctx.fillStyle = '#3a322a'; ctx.strokeStyle = '#120e0a'; ctx.lineWidth = 2; ctx.beginPath(); ctx.ellipse(cx, cy, 17 * u, 22 * u, 0, 0, 7); ctx.fill(); ctx.stroke();
  ctx.fillStyle = '#2a2420'; ctx.beginPath(); ctx.ellipse(cx, cy + 18 * u, 11 * u, 7 * u, 0, 0, 7); ctx.fill();
  for (const l of [-1, 1]) brasa(l * 7, -192 + resp, 7, 0.95);
  // coroa de chamas
  for (let k = -2; k <= 2; k++) { const hx = k * 7, hh = 18 + 8 * Math.abs(Math.sin(t * 6 + k)); const [bx, by] = P2(hx, -208 + resp), [tx, ty] = P2(hx, -208 - hh + resp); const g = ctx.createLinearGradient(bx, by, tx, ty); g.addColorStop(0, 'rgba(255,140,30,0.95)'); g.addColorStop(1, 'rgba(255,230,150,0)'); ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(bx - 4 * u, by); ctx.quadraticCurveTo(bx, by - hh * 0.6 * u, tx, ty); ctx.quadraticCurveTo(bx, by - hh * 0.4 * u, bx + 4 * u, by); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  // anel de crânios em órbita (os da frente desenhados por cima)
  for (let k = 0; k < 6; k++) { const an = t * (conj ? 2.4 : 0.9) + k * Math.PI / 3, ox = Math.cos(an) * 62, oy = -130 + Math.sin(an) * 14 + resp; const [sx, sy] = P2(ox, oy), r = (5 + 2 * (Math.sin(an) + 1)) * u; ctx.globalAlpha = alfa * (0.55 + 0.45 * (Math.sin(an) + 1) / 2); ctx.fillStyle = '#d8ccb4'; ctx.beginPath(); ctx.arc(sx, sy, r, 0, 7); ctx.fill(); ctx.fillStyle = '#ff7a20'; ctx.beginPath(); ctx.arc(sx - r * 0.35, sy - r * 0.1, r * 0.25, 0, 7); ctx.arc(sx + r * 0.35, sy - r * 0.1, r * 0.25, 0, 7); ctx.fill(); }
  ctx.restore(); ctx.lineWidth = 1;
}
// ---- silhueta própria dos monstros que ainda usam um sprite emprestado: desenhada por trás (camada 0) e pela frente (camada 1) ----
const ADORNOS = { Sucubo: 1, Magistrado: 1, CavaleiroNegro: 1 };
function adorno(especie, camada, px, py, e, id) {
  const t = performance.now() / 1000 + id * 0.37;
  ctx.save(); ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  if (especie === 'Sucubo') {
    if (camada === 0) {          // asas de morcego abertas atrás dos ombros e a cauda
      const bate = Math.sin(t * 5) * 5 * e, oy = py - 74 * e;
      for (const l of [-1, 1]) {
        ctx.fillStyle = 'rgba(92,10,30,0.92)'; ctx.strokeStyle = '#1c0408'; ctx.lineWidth = 1.5;
        ctx.beginPath(); ctx.moveTo(px + l * 6 * e, oy); ctx.lineTo(px + l * 30 * e, oy - 30 * e - bate); ctx.lineTo(px + l * 50 * e, oy - 8 * e - bate * 0.6);
        ctx.quadraticCurveTo(px + l * 40 * e, oy + 2 * e, px + l * 42 * e, oy + 14 * e); ctx.quadraticCurveTo(px + l * 30 * e, oy + 6 * e, px + l * 27 * e, oy + 20 * e); ctx.quadraticCurveTo(px + l * 17 * e, oy + 8 * e, px + l * 10 * e, oy + 18 * e); ctx.closePath(); ctx.fill(); ctx.stroke();
        ctx.strokeStyle = 'rgba(190,50,80,0.7)'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(px + l * 30 * e, oy - 30 * e - bate); ctx.lineTo(px + l * 42 * e, oy + 14 * e); ctx.moveTo(px + l * 30 * e, oy - 30 * e - bate); ctx.lineTo(px + l * 27 * e, oy + 20 * e); ctx.stroke();
      }
      ctx.strokeStyle = '#5c0a1e'; ctx.lineWidth = 3 * e; ctx.beginPath(); ctx.moveTo(px - 4 * e, py - 22 * e); ctx.quadraticCurveTo(px - 30 * e, py - 6 * e + Math.sin(t * 3) * 3 * e, px - 34 * e, py - 26 * e); ctx.stroke();
      ctx.fillStyle = '#5c0a1e'; ctx.beginPath(); ctx.moveTo(px - 34 * e, py - 34 * e); ctx.lineTo(px - 39 * e, py - 24 * e); ctx.lineTo(px - 29 * e, py - 24 * e); ctx.fill();
    } else {                     // chifres curtos
      ctx.strokeStyle = '#e8d8c8'; ctx.lineWidth = 2.5 * e; const hy = py - 104 * e;
      for (const l of [-1, 1]) { ctx.beginPath(); ctx.moveTo(px + l * 5 * e, hy + 6 * e); ctx.quadraticCurveTo(px + l * 12 * e, hy + 2 * e, px + l * 10 * e, hy - 8 * e); ctx.stroke(); }
    }
  } else if (especie === 'Magistrado') {
    if (camada === 0) {          // halo roxo atrás da cabeça
      const hy = py - 100 * e, g = ctx.createRadialGradient(px, hy, 4 * e, px, hy, 26 * e); g.addColorStop(0, 'rgba(200,130,255,0.55)'); g.addColorStop(1, 'rgba(120,40,220,0)');
      ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = g; ctx.beginPath(); ctx.arc(px, hy, 26 * e, 0, 7); ctx.fill();
    } else {                     // capuz alto em ponta, aro do halo e as mãos acesas
      const hy = py - 102 * e;
      ctx.fillStyle = '#2a0f4a'; ctx.strokeStyle = '#12061f'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.moveTo(px - 10 * e, hy + 12 * e); ctx.quadraticCurveTo(px - 8 * e, hy - 8 * e, px + 2 * e, hy - 26 * e); ctx.quadraticCurveTo(px + 9 * e, hy - 6 * e, px + 10 * e, hy + 12 * e); ctx.closePath(); ctx.fill(); ctx.stroke();
      ctx.globalCompositeOperation = 'lighter'; ctx.strokeStyle = `rgba(210,150,255,${0.6 + 0.3 * Math.sin(t * 4)})`; ctx.lineWidth = 2 * e; ctx.beginPath(); ctx.ellipse(px, hy - 30 * e, 12 * e, 4 * e, 0, 0, 7); ctx.stroke();
      for (const l of [-1, 1]) { const mx = px + l * 17 * e, my = py - 56 * e + Math.sin(t * 6 + l) * 2 * e, g2 = ctx.createRadialGradient(mx, my, 0, mx, my, 11 * e); g2.addColorStop(0, 'rgba(240,210,255,0.95)'); g2.addColorStop(0.4, 'rgba(170,80,255,0.6)'); g2.addColorStop(1, 'rgba(120,40,220,0)'); ctx.fillStyle = g2; ctx.beginPath(); ctx.arc(mx, my, 11 * e, 0, 7); ctx.fill(); }
    }
  } else if (especie === 'Diablo') {
    const k = e / 3.1;          // o sprite emprestado é pequeno e vem ampliado: as medidas abaixo são em pixels do chefe
    if (camada === 0) {          // crista de espinhos nas costas e brasa no chão
      const g = ctx.createRadialGradient(px, py, 4, px, py, 120 * k); g.addColorStop(0, `rgba(255,90,20,${0.35 + 0.1 * Math.sin(t * 3)})`); g.addColorStop(1, 'rgba(160,20,0,0)');
      ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(px, py, 120 * k, 56 * k, 0, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over';
      ctx.fillStyle = '#3a0606'; ctx.strokeStyle = '#120202'; ctx.lineWidth = 2;
      for (let q = 0; q < 5; q++) { const bx = px - 46 * k + q * 6 * k, by = py - (150 - q * 22) * k; ctx.beginPath(); ctx.moveTo(bx + 16 * k, by + 14 * k); ctx.lineTo(bx - 22 * k, by - 12 * k); ctx.lineTo(bx + 18 * k, by - 8 * k); ctx.closePath(); ctx.fill(); ctx.stroke(); }
    } else {                     // dois chifres grandes curvos e os olhos acesos
      const hy = py - 196 * k;
      ctx.strokeStyle = '#e8dcc8'; ctx.lineWidth = 9 * k;
      for (const l of [-1, 1]) { ctx.beginPath(); ctx.moveTo(px + l * 16 * k, hy + 16 * k); ctx.quadraticCurveTo(px + l * 58 * k, hy + 8 * k, px + l * 50 * k, hy - 44 * k); ctx.stroke(); }
      ctx.strokeStyle = '#6a5a48'; ctx.lineWidth = 3 * k; for (const l of [-1, 1]) { ctx.beginPath(); ctx.moveTo(px + l * 20 * k, hy + 12 * k); ctx.quadraticCurveTo(px + l * 52 * k, hy + 6 * k, px + l * 48 * k, hy - 36 * k); ctx.stroke(); }
      ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = `rgba(255,220,90,${0.8 + 0.2 * Math.sin(t * 7)})`; for (const l of [-1, 1]) { ctx.beginPath(); ctx.ellipse(px + l * 8 * k, hy + 26 * k, 5 * k, 2.5 * k, 0, 0, 7); ctx.fill(); }
    }
  } else if (especie === 'Lazarus') {
    if (camada === 0) {          // cajado alto com um globo de sangue aceso na ponta
      ctx.strokeStyle = '#2a1608'; ctx.lineWidth = 4 * e; ctx.beginPath(); ctx.moveTo(px + 20 * e, py - 4 * e); ctx.lineTo(px + 24 * e, py - 128 * e); ctx.stroke();
      const gx = px + 24 * e, gy = py - 134 * e, g = ctx.createRadialGradient(gx, gy, 0, gx, gy, 16 * e); g.addColorStop(0, `rgba(255,200,180,${0.9 + 0.1 * Math.sin(t * 5)})`); g.addColorStop(0.4, 'rgba(220,30,30,0.7)'); g.addColorStop(1, 'rgba(160,0,0,0)');
      ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = g; ctx.beginPath(); ctx.arc(gx, gy, 16 * e, 0, 7); ctx.fill();
    } else {                     // mitra de arcebispo, vermelha com friso dourado, e estola
      const hy = py - 104 * e;
      ctx.fillStyle = '#7a0c12'; ctx.strokeStyle = '#d9b25f'; ctx.lineWidth = 1.5 * e;
      ctx.beginPath(); ctx.moveTo(px - 9 * e, hy + 10 * e); ctx.lineTo(px - 10 * e, hy - 8 * e); ctx.lineTo(px, hy - 30 * e); ctx.lineTo(px + 10 * e, hy - 8 * e); ctx.lineTo(px + 9 * e, hy + 10 * e); ctx.closePath(); ctx.fill(); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(px, hy - 28 * e); ctx.lineTo(px, hy + 9 * e); ctx.moveTo(px - 9 * e, hy + 3 * e); ctx.lineTo(px + 9 * e, hy + 3 * e); ctx.stroke();
      ctx.strokeStyle = 'rgba(217,178,95,0.85)'; ctx.lineWidth = 3 * e; for (const l of [-1, 1]) { ctx.beginPath(); ctx.moveTo(px + l * 6 * e, py - 84 * e); ctx.lineTo(px + l * 8 * e, py - 38 * e); ctx.stroke(); }
    }
  } else if (especie === 'CavaleiroNegro') {
    if (camada === 0) {          // espada larga às costas, atravessada
      ctx.strokeStyle = '#0c0c12'; ctx.lineWidth = 8 * e; ctx.beginPath(); ctx.moveTo(px + 14 * e, py - 46 * e); ctx.lineTo(px + 34 * e, py - 112 * e); ctx.stroke();
      ctx.strokeStyle = '#9aa0b0'; ctx.lineWidth = 5 * e; ctx.beginPath(); ctx.moveTo(px + 15 * e, py - 50 * e); ctx.lineTo(px + 33 * e, py - 109 * e); ctx.stroke();
      ctx.strokeStyle = '#5a4a2a'; ctx.lineWidth = 4 * e; ctx.beginPath(); ctx.moveTo(px + 8 * e, py - 50 * e); ctx.lineTo(px + 22 * e, py - 46 * e); ctx.stroke();
    } else {                     // elmo com chifres e viseira em brasa, ombreiras
      const hy = py - 100 * e;
      ctx.fillStyle = '#14141c'; ctx.strokeStyle = '#3a3a4a'; ctx.lineWidth = 1.5;
      for (const l of [-1, 1]) { ctx.beginPath(); ctx.ellipse(px + l * 15 * e, py - 80 * e, 10 * e, 7 * e, l * 0.3, 0, 7); ctx.fill(); ctx.stroke(); }
      ctx.beginPath(); ctx.moveTo(px - 9 * e, hy + 12 * e); ctx.lineTo(px - 9 * e, hy - 2 * e); ctx.quadraticCurveTo(px, hy - 12 * e, px + 9 * e, hy - 2 * e); ctx.lineTo(px + 9 * e, hy + 12 * e); ctx.closePath(); ctx.fill(); ctx.stroke();
      ctx.strokeStyle = '#8a8a9a'; ctx.lineWidth = 2.5 * e; for (const l of [-1, 1]) { ctx.beginPath(); ctx.moveTo(px + l * 8 * e, hy - 2 * e); ctx.quadraticCurveTo(px + l * 18 * e, hy - 6 * e, px + l * 17 * e, hy - 20 * e); ctx.stroke(); }
      ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = `rgba(255,70,30,${0.7 + 0.25 * Math.sin(t * 3)})`; ctx.fillRect(px - 6 * e, hy + 2 * e, 12 * e, 2.5 * e);
    }
  }
  ctx.restore(); ctx.lineWidth = 1;
}
function desenharObjeto(dt) {
  // perigos anunciados (lava): círculo que se fecha até estourar
  for (const pg of perigos) {
    pg.t += dt; const [px, py] = naTela(pg.x, pg.y), R = pg.r * arena.px_por_unidade_x, k = Math.min(1, pg.t / pg.dur);
    ctx.fillStyle = pg.pedra ? `rgba(0,0,0,${0.15 + 0.35 * k})` : `rgba(255,90,20,${0.12 + 0.25 * k})`; ctx.beginPath(); ctx.ellipse(px, py, R, R * 0.5, 0, 0, 7); ctx.fill();
    ctx.strokeStyle = `rgba(255,${200 - 120 * k | 0},40,0.9)`; ctx.lineWidth = 3; ctx.beginPath(); ctx.ellipse(px, py, R, R * 0.5, 0, 0, 7); ctx.stroke();
    ctx.beginPath(); ctx.ellipse(px, py, R * k, R * 0.5 * k, 0, 0, 7); ctx.stroke();
    if (Math.random() < 0.4) particulas.push(pg.pedra ? [pg.x + rnd(-pg.r, pg.r) * 0.6, pg.y + rnd(-pg.r, pg.r) * 0.4, 7, 0, 0, -9, '#8a8078', 0.7] : [pg.x + rnd(-pg.r, pg.r) * 0.6, pg.y + rnd(-pg.r, pg.r) * 0.4, 0.1, 0, 0, rnd(2, 5), '#ff8a30', rnd(0.3, 0.7)]);
  }
  perigos = perigos.filter((pg) => pg.t < pg.dur + 0.1);
  // o que espera nas salas ainda não visitadas: o baú, o santuário, o mercador... já estão lá, à vista
  mapa.forEach((m, i) => {
    const t = masm.tipos[i];
    if (!revelada(i)) return;
    if (i <= masm.sala || !(t === 3 || t === 4 || t === 5 || t === 7 || t === 9 || t === 10 || t === 12 || t === 13 || t === 14 || t === 15 || t === 16 || t === 18 || t === 19)) return;
    if (objeto && i === masm.sala) return;
    const o2 = offDaSala(i), [fx, fy] = W(o2[0] + 10, o2[1] + 6);
    pintarObjeto({ tipo: t, x: 10, y: 6, t: tempoSim + i, aberto: 0, tocado: false, futuro: true }, fx, fy);
  });
  if (!objeto) return;
  const o = objeto; o.t += dt; if (o.aberto) { o.aberto += dt; if (o.aberto > 1.6 && masm.fase !== 'Explorando') { objeto = null; return; } }
  { const [px, py] = naTela(o.x, o.y); pintarObjeto(o, px, py); }
}
// barril (0), barril explosivo (1), sarcófago (2), urna (3)
function pintarQuebravel(tipo, px, py, quebrado, sem) {
  ctx.save(); ctx.translate(px, py); ctx.scale(1.4, 1.4);
  ctx.fillStyle = 'rgba(0,0,0,0.35)'; ctx.beginPath(); ctx.ellipse(0, 2, tipo === 2 ? 30 : 15, tipo === 2 ? 11 : 6, 0, 0, 7); ctx.fill();
  if (quebrado) {
    // destroços: tábuas, cacos ou a tampa caída
    ctx.fillStyle = tipo === 2 ? '#5a5650' : tipo === 3 ? '#8a6a48' : '#4a3422';
    for (let q = 0; q < 5; q++) { const a0 = sem + q * 1.9, r = 6 + (q * 5) % 13; ctx.save(); ctx.translate(Math.cos(a0) * r, Math.sin(a0) * r * 0.45); ctx.rotate(a0); ctx.fillRect(-6, -1.5, 12, 3); ctx.restore(); }
    if (tipo === 1) { ctx.fillStyle = 'rgba(20,14,10,0.55)'; ctx.beginPath(); ctx.ellipse(0, 0, 26, 11, 0, 0, 7); ctx.fill(); }
    if (tipo === 2) { ctx.fillStyle = '#3a3834'; ctx.fillRect(-26, -10, 52, 10); ctx.fillStyle = '#0c0a0a'; ctx.fillRect(-22, -9, 44, 6); }
  } else if (tipo === 0 || tipo === 1) {
    const g = ctx.createLinearGradient(-12, 0, 12, 0); g.addColorStop(0, '#3a2616'); g.addColorStop(0.5, tipo === 1 ? '#8a3a22' : '#7a5632'); g.addColorStop(1, '#2c1c10');
    ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(-10, 0); ctx.quadraticCurveTo(-14, -15, -9, -30); ctx.lineTo(9, -30); ctx.quadraticCurveTo(14, -15, 10, 0); ctx.closePath(); ctx.fill();
    ctx.fillStyle = tipo === 1 ? '#5a2414' : '#5a4026'; ctx.beginPath(); ctx.ellipse(0, -30, 9, 3.5, 0, 0, 7); ctx.fill();
    ctx.strokeStyle = '#1a1410'; ctx.lineWidth = 2; for (const h of [-7, -22]) { ctx.beginPath(); ctx.moveTo(-12, h); ctx.quadraticCurveTo(0, h + 4, 12, h); ctx.stroke(); }
    if (tipo === 1) { ctx.fillStyle = `rgba(255,${120 + 60 * Math.sin(tempoSim * 5 + sem) | 0},40,0.9)`; ctx.beginPath(); ctx.arc(0, -34, 2.6, 0, 7); ctx.fill(); ctx.fillStyle = '#e8c070'; ctx.font = 'bold 11px Georgia'; ctx.textAlign = 'center'; ctx.fillText('✹', 0, -12); }
  } else if (tipo === 3) {
    const g = ctx.createLinearGradient(-9, 0, 9, 0); g.addColorStop(0, '#5a4028'); g.addColorStop(0.45, '#b08a5a'); g.addColorStop(1, '#3a2818');
    ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(-5, 0); ctx.quadraticCurveTo(-13, -10, -6, -20); ctx.lineTo(-4, -25); ctx.lineTo(4, -25); ctx.lineTo(6, -20); ctx.quadraticCurveTo(13, -10, 5, 0); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = '#2a1c10'; ctx.lineWidth = 1.2; ctx.stroke(); ctx.beginPath(); ctx.moveTo(-8, -14); ctx.lineTo(8, -14); ctx.stroke();
  } else {
    // sarcófago de pedra com tampa
    ctx.fillStyle = '#4a4844'; ctx.beginPath(); ctx.moveTo(-28, -2); ctx.lineTo(0, 10); ctx.lineTo(28, -2); ctx.lineTo(28, -16); ctx.lineTo(0, -4); ctx.lineTo(-28, -16); ctx.closePath(); ctx.fill();
    ctx.fillStyle = '#6a6862'; ctx.beginPath(); ctx.moveTo(-28, -16); ctx.lineTo(0, -28); ctx.lineTo(28, -16); ctx.lineTo(0, -4); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = '#22201e'; ctx.lineWidth = 1.5; ctx.stroke(); ctx.beginPath(); ctx.moveTo(0, -24); ctx.lineTo(0, -9); ctx.moveTo(-9, -18); ctx.lineTo(9, -18); ctx.stroke();
  }
  ctx.restore();
}
function pintarObjeto(o, px, py) {
  const ab = o.aberto > 0, pulso = 0.6 + 0.4 * Math.sin(o.t * 5);
  ctx.save(); ctx.translate(px, py);
  ctx.fillStyle = 'rgba(0,0,0,0.45)'; ctx.beginPath(); ctx.ellipse(0, 4, 34, 12, 0, 0, 7); ctx.fill();
  if (!ab && !o.futuro) { ctx.strokeStyle = `rgba(217,178,95,${0.4 + 0.5 * pulso})`; ctx.lineWidth = o.tocado ? 4 : 2; ctx.beginPath(); ctx.ellipse(0, 4, 44 + 4 * pulso, 16 + 2 * pulso, 0, 0, 7); ctx.stroke(); }
  if (o.tipo === 3) {           // baú
    ctx.fillStyle = '#4a2c16'; ctx.fillRect(-26, -30, 52, 32); ctx.strokeStyle = '#d9b25f'; ctx.lineWidth = 3; ctx.strokeRect(-26, -30, 52, 32);
    ctx.fillStyle = '#d9b25f'; ctx.fillRect(-4, -20, 8, 10);
    ctx.save(); ctx.translate(-26, -30); ctx.rotate(ab ? -1.1 : 0); ctx.fillStyle = '#5c3a1c'; ctx.fillRect(0, -16, 52, 16); ctx.strokeRect(0, -16, 52, 16); ctx.restore();
    if (ab) { ctx.globalCompositeOperation = 'lighter'; const g = ctx.createRadialGradient(0, -34, 0, 0, -34, 60); g.addColorStop(0, 'rgba(255,220,100,0.8)'); g.addColorStop(1, 'rgba(255,220,100,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, -34, 60, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 4) {    // santuário
    ctx.fillStyle = '#3a3a44'; ctx.beginPath(); ctx.moveTo(-16, 2); ctx.lineTo(-10, -74); ctx.lineTo(0, -88); ctx.lineTo(10, -74); ctx.lineTo(16, 2); ctx.closePath(); ctx.fill(); ctx.strokeStyle = '#1a1a22'; ctx.lineWidth = 2; ctx.stroke();
    ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = `rgba(110,200,255,${ab ? 1 : 0.4 + 0.4 * pulso})`; ctx.font = '26px Georgia'; ctx.textAlign = 'center'; ctx.fillText('✦', 0, -38);
    const g = ctx.createRadialGradient(0, -44, 0, 0, -44, ab ? 90 : 46); g.addColorStop(0, 'rgba(110,200,255,0.5)'); g.addColorStop(1, 'rgba(110,200,255,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, -44, 90, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over';
  } else if (o.tipo === 5) {    // almas presas
    ctx.globalCompositeOperation = 'lighter'; const g = ctx.createRadialGradient(0, -34, 0, 0, -34, 40); g.addColorStop(0, `rgba(120,255,170,${ab ? 0.2 : 0.7 * pulso})`); g.addColorStop(1, 'rgba(120,255,170,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, -34, 40, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over';
    ctx.strokeStyle = '#2a2a2e'; ctx.lineWidth = 4; for (let i = -2; i <= 2; i++) { ctx.beginPath(); ctx.moveTo(i * 9, 0); ctx.lineTo(i * 9 * (ab ? 1.8 : 1), -62); ctx.stroke(); }
    ctx.beginPath(); ctx.ellipse(0, 0, 22, 7, 0, 0, 7); ctx.stroke(); if (!ab) { ctx.beginPath(); ctx.ellipse(0, -62, 22, 7, 0, 0, 7); ctx.stroke(); }
  } else if (o.tipo === 13) {   // cofre: arca de ferro com cadeado; aberta, derrama ouro
    ctx.fillStyle = '#2c2c34'; ctx.fillRect(-30, -40, 60, 42); ctx.strokeStyle = '#8a8a96'; ctx.lineWidth = 3; ctx.strokeRect(-30, -40, 60, 42);
    for (const bx of [-18, 0, 18]) { ctx.fillStyle = '#5a5a66'; ctx.fillRect(bx - 2, -40, 4, 42); }
    if (!ab) { ctx.fillStyle = '#d9b25f'; ctx.fillRect(-7, -24, 14, 13); ctx.strokeStyle = '#d9b25f'; ctx.beginPath(); ctx.arc(0, -25, 6, Math.PI, 0); ctx.stroke(); }
    else { ctx.globalCompositeOperation = 'lighter'; const g = ctx.createRadialGradient(0, -40, 0, 0, -40, 70); g.addColorStop(0, 'rgba(255,220,100,0.8)'); g.addColorStop(1, 'rgba(255,220,100,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, -40, 70, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 12) {   // armadilha: placa de espinhos
    ctx.fillStyle = '#2a2428'; ctx.beginPath(); ctx.ellipse(0, 0, 46, 20, 0, 0, 7); ctx.fill(); ctx.strokeStyle = '#5a1a20'; ctx.lineWidth = 2; ctx.stroke();
    const sobe = ab ? Math.min(1, o.aberto * 6) : 0.35 + 0.15 * pulso;
    for (let i = -3; i <= 3; i++) for (let j = -1; j <= 1; j++) { const sx = i * 11 + j * 4, sy = j * 6; ctx.fillStyle = '#b8b0a0'; ctx.beginPath(); ctx.moveTo(sx - 3, sy); ctx.lineTo(sx + 3, sy); ctx.lineTo(sx, sy - 34 * sobe); ctx.closePath(); ctx.fill(); }
  } else if (o.tipo === 9) {    // mercador: vulto encapuzado com um saco
    ctx.fillStyle = '#2a2018'; ctx.beginPath(); ctx.moveTo(-18, 2); ctx.lineTo(-12, -52); ctx.quadraticCurveTo(0, -78, 12, -52); ctx.lineTo(18, 2); ctx.closePath(); ctx.fill(); ctx.strokeStyle = '#d9b25f'; ctx.lineWidth = 2; ctx.stroke();
    ctx.fillStyle = '#000'; ctx.beginPath(); ctx.ellipse(0, -52, 7, 9, 0, 0, 7); ctx.fill(); ctx.fillStyle = `rgba(255,216,74,${pulso})`; ctx.fillRect(-4, -54, 2.5, 2.5); ctx.fillRect(2, -54, 2.5, 2.5);
    ctx.fillStyle = '#5c4a2c'; ctx.beginPath(); ctx.ellipse(26, -8, 13, 12, 0, 0, 7); ctx.fill(); ctx.stroke();
  } else if (o.tipo === 10) {   // altar de sangue
    ctx.fillStyle = '#34282c'; ctx.fillRect(-30, -22, 60, 24); ctx.fillStyle = '#463a3e'; ctx.fillRect(-36, -30, 72, 10);
    ctx.fillStyle = `rgba(190,20,36,${0.7 + 0.3 * pulso})`; ctx.fillRect(-28, -30, 56, 4); ctx.fillRect(-20, -26, 5, 18); ctx.fillRect(8, -26, 4, 12);
    if (ab) { ctx.globalCompositeOperation = 'lighter'; const g = ctx.createRadialGradient(0, -36, 0, 0, -36, 70); g.addColorStop(0, 'rgba(255,40,60,0.6)'); g.addColorStop(1, 'rgba(255,40,60,0)'); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, -36, 70, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 18) {    // sala de tortura: trave de madeira com correntes, um corpo pendurado e o chão manchado
    ctx.fillStyle = 'rgba(110,10,14,0.55)'; ctx.beginPath(); ctx.ellipse(0, 2, 34, 12, 0, 0, 7); ctx.fill();
    ctx.fillStyle = '#2c1c12'; ctx.fillRect(-34, -84, 5, 86); ctx.fillRect(29, -84, 5, 86); ctx.fillRect(-36, -88, 72, 6);
    ctx.strokeStyle = '#7a7a80'; ctx.lineWidth = 1.5; ctx.setLineDash([3, 2]); for (const cx2 of [-16, 0, 16]) { ctx.beginPath(); ctx.moveTo(cx2, -82); ctx.lineTo(cx2 + Math.sin(o.t * 1.5 + cx2) * 1.5, cx2 === 0 ? -58 : -44); ctx.stroke(); } ctx.setLineDash([]);
    const bal = Math.sin(o.t * 1.5) * 1.5;
    ctx.fillStyle = '#8a8478'; ctx.beginPath(); ctx.arc(bal, -53, 5, 0, 7); ctx.fill(); ctx.fillRect(bal - 4, -48, 8, 20); ctx.fillRect(bal - 6, -46, 2.5, 14); ctx.fillRect(bal + 3.5, -46, 2.5, 14); ctx.fillRect(bal - 3.5, -28, 3, 16); ctx.fillRect(bal + 0.5, -28, 3, 16);
    ctx.fillStyle = '#5a0c10'; ctx.fillRect(bal - 4, -40, 8, 3);
    for (const hx of [-16, 16]) { ctx.strokeStyle = '#9a9aa0'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(hx, -40, 4, 0.3, 5.2); ctx.stroke(); }
    if (!ab) { ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = `rgba(200,30,40,${0.10 + 0.10 * pulso})`; ctx.beginPath(); ctx.arc(0, -40, 40, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 21) {    // tumba do Rei: sarcófago de pedra sobre um estrado, coroa entalhada e velas
    ctx.fillStyle = 'rgba(0,0,0,0.4)'; ctx.beginPath(); ctx.ellipse(0, 4, 58, 20, 0, 0, 7); ctx.fill();
    ctx.fillStyle = '#2a2a30'; ctx.beginPath(); ctx.moveTo(-52, 0); ctx.lineTo(0, 22); ctx.lineTo(52, 0); ctx.lineTo(0, -22); ctx.closePath(); ctx.fill();
    ctx.fillStyle = '#4a4a54'; ctx.beginPath(); ctx.moveTo(-40, -12); ctx.lineTo(0, 6); ctx.lineTo(40, -12); ctx.lineTo(40, -34); ctx.lineTo(0, -16); ctx.lineTo(-40, -34); ctx.closePath(); ctx.fill();
    ctx.fillStyle = '#6a6a76'; ctx.beginPath(); ctx.moveTo(-40, -34); ctx.lineTo(0, -16); ctx.lineTo(40, -34); ctx.lineTo(0, -52); ctx.closePath(); ctx.fill(); ctx.strokeStyle = '#1a1a20'; ctx.lineWidth = 1.5; ctx.stroke();
    ctx.fillStyle = '#d8b040'; ctx.beginPath(); ctx.moveTo(-10, -32); ctx.lineTo(-10, -42); ctx.lineTo(-5, -37); ctx.lineTo(0, -44); ctx.lineTo(5, -37); ctx.lineTo(10, -42); ctx.lineTo(10, -32); ctx.closePath(); ctx.fill();
    for (const vx of [-50, 50]) { ctx.fillStyle = '#d8d0b8'; ctx.fillRect(vx - 2, -18, 4, 14); ctx.fillStyle = `rgba(255,${170 + 40 * Math.sin(tempoSim * 9 + vx)},70,0.95)`; ctx.beginPath(); ctx.ellipse(vx, -22, 3, 6, 0, 0, 7); ctx.fill(); }
  } else if (o.tipo === 20) {    // porta do Açougueiro: batente de madeira manchado, ganchos e uma poça de sangue saindo por baixo
    ctx.fillStyle = 'rgba(120,8,14,0.7)'; ctx.beginPath(); ctx.ellipse(0, 6, 46, 16, 0, 0, 7); ctx.fill();
    ctx.fillStyle = '#1c1210'; ctx.fillRect(-30, -86, 60, 86); ctx.fillStyle = '#3a2416'; ctx.fillRect(-25, -80, 50, 80);
    ctx.strokeStyle = '#1a0f0a'; ctx.lineWidth = 2; for (const lx of [-12, 0, 12]) { ctx.beginPath(); ctx.moveTo(lx, -80); ctx.lineTo(lx, 0); ctx.stroke(); }
    ctx.fillStyle = '#6a6a70'; ctx.fillRect(-25, -62, 50, 5); ctx.fillRect(-25, -24, 50, 5);
    ctx.fillStyle = 'rgba(150,10,16,0.85)'; ctx.beginPath(); ctx.moveTo(-18, -70); ctx.lineTo(-6, -40); ctx.lineTo(-14, -8); ctx.lineTo(-22, -36); ctx.closePath(); ctx.fill(); ctx.beginPath(); ctx.ellipse(10, -46, 9, 13, 0.3, 0, 7); ctx.fill();
    ctx.strokeStyle = '#8a8a90'; ctx.lineWidth = 2; for (const hx of [-44, 44]) { ctx.beginPath(); ctx.moveTo(hx, -84); ctx.lineTo(hx, -56); ctx.arc(hx + 5, -56, 5, Math.PI, 0, true); ctx.stroke(); }
  } else if (o.tipo === 19) {    // arsenal: suporte de armas com lâminas e um escudo, e um manequim de armadura
    ctx.fillStyle = '#2c1c12'; ctx.fillRect(-36, -8, 44, 8); ctx.fillRect(-36, -50, 4, 44); ctx.fillRect(4, -50, 4, 44); ctx.fillRect(-36, -52, 44, 4);
    ctx.strokeStyle = '#b8c0cc'; ctx.lineWidth = 2.5; for (const lx of [-26, -16, -6]) { ctx.beginPath(); ctx.moveTo(lx, -6); ctx.lineTo(lx, -64 + (lx === -16 ? -8 : 0)); ctx.stroke(); ctx.strokeStyle = '#6a5030'; ctx.beginPath(); ctx.moveTo(lx - 4, -14); ctx.lineTo(lx + 4, -14); ctx.stroke(); ctx.strokeStyle = '#b8c0cc'; }
    ctx.fillStyle = '#5a3a22'; ctx.beginPath(); ctx.moveTo(-34, -30); ctx.quadraticCurveTo(-24, -34, -14, -30); ctx.lineTo(-14, -18); ctx.quadraticCurveTo(-24, -8, -34, -18); ctx.closePath(); ctx.fill(); ctx.strokeStyle = '#a8a090'; ctx.lineWidth = 1.2; ctx.stroke();
    ctx.fillStyle = '#3a2a1a'; ctx.fillRect(26, -40, 3, 40); ctx.fillRect(18, -2, 19, 4);
    if (!ab) { const ga = ctx.createLinearGradient(16, 0, 40, 0); ga.addColorStop(0, '#4a5058'); ga.addColorStop(0.5, '#a8b0bc'); ga.addColorStop(1, '#3a4048'); ctx.fillStyle = ga; ctx.beginPath(); ctx.moveTo(17, -44); ctx.lineTo(38, -44); ctx.lineTo(36, -18); ctx.lineTo(19, -18); ctx.closePath(); ctx.fill(); ctx.beginPath(); ctx.arc(27.5, -50, 6, 0, 7); ctx.fill(); ctx.fillStyle = '#14181c'; ctx.fillRect(24, -52, 7, 2);
      ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = `rgba(200,215,240,${0.10 + 0.12 * pulso})`; ctx.beginPath(); ctx.arc(27, -34, 26, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 16) {    // biblioteca: estante alta de livros entre duas velas, com um tomo aberto no atril
    ctx.fillStyle = '#2a1a10'; ctx.fillRect(-30, -74, 60, 74); ctx.fillStyle = '#3c2616'; ctx.fillRect(-33, -78, 66, 6); ctx.fillRect(-33, -2, 66, 5);
    for (let pr = 0; pr < 4; pr++) {
      const y0 = -70 + pr * 17; ctx.fillStyle = '#140c08'; ctx.fillRect(-27, y0, 54, 14);
      for (let lv = 0; lv < 9; lv++) { const hh = 9 + ((lv * 7 + pr * 5) % 5); ctx.fillStyle = ['#6a2a22', '#2a4a5a', '#5a4a22', '#3a2a5a', '#2a5a3a'][(lv + pr * 2) % 5]; ctx.fillRect(-26 + lv * 6, y0 + 14 - hh, 5, hh); }
    }
    for (const sx2 of [-44, 44]) { ctx.fillStyle = '#3a3028'; ctx.fillRect(sx2 - 2, -30, 4, 30); ctx.fillStyle = '#e8dcc0'; ctx.fillRect(sx2 - 2.5, -40, 5, 10); ctx.globalCompositeOperation = 'lighter'; const gv = ctx.createRadialGradient(sx2, -45, 0, sx2, -45, 26); gv.addColorStop(0, `rgba(255,200,110,${0.55 + 0.2 * pulso})`); gv.addColorStop(1, 'rgba(255,170,80,0)'); ctx.fillStyle = gv; ctx.beginPath(); ctx.arc(sx2, -45, 26, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
    if (!ab) { ctx.fillStyle = '#3c2616'; ctx.fillRect(-3, 6, 6, 16); ctx.fillStyle = '#e8dcc0'; ctx.beginPath(); ctx.moveTo(-14, 8); ctx.lineTo(0, 4); ctx.lineTo(14, 8); ctx.lineTo(14, 0); ctx.lineTo(0, -4); ctx.lineTo(-14, 0); ctx.closePath(); ctx.fill(); ctx.strokeStyle = '#4a3a2a'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(0, -4); ctx.lineTo(0, 4); ctx.stroke(); ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = `rgba(170,140,255,${0.15 + 0.15 * pulso})`; ctx.beginPath(); ctx.arc(0, 0, 22, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 15) {    // ninho: ovos pálidos numa cama de teia e ossos, pulsando
    ctx.fillStyle = 'rgba(30,24,20,0.9)'; ctx.beginPath(); ctx.ellipse(0, 0, 40, 15, 0, 0, 7); ctx.fill();
    ctx.strokeStyle = 'rgba(210,200,170,0.35)'; ctx.lineWidth = 1; for (let q = 0; q < 9; q++) { const a0 = q * 0.7; ctx.beginPath(); ctx.moveTo(Math.cos(a0) * 40, Math.sin(a0) * 15); ctx.lineTo(Math.cos(a0 + 2.4) * 34, Math.sin(a0 + 2.4) * 12); ctx.stroke(); }
    if (!ab) for (const [ex, ey, er, f] of [[-18, -6, 9, 0], [-2, -9, 11, 1.3], [15, -6, 9, 2.6], [-9, -1, 7, 3.9], [8, -1, 7, 5.1]]) {
      const p2 = 1 + 0.06 * Math.sin(o.t * 6 + f), g2 = ctx.createRadialGradient(ex - 2, ey - er, 1, ex, ey - er * 0.6, er * 1.3); g2.addColorStop(0, '#f4f0d0'); g2.addColorStop(0.7, '#a8b060'); g2.addColorStop(1, '#4a5a20');
      ctx.fillStyle = g2; ctx.beginPath(); ctx.ellipse(ex, ey - er * 0.7, er * 0.75 * p2, er * p2, 0, 0, 7); ctx.fill(); ctx.strokeStyle = '#1c2408'; ctx.lineWidth = 1; ctx.stroke();
    } else { ctx.fillStyle = '#c8cc90'; for (const [ex, ey] of [[-20, -3], [-6, -5], [10, -4], [22, -1], [0, 2]]) { ctx.beginPath(); ctx.moveTo(ex - 5, ey); ctx.lineTo(ex - 2, ey - 6); ctx.lineTo(ex + 1, ey - 2); ctx.lineTo(ex + 5, ey - 5); ctx.lineTo(ex + 6, ey); ctx.closePath(); ctx.fill(); } }
    if (!ab) { ctx.globalCompositeOperation = 'lighter'; const gn = ctx.createRadialGradient(0, -10, 0, 0, -10, 44); gn.addColorStop(0, `rgba(190,220,90,${0.18 + 0.14 * pulso})`); gn.addColorStop(1, 'rgba(190,220,90,0)'); ctx.fillStyle = gn; ctx.beginPath(); ctx.arc(0, -10, 44, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over'; }
  } else if (o.tipo === 14) {    // forja de ossos: bigorna de pedra, brasa e ossos empilhados
    ctx.fillStyle = '#4a4246'; ctx.fillRect(-22, -14, 44, 14); ctx.fillStyle = '#62585c'; ctx.fillRect(-28, -24, 56, 10); ctx.strokeStyle = '#0a0808'; ctx.lineWidth = 1.5; ctx.strokeRect(-28, -24, 56, 10); ctx.strokeRect(-22, -14, 44, 14); ctx.fillStyle = '#1c181a'; ctx.fillRect(-12, -4, 24, 6);
    ctx.fillStyle = '#d8d0b8'; for (const [bx, by, bw] of [[-40, -6, 18], [-38, -11, 14], [24, -5, 16], [27, -10, 12]]) { ctx.fillRect(bx, by, bw, 3); ctx.beginPath(); ctx.arc(bx, by + 1.5, 2.6, 0, 7); ctx.arc(bx + bw, by + 1.5, 2.6, 0, 7); ctx.fill(); }
    ctx.globalCompositeOperation = 'lighter'; const gf = ctx.createRadialGradient(0, -26, 0, 0, -26, 46); gf.addColorStop(0, `rgba(255,150,60,${0.45 + 0.25 * pulso})`); gf.addColorStop(1, 'rgba(255,120,40,0)'); ctx.fillStyle = gf; ctx.beginPath(); ctx.arc(0, -26, 46, 0, 7); ctx.fill(); ctx.globalCompositeOperation = 'source-over';
    ctx.fillStyle = `rgba(255,${150 + 60 * pulso | 0},70,0.95)`; ctx.fillRect(-18, -27, 36, 3);
    if (!o.futuro && Math.random() < 0.4) particulas.push([o.x + rnd(-0.3, 0.3), o.y, 0.7, rnd(-0.6, 0.6), 0, rnd(4, 8), '#ffb050', rnd(0.3, 0.7)]);
  } else if (o.tipo === 7) {    // fonte
    ctx.fillStyle = '#3a3438'; ctx.beginPath(); ctx.ellipse(0, -6, 36, 14, 0, 0, 7); ctx.fill(); ctx.fillStyle = '#2a2428'; ctx.fillRect(-7, -6, 14, 10);
    ctx.fillStyle = `rgba(170,20,32,${0.8 + 0.2 * pulso})`; ctx.beginPath(); ctx.ellipse(0, -8, 30, 10, 0, 0, 7); ctx.fill();
    if (!o.futuro && Math.random() < 0.5) particulas.push([o.x + rnd(-0.3, 0.3), o.y, 0.5, rnd(-0.4, 0.4), 0, rnd(3, 6), '#c8202c', rnd(0.4, 0.8)]);
  }
  ctx.restore();
}
try { dica = localStorage.getItem(CHAVE + '-dica') === '1'; } catch (e) { /* sem armazenamento */ }
function desenhar(dt) {
  if (mapaSujo && mapa.length) { mapaSujo = false; salaSuja = false; assar = -1; so = offDaSala(masm.sala); if (!reordenado) { limparArena(true); camSalto = true; } reconstruirMundo(reordenado); reordenado = false; }
  if (salaSuja) {
    salaSuja = false;
    // o sangue da sala que ficou para trás passa a fazer parte dela; a sala nova ganha o chão em detalhe
    if (assar >= 0 && salasCv[assar]) { const cv = salasCv[assar], c2 = cv.getContext('2d'); construirSala(assar, cv, 0.5); c2.setTransform(1, 0, 0, 1, 0, 0); c2.drawImage(sangue, 0, 0, cv.width, cv.height); }
    assar = -1; mortos.clear(); cadaverDe.clear(); sctx.clearRect(0, 0, CW, CH);
    so = offDaSala(masm.sala); construirSala();
  }
  const dvg = deltaDaViagem();
  const larg = tela.width, alt = tela.height, escPerto = alt / VISTA_H;
  const lim = limitesDoMundo(), escTudo = Math.min(escPerto, larg / (lim[2] - lim[0] + 160), alt * 0.7 / (lim[3] - lim[1] + 190));   // sobra espaço para os botões e avisos da base
  if (zoomLivre) zoomLivre = Math.max(escTudo * 0.7, Math.min(escPerto * 1.4, zoomLivre));
  const escSala = Math.max(escTudo, Math.min(escPerto * 0.8, Math.max(larg / (CW * 0.96), alt / (CH * 1.04))));   // modo Sala: a sala atual cabe na altura (ou na largura, em tela larga)
  const escAlvo = zoomLivre || (zoomModo === 0 ? escPerto : zoomModo === 2 ? escTudo : escSala);
  escAtual += (escAlvo - escAtual) * Math.min(1, dt * 4); if (Math.abs(escAlvo - escAtual) < 0.0005) escAtual = escAlvo;
  const esc = escAtual, visivel = larg / esc, visAlt = alt / esc, reforco = Math.max(1, Math.min(1.7, (escPerto * 0.5) / esc));   // no mapa inteiro as figuras crescem para continuarem legíveis
  {
    let sx = 0, sy = 0, n = 0;
    for (const s of estado.values()) if (s.viva) { const d = dvg && !s.inimigo ? dvg : null, [px, py] = naTela(s.rx + (d ? d[0] : 0), s.ry + (d ? d[1] : 0)), w = s.necro ? (zoomModo === 1 ? 1.5 : 3) : 1; sx += px * w; sy += py * w; n += w; }
    if (!dvg) { for (const o of orbes) { const [px, py] = naTela(o[0], o[1]); sx += px * 3; sy += py * 3; n += 3; } if (objeto) { const [px, py] = naTela(objeto.x, objeto.y); sx += px * 4; sy += py * 4; n += 4; } }
    const c0 = naTela(10, 6); let cx = n ? sx / n : c0[0], cy = n ? sy / n : c0[1];
    if (zoomModo === 2) { cx = (lim[0] + lim[2]) / 2; cy = (lim[1] + lim[3]) / 2 + visAlt * 0.085; }
    else if (zoomModo === 1 && !zoomLivre && mapa.length && !dvg) { const o = offDaSala(masm.sala), mA = W(o[0], o[1] + 6), mB = W(o[0] + 20, o[1] + 6), meio = (mA[0] + mB[0]) / 2, folga = Math.max(0, (CW * 0.96 - visivel) / 2); cx = Math.max(meio - folga, Math.min(meio + folga, cx)); cy = W(o[0] + 10, o[1] + 6)[1] - visAlt * 0.04; }   // modo Sala: a câmera fica presa à sala e só desliza de lado atrás da luta
    const eEl = $('encruzilhada'), painelEscolha = zoomModo !== 2 && ((!eEl.hidden && !eEl.classList.contains('min')) || !$('bencaos').hidden);   // com um painel de escolha aberto, a ação desce para continuar à vista
    const ax = cx + pan[0] - visivel / 2, ay = cy + pan[1] - visAlt * (painelEscolha ? 0.67 : 0.56), kc = Math.min(1, dt * (arrasto && arrasto.moveu ? 30 : 3));
    if (camForcar || (camSalto && zoomModo === 2 && !zoomLivre)) { camSalto = false; camForcar = false; camX = ax; camY = ay; escAtual = escAlvo; }   // andar novo: o mapa inteiro já aparece enquadrado
    else if (Math.abs(ax - camX) > visivel * 3) { camX = ax; camY = ay; } else { camX += (ax - camX) * kc; camY += (ay - camY) * kc; }
  }
  ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.fillStyle = '#050305'; ctx.fillRect(0, 0, larg, alt);
  const tx = tremor > 0 ? rnd(-1, 1) * tremor * 14 : 0, ty = tremor > 0 ? rnd(-1, 1) * tremor * 9 : 0; tremor = Math.max(0, tremor - dt);
  ctx.setTransform(esc, 0, 0, esc, (-camX + tx) * esc, (-camY + ty) * esc);
  // corredores: lajes entre as portas de salas vizinhas
  const pares = []; for (let i = 0; i < mapa.length; i++) for (let j2 = 0; j2 < i; j2++) if (vizinhas(i, j2)) pares.push([i, j2]);
  pares.forEach(([i, j2]) => {
    const a = offDaSala(i), b = offDaSala(j2), percorrido = i <= masm.sala && j2 <= masm.sala;
    let q;
    if (a[1] === b[1]) { const x0 = Math.min(a[0], b[0]) + 20.6, x1 = Math.max(a[0], b[0]) - 0.6; q = [[x0, a[1] + 3.6], [x1, a[1] + 3.6], [x1, a[1] + 8.4], [x0, a[1] + 8.4]]; }
    else { const y0 = Math.min(a[1], b[1]) + 12.6, y1 = Math.max(a[1], b[1]) - 0.6; q = [[a[0] + 7.6, y0], [a[0] + 12.4, y0], [a[0] + 12.4, y1], [a[0] + 7.6, y1]]; }
    const pt = q.map(([x, y]) => W(x, y));
    ctx.beginPath(); pt.forEach(([x, y], k) => (k ? ctx.lineTo(x, y) : ctx.moveTo(x, y))); ctx.closePath();
    ctx.fillStyle = percorrido ? '#3a3432' : '#2a2626'; ctx.fill(); ctx.strokeStyle = '#0a0808'; ctx.lineWidth = 3; ctx.stroke();
    if (percorrido) { const m0 = [(pt[0][0] + pt[3][0]) / 2, (pt[0][1] + pt[3][1]) / 2], m1 = [(pt[1][0] + pt[2][0]) / 2, (pt[1][1] + pt[2][1]) / 2]; ctx.strokeStyle = 'rgba(109,255,168,0.28)'; ctx.lineWidth = 5; ctx.beginPath(); ctx.moveTo(m0[0], m0[1]); ctx.lineTo(m1[0], m1[1]); ctx.stroke(); }
    // juntas das lajes
    ctx.lineWidth = 1; ctx.strokeStyle = 'rgba(0,0,0,0.45)';
    for (let f = 0.2; f < 1; f += 0.2) { const p0 = [pt[0][0] + (pt[1][0] - pt[0][0]) * f, pt[0][1] + (pt[1][1] - pt[0][1]) * f], p1 = [pt[3][0] + (pt[2][0] - pt[3][0]) * f, pt[3][1] + (pt[2][1] - pt[3][1]) * f]; ctx.beginPath(); ctx.moveTo(p0[0], p0[1]); ctx.lineTo(p1[0], p1[1]); ctx.stroke(); }
  });
  // o caminho até a próxima sala: setas douradas correndo pelos corredores
  if (mapa[masm.sala + 1] && !dvg) {
    const cam = caminhoNoMapa(masm.sala, masm.sala + 1);
    for (let k = 0; k + 1 < cam.length; k++) {
      const a = offDaSala(cam[k]), b = offDaSala(cam[k + 1]), dxs = Math.sign(b[0] - a[0]), dys = Math.sign(b[1] - a[1]);
      for (let q = 0; q < 3; q++) {
        const f = 0.36 + 0.28 * (((tempoSim * 0.9 + q / 3) % 1)), cx0 = a[0] + 10 + (b[0] - a[0]) * f, cy0 = a[1] + 6 + (b[1] - a[1]) * f;
        const p0 = W(cx0 + dxs * 0.9, cy0 + dys * 0.9), p1 = W(cx0 - dxs * 0.5 - dys * 0.9, cy0 - dys * 0.5 - dxs * 0.9), p2 = W(cx0 - dxs * 0.5 + dys * 0.9, cy0 - dys * 0.5 + dxs * 0.9);
        ctx.fillStyle = `rgba(255,216,74,${0.35 + 0.5 * Math.sin(Math.PI * ((tempoSim * 0.9 + q / 3) % 1))})`;
        ctx.beginPath(); ctx.moveTo(p0[0], p0[1]); ctx.lineTo(p1[0], p1[1]); ctx.lineTo(p2[0], p2[1]); ctx.closePath(); ctx.fill();
      }
    }
  }
  // salas, das do fundo para as da frente
  const ordem = mapa.map((m, i) => i).sort((a, b) => W(...offDaSala(a))[1] - W(...offDaSala(b))[1]);
  for (const i of ordem) {
    const o = offDaSala(i), [wx, wy] = W(o[0], o[1]);
    if (wx - OX > camX + visivel || wx - OX + CW < camX || wy - OY > camY + visAlt || wy - OY + CH < camY) continue;   // fora da tela
    if (i === masm.sala) { ctx.drawImage(chao, wx - OX, wy - OY); ctx.drawImage(sangue, wx - OX, wy - OY); pintarGrade(wx - OX, wy - OY, dt); }
    else if (salasCv[i]) { ctx.globalAlpha = !revelada(i) ? 0.24 : i > masm.sala ? 0.9 : 0.7; ctx.drawImage(salasCv[i], wx - OX, wy - OY, CW, CH); ctx.globalAlpha = 1; }
  }
  if (!mapa.length) { ctx.drawImage(chao, -OX, -OY); ctx.drawImage(sangue, -OX, -OY); }
  if (transicao > 0) transicao = Math.max(0, transicao - dt * 1.6);
  desenharObjeto(dt);

  for (const [x, y, r] of pocas) {
    const [px, py] = naTela(x, y);
    ctx.fillStyle = 'rgba(90,150,30,0.28)'; ctx.beginPath(); ctx.ellipse(px, py, r * arena.px_por_unidade_x, r * arena.px_por_unidade_y, 0, 0, 7); ctx.fill();
  }
  const lista = [];
  for (const s of mortos.values()) if (!s.over) lista.push([s, 'morrer', tempoSim - s.t, true]);
  for (const s of estado.values()) {
    const k = Math.min(1, dt * 14); s.rx += (s.x - s.rx) * k; s.ry += (s.y - s.ry) * k;
    if (!s.viva && mortos.has(s.id)) continue;
    if (!s.viva) continue;
    lista.push([s, dvg && !s.inimigo ? 'andar' : ACAO[s.anim] || 'parado', dvg && !s.inimigo ? tempoSim * 1.6 : tempoSim - s.t, false]);
  }
  lista.sort((a, b) => b[0].ry - a[0].ry);
  // a patrulha errante: um bando que anda pelo andar; se estiver na sala quando o grupo chegar, entra na luta
  if (patrulha && mapa[patrulha.sala]) {
    const o = offDaSala(patrulha.sala), destino = [o[0] + 15, o[1] + 3];
    if (!patPos || silencio) { patPos = destino.slice(); patRota = []; patAlvo = patrulha.sala; }
    if (patAlvo !== patrulha.sala || patGeo !== lerMapa.geo) {
      // mudou de sala: segue pelos corredores, de centro em centro, a partir da sala onde está agora
      patAlvo = patrulha.sala; patGeo = lerMapa.geo;
      let de = 0, md = 1e9; mapa.forEach((m, k) => { const c = offDaSala(k), d = Math.hypot(c[0] + 10 - patPos[0], c[1] + 6 - patPos[1]); if (d < md) { md = d; de = k; } });
      patRota = caminhoNoMapa(de, patrulha.sala).map((k) => { const c = offDaSala(k); return [c[0] + 10, c[1] + 6]; }); patRota.push(destino);
    }
    const alvoP = patRota.length ? patRota[0] : destino;
    const ddx = alvoP[0] - patPos[0], ddy = alvoP[1] - patPos[1], dist = Math.hypot(ddx, ddy), andando = dist > 0.3 || patRota.length > 1;
    if (dist > 0.01) { const passo = Math.min(dist, 11 * dt * Math.max(1, velocidade)); patPos[0] += ddx / dist * passo; patPos[1] += ddy / dist * passo; }
    if (dist <= 0.3 && patRota.length) patRota.shift();
    patrulha.especies.forEach((esp, q) => {
      const [px, py] = W(patPos[0] + (q - 1) * 1.3, patPos[1] + (q % 2) * 1.1), m = monstro(esp, false), e = reforco;
      const [img, sx, sy] = quadro(m, andando ? 'andar' : 'parado', andando ? direcao(ddx, ddy) : direcao(-1, 0), tempoSim * (andando ? 1.4 : 0.6) + q);
      ctx.strokeStyle = 'rgba(255,110,80,0.75)'; ctx.lineWidth = 2.5; ctx.beginPath(); ctx.ellipse(px, py, 22 * reforco, 9 * reforco, 0, 0, 7); ctx.stroke();
      ctx.drawImage(img, sx, sy, m.W, m.H, Math.round(px - m.pivo[0] * e), Math.round(py - (m.H - m.pivo[1]) * e), m.W * e, m.H * e);
    });
    const [tx2, ty2] = W(patPos[0], patPos[1]), kk = 1 / esc;
    ctx.textAlign = 'center'; ctx.font = `${11 * kk}px Georgia`; ctx.fillStyle = '#ff7a5a'; ctx.fillText('patrulha', tx2, ty2 - 100 * reforco - 6 * kk);
  } else if (!patrulha) patPos = null;
  // barris, urnas e sarcófagos de todas as salas (os quebrados viram destroços)
  for (const [sala, tipoQ, qx, qy, quebrado] of quebraveis) {
    if (!revelada(sala)) continue;
    const o = offDaSala(sala), [px, py] = W(o[0] + qx, o[1] + qy);
    if (px < camX - 200 || px > camX + visivel + 200 || py < camY - 200 || py > camY + visAlt + 200) continue;
    pintarQuebravel(tipoQ, px, py, quebrado, qx * 7 + qy * 13);
  }
  // inimigos à espera nas salas: parados, virados para a entrada, um pouco apagados até despertarem
  for (const [sala, especie, ix, iy, elite, ehChefe] of [...aEspera].sort((a, b) => b[3] - a[3] + (a[2] - b[2]))) {
    if (!revelada(sala)) continue;
    const o = offDaSala(sala), [px, py] = W(o[0] + ix, o[1] + iy), m = monstro(especie, elite), prov = visual(especie), e = (prov ? prov[1] : 1) * reforco;
    if (px < camX - 300 || px > camX + visivel + 300 || py < camY - 300 || py > camY + visAlt + 300) continue;
    // encaram o grupo, esteja ele onde estiver no andar: dá para ver que sabem que alguém vem
    const gx = so[0] + (necro ? necro.rx : 5) - (o[0] + ix), gy = so[1] + (necro ? necro.ry : 6) - (o[1] + iy);
    const [img, sx, sy] = quadro(m, 'parado', Math.abs(gx) + Math.abs(gy) > 0.5 ? direcao(gx, gy) : direcao(-1, 0), tempoSim * 0.6 + ix);
    if (elite || ehChefe) { ctx.strokeStyle = ehChefe ? 'rgba(255,50,60,0.6)' : 'rgba(255,190,60,0.6)'; ctx.lineWidth = 3; ctx.beginPath(); ctx.ellipse(px, py, (ehChefe ? 46 : 28) * reforco, (ehChefe ? 19 : 12) * reforco, 0, 0, 7); ctx.stroke(); }
    // raio de luz (Diablo 1): fora da luz do necromante o adormecido é só um vulto escuro
    const naLuz = sala === masm.sala && necro && Math.hypot(gx, gy) <= RAIO_DE_LUZ;
    ctx.globalAlpha = naLuz ? 0.95 : 0.9; ctx.filter = naLuz ? 'none' : 'brightness(0.16) saturate(0.4)';
    if (prov) {
      if (tinta.width !== m.W || tinta.height !== m.H) { tinta.width = m.W; tinta.height = m.H; }
      tctx.globalCompositeOperation = 'source-over'; tctx.clearRect(0, 0, m.W, m.H); tctx.drawImage(img, sx, sy, m.W, m.H, 0, 0, m.W, m.H);
      tctx.globalCompositeOperation = 'source-atop'; tctx.fillStyle = `rgba(${prov[2]},${prov[3]})`; tctx.fillRect(0, 0, m.W, m.H);
      ctx.drawImage(tinta, 0, 0, m.W, m.H, Math.round(px - m.pivo[0] * e), Math.round(py - (m.H - m.pivo[1]) * e), m.W * e, m.H * e);
    } else ctx.drawImage(img, sx, sy, m.W, m.H, Math.round(px - m.pivo[0] * e), Math.round(py - (m.H - m.pivo[1]) * e), m.W * e, m.H * e);
    ctx.globalAlpha = 1; ctx.filter = 'none';
  }
  if (viag && viag.dur && viag.total > 0.01) {      // porta emperrada: duas folhas de madeira fechadas na frente do grupo; tremem a cada pancada e somem quando cedem
    const eP = tempoSim - viag.t0, T1p = viag.T * Math.max(0.2, viag.ate / viag.total), fimP = T1p + viag.dur;
    if (eP < fimP + 0.35) {
      let dP = Math.min(viag.total, viag.ate + 0.9), kP = 0; while (kP < viag.seg.length - 1 && dP > viag.seg[kP]) { dP -= viag.seg[kP]; kP++; }
      const aP = viag.pts[kP], bP = viag.pts[kP + 1], fP = viag.seg[kP] > 0 ? dP / viag.seg[kP] : 1, cx = aP[0] + (bP[0] - aP[0]) * fP, cy = aP[1] + (bP[1] - aP[1]) * fP;
      const lP = Math.hypot(bP[0] - aP[0], bP[1] - aP[1]) || 1, nx = -(bP[1] - aP[1]) / lP, ny = (bP[0] - aP[0]) / lP;
      const bate = eP >= T1p && eP < fimP ? Math.abs(Math.sin((eP - T1p) * 7)) : 0, abre = eP > fimP ? (eP - fimP) / 0.35 : 0, hP = 74 * reforco;
      ctx.save(); ctx.globalAlpha = 1 - abre;
      for (const lado of [-1, 1]) {
        const larguraF = 1.35 * (1 - 0.6 * abre), [x0, y0] = W(cx + nx * lado * 0.05, cy + ny * lado * 0.05), [x1, y1] = W(cx + nx * lado * larguraF, cy + ny * lado * larguraF), tr = bate * 2.5 * lado;
        ctx.fillStyle = '#3a2414'; ctx.strokeStyle = '#120a06'; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.moveTo(x0 + tr, y0); ctx.lineTo(x1 + tr, y1); ctx.lineTo(x1 + tr, y1 - hP); ctx.lineTo(x0 + tr, y0 - hP); ctx.closePath(); ctx.fill(); ctx.stroke();
        ctx.strokeStyle = '#6a5a4a'; ctx.lineWidth = 3;
        for (const fr of [0.25, 0.72]) { ctx.beginPath(); ctx.moveTo(x0 + tr, y0 - hP * fr); ctx.lineTo(x1 + tr, y1 - hP * fr); ctx.stroke(); }
      }
      ctx.restore(); ctx.lineWidth = 1;
    }
  }
  if (masm.tipos && masm.tipos[masm.sala] === 24 && masm.fase === 'EmOnda' && necro) {     // Salões dos Cegos: a sala fica às escuras, só um círculo de luz em volta do necromante
    const [lx, ly] = naTela(necro.rx, necro.ry), rl = 170 * reforco, gE = ctx.createRadialGradient(lx, ly - 30 * reforco, rl * 0.5, lx, ly - 30 * reforco, rl * 1.6);
    gE.addColorStop(0, 'rgba(0,0,0,0)'); gE.addColorStop(1, 'rgba(0,0,6,0.76)'); ctx.fillStyle = gE; ctx.fillRect(lx - 6000, ly - 6000, 12000, 12000);
  }
  if (apocFx.length) {      // Apocalipse: anel de fogo que se abre no chão até o raio do golpe
    const agoraA = performance.now(); apocFx = apocFx.filter((a) => agoraA - a.t < 800);
    ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineJoin = 'round';
    for (const a of apocFx) {
      const fA = (agoraA - a.t) / 800, abre = Math.min(1, fA / 0.6), rA = a.r * (0.15 + 0.85 * abre), forcaA = 1 - Math.max(0, (fA - 0.6) / 0.4);
      const volta = (raio) => { ctx.beginPath(); for (let k = 0; k <= 40; k++) { const an = k / 40 * Math.PI * 2, [qx, qy] = W(a.p[0] + Math.cos(an) * raio, a.p[1] + Math.sin(an) * raio); if (k) ctx.lineTo(qx, qy); else ctx.moveTo(qx, qy); } ctx.closePath(); };
      volta(rA); ctx.fillStyle = `rgba(255,90,10,${0.16 * forcaA})`; ctx.fill();
      ctx.strokeStyle = `rgba(255,140,30,${0.85 * forcaA})`; ctx.lineWidth = 9 * reforco; ctx.stroke();
      ctx.strokeStyle = `rgba(255,240,180,${0.9 * forcaA})`; ctx.lineWidth = 3 * reforco; ctx.stroke();
      for (let k = 0; k < 14; k++) { const an = k / 14 * Math.PI * 2 + fA * 2, [qx, qy] = W(a.p[0] + Math.cos(an) * rA, a.p[1] + Math.sin(an) * rA), hf = (26 + 12 * Math.sin(agoraA / 90 + k)) * reforco * forcaA, gl = ctx.createRadialGradient(qx, qy - hf * 0.4, 1, qx, qy - hf * 0.4, hf); gl.addColorStop(0, `rgba(255,230,140,${0.8 * forcaA})`); gl.addColorStop(0.4, `rgba(255,120,20,${0.5 * forcaA})`); gl.addColorStop(1, 'rgba(160,20,0,0)'); ctx.fillStyle = gl; ctx.beginPath(); ctx.ellipse(qx, qy - hf * 0.45, 11 * reforco, hf, 0, 0, 7); ctx.fill(); }
    }
    ctx.restore(); ctx.lineWidth = 1;
  }
  if (teleFx.length) {      // teleporte: anel roxo que se abre onde o Magistrado some e onde reaparece
    const agoraT = performance.now(); teleFx = teleFx.filter((t) => agoraT - t.t < 600);
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    for (const t of teleFx) { const idT = agoraT - t.t; if (idT < 0) continue; const fT = idT / 600, [tx, ty] = W(t.p[0], t.p[1]), rT = (10 + 34 * fT) * reforco;
      ctx.strokeStyle = `rgba(190,110,255,${0.9 * (1 - fT)})`; ctx.lineWidth = 3 * reforco; ctx.beginPath(); ctx.ellipse(tx, ty, rT, rT * 0.5, 0, 0, 7); ctx.stroke();
      const gT = ctx.createRadialGradient(tx, ty - 24 * reforco, 0, tx, ty - 24 * reforco, 40 * reforco); gT.addColorStop(0, `rgba(210,150,255,${0.55 * (1 - fT)})`); gT.addColorStop(1, 'rgba(120,40,220,0)'); ctx.fillStyle = gT; ctx.beginPath(); ctx.arc(tx, ty - 24 * reforco, 40 * reforco, 0, 7); ctx.fill(); }
    ctx.restore(); ctx.lineWidth = 1;
  }
  if (raiosFx.length) {     // relâmpago em cadeia: cada salto é um ziguezague branco-azulado que dura um instante
    const agoraR = performance.now(); raiosFx = raiosFx.filter((r) => agoraR - r.t < 520);
    ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    for (const r of raiosFx) {
      const idR = agoraR - r.t; if (idR < 0) continue;
      const fR = 1 - idR / 520, [x0, y0] = W(r.a[0], r.a[1]), [x1, y1] = W(r.b[0], r.b[1]), alto = 26 * reforco, nR = 7, dxR = x1 - x0, dyR = y1 - y0, lR = Math.hypot(dxR, dyR) || 1;
      const pts = []; for (let k = 0; k <= nR; k++) { const f = k / nR, des = k === 0 || k === nR ? 0 : Math.sin(r.s + k * 12.9898 + Math.floor(idR / 60) * 7.1) * 11 * reforco; pts.push([x0 + dxR * f - dyR / lR * des, y0 - alto + dyR * f + dxR / lR * des]); }
      for (const [larg2, cor] of [[7 * reforco, `rgba(90,140,255,${0.35 * fR})`], [2.2 * reforco, `rgba(235,245,255,${0.95 * fR})`]]) { ctx.strokeStyle = cor; ctx.lineWidth = larg2; ctx.beginPath(); pts.forEach(([px, py], k) => { if (k) ctx.lineTo(px, py); else ctx.moveTo(px, py); }); ctx.stroke(); }
      const gR = ctx.createRadialGradient(x1, y1 - alto, 0, x1, y1 - alto, 26 * reforco); gR.addColorStop(0, `rgba(200,225,255,${0.6 * fR})`); gR.addColorStop(1, 'rgba(60,110,255,0)'); ctx.fillStyle = gR; ctx.beginPath(); ctx.arc(x1, y1 - alto, 26 * reforco, 0, 7); ctx.fill();
    }
    ctx.restore(); ctx.lineWidth = 1;
  }
  muralhasFx = muralhasFx.filter((mF) => tempoSim - mF.t0 >= 0 && tempoSim - mF.t0 <= mF.dur);
  for (const muralhaFx of muralhasFx) {
    const idadeM = tempoSim - muralhaFx.t0;
    {
      const forca = Math.min(1, idadeM / 0.35) * Math.min(1, (muralhaFx.dur - idadeM) / 0.6), N = 16, agora = performance.now() / 1000;
      ctx.save(); ctx.globalCompositeOperation = muralhaFx.ini ? 'source-over' : 'lighter';
      for (let k = 0; k <= N; k++) {
        const f = k / N, [fx, fy] = W(muralhaFx.a[0] + muralhaFx.d[0] * f + muralhaFx.so[0], muralhaFx.a[1] + muralhaFx.d[1] * f + muralhaFx.so[1]);
        const tremula = 0.75 + 0.25 * Math.sin(agora * 9 + k * 1.7), hf = (34 + 14 * Math.sin(agora * 6 + k * 2.3)) * reforco * forca, wf = 13 * reforco;
        const gl = ctx.createRadialGradient(fx, fy - hf * 0.35, 1, fx, fy - hf * 0.35, hf); gl.addColorStop(0, muralhaFx.ini ? `rgba(190,255,150,${0.95 * forca * tremula})` : `rgba(255,230,140,${0.75 * forca * tremula})`); gl.addColorStop(0.35, muralhaFx.ini ? `rgba(30,170,40,${0.8 * forca * tremula})` : `rgba(255,130,30,${0.5 * forca * tremula})`); gl.addColorStop(1, 'rgba(160,20,0,0)');
        ctx.fillStyle = gl; ctx.beginPath(); ctx.ellipse(fx, fy - hf * 0.45, wf, hf, 0, 0, 7); ctx.fill();
        ctx.fillStyle = muralhaFx.ini ? `rgba(40,200,60,${0.3 * forca})` : `rgba(255,120,20,${0.22 * forca})`; ctx.beginPath(); ctx.ellipse(fx, fy, wf * 1.5, wf * 0.7, 0, 0, 7); ctx.fill();
      }
      ctx.restore();
    }
  }
  if (portalFx) {
    const idade = (performance.now() - portalFx.t) / 1000;
    if (idade > 3.5) portalFx = null;
    else {
      const [px, py] = W(portalFx.x + portalFx.so[0], portalFx.y + portalFx.so[1]), abre = Math.min(1, idade / 0.5), some = idade > 2.8 ? Math.max(0, 1 - (idade - 2.8) / 0.7) : 1, rx = 30 * abre * reforco, ry = 58 * abre * reforco;
      ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = some;
      const gl = ctx.createRadialGradient(px, py - ry, 2, px, py - ry, ry * 1.9); gl.addColorStop(0, 'rgba(120,190,255,0.55)'); gl.addColorStop(1, 'rgba(40,90,255,0)'); ctx.fillStyle = gl; ctx.beginPath(); ctx.arc(px, py - ry, ry * 1.9, 0, 7); ctx.fill();
      ctx.globalCompositeOperation = 'source-over';
      ctx.fillStyle = 'rgba(20,60,160,0.85)'; ctx.beginPath(); ctx.ellipse(px, py - ry, rx, ry, 0, 0, 7); ctx.fill();
      ctx.strokeStyle = 'rgba(170,215,255,0.95)'; ctx.lineWidth = 3 * reforco; ctx.stroke();
      ctx.strokeStyle = 'rgba(120,180,255,0.6)'; ctx.lineWidth = 1.5;
      for (let k = 0; k < 3; k++) { ctx.beginPath(); ctx.ellipse(px, py - ry, rx * (0.75 - k * 0.2), ry * (0.75 - k * 0.2), idade * (2 + k), 0, 5); ctx.stroke(); }
      ctx.restore(); ctx.lineWidth = 1;
    }
  }
  for (const [s, acao, tempo, morto] of lista) {
    const m = s.necro && deFoice && monstros.necromante_foice ? monstros.necromante_foice : monstro(s.especie, s.elite), dvE = dvg && !s.inimigo && !morto ? (deltaDaViagem(s.necro ? 0.02 : 0.05 + 0.3 * ((s.id * 7) % 10) / 10, s.rx - 5, s.ry - 6) || [0, 0, 1, 0]) : null, cen = !s.inimigo && !morto ? cenaDoGrupo(s) : null, [px, py] = cen ? naTela(cen[0], cen[1]) : naTela(s.rx + (dvE ? dvE[0] : 0), s.ry + (dvE ? dvE[1] : 0));
    if (cen && !cen[2]) continue;      // já desceu a escada (ou ainda não entrou)
    if (reforco > 1.01) { ctx.save(); ctx.translate(px, py); ctx.scale(reforco, reforco); ctx.translate(-px, -py); }
    if (!morto) {
      // cor conta a força: aliados pela qualidade da alma; inimigos pelo perigo (comum sem anel, elite dourado, chefe vermelho)
      const corQ = ['160,160,160', '235,228,210', '110,168,255', '255,216,74', '255,77,106'];
      let cor = null, larg2 = 2, raio = 22;
      if (s.necro) cor = '110,255,170';
      else if (!s.inimigo) { cor = corQ[Math.max(0, s.qual)] || corQ[1]; larg2 = 2 + Math.max(0, s.qual) * 0.6; }
      else if (s.chefe) { cor = '255,50,60'; larg2 = 4; raio = 46; }
      else if (s.elite) { cor = '255,190,60'; larg2 = 3; raio = 28; }
      if (cor) {
        const pulso2 = (s.inimigo || s.qual >= 3) ? 0.55 + 0.25 * Math.sin(tempoSim * 4 + s.id) : 0.5;
        if (s.qual >= 2 || s.elite || s.chefe) { ctx.fillStyle = `rgba(${cor},${0.10 + (s.qual >= 3 || s.chefe ? 0.08 : 0)})`; ctx.beginPath(); ctx.ellipse(px, py, raio, raio * 0.42, 0, 0, 7); ctx.fill(); }
        ctx.strokeStyle = `rgba(${cor},${pulso2})`; ctx.lineWidth = larg2; ctx.beginPath(); ctx.ellipse(px, py, raio, raio * 0.42, 0, 0, 7); ctx.stroke();
      }
    }
    if (!morto && s.gelo > tempoSim) { ctx.fillStyle = 'rgba(140,200,255,0.35)'; ctx.beginPath(); ctx.ellipse(px, py - 30, 26, 44, 0, 0, 7); ctx.fill(); ctx.strokeStyle = 'rgba(200,235,255,0.8)'; ctx.lineWidth = 2; ctx.stroke(); }
    if (!morto && s.preso > tempoSim) { ctx.strokeStyle = 'rgba(170,110,255,0.9)'; ctx.lineWidth = 3; for (let q = 0; q < 3; q++) { ctx.beginPath(); ctx.ellipse(px, py - 14 - q * 22, 22 - q * 2, 8, 0, 0, 7); ctx.stroke(); } }
    if (!morto && s.golpe > 0) {
      const k = Math.min(1, s.golpe / 1.4), R = 2.3 * arena.px_por_unidade_x;
      ctx.fillStyle = `rgba(255,40,20,${0.10 + 0.12 * (1 - k)})`; ctx.beginPath(); ctx.ellipse(px, py, R, R * 0.5, 0, 0, 7); ctx.fill();
      ctx.strokeStyle = '#ff5a30'; ctx.lineWidth = 3; ctx.beginPath(); ctx.ellipse(px, py, R * (0.25 + 0.75 * k), R * 0.5 * (0.25 + 0.75 * k), 0, 0, 7); ctx.stroke();
    }
    if (!morto && s.id === marcado) {
      // Marca do Ceifador: mira vermelha girando aos pés do alvo
      ctx.save(); ctx.translate(px, py); ctx.scale(1, 0.42); ctx.rotate(tempoSim * 2.4);
      ctx.strokeStyle = '#ff3040'; ctx.lineWidth = 3; ctx.setLineDash([14, 9]); ctx.beginPath(); ctx.arc(0, 0, s.chefe ? 60 : 34, 0, 7); ctx.stroke(); ctx.restore();
    }
    if (!morto && !s.inimigo && !s.necro && poderes[2] > 0) { ctx.fillStyle = 'rgba(255,60,40,0.22)'; ctx.beginPath(); ctx.ellipse(px, py, 26, 11, 0, 0, 7); ctx.fill(); }
    const [img, sx, sy] = quadro(m, cen && !cen[5] ? 'andar' : acao, cen && !cen[5] ? direcao(cen[3], cen[4]) : dvE ? direcao(dvE[2], dvE[3]) : s.dir, cen && !cen[5] ? tempoSim * 1.4 : tempo);
    const prov = s.necro ? null : (masm.qualSecreto === 3 && s.inimigo ? [null, 1, '60,80,210', 0.5] : nomesUnicos.get(s.id) === 'O Açougueiro' ? [null, 1.6, '170,20,24', 0.36] : nomesUnicos.get(s.id) === 'Rei Esqueleto' ? [null, 1.55, '230,200,90', 0.22] : nomesUnicos.get(s.id) === 'Golem de Ossos' ? [null, 1.25, '120,230,170', 0.3] : s.campeao ? [null, 1.12, '70,130,255', 0.34] : visual(s.especie)), e = prov ? prov[1] : 1;   // Cripta dos Espelhos: os reflexos são sombras azuladas
    const dx0 = Math.round(px - m.pivo[0] * e), dy0 = Math.round(py - (m.H - m.pivo[1]) * e);
    const ocultoA = !morto && ocultos.has(s.id); if (ocultoA) { const tO = performance.now() / 1000 + s.id; ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.strokeStyle = `rgba(190,120,255,${0.55 + 0.3 * Math.sin(tO * 5)})`; ctx.lineWidth = 2; ctx.setLineDash([5, 4]); ctx.lineDashOffset = -tO * 20; ctx.beginPath(); ctx.ellipse(px, py, 26 * reforco, 11 * reforco, 0, 0, 7); ctx.stroke(); ctx.setLineDash([]); ctx.restore(); ctx.save(); ctx.globalAlpha = 0.3 + 0.08 * Math.sin(tO * 3); }
    if (!morto && ADORNOS[s.especie]) adorno(s.especie, 0, px, py, e, s.id);
    if (s.especie === 'Diablo' && !morto) desenharDiablo(px, py, reforco, s, 1);
    else if (s.especie === 'Lazarus' && !morto) desenharLazarus(px, py, reforco, s, 1);
    else if (prov && !(morto && COR_MORTE[s.cond])) {
      if (tinta.width !== m.W || tinta.height !== m.H) { tinta.width = m.W; tinta.height = m.H; }
      tctx.globalCompositeOperation = 'source-over'; tctx.clearRect(0, 0, m.W, m.H); tctx.drawImage(img, sx, sy, m.W, m.H, 0, 0, m.W, m.H);
      tctx.globalCompositeOperation = 'source-atop'; tctx.fillStyle = `rgba(${prov[2]},${prov[3]})`; tctx.fillRect(0, 0, m.W, m.H);
      ctx.drawImage(tinta, 0, 0, m.W, m.H, dx0, dy0, m.W * e, m.H * e);
    } else if (morto && COR_MORTE[s.cond]) {
      // cadáver tingido pela condição da morte (queimado, congelado, putrefato...)
      if (tinta.width !== m.W || tinta.height !== m.H) { tinta.width = m.W; tinta.height = m.H; }
      tctx.globalCompositeOperation = 'source-over'; tctx.clearRect(0, 0, m.W, m.H); tctx.drawImage(img, sx, sy, m.W, m.H, 0, 0, m.W, m.H);
      tctx.globalCompositeOperation = 'source-atop'; tctx.fillStyle = `rgba(${COR_MORTE[s.cond]},0.45)`; tctx.fillRect(0, 0, m.W, m.H);
      ctx.drawImage(tinta, 0, 0, m.W, m.H, dx0, dy0, m.W * e, m.H * e);
    } else ctx.drawImage(img, sx, sy, m.W, m.H, dx0, dy0, m.W * e, m.H * e);
    if (ocultoA) ctx.restore();
    if (!morto && ADORNOS[s.especie]) adorno(s.especie, 1, px, py, e, s.id);
    if (!morto && s.flash > 0 && s.especie === 'Lazarus') { s.flash -= dt; ctx.globalCompositeOperation = 'lighter'; desenharLazarus(px, py, reforco, s, Math.min(1, s.flash * 9) * 0.6); ctx.globalCompositeOperation = 'source-over'; }
    else if (!morto && s.flash > 0 && s.especie === 'Diablo') { s.flash -= dt; ctx.globalCompositeOperation = 'lighter'; desenharDiablo(px, py, reforco, s, Math.min(1, s.flash * 9) * 0.6); ctx.globalCompositeOperation = 'source-over'; }
    else if (!morto && s.flash > 0) {
      // clarão de impacto: o mesmo quadro somado por cima
      s.flash -= dt; ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = Math.min(1, s.flash * 9);
      ctx.drawImage(img, sx, sy, m.W, m.H, dx0, dy0, m.W * e, m.H * e); ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'source-over';
    }
    if (!morto && s.golpe > 0) {
      const tg = py - (s.chefe ? 190 : 146); ctx.font = 'bold 30px Georgia'; ctx.textAlign = 'center'; ctx.strokeStyle = '#000'; ctx.lineWidth = 4;
      ctx.fillStyle = Math.sin(tempoSim * 22) > 0 ? '#ffd84a' : '#ff5a30'; ctx.strokeText('!', px, tg); ctx.fillText('!', px, tg);
    }
    if (!morto && nomesUnicos.has(s.id)) {
      const nm = nomesUnicos.get(s.id), ty = py - 150 * (prov ? prov[1] : 1);
      if (unicoAnunciar === s.id) { unicoAnunciar = -1; aviso(nm); anotar(`<b style="color:#ffcf5a">${nm}</b> e seu séquito guardam esta sala.`); }
      const zc = Math.max(0.3, Math.min(1, ctx.getTransform().a / (window.devicePixelRatio || 1)));   // escala atual da câmera
      if (performance.now() < (aviso.ate || 0)) ctx.globalAlpha = 0.12;   // com o aviso central na tela, o nome some para os dois não se embolarem
      ctx.font = `bold ${Math.round(14 / zc)}px Georgia`; ctx.textAlign = 'center'; ctx.lineWidth = 3 / zc; ctx.strokeStyle = 'rgba(0,0,0,0.85)'; ctx.strokeText(nm, px, ty); ctx.fillStyle = '#ffcf5a'; ctx.fillText(nm, px, ty); ctx.globalAlpha = 1;   // legível também no mapa inteiro
    }
    if (morto && nomesUnicos.has(s.id)) { const nm = nomesUnicos.get(s.id); nomesUnicos.delete(s.id); aviso(nm + ' caiu!'); anotar(`<b style="color:#ffcf5a">${nm}</b> foi destruído e largou seu tesouro.`); clarao = 0.4; }
    if (!morto && s.id === marcado) {
      const topoM = py - (s.chefe ? 172 : 128), b2 = Math.sin(tempoSim * 6) * 3;
      ctx.fillStyle = '#ff3040'; ctx.beginPath(); ctx.moveTo(px, topoM + 12 + b2); ctx.lineTo(px - 8, topoM + b2); ctx.lineTo(px + 8, topoM + b2); ctx.fill();
    }
    if (!morto && (s.vida < 0.999 || s.escudo > 0.01)) {
      const w = s.chefe ? 70 : 30, topo = py - (s.chefe ? 150 : 108);
      ctx.fillStyle = 'rgba(0,0,0,0.8)'; ctx.fillRect(px - w / 2, topo, w, 4);
      ctx.fillStyle = s.inimigo ? (s.chefe ? '#ff3040' : s.elite ? '#ffb030' : s.campeao ? '#5a9cff' : '#be1414') : (s.necro ? '#6dffa8' : ['#9a9a9a', '#3cc878', '#6ea8ff', '#ffd84a', '#ff4d6a'][Math.max(0, s.qual)] || '#3cc878'); ctx.fillRect(px - w / 2, topo, w * Math.max(0, s.vida), 4);
      if (s.escudo > 0.01) { ctx.fillStyle = '#e8e0c8'; ctx.fillRect(px - w / 2, topo - 3, w * Math.min(1, s.escudo), 2); }   // couraça de ossos / escudo: faixa clara por cima da vida
    }
    if (reforco > 1.01) ctx.restore();
  }
  // rótulos das salas: o que há em cada uma, sempre do mesmo tamanho na tela
  // raio de luz: o grupo carrega a claridade; longe dele a masmorra escurece
  if (!masm.secreto) {
    const [lxc, lyc] = naTela(necro ? necro.rx : 5, necro ? necro.ry : 6), r0 = 330, r1 = 900;
    const gl = ctx.createRadialGradient(lxc, lyc - 40, r0, lxc, lyc - 40, r1); gl.addColorStop(0, 'rgba(0,0,0,0)'); gl.addColorStop(1, 'rgba(0,0,0,0.36)');
    ctx.fillStyle = gl; ctx.fillRect(camX - 50, camY - 50, visivel + 100, visAlt + 100);
  }
  if (mapa.length > 1) {
    ctx.textAlign = 'center';
    const porSala = {}; for (const a of aEspera) porSala[a[0]] = (porSala[a[0]] || 0) + 1;
    mapa.forEach((m, i) => {
      if (i === masm.sala && zoomModo !== 2) return;
      const o = offDaSala(i), [lx, ly0] = W(o[0] + 10, o[1] + 6), t = masm.tipos[i] || 0, k = 1 / esc, feita = i < masm.sala, ly = ly0 - (feita ? 0 : 62 * k + 40);   // acima do que há na sala, para não cobrir
      const oculta = !revelada(i), cor = oculta ? '#6a6068' : t === 2 ? '#ff5060' : t === 8 ? '#ff9ad0' : t >= 3 && t !== 11 ? '#d9b25f' : '#b8a890';
      ctx.globalAlpha = feita ? 0.4 : i === masm.sala ? 0.55 : oculta ? 0.7 : 0.95;
      if (i === masm.sala + 1) { ctx.strokeStyle = `rgba(255,216,74,${0.5 + 0.4 * Math.sin(tempoSim * 5)})`; ctx.lineWidth = 2.5 * k; ctx.beginPath(); ctx.arc(lx, ly - 4 * k, (20 + 3 * Math.sin(tempoSim * 5)) * k, 0, 7); ctx.stroke(); }
      ctx.fillStyle = 'rgba(8,5,7,0.8)'; ctx.beginPath(); ctx.arc(lx, ly - 4 * k, 15 * k, 0, 7); ctx.fill(); ctx.strokeStyle = cor; ctx.lineWidth = 1.5 * k; ctx.stroke();
      ctx.fillStyle = cor; ctx.font = `${18 * k}px Georgia`; ctx.fillText(feita ? '✓' : oculta ? '?' : SALAS[t][0], lx, ly + 2 * k);
      if (!feita && !oculta) {
        // o que a sala guarda: quantos inimigos esperam lá, ou o que o objeto dá — para a rota valer a escolha
        const n = porSala[i] || 0, nome = t === 0 || t === 1 || t === 2 ? (n ? `${n} inimigo${n > 1 ? 's' : ''}${t === 1 ? ' + campeão' : ''}` : '') : t === 11 ? '' : SALAS[t][1];
        if (nome) { ctx.font = `${11 * k}px Georgia`; ctx.fillStyle = '#d8c9b4'; ctx.fillText(t === 13 && chaveCofre[1] ? 'Cofre (você tem a chave)' : nome, lx, ly + 24 * k); }
        if (patrulha && patrulha.sala === i) { ctx.font = `${11 * k}px Georgia`; ctx.fillStyle = '#ff7a5a'; ctx.fillText('+ patrulha (' + patrulha.especies.length + ')', lx, ly + (i === chaveCofre[0] && !chaveCofre[1] ? 50 : 37) * k); }
        if (i === chaveCofre[0] && !chaveCofre[1]) { ctx.font = `${11 * k}px Georgia`; ctx.fillStyle = '#ffd84a'; ctx.fillText('guardam a chave do cofre', lx, ly + 37 * k); }
        if (DICA_SALA[t] && (larg >= 900 * (window.devicePixelRatio || 1) || i === masm.sala + 1)) { ctx.font = `${9.5 * k}px Georgia`; ctx.fillStyle = '#8fd89f'; ctx.fillText(DICA_SALA[t], lx, ly + 36 * k); }
      }
      ctx.globalAlpha = 1;
    });
  }

  // orbes por cima de tudo: são alvo de toque
  for (const [x, y] of orbes) {
    const [px, py] = naTela(x, y), b = Math.sin(tempoSim * 5 + x) * 4;
    const pulso = 1 + 0.25 * Math.sin(tempoSim * 8);
    for (const [r, a] of [[24 * pulso, 0.14], [13, 0.4], [6, 1]]) { ctx.fillStyle = `rgba(120,255,170,${a})`; ctx.beginPath(); ctx.arc(px, py - 30 + b, r, 0, 7); ctx.fill(); }
  }

  for (const [x, y, dx, dy, lanca] of projeteis) {
    const [px, py] = naTela(x, y), ddx = (dx + dy) * 11.3, ddy = (dx - dy) * 5.7;
    ctx.lineCap = 'round';
    if (lanca) {
      ctx.strokeStyle = 'rgba(120,255,160,0.45)'; ctx.lineWidth = 7; ctx.beginPath(); ctx.moveTo(px - ddx * 4, py - 52 - ddy * 4); ctx.lineTo(px + ddx * 1.5, py - 52 + ddy * 1.5); ctx.stroke();
      ctx.strokeStyle = '#f0ebd7'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(px - ddx * 2.5, py - 52 - ddy * 2.5); ctx.lineTo(px + ddx * 1.5, py - 52 + ddy * 1.5); ctx.stroke();
    } else {
      ctx.strokeStyle = '#e1d2aa'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(px - ddx, py - 52 - ddy); ctx.lineTo(px + ddx, py - 52 + ddy); ctx.stroke();
    }
  }
  for (const r of raios) {
    const [x1, y1] = naTela(r[2], r[3]), a = r[4] / 0.4;
    if (r[6]) {
      const [x0, y0] = naTela(r[0], r[1]); ctx.strokeStyle = `rgba(170,200,255,${r[4] / 0.18})`; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(x0, y0 - 30); ctx.lineTo((x0 + x1) / 2 + rnd(-8, 8), (y0 + y1) / 2 - 30 + rnd(-8, 8)); ctx.lineTo(x1, y1 - 10); ctx.stroke();
    } else if (r[5]) {
      const k = 1 - r[4] / 0.35;
      ctx.strokeStyle = `rgba(255,150,60,${1 - k})`; ctx.lineWidth = 5; ctx.beginPath();
      ctx.ellipse(x1, y1, r[5] * arena.px_por_unidade_x * k, r[5] * arena.px_por_unidade_y * k, 0, 0, 7); ctx.stroke();
    } else {
      const [x0, y0] = naTela(r[0], r[1]);
      ctx.strokeStyle = `rgba(120,255,160,${a})`; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(x0, y0 - 80); ctx.lineTo(x1, y1 - 40); ctx.stroke();
      ctx.fillStyle = `rgba(120,255,160,${a / 2})`; ctx.beginPath(); ctx.arc(x1, y1 - 40, 14, 0, 7); ctx.fill();
    }
    r[4] -= dt;
  }
  raios = raios.filter((r) => r[4] > 0);
  for (const p of particulas) {
    p[5] -= 14 * dt; p[0] += p[3] * dt; p[1] += p[4] * dt; p[2] += p[5] * dt; p[7] -= dt;
    if (p[2] <= 0) { if (p[6] === '#78080c' || p[6] === '#82080c' || p[6] === '#5a0408') mancha(p[0], p[1], 4, 1); p[7] = 0; continue; }
    const [px, py] = naTela(p[0], p[1]);
    ctx.fillStyle = p[6]; ctx.fillRect(px - 1.5, py - p[2] * arena.px_por_unidade_x * 0.6 - 1.5, 3, 3);
  }
  particulas = particulas.filter((p) => p[7] > 0);
  if (particulas.length > 900) particulas.splice(0, particulas.length - 900);
  // almas ceifadas voam até o necromante
  for (const a of almasVoando) {
    a.t += dt * 1.8; if (!necro) { a.t = 2; continue; }
    const [x0, y0] = naTela(a.x, a.y), [x1, y1] = naTela(necro.rx, necro.ry), k = Math.min(1, a.t), kk = k * k * (3 - 2 * k);
    const ax = x0 + (x1 - x0) * kk, ay = y0 - 30 + (y1 - 60 - (y0 - 30)) * kk - Math.sin(k * Math.PI) * 70;
    for (const [r, al] of [[14, 0.2], [7, 0.5], [3.5, 1]]) { ctx.fillStyle = `rgba(140,255,190,${al})`; ctx.beginPath(); ctx.arc(ax, ay, r, 0, 7); ctx.fill(); }
    if (Math.random() < 0.6) particulas.push([a.x + (necro.rx - a.x) * kk, a.y + (necro.ry - a.y) * kk, 1.2 + Math.sin(k * Math.PI), rnd(-0.5, 0.5), rnd(-0.4, 0.4), 0.5, '#78ffaa', 0.35]);
  }
  almasVoando = almasVoando.filter((a) => a.t < 1);
  ambiente(dt, larg, alt);
  if (clarao > 0) { ctx.fillStyle = `rgba(255,190,110,${clarao * 0.35})`; ctx.fillRect(0, 0, larg, alt); clarao = Math.max(0, clarao - dt * 2.2); }
  // penumbra: bordas escuras e luz de tocha tremendo
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  const tremula = 0.5 + 0.06 * Math.sin(tempoSim * 9) + 0.04 * Math.sin(tempoSim * 23.7);
  const grad = ctx.createRadialGradient(larg / 2, alt * 0.55, alt * 0.25, larg / 2, alt * 0.55, Math.max(larg, alt) * 0.75);
  grad.addColorStop(0, 'rgba(0,0,0,0)'); grad.addColorStop(1, `rgba(0,0,0,${tremula})`);
  ctx.fillStyle = grad; ctx.fillRect(0, 0, larg, alt);
  if (poderes[2] > 0) { ctx.fillStyle = `rgba(160,10,10,${0.08 + 0.04 * Math.sin(tempoSim * 12)})`; ctx.fillRect(0, 0, larg, alt); }
  // barra do chefe no alto da arena
  let chefe = null; for (const s of estado.values()) if (s.chefe && s.viva && s.inimigo) chefe = s;
  let campeao = null; if (!chefe) for (const s of estado.values()) if (s.elite && s.viva && s.inimigo && campeoes.has(s.id)) { campeao = s; break; }
  if (campeao) chefe = campeao;
  if (chefe) {
    const bw = Math.min(larg * 0.7, 520 * (larg / tela.clientWidth)), bx = (larg - bw) / 2, by = alt * 0.18, bh = Math.max(8, alt * 0.022);
    ctx.fillStyle = 'rgba(0,0,0,0.75)'; ctx.fillRect(bx - 2, by - 2, bw + 4, bh + 4);
    ctx.fillStyle = campeao ? '#8a6a14' : '#7a0c14'; ctx.fillRect(bx, by, bw * Math.max(0, chefe.vida), bh);
    ctx.strokeStyle = '#d9b25f'; ctx.lineWidth = 1; ctx.strokeRect(bx - 2, by - 2, bw + 4, bh + 4);
    ctx.fillStyle = '#e8d8b8'; ctx.textAlign = 'center'; ctx.font = `${Math.round(bh * 1.5)}px Georgia`; ctx.fillText(campeao ? campeoes.get(campeao.id).nome : nomeDoChefe(chefe.especie), larg / 2, by - bh * 0.6);
  }
  ctx.setTransform(esc, 0, 0, esc, (-camX + tx) * esc, (-camY + ty) * esc);
  ctx.textAlign = 'center';
  for (const n of numeros) {
    const [px, py] = naTela(n.x, n.y); n.t -= dt;
    ctx.globalAlpha = Math.min(1, n.t * 2.5); ctx.font = (n.crit ? 'bold 22px' : '15px') + ' Georgia';
    ctx.fillStyle = n.cor || (n.amigo ? '#ff6a6a' : (n.crit ? '#ffd84a' : '#f0e8d8')); if (n.txt) ctx.font = 'bold 14px Georgia';
    ctx.strokeStyle = '#000'; ctx.lineWidth = 3;
    const yy = py - 95 - (0.8 - n.t) * 45; ctx.textAlign = 'center'; ctx.strokeText(n.txt || n.v, px, yy); ctx.fillText(n.txt || n.v, px, yy);
  }
  ctx.globalAlpha = 1; numeros = numeros.filter((n) => n.t > 0);
  if (numeros.length > 60) numeros.splice(0, numeros.length - 60);
  const sq = `+${fmt(saque[0])} ossos · ${saque[1]} almas · ${saque[2]} itens`; if (sq !== desenhar.sq) { desenhar.sq = sq; $('t-saque').textContent = sq; }
  // encruzilhada: três pactos, um toque, tempo contado
  const enc = $('encruzilhada'), chave = oferta && pactos.length ? oferta.slice(0, 3).join(',') : '';
  if (chave !== ofertaMostrada) {
    ofertaMostrada = chave; enc.hidden = !chave; encAberta = false;
    if (chave) enc.innerHTML = '<div class="cab"><span>Encruzilhada · escolha um pacto</span><span id="enc-t"></span></div><div class="ops">' +
      oferta.slice(0, 3).map((id, i) => `<button data-o="${i}"><b>${escHtml(pactos[id].nome)}</b><span class="g">${escHtml(pactos[id].ganho)}</span><span class="p">${escHtml(pactos[id].preco)}</span></button>`).join('') + '</div>';
  }
  if (chave) { const t = $('enc-t'); if (t) t.textContent = Math.ceil(oferta[3]) + 's' + (chefe && !encAberta ? ' · toque para ver' : ''); }
  enc.classList.toggle('min', !!chave && !!chefe && !encAberta);   // em luta de chefe ou campeão, o pacto fica recolhido para não cobrir a arena
  // santuário: três bênçãos à escolha
  const chB = bencaos ? bencaos.slice(0, 3).join(',') : '';
  if (chB !== bencaosMostradas) {
    bencaosMostradas = chB; $('bencaos').hidden = !chB;
    if (chB && bencaos[0] === 13) {
      $('bencaos').innerHTML = `<div class="cab"><span>A Câmara de Ossos</span><span id="ben-t"></span></div><p class="frase">“Além do Salão dos Heróis jaz a Câmara de Ossos. Morte eterna aguarda quem lhe roubar os tesouros.”</p><p class="nota">Missão deste save. Atrás do selo há uma sala cheia de esqueletos, dois deles de elite. Limpa a sala, você leva um <b>livro de feitiço garantido</b> (um feitiço que você ainda não conhece, se houver) e ouro. Sem resposta, o necromante só rompe o selo depois de já ter concluído o andar 8.</p><div class="ops"><button data-b="0"><b>Romper o selo</b><span class="g">enfrentar a Câmara</span></button><button data-b="1"><b>Deixar selada</b><span class="g">ela continua aqui na próxima descida</span></button></div>`;
    } else if (chB && bencaos[0] === 12) {
      $('bencaos').innerHTML = `<div class="cab"><span>A tumba do Rei Esqueleto</span><span id="ben-t"></span></div><p class="frase">“O calor da vida entrou na minha tumba. Prepare-se, mortal, para servir ao meu senhor.”</p><p class="nota">Missão deste save. No sarcófago dorme o Rei Esqueleto: enquanto ele vive, mais esqueletos se erguem. Mate o Rei primeiro. Vencê-lo dá a <b>Coroa do Rei Morto</b> e ouro. Sem resposta, o necromante só abre depois de já ter concluído o andar 7.</p><div class="ops"><button data-b="0"><b>Abrir a tumba</b><span class="g">enfrentar o Rei Esqueleto</span></button><button data-b="1"><b>Seguir adiante</b><span class="p" style="color:var(--fraco)">ele continua aqui na próxima descida</span></button></div>`;
    } else if (chB && bencaos[0] === 11) {
      $('bencaos').innerHTML = `<div class="cab"><span>A porta do Açougueiro</span><span id="ben-t"></span></div><p class="frase">“Ah... carne fresca!”</p><p class="nota">Missão deste save. Atrás da porta, entre os corpos, está o Açougueiro: um só inimigo, muito forte e rápido. Vencê-lo dá o <b>Cutelo do Açougueiro</b> e ouro. Sem resposta, o necromante só abre depois de já ter concluído o andar 5.</p><div class="ops"><button data-b="0"><b>Abrir a porta</b><span class="g">enfrentar o Açougueiro</span></button><button data-b="1"><b>Seguir adiante</b><span class="p" style="color:var(--fraco)">ele continua aqui na próxima descida</span></button></div>`;
    } else if (chB) { const sv = SANTUARIOS[bencaos[0]] || ['Santuário', '', ''], conhecido = bencaos[1] === 1;
      $('bencaos').innerHTML = `<div class="cab"><span>${escHtml(sv[0])}</span><span id="ben-t"></span></div><p class="frase">“${escHtml(sv[1])}”</p><p class="nota">${conhecido ? 'Você já conhece este santuário: ' + escHtml(sv[2]) + '.' : 'Você ainda não sabe o que ele faz. Pode ajudar ou cobrar um preço.'} Sem resposta, o necromante usa.</p><div class="ops"><button data-b="0"><b>Usar o santuário</b><span class="g">${conhecido ? escHtml(sv[2]) : 'descobrir o efeito'}</span></button><button data-b="1"><b>Seguir adiante</b><span class="p" style="color:var(--fraco)">deixar o santuário intocado</span></button></div>`; }
  }
  if (chB) { const t = $('ben-t'); if (t) t.textContent = Math.ceil(bencaos[3]) + 's'; }
  const NOMES_RITO = ['', 'Rito da Pressa', 'Rito da Colheita', 'Rito do Banquete'];
  const tp = ativos.length && pactos.length ? ativos.map((id) => pactos[id].nome.replace('Pacto d', 'd').replace(/^d[aeo] /, '')).join(' · ') : '';
  const tp2 = [pressagio ? 'Presságio: ' + PRESSAGIOS[pressagio][0] : '', ritoEmCurso ? NOMES_RITO[ritoEmCurso] : '', tp ? 'Pactos: ' + tp : ''].filter(Boolean).join(' · ');
  if (tp2 !== desenhar.tp) { desenhar.tp = tp2; $('t-pactos').textContent = tp2; }
  const ts = sopro > 0 ? `Sopro ${Math.ceil(sopro)}s` : 'Sopro pronto'; if ($('b-sopro').textContent !== ts) $('b-sopro').textContent = ts;
  const NOMES_FI = ['', 'Raio Sombrio', 'Ceifada', 'Chuva de Ossos', 'Escudo de Almas'];
  for (let e = 0; e < 2; e++) {
    const bi = $('b-item' + e), f = itens[e][0]; bi.hidden = !f; if (!f) continue;
    const tx = itens[e][1] > 0 ? `${NOMES_FI[f]} ${Math.ceil(itens[e][1])}s` : NOMES_FI[f];
    if (bi.textContent !== tx) bi.textContent = tx; bi.disabled = itens[e][1] > 0;
  }
  const bd = $('b-detonar'), bf = $('b-frenesi');
  const td = poderes[0] > 0 ? `Detonar ${Math.ceil(poderes[0])}s` : `Detonar (${poderes[3]})`, tf = poderes[2] > 0 ? 'FRENESI!' : poderes[1] > 0 ? `Frenesi ${Math.ceil(poderes[1])}s` : 'Frenesi';
  if (bd.textContent !== td) bd.textContent = td; if (bf.textContent !== tf) bf.textContent = tf;
  bd.disabled = poderes[0] > 0 || poderes[3] === 0; bf.disabled = poderes[1] > 0; bf.classList.toggle('ligado', poderes[2] > 0);
  $('mana').firstElementChild.style.width = (mana * 100).toFixed(0) + '%';
  { const livre = !!(zoomLivre || pan[0] || pan[1]); if (livre !== desenhar.livre) { desenhar.livre = livre; pintarZoom(); } }
  // tira do grupo: retrato e vida de cada aliado; toque num ferido usa o Sopro Profano
  desenhar.tg = (desenhar.tg || 0) + dt;
  if (desenhar.tg > 0.25) {
    desenhar.tg = 0;
    {   // globos de vida e mana do necromante e o cinto de poções, como no painel do Diablo 1
      const vN = necro && necro.viva ? Math.max(0, Math.min(1, necro.vida)) : 0, mN = Math.max(0, Math.min(1, mana)), gv = $('g-vida'), gm = $('g-mana');
      gv.firstElementChild.style.height = Math.round(vN * 100) + '%'; gv.lastElementChild.textContent = Math.round(vN * 100) + '%'; gv.classList.toggle('baixo', vN > 0 && vN < 0.25);
      gm.firstElementChild.style.height = Math.round(mN * 100) + '%'; gm.lastElementChild.textContent = manaAbs[1] ? Math.round(manaAbs[0]) + '' : Math.round(mN * 100) + '%';
      const cx = cintoHud ? cintoHud[0] + ',' + cintoHud[1] + ',' + cintoMax : '';
      if (cx !== desenhar.cx) { desenhar.cx = cx; let hc = ''; if (cintoHud) { for (let k = 0; k < Math.min(6, cintoMax); k++) hc += `<span class="${k < cintoHud[0] ? 'v' : ''}"></span>`; for (let k = 0; k < Math.min(6, cintoMax); k++) hc += `<span class="${k < cintoHud[1] ? 'm' : ''}"></span>`; } const ce = $('cinto'); ce.style.gridTemplateColumns = `repeat(${Math.max(1, Math.min(6, cintoMax))}, 11px)`; ce.innerHTML = hc; ce.title = cintoHud ? `Cinto: ${cintoHud[0]} poções de vida e ${cintoHud[1]} de mana (bebe sozinho pela regra da Curandeira)` : ''; }
    }
    const corQ = ['#9a9a9a', '#e6e0d0', '#6fa8ff', '#ffd84a', '#ff4d6a'];
    let hg = '';
    const grupo = [...estado.values()].filter((s) => !s.inimigo).sort((a, b) => (b.necro ? 1 : 0) - (a.necro ? 1 : 0) || a.id - b.id).slice(0, 24);
    for (const s of grupo) {
      const v = s.viva ? Math.max(0, Math.min(1, s.vida)) : 0, cor = s.necro ? '#6dffa8' : corQ[Math.max(0, s.qual)] || corQ[1];
      hg += `<div class="ret ${s.viva ? '' : 'morto'} ${s.viva && v < 0.5 && sopro <= 0 ? 'cura' : ''}" data-ret="${s.id}" style="border-color:${cor}">${thumb(s.necro && deFoice && monstros.necromante_foice ? 'necromante_foice' : s.especie)}<i><b style="width:${Math.round(v * 100)}%;background:${v > 0.5 ? '#5a9f5a' : v > 0.25 ? '#c8a030' : '#c8323c'}"></b></i>${s.viva && s.escudo > 0.01 ? `<u style="width:${Math.round(Math.min(1, s.escudo) * 100)}%"></u>` : ''}</div>`;
    }
    if (hg !== desenhar.hg) { desenhar.hg = hg; $('grupo').innerHTML = hg; }
    atualizarLicao(); atualizarFicha();
  }
}

// ---- laço principal ----
let J = null, velocidade = 1, acumulado = 0, ultimo = 0, atraso = 0, silencio = false, saiuEm = 0;
const AUSENCIA_MAX = 8 * 3600;   // segundos de progresso ausente, no máximo
function ausencia(seg) { if (seg > 20) atraso += Math.round(Math.min(seg, AUSENCIA_MAX) * TPS); }
function laco(agora) {
  const dt = Math.min(0.1, (agora - ultimo) / 1000 || 0); ultimo = agora;
  if (atraso > 0) {
    // tempo ausente (aba fechada ou em segundo plano): a masmorra continuou; recupera em lotes sem travar a página
    const ini = performance.now(); let saida = '';
    while (atraso > 0 && performance.now() - ini < 14) { const n = Math.min(100, atraso); saida = J.Avancar(n); atraso -= n; tempoSim += n / TPS; }
    silencio = true; ler(saida); silencio = false; particulas = []; numeros = []; raios = [];
    const c = $('ausente'); c.style.display = atraso > 0 ? 'flex' : 'none';
    c.textContent = `Enquanto você esteve fora, a masmorra não parou… faltam ${Math.ceil(atraso / TPS / 60)} min`;
    if (atraso <= 0) { salvar(); atualizarPainel(true); }
  } else {
    acumulado += dt * velocidade * TPS;
    const ticks = Math.min(8, Math.floor(acumulado));
    if (ticks > 0) { acumulado -= ticks; tempoSim += ticks / TPS; ler(J.Avancar(ticks)); }
  }
  desenhar(dt * Math.max(velocidade, 0.0001));
  requestAnimationFrame(laco);
}

function ajustarTela() {
  const larg = $('cena').clientWidth, razao = Math.min(window.devicePixelRatio || 1, 2);
  const altFinal = larg < 700 ? Math.min(window.innerHeight * 0.5, 430) : Math.max(260, Math.min(larg * 0.62, window.innerHeight * 0.56));
  tela.width = Math.round(larg * razao); tela.height = Math.round(Math.max(200, altFinal) * razao);
  tela.style.height = Math.max(200, altFinal) + 'px';
}

function salvar() { try { localStorage.setItem(CHAVE, J.Salvar()); localStorage.setItem(CHAVE + '-hora', String(Date.now())); } catch (e) { /* sem armazenamento: joga-se sem salvar */ } }

// ---- painéis ----
const abasSel = { esq: 'exercito', dir: 'combate', baixo: 'necropole' }, LADO = { exercito: 'esq', equip: 'esq', combate: 'dir', masmorra: 'dir', bestiario: 'dir', necropole: 'baixo', talentos: 'baixo', ferreiro: 'baixo' };
const ALVO = { esq: 'p-esq', dir: 'p-dir', baixo: 'painel' }, htmlAnterior = { esq: '', dir: '', baixo: '' };
const botoesDeAba = () => document.querySelectorAll('.abas button[data-aba]');
let painelAnterior = '', missaoAnterior = '', recursosAnterior = '', exFiltro = 'todos', exOrdem = 'campo';
try { exFiltro = localStorage.getItem(CHAVE + '-filtro') || 'todos'; exOrdem = localStorage.getItem(CHAVE + '-ordem') || 'campo'; } catch (e) { /* padrão */ }
const fmt = (n) => n >= 1e6 ? (n / 1e6).toFixed(2) + 'M' : n >= 1e4 ? (n / 1e3).toFixed(1) + 'k' : String(n);
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const ESPACOS = ['Arma', 'Mão secundária', 'Elmo', 'Peitoral', 'Luvas', 'Botas', 'Amuleto', 'Anel', 'Anel'];
const TRACOS = { 0: ['Faminto', 'rouba vida a cada golpe'], 1: ['Frenético', 'ataca mais rápido'], 2: ['Pétreo', 'mais armadura'], 3: ['Rancoroso', 'explode ao morrer'],
  4: ['Leal', 'sofre menos dano perto do necromante'], 5: ['Incandescente', 'parte do dano vira fogo'], 11: ['Colosso', 'muito mais vida'], 12: ['Carniceiro', 'muito mais dano'], 13: ['Agourento', 'mais acertos críticos'] };
const donsHtml = (dn) => dn && dn.some((x) => x) ? '<small>Dons: ' + ['Vida', 'Dano', 'Velocidade'].map((n, k) => dn[k] ? `<span style="color:${dn[k] > 0 ? '#8fd89f' : '#e08a8a'}">${n} ${dn[k] > 0 ? '+' : ''}${dn[k]}%</span>` : '').filter(Boolean).join(' · ') + '</small>' : '';
// miniatura do sprite (quadro parado, de frente para a câmera) para as listas
function thumb(sprite) {
  const m = monstro(sprite, false), a = m.acoes.parado; if (!a) return '<div class="thumb"></div>';
  const k = 46 / Math.min(m.W, m.H) * 1.9, x = 23 - m.pivo[0] * k, y = 40 - (m.H - m.pivo[1]) * k;
  return `<div class="thumb" style="background-image:url(arte/${m.meta.nome}/${a.arquivo.replace('.png', '.webp')});background-size:${a.frames * m.W * k}px ${8 * m.H * k}px;background-position:${x}px ${y - m.H * k}px"></div>`;
}
const HABILIDADE = { cavaleironegro: ['Couraça', 'sofre 25% menos dano físico'], magistrado: ['Passo Sombrio', 'ferido abaixo de metade da vida, teleporta para trás do necromante (a cada 6 s)'], sucubo: ['Beijo de Sangue', '20% do dano de cada acerto cura o necromante'], esqueleto: ['Aparar', 'ignora o primeiro golpe que levar em cada sala'], zumbi: ['Miasma', 'ao cair, envenena os inimigos em volta'], caodecripta: ['Sede', 'ataca 25% mais rápido por 4 s depois de matar'], arqueiroossudo: ['Olho Morto', '+30% de dano em alvos com a vida ainda cheia'], diabrete: ['Matilha', '+8% de dano por outro Diabrete vivo no exército (até 3)'], acolitocaido: ['Agouro', 'o golpe enfraquece o alvo por 2,5 s (menos dano; não afeta chefes)'], carnical: ['Banquete', 'ao matar, devora o corpo e recupera 15% da vida'], diabodeguerra: ['Fúria de Guerra', '+25% de dano enquanto estiver abaixo de metade da vida'], aparicao: ['Incorpórea', 'um a cada 4 golpes simplesmente a atravessa'], colossodeossos: ['Baluarte', 'enquanto estiver de pé, os outros minions sofrem 10% menos dano'], resprofana: ['Investida', 'o primeiro golpe de cada sala causa dano dobrado'] };
const tracosHtml = (t) => t.length ? '<small>' + t.map((k) => TRACOS[k] ? `<b style="color:#c8a86a;font-weight:normal">${TRACOS[k][0]}</b>: ${TRACOS[k][1]}` : '').join(' · ') + '</small>' : '';
const QUALIDADES = ['Fraca', 'Comum', 'Vigorosa', 'Ancestral', 'Profana'];

function itemHtml(it) { return `<span class="r${it.r}">${esc(it.nome)}</span> <span class="nota">nv ${it.nivel}</span><small>${it.afixos.map(esc).join(' · ')}</small>`; }

function atualizarPainel(forcar) {
  const d = JSON.parse(J.Painel());
  if (d.vitoria && d.vitoria.pendente && $('vitoria').hidden) {
    const dfV = DIFICULDADES[d.vitoria.dif][0], prox = d.vitoria.dif < 2 ? DIFICULDADES[d.vitoria.dif + 1][0] : null;
    $('vit-texto').innerHTML = `Diablo foi destruído no andar 40 e a campanha <b>${dfV}</b> está vencida. Um item <b style="color:#ffcf5a">Único travado</b> foi para o seu inventário.` + (prox ? ` A masmorra não acaba aqui: o <b>${prox}</b> espera, com inimigos imunes e tesouros melhores, e ascender agora rende muitas Cinzas.` : ' Não há dificuldade acima desta: resta descer mais fundo e ascender.');
    $('vit-num').innerHTML = `<div>${fmt(d.vitoria.mortes)}<small>inimigos mortos</small></div><div>${d.vitoria.vidas}<small>${d.vitoria.vidas > 1 ? 'vidas' : 'vida'}</small></div><div>${d.vitoria.nivel}<small>nível</small></div>`;
    $('vitoria').hidden = false; som('bencao');
  }
  cintoHud = d.cinto ? [d.cinto.v, d.cinto.m] : null; cintoMax = d.cinto ? d.cinto.max : 3; $('b-portal').hidden = !(d.portal && d.portal.agora);
  pactos = d.pactos; ritoEmCurso = d.ritoEmCurso;
  $('t-andar').textContent = `Andar ${d.andar} · ${TEMAS_ANDAR[temaDoAndar(d.andar)][0]}${d.difAgora ? ' · ' + DIFICULDADES[d.difAgora][0] : ''}` + (d.recorde ? ` · rec. ${d.recorde}` : '');
  $('t-nivel').textContent = d.nivel; $('t-xp').style.width = Math.round(d.xp * 100) + '%';
  const IC = {
    ossos: '<path d="M5 5l14 14M4 7a2 2 0 1 1 3-3M17 20a2 2 0 1 0 3-3" stroke="#d8c9b4"/>', ess: '<path d="M12 3c4 5 6 8 6 11a6 6 0 0 1-12 0c0-3 2-6 6-11z" stroke="#6dffa8"/>',
    sangue: '<path d="M12 3c4 5 6 8 6 11a6 6 0 0 1-12 0c0-3 2-6 6-11z" fill="#a01420" stroke="#ff5060"/>', frag: '<path d="M12 3l5 7-5 11-5-11z" stroke="#9a6aff"/>',
    cmd: '<path d="M5 19V9l7-5 7 5v10M9 19v-6h6v6" stroke="#d9b25f"/>', cinzas: '<path d="M4 19h16M7 19c0-5 3-6 3-10M14 19c0-6 3-7 2-12" stroke="#aaa"/>',
  };
  const folga = d.poder / Math.max(1, d.poderSugerido), corP = folga >= 1 ? '#8fd89f' : folga >= 0.8 ? '#ffd84a' : '#ff6a6a';
  IC.poder = `<path d="M12 3l2.6 5.6 6.1.7-4.5 4.2 1.2 6-5.4-3-5.4 3 1.2-6L3.3 9.3l6.1-.7z" stroke="${corP}"/>`;
  const fichas = [['poder', `<span style="color:${corP}">${fmt(d.poder)}</span>`, `Poder · andar pede ${fmt(d.poderSugerido)}`], ['ossos', fmt(d.ossos), 'Ossos'], ['ess', fmt(d.essencia), 'Essência'], ['cmd', `${d.comando}/${d.comandoMax}`, 'Comando'], ['sangue', fmt(d.sangue), 'Sangue'], ['frag', fmt(d.fragmentos), 'Fragmentos'], ['ouro', `<span style="color:#ffd84a">${fmt(d.ouroTotal)}</span>`, d.ouroDaExpedicao ? `Ouro · +${fmt(d.ouroDaExpedicao)} nesta descida` : 'Ouro']];
  if (d.ascensao.cinzas || d.ascensao.vezes) fichas.push(['cinzas', fmt(d.ascensao.cinzas), 'Cinzas']);
  if (d.sorte > 0) fichas.push(['frag', fmt(d.sorte), 'Sorte']);
  if (d.cheats) fichas.push(['cmd', 'teste', 'save']);
  const hr = fichas.map(([k, v, n]) => `<span class="rec" data-rec="${k}"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${IC[k]}</svg><b>${v}</b><small>${n}</small></span>`).join('');
  if (hr !== recursosAnterior) { recursosAnterior = hr; $('recursos').innerHTML = hr; }
  // missão atual: objetivo sempre à vista
  const m = d.missao; let hm = '';
  if (!m) hm = '<div class="corpo"><span class="tit">Todas as missões cumpridas</span><div class="hist">O que foi roubado de ti continua mais abaixo.</div></div>';
  else {
    const premio = [m.ossos ? fmt(m.ossos) + ' Ossos' : '', m.essencia ? fmt(m.essencia) + ' Ess.' : ''].filter(Boolean).join(' + ');
    hm = `<div class="corpo"><span class="tit">Missão ${m.n}/${m.total} · ${esc(m.titulo)}</span><div class="hist">${esc(m.historia)}</div>${esc(m.objetivo)}` +
      (m.de > 1 ? ` <span class="nota">(${m.feito}/${m.de})</span>` : '') + `<div class="barra"><div style="width:${Math.round(100 * m.feito / m.de)}%"></div></div></div>` +
      (m.pronta ? `<button data-ac="resgatar" class="pronta">Resgatar<br><small>${premio}</small></button>`
        : `<span class="nota">Prêmio:<br>${premio}</span>`);
  }
  if (hm !== missaoAnterior) { missaoAnterior = hm; $('missao').innerHTML = hm; }
  const nomes = ['exercito', 'necropole', 'equip', 'bestiario', 'talentos', 'combate', 'masmorra'];
  d.abertos[5] = true; d.abertos[6] = true;
  d.abertos[3] = d.abertos[0];   // o Bestiário abre junto com o Exército
  d.abertos[4] = d.nivel >= 1 + d.niveisPorPonto || d.talentos.some((t) => t.pts > 0);   // Talentos abrem com o primeiro ponto
  const aberto = (n) => d.abertos[nomes.indexOf(n === 'ferreiro' ? 'equip' : n)];   // o Ferreiro abre junto com o Inventário
  // selos: quantas coisas dá para fazer em cada aba agora
  const pend = {
    exercito: d.almas.filter((a) => a.custo <= d.essencia).length + d.fusoes.filter((f) => f.pode).length + d.minions.filter((m) => (m.imbuir >= 0 && m.imbuir <= d.fragmentos) || (m.evoluir >= 0 && m.evoluir <= d.essencia)).length,
    necropole: d.construcoes.filter((c) => c.pode).length + d.ascensao.legados.filter((l) => l.pode).length, talentos: d.pontos + (d.atributos.auto ? 0 : d.atributos.livres),
  };
  for (const b of botoesDeAba()) {
    const n = b.dataset.aba, lado = b.parentElement.dataset.lado;
    b.hidden = !aberto(n);
    if (abasSel[lado] === n && !aberto(n)) { const prox = Object.keys(LADO).find((k) => LADO[k] === lado && aberto(k)); if (prox) abasSel[lado] = prox; }
    if (pend[n]) b.dataset.n = pend[n] > 9 ? '9+' : pend[n]; else delete b.dataset.n;
  }
  for (const b of botoesDeAba()) b.classList.toggle('ativo', abasSel[b.parentElement.dataset.lado] === b.dataset.aba);
  { const be = $('doca').querySelector('[data-abre="esq"]'); if (pend.exercito) be.dataset.n = pend.exercito > 9 ? '9+' : pend.exercito; else delete be.dataset.n; }
  invAberto = abasSel.esq === 'equip'; $('inventario').hidden = !invAberto; $('p-esq').hidden = invAberto;
  const render = (aba) => {
  let h = '';
  if (!aberto(aba)) h = '<p class="nota">Assista à descida e cumpra a primeira missão. O Exército, a Necrópole e o Inventário se abrem conforme você avança.</p>';
  else if (aba === 'exercito') {
    h += `<h3>Minions (${d.comando}/${d.comandoMax} de comando)</h3>`;
    if (!d.minions.length) h += '<p class="nota">Nenhum minion ainda. Ceife almas na masmorra e erga-as aqui.</p>';
    // filtro e ordem da lista (as ações continuam usando o índice original de cada minion)
    const papelDe = (m) => m.ficha.papel.split(':')[0];
    const FILTROS = [['todos', 'Todos'], ['campo', 'Em campo'], ['reserva', 'Na reserva'], ['f:Morto-vivo', 'Mortos-vivos'], ['f:Demônio', 'Demônios'], ['f:Besta', 'Bestas'], ['f:Aberração', 'Aberrações'], ['f:Espírito', 'Espíritos'],
      ['p:Tanque', 'Tanques'], ['p:Lutador', 'Lutadores'], ['p:Atirador', 'Atiradores'], ['p:Conjurador', 'Conjuradores'], ['p:Suporte', 'Suportes'], ['p:Enxame', 'Enxames'], ['q:2', 'Vigorosa ou melhor'], ['q:3', 'Ancestral ou melhor']];
    const ORDENS = [['poder', 'Maior Poder'], ['nivel', 'Maior nível'], ['qualidade', 'Melhor qualidade'], ['dano', 'Mais dano por segundo'], ['vida', 'Mais vida'], ['especie', 'Espécie'], ['campo', 'Em campo primeiro'], ['ordem', 'Ordem de chegada']];
    const passa = (m) => exFiltro === 'todos' || (exFiltro === 'campo' && m.ativo) || (exFiltro === 'reserva' && !m.ativo) || (exFiltro[1] === ':' && (
      (exFiltro[0] === 'f' && m.ficha.familia === exFiltro.slice(2)) || (exFiltro[0] === 'p' && papelDe(m) === exFiltro.slice(2)) || (exFiltro[0] === 'q' && m.q >= +exFiltro.slice(2))));
    const chave = { poder: (m) => -m.ficha.poder, nivel: (m) => -m.nivel, qualidade: (m) => -m.q * 1e9 - m.ficha.poder, dano: (m) => -m.ficha.dps, vida: (m) => -m.ficha.vida, campo: (m) => (m.ativo ? 0 : 1) * 1e12 - m.ficha.poder, ordem: () => 0 };
    const lista = d.minions.map((m, i) => [m, i]).filter(([m]) => passa(m));
    if (exOrdem === 'especie') lista.sort((a, b) => a[0].nome.localeCompare(b[0].nome) || b[0].ficha.poder - a[0].ficha.poder); else lista.sort((a, b) => chave[exOrdem](a[0]) - chave[exOrdem](b[0]) || a[1] - b[1]);
    const somaPoder = lista.reduce((t, [m]) => t + m.ficha.poder, 0);
    h += `<div class="linha"><select data-ex="filtro" style="max-width:none;flex:1">${FILTROS.map(([k, n]) => `<option value="${k}" ${k === exFiltro ? 'selected' : ''}>${n}</option>`).join('')}</select><select data-ex="ordem" style="max-width:none;flex:1">${ORDENS.map(([k, n]) => `<option value="${k}" ${k === exOrdem ? 'selected' : ''}>${n}</option>`).join('')}</select></div>`;
    h += `<p class="nota">${lista.length} de ${d.minions.length} minions · Poder somado ${fmt(somaPoder)}</p>`;
    lista.forEach(([m, i]) => {
      h += `<div class="linha">${thumb(m.ficha.sprite)}<div class="corpo toque" data-ficha="${i}" title="Ver a ficha"><span class="q${m.q}">${esc(m.nome)}</span> <span class="nota">nv ${m.nivel}/${m.max} · ${QUALIDADES[m.q]} · estágio ${m.estagio} · ${m.comando} de comando · <b style="color:var(--ouro);font-weight:normal">Poder ${fmt(m.ficha.poder)}</b></span>${donsHtml(m.ficha.dons)}${HABILIDADE[m.ficha.sprite] ? `<small><b style="color:#7fd6a0;font-weight:normal">${HABILIDADE[m.ficha.sprite][0]}</b>: ${HABILIDADE[m.ficha.sprite][1]}</small>` : ''}${tracosHtml(m.tracos)}` +
        `<div class="xp"><div style="width:${Math.round(m.xp * 100)}%"></div></div></div>` +
        (m.imbuir >= 0 ? `<button data-ac="imbuir" data-i="${i}" ${m.imbuir > d.fragmentos ? 'disabled' : ''} title="Sobe a qualidade da alma">Imbuir · ${fmt(m.imbuir)} Frag.</button>` : '') +
        (m.evoluir >= 0 ? `<button data-ac="evoluir" data-i="${i}" ${m.evoluir > d.essencia ? 'disabled' : ''}>Evoluir · ${fmt(m.evoluir)} Ess.</button>` : '') +
        `<button data-ac="alternar" data-i="${i}" class="${m.ativo ? 'ativo' : ''}">${m.ativo ? 'Em campo' : 'Na reserva'}</button>` +
        `<button data-ac="sacrificar" data-i="${i}" title="O minion some e vira Fragmentos de Alma">Sacrificar · +${fmt(m.frag)}</button></div>`;
    });
    const opq = (v) => QUALIDADES.map((q, k) => `<option value="${k}" ${k === v ? 'selected' : ''}>${k === 0 ? 'nenhuma' : 'abaixo de ' + q}</option>`).join('');
    h += `<h3>Postura do exército</h3><div class="linha"><div class="corpo">${['Agressiva', 'Defensiva', 'Escolta'][d.postura]}<small>${['Os minions caçam qualquer inimigo na arena', 'Só enfrentam quem chega perto do necromante', 'Ficam colados no necromante e o protegem'][d.postura]} · vale na próxima expedição</small></div><select data-postura="1">${['Agressiva', 'Defensiva', 'Escolta'].map((n, k) => `<option value="${k}" ${k === d.postura ? 'selected' : ''}>${n}</option>`).join('')}</select></div>`;
    h += `<h3>Regras automáticas</h3><div class="linha"><div class="corpo">Dissolver almas ao ceifar<small>viram Essência em vez de ocupar o Filactério (espécie nova nunca é dissolvida)</small></div><select data-regra="1">${opq(d.regras[1])}</select></div>` +
      `<div class="linha"><div class="corpo">Sacrificar minions da reserva<small>ao fim de cada expedição viram Fragmentos de Alma; quem está em campo nunca é tocado</small></div><select data-regra="2">${opq(d.regras[2])}</select></div>`;
    if (d.fusoes.length) {
      h += '<h3>Fusão de almas</h3><p class="nota">Três almas iguais viram uma de qualidade acima, que herda os traços delas. Um corpo melhor ocupa o mesmo Comando que um fraco.</p>';
      d.fusoes.forEach((f) => { h += `<div class="linha"><div class="corpo">3 × <span class="q${f.q}">${esc(f.nome)} ${QUALIDADES[f.q]}</span> → <span class="q${f.q + 1}">${QUALIDADES[f.q + 1]}</span><small>você tem ${f.n}</small></div><button data-ac="fundir" data-e="${f.e}" data-q="${f.q}" ${f.pode ? '' : 'disabled'}>Fundir · ${fmt(f.custo)} Ess.</button></div>`; });
    }
    h += `<h3>Filactério (${d.almas.length}/${d.filacterioMax} almas)</h3>`;
    if (!d.almas.length) h += '<p class="nota">Vazio. As almas ceifadas chegam ao fim de cada expedição.</p>';
    d.almas.map((a, i) => [a, i]).sort((x, y) => y[0].q - x[0].q || x[1] - y[1]).forEach(([a, i]) => { h += `<div class="linha">${thumb(a.sprite)}<div class="corpo"><span class="q${a.q}">${esc(a.nome)}</span> <span class="nota">${QUALIDADES[a.q]} · ${a.comando} de comando</span>${donsHtml(a.dons)}${tracosHtml(a.tracos)}</div><button data-ac="erguer" data-i="${i}" ${a.custo > d.essencia ? 'disabled' : ''}>Erguer · ${fmt(a.custo)} Ess.</button></div>`; });
    h += '<p class="nota">Mudanças no exército valem a partir da próxima expedição.</p><div class="rodape"><button data-ac="recomecar">Recomeçar expedição agora</button></div>';
  } else if (aba === 'ferreiro') {
    h += `<p class="nota">O Ferreiro compra o que você traz da masmorra e vende um estoque que muda a cada expedição. Você tem <b style="color:#ffd84a;font-weight:normal">${fmt(d.ouroTotal)} de ouro</b>${d.ouroDaExpedicao ? ` (+${fmt(d.ouroDaExpedicao)} chegam quando esta descida terminar)` : ''}.</p>`;
    // a Curandeira: poções para o cinto
    const ct = d.cinto;
    h += `<h3>Curandeira · poções</h3><p class="nota">O necromante bebe sozinho, pela regra que você escolher abaixo (uma poção a cada 6 s; a regra vale na próxima descida). O cinto leva ${ct.max} de cada. Barris quebrados às vezes rendem uma.</p>` +
      `<div class="linha"><div class="corpo"><span style="color:#ff7a7a">Poção de vida</span> <span class="nota">${ct.gv}/${ct.max} no cinto</span><small>devolve 45% da vida do necromante</small></div><button data-ac="limiar-pocao" data-i="0" title="Toque para mudar a regra">beber abaixo de ${ct.lv}%</button><button data-ac="pocao" data-i="0" ${ct.gv >= ct.max || ct.pv > d.ouroTotal ? 'disabled' : ''}>${ct.gv >= ct.max ? 'Cinto cheio' : fmt(ct.pv) + ' ouro'}</button></div>` +
      `<div class="linha"><div class="corpo"><span style="color:#7ab0ff">Poção de mana</span> <span class="nota">${ct.gm}/${ct.max} no cinto</span><small>devolve 40% da mana</small></div><button data-ac="limiar-pocao" data-i="1" title="Toque para mudar a regra">beber abaixo de ${ct.lm}%</button><button data-ac="pocao" data-i="1" ${ct.gm >= ct.max || ct.pm > d.ouroTotal ? 'disabled' : ''}>${ct.gm >= ct.max ? 'Cinto cheio' : fmt(ct.pm) + ' ouro'}</button></div>` +
      `<div class="linha"><div class="corpo">Repor o cinto ao voltar<small>depois de cada descida, compra as poções que faltam com o ouro que houver (depois do reparo)</small></div><button data-ac="repor-pocoes" class="${ct.repor ? 'ativo' : ''}">${ct.repor ? 'Ligado' : 'Desligado'}</button></div>`;
    // a Bruxa: livros de feitiço
    h += `<h3>Bruxa · livros de feitiço</h3><p class="nota">Dois livros por descida. Cada livro sobe um nível do feitiço; se faltar Magia para ler, fica guardado. É o jeito certo de aprender o <b>Golem</b>.</p>`;
    { const pt = d.portal; h += `<div class="linha"><div class="corpo"><span style="color:#7ab0ff">Pergaminho de Portal da Cidade</span> <span class="nota">${pt.n}/${pt.max}</span><small>sai da descida com todo o saque e a próxima começa no mesmo andar${pt.andar ? ` · <b style="color:#7ab0ff">portal aberto no andar ${pt.andar}</b>` : ''}</small></div><button data-ac="pergaminho" ${pt.n >= pt.max || pt.preco > d.ouroTotal ? 'disabled' : ''}>${pt.n >= pt.max ? 'Cheio' : fmt(pt.preco) + ' ouro'}</button></div>` +
      `<div class="linha"><div class="corpo">Abrir o portal sozinho<small>desligado por padrão: voltar sempre ao andar mais difícil rende menos do que recuar e juntar forças. Ligado, abre quando a derrota é certa: luta sem nenhum minion de pé, ou necromante abaixo de 20% sem poção de vida (do andar 3 em diante)</small></div><button data-ac="portal-auto" class="${pt.auto ? 'ativo' : ''}">${pt.auto ? 'Ligado' : 'Desligado'}</button></div>`; }
    if (!d.bruxa || !d.bruxa.length) h += '<p class="nota">A prateleira enche depois da próxima descida.</p>';
    (d.bruxa || []).forEach((l, i) => { h += l ? `<div class="linha"><div class="corpo"><span style="color:#9fd0ff">Livro de ${esc(l.nome)}</span> <span class="nota">${l.nivel ? 'você tem nível ' + l.nivel : 'feitiço novo'}</span><small>${l.magia > d.atributos.v[1] ? 'pede Magia ' + l.magia + ' (você tem ' + d.atributos.v[1] + '): ficará guardado' : 'você já pode ler'}</small></div><button data-ac="livro" data-i="${i}" ${l.preco > d.ouroTotal ? 'disabled title="Falta ouro"' : ''}>${fmt(l.preco)} ouro</button></div>` : '<div class="linha"><div class="corpo"><span class="nota">Vendido</span></div></div>'; });
    h += '<h3>Ferreiro · à venda</h3>';
    if (!d.loja.length) h += '<p class="nota">Estoque vendido. Volta cheio depois da próxima expedição.</p>';
    d.loja.forEach((l, i) => {
      const it = l.item;
      h += `<div class="linha">${icone(it.tipo)}<div class="corpo"><span class="r${it.r}">${esc(it.nome)}</span> <span class="nota">${RARIDADES[it.r]} · ${TIPOS[it.tipo]} · nível ${it.nivel} · Poder ${fmt(it.poder)}</span><small>${it.afixos.map(esc).join(' · ')}</small></div>` +
        `<button data-ac="comprar" data-i="${i}" ${l.pode ? '' : 'disabled'} title="${l.preco > d.ouroTotal ? 'Falta ouro' : l.pode ? '' : 'Sem espaço no inventário'}">${!l.pode && l.preco <= d.ouroTotal ? 'Inventário cheio' : `Comprar · ${fmt(l.preco)} ouro`}</button></div>`;
    });
    h += `<div class="linha"><div class="corpo">Reparo automático<small>ao voltar de cada descida, o Ferreiro já conserta o equipamento com o seu ouro (se der)</small></div><button data-ac="auto-reparo" class="${d.autoReparo ? 'ativo' : ''}">${d.autoReparo ? 'Ligado' : 'Desligado'}</button></div>`;
    // melhorar e imbuir o que está vestido
    const vestidos = d.equipados.map((it, i) => [it, i]).filter(([it]) => it && !it.nid);
    h += `<h3>Melhorar e imbuir</h3><p class="nota">Cada <b>+1</b> aumenta em 6% tudo o que o item dá (até +5). <b>Imbuir</b> dá um afixo novo a itens Comuns e Mágicos (até 2).</p>` +
      `<div class="linha"><div class="corpo">Melhoria automática<small>ao voltar de cada descida, o ouro que sobrar do reparo vira +1 no item mais barato de melhorar</small></div><button data-ac="auto-melhoria" class="${d.autoMelhoria ? 'ativo' : ''}">${d.autoMelhoria ? 'Ligada' : 'Desligada'}</button></div>`;
    if (!vestidos.length) h += '<p class="nota">Equipe um item para melhorá-lo.</p>';
    vestidos.forEach(([it, i]) => { h += `<div class="linha">${icone(it.tipo)}<div class="corpo"><span class="r${it.r}">${esc(it.nome)}</span><small>nível ${it.nivel}${it.mel >= 5 ? ' · melhoria máxima' : ''}</small></div>` +
      (it.custoMel > 0 ? `<button data-ac="melhorar-item" data-i="${i}" ${it.custoMel > d.ouroTotal ? 'disabled title="Falta ouro"' : ''}>+${it.mel + 1} · ${fmt(it.custoMel)} ouro</button>` : '') +
      (it.custoImb > 0 ? `<button data-ac="imbuir" data-i="${i}" ${it.custoImb > d.ouroTotal ? 'disabled title="Falta ouro"' : ''}>Imbuir · ${fmt(it.custoImb)} ouro</button>` : '') + '</div>'; });
    const gastos = d.equipados.map((it, i) => [it, i]).filter(([it]) => it && it.reparo > 0);
    if (gastos.length) {
      const quebrados = gastos.filter(([it]) => it.dur <= 0).length;
      h += `<h3>Reparar</h3><div class="linha"><div class="corpo">${gastos.length} ${gastos.length > 1 ? 'itens equipados gastos' : 'item equipado gasto'}${quebrados ? ` · <span style="color:#ff6a6a">${quebrados} quebrado${quebrados > 1 ? 's' : ''}</span>` : ''}<small>Cada descida gasta o que está equipado. Item quebrado não dá bônus nenhum até o reparo.</small></div><button data-ac="reparar-tudo" ${d.ouroTotal > 0 ? '' : 'disabled'}>Reparar tudo · ${fmt(d.reparoTotal)} ouro</button></div>`;
      gastos.forEach(([it, i]) => { h += `<div class="linha">${icone(it.tipo)}<div class="corpo"><span class="r${it.r}">${esc(it.nome)}</span> <span class="nota" style="color:${it.dur <= 0 ? '#ff6a6a' : ''}">durabilidade ${it.dur}/${it.durMax}${it.dur <= 0 ? ' · quebrado' : ''}</span></div><button data-ac="reparar" data-i="${i}" ${it.reparo > d.ouroTotal ? 'disabled' : ''}>Reparar · ${fmt(it.reparo)} ouro</button></div>`; });
    }
    // o Ancião: uma dica sobre o que vem pela frente
    { const prox = (d.recorde || 0) + 1, tm = TEMAS_ANDAR[temaDoAndar(prox)], dicas = [`O andar ${prox} fica em <b>${tm[0]}</b>: ${tm[4]}.`];
      if (d.acougueiro === 1) dicas.push('O <b>Açougueiro</b> ainda espera atrás da porta do andar 2. Volte forte: ele larga o Cutelo.');
      if (d.cegos === 1) dicas.push('Os <b>Salões dos Cegos</b> esperam no andar ' + d.cegosAndar + '. Lá os inimigos atacam do escuro: o primeiro golpe deles dói mais.');
      if (d.camara === 1) dicas.push('A <b>Câmara de Ossos</b> continua selada no andar ' + d.camaraAndar + '. Lá dentro há um livro de feitiço garantido.');
      if (d.rei === 1) dicas.push('O <b>Rei Esqueleto</b> dorme na tumba do andar 3. Mate o Rei antes dos esqueletos que ele ergue.');
      const faltam = d.unicos.filter((u) => !u.achado).length; if (faltam) dicas.push(`Ainda há ${faltam} itens Únicos que você não achou. Pesadelo e Inferno guardam os melhores.`);
      if (d.grimorio[8] && !d.grimorio[8].nivel) dicas.push('Você ainda não conhece o <b>Relâmpago em Cadeia</b>. É dano de raio: bom contra Súcubos e contra o que é imune a sombra.');
      if (d.grimorio[7] && !d.grimorio[7].nivel) dicas.push('Você ainda não conhece a <b>Muralha de Fogo</b>. O livro cai de chefes e elites, e a Bruxa às vezes o vende; fogo fere o que é imune a sombra e veneno.');
      if (d.grimorio[6] && !d.grimorio[6].nivel) dicas.push('Você ainda não conhece o <b>Golem</b>. A Bruxa às vezes tem o livro.');
      h += `<h3>Ancião</h3><p class="nota" style="font-style:italic">“Fique um pouco e escute.”</p>` + dicas.slice(0, 3).map((t) => `<div class="linha"><div class="corpo"><small style="font-size:13px;color:#d8cfc0">${t}</small></div></div>`).join(''); }
    const fechados = d.inventario.map((it, i) => [it, i]).filter(([it]) => it && it.nid);
    if (fechados.length) {
      const total = fechados.reduce((t, [it]) => t + it.custoId, 0);
      h += `<h3>Ancião · identificar</h3><div class="linha"><div class="corpo">${fechados.length} ${fechados.length > 1 ? 'itens não identificados' : 'item não identificado'} no inventário<small>Raros para cima caem fechados: não equipam nem mostram o que fazem até serem identificados.</small></div><button data-ac="identificar-tudo">Identificar tudo · ${fmt(total)} ouro</button><button data-ac="vender-fechados" title="Vende todos sem identificar, por 40% do valor">Vender fechados · +${fmt(fechados.reduce((t, [it]) => t + (it.trava ? 0 : it.ouro), 0))} ouro</button></div>`;
      fechados.forEach(([it, i]) => { h += `<div class="linha">${icone(it.tipo)}<div class="corpo"><span class="r${it.r}">${TIPOS[it.tipo]} não identificado</span> <span class="nota">${RARIDADES[it.r]} · nível ${it.nivel}</span></div><button data-ac="identificar" data-i="${i}" ${it.custoId > d.ouroTotal ? 'disabled' : ''}>Identificar · ${fmt(it.custoId)} ouro</button></div>`; });
    }
    h += '<h3>Vender do inventário</h3>';
    const vendaveis = d.inventario.map((it, i) => [it, i]).filter(([it]) => it && !it.trava);
    if (!vendaveis.length) h += '<p class="nota">Nada para vender: o inventário está vazio (ou tudo está travado).</p>';
    vendaveis.sort((a, b) => a[0].r - b[0].r || a[0].poder - b[0].poder).forEach(([it, i]) => {
      h += `<div class="linha">${icone(it.tipo)}<div class="corpo"><span class="r${it.r}">${it.nid ? TIPOS[it.tipo] + ' não identificado' : esc(it.nome)}</span> <span class="nota">${RARIDADES[it.r]}${it.nid ? '' : ' · Poder ' + fmt(it.poder)}</span></div><button data-ac="vender-item" data-i="${i}">Vender · +${fmt(it.ouro)} ouro</button></div>`;
    });
  } else if (aba === 'talentos') {
    {
      const at = d.atributos, pp = at.porPonto, n = (x) => (Math.round(x * 100) / 100).toLocaleString('pt-BR');
      const linhas = [['Força', `+${n(at.v[0] * pp[0])}% de dano dos minions`, `cada ponto: +${n(pp[0])}% de dano dos minions`], ['Magia', `+${n(at.v[1] * pp[1])}% de dano de feitiços · +${n(at.v[1] * pp[2])} de mana`, `cada ponto: +${n(pp[1])}% de dano de feitiços e +${n(pp[2])} de mana`],
        ['Destreza', `+${n(at.v[2] * pp[3])}% de chance de crítico · +${n(at.v[2] * pp[4])}% de velocidade de ataque dos minions`, `cada ponto: +${n(pp[3])}% de crítico e +${n(pp[4])}% de velocidade`], ['Vitalidade', `+${n(at.v[3] * pp[5])}% de vida dos minions · +${n(at.v[3] * pp[6])}% de vida do necromante`, `cada ponto: +${n(pp[5])}% de vida dos minions e +${n(pp[6])}% do necromante`]];
      h += `<h3>Livro de feitiços</h3><p class="nota">Como no Diablo: cada livro lido sobe um nível do feitiço (+3% de potência, −2% de mana). Livros caem de chefes, Únicos, elites e das Bibliotecas. Ler exige <b>Magia</b>; o que ainda não dá para ler fica guardado e é lido sozinho quando a Magia chegar. Sua Magia: <b>${d.atributos.v[1]}</b>${d.livrosNaDescida ? ` · ${d.livrosNaDescida} livro${d.livrosNaDescida > 1 ? 's' : ''} nesta descida` : ''}.</p>`;
      d.grimorio.forEach((g) => { const falta = g.guardados > 0 && g.nivel < g.max; h += `<div class="linha"><div class="corpo">${esc(g.nome)} <span class="nota">${g.nivel ? 'nível ' + g.nivel + '/' + g.max : 'ainda não aprendido: ache o livro'}</span><small>${g.nome.startsWith('Golem') ? 'um só por vez: tanque que segura a linha por 40 s · ' : g.nome.startsWith('Relâmpago') ? 'dano de raio no inimigo mais próximo, saltando para até 4 vizinhos (20% a menos por salto) · ' : g.nome.startsWith('Muralha') ? 'linha de chamas atravessada no caminho de 3 ou mais inimigos; dano de fogo a cada meio segundo por 6 s · ' : ''}${g.pot ? '+' + g.pot + '% de potência · ' : ''}${g.mana} de mana${g.nivel < g.max ? ` · próximo nível pede Magia ${g.magia}` : ' · nível máximo'}</small></div>${falta ? `<span class="nota" style="color:#ffcf5a;white-space:nowrap">${g.guardados} guardado${g.guardados > 1 ? 's' : ''} · falta Magia</span>` : ''}</div>`; });
      h += `<h3>Atributos do necromante</h3><p class="nota">Cinco pontos a cada nível, como no Diablo. Valem a partir da próxima expedição.</p>`;
      h += `<div class="linha"><div class="corpo">Pontos livres: <b style="color:var(--ouro);font-weight:normal">${at.livres}</b><small>${at.auto ? 'distribuição automática: os pontos novos vão sozinhos para o atributo mais baixo' : 'distribuição manual: use os botões + de cada atributo'}</small></div><button data-ac="auto-atributos" class="${at.auto ? 'ativo' : ''}">Automático: ${at.auto ? 'ligado' : 'desligado'}</button>${at.redistribuir ? `<button data-ac="redistribuir-atributos" ${at.redistribuir > d.essencia ? 'disabled' : ''}>Redistribuir · ${fmt(at.redistribuir)} Ess.</button>` : ''}</div>`;
      linhas.forEach((l, i) => { h += `<div class="linha"><div class="corpo"><b style="font-weight:normal;color:var(--ouro)">${l[0]} ${at.v[i]}</b> <span class="nota">${l[1]}</span><small>${l[2]}</small></div><button data-ac="atributo" data-i="${i}" data-n="1" ${at.livres >= 1 ? '' : 'disabled'}>+1</button><button data-ac="atributo" data-i="${i}" data-n="5" ${at.livres >= 5 ? '' : 'disabled'}>+5</button></div>`; });
      h += '<h3>Talentos</h3>';
    }
    h += `<p class="nota">Um ponto a cada ${d.niveisPorPonto} níveis do necromante. Os talentos mais fundos de um ramo pedem pontos no mesmo ramo. Valem a partir da próxima expedição.</p>`;
    h += `<div class="linha"><div class="corpo">Pontos para gastar: <b style="color:var(--ouro);font-weight:normal">${d.pontos}</b></div>${d.redistribuir ? `<button data-ac="redistribuir" ${d.redistribuir > d.essencia ? 'disabled' : ''}>Redistribuir · ${fmt(d.redistribuir)} Ess.</button>` : ''}</div>`;
    ['Ossos · o exército', 'Sangue · o necromante', 'Sombras · almas e saque'].forEach((titulo, ramo) => {
      h += `<h3>${titulo}</h3>`;
      d.talentos.forEach((t, i) => {
        if (t.ramo !== ramo) return;
        const travado = t.noRamo < t.requer;
        h += `<div class="linha" ${travado ? 'style="opacity:.55"' : ''}><div class="corpo">${esc(t.nome)} <span class="nota">${t.pts}/${t.max}</span>` +
          `<small>${travado ? `Pede ${t.requer} pontos neste ramo (você tem ${t.noRamo})` : t.pts ? esc(t.agora) + (t.pts < t.max ? ' → ' + esc(t.prox) : '') : 'Próximo ponto: ' + esc(t.prox)}</small></div>` +
          (t.pts < t.max ? `<button data-ac="investir" data-i="${i}" ${t.pode ? '' : 'disabled'}>+1</button>` : '<span class="nota">máx.</span>') + '</div>';
      });
    });
  } else if (aba === 'masmorra') {
    const t = masm.tipos[masm.sala];
    h += mapaGrandeHtml();
    if (masm.amb) h += `<div class="linha"><div class="corpo">Ambiente: ${AMBIENTES[masm.amb][0]}<small>${AMBIENTES[masm.amb][1]}</small></div></div>`;
    if (masm.bencao) h += `<div class="linha"><div class="corpo">Santuário ativo<small>${BENCAOS[masm.bencao]} · até o fim do andar</small></div></div>`;
    santuariosVistos |= d.santuarios;
    if (d.rei === 1 || d.rei === 3 || d.acougueiro === 1 || d.acougueiro === 3 || d.fonte === 1 || d.fonte === 3 || d.camara === 1 || d.camara === 3 || d.cegos === 1 || d.cegos === 3) h += `<h3>Missões deste save</h3>`;
    if (d.cegos === 1 || d.cegos === 3) h += `<div class="linha"><div class="corpo">${d.cegos === 3 ? '<b style="color:#8fd89f">Os Salões dos Cegos · cumprida</b>' : '<b style="color:#ffcf5a">Os Salões dos Cegos</b>'}<small>${d.cegos === 3 ? 'Os Ocultos caíram. O amuleto Olho Óptico é seu.' : 'Andar ' + d.cegosAndar + ': uma sala às escuras onde os Ocultos só aparecem em cima do grupo. Recompensa: o amuleto Olho Óptico.'}</small></div></div>`;
    if (d.camara === 1 || d.camara === 3) h += `<div class="linha"><div class="corpo">${d.camara === 3 ? '<b style="color:#8fd89f">A Câmara de Ossos · cumprida</b>' : '<b style="color:#ffcf5a">A Câmara de Ossos</b>'}<small>${d.camara === 3 ? 'O selo foi rompido e o livro é seu.' : 'Andar ' + d.camaraAndar + ', atrás de um selo: esqueletos guardam um livro de feitiço garantido.'}</small></div></div>`;
    if (d.fonte === 1 || d.fonte === 3) h += `<div class="linha"><div class="corpo">${d.fonte === 3 ? '<b style="color:#8fd89f">A Fonte Envenenada · cumprida</b>' : '<b style="color:#ffcf5a">A Fonte Envenenada</b>'}<small>${d.fonte === 3 ? 'A água voltou a correr limpa. A Curandeira vende poções ' + d.fonteDesc + '% mais baratas até o fim desta vida.' : 'Andar ' + d.fonteAndar + ': mortos apodrecem a nascente da vila. Limpe a sala e a Curandeira passa a vender poções ' + d.fonteDesc + '% mais baratas.'}</small></div></div>`;
    if (d.rei === 1 || d.rei === 3) h += `<div class="linha"><div class="corpo">${d.rei === 3 ? '<b style="color:#8fd89f">O Rei Esqueleto · cumprida</b>' : '<b style="color:#ffcf5a">O Rei Esqueleto</b>'}<small>${d.rei === 3 ? 'De volta ao pó. A Coroa do Rei Morto é sua.' : 'Andar 3, numa tumba selada. Ergue esqueletos enquanto vive. Recompensa: a Coroa do Rei Morto.'}</small></div></div>`;
    if (d.acougueiro === 1 || d.acougueiro === 3) h += `<div class="linha"><div class="corpo">${d.acougueiro === 3 ? '<b style="color:#8fd89f">O Açougueiro · cumprida</b>' : '<b style="color:#ffcf5a">O Açougueiro</b>'}<small>${d.acougueiro === 3 ? 'Morto. O Cutelo do Açougueiro é seu.' : 'Andar 2, atrás de uma porta trancada. Forte e rápido: volte quando estiver pronto. Recompensa: o Cutelo do Açougueiro.'}</small></div></div>`;
    h += `<h3>Livro de Únicos</h3><p class="nota">Itens de nome próprio, com afixos fixos, mais fortes que qualquer Raro, e um efeito só deles (vale enquanto o item está vestido). Marcados com ★. Você achou ${d.unicos.filter((u) => u.achado).length} de ${d.unicos.length}.</p>`;
    d.unicos.forEach((u) => { h += `<div class="linha">${icone(u.tipo)}<div class="corpo">${u.achado ? `<b style="color:#ffcf5a;font-weight:normal">★ ${esc(u.nome)}</b>` : `<span style="color:var(--fraco)">? ? ?</span> <span class="nota">${TIPOS[u.tipo]}</span>`}<small>${u.achado ? '<span style="color:#e8dcb0">' + esc(u.efeito) + '</span><br>' : ''}${esc(u.lenda)}</small></div></div>`; });
    h += `<h3>Livro dos Santuários</h3><p class="nota">Como no Diablo, cada santuário só diz o que faz depois de usado. O que você descobre fica anotado aqui (${SANTUARIOS.filter((x, k) => x && (d.santuarios & (1 << k))).length} de ${SANTUARIOS.length - 1}).</p>` +
      SANTUARIOS.map((x, k) => (x ? `<div class="linha"><div class="corpo"><span style="color:${d.santuarios & (1 << k) ? '#7fd8ff' : '#8d7c6c'}">${x[0]}</span> <span class="nota">“${x[1]}”</span><small>${d.santuarios & (1 << k) ? x[2] : '? ? ?'}</small></div></div>` : '')).join('');
    if (pressagio) h += `<div class="linha"><div class="corpo">Presságio: ${PRESSAGIOS[pressagio][0]}<small>${PRESSAGIOS[pressagio][1]}</small></div></div>`;
    h += '<h3>Diário da descida</h3><div class="diario">' + (diario.length ? diario.map(([a, tx]) => `<div><small>andar ${a}</small>${tx}</div>`).join('') : '<p class="nota">Nada digno de nota ainda. Baús, santuários, emboscadas e achados raros aparecem aqui.</p>') + '</div>';
    h += '<p class="nota">Entre os combates surgem salas especiais. Do andar 5 em diante, raramente, o fim de um andar abre um nível secreto.</p>';
  } else if (aba === 'combate') {
    h += estatHtml(estat, 'Expedição em curso') + estatHtml(estatAnterior, 'Expedição anterior');
  } else if (aba === 'bestiario') {
    h += `<h3>Bestiário</h3><p class="nota">Cada marco de mortes de uma espécie dá um nível de Domínio: seus minions dessa espécie ganham +${d.bestiario[0].porNivel}% de vida e dano por nível.</p>`;
    d.bestiario.forEach((b) => {
      if (!b.mortes && !b.conhecida) { h += '<div class="linha"><div class="corpo"><span class="nota">??? · espécie ainda não encontrada</span></div></div>'; return; }
      const pct = b.prox ? Math.min(100, Math.round(100 * b.mortes / b.prox)) : 100;
      const kb = b.nome.normalize('NFD').replace(/[^a-zA-Z]/g, '').toLowerCase(), hb = HABILIDADE[kb];
      const TD = ['físico', 'fogo', 'gelo', 'raio', 'veneno', 'sombra'], rs = (b.res && b.res[d.dif]) || [], imu = [], rst = [], frc = [];
      rs.forEach((v, t) => { if (v >= 100) imu.push(TD[t]); else if (v > 0) rst.push(TD[t] + ' ' + v + '%'); else if (v < 0) frc.push(TD[t] + ' +' + (-v) + '%'); });
      const linhaRes = (imu.length ? `<b style="color:#ff8a6a;font-weight:normal">Imune a ${imu.join(', ')}</b> · ` : '') + (rst.length ? 'resiste a ' + rst.join(', ') : '') + (frc.length ? (rst.length ? ' · ' : '') + `<span style="color:#8fd89f">fraco a ${frc.join(', ')}</span>` : '');
      h += `<div class="linha">${monstros[kb] ? thumb(kb) : ''}<div class="corpo">${esc(b.nome)} <span class="nota">Domínio ${b.dominio}/${b.max}${b.bonus ? ` · +${b.bonus}% vida e dano` : ''}</span>` +
        `<small>${b.prox ? `${fmt(b.mortes)} de ${fmt(b.prox)} mortes para o próximo nível` : `${fmt(b.mortes)} mortes · Domínio completo`}</small>${linhaRes ? `<small>Como inimigo (${DIFICULDADES[d.dif][0]}): ${linhaRes}</small>` : ''}${hb ? `<small>Erguido como minion — <b style="color:#7fd6a0;font-weight:normal">${hb[0]}</b>: ${hb[1]}</small>` : ''}<div class="xp"><div style="width:${pct}%;background:#a01420"></div></div></div></div>`;
    });
  } else if (aba === 'necropole') {
    h += `<h3>Dificuldade</h3><p class="nota">Como no Diablo: cada dificuldade é uma descida própria, e nas mais altas os inimigos resistem mais e alguns ficam <b>imunes</b> a um tipo de dano (o Bestiário mostra quais); com o seu Ponto de Ancoragem (trocar não apaga o da outra). Vencer o chefe do Ato 2 abre a seguinte. A troca vale a partir da próxima descida${d.difAgora !== d.dif ? ' (esta ainda é ' + DIFICULDADES[d.difAgora][0] + ')' : ''}.</p>`;
    DIFICULDADES.forEach((df, k) => { const aberta = k <= d.difMax, atual = k === d.dif; h += `<div class="linha"><div class="corpo">${df[0]}${d.vitoria && (d.vitoria.feitas >> k) & 1 ? ' <span style="color:#ffcf5a">· Diablo vencido</span>' : ''}<small>${df[1]}</small></div><button data-ac="dificuldade" data-i="${k}" class="${atual ? 'ativo' : ''}" ${atual || !aberta ? 'disabled' : ''}>${atual ? 'Escolhida' : aberta ? 'Escolher' : 'Trancada'}</button></div>`; });
    h += '<h3>Construções</h3>';
    d.construcoes.forEach((c, i) => {
      const custo = c.nivel >= c.max ? 'Máximo' : !c.aberta ? `Abre no andar ${c.andar}` : `${fmt(c.ossos)} Ossos` + (c.essencia ? ` · ${fmt(c.essencia)} Ess.` : '');
      h += `<div class="linha"><div class="corpo">${esc(c.nome)} <span class="nota">nv ${c.nivel}/${c.max}</span><small>${esc(c.efeito)}</small></div><button data-ac="melhorar" data-i="${i}" ${c.pode ? '' : 'disabled'}>${custo}</button></div>`;
    });
    h += `<h3>Ritos de Sangue <span class="nota" style="text-transform:none;letter-spacing:0">· você tem ${fmt(d.sangue)} de Sangue</span></h3><p class="nota">O Sangue vem de elites, chefes e execuções. Um rito vale só para a próxima expedição.</p>`;
    d.ritos.forEach((r) => { const prep = d.ritoPreparado === r.id; h += `<div class="linha"><div class="corpo">${esc(r.nome)}<small>${esc(r.efeito)}</small></div><button data-ac="rito" data-i="${r.id}" class="${prep ? 'ativo' : ''}" ${prep || r.sangue > d.sangue ? 'disabled' : ''}>${prep ? 'Preparado' : fmt(r.sangue) + ' Sangue'}</button></div>`; });
    h += '<h3>Oferendas</h3><p class="nota">Cada inimigo deixa Restos conforme a morte que teve (queimado, congelado, mutilado…). Cada tipo alimenta um altar próprio, e os altares sobem sozinhos ao fim de cada expedição.</p>';
    d.oferendas.forEach((o) => { const pc = o.nivel >= o.max ? 100 : Math.min(100, Math.round(100 * o.restos / o.custo)); h += `<div class="linha"><div class="corpo">${esc(o.nome)} <span class="nota">nível ${o.nivel}/${o.max}</span><small>${o.nivel ? esc(o.texto) : '+' + o.porNivel + '% por nível'}${o.nivel >= o.max ? ' · completo' : ` · ${fmt(o.restos)} de ${fmt(o.custo)} Restos para o próximo`}</small><div class="xp"><div style="width:${pc}%;background:#8a6a3a"></div></div></div></div>`; });
    const as = d.ascensao;
    h += `<h3>Ascensão${as.vezes ? ` <span class="nota" style="text-transform:none;letter-spacing:0">· ${as.vezes}ª vida · ${fmt(as.cinzas)} Cinzas</span>` : ''}</h3>`;
    if (!as.pode && !as.vezes) h += '<p class="nota">Vença a chefe do Ato 1 (andar 10) para poder ascender: morrer de propósito, recomeçar e levar Cinzas que compram Legados permanentes.</p>';
    else {
      h += '<p class="nota">Ascender zera níveis, recursos, construções, exército, talentos e altares. Ficam o Bestiário, as missões, as regras, os Legados e os itens travados no inventário.</p>';
      as.legados.forEach((l, i) => { h += `<div class="linha"><div class="corpo">${esc(l.nome)} <span class="nota">nível ${l.nivel}/${l.max}</span><small>${l.nivel ? esc(l.texto) + ' · ' : ''}+${l.porNivel}% por nível</small></div><button data-ac="legado" data-i="${i}" ${l.pode ? '' : 'disabled'}>${l.nivel >= l.max ? 'Máximo' : fmt(l.custo) + ' Cinzas'}</button></div>`; });
      // a Árvore de Ascensão: cinco ramos, de cima para baixo; cada nó pede o anterior
      h += `<h3>Árvore de Ascensão</h3><p class="nota">Cinco ramos de oito nós, comprados em ordem. Quatro dão força; a <b>Herança</b> não dá força, dá pressa: a vida seguinte já começa adiantada. Os cinco primeiros custam Cinzas; os três últimos também pedem <b style="color:#b79cff">Selos do Abismo</b> (1 por chefe vencido no Pesadelo, 2 no Inferno). O último nó de cada ramo muda uma regra do jogo. Você tem <b>${fmt(as.cinzas)}</b> Cinzas e <b style="color:#b79cff">${as.selos}</b> Selos.</p><div class="arvore">`;
      as.arvore.forEach((ra) => { h += `<div class="ramo"><div class="rt">${esc(ra.nome)}</div>` + ra.nos.map((no, k) => `<div class="no ${k < ra.n ? 'tem' : k === ra.n ? 'prox' : ''} ${no.final ? 'fim' : ''}" title="${esc(no.texto)}">${k < ra.n ? '✓ ' : ''}${esc(no.nome)}</div>`).join('<div class="liga"></div>') + '</div>'; });
      h += '</div>';
      as.arvore.forEach((ra, i) => { const no = ra.nos[ra.n]; h += no ? `<div class="linha"><div class="corpo"><span class="nota">${esc(ra.nome)} ${ra.n + 1}/8</span> ${esc(no.nome)}<small>${esc(no.texto)}</small></div><button data-ac="arvore" data-i="${i}" ${ra.pode ? '' : 'disabled'}>${ra.custoC} Cinzas${ra.custoS ? ' + ' + ra.custoS + ' Selo' + (ra.custoS > 1 ? 's' : '') : ''}</button></div>` : `<div class="linha"><div class="corpo"><span class="nota">${esc(ra.nome)}</span> <b style="color:#8fd89f;font-weight:normal">ramo completo</b><small>${esc(ra.nos[7].texto)}</small></div></div>`; });
      const prox5 = (Math.floor(as.maior / 5) + 1) * 5, cz = (a) => Math.floor((a / 5) * (a / 5));
      h += `<p class="nota"><b>Se ascender agora:</b> ${as.pode ? '+' + fmt(as.renderia) + ' Cinzas' : 'ainda não pode nesta vida'} <span style="color:#cfc4b4">(andar ${as.maior} → ${as.czAndar} · ${as.nChefes} chefe${as.nChefes === 1 ? '' : 's'} de ato → +${as.czChefes}${+as.czFator > 1 ? ' · ' + DIFICULDADES[d.dif][0] + ' ×' + String(as.czFator).replace('.', ',') : ''})</span>. Pela profundidade: o andar ${prox5} vale ${cz(prox5)}, o ${prox5 + 5} vale ${cz(prox5 + 5)}; cada chefe de ato vale +${as.czPorChefe}; ascender no Pesadelo multiplica por 1,5 e no Inferno por 2.<br><b>O que a vida seguinte já leva:</b> ${as.leva.length ? as.leva.map(esc).join(' · ') : 'nada da Herança ainda (os outros ramos e os Legados valem sempre)'}.</p>`;
      h += `<div class="rodape"><button data-ac="ascender" ${as.pode && as.renderia > 0 ? '' : 'disabled'}>${as.pode ? `Ascender agora · +${fmt(as.renderia)} Cinzas` : 'Vença a chefe do Ato 1 nesta vida para ascender de novo'}</button></div>`;
    }
    h += '<div class="rodape"><button data-ac="novo">Apagar save e começar de novo</button></div>';
  }
  return h;
  };
  // tira dos automáticos: cada feitiço pode disparar sozinho, com recarga maior
  const nomesAuto = ['Detonar', 'Frenesi', 'Sopro', itens[0][0] ? ['', 'Raio Sombrio', 'Ceifada', 'Chuva de Ossos', 'Escudo de Almas'][itens[0][0]] : null, itens[1][0] ? ['', 'Raio Sombrio', 'Ceifada', 'Chuva de Ossos', 'Escudo de Almas'][itens[1][0]] : null];
  const ha = `<span title="No automático a recarga é ${d.autoPenalidade}% maior">Automático (recarga +${d.autoPenalidade}%):</span>` + nomesAuto.map((n, i) => n ? `<button data-auto="${i}" class="${d.auto[i] ? 'ativo' : ''}">${n}</button>` : '').join('');
  if (ha !== autosAnterior) { autosAnterior = ha; $('autos').innerHTML = ha; }
  desenharInventario(d);
  desenharFicha(d);
  for (const lado of ['esq', 'dir', 'baixo']) {
    if (lado === 'esq' && invAberto) continue;
    const h = render(abasSel[lado]);
    if (forcar || h !== htmlAnterior[lado]) { htmlAnterior[lado] = h; $(ALVO[lado]).innerHTML = h; }
  }
}

// ---- inventário em grade (gaveta à esquerda) ----
let invAberto = false, invSel = null, invAnterior = '';
const RARIDADES = ['Comum', 'Mágico', 'Raro', 'Lendário', 'Profano'];
const TIPOS = ['Cajado', 'Foice', 'Grimório', 'Relicário', 'Elmo', 'Peitoral', 'Luvas', 'Botas', 'Amuleto', 'Anel'];
// arte dos itens: modelos do pipeline (blender/gerar_itens.py), um por tipo
const ARTE_ITEM = ['cajado', 'foice', 'grimorio', 'relicario', 'elmo', 'peitoral', 'luvas', 'botas', 'amuleto', 'anel'];
const icone = (t) => `<img alt="${TIPOS[t] || ''}" src="arte/itens/${ARTE_ITEM[t] || 'anel'}.webp" draggable="false">`;
let venderAte = 1, venderOk = 0;

let ultimoInv = null;
function desenharInventario(d) {
  ultimoInv = d.inventario;
  const conjSel = (() => { if (!invSel) return ''; const it = invSel[0] === 'e' ? d.equipados[+invSel.slice(1)] : d.inventario[+invSel.slice(1)]; return it && it.conj ? it.conj.nome : ''; })();
  if (!invAberto) return;
  const g = d.grade, cel = 100 / g.w;
  let h = `<div class="cab"><span>Inventário</span><button data-inv="fechar">Fechar</button></div>`;
  h += `<p class="nota">Estilo de combate: <b style="color:var(--texto);font-weight:normal">${d.corpoACorpo ? 'corpo a corpo (foice): avança, corta em arco, mais vida' : 'à distância (cajado ou mãos vazias): fica atrás do exército'}</b></p>`;
  h += '<div class="boneco">' + d.equipados.map((it, i) => `<div class="vaga ${it ? 'r' + it.r : ''} ${invSel === 'e' + i ? 'sel' : ''}" data-inv="e${i}" title="${ESPACOS[i]}${it ? ' · durabilidade ' + it.dur + '/' + it.durMax : ''}">${it ? icone(it.tipo) + (it.dur <= 0 ? '<span class="quebrado">✖</span>' : '') + `<i class="dur"><b style="width:${Math.round(100 * it.dur / it.durMax)}%;background:${it.dur <= 0 ? '#c8323c' : it.dur / it.durMax < 0.3 ? '#c8a030' : '#5a9f5a'}"></b></i>` : `<small>${ESPACOS[i]}</small>`}</div>`).join('') + '</div>';
  h += `<div class="grade" style="aspect-ratio:${g.w}/${g.h};background-size:${cel}% ${100 / g.h}%">`;
  const fora = [];
  d.inventario.forEach((it, i) => {
    const [x, y] = g.pos[i];
    if (x < 0) { fora.push(i); return; }
    h += `<div class="item r${it.r} ${invSel === 'i' + i ? 'sel' : ''}" data-inv="i${i}" style="left:${x * cel}%;top:${y * 100 / g.h}%;width:${it.w * cel}%;height:${it.h * 100 / g.h}%">${icone(it.tipo)}${it.conj ? `<span class="marca-conj${conjSel === it.conj.nome ? ' par' : ''}" title="${esc(it.conj.nome)}">◆</span>` : ''}${it.nid ? '<span class="marca-nid">?</span>' : ''}${it.req && it.req[2] < it.req[1] ? '<span class="marca-req" title="Falta atributo para vestir"></span>' : ''}${it.trava ? '<span class="trava">travado</span>' : ''}</div>`;
  });
  h += '</div>';
  if (fora.length) h += `<p class="nota">${fora.length} item(ns) sem lugar na grade: equipe ou desmanche algo.</p>`;
  let sel = null, ehEquipado = false, idx = -1;
  if (invSel) { ehEquipado = invSel[0] === 'e'; idx = +invSel.slice(1); sel = ehEquipado ? d.equipados[idx] : d.inventario[idx]; }
  if (sel) {
    h += `<div class="ficha r${sel.r}"><b>${sel.nid ? TIPOS[sel.tipo] + ' não identificado' : esc(sel.nome)}</b><span class="nota">${RARIDADES[sel.r]} · ${TIPOS[sel.tipo]} · nível ${sel.nivel} · ocupa ${sel.w}×${sel.h} · <span style="color:var(--ouro)">Poder ${fmt(sel.poder)}</span></span>` +
      `<div class="nota" style="color:${sel.dur <= 0 ? '#ff6a6a' : sel.dur / sel.durMax < 0.3 ? '#e0b040' : ''}">Durabilidade ${sel.dur}/${sel.durMax}${sel.dur <= 0 ? ' · QUEBRADO: não dá nenhum bônus até ser reparado' : ''}</div>` +
      (sel.req && sel.req[1] > 0 ? `<div class="nota" style="color:${sel.req[2] >= sel.req[1] ? '#8fd89f' : '#ff6a6a'}">Requer ${['Força', 'Magia', 'Destreza', 'Vitalidade'][sel.req[0]]} ${sel.req[1]} · você tem ${sel.req[2]}${sel.req[2] >= sel.req[1] ? '' : ' · faltam ' + (sel.req[1] - sel.req[2]) + (ehEquipado ? ' (já vestido: continua valendo)' : ': os pontos automáticos vão para lá primeiro')}</div>` : '') +
      (sel.nid ? '<div class="nota">As propriedades deste item estão ocultas. Identifique-o com ouro para ver o que ele faz e poder equipá-lo.</div>' : '') +
      sel.afixos.map((a) => `<div>${esc(a)}</div>`).join('') +
      (sel.conj ? `<div style="margin-top:6px;border-left:3px solid #c05ad8;padding-left:8px"><b style="color:#d58cf0">${esc(sel.conj.nome)}</b> <span class="nota">· ${sel.conj.tem}/3 peças equipadas</span><div class="nota">${sel.conj.pecas.map(esc).join(' · ')}</div><div style="opacity:${sel.conj.tem >= 2 ? 1 : 0.5}">2 peças: ${sel.conj.b2.map(esc).join(', ')}</div><div style="opacity:${sel.conj.tem >= 3 ? 1 : 0.5}">3 peças: ${sel.conj.b3.map(esc).join(', ')}</div></div>` : '') +
      (sel.tipo === 0 ? '<div class="nota">Cajado: o necromante luta à distância.</div>' : sel.tipo === 1 ? '<div class="nota">Foice: o necromante luta corpo a corpo.</div>' : '') +
      (sel.trava ? '<div class="nota">Travado: não é vendido nem desmanchado.</div>' : '') +
      '<div class="rodape">' + (ehEquipado ? `<button data-ac="desequipar" data-i="${idx}">Tirar</button>` + (sel.reparo > 0 ? `<button data-ac="reparar" data-i="${idx}" ${sel.reparo > d.ouroTotal ? 'disabled title="Falta ouro"' : ''}>Reparar · ${fmt(sel.reparo)} ouro</button>` : '') : (sel.nid ? `<button data-ac="identificar" data-i="${idx}" ${sel.custoId > d.ouroTotal ? 'disabled title="Falta ouro"' : ''}>Identificar · ${fmt(sel.custoId)} ouro</button>` : `<button data-ac="equipar" data-i="${idx}" ${sel.req && sel.req[2] < sel.req[1] ? 'disabled title="Falta atributo"' : ''}>${sel.req && sel.req[2] < sel.req[1] ? 'Falta ' + ['Força', 'Magia', 'Destreza', 'Vitalidade'][sel.req[0]] : 'Equipar'}</button>`) + `<button data-ac="desmanchar" data-i="${idx}" ${sel.trava ? 'disabled' : ''}>Desmanchar · +${fmt(sel.ess)} Ess.</button><button data-ac="vender-item" data-i="${idx}" ${sel.trava ? 'disabled' : ''}>Vender · +${fmt(sel.ouro)} ouro</button>`) +
      `<button data-ac="travar" data-i="${idx}" data-eq="${ehEquipado ? 1 : 0}" class="${sel.trava ? 'ativo' : ''}">${sel.trava ? 'Destravar' : 'Travar'}</button></div></div>`;
  } else h += '<p class="nota">Toque num item para ver a descrição.</p>';
  let nV = 0, eV = 0; d.inventario.forEach((it) => { if (!it.trava && it.r <= venderAte) { nV++; eV += it.ess; } });
  h += `<div class="rodape"><button data-ac="auto">Equipar os melhores</button></div>`;
  h += `<div class="linha" style="margin-top:10px"><div class="corpo">Desmanchar tudo<small>vira Essência; itens travados e equipados ficam. Para ouro, venda no Ferreiro.</small></div><select data-vender="1">` +
    RARIDADES.map((r, k) => `<option value="${k}" ${k === venderAte ? 'selected' : ''}>${k === 0 ? 'só Comuns' : 'até ' + r}</option>`).join('') +
    `</select></div><div class="rodape" style="margin-top:0"><button data-ac="vender-tudo" ${nV ? '' : 'disabled'} class="${Date.now() - venderOk < 4000 ? 'ativo' : ''}">${Date.now() - venderOk < 4000 ? 'Confirmar: ' : ''}Desmanchar ${nV} ${nV === 1 ? 'item' : 'itens'} · +${fmt(eV)} Ess.</button></div>`;
  h += `<div class="linha" style="margin-top:10px"><div class="corpo">Desmanchar ao pegar<small>itens abaixo desta raridade viram Essência na hora</small></div><select data-regra="0">` +
    RARIDADES.map((r, k) => `<option value="${k}" ${k === d.regras[0] ? 'selected' : ''}>${k === 0 ? 'nada' : 'abaixo de ' + r}</option>`).join('') + '</select></div>';
  h += '<p class="nota">O que você equipa vale a partir da próxima expedição.</p>';
  if (h !== invAnterior) { invAnterior = h; $('inventario').innerHTML = h; }
}
function acaoDoPainel(b) {
  const i = +b.dataset.i;
  switch (b.dataset.ac) {
    case 'equipar': { const cj = ultimoInv && ultimoInv[i] && ultimoInv[i].conj; J.Equipar(i); invSel = null; if (cj) { const n = Math.min(3, cj.tem + 1); if (n >= 2) { aviso(`${cj.nome}: bônus de ${n} peças ativo!`); som('bencao'); } else aviso(`${cj.nome}: 1 de 3 peças`); } break; }
    case 'desequipar': J.Desequipar(i); invSel = null; break;
    case 'desmanchar': J.Desmanchar(i); invSel = null; break;
    case 'vender-item': J.VenderItem(i); invSel = null; som('toque'); break;
    case 'reparar': if (J.RepararEquipado(i)) { som('golpe'); aviso('Item reparado'); } break;
    case 'identificar': if (J.IdentificarItem(i)) { som('bencao'); aviso('Item identificado'); } break;
    case 'auto': J.AutoEquipar(); invSel = null; break;
    case 'travar': J.Travar(i, b.dataset.eq === '1'); break;
    case 'vender-tudo':
      if (Date.now() - venderOk > 4000) { venderOk = Date.now(); break; }
      { const [n, e] = J.VenderTudo(venderAte).split(' '); venderOk = 0; invSel = null; aviso(`${n} itens vendidos · +${fmt(+e)} de Essência`); }
      break;
  }
}
$('inventario').addEventListener('click', (ev) => {
  if (!J) return;
  const b = ev.target.closest('button[data-ac]');
  if (b) { acaoDoPainel(b); salvar(); atualizarPainel(true); return; }
  const v = ev.target.closest('[data-inv]'); if (!v) return;
  if (v.dataset.inv === 'fechar') { invAberto = false; $('inventario').classList.remove('aberto'); return; }
  invSel = invSel === v.dataset.inv ? null : v.dataset.inv; atualizarPainel(true);
});
document.addEventListener('change', (ev) => {
  const se = ev.target.closest('select[data-ex]'); if (se) { if (se.dataset.ex === 'filtro') exFiltro = se.value; else exOrdem = se.value; try { localStorage.setItem(CHAVE + '-filtro', exFiltro); localStorage.setItem(CHAVE + '-ordem', exOrdem); } catch (e) { /* tudo bem */ } if (J) atualizarPainel(true); return; }
  const sp = ev.target.closest('select[data-postura]'); if (sp && J) { J.Postura(+sp.value); salvar(); atualizarPainel(true); return; }
  const sv = ev.target.closest('select[data-vender]'); if (sv) { venderAte = +sv.value; venderOk = 0; if (J) atualizarPainel(true); return; }
  const sl = ev.target.closest('select[data-regra]'); if (!sl || !J) return;
  J.Regra(+sl.dataset.regra, +sl.value); salvar(); atualizarPainel(true);
});

// ---- ficha do minion: retrato animado, atributos, traços e ações ----
let fichaIdx = -1, fichaAnterior = '', fichaMinion = null, fichaAcao = 0, fichaDir = 1, fichaT0 = 0;
const ACOES_FICHA = ['parado', 'andar', 'atacar', 'morrer'];
function desenharFicha(d) {
  const el = $('ficha-minion');
  if (fichaIdx < 0 || fichaIdx >= d.minions.length) { fichaIdx = -1; fichaMinion = null; el.hidden = true; return; }
  const m = d.minions[fichaIdx], f = m.ficha; fichaMinion = m; el.hidden = false;
  const num = (v) => v >= 100 ? fmt(Math.round(v)) : (Math.round(v * 10) / 10).toString();
  let h = `<div class="nome q${m.q}">${esc(m.nome)}</div><div class="sub">${QUALIDADES[m.q]} (atributos ×${f.qmult}) · nível ${m.nivel}/${m.max} · estágio ${m.estagio}<br>${f.familia} · porte ${f.porte} · ${m.comando} de Comando</div>`;
  h += `<div class="xp"><div style="width:${Math.round(m.xp * 100)}%"></div></div>`;
  h += `<div class="attrs"><div><span>Vida</span><b>${num(f.vida)}</b></div><div><span>Dano por golpe</span><b>${num(f.dano)}</b></div>` +
    `<div><span>Golpes por segundo</span><b>${num(f.vel)}</b></div><div><span>Dano por segundo</span><b>${num(f.dps)}</b></div>` +
    `<div><span>Armadura</span><b>${num(f.armadura)}</b></div><div><span>Alcance</span><b>${num(f.alcance)}</b></div>` +
    `<div><span>Movimento</span><b>${num(f.mov)}</b></div><div><span>Tipo de dano</span><b>${esc(f.tipoDano)}</b></div></div>`;
  h += `<div class="tr" style="text-align:center;font-size:16px"><b>Poder ${fmt(f.poder)}</b></div>` + (f.dons.some((x) => x) ? `<div class="tr">${donsHtml(f.dons).replace(/<\/?small>/g, '')} <span class="nota">(de nascença, já nos números)</span></div>` : '');
  h += `<div class="tr">${esc(f.papel)}</div>`;
  if (f.dominio) h += `<div class="tr"><b>Domínio ${f.dominio}</b>: bônus do Bestiário já incluído nos números acima</div>`;
  { const hb = HABILIDADE[f.sprite]; if (hb) h += `<div class="tr" style="border-left:3px solid #7fd6a0;padding-left:8px"><b style="color:#7fd6a0">${hb[0]}</b> <span class="nota">(habilidade da espécie)</span>: ${hb[1]}</div>`; }
  m.tracos.forEach((k) => { if (TRACOS[k]) h += `<div class="tr"><b>${TRACOS[k][0]}</b>: ${TRACOS[k][1]}</div>`; });
  if (!m.tracos.length) h += '<div class="tr nota">Alma sem traços. Fundir ou Imbuir sobe a qualidade; almas melhores trazem traços.</div>';
  h += '<p class="nota">Toque no retrato para trocar a animação. Os números valem para a próxima expedição.</p><div class="rodape">' +
    `<button data-ac="alternar" data-i="${fichaIdx}" class="${m.ativo ? 'ativo' : ''}">${m.ativo ? 'Em campo' : 'Na reserva'}</button>` +
    (m.imbuir >= 0 ? `<button data-ac="imbuir" data-i="${fichaIdx}" ${m.imbuir > d.fragmentos ? 'disabled' : ''}>Imbuir · ${fmt(m.imbuir)} Frag.</button>` : '') +
    (m.evoluir >= 0 ? `<button data-ac="evoluir" data-i="${fichaIdx}" ${m.evoluir > d.essencia ? 'disabled' : ''}>Evoluir · ${fmt(m.evoluir)} Ess.</button>` : '') +
    `<button data-ac="sacrificar" data-i="${fichaIdx}">Sacrificar · +${fmt(m.frag)}</button><button data-fechar="1">Fechar</button></div>`;
  if (h !== fichaAnterior) { fichaAnterior = h; $('ficha-corpo').innerHTML = h; }
}
function animarRetrato(agora) {
  requestAnimationFrame(animarRetrato);
  if (!fichaMinion) return;
  const c = $('retrato'), x = c.getContext('2d'), m = monstro(fichaMinion.ficha.sprite, false);
  let nome = ACOES_FICHA[fichaAcao % ACOES_FICHA.length]; let a = m.acoes[nome] || m.acoes.conjurar || m.acoes.parado;
  const t = (agora - fichaT0) / 1000; let fr = Math.floor(t * a.fps);
  if (!a.loop && fr >= a.frames + 6) { fichaT0 = agora; fr = 0; }
  fr = a.loop ? fr % a.frames : Math.min(fr, a.frames - 1);
  x.clearRect(0, 0, c.width, c.height);
  const k = Math.min(c.width / m.W, c.height / m.H) * 1.7;      // o quadro tem margem: amplia e ancora nos pés
  x.imageSmoothingEnabled = true;
  x.drawImage(a.img, fr * m.W, fichaDir * m.H, m.W, m.H, c.width / 2 - m.pivo[0] * k, c.height * 0.80 - (m.H - m.pivo[1]) * k, m.W * k, m.H * k);
  x.fillStyle = '#8d7c6c'; x.font = '12px Georgia'; x.textAlign = 'center'; x.fillText(a.nome, c.width / 2, c.height - 8);
}
requestAnimationFrame(animarRetrato);
$('retrato').addEventListener('click', () => { fichaAcao++; fichaT0 = performance.now(); if (fichaAcao % ACOES_FICHA.length === 0) fichaDir = (fichaDir + 1) % 8; });
$('ficha-minion').addEventListener('click', (ev) => {
  if (ev.target === $('ficha-minion') || ev.target.closest('[data-fechar]')) { fichaIdx = -1; atualizarPainel(true); return; }
  const b = ev.target.closest('button[data-ac]'); if (!b || !J) return;
  const i = +b.dataset.i;
  if (b.dataset.ac === 'alternar') J.AlternarMinion(i);
  else if (b.dataset.ac === 'imbuir') { if (J.Imbuir(i)) aviso('Alma imbuída'); }
  else if (b.dataset.ac === 'evoluir') { if (J.Evoluir(i)) aviso('Minion evoluído'); }
  else if (b.dataset.ac === 'sacrificar') {
    if (b.dataset.ok !== '1') { b.dataset.ok = '1'; b.textContent = 'Confirmar sacrifício'; b.classList.add('ativo'); return; }
    if (J.Sacrificar(i)) { aviso('Minion sacrificado'); fichaIdx = -1; }
  }
  salvar(); atualizarPainel(true);
});

let confirmarNovo = 0;
// toque na arena: ceifa o orbe mais próximo do dedo
function toqueNaArena(ev) {
  if (!J) return;
  const r = tela.getBoundingClientRect(), k = (tela.width / r.width) / escAtual;
  const ax = (ev.clientX - r.left) * k + camX, ay = (ev.clientY - r.top) * k + camY;
  if (objeto && !objeto.aberto && masm.fase === 'Explorando') { const [ox, oy] = naTela(objeto.x, objeto.y); if ((ox - ax) ** 2 + (oy - 30 - ay) ** 2 < 90 * 90) { J.Interagir(); aprendeu('sala'); if (!objeto.tocado) { objeto.tocado = true; aviso('Você pôs a mão: rende mais'); } return; } }
  // toque no rótulo de uma sala ainda não visitada: o grupo vai para ela em seguida
  for (let i = masm.sala + 1; i < mapa.length - 1; i++) {
    const o = offDaSala(i), [lx, ly0] = W(o[0] + 10, o[1] + 6), kk = 1 / escAtual, ly = ly0 - (62 * kk + 40) - 4 * kk;
    if ((lx - ax) ** 2 + (ly - ay) ** 2 < (30 * kk) ** 2) {
      if (i === masm.sala + 1) { aviso('Essa já é a próxima sala'); return; }
      if (J.IrPara(i)) { aprendeu('rota'); som('toque'); { const nomeR = revelada(i) ? SALAS[masm.tipos[i]][1] : 'sala desconhecida'; aviso('Rota mudada · próxima: ' + nomeR); anotar(`Você mandou o grupo para <b>${nomeR}</b> primeiro.`); } }
      return;
    }
  }
  // toque num barril, urna ou sarcófago da sala atual: quebra na hora
  if (masm.fase === 'EmOnda') {
    let qi = -1, mq = 34 * 34;
    quebraveis.forEach((q, i) => { if (q[4] || q[0] !== masm.sala) return; const [px, py] = naTela(q[2], q[3]), d = (px - ax) ** 2 + (py - 14 - ay) ** 2; if (d < mq) { mq = d; qi = i; } });
    if (qi >= 0 && J.Quebrar(qi)) { som('toque'); return; }
  }
  let melhor = null, md = 70 * 70;
  for (const o of orbes) { const [px, py] = naTela(o[0], o[1]); const d = (px - ax) ** 2 + (py - 30 - ay) ** 2; if (d < md) { md = d; melhor = o; } }
  if (melhor) { J.Ceifar(melhor[2]); aprendeu('orbe'); try { localStorage.setItem(CHAVE + '-dica', '1'); } catch (e) { /* tudo bem */ } return; }
  // toque num aliado ferido: Sopro Profano (se não estiver em recarga)
  if (sopro <= 0) {
    let al = null, mdA = 60 * 60;
    for (const s of estado.values()) {
      if (!s.viva || s.inimigo || s.vida > 0.85) continue;
      const [px, py] = naTela(s.rx, s.ry), d = (px - ax) ** 2 + (py - 45 - ay) ** 2;
      if (d < mdA) { mdA = d; al = s; }
    }
    if (al) { J.Curar(al.id); aprendeu('cura'); for (let i = 0; i < 18; i++) particulas.push([al.rx, al.ry, 0.3, rnd(-1, 1), rnd(-0.6, 0.6), rnd(2, 5), '#78ffaa', rnd(0.5, 1)]); if (!dicaSopro) { dicaSopro = true; aviso('Sopro Profano: aliado curado'); } return; }
  }
  // sem orbe por perto: o toque marca o inimigo mais próximo do dedo
  let alvoM = null; md = 80 * 80;
  for (const s of estado.values()) {
    if (!s.viva || !s.inimigo) continue;
    const [px, py] = naTela(s.rx, s.ry), d = (px - ax) ** 2 + (py - 45 - ay) ** 2;
    if (d < (s.golpe > 0 ? md * 4 : md) && !(alvoM && alvoM.golpe > 0 && !(s.golpe > 0))) { md = s.golpe > 0 ? d / 4 : d; alvoM = s; }
  }
  if (alvoM) { J.Marcar(alvoM.id); aprendeu(alvoM.golpe > 0 ? 'golpe' : 'marca'); if (!dicaMarca) { dicaMarca = true; aviso('Alvo marcado: os minions vão nele'); } }
}
// ponteiros na arena: toque curto age; arrastar move a câmera; pinça ou roda do mouse dá zoom
const ponteiros = new Map(); let arrasto = null, pinca = 0;
tela.addEventListener('pointerdown', (ev) => {
  ponteiros.set(ev.pointerId, [ev.clientX, ev.clientY]); try { tela.setPointerCapture(ev.pointerId); } catch (e) { /* tudo bem */ }
  if (ponteiros.size === 1) arrasto = { x: ev.clientX, y: ev.clientY, t: performance.now(), moveu: false };
  else if (ponteiros.size === 2) { const [a, b] = [...ponteiros.values()]; pinca = Math.hypot(a[0] - b[0], a[1] - b[1]); if (arrasto) arrasto.moveu = true; }
});
tela.addEventListener('pointermove', (ev) => {
  if (!ponteiros.has(ev.pointerId)) return;
  const ant = ponteiros.get(ev.pointerId); ponteiros.set(ev.pointerId, [ev.clientX, ev.clientY]);
  const r = tela.getBoundingClientRect(), k = (tela.width / r.width) / escAtual;
  if (ponteiros.size === 2) {
    const [a, b] = [...ponteiros.values()], d = Math.hypot(a[0] - b[0], a[1] - b[1]);
    if (pinca > 0 && d > 0) zoomLivre = (zoomLivre || escAtual) * (d / pinca); pinca = d; return;
  }
  if (!arrasto) return;
  if (!arrasto.moveu && Math.hypot(ev.clientX - arrasto.x, ev.clientY - arrasto.y) > 8) arrasto.moveu = true;
  if (arrasto.moveu) { pan[0] -= (ev.clientX - ant[0]) * k; pan[1] -= (ev.clientY - ant[1]) * k; }
});
const soltar = (ev) => {
  const era = ponteiros.size; ponteiros.delete(ev.pointerId); pinca = 0;
  if (era === 1 && arrasto && !arrasto.moveu && performance.now() - arrasto.t < 600 && ev.type === 'pointerup') toqueNaArena(ev);
  if (ponteiros.size === 0) arrasto = null;
};
tela.addEventListener('pointerup', soltar); tela.addEventListener('pointercancel', soltar);
tela.addEventListener('wheel', (ev) => { ev.preventDefault(); zoomLivre = (zoomLivre || escAtual) * Math.exp(-ev.deltaY * 0.0012); }, { passive: false });
// usado pelos testes automatizados: posição na página do primeiro orbe visível
window.__orbeNaTela = () => {
  if (!orbes.length) return null;
  const r = tela.getBoundingClientRect(), k = escAtual / (tela.width / r.width), [px, py] = naTela(orbes[0][0], orbes[0][1]);
  const x = r.left + (px - camX) * k, y = r.top + (py - 30 - camY) * k;
  return x > r.left + 4 && x < r.right - 4 ? [x, y] : null;
};
window.__objetoNaTela = () => { if (!objeto || objeto.aberto) return null; const r = tela.getBoundingClientRect(), k = escAtual / (tela.width / r.width), [px, py] = naTela(objeto.x, objeto.y); return [r.left + (px - camX) * k, r.top + (py - 30 - camY) * k, objeto.tocado]; };
window.__rotulo = (i) => { const r = tela.getBoundingClientRect(), k = escAtual / (tela.width / r.width), o = offDaSala(i), [lx, ly0] = W(o[0] + 10, o[1] + 6), kk = 1 / escAtual; return [r.left + (lx - camX) * k, r.top + (ly0 - (62 * kk + 40) - 4 * kk - camY) * k, masm.sala, mapa.length, masm.tipos.join(',')]; };
window.__focar = () => { camForcar = true; };   // testes: assenta a câmera no próximo quadro
window.__grade = () => (saidaAtual === 'o' || saidaAtual === 'n' ? gradeAberta : -1);   // testes: -1 = saída sem parede; 0 = fechada; 1 = aberta
window.__campeoes = () => { let n = 0; for (const s of estado.values()) if (s.campeao && s.viva) n++; return n; };
window.__flancos = () => (vistosDeEspecie && vistosDeEspecie["37"]) ? 1 : 0;
window.__viajando = () => !!deltaDaViagem();
window.__nomesUnicos = () => { const r = []; for (const s of estado.values()) if (s.viva && nomesUnicos.has(s.id)) r.push(nomesUnicos.get(s.id)); return r.join('|'); };
window.__unicos = () => { let n = 0; for (const s of estado.values()) if (s.viva && nomesUnicos.has(s.id)) n++; return n; };
window.__quebraveis = () => quebraveis.filter((q) => q[0] === masm.sala && !q[4]).length;
window.__objetoTipo = () => (objeto && !objeto.aberto ? objeto.tipo : -1);
window.__patrulha = () => (patrulha ? patrulha.sala : -1);
window.__cena = () => [!!descida, !!chegada];
window.__titulo = () => tituloAndar ? tituloAndar.a + "|" + tituloAndar.b : "";
window.__marcarPrimeiro = () => { const al = [...estado.values()].filter((e) => e.viva && e.inimigo); if (al.length < 3 || !J) return false; const u = al.find((e) => e.campeao) || al[0]; J.Marcar(u.id); return true; };
window.__ficha = () => fichaTxt;
window.__imune = () => (vistosDeEspecie && vistosDeEspecie['38']) ? 1 : 0;
window.__marcarUnico = () => { for (const e of estado.values()) if (e.viva && nomesUnicos.has(e.id) && e.inimigo && J) { J.Marcar(e.id); return true; } return false; };
window.__marcarEspecie = (nome) => { for (const e of estado.values()) if (e.viva && e.inimigo && e.especie === nome && J) { J.Marcar(e.id); return true; } return false; };
window.__ocultos = () => ocultos.size;
window.__apoc = () => apocFx.length;
window.__teles = () => teleFx.length;
window.__raios = () => raiosFx.length;
for (const idG of ['g-vida', 'g-mana']) $(idG).addEventListener('click', () => { const g = $(idG); g.classList.add('mostra'); clearTimeout(g.tm); g.tm = setTimeout(() => g.classList.remove('mostra'), 2500); });
$('vit-ok').addEventListener('click', () => { if (J) J.ReconhecerVitoria(); $('vitoria').hidden = true; });
window.__muralha = () => muralhasFx.length ? 1 : 0;
window.__muralhaInimiga = () => muralhasFx.some((m) => m.ini) ? 1 : 0;
window.__dbg = () => [orbes.length, orbes[0] ? naTela(orbes[0][0], orbes[0][1])[0] | 0 : 0, camX | 0, tela.getBoundingClientRect().width, chao.height];
$('bencaos').addEventListener('click', (ev) => { const b = ev.target.closest('button[data-b]'); if (b && J) { J.EscolherBencao(+b.dataset.b); aprendeu('sala'); som('toque'); } });
let encAberta = false;
$('encruzilhada').addEventListener('click', (ev) => { if (ev.target.closest('.cab')) { encAberta = !encAberta; return; } const b = ev.target.closest('button[data-o]'); if (b && J) { J.Pacto(+b.dataset.o); aviso('Pacto selado'); } });
$('resumo').addEventListener('click', () => { $('resumo').hidden = true; });
$('missao').addEventListener('click', (ev) => { if (!ev.target.closest('button')) $('missao').classList.toggle('min'); });
$('recursos').addEventListener('click', (ev) => { if (ev.target.closest('.rec')) $('ajuda').hidden = false; });
// ---- painel de testes (cheats) ----
const CAMPOS_CHEAT = [['ossos', 'Ossos', (d) => d.ossos], ['essencia', 'Essência', (d) => d.essencia], ['sangue', 'Sangue', (d) => d.sangue], ['fragmentos', 'Fragmentos de Alma', (d) => d.fragmentos],
  ['cinzas', 'Cinzas', (d) => d.cinzas], ['nivel', 'Nível do necromante', (d) => d.nivel], ['comando', 'Comando base', (d) => d.comandoBase],
  ['poder', 'Poder Necromântico (%)', (d) => d.poderNec, 'soma em todos os atributos dos minions'], ['sorte', 'Sorte (pontos)', (d) => d.sorte, 'cada ponto: +0,5% de itens e almas, e 1% de chance de vir melhor'],
  ['andar', 'Maior andar concluído', (d) => d.recorde, 'a próxima expedição parte daí; chefes anteriores contam como vencidos']];
function abrirCheats() {
  const d = JSON.parse(J.Painel());
  $('cheats-campos').innerHTML = CAMPOS_CHEAT.map(([k, n, f, dica]) => `<div class="campo"><span>${n}${dica ? `<small>${dica}</small>` : ''}</span><input type="number" min="0" data-campo="${k}" value="${f(d)}"><button data-cheat="${k}">Definir</button></div>`).join('');
  $('cheats').hidden = false;
}
$('b-cheats').addEventListener('click', () => { if (J) abrirCheats(); });
$('cheats').addEventListener('click', (ev) => {
  if (ev.target === $('cheats') || ev.target.id === 'b-cheats-fechar') { $('cheats').hidden = true; return; }
  const b = ev.target.closest('button[data-cheat]'); if (!b || !J) return;
  const campo = $('cheats').querySelector(`input[data-campo="${b.dataset.cheat}"]`);
  const v = campo ? +campo.value : +b.dataset.v;
  if (!Number.isFinite(v)) return;
  J.Cheat(b.dataset.cheat, v); limparArena(true); andar = 0; salvar(); atualizarPainel(true); abrirCheats(); aviso('Teste aplicado');
});
$('b-ajuda').addEventListener('click', () => { $('ajuda').hidden = false; });
const pintarSom = () => { $('b-som').textContent = somLigado ? '♪' : '♪̸'; $('b-som').style.opacity = somLigado ? 1 : 0.45; };
$('b-som').addEventListener('click', () => { somLigado = !somLigado; try { localStorage.setItem('necromante-idle-save-v1-som', somLigado ? '1' : '0'); } catch (e) { /* tudo bem */ } pintarSom(); if (somLigado) { ligarSom(); setTimeout(() => som('bau'), 60); } });
pintarSom();
$('ajuda').addEventListener('click', (ev) => { if (ev.target === $('ajuda') || ev.target.id === 'b-ajuda-fechar') $('ajuda').hidden = true; });
$('b-item0').addEventListener('click', () => { if (J) J.FeiticoDeItem(0); });
$('b-item1').addEventListener('click', () => { if (J) J.FeiticoDeItem(1); });
$('autos').addEventListener('click', (ev) => { const b = ev.target.closest('button[data-auto]'); if (b && J) { J.AlternarAuto(+b.dataset.auto); salvar(); atualizarPainel(true); } });
$('b-detonar').addEventListener('click', () => { if (J) { J.Detonar(); aprendeu('detonar'); } });
const DIFICULDADES = [['Normal', 'a descida como você a conhece'], ['Pesadelo', 'uma descida nova, do andar 1, mas continuando de onde o Normal parou: o andar 1 tem a força do andar 21 · itens 20 níveis acima e mais raros, 30% mais ouro'], ['Inferno', 'outra descida nova: o andar 1 tem a força do andar 41 · itens 40 níveis acima, bem mais raros, 60% mais ouro · é o fim de jogo, longo']];
const RAIO_DE_LUZ = 7.5;   // em unidades da sala: até onde a luz do necromante mostra os adormecidos
const NOMES_ZOOM = ['Perto', 'Sala', 'Andar inteiro'];
const pintarZoom = () => { $('b-zoom').textContent = '⤢ ' + (zoomLivre || pan[0] || pan[1] ? 'Recentrar' : NOMES_ZOOM[zoomModo]); $('salas').style.display = zoomModo === 2 ? 'none' : ''; };
$('b-zoom').addEventListener('click', () => { aprendeu('mapa'); if (zoomLivre || pan[0] || pan[1]) { zoomLivre = 0; pan = [0, 0]; pintarZoom(); return; } zoomModo = (zoomModo + 1) % 3; try { localStorage.setItem('necromante-idle-save-v1-zoom', String(zoomModo)); } catch (e) { /* tudo bem */ } pintarZoom(); });
pintarZoom();
$('salas').addEventListener('click', () => { aprendeu('mapa'); $('mapa-corpo').innerHTML = mapaGrandeHtml(); $('mapa-grande').hidden = false; });
$('mapa-grande').addEventListener('click', (ev) => { if (ev.target === $('mapa-grande') || ev.target.id === 'b-mapa-fechar') $('mapa-grande').hidden = true; });
$('grupo').addEventListener('click', (ev) => { const r = ev.target.closest('[data-ret]'); if (!r || !J) return; const s = estado.get(+r.dataset.ret); if (s && s.viva && s.vida < 0.85 && sopro <= 0) { J.Curar(s.id); aprendeu('cura'); aviso('Sopro Profano'); for (let i = 0; i < 18; i++) particulas.push([s.rx, s.ry, 0.3, rnd(-1, 1), rnd(-0.6, 0.6), rnd(2, 5), '#78ffaa', rnd(0.5, 1)]); } else if (s && sopro > 0) aviso(`Sopro em recarga: ${Math.ceil(sopro)} s`); });
$('b-frenesi').addEventListener('click', () => { if (J) J.Frenesi(); });
$('b-portal').addEventListener('click', () => { if (J && J.Portal()) $('b-portal').hidden = true; });
$('missao').addEventListener('click', (ev) => {
  const b = ev.target.closest('button[data-ac="resgatar"]'); if (!b || !J) return;
  if (J.Resgatar()) { aviso('Missão cumprida'); salvar(); atualizarPainel(true); }
});
for (const idp of ['painel', 'p-esq', 'p-dir']) $(idp).addEventListener('click', (ev) => {
  const fc = ev.target.closest('[data-ficha]'); if (fc && J && !ev.target.closest('button')) { fichaIdx = +fc.dataset.ficha; fichaAcao = 0; fichaDir = 1; fichaT0 = performance.now(); atualizarPainel(true); return; }
  const b = ev.target.closest('button[data-ac]'); if (!b || !J) return;
  const i = +b.dataset.i;
  switch (b.dataset.ac) {
    case 'alternar': J.AlternarMinion(i); break;
    case 'erguer': J.Erguer(i); break;
    case 'legado': J.ComprarLegado(i); break;
    case 'arvore': if (J.ComprarNoDaArvore(i)) { som('bencao'); aviso('Nó da Árvore de Ascensão comprado'); } break;
    case 'ascender':
      if (b.dataset.ok !== '1') { b.dataset.ok = '1'; b.textContent = 'Confirmar: recomeçar a vida'; b.classList.add('ativo'); return; }
      { const c = J.Ascender(); if (c > 0) { limparArena(true); andar = 0; aviso(`Ascensão: +${c} Cinzas`); } }
      break;
    case 'ofertar': J.Ofertar(i); break;
    case 'rito': if (J.PrepararRito(i)) aviso('Rito preparado'); break;
    case 'imbuir': if (J.Imbuir(i)) aviso('Alma imbuída'); break;
    case 'sacrificar':
      if (b.dataset.ok !== '1') { b.dataset.ok = '1'; b.textContent = 'Confirmar sacrifício'; b.classList.add('ativo'); return; }
      if (J.Sacrificar(i)) aviso('Minion sacrificado'); break;
    case 'investir': J.Investir(i); break;
    case 'redistribuir': if (J.Redistribuir()) aviso('Talentos redistribuídos'); break;
    case 'fundir': if (J.Fundir(+b.dataset.e, +b.dataset.q)) aviso('Almas fundidas'); break;
    case 'evoluir': if (J.Evoluir(i)) aviso('Minion evoluído'); break;
    case 'melhorar': J.Melhorar(i); break;
    case 'equipar': J.Equipar(i); break;
    case 'desequipar': J.Desequipar(i); break;
    case 'desmanchar': J.Desmanchar(i); break;
    case 'vender-item': J.VenderItem(i); som('toque'); break;
    case 'identificar': if (J.IdentificarItem(i)) { som('bencao'); aviso('Item identificado'); } break;
    case 'reparar': if (J.RepararEquipado(i)) { som('golpe'); aviso('Item reparado'); } break;
    case 'auto-reparo': J.AlternarAutoReparo(); break;
    case 'auto-melhoria': J.AlternarAutoMelhoria(); break;
    case 'repor-pocoes': J.AlternarReporPocoes(); break;
    case 'pergaminho': if (J.ComprarPergaminho()) { som('bencao'); aviso('Pergaminho de Portal comprado'); } break;
    case 'portal-auto': J.AlternarPortalAutomatico(); break;
    case 'livro': if (J.ComprarLivro(i)) { som('bencao'); aviso('Livro de feitiço comprado'); } break;
    case 'limiar-pocao': { const op = i ? [10, 15, 30, 50] : [20, 35, 50, 65], at = +(b.textContent.match(/(\d+)%/) || [0, 0])[1], nx = op[(op.indexOf(at) + 1) % op.length]; J.DefinirLimiarDePocao(i, nx); break; }
    case 'pocao': if (J.ComprarPocao(i)) { som('bencao'); aviso(i ? 'Poção de mana no cinto' : 'Poção de vida no cinto'); } break;
    case 'melhorar-item': if (J.MelhorarEquipado(i)) { som('golpe'); aviso('Item melhorado'); } break;
    case 'imbuir': if (J.ImbuirEquipado(i)) { som('bencao'); aviso('Item imbuído: afixo novo'); } break;
    case 'dificuldade': if (J.DefinirDificuldade(i)) aviso('Dificuldade: ' + DIFICULDADES[i][0] + ' · vale na próxima descida'); break;
    case 'auto-atributos': J.AlternarAutoAtributos(); break;
    case 'redistribuir-atributos': if (J.RedistribuirAtributos()) aviso('Pontos de atributo devolvidos'); break;
    case 'atributo': if (J.GastarAtributo(i, +b.dataset.n || 1)) som('toque'); break;
    case 'reparar-tudo': { const n = J.RepararTudo(); if (n) { som('golpe'); aviso(n + (n > 1 ? ' itens reparados' : ' item reparado')); } else aviso('Falta ouro para reparar'); break; }
    case 'vender-fechados': { const n = J.VenderFechados(); if (n) { som('toque'); aviso(n + ' vendidos sem identificar'); } break; }
    case 'identificar-tudo': { const n = J.IdentificarTudo(); if (n) { som('bencao'); aviso(n + (n > 1 ? ' itens identificados' : ' item identificado')); } else aviso('Falta ouro para identificar'); break; }
    case 'comprar': if (J.ComprarItem(i)) { som('bencao'); aviso('Comprado: foi para o inventário'); } break;
    case 'auto': J.AutoEquipar(); break;
    case 'recomecar': J.Recomecar(); limparArena(true); andar = 0; break;
    case 'novo':
      if (Date.now() - confirmarNovo > 4000) { confirmarNovo = Date.now(); b.textContent = 'Toque de novo para confirmar'; return; }
      J.NovoJogo(); limparArena(true); andar = 0; break;
  }
  salvar(); atualizarPainel(true);
});
document.addEventListener('click', (ev) => {
  const b = ev.target.closest('.abas button[data-aba]');
  if (b) { abasSel[b.parentElement.dataset.lado] = b.dataset.aba; if (J) atualizarPainel(true); return; }
  const f = ev.target.closest('[data-fecha]'); if (f) { $(f.dataset.fecha).classList.remove('aberto'); return; }
  const a = ev.target.closest('[data-abre]'); if (a) { $(a.dataset.abre === 'esq' ? 'dir' : 'esq').classList.remove('aberto'); $(a.dataset.abre).classList.toggle('aberto'); return; }
  // gaveta aberta no celular: toque fora fecha
  if (!ev.composedPath().some((el) => el.classList && (el.classList.contains('lado') || el.id === 'ficha-minion' || el.id === 'cheats' || el.id === 'ajuda' || el.id === 'mapa-grande' || el.id === 'salas'))) { $('esq').classList.remove('aberto'); $('dir').classList.remove('aberto'); }
});
$('vel').addEventListener('click', (ev) => {
  const b = ev.target.closest('button'); if (!b) return;
  velocidade = +b.dataset.v; for (const x of $('vel').querySelectorAll('button')) x.classList.toggle('ativo', x === b);
});

async function iniciar() {
  try {
    const pronto = globalThis.NecroJogo ? Promise.resolve() : new Promise((ok) => globalThis.addEventListener('necro-pronto', ok, { once: true }));
    await Promise.all([carregarArte(), pronto]);
    J = globalThis.NecroJogo;
    let save = ''; try { save = localStorage.getItem(CHAVE) || ''; } catch (e) { /* segue sem save */ }
    J.Iniciar(save);
    if (!save) { $('ajuda').hidden = false; }
    ajustarTela(); window.addEventListener('resize', ajustarTela);
    $('carregando').remove();
    atualizarPainel(true);
    setInterval(() => atualizarPainel(false), 1000);
    setInterval(salvar, 15000);
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) { salvar(); saiuEm = Date.now(); }
      else if (saiuEm) { ausencia((Date.now() - saiuEm) / 1000); saiuEm = 0; }
    });
    try { const h = +localStorage.getItem(CHAVE + '-hora'); if (save && h) ausencia((Date.now() - h) / 1000); } catch (e) { /* sem relógio salvo */ }
    requestAnimationFrame((t) => { ultimo = t; requestAnimationFrame(laco); });
  } catch (e) {
    console.error('falha ao iniciar: ' + (e && e.stack ? e.stack : e));
    let c = $('carregando'); if (!c) { c = document.createElement('div'); c.id = 'carregando'; $('cena').appendChild(c); }
    c.textContent = 'Não foi possível iniciar o jogo neste navegador: ' + (e && e.message ? e.message : e);
  }
}
globalThis.addEventListener('necro-erro', (e) => { const c = $('carregando'); if (c) c.textContent = 'Não foi possível carregar o núcleo do jogo: ' + e.detail; });
iniciar();
