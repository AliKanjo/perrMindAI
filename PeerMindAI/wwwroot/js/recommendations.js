/* Student dashboard: dismiss a study recommendation without reloading the page (fetch + JSON). */
(function () {
    'use strict';

    const { $, $$, api, toast } = window.PeerMind;

    $$('[data-dismiss-recommendation]').forEach(button => button.addEventListener('click', async () => {
        const row = button.closest('[data-recommendation]');
        button.disabled = true;
        try {
            await api(`/recommendations/${row.dataset.recommendation}/dismiss`, { method: 'POST' });
            row.hidden = true;
            row.classList.add('hide');
            if (!$('[data-recommendation]:not(.hide)')) {
                $('#recommendations').insertAdjacentHTML('beforeend', '<p class="sm sub">All recommendations dismissed.</p>');
            }
        } catch (error) {
            button.disabled = false;
            toast(error.message, 'bad');
        }
    }));
})();
