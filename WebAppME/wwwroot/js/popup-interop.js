// BatteryTestingSystem.UI Popup Interop
// Handles click-outside detection for dropdowns, popovers, selects, and sheets

window.SafeEyeStreamPopup = {
    _registeredElements: new Map(),
    _documentClickHandler: null,
    _escapeKeyHandler: null,
    _initialized: false,

    // Initialize the global click handler
    initialize: function () {
        if (this._initialized) return;

        this._documentClickHandler = (event) => {
            this._registeredElements.forEach((callback, elementId) => {
                const element = document.getElementById(elementId);
                if (element && !element.contains(event.target)) {
                    callback.invokeMethodAsync('OnClickOutside');
                }
            });
        };

        this._escapeKeyHandler = (event) => {
            if (event.key === 'Escape') {
                this._registeredElements.forEach((callback, elementId) => {
                    callback.invokeMethodAsync('OnEscapePressed');
                });
            }
        };

        // Use setTimeout to avoid catching the click that opened the popup
        document.addEventListener('click', this._documentClickHandler, true);
        document.addEventListener('keydown', this._escapeKeyHandler, true);
        this._initialized = true;
    },

    // Register an element for click-outside detection
    register: function (elementId, dotNetRef) {
        this.initialize();
        this._registeredElements.set(elementId, dotNetRef);
    },

    // Unregister an element
    unregister: function (elementId) {
        const callback = this._registeredElements.get(elementId);
        if (callback) {
            this._registeredElements.delete(elementId);
        }
    },

    // Close all registered popups except the specified one
    closeAllExcept: function (exceptElementId) {
        this._registeredElements.forEach((callback, elementId) => {
            if (elementId !== exceptElementId) {
                callback.invokeMethodAsync('OnClickOutside');
            }
        });
    },

    // Cleanup all handlers
    dispose: function () {
        if (this._documentClickHandler) {
            document.removeEventListener('click', this._documentClickHandler, true);
        }
        if (this._escapeKeyHandler) {
            document.removeEventListener('keydown', this._escapeKeyHandler, true);
        }
        this._registeredElements.clear();
        this._initialized = false;
    }
};

window.setThemeOnBody = function (theme, UserName = null) {

    const key = UserName
        ? `app-theme-${UserName}`
        : "app-theme";

    localStorage.setItem(key, theme);

    document.body.classList.remove(
        "dark",
        "theme-shieldos",
        "theme-ironman",
        "theme-tech",
        "theme-future"
    );

    // Apply the selected theme
    switch (theme) {
        case "Light":
            // Light = default, no class needed
            break;
        case "Dark":
            document.body.classList.add("dark");
            break;
        case "System":
            // Optional: you can detect system preference
            if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
                document.body.classList.add("dark");
            }
            break;
        case "ShieldOS":
            document.body.classList.add("theme-shieldos");
            break;
        case "IronMan":
            document.body.classList.add("theme-ironman");
            break;
        case "Tech":
            document.body.classList.add("theme-tech");
            break;
        case "Future":
            document.body.classList.add("theme-future");
            break;
        default:
            console.warn("Unknown theme:");
    }
};

window.getSavedTheme = function (UserName = null) {

    const key = Object.keys(localStorage)
        .find(k => k.startsWith(`app-theme-${UserName}`));

    return key ? localStorage.getItem(key) : 'Light';
};

window.applySavedTheme = function (UserName = null) {

    const value = window.getSavedTheme(UserName);

    window.setThemeOnBody(value, UserName);
};
