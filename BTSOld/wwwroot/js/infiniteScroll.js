// wwwroot/js/infiniteScroll.js
// IntersectionObserver-based infinite scroll for Blazor
// Attach via: <script src="js/infiniteScroll.js"></script> in index.html/_Host.cshtml

window.InfiniteScroll = (() => {
    const observers = {};

    function init(containerId, sentinelId, dotNetRef) {
        // Clean up any existing observer for this container
        dispose(containerId);

        const sentinel = document.getElementById(sentinelId);
        if (!sentinel) {
            console.warn(`[InfiniteScroll] Sentinel #${sentinelId} not found`);
            return;
        }

        const container = document.getElementById(containerId);
        const root = container || null; // null = viewport

        const observer = new IntersectionObserver(
            (entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        dotNetRef.invokeMethodAsync('OnSentinelVisible');
                    }
                });
            },
            {
                root: root,
                rootMargin: '0px 0px 200px 0px', // Trigger 200px before hitting bottom
                threshold: 0.01,
            }
        );

        observer.observe(sentinel);
        observers[containerId] = observer;
    }

    function dispose(containerId) {
        if (observers[containerId]) {
            observers[containerId].disconnect();
            delete observers[containerId];
        }
    }

    return { init, dispose };
})();
