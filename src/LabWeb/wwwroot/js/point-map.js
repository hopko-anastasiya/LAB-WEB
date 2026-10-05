document.querySelectorAll('[data-point-map]').forEach((el) => {
    const latInput = el.dataset.latInput ? document.querySelector(el.dataset.latInput) : null;
    const lngInput = el.dataset.lngInput ? document.querySelector(el.dataset.lngInput) : null;
    const read = () => [
        parseFloat(latInput?.value ?? el.dataset.lat),
        parseFloat(lngInput?.value ?? el.dataset.lng)
    ];
    const valid = ([lat, lng]) => Number.isFinite(lat) && Number.isFinite(lng);

    const start = valid(read()) ? read() : [50.4501, 30.5234];
    const map = L.map(el, { scrollWheelZoom: !!latInput }).setView(start, 15);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);

    const icon = L.divIcon({
        className: 'map-marker',
        html: `<span class="map-lamp lamp-${(el.dataset.status || 'working').toLowerCase()}"></span>`,
        iconSize: [28, 28],
        iconAnchor: [14, 14]
    });
    const marker = L.marker(start, { icon, draggable: !!latInput, keyboard: false }).addTo(map);

    if (!latInput || !lngInput) return;

    const write = (latlng) => {
        latInput.value = latlng.lat.toFixed(6);
        lngInput.value = latlng.lng.toFixed(6);
    };
    map.on('click', (e) => {
        marker.setLatLng(e.latlng);
        write(e.latlng);
    });
    marker.on('dragend', () => write(marker.getLatLng()));
    [latInput, lngInput].forEach((input) => input.addEventListener('change', () => {
        const pos = read();
        if (!valid(pos)) return;
        marker.setLatLng(pos);
        map.panTo(pos);
    }));
});
