// A configuração binária do runtime (runtimeconfig.bin) vai embutida aqui para a página depender só de arquivos comuns da web.
const CONFIG_RUNTIME = '__RUNTIMECONFIG__';
function bytes(b64) { const s = atob(b64), a = new Uint8Array(s.length); for (let i = 0; i < s.length; i++) a[i] = s.charCodeAt(i); return a; }
// o runtime pede esse arquivo por fetch; a resposta sai daqui mesmo
const buscar = globalThis.fetch.bind(globalThis);
globalThis.fetch = (alvo, opcoes) => String(alvo && alvo.url ? alvo.url : alvo).endsWith('runtimeconfig.bin')
  ? Promise.resolve(new Response(bytes(CONFIG_RUNTIME), { status: 200, headers: { 'content-type': 'application/octet-stream' } }))
  : buscar(alvo, opcoes);
try {
  const { dotnet } = await import('./nucleo/dotnet.js');
  const { getAssemblyExports, getConfig } = await dotnet.withConfig({ disableIntegrityCheck: true }).create();
  const exports = await getAssemblyExports(getConfig().mainAssemblyName);
  globalThis.NecroJogo = exports.Jogo;
  globalThis.dispatchEvent(new Event('necro-pronto'));
} catch (e) {
  console.error(e);
  globalThis.dispatchEvent(new CustomEvent('necro-erro', { detail: String(e && e.message ? e.message : e) }));
}
