window.downloadFile = function (filename, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.selectFile = function (inputId) {
    const input = document.getElementById(inputId);
    if (input) {
        input.click();
    }
};


window.cssVars = {
    set: (name, value) =>
        document.documentElement.style.setProperty(name, value),

    remove: (name) =>
        document.documentElement.style.removeProperty(name),

    get: (name) =>
        getComputedStyle(document.documentElement)
            .getPropertyValue(name)
            .trim()
};

window.localStorageHelper = {
    set: (key, value) => {
        localStorage.setItem(key, JSON.stringify(value));
    },

    get: (key) => {
        const item = localStorage.getItem(key);
        return item ? JSON.parse(item) : null;
    },

    remove: (key) => {
        localStorage.removeItem(key);
    }
};

function BlazorDownloadFile(filename, contentType, content) {
    const file = new File([content], filename, { type: contentType });
    const exportUrl = URL.createObjectURL(file);
    const a = document.createElement("a");
    document.body.appendChild(a);
    a.href = exportUrl;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(exportUrl);
}

window.BlazorFocusNext = (shift = false) => {
    const focused = document.activeElement;

    const focusable = Array.from(document.querySelectorAll('button, input, select, textarea, a[href], [tabindex]:not([tabindex="-1"])'))
        .filter(el => el.offsetParent !== null && !el.disabled);

    const index = focusable.indexOf(focused);
    if (index === -1) return;

    let nextIndex;
    if (shift) {
        nextIndex = index > 0 ? index - 1 : index; 
    } else {
        nextIndex = index < focusable.length - 1 ? index + 1 : index; 
    }
    const next = focusable[nextIndex];
    if (next) next.focus();
};


window.printManual = function (htmlContent) {
    const printWindow = window.open('', '_blank');
    if (!printWindow) {
        alert('Please allow pop-ups to export the User Manual as a PDF.');
        return;
    }
    // Built entirely from our own static, hardcoded manual content (no user input) —
    // still avoid document.write in favor of a DOM-based assignment.
    printWindow.document.documentElement.innerHTML = htmlContent;

    const waitForImages = () => {
        const imgs = Array.from(printWindow.document.images);
        return Promise.all(imgs.map(img => img.complete ? Promise.resolve() : new Promise(resolve => {
            img.addEventListener('load', resolve, { once: true });
            img.addEventListener('error', resolve, { once: true });
        })));
    };

    waitForImages().then(() => {
        printWindow.focus();
        printWindow.print();
    });
};

window.getViewportWidth = () => window.innerWidth;
window.getViewportHeight = () => window.innerHeight;
window.getElementRect = (element) => {
    const rect = element.getBoundingClientRect();
    return {
        width: rect.width,
        height: rect.height,
        top: rect.top,
        left: rect.left,
        right: rect.right,
        bottom: rect.bottom
    };
};
