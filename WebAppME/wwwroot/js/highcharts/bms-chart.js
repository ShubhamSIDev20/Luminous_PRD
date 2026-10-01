// wwwroot/js/bms-chart.js
// Requires Highstock loaded before this file:
// <script src="https://code.highcharts.com/stock/highstock.js"></script>

window.BmsChart = (() => {
    const charts = {};

    function resolveVar(name) {
        return getComputedStyle(document.documentElement)
            .getPropertyValue(name).trim();
    }

    function hslVar(name) {
        const val = resolveVar(name);
        return val ? `hsl(${val})` : null;
    }

    function buildTheme() {
        return {
            bg:      'transparent',
            fg:      hslVar('--foreground')      || '#222',
            muted:   hslVar('--muted-foreground') || '#888',
            card:    hslVar('--card')             || '#fff',
            border:  hslVar('--border')           || '#ddd',
            primary: hslVar('--primary')          || '#0ea5e9',
            btnBg:   hslVar('--secondary')        || '#f0f0f0',
            btnFg:   hslVar('--secondary-foreground') || '#333',
        };
    }
    function fmt4(val) {
        if (val === null || val === undefined) return '�';
        const n = Number(val);
        if (Number.isInteger(n)) return n.toString();
        // Use toPrecision for significant figures, but we want decimal places
        // Round to 4 decimal places, then strip trailing zeros
        return parseFloat(n.toFixed(4)).toString();
    }

    function render(opts) {
        const { chartId, isTimeX, xLabel, series, yAxis, theme: _ } = opts;
        const el = document.getElementById(chartId);
        if (!el) return;

        const t = buildTheme();

        // Destroy existing instance safely (may already be gone if DOM was recreated)
        if (charts[chartId]) {
            try { charts[chartId].destroy(); } catch (_) {}
            delete charts[chartId];
        }

        // Set timezone
        Highcharts.setOptions({
            time: { useUTC: false }
        });

        const chartOpts = {
            chart: {
                backgroundColor: t.bg,
                style: { fontFamily: 'Inter, sans-serif', color: t.fg },
                zooming: { type: 'xy' },
            },
            title: { text: undefined },
            credits: { enabled: false },

            xAxis: {
                type: isTimeX ? 'datetime' : 'linear',
                title: {
                    text: isTimeX ? undefined : (xLabel || ''),
                    style: { color: t.muted }
                },
                dateTimeLabelFormats: {
                    millisecond: '%H:%M:%S.%L',
                    second: '%H:%M:%S',
                    minute: '%H:%M',
                    hour: '%H:%M',
                    day: '%e %b',
                    week: '%e %b',
                    month: '%b %y',
                    year: '%Y'
                },
                labels: { style: { color: t.muted } },
                lineColor: t.border,
                tickColor: t.border,
                gridLineColor: 'rgba(0,0,0,0.05)',
            },

            yAxis: yAxis.map((ya, i) => ({
                title: {
                    text: ya.title?.text ?? '',
                    style: ya.title?.style ?? { color: t.muted }
                },
                labels: {
                    style: ya.labels?.style ?? { color: t.muted },
                    // Cap y-axis tick labels to 4 decimal places too
                    formatter: function () { return fmt4(this.value); },
                },
                //opposite: i % 2 === 1,
                opposite: false,
                tickAmount: 5,
                minPadding: 0.1,
                maxPadding: 0.1,
                //tickInterval: 10,   
                //startOnTick: true,
                //endOnTick: true,
                gridLineColor: 'rgba(0,0,0,0.06)',
            })),

            series: series.map((s, i) => ({
                ...s,
                type: 'line',
                yAxis: 0, //i,
                marker: { enabled: false },
                lineWidth: 1.5,
                // Override tooltip per-point to enforce 4 dp max
                tooltip: {
                    pointFormatter: function () {
                        return `<span style="color:${this.color}">\u25CF</span> ${this.series.name}: <b>${fmt4(this.y)} ${s.tooltip?.valueSuffix ?? ''}</b><br/>`;
                    }
                },
            })),

            legend: {
                enabled: true,
                itemStyle: { color: t.fg, fontWeight: '500' },
                itemHoverStyle: { color: t.primary },
            },

            tooltip: {
                shared: true,
                backgroundColor: t.card,
                borderColor: t.border,
                style: { color: t.fg },
            },

            // Range selector only for time X
            rangeSelector: isTimeX ? {
                selected: 1,
                buttons: [
                    { type: 'minute', count: 30, text: '30m' },
                    { type: 'hour',   count: 1,  text: '1h'  },
                    { type: 'hour',   count: 2,  text: '2h'  },
                    { type: 'all',               text: 'All' },
                ],
                buttonTheme: {
                    fill: t.btnBg,
                    stroke: t.border,
                    style: { color: t.btnFg },
                    states: {
                        hover:  { fill: t.primary, style: { color: '#fff' } },
                        select: { fill: t.primary, style: { color: '#fff' } },
                    },
                },
                inputStyle:  { color: t.fg, backgroundColor: t.btnBg },
                labelStyle:  { color: t.muted },
            } : { enabled: false },

            navigator: isTimeX ? {
                enabled: true,
                //maskFill: hslVar('--primary') ? `${hslVar('--primary')}26` : 'rgba(14,165,233,0.15)',
                series: { color: t.primary },
                xAxis: { 
                    labels:  { style: { color: t.muted } },
                    ordinal: true,
                    dateTimeLabelFormats: {
                        millisecond: '%H:%M:%S.%L',
                        second: '%H:%M:%S',
                        minute: '%H:%M',
                        hour: '%H:%M',
                        day: '%e %b',
                        week: '%e %b',
                        month: '%b %y',
                        year: '%Y'
                    }
                },
                rotation : 0
            } : { enabled: false },

            scrollbar: isTimeX ? {
                barBackgroundColor:    t.btnBg,
                barBorderColor:        t.border,
                buttonBackgroundColor: t.btnBg,
                buttonBorderColor:     t.border,
                trackBackgroundColor:  t.card,
                trackBorderColor:      t.border,
            } : { enabled: false },
        };


        if (isTimeX) {
            charts[chartId] = Highcharts.stockChart(chartId, chartOpts);
        } else {
            charts[chartId] = Highcharts.chart(chartId, chartOpts);
        }
    }

    function destroy(chartId) {
        if (charts[chartId]) {
            try { charts[chartId].destroy(); } catch (_) {}
            delete charts[chartId];
        }
    }

    function reflow(chartId) {
        if (charts[chartId]) {
            try { charts[chartId].reflow(); } catch (_) {}
        }
    }

    /**
     * Append new points to existing series without full chart redraw.
     * seriesPoints: Array of { seriesIndex, points: [[x,y], ...] }
     * redraw is deferred until all series updated, then one redraw call.
     */
    function appendPoints(chartId, seriesPoints) {
        const chart = charts[chartId];
        if (!chart) return;
        try {
            seriesPoints.forEach(sp => {
                const s = chart.series[sp.seriesIndex];
                if (!s) return;
                sp.points.forEach(pt => {
                    s.addPoint(pt, false, false); // no redraw, no shift
                });
            });
            chart.redraw(false); // single redraw, no animation to stay snappy
        } catch (_) {}
    }

    function tickAmount(chartId, tickAmount = null) 
    {
        if (tickAmount != null && tickAmount > 0)
        {
            const chart = charts[chartId];
            if (!chart) return;
            try {
                chart.yAxis.forEach(ya => ya.update({ tickAmount }, false));
                chart.redraw(false);
            } catch (_) { }
        }
    }

    /**
     * Renders the Highcharts chart to a PNG and returns it as base64.
     * Returns null (not rejected) on any failure so the caller can skip gracefully.
     */
    function getChartPngBase64(chartId) {
        return new Promise((resolve) => {   // never rejects — always resolves null on error
            try {
                const chart = charts[chartId];
                if (!chart || typeof chart.getSVG !== 'function') { resolve(null); return; }

                let svgStr;
                try {
                    svgStr = chart.getSVG({
                        chart: { backgroundColor: '#ffffff', width: 1200, height: 500 }
                    });
                } catch (_) { resolve(null); return; }

                if (!svgStr) { resolve(null); return; }

                // Use data-URI instead of createObjectURL — safer inside Blazor WebView
                const encoded = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svgStr);

                const img = new Image();
                img.onload = () => {
                    try {
                        const canvas = document.createElement('canvas');
                        canvas.width  = 1200;
                        canvas.height = 500;
                        const ctx = canvas.getContext('2d');
                        ctx.fillStyle = '#ffffff';
                        ctx.fillRect(0, 0, canvas.width, canvas.height);
                        ctx.drawImage(img, 0, 0);
                        const base64 = canvas.toDataURL('image/png').split(',')[1];
                        resolve(base64 || null);
                    } catch (_) { resolve(null); }
                };
                img.onerror = () => resolve(null);
                img.src = encoded;

            } catch (_) { resolve(null); }
        });
    }

        return { render, destroy, reflow, appendPoints, tickAmount, getChartPngBase64 };
})();
