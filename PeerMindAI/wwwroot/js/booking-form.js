/* Tutor profile: the day chips switch between the time slots of each day,
   and the total price follows the selected slot (hourly rate x length). */
(function () {
    'use strict';

    const { $, $$ } = window.PeerMind;

    const form = $('#booking-form');
    if (!form) return;   // the tutor has no free slots

    const hourlyRate = Number(form.dataset.rate);
    const total = $('#booking-total');

    function showDay(dayIndex) {
        $$('[data-day]', form).forEach(chip => chip.classList.toggle('on', chip.dataset.day === dayIndex));
        $$('[data-day-panel]', form).forEach(panel => panel.classList.toggle('hide', panel.dataset.dayPanel !== dayIndex));
    }

    function updateTotal() {
        const selected = $('input[name="SlotId"]:checked', form);
        if (!selected) {
            total.textContent = 'Pick a time';
            return;
        }
        const minutes = Number(selected.dataset.minutes);
        const price = Math.round(hourlyRate * minutes / 60 * 100) / 100;
        total.textContent = `$${price} (${minutes} min)`;
    }

    $$('[data-day]', form).forEach(chip => chip.addEventListener('click', () => {
        // a slot of another day must not stay selected while it is hidden
        $$('input[name="SlotId"]', form).forEach(radio => { radio.checked = false; });
        showDay(chip.dataset.day);
        updateTotal();
    }));

    $$('input[name="SlotId"]', form).forEach(radio => radio.addEventListener('change', updateTotal));

    // after a failed submit the server re-selects the slot: open its day
    const preselected = $('input[name="SlotId"]:checked', form);
    if (preselected) showDay(preselected.closest('[data-day-panel]').dataset.dayPanel);
    updateTotal();
})();
