// wwwroot/js/windowDragManager.js

window.windowDragManager = {
    activeWindow: null,

    initialize: function () {
        if (!this.initialized) {
            document.addEventListener('mousemove', this.handleMouseMove.bind(this));
            document.addEventListener('mouseup', this.handleMouseUp.bind(this));

            // Close context menus on click
            document.addEventListener('click', () => {
                const event = new CustomEvent('closeContextMenus');
                document.dispatchEvent(event);
            });

            this.initialized = true;
        }
    },

    setActiveWindow: function (dotNetRef) {
        this.activeWindow = dotNetRef;
    },

    clearActiveWindow: function () {
        this.activeWindow = null;
    },

    handleMouseMove: function (e) {
        if (this.activeWindow) {
            this.activeWindow.invokeMethodAsync('HandleMouseMove', e.clientX, e.clientY);
        }
    },

    handleMouseUp: function (e) {
        if (this.activeWindow) {
            this.activeWindow.invokeMethodAsync('HandleMouseUp');
            this.activeWindow = null;
        }
    }
};

// Initialize on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.windowDragManager.initialize();
    });
} else {
    window.windowDragManager.initialize();
}