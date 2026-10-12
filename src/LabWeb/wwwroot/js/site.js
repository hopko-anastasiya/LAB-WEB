document.addEventListener('change', (e) => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.dataset.preview) return;
    const img = document.querySelector(input.dataset.preview);
    const file = input.files && input.files[0];
    if (img && file && file.type.startsWith('image/')) {
        img.src = URL.createObjectURL(file);
    }
});

document.addEventListener('input', (e) => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || !input.dataset.iconPreview) return;
    const icon = document.querySelector(input.dataset.iconPreview);
    if (icon) icon.className = 'bi ' + input.value.trim();
});

document.addEventListener('click', (e) => {
    const button = e.target.closest('[data-copy]');
    if (!button) return;
    const source = document.querySelector(button.dataset.copy);
    const text = source?.value ?? source?.textContent ?? '';
    navigator.clipboard?.writeText(text.trim()).then(() => {
        const label = button.textContent;
        button.textContent = 'Скопійовано';
        setTimeout(() => { button.textContent = label; }, 1500);
    });
});
