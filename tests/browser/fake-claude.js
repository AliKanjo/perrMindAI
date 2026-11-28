// Local stand-in for the AI providers, used only by the automated tests.
// It answers like the Anthropic Messages API (/v1/messages) and like Groq's
// OpenAI-compatible API (/openai/v1/chat/completions), checks the key and headers,
// and can simulate an "I cannot answer" reply, an error and a time-out.
const http = require('http');
const PORT = Number(process.env.PORT || 5099);
let calls = 0;

function quizJson(prompt, system) {
    const count = Number((system.match(/Write exactly (\d+) questions/) || [])[1] || 5);
    const pages = [...prompt.matchAll(/\(page (\d+)\)/g)].map(m => Number(m[1]));
    const topics = ((prompt.match(/Course topics: (.*)/) || [])[1] || '').split(', ').filter(t => t && t !== '(none)');
    const questions = [];
    for (let i = 0; i < count; i++) {
        questions.push({
            question: `Practice question ${i + 1}: which statement matches the material?`,
            options: [`Statement A${i}`, `Statement B${i}`, `Statement C${i}`, `Statement D${i}`],
            correct: i % 4,
            explanation: `Statement ${'ABCD'[i % 4]}${i} is what the material says.`,
            page: pages[i % Math.max(1, pages.length)] || 1,
            topic: topics.length ? topics[i % topics.length] : ''
        });
    }
    // one broken question (only three options): the application must skip it
    questions.push({ question: 'Broken question', options: ['x', 'y', 'z'], correct: 0 });
    return JSON.stringify({ title: 'Practice quiz', questions });
}

function answerFor(system, prompt) {
    if (system.includes('multiple-choice practice quizzes')) {
        if (prompt.includes('broken-material')) return 'Sorry, here is no JSON at all.';
        return quizJson(prompt, system);
    }
    const question = (prompt.split('Question:').pop() || '').toLowerCase();
    if (question.includes('invented')) return 'NOT_IN_MATERIALS';
    const excerpts = (prompt.match(/\[\d+\] \(/g) || []).length;
    return `Based on your notes: this answer uses the most relevant excerpt [1]${excerpts > 1 ? ' and also [2]' : ''}.`;
}

http.createServer((req, res) => {
    let body = '';
    req.on('data', chunk => body += chunk);
    req.on('end', () => {
        const send = (code, obj) => { res.writeHead(code, { 'content-type': 'application/json' }); res.end(JSON.stringify(obj)); };
        if (req.url === '/calls') return send(200, { calls });

        const isAnthropic = req.method === 'POST' && req.url === '/v1/messages';
        const isGroq = req.method === 'POST' && req.url === '/openai/v1/chat/completions';
        if (!isAnthropic && !isGroq) return send(404, { error: { message: 'not found' } });
        calls++;

        // authentication as each provider expects it
        if (isAnthropic && (req.headers['x-api-key'] !== 'test-key' || !req.headers['anthropic-version'])) {
            return send(401, { type: 'error', error: { type: 'authentication_error', message: 'invalid x-api-key' } });
        }
        if (isGroq && req.headers.authorization !== 'Bearer test-key') {
            return send(401, { error: { message: 'Invalid API Key', type: 'invalid_request_error' } });
        }

        let data;
        try { data = JSON.parse(body); } catch { return send(400, {}); }
        const system = isAnthropic ? data.system || '' : (data.messages || []).find(m => m.role === 'system')?.content || '';
        const prompt = (data.messages || []).find(m => m.role === 'user')?.content || '';
        if (!data.model || !data.max_tokens || !system || !prompt) return send(400, { error: { message: 'bad body' } });
        // reasoning models on Groq: the app must keep the thinking short and leave room for it
        if (isGroq && data.model.includes('gpt-oss') && (data.reasoning_effort !== 'low' || data.max_tokens < 2000)) {
            return send(400, { error: { message: 'reasoning settings missing' } });
        }

        const question = (prompt.split('Question:').pop() || '').toLowerCase();
        if (question.includes('crash')) return send(500, { error: { message: 'internal' } });
        const reply = text => isAnthropic
            ? send(200, { id: 'msg_test', type: 'message', role: 'assistant', model: data.model, stop_reason: 'end_turn',
                          content: [{ type: 'text', text }], usage: { input_tokens: 420, output_tokens: 38 } })
            : send(200, { id: 'chatcmpl-test', object: 'chat.completion', model: data.model,
                          choices: [{ index: 0, message: { role: 'assistant', content: text }, finish_reason: 'stop' }],
                          usage: { prompt_tokens: 410, completion_tokens: 36, total_tokens: 446 } });
        if (question.includes('slow')) return setTimeout(() => reply('late'), 6000);
        reply(answerFor(system, prompt));
    });
}).listen(PORT, () => console.log(`fake AI service on ${PORT}`));
