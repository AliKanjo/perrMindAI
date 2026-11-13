/* Tutor "Booking requests": accept, decline and complete sessions with fetch().
   The server answers in JSON and only the affected card is updated. */
(function () {
    'use strict';

    const { $, $$, api, badge, toast } = window.PeerMind;

    const STATUS_COLORS = { Confirmed: 'ok', Declined: 'bad', Completed: 'blue' };

    async function send(card, url, body) {
        const actions = $('[data-actions]', card);
        actions.classList.add('spin');
        try {
            const data = await api(url, { method: 'POST', body });

            // show the new status and remove the buttons that no longer apply
            $('[data-status]', card).innerHTML = badge(data.status, STATUS_COLORS[data.status]);
            actions.remove();
            toast(data.message, 'ok');

            const pendingLeft = $$('#pending-list [data-actions]').length;
            if (pendingLeft === 0) $('#pending-empty')?.classList.remove('hide');
        } catch (error) {
            // e.g. the student cancelled the request in the meantime
            actions.classList.remove('spin');
            toast(error.message, 'bad');
        }
    }

    document.addEventListener('click', event => {
        const card = event.target.closest('[data-booking]');
        if (!card) return;
        const bookingId = card.dataset.booking;

        const respond = event.target.closest('[data-respond]');
        if (respond) {
            send(card, `/bookings/${bookingId}/respond`, { accept: respond.dataset.respond === 'true' });
            return;
        }
        if (event.target.closest('[data-complete]')) {
            send(card, `/bookings/${bookingId}/complete`);
        }
    });
})();
