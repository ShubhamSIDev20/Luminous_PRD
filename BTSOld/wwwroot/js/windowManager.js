window.windowDragManager = {
    activeWindow: null,

    initialize: function () {
        if (!this.initialized) {
            document.addEventListener('mousemove', (e) => {
                if (this.activeWindow) {
                    this.activeWindow.invokeMethodAsync('HandleMouseMove', e.clientX, e.clientY);
                }
            });

            document.addEventListener('mouseup', (e) => {
                if (this.activeWindow) {
                    this.activeWindow.invokeMethodAsync('HandleMouseUp');
                    this.activeWindow = null;
                }
            });

            this.initialized = true;
        }
    },

    setActiveWindow: function (windowRef) {
        this.activeWindow = windowRef;
    },

    clearActiveWindow: function () {
        this.activeWindow = null;
    }
};

window.windowDragManager.initialize();