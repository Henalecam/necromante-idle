// Desenho e interface da versão de navegador. As regras do jogo rodam no núcleo C# (WebAssembly): aqui só se desenha.
const ESPECIES = ['esqueleto', 'zumbi', 'diabrete', 'necromante', 'arqueiroossudo', 'acolitocaido', 'abadessacosturada', 'caodecripta', 'esqueleto_elite'];
const ACAO = { Parado: 'parado', Andando: 'andar', Atacando: 'atacar', Conjurando: 'conjurar', Morrendo: 'morrer' };
const TPS = 10, CHAVE = 'necromante-idle-save-v1';
const $ = (id) => document.getElementById(id);
const tela = $('tela'), ctx = tela.getContext('2d');

function imagem(src) {
  return new Promise((ok, erro) => { const i = new Image(); i.onload = () => ok(i); i.onerror = () => erro(new Error('imagem ' + src)); i.src = src; });
}
async function json(src) { return (await fetch(src)).json(); }

const monstros = {};
let arena, chao, sangue, sctx;

async function carregarArte() {
  arena = await json('arte/arena/arena.json');
  chao = await imagem('arte/arena/arena_chao.webp');
  sangue = document.createElement('canvas'); sangue.width = chao.width; sangue.height = chao.height; sctx = sangue.getContext('2d');
  await Promise.all(ESPECIES.map(async (n) => {
    const meta = await json(`arte/${n}/${n}.json`);
    const acoes = {};
    await Promise.all(meta.acoes.map(async (a) => { acoes[a.nome] = { ...a, img: await imagem(`arte/${n}/${a.arquivo.replace('.png', '.webp')}`) }; }));
    monstros[n] = { meta, acoes, W: meta.largura_frame, H: meta.altura_frame, pivo: meta.pivo_px };
  }));
}

function monstro(especie, elite) {
  const n = especie.toLowerCase();
  return (elite && monstros[n + '_elite']) || monstros[n] || monstros.esqueleto;
}
function direcao(dx, dy) { return Math.round(((Math.atan2(dx, -dy) * 180 / Math.PI + 360) % 360) / 45) % 8; }
function escHtml(t) { return String(t).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }
function naTela(x, y) {
  const hc = chao.height - arena.topo_px;
  return [(arena.margem_sim + x) * arena.px_por_unidade_x, arena.topo_px + hc - (arena.margem_sim + y) * arena.px_por_unidade_y];
}
const rnd = (a, b) => a + Math.random() * (b - a);
function gauss() { return (Math.random() + Math.random() + Math.random() - 1.5) * 1.2; }

function mancha(x, y, raio, n, cor = [70, 4, 8]) {
  const [px, py] = naTela(x, y);
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
let tempoSim = 0, andar = 0, mana = 0, necro = null, tremor = 0, saque = [0, 0, 0], poderes = [0, 0, 0, 0], oferta = null, ativos = [], pactos = [], ofertaMostrada = '';
const COR_MORTE = { Queimado: '20,14,12', Congelado: '120,170,210', Putrefato: '60,90,25', Eletrizado: '40,40,60', Oco: '50,30,80' };
const tinta = document.createElement('canvas'), tctx = tinta.getContext('2d');

function limparArena() { mortos.clear(); cadaverDe.clear(); sctx.clearRect(0, 0, sangue.width, sangue.height); particulas = []; }

function aviso(texto) {
  if (silencio) return;
  const a = $('aviso'); a.textContent = texto; a.style.opacity = 1;
  clearTimeout(aviso.t); aviso.t = setTimeout(() => { a.style.opacity = 0; }, 2200);
}

function ler(texto) {
  const vivos = new Set(); orbes = []; projeteis = []; pocas = []; oferta = null; ativos = [];
  for (const ln of texto.split('\n')) {
    if (!ln) continue;
    const p = ln.split(' ');
    switch (p[0]) {
      case 'T': {
        const a = +p[1];
        if (a !== andar) { if (andar) limparArena(); andar = a; aviso('Andar ' + a); }
        mana = +p[2] / Math.max(1, +p[3]); saque = [+p[4] || 0, +p[5] || 0, +p[6] || 0];
        poderes = [+p[7] || 0, +p[8] || 0, +p[9] || 0, +p[10] || 0]; break;
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
        if (s.necro) necro = s;
        break;
      }
      case 'O': orbes.push([+p[2], +p[3], +p[1]]); if (!dica && !silencio) { dica = true; aviso('Toque nos orbes verdes para ceifar na hora'); } break;
      case 'P': projeteis.push([+p[2], +p[3], +p[4], +p[5], p[6] === '1']); break;
      case 'S': pocas.push([+p[2], +p[3], +p[4]]); break;
      case 'K': oferta = [+p[1], +p[2], +p[3], +p[4]]; break;
      case 'A': ativos = p.slice(1).map(Number); break;
      case 'V': evento(p); break;
      case 'F': fimDeExpedicao(p); break;
    }
  }
  for (const id of estado.keys()) if (!vivos.has(id)) estado.delete(id);
}

function evento(v) {
  if (silencio && v[1] !== 'CadaverCriado' && v[1] !== 'CadaverConsumido' && v[1] !== 'CadaverRemovido' && v[1] !== 'Morte') return;
  const tipo = v[1], x = +v[10], y = +v[11], dx = +v[12], dy = +v[13];
  if (tipo === 'Dano' && +v[4] > 0) {
    if (Math.random() < 0.6) mancha(x + rnd(-0.3, 0.3), y + rnd(-0.2, 0.2), 5, 2);
    for (let i = 0; i < 3; i++) particulas.push([x, y, 0.9, rnd(-1.5, 1.5), rnd(-1, 1), rnd(1, 3.5), '#78080c', 0.6]);
    const alvo = estado.get(+v[3]);
    if (alvo && (v[6] === '1' || +v[4] >= 1)) numeros.push({ x, y, v: Math.round(+v[4]), crit: v[6] === '1', t: 0.8, amigo: alvo && !alvo.inimigo });
  } else if (tipo === 'Morte') {
    const id = +v[3], s = estado.get(id); if (!s) return;
    const over = v[8] === '1';
    mortos.set(id, { ...s, t: tempoSim, over, cond: v[7] });
    mancha(x, y, over ? 26 : 16, over ? 14 : 8);
    if (over || s.chefe) tremor = s.chefe ? 0.5 : 0.18;
    const f = over ? 5 : 2.5;
    for (let i = 0, k = over ? 46 : 14; i < k; i++)
      particulas.push([x, y, 0.8, dx * f * Math.random() + rnd(-f, f) * 0.6, dy * f * Math.random() + rnd(-f, f) * 0.4, rnd(1.5, over ? 6.5 : 4),
        over ? ['#82080c', '#5a0408', '#c8bea0'][Math.random() * 3 | 0] : '#78080c', rnd(0.5, 1.1)]);
  } else if (tipo === 'CadaverCriado') cadaverDe.set(+v[3], +v[2]);
  else if (tipo === 'CadaverConsumido' || tipo === 'CadaverRemovido') {
    const id = cadaverDe.get(+v[3]); cadaverDe.delete(+v[3]);
    if (mortos.has(id)) {
      if (tipo === 'CadaverConsumido') {
        mancha(x, y, 22, 12);
        for (let i = 0; i < 30; i++) particulas.push([x, y, 0.4, rnd(-4, 4), rnd(-3, 3), rnd(2, 6), Math.random() < 0.5 ? '#82080c' : '#c8bea0', rnd(0.5, 1)]);
      }
      mortos.delete(id);
    }
  } else if (tipo === 'FeiticoLancado') {
    const n = estado.get(+v[2]);
    if (n && v[14] !== '0') raios.push([n.x, n.y, x, y, 0.4]);
  } else if (tipo === 'Explosao') {
    raios.push([x, y, x, y, 0.35, +v[4] || 1.5]);
    tremor = Math.max(tremor, 0.15);
  } else if (tipo === 'AlmaCeifada') {
    if (v[6] === '1') { aviso('Alma melhorada pelo toque!'); for (let i = 0; i < 24; i++) particulas.push([x, y, 1, rnd(-3, 3), rnd(-2, 2), rnd(3, 7), '#78ffaa', rnd(0.6, 1.1)]); }
    for (let i = 0; i < 14; i++) particulas.push([x, y, 1, rnd(-1.5, 1.5), rnd(-1, 1), rnd(2, 5), '#78ffaa', rnd(0.4, 0.8)]);
  }
}

function fimDeExpedicao(p) {
  aviso(`Expedição encerrada no andar ${p[1]} · ${p[2]} almas · ${p[3]} itens`);
  limparArena(); andar = 0; if (!silencio) { salvar(); atualizarPainel(true); }
}

function quadro(m, acao, dir, tempo) {
  let a = m.acoes[acao];
  if (!a) a = (acao === 'conjurar' && m.acoes.atacar) || (acao === 'atacar' && m.acoes.conjurar) || m.acoes.parado;
  let f = Math.floor(tempo * a.fps);
  f = a.loop ? f % a.frames : Math.min(f, a.frames - 1);
  return [a.img, f * m.W, dir * m.H];
}

let camX = 0, dica = false;
try { dica = localStorage.getItem(CHAVE + '-dica') === '1'; } catch (e) { /* sem armazenamento */ }
function desenhar(dt) {
  const larg = tela.width, alt = tela.height, esc = alt / chao.height;
  const visivel = larg / esc;
  let alvo = chao.width / 2 - visivel / 2;
  if (visivel < chao.width && necro) {
    // tela estreita: a câmera segue a briga (e os orbes, que são alvo de toque); o necromante pode ficar fora do quadro
    let soma = 0, n = 0;
    for (const s of estado.values()) if (s.viva && !s.necro) { soma += naTela(s.rx, s.ry)[0]; n++; }
    for (const o of orbes) { soma += naTela(o[0], o[1])[0] * 3; n += 3; }
    const nx = naTela(necro.rx, necro.ry)[0];
    alvo = (n ? soma / n : nx) - visivel / 2;
  }
  alvo = Math.max(0, Math.min(Math.max(0, chao.width - visivel), alvo));
  if (visivel >= chao.width) alvo = (chao.width - visivel) / 2;
  camX += (alvo - camX) * Math.min(1, dt * 3);
  ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.fillStyle = '#060406'; ctx.fillRect(0, 0, larg, alt);
  const tx = tremor > 0 ? rnd(-1, 1) * tremor * 14 : 0, ty = tremor > 0 ? rnd(-1, 1) * tremor * 9 : 0; tremor = Math.max(0, tremor - dt);
  ctx.setTransform(esc, 0, 0, esc, (-camX + tx) * esc, ty * esc);
  ctx.drawImage(chao, 0, 0); ctx.drawImage(sangue, 0, 0);

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
    lista.push([s, ACAO[s.anim] || 'parado', tempoSim - s.t, false]);
  }
  lista.sort((a, b) => b[0].ry - a[0].ry);
  for (const [s, acao, tempo, morto] of lista) {
    const m = monstro(s.especie, s.elite), [px, py] = naTela(s.rx, s.ry);
    if (!morto && !s.inimigo) { ctx.strokeStyle = 'rgba(60,255,140,0.3)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.ellipse(px, py, 22, 9, 0, 0, 7); ctx.stroke(); }
    const [img, sx, sy] = quadro(m, acao, s.dir, tempo);
    const e = s.chefe ? 1 : 1;
    const dx0 = Math.round(px - m.pivo[0] * e), dy0 = Math.round(py - (m.H - m.pivo[1]) * e);
    if (morto && COR_MORTE[s.cond]) {
      // cadáver tingido pela condição da morte (queimado, congelado, putrefato...)
      if (tinta.width !== m.W || tinta.height !== m.H) { tinta.width = m.W; tinta.height = m.H; }
      tctx.globalCompositeOperation = 'source-over'; tctx.clearRect(0, 0, m.W, m.H); tctx.drawImage(img, sx, sy, m.W, m.H, 0, 0, m.W, m.H);
      tctx.globalCompositeOperation = 'source-atop'; tctx.fillStyle = `rgba(${COR_MORTE[s.cond]},0.45)`; tctx.fillRect(0, 0, m.W, m.H);
      ctx.drawImage(tinta, dx0, dy0);
    } else ctx.drawImage(img, sx, sy, m.W, m.H, dx0, dy0, m.W * e, m.H * e);
    if (!morto && s.vida < 0.999) {
      const w = s.chefe ? 70 : 30, topo = py - (s.chefe ? 150 : 108);
      ctx.fillStyle = 'rgba(0,0,0,0.8)'; ctx.fillRect(px - w / 2, topo, w, 4);
      ctx.fillStyle = s.inimigo ? '#be1414' : (s.necro ? '#e0c060' : '#3cc878'); ctx.fillRect(px - w / 2, topo, w * Math.max(0, s.vida), 4);
    }
  }

  // orbes por cima de tudo: são alvo de toque
  for (const [x, y] of orbes) {
    const [px, py] = naTela(x, y), b = Math.sin(tempoSim * 5 + x) * 4;
    const pulso = 1 + 0.25 * Math.sin(tempoSim * 8);
    for (const [r, a] of [[24 * pulso, 0.14], [13, 0.4], [6, 1]]) { ctx.fillStyle = `rgba(120,255,170,${a})`; ctx.beginPath(); ctx.arc(px, py - 30 + b, r, 0, 7); ctx.fill(); }
  }

  for (const [x, y, dx, dy, lanca] of projeteis) {
    const [px, py] = naTela(x, y), ddx = dx * 16, ddy = -dy * 8;
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
    if (r[5]) {
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
    if (p[2] <= 0) { if (p[6] !== '#c8bea0' && p[6] !== '#78ffaa') mancha(p[0], p[1], 4, 1); p[7] = 0; continue; }
    const [px, py] = naTela(p[0], p[1]);
    ctx.fillStyle = p[6]; ctx.fillRect(px - 1.5, py - p[2] * arena.px_por_unidade_x * 0.6 - 1.5, 3, 3);
  }
  particulas = particulas.filter((p) => p[7] > 0);
  if (particulas.length > 900) particulas.splice(0, particulas.length - 900);
  ctx.textAlign = 'center';
  for (const n of numeros) {
    const [px, py] = naTela(n.x, n.y); n.t -= dt;
    ctx.globalAlpha = Math.min(1, n.t * 2.5); ctx.font = (n.crit ? 'bold 22px' : '15px') + ' Georgia';
    ctx.fillStyle = n.amigo ? '#ff6a6a' : (n.crit ? '#ffd84a' : '#f0e8d8');
    ctx.strokeStyle = '#000'; ctx.lineWidth = 3;
    const yy = py - 95 - (0.8 - n.t) * 45; ctx.strokeText(n.v, px, yy); ctx.fillText(n.v, px, yy);
  }
  ctx.globalAlpha = 1; numeros = numeros.filter((n) => n.t > 0);
  if (numeros.length > 60) numeros.splice(0, numeros.length - 60);
  const sq = `+${fmt(saque[0])} ossos · ${saque[1]} almas · ${saque[2]} itens`; if (sq !== desenhar.sq) { desenhar.sq = sq; $('t-saque').textContent = sq; }
  // encruzilhada: três pactos, um toque, tempo contado
  const enc = $('encruzilhada'), chave = oferta && pactos.length ? oferta.slice(0, 3).join(',') : '';
  if (chave !== ofertaMostrada) {
    ofertaMostrada = chave; enc.hidden = !chave;
    if (chave) enc.innerHTML = '<div class="cab"><span>Encruzilhada · escolha um pacto</span><span id="enc-t"></span></div><div class="ops">' +
      oferta.slice(0, 3).map((id, i) => `<button data-o="${i}"><b>${escHtml(pactos[id].nome)}</b><span class="g">${escHtml(pactos[id].ganho)}</span><span class="p">${escHtml(pactos[id].preco)}</span></button>`).join('') + '</div>';
  }
  if (chave) { const t = $('enc-t'); if (t) t.textContent = Math.ceil(oferta[3]) + 's'; }
  const tp = ativos.length && pactos.length ? ativos.map((id) => pactos[id].nome.replace('Pacto d', 'd').replace(/^d[aeo] /, '')).join(' · ') : '';
  if (tp !== desenhar.tp) { desenhar.tp = tp; $('t-pactos').textContent = tp ? 'Pactos: ' + tp : ''; }
  const bd = $('b-detonar'), bf = $('b-frenesi');
  const td = poderes[0] > 0 ? `Detonar ${Math.ceil(poderes[0])}s` : `Detonar (${poderes[3]})`, tf = poderes[2] > 0 ? 'FRENESI!' : poderes[1] > 0 ? `Frenesi ${Math.ceil(poderes[1])}s` : 'Frenesi';
  if (bd.textContent !== td) bd.textContent = td; if (bf.textContent !== tf) bf.textContent = tf;
  bd.disabled = poderes[0] > 0 || poderes[3] === 0; bf.disabled = poderes[1] > 0; bf.classList.toggle('ligado', poderes[2] > 0);
  $('mana').firstElementChild.style.width = (mana * 100).toFixed(0) + '%';
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
  const alt = Math.max(220, Math.min(larg * chao.height / chao.width * 1.0, window.innerHeight * 0.5));
  const altFinal = larg < 700 ? Math.min(window.innerHeight * 0.42, 340) : alt;
  tela.width = Math.round(larg * razao); tela.height = Math.round(Math.max(200, altFinal) * razao);
  tela.style.height = Math.max(200, altFinal) + 'px';
}

function salvar() { try { localStorage.setItem(CHAVE, J.Salvar()); localStorage.setItem(CHAVE + '-hora', String(Date.now())); } catch (e) { /* sem armazenamento: joga-se sem salvar */ } }

// ---- painéis ----
let aba = 'exercito', painelAnterior = '', missaoAnterior = '';
const fmt = (n) => n >= 1e6 ? (n / 1e6).toFixed(2) + 'M' : n >= 1e4 ? (n / 1e3).toFixed(1) + 'k' : String(n);
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const ESPACOS = ['Arma', 'Mão secundária', 'Elmo', 'Peitoral', 'Luvas', 'Botas', 'Amuleto', 'Anel', 'Anel'];
const TRACOS = { 0: ['Faminto', 'rouba vida a cada golpe'], 1: ['Frenético', 'ataca mais rápido'], 2: ['Pétreo', 'mais armadura'], 3: ['Rancoroso', 'explode ao morrer'],
  4: ['Leal', 'sofre menos dano perto do necromante'], 5: ['Incandescente', 'parte do dano vira fogo'], 11: ['Colosso', 'muito mais vida'], 12: ['Carniceiro', 'muito mais dano'], 13: ['Agourento', 'mais acertos críticos'] };
const tracosHtml = (t) => t.length ? '<small>' + t.map((k) => TRACOS[k] ? `<b style="color:#c8a86a;font-weight:normal">${TRACOS[k][0]}</b>: ${TRACOS[k][1]}` : '').join(' · ') + '</small>' : '';
const QUALIDADES = ['Fraca', 'Comum', 'Vigorosa', 'Ancestral', 'Profana'];

function itemHtml(it) { return `<span class="r${it.r}">${esc(it.nome)}</span> <span class="nota">nv ${it.nivel}</span><small>${it.afixos.map(esc).join(' · ')}</small>`; }

function atualizarPainel(forcar) {
  const d = JSON.parse(J.Painel());
  pactos = d.pactos;
  $('t-andar').textContent = `Andar ${d.andar}` + (d.recorde ? ` · recorde ${d.recorde}` : '');
  $('t-nivel').textContent = d.nivel; $('t-ossos').textContent = fmt(d.ossos); $('t-ess').textContent = fmt(d.essencia);
  $('t-cmd').textContent = `${d.comando}/${d.comandoMax}`;
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
  const nomes = ['exercito', 'necropole', 'equip', 'bestiario'];
  d.abertos[3] = d.abertos[0];   // o Bestiário abre junto com o Exército
  for (const b of $('abas').children) b.hidden = !d.abertos[nomes.indexOf(b.dataset.aba)];
  if (!d.abertos[nomes.indexOf(aba)]) {
    const prox = nomes.find((n, i) => d.abertos[i]);
    if (prox) { aba = prox; for (const x of $('abas').children) x.classList.toggle('ativo', x.dataset.aba === aba); }
  }
  let h = '';
  if (!d.abertos.some(Boolean)) h = '<p class="nota">Assista à descida e cumpra a primeira missão. O Exército, a Necrópole e o Equipamento se abrem conforme você avança.</p>';
  else if (aba === 'exercito') {
    h += `<h3>Minions (${d.comando}/${d.comandoMax} de comando)</h3>`;
    if (!d.minions.length) h += '<p class="nota">Nenhum minion ainda. Ceife almas na masmorra e erga-as aqui.</p>';
    d.minions.forEach((m, i) => {
      h += `<div class="linha"><div class="corpo"><span class="q${m.q}">${esc(m.nome)}</span> <span class="nota">nv ${m.nivel}/${m.max} · ${QUALIDADES[m.q]} · estágio ${m.estagio} · ${m.comando} de comando</span>${tracosHtml(m.tracos)}` +
        `<div class="xp"><div style="width:${Math.round(m.xp * 100)}%"></div></div></div>` +
        (m.evoluir >= 0 ? `<button data-ac="evoluir" data-i="${i}" ${m.evoluir > d.essencia ? 'disabled' : ''}>Evoluir · ${fmt(m.evoluir)} Ess.</button>` : '') +
        `<button data-ac="alternar" data-i="${i}" class="${m.ativo ? 'ativo' : ''}">${m.ativo ? 'Em campo' : 'Na reserva'}</button></div>`;
    });
    if (d.fusoes.length) {
      h += '<h3>Fusão de almas</h3><p class="nota">Três almas iguais viram uma de qualidade acima, que herda os traços delas. Um corpo melhor ocupa o mesmo Comando que um fraco.</p>';
      d.fusoes.forEach((f) => { h += `<div class="linha"><div class="corpo">3 × <span class="q${f.q}">${esc(f.nome)} ${QUALIDADES[f.q]}</span> → <span class="q${f.q + 1}">${QUALIDADES[f.q + 1]}</span><small>você tem ${f.n}</small></div><button data-ac="fundir" data-e="${f.e}" data-q="${f.q}" ${f.pode ? '' : 'disabled'}>Fundir · ${fmt(f.custo)} Ess.</button></div>`; });
    }
    h += `<h3>Filactério (${d.almas.length}/${d.filacterioMax} almas)</h3>`;
    if (!d.almas.length) h += '<p class="nota">Vazio. As almas ceifadas chegam ao fim de cada expedição.</p>';
    d.almas.forEach((a, i) => { h += `<div class="linha"><div class="corpo"><span class="q${a.q}">${esc(a.nome)}</span> <span class="nota">${QUALIDADES[a.q]} · ${a.comando} de comando</span>${tracosHtml(a.tracos)}</div><button data-ac="erguer" data-i="${i}" ${a.custo > d.essencia ? 'disabled' : ''}>Erguer · ${fmt(a.custo)} Ess.</button></div>`; });
    h += '<p class="nota">Mudanças no exército valem a partir da próxima expedição.</p><div class="rodape"><button data-ac="recomecar">Recomeçar expedição agora</button></div>';
  } else if (aba === 'bestiario') {
    h += `<h3>Bestiário</h3><p class="nota">Cada marco de mortes de uma espécie dá um nível de Domínio: seus minions dessa espécie ganham +${d.bestiario[0].porNivel}% de vida e dano por nível.</p>`;
    d.bestiario.forEach((b) => {
      if (!b.mortes && !b.conhecida) { h += '<div class="linha"><div class="corpo"><span class="nota">??? · espécie ainda não encontrada</span></div></div>'; return; }
      const pct = b.prox ? Math.min(100, Math.round(100 * b.mortes / b.prox)) : 100;
      h += `<div class="linha"><div class="corpo">${esc(b.nome)} <span class="nota">Domínio ${b.dominio}/${b.max}${b.bonus ? ` · +${b.bonus}% vida e dano` : ''}</span>` +
        `<small>${b.prox ? `${fmt(b.mortes)} de ${fmt(b.prox)} mortes para o próximo nível` : `${fmt(b.mortes)} mortes · Domínio completo`}</small><div class="xp"><div style="width:${pct}%;background:#a01420"></div></div></div></div>`;
    });
  } else if (aba === 'necropole') {
    h += '<h3>Construções</h3>';
    d.construcoes.forEach((c, i) => {
      const custo = c.nivel >= c.max ? 'Máximo' : !c.aberta ? `Abre no andar ${c.andar}` : `${fmt(c.ossos)} Ossos` + (c.essencia ? ` · ${fmt(c.essencia)} Ess.` : '');
      h += `<div class="linha"><div class="corpo">${esc(c.nome)} <span class="nota">nv ${c.nivel}/${c.max}</span><small>${esc(c.efeito)}</small></div><button data-ac="melhorar" data-i="${i}" ${c.pode ? '' : 'disabled'}>${custo}</button></div>`;
    });
    h += '<div class="rodape"><button data-ac="novo">Apagar save e começar de novo</button></div>';
  } else {
    h += '<h3>Equipado</h3>';
    d.equipados.forEach((it, i) => { h += `<div class="linha"><div class="corpo"><span class="nota">${ESPACOS[i]}</span> ${it ? itemHtml(it) : '<span class="nota">— vazio —</span>'}</div>${it ? `<button data-ac="desequipar" data-i="${i}">Tirar</button>` : ''}</div>`; });
    h += `<h3>Inventário (${d.inventario.length}/${d.inventarioMax})</h3><div class="rodape" style="margin:0 0 8px"><button data-ac="auto">Equipar os melhores</button></div>`;
    if (!d.inventario.length) h += '<p class="nota">Vazio. Os itens caem dos inimigos e chegam ao fim de cada expedição.</p>';
    d.inventario.forEach((it, i) => { h += `<div class="linha"><div class="corpo">${itemHtml(it)}</div><button data-ac="equipar" data-i="${i}">Equipar</button><button data-ac="desmanchar" data-i="${i}">+${fmt(it.ess)} Ess.</button></div>`; });
    h += '<p class="nota">Itens equipados valem a partir da próxima expedição.</p>';
  }
  if (forcar || h !== painelAnterior) { painelAnterior = h; $('painel').innerHTML = h; }
}

let confirmarNovo = 0;
// toque na arena: ceifa o orbe mais próximo do dedo
tela.addEventListener('pointerdown', (ev) => {
  if (!J || !orbes.length) return;
  const r = tela.getBoundingClientRect(), k = chao.height / r.height;
  const ax = (ev.clientX - r.left) * k + camX, ay = (ev.clientY - r.top) * k;
  let melhor = null, md = 70 * 70;
  for (const o of orbes) { const [px, py] = naTela(o[0], o[1]); const d = (px - ax) ** 2 + (py - 30 - ay) ** 2; if (d < md) { md = d; melhor = o; } }
  if (melhor) { J.Ceifar(melhor[2]); try { localStorage.setItem(CHAVE + '-dica', '1'); } catch (e) { /* tudo bem */ } }
});
// usado pelos testes automatizados: posição na página do primeiro orbe visível
window.__orbeNaTela = () => {
  if (!orbes.length) return null;
  const r = tela.getBoundingClientRect(), k = r.height / chao.height, [px, py] = naTela(orbes[0][0], orbes[0][1]);
  const x = r.left + (px - camX) * k, y = r.top + (py - 30) * k;
  return x > r.left + 4 && x < r.right - 4 ? [x, y] : null;
};
window.__dbg = () => [orbes.length, orbes[0] ? naTela(orbes[0][0], orbes[0][1])[0] | 0 : 0, camX | 0, tela.getBoundingClientRect().width, chao.height];
$('encruzilhada').addEventListener('click', (ev) => { const b = ev.target.closest('button[data-o]'); if (b && J) { J.Pacto(+b.dataset.o); aviso('Pacto selado'); } });
$('b-detonar').addEventListener('click', () => { if (J) J.Detonar(); });
$('b-frenesi').addEventListener('click', () => { if (J) J.Frenesi(); });
$('missao').addEventListener('click', (ev) => {
  const b = ev.target.closest('button[data-ac="resgatar"]'); if (!b || !J) return;
  if (J.Resgatar()) { aviso('Missão cumprida'); salvar(); atualizarPainel(true); }
});
$('painel').addEventListener('click', (ev) => {
  const b = ev.target.closest('button[data-ac]'); if (!b || !J) return;
  const i = +b.dataset.i;
  switch (b.dataset.ac) {
    case 'alternar': J.AlternarMinion(i); break;
    case 'erguer': J.Erguer(i); break;
    case 'fundir': if (J.Fundir(+b.dataset.e, +b.dataset.q)) aviso('Almas fundidas'); break;
    case 'evoluir': if (J.Evoluir(i)) aviso('Minion evoluído'); break;
    case 'melhorar': J.Melhorar(i); break;
    case 'equipar': J.Equipar(i); break;
    case 'desequipar': J.Desequipar(i); break;
    case 'desmanchar': J.Desmanchar(i); break;
    case 'auto': J.AutoEquipar(); break;
    case 'recomecar': J.Recomecar(); limparArena(); andar = 0; break;
    case 'novo':
      if (Date.now() - confirmarNovo > 4000) { confirmarNovo = Date.now(); b.textContent = 'Toque de novo para confirmar'; return; }
      J.NovoJogo(); limparArena(); andar = 0; break;
  }
  salvar(); atualizarPainel(true);
});
$('abas').addEventListener('click', (ev) => {
  const b = ev.target.closest('button'); if (!b) return;
  aba = b.dataset.aba; for (const x of $('abas').children) x.classList.toggle('ativo', x === b);
  if (J) atualizarPainel(true);
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
