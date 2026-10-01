// ============================================================================
// LEGEND STORAGE (In-Memory - No localStorage)
// ============================================================================
window.chartLegendStore = {
    storage: new Map(),

    loadHidden(key) {
        return this.storage.get(key) || new Set();
    },

    saveHidden(key, hiddenSet) {
        this.storage.set(key, hiddenSet);
    },

    hideSeries(key, seriesName) {
        const hidden = this.loadHidden(key);
        hidden.add(seriesName);
        this.saveHidden(key, hidden);
    },

    showSeries(key, seriesName) {
        const hidden = this.loadHidden(key);
        hidden.delete(seriesName);
        this.saveHidden(key, hidden);
    },

    clear(key) {
        this.storage.delete(key);
    }
};

// ============================================================================
// THEME COLOR UTILITIES
// ============================================================================
const ThemeUtils = {
    getColor(varName, fallback = 'transparent', alpha = null) {
        try {
            const root = document.documentElement;
            const raw = getComputedStyle(root)
                .getPropertyValue(varName)
                .trim();

            if (!raw) return fallback;

            // raw example: "199 89% 42%"
            const hsl = raw.replace(/\s+/g, ', ');

            // If alpha is explicitly provided
            if (alpha !== null && !isNaN(alpha)) {
                return `hsla(${hsl}, ${alpha})`;
            }

            return `hsl(${hsl})`;
        } catch (err) {
            console.warn(`Failed to get theme color for ${varName}:`, err);
            return fallback;
        }
    }
};


// ============================================================================
// CHART CONFIGURATION BUILDERS
// ============================================================================
const ChartConfig = {
    getXAxisConfig(isAdvancedMode, selectedXAxis) {
        const config = {
            gridLineWidth: 0.3,
            gridLineColor: ThemeUtils.getColor('--border', '#e0e0e0'),
            labels: {
                style: {
                    color: ThemeUtils.getColor('--muted-foreground', '#666')
                }
            }
        };

        if (isAdvancedMode && selectedXAxis !== "DateTime") {
            // Custom X-axis (non-DateTime) for main chart
            config.type = 'linear';
            config.ordinal = false;
            config.title = {
                text: selectedXAxis,
                style: {
                    color: ThemeUtils.getColor('--foreground', '#000')
                }
            };
            config.labels.rotation = -45;
            config.labels.align = 'right';
            config.tickPixelInterval = 80;
            config.minRange = 0;
        } else {
            // DateTime X-axis
            config.type = 'datetime';
            config.ordinal = true;
            config.dateTimeLabelFormats = {
                second: '%H:%M:%S',
                minute: '%H:%M',
                hour: '%H:%M',
                day: '%e %b',
                week: '%e %b',
                month: '%b %Y',
                year: '%Y'
            };
            config.labels.rotation = 0;
            config.tickPixelInterval = 150;
        }

        return config;
    },

    getLegendConfig(isAdvancedMode) {
        const config = {
            enabled: true,
            backgroundColor: 'transparent',
            borderColor: 'transparent',
            borderWidth: 0,
            itemStyle: {
                color: ThemeUtils.getColor('--foreground', '#000'),
                fontWeight: 'bold'
            }
        };

        if (isAdvancedMode) {
            config.align = 'left';
            config.verticalAlign = 'middle';
            config.layout = 'vertical';
            config.x = 10;
            config.y = 0;
            config.itemWidth = 150;
        } else {
            config.align = 'center';
            config.verticalAlign = 'bottom';
            config.layout = 'horizontal';
            config.itemWidth = 120;
        }

        return config;
    },

    getRangeSelectorConfig(isAdvancedMode, selectedXAxis) {
        // Disable range selector in advanced mode with custom X-axis
        if (isAdvancedMode && selectedXAxis !== "DateTime") {
            return {
                enabled: false
            };
        }

        return {
            selected: 5,
            buttons: [
                { type: 'minute', count: 1, text: '1m' },
                { type: 'minute', count: 5, text: '5m' },
                { type: 'minute', count: 30, text: '30m' },
                { type: 'hour', count: 1, text: '1h' },
                { type: 'hour', count: 3, text: '3h' },
                { type: 'all', text: 'All' }
            ],
            inputEnabled: false,
            enabled: true
        };
    },

    getYAxisConfig() {
        return {
            labels: {
                enabled: true,
                style: {
                    color: ThemeUtils.getColor('--muted-foreground', '#666')
                },
                format: '{value:.4f}'
            },
            lineWidth: 1,
            gridLineWidth: 0.3,
            gridLineColor: ThemeUtils.getColor('--border', '#e0e0e0'),
            //tickInterval: 10,
            tickAmount: 10,
            opposite: false
        };
    }
};

// ============================================================================
// MAIN HIGHCHARTS HELPER
// ============================================================================
window.highchartsHelper = {
    chartExists(chartId) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        return !!chart;
    },

    addMouseWheelZoom(chart) {
        const container = chart.container;

        // Remove existing wheel listener if any
        if (container.__hcWheelHandler) {
            container.removeEventListener('wheel', container.__hcWheelHandler);
        }

        const wheelHandler = (e) => {
            e.preventDefault();

            const xAxis = chart.xAxis[0];
            const extremes = xAxis.getExtremes();
            const range = extremes.max - extremes.min;
            const mouseX = e.chartX || (e.offsetX !== undefined ? e.offsetX : e.layerX);

            // Calculate the point in data coordinates where the mouse is
            const mouseValue = xAxis.toValue(mouseX);

            // Zoom factor (scroll up = zoom in, scroll down = zoom out)
            const delta = e.deltaY || e.detail || e.wheelDelta;
            const zoomFactor = delta > 0 ? 1.1 : 0.9; // 10% zoom per scroll

            // Calculate new range
            const newRange = range * zoomFactor;

            // Keep the zoom centered on mouse position
            const mousePercent = (mouseValue - extremes.min) / range;
            const newMin = mouseValue - (newRange * mousePercent);
            const newMax = mouseValue + (newRange * (1 - mousePercent));

            // Apply new extremes
            xAxis.setExtremes(newMin, newMax, true, false);
        };

        container.addEventListener('wheel', wheelHandler, { passive: false });
        container.__hcWheelHandler = wheelHandler;
    },

    zoomChart(chartId, factor) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (!chart) return;

        const xAxis = chart.xAxis[0];
        const extremes = xAxis.getExtremes();
        const currentMin = extremes.min;
        const currentMax = extremes.max;
        const range = currentMax - currentMin;
        const center = (currentMin + currentMax) / 2;

        // Calculate new range
        const newRange = range * factor;
        const newMin = center - (newRange / 2);
        const newMax = center + (newRange / 2);

        // Apply new extremes
        xAxis.setExtremes(newMin, newMax, true, false);
    },

    resetZoom(chartId) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (!chart) return;

        const xAxis = chart.xAxis[0];

        // Reset to show all data
        xAxis.setExtremes(null, null, true, false);
    },

    destroyChart(chartId) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (chart) {
            this.unbind(chartId);
            chart.destroy();
        }
    },

    initChart(chartId, title, seriesData, navigatorData, userName = "", isAdvancedMode = false, selectedXAxis = "DateTime") {
        try {
            const legendKey = `${userName}-chart`;
            const hiddenSeries = window.chartLegendStore.loadHidden(legendKey);

            // Apply hidden series visibility
            seriesData.forEach(s => {
                if (hiddenSeries.has(s.name)) {
                    s.visible = false;
                }
            });

            // Set timezone
            Highcharts.setOptions({
                time: { useUTC: false }
            });

            // Destroy existing chart
            this.destroyChart(chartId);

            // Build lookup map for custom X values
            let customXMap = null;
            if (isAdvancedMode && selectedXAxis !== "DateTime" && seriesData.length > 0) {
                customXMap = new Map();
                seriesData[0].data.forEach((point, index) => {
                    customXMap.set(index, point.customX);
                });
            }

            // Build configuration
            const xAxisConfig = ChartConfig.getXAxisConfig(isAdvancedMode, selectedXAxis);
            const legendConfig = ChartConfig.getLegendConfig(isAdvancedMode);
            const rangeSelectorConfig = ChartConfig.getRangeSelectorConfig(isAdvancedMode, selectedXAxis);
            const yAxisConfig = ChartConfig.getYAxisConfig();

            // Add custom labels if in advanced mode
            if (customXMap) {
                xAxisConfig.labels = {
                    rotation: -45,
                    align: 'right',
                    style: {
                        color: ThemeUtils.getColor('--muted-foreground', '#666')
                    },
                    formatter: function () {
                        const customVal = customXMap.get(Math.round(this.value));
                        return customVal !== undefined ? customVal.toFixed(2) : '';
                    }
                };
            }

            // Navigator configuration
            const navigatorConfig = {
                enabled: !isAdvancedMode,
                series: {
                    data: navigatorData,
                    lineWidth: 1
                },
                xAxis: {
                    type: 'datetime',
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
                }
            };

            // Tooltip configuration
            const tooltipConfig = {
                shared: true,
                crosshairs: true
            };

            if (isAdvancedMode && selectedXAxis !== "DateTime") {
                tooltipConfig.formatter = function () {
                    let tooltip = `<b>${Highcharts.dateFormat('%Y-%m-%d %H:%M:%S', this.points[0].point.timestamp)}</b><br/>`;
                    tooltip += `<b>${selectedXAxis}: ${this.points[0].point.customX.toFixed(4)}</b><br/>`;

                    this.points.forEach(point => {
                        tooltip += `<span style="color:${point.color}">●</span> ${point.series.name}: <b>${point.y.toFixed(4)}</b><br/>`;
                    });

                    return tooltip;
                };
            }

            const chart = Highcharts.stockChart(chartId, {
                chart: {
                    zoomType: 'x',
                    backgroundColor: 'transparent',
                    marginLeft: isAdvancedMode ? 180 : undefined,
                    panning: {
                        enabled: true,
                        type: 'x'
                    },
                    panKey: 'shift'
                },
                rangeSelector: rangeSelectorConfig,
                navigator: navigatorConfig,
                scrollbar: {
                    enabled: !isAdvancedMode
                },
                tooltip: tooltipConfig,
                title: {
                    text: title,
                    style: {
                        color: ThemeUtils.getColor('--foreground', '#000')
                    }
                },
                legend: legendConfig,
                xAxis: xAxisConfig,
                yAxis: yAxisConfig,
                plotOptions: {
                    series: {
                        turboThreshold: 0,
                        marker: { enabled: false },
                        dataGrouping: { enabled: !isAdvancedMode },
                        events: {
                            hide() {
                                window.chartLegendStore.hideSeries(legendKey, this.name);
                            },
                            show() {
                                window.chartLegendStore.showSeries(legendKey, this.name);
                            }
                        }
                    }
                },
                credits: { enabled: false },
                series: seriesData
            });

            // Set initial zoom for custom X-axis
            if (isAdvancedMode && selectedXAxis !== "DateTime") {
                const xAxis = chart.xAxis[0];
                if (seriesData.length > 0 && seriesData[0].data.length > 0) {
                    xAxis.setExtremes(0, seriesData[0].data.length - 1, true, false);
                }
            }

            chart.reflow();
            chart.setSize(document.getElementById(chartId).clientWidth, null, false);
            this.bind(chartId);
        } catch (err) {
            console.error("Highcharts initChart error:", err);
        }
    },

    updateYAxisTickInterval(chartId, tickInterval) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (chart && !isNaN(tickInterval) && tickInterval > 0) {
            chart.yAxis[0].update({ tickInterval: tickInterval });
        }
    },

    addPoint(chartId, seriesName, point) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (!chart) return;

        const series = chart.series.find(s => s.name === seriesName);
        if (series) {
            series.addPoint(point, true, false);
        }
    },

    addPointsBatch(chartId, pointsBatch) {
        try {
            const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
            if (!chart) return;

            for (const seriesName in pointsBatch) {
                const series = chart.series.find(s => s.name === seriesName);
                if (series) {
                    series.addPoint(pointsBatch[seriesName], false);
                }
            }

            chart.redraw();
        } catch (err) {
            console.error("Highcharts addPointsBatch error:", err);
        }
    },

    refreshTheme(chartId) {
        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (!chart) return;

        chart.update({
            title: {
                style: {
                    color: ThemeUtils.getColor('--foreground', '#000')
                }
            },
            legend: {
                itemStyle: {
                    color: ThemeUtils.getColor('--foreground', '#000')
                }
            },
            xAxis: {
                gridLineColor: ThemeUtils.getColor('--border', '#e0e0e0'),
                labels: {
                    style: {
                        color: ThemeUtils.getColor('--muted-foreground', '#666')
                    }
                }
            },
            yAxis: {
                gridLineColor: ThemeUtils.getColor('--border', '#e0e0e0'),
                labels: {
                    style: {
                        color: ThemeUtils.getColor('--muted-foreground', '#666')
                    }
                }
            }
        }, true);
    },

    bind(chartId) {
        const container = document.getElementById(chartId);
        if (!container) return;

        const chart = Highcharts.charts.find(c => c && c.renderTo.id === chartId);
        if (!chart) return;

        this.unbind(chartId);

        const resize = () => {
            const width = container.clientWidth;
            if (!width) return;
            chart.setSize(width, null, false);
            chart.reflow();
        };

        window.addEventListener("resize", resize);

        const observer = new ResizeObserver(resize);
        observer.observe(container);

        const fullscreenChangeHandler = () => {
            setTimeout(() => {
                this.refreshTheme(chartId);
                resize();
            }, 100);
        };

        document.addEventListener('fullscreenchange', fullscreenChangeHandler);
        document.addEventListener('webkitfullscreenchange', fullscreenChangeHandler);
        document.addEventListener('mozfullscreenchange', fullscreenChangeHandler);
        document.addEventListener('MSFullscreenChange', fullscreenChangeHandler);

        container.__hcObserver = observer;
        container.__hcResizeHandler = resize;
        container.__hcFullscreenHandler = fullscreenChangeHandler;

        requestAnimationFrame(resize);
    },

    unbind(chartId) {
        const container = document.getElementById(chartId);
        if (!container) return;

        // ResizeObserver
        if (container.__hcObserver) {
            container.__hcObserver.disconnect();
            delete container.__hcObserver;
        }

        // Window resize handler
        if (container.__hcResizeHandler) {
            window.removeEventListener("resize", container.__hcResizeHandler);
            delete container.__hcResizeHandler;
        }

        // Fullscreen handlers
        if (container.__hcFullscreenHandler) {
            document.removeEventListener('fullscreenchange', container.__hcFullscreenHandler);
            document.removeEventListener('webkitfullscreenchange', container.__hcFullscreenHandler);
            document.removeEventListener('mozfullscreenchange', container.__hcFullscreenHandler);
            document.removeEventListener('MSFullscreenChange', container.__hcFullscreenHandler);
            delete container.__hcFullscreenHandler;
        }

        // Mouse wheel handler
        if (container.__hcWheelHandler) {
            container.removeEventListener('wheel', container.__hcWheelHandler);
            delete container.__hcWheelHandler;
        }
    }
};