// wwwroot/js/elementSize.js
// ResizeObserver-based element width reporting for Blazor components.
// Attach via: <script src="js/elementSize.js"></script> in App.razor
window.ElementSize = (() => {
    const observers = {};

    function observe(elementId, dotNetRef) {
        dispose(elementId);

        const el = document.getElementById(elementId);
        if (!el) return;

        function report() {
            dotNetRef.invokeMethodAsync('OnElementResized', el.clientWidth);
        }

        report();

        if (window.ResizeObserver) {
            const ro = new ResizeObserver(report);
            ro.observe(el);
            observers[elementId] = { disconnect: () => ro.disconnect() };
        } else {
            window.addEventListener('resize', report);
            observers[elementId] = { disconnect: () => window.removeEventListener('resize', report) };
        }
    }

    function dispose(elementId) {
        if (observers[elementId]) {
            observers[elementId].disconnect();
            delete observers[elementId];
        }
    }

    return { observe, dispose };
})();
