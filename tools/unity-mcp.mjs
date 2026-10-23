// Project-owned MCP stdio client for the official Unity CLI, with bounded calls.
// No HTTP descriptor/token discovery and no second Editor bridge.
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { readFile, access } from 'node:fs/promises';
import path from 'node:path';

const [mode = 'probe', name = '', argumentFile] = process.argv.slice(2);
const project = path.resolve(process.env.UNITY_PROJECT_PATH || process.cwd());
await access(path.join(project, 'ProjectSettings', 'ProjectVersion.txt'));
const cli = process.env.UNITY_CLI || 'unity';
const child = spawn(cli, ['mcp', '--project-path', project], {
  cwd: project, stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true,
});
let requestId = 0;
let stderr = '';
const pending = new Map();
const lines = createInterface({ input: child.stdout });
child.stderr.setEncoding('utf8');
child.stderr.on('data', chunk => { stderr = (stderr + chunk).slice(-2000); });
lines.on('line', line => {
  let message;
  try { message = JSON.parse(line); } catch { return; }
  if (!pending.has(message.id)) return;
  const request = pending.get(message.id);
  pending.delete(message.id);
  clearTimeout(request.timer);
  if (message.error) request.reject(new Error(JSON.stringify(message.error)));
  else request.resolve(message.result);
});
function rejectAll(error) {
  for (const p of pending.values()) { clearTimeout(p.timer); p.reject(error); }
  pending.clear();
}
child.on('error', rejectAll);
child.on('exit', code => rejectAll(new Error(`Unity MCP process exited (${code}). ${stderr}`)));
function request(method, params, timeoutMs = 30000) {
  return new Promise((resolve, reject) => {
    const id = ++requestId;
    const timer = setTimeout(() => {
      pending.delete(id);
      reject(new Error(`MCP ${method} exceeded ${timeoutMs} ms`));
    }, timeoutMs);
    pending.set(id, { resolve, reject, timer });
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n');
  });
}
function notify(method, params = {}) {
  child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method, params }) + '\n');
}

try {
  const initialized = await request('initialize', {
    protocolVersion: '2025-03-26', capabilities: {},
    clientInfo: { name: 'game-test-project-client', version: '0.1.0' },
  });
  notify('notifications/initialized');
  const listed = await request('tools/list', {});
  if (!Array.isArray(listed.tools) || !listed.tools.length) throw new Error('Unity returned an empty MCP tool catalog');
  if (mode === 'list') {
    console.log(JSON.stringify(listed.tools.filter(t => !name || t.name.includes(name)), null, 2));
  } else if (mode === 'call') {
    if (!listed.tools.some(t => t.name === name)) throw new Error(`Tool not advertised by Unity: ${name}`);
    const args = argumentFile ? JSON.parse(await readFile(argumentFile, 'utf8')) : {};
    const result = await request('tools/call', { name, arguments: args });
    console.log(JSON.stringify(result, null, 2));
    if (result.isError) process.exitCode = 1;
  } else if (mode === 'probe') {
    const status = listed.tools.find(t => t.name === 'editor_status');
    const scenes = listed.tools.find(t => t.name === 'list_open_scenes');
    if (!status || !scenes) throw new Error('Required read-only Editor/scene tools were not advertised');
    const editor = await request('tools/call', { name: status.name, arguments: {} });
    const openScenes = await request('tools/call', { name: scenes.name, arguments: {} });
    console.log(JSON.stringify({ protocol: initialized.protocolVersion, tools: listed.tools.length, editor, openScenes }, null, 2));
    if (editor.isError || openScenes.isError) process.exitCode = 1;
  } else throw new Error('Usage: unity-mcp.mjs probe | list [pattern] | call tool [arguments.json]');
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
} finally {
  lines.close();
  child.stdin.end();
  child.kill();
}
