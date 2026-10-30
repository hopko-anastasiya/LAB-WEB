window.nezlamniCharts = (() => {
    const colors = {
        power: '#F5B841',
        noPower: '#6F8FD6',
        hot: '#E8833A',
        surface: '#14213D',
        grid: '#2A3B61',
        text: '#93A0BA',
        ink: '#E8ECF4'
    };

    Chart.defaults.font.family = 'Onest, system-ui, sans-serif';
    Chart.defaults.color = colors.text;
    Chart.defaults.borderColor = colors.grid;
    Chart.defaults.maintainAspectRatio = false;
    Object.assign(Chart.defaults.plugins.tooltip, {
        backgroundColor: '#1B2B4D',
        borderColor: colors.grid,
        borderWidth: 1,
        titleColor: colors.ink,
        bodyColor: colors.ink,
        padding: 10
    });

    const axis = (extra = {}) => ({
        border: { display: false },
        grid: { color: colors.grid, drawTicks: false },
        ticks: { padding: 6 },
        ...extra
    });
    const noGrid = (extra = {}) => axis({ grid: { display: false }, ...extra });
    const percentAxis = { min: 0, max: 100, ticks: { padding: 6, callback: (v) => `${v}%` } };
    const bar = { borderRadius: 4, borderSkipped: 'start' };

    function create(id, config) {
        const canvas = document.getElementById(id);
        return canvas ? new Chart(canvas, config) : null;
    }

    function analytics({ days, hours, load }) {
        create('daysChart', {
            type: 'bar',
            data: {
                labels: days.map((d) => d.label),
                datasets: [
                    { label: 'Зі світлом', data: days.map((d) => d.withPower), backgroundColor: colors.power, borderColor: colors.surface, borderWidth: { top: 2 }, ...bar },
                    { label: 'Без світла', data: days.map((d) => d.withoutPower), backgroundColor: colors.noPower, ...bar }
                ]
            },
            options: {
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { position: 'top', align: 'start', labels: { boxWidth: 12, boxHeight: 12, useBorderRadius: true, borderRadius: 3 } } },
                scales: {
                    x: noGrid({ stacked: true, ticks: { padding: 6, maxRotation: 0, autoSkipPadding: 14 } }),
                    y: axis({ stacked: true, beginAtZero: true, ticks: { padding: 6, precision: 0 } })
                }
            }
        });

        create('hoursChart', {
            type: 'bar',
            data: {
                labels: hours.map((h) => h.label),
                datasets: [{ label: 'Звіти зі світлом', data: hours.map((h) => h.percent), backgroundColor: colors.power, ...bar }]
            },
            options: {
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: (items) => `${items[0].label}:00`,
                            label: (item) => `${item.raw}% зі світлом, звітів: ${hours[item.dataIndex].reports}`
                        }
                    }
                },
                scales: {
                    x: noGrid({ ticks: { padding: 6, maxRotation: 0, autoSkipPadding: 10 } }),
                    y: axis(percentAxis)
                }
            }
        });

        create('loadChart', {
            type: 'bar',
            data: {
                labels: load.map((p) => p.label),
                datasets: [{
                    label: 'Завантаженість',
                    data: load.map((p) => p.percent),
                    backgroundColor: load.map((p) => (p.percent >= 90 ? colors.hot : colors.power)),
                    maxBarThickness: 16,
                    ...bar
                }]
            },
            options: {
                indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: (item) => {
                                const p = load[item.dataIndex];
                                return p.percent == null ? 'Звітів ще немає' : `${p.occupancy} з ${p.capacity} осіб, ${p.percent}%`;
                            }
                        }
                    }
                },
                scales: {
                    x: axis({ ...percentAxis, max: Math.max(100, ...load.map((p) => p.percent ?? 0)) }),
                    y: noGrid({ ticks: { color: colors.ink, padding: 6 } })
                }
            }
        });
    }

    function occupancy({ labels, values, capacity }) {
        create('occupancyChart', {
            type: 'line',
            data: {
                labels,
                datasets: [{
                    label: 'Людей у пункті',
                    data: values,
                    borderColor: colors.power,
                    backgroundColor: colors.power,
                    borderWidth: 2,
                    pointRadius: 3,
                    pointHoverRadius: 5,
                    tension: .25
                }]
            },
            options: {
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { display: false } },
                scales: {
                    x: noGrid({ ticks: { padding: 6, maxRotation: 0, autoSkipPadding: 18 } }),
                    y: axis({ min: 0, suggestedMax: capacity, ticks: { padding: 6, precision: 0 } })
                }
            }
        });
    }

    return { analytics, occupancy };
})();
