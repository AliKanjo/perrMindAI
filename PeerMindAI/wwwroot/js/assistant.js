/* AI assistant: sends the question with fetch() as JSON and shows the answer
   without reloading the page. The server does the searching and calls the AI. */
(function () {
    'use strict';

    const { $, $$, api, toast, escapeHtml } = window.PeerMind;

    const form = $('#ask-form');
    if (!form) return;   // no chat selected

    const chat = $('#chat');
    const input = $('#question');
    const button = $('button[type="submit"]', form);
    const conversationId = form.dataset.conversation;

    function addBubble(html, className) {
        $('#chat-hello')?.remove();
        const bubble = document.createElement('div');
        bubble.className = 'bubble ' + className;
        bubble.innerHTML = html;
        chat.appendChild(bubble);
        chat.scrollTop = chat.scrollHeight;
        return bubble;
    }

    function answerHtml(answer) {
        const sources = answer.sources
            ? `<div class="sources">Sources: ${escapeHtml(answer.sources)}</div>`
            : '';
        return `<div class="answer-text">${escapeHtml(answer.content)}</div>${sources}`;
    }

    async function ask(question) {
        question = question.trim();
        if (!question) {
            toast('Please type a question.', 'bad');
            input.focus();
            return;
        }

        addBubble(escapeHtml(question), 'me');
        const thinking = addBubble('<span class="sub">Searching your materials…</span>', 'ai');
        input.value = '';
        input.disabled = button.disabled = true;

        try {
            const data = await api(`/assistant/${conversationId}/ask`, { method: 'POST', body: { question } });
            thinking.innerHTML = answerHtml(data.answer);
            if (data.answer.isGrounded === false) thinking.classList.add('ungrounded');
        } catch (error) {
            // nothing was saved: show the error and give the question back
            thinking.remove();
            chat.lastElementChild?.remove();
            input.value = question;
            toast(error.message, 'bad');
        } finally {
            input.disabled = button.disabled = false;
            input.focus();
            chat.scrollTop = chat.scrollHeight;
        }
    }

    form.addEventListener('submit', event => {
        event.preventDefault();
        ask(input.value);
    });

    $$('[data-prompt]').forEach(chip => chip.addEventListener('click', () => ask(chip.dataset.prompt)));

    chat.scrollTop = chat.scrollHeight;
})();
