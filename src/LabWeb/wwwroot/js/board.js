(() => {
    const cfg = window.boardConfig;
    const MIN_PIXELS = 3;
    const MAX_POINTS = 1500;
    const PROGRESS_MS = 100;

    const el = document.getElementById('boardMap');
    const presence = document.getElementById('boardPresence');
    const map = L.map(el, { zoomControl: false }).setView([49.2, 31.2], 6);
    L.control.zoom({ position: 'bottomleft' }).addTo(map);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);

    for (const p of cfg.points) {
        L.marker([p.latitude, p.longitude], {
            icon: L.divIcon({
                className: 'map-marker',
                html: `<span class="map-lamp lamp-${p.status.toLowerCase()}"></span>`,
                iconSize: [28, 28],
                iconAnchor: [14, 14]
            }),
            interactive: false,
            keyboard: false
        }).addTo(map);
    }
    if (cfg.points.length) {
        map.fitBounds(cfg.points.map((p) => [p.latitude, p.longitude]), { padding: [60, 60], maxZoom: 14 });
    }

    const connection = new signalR.HubConnectionBuilder().withUrl(cfg.hubUrl).withAutomaticReconnect().build();
    const items = new Map();
    const drafts = new Map();
    const state = { tool: 'move', color: '#F5B841', width: 3, userId: null, isAdmin: false };

    const round = (latlng) => [Number(latlng.lat.toFixed(5)), Number(latlng.lng.toFixed(5))];
    const showError = (err) => setPresence(err?.message?.replace(/^.*HubException: /, '') || 'Не вдалося виконати дію.');
    const setPresence = (text) => { presence.textContent = text; };

    function strokeLayer(color, width, points) {
        return L.polyline(points, { color, weight: width, lineCap: 'round', lineJoin: 'round', smoothFactor: 1 });
    }

    function noteLayer(item) {
        const label = document.createElement('div');
        label.className = 'board-note';
        label.style.setProperty('--note', item.color);
        label.textContent = item.text;
        return L.marker(item.points[0], {
            icon: L.divIcon({ className: 'board-note-wrap', html: label, iconSize: null, iconAnchor: [0, 0] }),
            keyboard: false
        });
    }

    function addItem(item) {
        removeItem(item.id);
        const layer = item.kind === 'Note' ? noteLayer(item) : strokeLayer(item.color, item.width, item.points);
        layer.bindTooltip(item.authorName, { sticky: true, className: 'board-author' });
        layer.on('click', () => {
            if (state.tool !== 'erase') return;
            connection.invoke('DeleteItem', item.id).catch(showError);
        });
        layer.addTo(map);
        items.set(item.id, layer);
    }

    function removeItem(id) {
        items.get(id)?.remove();
        items.delete(id);
    }

    function removeDraft(clientId) {
        drafts.get(clientId)?.remove();
        drafts.delete(clientId);
    }

    function clearAll() {
        items.forEach((layer) => layer.remove());
        items.clear();
        drafts.forEach((layer) => layer.remove());
        drafts.clear();
    }

    connection.on('itemAdded', (item) => {
        if (item.clientId) {
            removeDraft(item.clientId);
            if (item.clientId === current?.finishedId) current = null;
        }
        addItem(item);
    });
    connection.on('itemRemoved', removeItem);
    connection.on('cleared', clearAll);
    connection.on('drawProgress', (clientId, color, width, points) => {
        const draft = drafts.get(clientId);
        if (draft) points.forEach((p) => draft.addLatLng(p));
        else drafts.set(clientId, strokeLayer(color, width, points).addTo(map));
    });
    connection.on('presence', (names) => setPresence(`На дошці: ${names.join(', ')}`));
    connection.onreconnecting(() => setPresence("Зв'язок втрачено, перепідключаємось…"));
    connection.onreconnected(load);
    connection.onclose(() => setPresence("Зв'язок із дошкою втрачено. Оновіть сторінку."));

    function load() {
        return connection.invoke('Load').then((board) => {
            state.userId = board.userId;
            state.isAdmin = board.isAdmin;
            clearAll();
            board.items.forEach(addItem);
        }).catch(showError);
    }

    let current = null;

    function pointerLatLng(e) {
        return map.containerPointToLatLng(L.point(e.clientX - el.getBoundingClientRect().left, e.clientY - el.getBoundingClientRect().top));
    }

    function flushProgress() {
        if (!current || current.sent >= current.points.length) return;
        const from = Math.max(0, current.sent - 1);
        const chunk = current.points.slice(from, from + 200);
        current.sent = from + chunk.length;
        connection.invoke('DrawProgress', current.id, state.color, state.width, chunk).catch(() => {});
    }

    function finishStroke() {
        if (!current || current.finishedId) return;
        clearInterval(current.timer);
        const { id, points, layer } = current;
        if (points.length < 2) {
            layer.remove();
            current = null;
            return;
        }
        current.finishedId = id;
        drafts.set(id, layer);
        connection.invoke('AddStroke', id, state.color, state.width, points).catch((err) => {
            removeDraft(id);
            current = null;
            showError(err);
        });
    }

    el.addEventListener('pointerdown', (e) => {
        if (state.tool !== 'pen' || e.button !== 0 || e.target.closest('.leaflet-control')) return;
        e.preventDefault();
        el.setPointerCapture(e.pointerId);
        const start = pointerLatLng(e);
        current = {
            id: crypto.randomUUID(),
            points: [round(start)],
            lastPixel: L.point(e.clientX, e.clientY),
            sent: 0,
            layer: strokeLayer(state.color, state.width, [start]).addTo(map)
        };
        current.timer = setInterval(flushProgress, PROGRESS_MS);
    });

    el.addEventListener('pointermove', (e) => {
        if (!current || current.finishedId) return;
        const pixel = L.point(e.clientX, e.clientY);
        if (pixel.distanceTo(current.lastPixel) < MIN_PIXELS) return;
        current.lastPixel = pixel;
        const latlng = pointerLatLng(e);
        current.points.push(round(latlng));
        current.layer.addLatLng(latlng);
        if (current.points.length >= MAX_POINTS) finishStroke();
    });

    el.addEventListener('pointerup', finishStroke);
    el.addEventListener('pointercancel', finishStroke);

    map.on('click', (e) => {
        if (state.tool !== 'note') return;
        const form = document.createElement('form');
        form.className = 'board-note-form';
        const input = document.createElement('input');
        input.className = 'form-control form-control-sm';
        input.maxLength = 200;
        input.required = true;
        input.placeholder = 'Текст примітки';
        input.setAttribute('aria-label', 'Текст примітки');
        const save = document.createElement('button');
        save.className = 'btn btn-primary btn-sm';
        save.textContent = 'Додати';
        form.append(input, save);
        const popup = L.popup({ className: 'map-popup', minWidth: 220 }).setLatLng(e.latlng).setContent(form).openOn(map);
        input.focus();
        form.addEventListener('submit', (event) => {
            event.preventDefault();
            const [lat, lng] = round(e.latlng);
            connection.invoke('AddNote', state.color, lat, lng, input.value).then(() => popup.remove()).catch(showError);
        });
    });

    function press(buttons, active) {
        buttons.forEach((b) => b.setAttribute('aria-pressed', String(b === active)));
    }

    const tools = [...document.querySelectorAll('.board-tool')];
    tools.forEach((button) => button.addEventListener('click', () => {
        state.tool = button.dataset.tool;
        el.dataset.tool = state.tool;
        press(tools, button);
        if (state.tool === 'pen') map.dragging.disable();
        else map.dragging.enable();
    }));

    const swatches = [...document.querySelectorAll('.board-swatch')];
    swatches.forEach((button) => button.addEventListener('click', () => {
        state.color = button.dataset.color;
        press(swatches, button);
    }));

    const widths = [...document.querySelectorAll('.board-width')];
    widths.forEach((button) => button.addEventListener('click', () => {
        state.width = Number(button.dataset.width);
        press(widths, button);
    }));

    document.getElementById('boardClear')?.addEventListener('click', () => {
        if (confirm('Видалити всі лінії та примітки для всієї громади?')) {
            connection.invoke('Clear').catch(showError);
        }
    });

    connection.start().then(load).catch(() => setPresence('Не вдалося підключитися до дошки. Оновіть сторінку.'));
})();
