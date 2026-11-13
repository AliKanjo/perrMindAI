/* Find a tutor: asynchronous search. Every filter change asks the server for
   JSON (GET /tutors/search) and redraws the result cards without a page reload. */
(function () {
    'use strict';

    const { $, api, avatar, badge, escapeHtml } = window.PeerMind;

    const results = $('#tutor-results');
    const count = $('#tutor-count');
    const filters = {
        q: $('#filter-search'),
        courseId: $('#filter-course'),
        minRating: $('#filter-rating'),
        withinDays: $('#filter-days')
    };

    let latestRequest = 0;

    function tutorCard(tutor) {
        const availability = tutor.nextAvailable
            ? `Next free slot: ${escapeHtml(tutor.nextAvailable)}`
            : 'No free slots at the moment';

        return `
            <div class="card">
                <div class="row">
                    ${avatar(tutor.fullName, 52)}
                    <div class="grow">
                        <div class="b">${escapeHtml(tutor.fullName)}</div>
                        <div class="xs sub">${escapeHtml(tutor.university || '')}</div>
                    </div>
                    ${badge('✓', 'ok')}
                </div>
                <p class="sm" style="margin:14px 0 4px"><b>${escapeHtml(tutor.courses || 'No courses yet')}</b></p>
                <p class="xs sub">${escapeHtml(tutor.headline || '')}</p>
                <div class="rowb sm" style="margin-top:10px">
                    ${tutor.reviewCount === 0
                        ? '<span class="sub">New tutor · no reviews yet</span>'
                        : `<span><span style="color:#F59E0B">★</span> <b>${Number(tutor.averageRating).toFixed(1)}</b>
                               <span class="sub">(${tutor.reviewCount})</span></span>`}
                    <b>$${Number(tutor.hourlyRate)}/hr</b>
                </div>
                <div class="xs sub" style="margin:10px 0 14px">${availability}</div>
                <a class="btn btn-pri btn-block" href="/tutors/${tutor.tutorId}">View profile</a>
            </div>`;
    }

    function messageCard(title, text) {
        return `<div class="card empty" style="grid-column:1/-1"><h3>${escapeHtml(title)}</h3><p class="sm">${escapeHtml(text)}</p></div>`;
    }

    async function search() {
        const requestNumber = ++latestRequest;
        const query = new URLSearchParams();
        for (const [name, input] of Object.entries(filters)) {
            if (input.value.trim()) query.set(name, input.value.trim());
        }

        results.classList.add('spin');
        try {
            const data = await api('/tutors/search?' + query);
            if (requestNumber !== latestRequest) return;   // a newer search is already running

            if (data.tutors.length === 0) {
                count.textContent = 'No tutors found';
                results.innerHTML = messageCard('No tutors match your search',
                    'Try another course, a lower rating or a wider time range.');
            } else {
                count.textContent = `${data.tutors.length} tutor(s) found`;
                results.innerHTML = data.tutors.map(tutorCard).join('');
            }
        } catch (error) {
            if (requestNumber !== latestRequest) return;
            count.textContent = 'Search failed';
            results.innerHTML = messageCard('The tutors could not be loaded', error.message);
        } finally {
            if (requestNumber === latestRequest) results.classList.remove('spin');
        }
    }

    // wait until the user stops typing before searching
    let typingTimer;
    filters.q.addEventListener('input', () => {
        clearTimeout(typingTimer);
        typingTimer = setTimeout(search, 300);
    });
    [filters.courseId, filters.minRating, filters.withinDays].forEach(select => select.addEventListener('change', search));

    search();
})();
