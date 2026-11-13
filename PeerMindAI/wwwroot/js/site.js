/* PeerMind AI - shared front-end code: toasts, modals, fetch helper and form validation. */
(function () {
    'use strict';

    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

    /* ---------- toast messages ---------- */

    let toastTimer;
    function toast(message, type = '') {
        const box = $('#toast');
        if (!box || !message) return;
        box.textContent = message;
        box.className = 'show ' + type;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => { box.className = type; }, 3500);
    }

    /* ---------- small HTML helpers (same look as the server-side Ui class) ---------- */

    const AVATAR_COLORS = ['#5B4EF0', '#0EA5E9', '#EC4899', '#F59E0B', '#10B981', '#8B5CF6', '#EF4444', '#14B8A6'];
    const HTML_ENTITIES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, character => HTML_ENTITIES[character]);
    }

    function avatar(name, size = 40) {
        name = (name || '?').trim();
        let hash = 0;
        for (const character of name) hash = (hash * 31 + character.charCodeAt(0)) % 97;
        const initials = name.split(' ').filter(Boolean).slice(0, 2).map(word => word[0]).join('');
        return `<span class="av" style="width:${size}px;height:${size}px;background:${AVATAR_COLORS[hash % AVATAR_COLORS.length]};` +
            `font-size:${Math.round(size * 0.38)}px">${escapeHtml(initials)}</span>`;
    }

    function badge(text, color = 'gray') {
        return `<span class="badge b-${color}">${escapeHtml(text)}</span>`;
    }

    /* ---------- fetch helper: JSON in, JSON out, errors as exceptions ---------- */

    async function api(url, options = {}) {
        const headers = { 'Accept': 'application/json' };
        const request = { method: options.method || 'GET', headers, credentials: 'same-origin' };

        if (request.method !== 'GET') {
            const token = $('#antiforgery input[name="__RequestVerificationToken"]');
            if (token) headers['RequestVerificationToken'] = token.value;
        }
        if (options.body !== undefined) {
            headers['Content-Type'] = 'application/json';
            request.body = JSON.stringify(options.body);
        }

        let response;
        try {
            response = await fetch(url, request);
        } catch {
            throw new Error('Could not reach the server. Please check your connection and try again.');
        }

        if (response.status === 401) {
            location.href = '/Account/Login?returnUrl=' + encodeURIComponent(location.pathname);
            throw new Error('Your session has expired. Please log in again.');
        }

        let data = null;
        try {
            data = await response.json();
        } catch {
            // the answer was not JSON (for example an error page)
        }

        if (!response.ok || !data || data.ok === false) {
            throw new Error(data?.message || 'Something went wrong on the server. Please try again.');
        }
        return data;
    }

    /* ---------- modals: <button data-modal="id"> opens, <button data-close> closes ---------- */

    document.addEventListener('click', event => {
        const opener = event.target.closest('[data-modal]');
        if (opener) {
            $('#' + opener.dataset.modal)?.classList.add('open');
            return;
        }
        if (event.target.matches('.modal') || event.target.closest('[data-close]')) {
            event.target.closest('.modal')?.classList.remove('open');
        }
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') $$('.modal.open').forEach(modal => modal.classList.remove('open'));
    });

    /* ---------- client-side form validation ----------
       Forms marked with data-validate are checked before they are sent.
       The rules are the data-val-* attributes that ASP.NET writes from the
       model's validation attributes, so browser and server show the same
       messages. The server validates everything again. */

    function fieldError(input) {
        const data = input.dataset;
        const value = (input.value || '').trim();

        if (input.type === 'checkbox') {
            if (data.requireOne) {
                // a group of checkboxes with the same name: at least one must be ticked
                const group = $$(`input[type="checkbox"][name="${input.name}"]`, input.form);
                return group.some(box => box.checked) ? null : data.requireOne;
            }
            return data.mustCheck && !input.checked ? data.mustCheck : null;
        }
        if (input.type === 'radio') {
            const group = $$(`input[type="radio"][name="${input.name}"]`, input.form);
            return data.valRequired && !group.some(radio => radio.checked) ? data.valRequired : null;
        }
        if (input.type === 'file') {
            if (data.valRequired && input.files.length === 0) return data.valRequired;
            const file = input.files[0];
            if (file && data.maxMb && file.size > Number(data.maxMb) * 1024 * 1024) {
                return `The file is too large. The maximum size is ${data.maxMb} MB.`;
            }
            if (file && data.extension && !file.name.toLowerCase().endsWith(data.extension)) {
                return `Please choose a ${data.extension.toUpperCase().replace('.', '')} file.`;
            }
            return null;
        }

        if (!value) return data.valRequired || null;

        if (data.valEmail && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value)) return data.valEmail;
        if (data.valLength) {
            const min = Number(data.valLengthMin || 0);
            const max = Number(data.valLengthMax || Infinity);
            if (value.length < min || value.length > max) return data.valLength;
        }
        if (data.valRegex && !new RegExp(`^(?:${data.valRegexPattern})$`).test(value)) return data.valRegex;
        if (data.valNumber && isNaN(Number(value))) return data.valNumber;
        if (data.valRange) {
            const number = Number(value);
            if (isNaN(number) || number < Number(data.valRangeMin) || number > Number(data.valRangeMax)) return data.valRange;
        }
        if (data.valEqualto) {
            // data-val-equalto-other looks like "*.Password"
            const otherName = data.valEqualtoOther.replace('*.', '');
            const other = $$('input', input.form).find(field => field.name === otherName || field.name.endsWith('.' + otherName));
            if (other && other.value !== input.value) return data.valEqualto;
        }
        return null;
    }

    function showFieldError(input, message) {
        let slot = $(`[data-valmsg-for="${input.name}"]`, input.form);
        if (!slot) {
            slot = document.createElement('span');
            slot.className = 'err';
            slot.dataset.valmsgFor = input.name;
            (input.closest('.field') || input.parentElement).appendChild(slot);
        }
        slot.textContent = message || '';
        if (input.matches('.in, .sel')) input.classList.toggle('bad', Boolean(message));
    }

    function validateForm(form) {
        const seenGroups = new Set();   // radio buttons and checkbox groups are checked once per name
        let firstInvalid = null;

        for (const input of $$('input, select, textarea', form)) {
            if (input.type === 'hidden' || input.disabled || !input.name) continue;
            if (input.type === 'radio' || input.dataset.requireOne) {
                if (seenGroups.has(input.name)) continue;
                seenGroups.add(input.name);
            }
            const message = fieldError(input);
            showFieldError(input, message);
            if (message && !firstInvalid) firstInvalid = input;
        }

        if (firstInvalid) {
            if (firstInvalid.matches('.in, .sel') && firstInvalid.type !== 'file') firstInvalid.focus();
            firstInvalid.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
        return !firstInvalid;
    }

    document.addEventListener('submit', event => {
        const form = event.target;
        if (form.matches('[data-validate]') && !validateForm(form)) {
            event.preventDefault();
        }
        if (event.defaultPrevented) {
            toast('Please correct the highlighted fields.', 'bad');
            return;
        }
        // forms with data-confirm="question" ask before they are sent (delete, approve ...)
        if (form.dataset.confirm && !window.confirm(form.dataset.confirm)) {
            event.preventDefault();
            return;
        }
        // stop double submissions (for example booking the same slot twice)
        $$('button[type="submit"]', form).forEach(button => button.classList.add('spin'));
    });

    // re-check a field as soon as the user changes it
    document.addEventListener('change', event => {
        const input = event.target;
        if (input.form?.matches('[data-validate]') && input.name) {
            showFieldError(input, fieldError(input));
        }
    });

    /* ---------- start-up: show the message the server left for this page ---------- */

    const serverToast = $('#toast');
    if (serverToast?.dataset.message) toast(serverToast.dataset.message, serverToast.dataset.type);

    window.PeerMind = { $, $$, toast, api, avatar, badge, escapeHtml };
})();
