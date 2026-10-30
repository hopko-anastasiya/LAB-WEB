(() => {
    const root = document.getElementById('guide');
    const doc = document.getElementById('guideDoc');
    const stateEl = document.getElementById('guideState');
    const addButton = document.getElementById('guideAdd');
    const storageKey = `nezlamni.guide.${root.dataset.community}`;
    const SAVE_MS = 250;
    const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

    const blocks = new Map();
    const timers = new Map();
    const editors = new Map();
    let names = [];
    let dragged = null;

    const connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.hub).withAutomaticReconnect().build();

    const setState = (text) => { stateEl.textContent = text; };
    const showPresence = () => setState(connection.state === signalR.HubConnectionState.Connected
        ? `Зараз тут: ${names.join(', ') || 'ви'}`
        : 'Немає зв’язку, зміни збережено в браузері');
    const ordered = () => [...blocks.values()].sort((a, b) => a.position - b.position);

    function readLocal() {
        try { return JSON.parse(localStorage.getItem(storageKey) || 'null'); } catch { return null; }
    }

    function writeLocal() {
        try {
            localStorage.setItem(storageKey, JSON.stringify({ blocks: ordered(), dirty: [...timers.keys(), ...pending] }));
        } catch { }
    }

    const pending = new Set();

    function send(id) {
        const block = blocks.get(id);
        if (!block) return;
        pending.add(id);
        writeLocal();
        if (connection.state !== signalR.HubConnectionState.Connected) return;
        connection.invoke('Upsert', block)
            .then(() => { pending.delete(id); writeLocal(); })
            .catch((err) => setState(err.message.replace(/^.*HubException: /, '')));
    }

    function schedule(id) {
        clearTimeout(timers.get(id));
        timers.set(id, setTimeout(() => {
            timers.delete(id);
            send(id);
        }, SAVE_MS));
        writeLocal();
    }

    function element(id) {
        return doc.querySelector(`[data-id="${id}"]`);
    }

    function flash(row, className) {
        if (!row || reduced) return;
        row.classList.remove(className);
        void row.offsetWidth;
        row.classList.add(className);
    }

    function createRow(block) {
        const row = document.createElement('div');
        row.className = `guide-row type-${block.type.toLowerCase()}`;
        row.dataset.id = block.id;

        const handle = document.createElement('button');
        handle.type = 'button';
        handle.className = 'guide-handle';
        handle.draggable = true;
        handle.setAttribute('aria-label', 'Перетягнути рядок. Стрілки вгору і вниз переміщують його');
        handle.innerHTML = '<i class="bi bi-grip-vertical" aria-hidden="true"></i>';

        const text = document.createElement('div');
        text.className = 'guide-text';
        text.contentEditable = 'plaintext-only';
        text.spellcheck = true;
        text.setAttribute('role', 'textbox');
        text.setAttribute('aria-label', 'Рядок документа');
        text.dataset.placeholder = 'Порожній рядок';
        text.textContent = block.text;

        const who = document.createElement('span');
        who.className = 'guide-who';
        who.hidden = true;

        row.append(handle, text, who);
        return row;
    }

    function render() {
        const list = ordered();
        const focusedId = document.activeElement?.closest?.('.guide-row')?.dataset.id;
        const first = new Map([...doc.children].map((el) => [el.dataset.id, el.getBoundingClientRect().top]));

        list.forEach((block, index) => {
            let row = element(block.id);
            if (!row) row = createRow(block);
            row.className = `guide-row type-${block.type.toLowerCase()}`;
            const text = row.querySelector('.guide-text');
            if (block.id !== focusedId && text.textContent !== block.text) text.textContent = block.text;
            if (doc.children[index] !== row) doc.insertBefore(row, doc.children[index] ?? null);
        });
        [...doc.children].forEach((el) => { if (!blocks.has(el.dataset.id)) el.remove(); });

        if (!reduced) {
            [...doc.children].forEach((el) => {
                const before = first.get(el.dataset.id);
                if (before === undefined) return;
                const delta = before - el.getBoundingClientRect().top;
                if (delta) el.animate([{ transform: `translateY(${delta}px)` }, { transform: 'none' }], { duration: 220, easing: 'ease-out' });
            });
        }
        showEditors();
    }

    function showEditors() {
        doc.querySelectorAll('.guide-row').forEach((row) => {
            const who = row.querySelector('.guide-who');
            const people = [...editors.values()].filter((e) => e.blockId === row.dataset.id).map((e) => e.name);
            who.hidden = people.length === 0;
            who.textContent = [...new Set(people)].join(', ');
            row.classList.toggle('is-edited', people.length > 0);
        });
    }

    const newId = () => crypto.randomUUID();

    function positionAfter(id) {
        const list = ordered();
        const index = list.findIndex((b) => b.id === id);
        if (index < 0) return (list.at(-1)?.position ?? 0) + 1;
        const next = list[index + 1];
        return next ? (list[index].position + next.position) / 2 : list[index].position + 1;
    }

    function addBlock(afterId, type = 'Paragraph', text = '') {
        const block = { id: newId(), type, text, position: positionAfter(afterId) };
        blocks.set(block.id, block);
        render();
        send(block.id);
        focusBlock(block.id, 0);
        return block;
    }

    function focusBlock(id, caret) {
        const text = element(id)?.querySelector('.guide-text');
        if (!text) return;
        text.focus();
        const range = document.createRange();
        range.selectNodeContents(text);
        range.collapse(caret === 0);
        const selection = getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function removeBlock(id) {
        clearTimeout(timers.get(id));
        timers.delete(id);
        pending.delete(id);
        blocks.delete(id);
        writeLocal();
        connection.invoke('Delete', id).catch(() => {});
    }

    function moveBlock(id, beforeId) {
        const list = ordered().filter((b) => b.id !== id);
        const index = beforeId ? list.findIndex((b) => b.id === beforeId) : list.length;
        const prev = list[index - 1]?.position;
        const next = list[index]?.position;
        const block = blocks.get(id);
        block.position = prev === undefined
            ? (next === undefined ? 1 : next - 1)
            : (next === undefined ? prev + 1 : (prev + next) / 2);
        render();
        flash(element(id), 'just-moved');
        send(id);
    }

    function applyMarkdownShortcut(block, text) {
        const match = /^(#|-|\*)\s(.*)$/s.exec(text.textContent);
        if (!match) return;
        block.type = match[1] === '#' ? 'Heading' : 'Bullet';
        block.text = match[2];
        text.textContent = match[2];
        element(block.id).className = `guide-row type-${block.type.toLowerCase()}`;
        focusBlock(block.id, 1);
    }

    doc.addEventListener('input', (e) => {
        const row = e.target.closest('.guide-row');
        if (!row) return;
        const block = blocks.get(row.dataset.id);
        block.text = e.target.textContent;
        applyMarkdownShortcut(block, e.target);
        schedule(block.id);
    });

    doc.addEventListener('keydown', (e) => {
        const row = e.target.closest('.guide-row');
        if (!row) return;
        const id = row.dataset.id;

        if (e.target.classList.contains('guide-handle') && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
            e.preventDefault();
            const list = ordered();
            const index = list.findIndex((b) => b.id === id);
            const target = e.key === 'ArrowUp' ? list[index - 1]?.id : list[index + 2]?.id ?? null;
            if (e.key === 'ArrowUp' && !target) return;
            if (e.key === 'ArrowDown' && index === list.length - 1) return;
            moveBlock(id, target);
            element(id).querySelector('.guide-handle').focus();
            return;
        }
        if (!e.target.classList.contains('guide-text')) return;

        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            const block = blocks.get(id);
            addBlock(id, block.type === 'Bullet' ? 'Bullet' : 'Paragraph');
        } else if (e.key === 'Backspace' && e.target.textContent === '') {
            const block = blocks.get(id);
            e.preventDefault();
            if (block.type !== 'Paragraph') {
                block.type = 'Paragraph';
                row.className = 'guide-row type-paragraph';
                schedule(id);
                return;
            }
            if (blocks.size === 1) return;
            const list = ordered();
            const previous = list[list.findIndex((b) => b.id === id) - 1] ?? list[1];
            removeBlock(id);
            render();
            focusBlock(previous.id, 1);
        }
    });

    doc.addEventListener('focusin', (e) => {
        const id = e.target.closest('.guide-row')?.dataset.id;
        if (id && connection.state === signalR.HubConnectionState.Connected) connection.invoke('Editing', id).catch(() => {});
    });
    doc.addEventListener('focusout', () => {
        if (connection.state === signalR.HubConnectionState.Connected) connection.invoke('Editing', null).catch(() => {});
    });

    doc.addEventListener('dragstart', (e) => {
        const row = e.target.closest?.('.guide-row');
        if (!row) return;
        dragged = row.dataset.id;
        row.classList.add('is-dragging');
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', dragged);
        e.dataTransfer.setDragImage(row, 16, 16);
    });
    doc.addEventListener('dragover', (e) => {
        if (!dragged) return;
        e.preventDefault();
        const row = e.target.closest('.guide-row');
        doc.querySelectorAll('.drop-before, .drop-after').forEach((el) => el.classList.remove('drop-before', 'drop-after'));
        if (!row || row.dataset.id === dragged) return;
        const rect = row.getBoundingClientRect();
        row.classList.add(e.clientY < rect.top + rect.height / 2 ? 'drop-before' : 'drop-after');
    });
    doc.addEventListener('drop', (e) => {
        if (!dragged) return;
        e.preventDefault();
        const row = doc.querySelector('.drop-before, .drop-after');
        if (row) {
            const beforeId = row.classList.contains('drop-before') ? row.dataset.id : row.nextElementSibling?.dataset.id ?? null;
            if (beforeId !== dragged) moveBlock(dragged, beforeId);
        }
    });
    doc.addEventListener('dragend', () => {
        doc.querySelectorAll('.is-dragging, .drop-before, .drop-after').forEach((el) => el.classList.remove('is-dragging', 'drop-before', 'drop-after'));
        dragged = null;
    });

    addButton.addEventListener('click', () => addBlock(ordered().at(-1)?.id));

    connection.on('blockChanged', (block) => {
        const isNew = !blocks.has(block.id);
        const old = blocks.get(block.id);
        const moved = old && old.position !== block.position;
        blocks.set(block.id, block);
        pending.delete(block.id);
        render();
        flash(element(block.id), moved ? 'just-moved' : isNew ? 'just-added' : 'just-changed');
        writeLocal();
    });
    connection.on('blockRemoved', (id) => {
        const row = element(id);
        if (!row) return;
        const finish = () => {
            blocks.delete(id);
            render();
            writeLocal();
        };
        if (reduced) finish();
        else row.animate([{ opacity: 1 }, { opacity: 0, transform: 'translateX(-1rem)' }], { duration: 200 }).onfinish = finish;
    });
    connection.on('editing', (connectionId, blockId, name) => {
        if (blockId) editors.set(connectionId, { blockId, name });
        else editors.delete(connectionId);
        showEditors();
    });
    connection.on('presence', (list) => {
        names = list;
        showPresence();
    });
    connection.onreconnecting(() => setState('Немає зв’язку, зміни збережено в браузері'));
    connection.onreconnected(sync);
    connection.onclose(() => setState('Зв’язок втрачено. Оновіть сторінку, зміни збережено в браузері.'));

    async function sync() {
        const server = await connection.invoke('Open');
        const local = readLocal();
        const dirty = new Set(local?.dirty ?? []);
        const unsaved = (local?.blocks ?? []).filter((b) => dirty.has(b.id));

        blocks.clear();
        server.forEach((b) => blocks.set(b.id, b));
        unsaved.forEach((b) => blocks.set(b.id, b));
        pending.clear();
        unsaved.forEach((b) => send(b.id));

        render();
        writeLocal();
        showPresence();
    }

    const cached = readLocal();
    (cached?.blocks ?? []).forEach((b) => blocks.set(b.id, b));
    (cached?.dirty ?? []).forEach((id) => pending.add(id));
    render();

    window.addEventListener('beforeunload', writeLocal);

    connection.start().then(sync).catch(() => setState('Немає зв’язку. Показуємо останню збережену копію.'));
})();
