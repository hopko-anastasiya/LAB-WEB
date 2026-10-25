(() => {
    const MAX_SIDE = 1600;
    const QUALITY = .85;
    const SIGNATURES = [
        { type: 'image/jpeg', bytes: [0xff, 0xd8, 0xff] },
        { type: 'image/png', bytes: [0x89, 0x50, 0x4e, 0x47] },
        { type: 'image/webp', bytes: [0x52, 0x49, 0x46, 0x46] }
    ];

    const size = (bytes) => (bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} КБ` : `${(bytes / 1024 / 1024).toFixed(1)} МБ`);

    async function detectType(file) {
        const header = new Uint8Array(await file.slice(0, 4).arrayBuffer());
        return SIGNATURES.find((s) => s.bytes.every((b, i) => header[i] === b))?.type ?? null;
    }

    async function shrink(file, type) {
        const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
        const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        bitmap.close();

        const outputType = type === 'image/png' ? 'image/png' : 'image/jpeg';
        const blob = await new Promise((resolve) => canvas.toBlob(resolve, outputType, QUALITY));
        if (!blob) throw new Error('canvas');
        const extension = outputType === 'image/png' ? 'png' : 'jpg';
        const name = file.name.replace(/\.[^.]+$/, '') || 'photo';
        return new File([blob], `${name}.${extension}`, { type: outputType, lastModified: Date.now() });
    }

    function statusFor(input) {
        let status = input.parentElement.querySelector('.photo-status');
        if (!status) {
            status = document.createElement('p');
            status.className = 'photo-status';
            status.setAttribute('aria-live', 'polite');
            input.after(status);
        }
        return status;
    }

    function setBusy(input, busy) {
        input.form?.querySelectorAll('[type=submit]').forEach((b) => { b.disabled = busy; });
    }

    document.addEventListener('change', async (e) => {
        const input = e.target;
        if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.dataset.preview) return;
        const file = input.files?.[0];
        if (!file) return;

        const status = statusFor(input);
        status.classList.remove('is-error');
        setBusy(input, true);
        try {
            const type = await detectType(file);
            if (!type) {
                input.value = '';
                status.classList.add('is-error');
                status.textContent = 'Це не схоже на зображення. Оберіть JPEG, PNG або WebP.';
                return;
            }

            const result = await shrink(file, type);
            const smaller = result.size < file.size;
            if (smaller) {
                const transfer = new DataTransfer();
                transfer.items.add(result);
                input.files = transfer.files;
            }
            status.textContent = smaller
                ? `Фото зменшено: ${size(file.size)} → ${size(result.size)}, дані про місце зйомки прибрано.`
                : `Фото готове: ${size(file.size)}.`;
        } catch {
            status.textContent = `Фото додано без обробки: ${size(file.size)}.`;
        } finally {
            setBusy(input, false);
        }
    });

    document.addEventListener('dragover', (e) => {
        if (e.target instanceof HTMLInputElement && e.target.type === 'file') e.target.classList.add('is-dragging');
    });
    ['dragleave', 'drop'].forEach((type) => document.addEventListener(type, (e) => {
        if (e.target instanceof HTMLInputElement) e.target.classList.remove('is-dragging');
    }));
})();
