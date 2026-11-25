// A configuração binária do runtime (runtimeconfig.bin) vai embutida aqui para a página depender só de arquivos comuns da web.
const CONFIG_RUNTIME = 'D01NaWNyb3NvZnQuRXh0ZW5zaW9ucy5EZXBlbmRlbmN5SW5qZWN0aW9uLlZlcmlmeU9wZW5HZW5lcmljU2VydmljZVRyaW1tYWJpbGl0eQR0cnVlZlN5c3RlbS5Db21wb25lbnRNb2RlbC5UeXBlQ29udmVydGVyLkVuYWJsZVVuc2FmZUJpbmFyeUZvcm1hdHRlckluRGVzaWdudGltZUxpY2Vuc2VDb250ZXh0U2VyaWFsaXphdGlvbgVmYWxzZR5TeXN0ZW0uR2xvYmFsaXphdGlvbi5JbnZhcmlhbnQEdHJ1ZStTeXN0ZW0uR2xvYmFsaXphdGlvbi5QcmVkZWZpbmVkQ3VsdHVyZXNPbmx5BHRydWU2U3lzdGVtLlJlZmxlY3Rpb24uTWV0YWRhdGEuTWV0YWRhdGFVcGRhdGVyLklzU3VwcG9ydGVkBWZhbHNlOVN5c3RlbS5SZXNvdXJjZXMuUmVzb3VyY2VNYW5hZ2VyLkFsbG93Q3VzdG9tUmVzb3VyY2VUeXBlcwVmYWxzZTxTeXN0ZW0uUnVudGltZS5JbnRlcm9wU2VydmljZXMuQnVpbHRJbkNvbUludGVyb3AuSXNTdXBwb3J0ZWQFZmFsc2VKU3lzdGVtLlJ1bnRpbWUuSW50ZXJvcFNlcnZpY2VzLkVuYWJsZUNvbnN1bWluZ01hbmFnZWRDb2RlRnJvbU5hdGl2ZUhvc3RpbmcFZmFsc2U5U3lzdGVtLlJ1bnRpbWUuSW50ZXJvcFNlcnZpY2VzLkVuYWJsZUNwcENMSUhvc3RBY3RpdmF0aW9uBWZhbHNlVlN5c3RlbS5SdW50aW1lLkludGVyb3BTZXJ2aWNlcy5NYXJzaGFsbGluZy5FbmFibGVHZW5lcmF0ZWRDb21JbnRlcmZhY2VDb21JbXBvcnRJbnRlcm9wBWZhbHNlRVN5c3RlbS5SdW50aW1lLlNlcmlhbGl6YXRpb24uRW5hYmxlVW5zYWZlQmluYXJ5Rm9ybWF0dGVyU2VyaWFsaXphdGlvbgVmYWxzZSZTeXN0ZW0uU3RhcnR1cEhvb2tQcm92aWRlci5Jc1N1cHBvcnRlZAVmYWxzZS1TeXN0ZW0uVGV4dC5FbmNvZGluZy5FbmFibGVVbnNhZmVVVEY3RW5jb2RpbmcFZmFsc2U8U3lzdGVtLlRleHQuSnNvbi5Kc29uU2VyaWFsaXplci5Jc1JlZmxlY3Rpb25FbmFibGVkQnlEZWZhdWx0BHRydWUtU3lzdGVtLlRocmVhZGluZy5UaHJlYWQuRW5hYmxlQXV0b3JlbGVhc2VQb29sBWZhbHNl';
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
