(() => {
    const chat = document.getElementById('chat');
    const log = document.getElementById('chatLog');
    const empty = document.getElementById('chatEmpty');
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    const send = document.getElementById('chatSend');
    const reset = document.getElementById('chatReset');
    const token = chat.querySelector('input[name="__RequestVerificationToken"]').value;

    const toolNames = {
        find_nearby_points: 'Шукаю пункти поруч',
        search_points: 'Шукаю пункти',
        get_point: 'Перевіряю пункт',
        get_point_reports: 'Читаю звіти пункту',
        list_services: 'Дивлюся перелік послуг'
    };

    let history = [];
    let controller = null;

    const escapeHtml = (s) => s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    function format(text) {
        return escapeHtml(text)
            .replace(/\[([^\]]+)\]\((\/punkt\/\d+)\)/g, '<a href="$2">$1</a>')
            .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
            .replace(/^\s*[-*]\s+/gm, '• ')
            .replace(/\n/g, '<br>');
    }

    function describeArgs(args) {
        const values = Object.entries(args || {}).map(([, v]) => (Array.isArray(v) ? v.join(', ') : String(v)));
        return values.join(', ');
    }

    function addMessage(role, text) {
        const el = document.createElement('div');
        el.className = `chat-msg chat-${role}`;
        if (text) el.textContent = text;
        log.append(el);
        el.scrollIntoView({ block: 'end' });
        return el;
    }

    function createTurn() {
        const turn = document.createElement('div');
        turn.className = 'chat-msg chat-model';
        const steps = document.createElement('details');
        steps.className = 'chat-steps';
        steps.open = true;
        steps.hidden = true;
        const summary = document.createElement('summary');
        summary.textContent = 'Хід роботи';
        const list = document.createElement('ol');
        steps.append(summary, list);
        const body = document.createElement('div');
        body.className = 'chat-text';
        const wait = document.createElement('span');
        wait.className = 'chat-wait';
        wait.textContent = 'Думаю…';
        turn.append(steps, body, wait);
        log.append(turn);
        return { turn, steps, list, body, wait, text: '', failed: false };
    }

    function addStep(ui, className, text) {
        const li = document.createElement('li');
        li.className = className;
        li.textContent = text;
        ui.list.append(li);
        ui.steps.hidden = false;
        return li;
    }

    function handleEvent(ui, type, data) {
        switch (type) {
            case 'tool_call': {
                const args = describeArgs(data.args);
                ui.pending = addStep(ui, 'step-tool', `${toolNames[data.name] ?? data.name}${args ? `: ${args}` : ''}`);
                break;
            }
            case 'tool_result':
                if (ui.pending) {
                    const result = document.createElement('small');
                    result.textContent = data.summary;
                    ui.pending.append(result);
                }
                break;
            case 'text':
                ui.text += data.text;
                ui.body.innerHTML = format(ui.text);
                break;
            case 'error':
                ui.failed = true;
                ui.body.insertAdjacentHTML('beforeend', `<p class="chat-error">${escapeHtml(data.message)}</p>`);
                break;
        }
        ui.turn.scrollIntoView({ block: 'end' });
    }

    async function ask(message) {
        empty.hidden = true;
        reset.hidden = false;
        addMessage('user', message);
        const ui = createTurn();
        send.disabled = true;
        controller = new AbortController();

        try {
            const response = await fetch(chat.dataset.url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
                body: JSON.stringify({ message, history }),
                signal: controller.signal
            });
            if (response.status === 429) throw new Error('Забагато запитів. Спробуйте за кілька хвилин.');
            if (!response.ok || !response.body) throw new Error('Не вдалося надіслати повідомлення. Оновіть сторінку.');

            const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
            let buffer = '';
            for (;;) {
                const { value, done } = await reader.read();
                if (done) break;
                buffer += value;
                let end;
                while ((end = buffer.indexOf('\n\n')) >= 0) {
                    const frame = buffer.slice(0, end);
                    buffer = buffer.slice(end + 2);
                    const type = /^event: (.+)$/m.exec(frame)?.[1];
                    const data = /^data: (.+)$/m.exec(frame)?.[1];
                    if (type && data) handleEvent(ui, type, JSON.parse(data));
                }
            }
            if (ui.text) {
                history.push({ role: 'user', text: message }, { role: 'model', text: ui.text.slice(0, 4000) });
                history = history.slice(-20);
                ui.steps.open = false;
            }
        } catch (err) {
            if (err.name !== 'AbortError') handleEvent(ui, 'error', { message: err.message });
        } finally {
            ui.wait.remove();
            send.disabled = false;
            controller = null;
        }
    }

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        const message = input.value.trim();
        if (!message || send.disabled) return;
        input.value = '';
        ask(message);
    });

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            form.requestSubmit();
        }
    });

    empty.querySelectorAll('.filter-chip').forEach((chip) => chip.addEventListener('click', () => ask(chip.textContent)));

    reset.addEventListener('click', () => {
        controller?.abort();
        history = [];
        log.querySelectorAll('.chat-msg').forEach((el) => el.remove());
        empty.hidden = false;
        reset.hidden = true;
    });
})();
