(() => {
    const WIDTH = 800;
    const HEIGHT = 450;
    const GROUND = HEIGHT - 44;
    const VAN = { width: 92, height: 40, speed: 460 };
    const colors = {
        sky: '#14213D', building: '#0E1830', dark: '#1C2846', lamp: '#F5B841', ember: '#E8833A',
        ground: '#2A3B61', ink: '#E8ECF4', ice: '#9FC3E8', muted: '#93A0BA'
    };

    const canvas = document.getElementById('gameCanvas');
    const ctx = canvas.getContext('2d');
    const overlay = document.getElementById('gameOverlay');
    const message = document.getElementById('gameMessage');
    const startButton = document.getElementById('gameStart');
    const scoreEl = document.getElementById('gameScore');
    const bestEl = document.getElementById('gameBest');

    const ratio = window.devicePixelRatio || 1;
    canvas.width = WIDTH * ratio;
    canvas.height = HEIGHT * ratio;
    ctx.scale(ratio, ratio);

    const buildings = [];
    let cursor = 0;
    let seed = 7;
    const next = () => (seed = (seed * 9301 + 49297) % 233280) / 233280;
    while (cursor < WIDTH) {
        const cols = 3 + Math.floor(next() * 4);
        const rows = 3 + Math.floor(next() * 7);
        const width = cols * 20 + 12;
        const windows = [];
        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) windows.push({ x: cursor + 8 + c * 20, y: GROUND - rows * 24 - 4 + r * 24, threshold: next() });
        }
        buildings.push({ x: cursor, width, top: GROUND - rows * 24 - 12, windows });
        cursor += width + 8;
    }

    const readBest = () => {
        try { return Number(localStorage.getItem('nezlamni.game.best')) || 0; } catch { return 0; }
    };
    const saveBest = (value) => {
        try { localStorage.setItem('nezlamni.game.best', String(value)); } catch { }
    };

    let best = readBest();
    bestEl.textContent = best;

    const keys = new Set();
    let state;
    let frame = null;
    let last = 0;

    function reset() {
        state = { x: WIDTH / 2, fuel: 70, time: 0, items: [], spawn: 0, flash: 0, running: true };
    }

    function spawn() {
        const pace = Math.min(state.time / 60, 1);
        const hazard = Math.random() < .28 + pace * .22;
        state.items.push({
            kind: hazard ? 'ice' : 'fuel',
            x: 24 + Math.random() * (WIDTH - 48),
            y: -24,
            speed: 150 + pace * 190 + Math.random() * 60
        });
        state.spawn = .75 - pace * .4;
    }

    function update(dt) {
        const direction = (keys.has('right') ? 1 : 0) - (keys.has('left') ? 1 : 0);
        state.x = Math.max(VAN.width / 2, Math.min(WIDTH - VAN.width / 2, state.x + direction * VAN.speed * dt));
        state.time += dt;
        state.fuel -= (6 + Math.min(state.time / 60, 1) * 7) * dt;
        state.flash = Math.max(0, state.flash - dt);
        state.spawn -= dt;
        if (state.spawn <= 0) spawn();

        for (const item of state.items) {
            item.y += item.speed * dt;
            const caught = item.y > GROUND - VAN.height - 10 && item.y < GROUND && Math.abs(item.x - state.x) < VAN.width / 2 + 10;
            if (caught) {
                item.done = true;
                if (item.kind === 'fuel') {
                    state.fuel = Math.min(100, state.fuel + 16);
                } else {
                    state.fuel -= 20;
                    state.flash = .25;
                }
            }
            if (item.y > HEIGHT) item.done = true;
        }
        state.items = state.items.filter((i) => !i.done);
        scoreEl.textContent = Math.floor(state.time);
        if (state.fuel <= 0) finish();
    }

    function drawCity(level) {
        ctx.fillStyle = colors.sky;
        ctx.fillRect(0, 0, WIDTH, HEIGHT);
        for (const b of buildings) {
            ctx.fillStyle = colors.building;
            ctx.fillRect(b.x, b.top, b.width, GROUND - b.top);
            for (const w of b.windows) {
                ctx.fillStyle = w.threshold < level ? colors.lamp : colors.dark;
                ctx.fillRect(w.x, w.y, 12, 16);
            }
        }
        ctx.fillStyle = colors.ground;
        ctx.fillRect(0, GROUND, WIDTH, HEIGHT - GROUND);
    }

    function drawItem(item) {
        if (item.kind === 'fuel') {
            ctx.fillStyle = colors.ember;
            ctx.fillRect(item.x - 11, item.y - 13, 22, 26);
            ctx.fillRect(item.x - 3, item.y - 19, 8, 6);
            ctx.fillStyle = colors.sky;
            ctx.fillRect(item.x - 6, item.y - 5, 12, 3);
        } else {
            ctx.fillStyle = colors.ice;
            ctx.beginPath();
            ctx.moveTo(item.x - 9, item.y - 16);
            ctx.lineTo(item.x + 9, item.y - 16);
            ctx.lineTo(item.x, item.y + 18);
            ctx.closePath();
            ctx.fill();
        }
    }

    function drawVan() {
        const left = state.x - VAN.width / 2;
        const top = GROUND - VAN.height;
        ctx.fillStyle = colors.ink;
        ctx.fillRect(left, top + 4, VAN.width - 26, VAN.height - 10);
        ctx.fillRect(left + VAN.width - 28, top + 14, 28, VAN.height - 20);
        ctx.fillStyle = colors.sky;
        ctx.fillRect(left + VAN.width - 22, top + 18, 16, 9);
        ctx.fillStyle = colors.lamp;
        ctx.fillRect(left + 12, top + 12, 32, 5);
        ctx.fillStyle = colors.building;
        for (const x of [left + 20, left + VAN.width - 18]) {
            ctx.beginPath();
            ctx.arc(x, GROUND - 4, 8, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    function drawFuel() {
        ctx.fillStyle = colors.building;
        ctx.fillRect(16, 16, 208, 16);
        ctx.fillStyle = state.fuel < 25 ? colors.ember : colors.lamp;
        ctx.fillRect(20, 20, Math.max(0, state.fuel) * 2, 8);
        ctx.fillStyle = colors.ink;
        ctx.font = '14px Onest, sans-serif';
        ctx.fillText('Пальне', 236, 29);
    }

    function draw() {
        drawCity(Math.max(0, state.fuel) / 100);
        state.items.forEach(drawItem);
        drawVan();
        drawFuel();
        if (state.flash > 0) {
            ctx.fillStyle = 'rgba(159, 195, 232, .25)';
            ctx.fillRect(0, 0, WIDTH, HEIGHT);
        }
    }

    function loop(now) {
        const dt = Math.min((now - last) / 1000, .05);
        last = now;
        update(dt);
        draw();
        if (state.running) frame = requestAnimationFrame(loop);
    }

    function start() {
        reset();
        overlay.hidden = true;
        canvas.focus?.();
        last = performance.now();
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(loop);
    }

    function finish() {
        state.running = false;
        const seconds = Math.floor(state.time);
        if (seconds > best) {
            best = seconds;
            saveBest(best);
            bestEl.textContent = best;
            message.textContent = `Новий рекорд: місто світилося ${seconds} с.`;
        } else {
            message.textContent = `Пальне скінчилося. Місто світилося ${seconds} с, ваш рекорд ${best} с.`;
        }
        startButton.textContent = 'Ще одна зміна';
        overlay.hidden = false;
    }

    const directionOf = (e) => ({ ArrowLeft: 'left', a: 'left', A: 'left', 'ф': 'left', ArrowRight: 'right', d: 'right', D: 'right', 'в': 'right' }[e.key]);
    window.addEventListener('keydown', (e) => {
        const direction = directionOf(e);
        if (!direction || !state?.running) return;
        keys.add(direction);
        e.preventDefault();
    });
    window.addEventListener('keyup', (e) => {
        const direction = directionOf(e);
        if (direction) keys.delete(direction);
    });
    canvas.addEventListener('pointermove', (e) => {
        if (!state?.running || e.pointerType === 'mouse' && !e.buttons) return;
        const rect = canvas.getBoundingClientRect();
        state.x = Math.max(VAN.width / 2, Math.min(WIDTH - VAN.width / 2, (e.clientX - rect.left) * WIDTH / rect.width));
    });
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) keys.clear();
    });
    startButton.addEventListener('click', start);

    reset();
    state.running = false;
    state.fuel = 55;
    draw();
})();
