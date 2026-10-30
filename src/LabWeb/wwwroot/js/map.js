(() => {
    const cfg = window.mapConfig;
    const statusNames = { Working: 'Працює', Limited: 'Працює з обмеженнями', Closed: 'Не працює' };
    const points = new Map();
    const markers = new Map();
    const filters = { statuses: new Set(['Working', 'Limited']), services: new Set() };

    const map = L.map('map', { zoomControl: false }).setView([49.2, 31.2], 6);
    L.control.zoom({ position: 'bottomleft' }).addTo(map);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);

    const list = document.getElementById('mapList');
    const empty = document.getElementById('mapEmpty');
    const summary = document.getElementById('mapSummary');

    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const lampClass = (status) => 'lamp-' + status.toLowerCase();
    const rtf = new Intl.RelativeTimeFormat('uk', { numeric: 'auto' });

    function ago(iso) {
        const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
        if (minutes < 1) return 'щойно';
        if (minutes < 60) return rtf.format(-minutes, 'minute');
        const hours = Math.round(minutes / 60);
        if (hours < 24) return rtf.format(-hours, 'hour');
        return rtf.format(-Math.round(hours / 24), 'day');
    }

    function flags(p) {
        const items = [
            [p.hasElectricity, 'bi-lightbulb', 'світло'],
            [p.hasInternet, 'bi-wifi', 'зв\'язок'],
            [p.hasHeating, 'bi-thermometer-half', 'тепло']
        ];
        return items.map(([on, icon, name]) =>
            `<i class="bi ${icon} flag ${on ? 'flag-on' : 'flag-off'}" title="${on ? 'Є' : 'Немає'} ${name}" aria-label="${on ? 'є' : 'немає'} ${name}"></i>`
        ).join('');
    }

    function popupHtml(p) {
        const services = p.serviceIds.map((id) => cfg.services[id]).filter(Boolean)
            .map((s) => `<span class="chip"><i class="bi ${esc(s.icon)}" aria-hidden="true"></i>${esc(s.name)}</span>`).join('');
        const people = p.occupancy == null ? '' : `<p class="map-pop-people">У пункті ${p.occupancy} з ${p.capacity} осіб</p>`;
        return `<div class="map-pop">
            <span class="lamp ${lampClass(p.status)}">${statusNames[p.status]}</span>
            <h2>${esc(p.name)}</h2>
            <p class="map-pop-addr">${esc(p.settlement)}, ${esc(p.address)}</p>
            <div class="map-pop-flags">${flags(p)}</div>
            ${people}
            ${p.schedule ? `<p class="map-pop-schedule">${esc(p.schedule)}</p>` : ''}
            <div class="map-pop-services">${services}</div>
            <a class="btn btn-primary btn-sm" href="${cfg.detailsUrl}${p.id}">Відкрити пункт</a>
        </div>`;
    }

    function icon(p) {
        return L.divIcon({
            className: 'map-marker',
            html: `<span class="map-lamp ${lampClass(p.status)}"></span>`,
            iconSize: [28, 28],
            iconAnchor: [14, 14],
            popupAnchor: [0, -12]
        });
    }

    const visible = (p) => filters.statuses.has(p.status) && [...filters.services].every((id) => p.serviceIds.includes(id));

    function render() {
        const shown = [...points.values()].filter(visible)
            .sort((a, b) => Object.keys(statusNames).indexOf(a.status) - Object.keys(statusNames).indexOf(b.status) || a.name.localeCompare(b.name, 'uk'));

        for (const [id, marker] of markers) {
            const on = shown.some((p) => p.id === id);
            if (on && !map.hasLayer(marker)) marker.addTo(map);
            if (!on && map.hasLayer(marker)) marker.remove();
        }

        list.innerHTML = shown.map((p) => `<li>
            <button type="button" class="map-row" data-id="${p.id}">
                <span class="lamp ${lampClass(p.status)}" aria-label="${statusNames[p.status]}"></span>
                <span class="map-row-body">
                    <span class="map-row-name">${esc(p.name)}</span>
                    <span class="map-row-addr">${esc(p.settlement)}, ${esc(p.address)}</span>
                </span>
                <span class="map-row-side">
                    <span class="map-row-flags">${flags(p)}</span>
                    <small>${ago(p.updatedAt)}</small>
                </span>
            </button>
        </li>`).join('');

        empty.hidden = shown.length > 0 || points.size === 0;
        const lit = shown.filter((p) => p.status === 'Working').length;
        summary.textContent = points.size === 0
            ? 'Пунктів поки немає.'
            : `На мапі ${shown.length} з ${points.size} пунктів, працюють ${lit}.`;
    }

    function upsert(p, { flicker = false } = {}) {
        const before = points.get(p.id);
        points.set(p.id, p);
        let marker = markers.get(p.id);
        if (!marker) {
            marker = L.marker([p.latitude, p.longitude], { icon: icon(p), title: p.name, riseOnHover: true })
                .bindPopup(() => popupHtml(points.get(p.id)), { className: 'map-popup', maxWidth: 280 });
            markers.set(p.id, marker);
        } else {
            marker.setLatLng([p.latitude, p.longitude]);
            marker.setIcon(icon(p));
            if (marker.isPopupOpen()) marker.setPopupContent(popupHtml(p));
        }
        render();
        if (flicker && (!before || before.status !== p.status)) {
            const el = marker.getElement()?.querySelector('.map-lamp');
            el?.classList.add('map-lamp-switch');
            list.querySelector(`.map-row[data-id="${p.id}"]`)?.classList.add('map-row-fresh');
        }
    }

    function remove(id) {
        markers.get(id)?.remove();
        markers.delete(id);
        points.delete(id);
        render();
    }

    function focus(id) {
        const marker = markers.get(id);
        if (!marker) return;
        map.once('moveend', () => marker.openPopup());
        map.flyTo(marker.getLatLng(), Math.max(map.getZoom(), 15), { duration: .6 });
    }

    list.addEventListener('click', (e) => {
        const row = e.target.closest('.map-row');
        if (row) focus(Number(row.dataset.id));
    });

    document.querySelectorAll('.filter-chip').forEach((chip) => chip.addEventListener('click', () => {
        const pressed = chip.getAttribute('aria-pressed') !== 'true';
        chip.setAttribute('aria-pressed', String(pressed));
        const set = chip.dataset.status ? filters.statuses : filters.services;
        const value = chip.dataset.status ?? Number(chip.dataset.service);
        pressed ? set.add(value) : set.delete(value);
        render();
    }));

    fetch(cfg.pointsUrl)
        .then((r) => {
            if (!r.ok) throw new Error(r.statusText);
            return r.json();
        })
        .then((data) => {
            data.forEach((p) => upsert(p));
            const coords = data.map((p) => [p.latitude, p.longitude]);
            if (coords.length) map.fitBounds(coords, { padding: [40, 40], maxZoom: 13 });
        })
        .catch(() => { summary.textContent = 'Не вдалося завантажити пункти. Оновіть сторінку.'; });

    window.pointsMap = { upsert, remove, render };
})();
