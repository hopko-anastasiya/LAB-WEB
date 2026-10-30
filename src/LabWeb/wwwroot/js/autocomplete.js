(($) => {
    const language = {
        inputTooShort: (args) => `Введіть ще ${args.minimum - args.input.length} символ(и)`,
        searching: () => 'Шукаємо…',
        noResults: () => 'Нічого не знайдено',
        errorLoading: () => 'Не вдалося завантажити варіанти',
        loadingMore: () => 'Завантажуємо ще…'
    };

    const escape = (s) => $('<div>').text(s ?? '').html();

    const template = (item) => {
        if (item.loading || !item.hint) return item.text;
        return $(`<span class="ac-option"><span>${escape(item.text)}</span><small>${escape(item.hint)}</small></span>`);
    };

    const mappers = {
        lookup: (x) => ({ id: x.id, text: x.text, hint: x.hint }),
        settlement: (x) => ({
            id: x.name,
            text: x.name,
            hint: x.region,
            latitude: x.latitude,
            longitude: x.longitude
        })
    };

    function setCoordinates(item) {
        const lat = document.querySelector('#Point_Latitude');
        const lng = document.querySelector('#Point_Longitude');
        if (!lat || !lng || item.latitude == null) return;
        lat.value = item.latitude;
        lng.value = item.longitude;
        lat.dispatchEvent(new Event('change'));
    }

    $('[data-autocomplete]').each(function () {
        const $el = $(this);
        const kind = $el.data('autocomplete');
        const isSettlement = kind === 'settlement';

        $el.select2({
            width: '100%',
            language,
            placeholder: $el.data('placeholder'),
            allowClear: !isSettlement,
            minimumInputLength: 3,
            tags: isSettlement,
            createTag: (params) => {
                const term = $.trim(params.term);
                return term ? { id: term, text: term, hint: 'немає в довіднику, буде збережено як є', newTag: true } : null;
            },
            insertTag: (data, tag) => data.push(tag),
            templateResult: template,
            ajax: {
                url: $el.data('url'),
                delay: 300,
                data: (params) => ({ q: params.term, limit: 20 }),
                processResults: (data) => ({ results: data.map(mappers[isSettlement ? 'settlement' : 'lookup']) })
            }
        });

        if (isSettlement) {
            $el.on('select2:select', (e) => {
                if (!e.params.data.newTag) setCoordinates(e.params.data);
            });
        }
        $el.on('change', () => $el.valid?.());
    });
})(jQuery);
