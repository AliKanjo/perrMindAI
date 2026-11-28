// Local stand-in for the Anthropic Messages API, used only for automated tests.
const http = require('http');
let calls = 0;
http.createServer((req, res) => {
  let body = '';
  req.on('data', c => body += c);
  req.on('end', () => {
    if (req.url === '/v1/messages') calls++;
    const send = (code, obj) => { res.writeHead(code, { 'content-type': 'application/json' }); res.end(JSON.stringify(obj)); };
    if (req.url === '/calls') return send(200, { calls });
    if (req.method !== 'POST' || req.url !== '/v1/messages') return send(404, { type: 'error', error: { type: 'not_found_error', message: 'no' } });
    if (req.headers['x-api-key'] !== 'test-key') return send(401, { type: 'error', error: { type: 'authentication_error', message: 'invalid x-api-key' } });
    if (!req.headers['anthropic-version']) return send(400, { type: 'error', error: { type: 'invalid_request_error', message: 'missing version' } });
    let data; try { data = JSON.parse(body); } catch { return send(400, {}); }
    const prompt = data.messages?.[0]?.content || '';
    const question = (prompt.split('Question:').pop() || '').toLowerCase();
    if (!data.model || !data.max_tokens || !data.system) return send(400, { type: 'error', error: { type: 'invalid_request_error', message: 'bad body' } });
    if (question.includes('invented')) return send(200, { content: [{ type: 'text', text: 'NOT_IN_MATERIALS' }], usage: { input_tokens: 300, output_tokens: 4 } });
    if (question.includes('crash')) return send(500, { type: 'error', error: { type: 'api_error', message: 'internal' } });
    if (question.includes('slow')) return setTimeout(() => send(200, { content: [{ type: 'text', text: 'late' }], usage: { input_tokens: 1, output_tokens: 1 } }), 6000);
    const excerpts = (prompt.match(/\[\d+\] \(/g) || []).length;
    send(200, {
      id: 'msg_test', type: 'message', role: 'assistant', model: data.model, stop_reason: 'end_turn',
      content: [{ type: 'text', text: `Based on your notes: this answer uses the most relevant excerpt [1]${excerpts > 1 ? ' and also [2]' : ''}.` }],
      usage: { input_tokens: 420, output_tokens: 38 }
    });
  });
}).listen(5099, () => console.log('fake claude on 5099'));
