// wwwroot/js/bms-export.js
// Allows child components to trigger the export via window.BmsExport.trigger()
// The actual DotNet export handler is registered by BmsDashboard on load.

window.BmsExport = (() => {
    let _dotnetRef = null;

    function register(dotnetRef) {
        _dotnetRef = dotnetRef;
    }

    function trigger() {
        return new Promise((resolve, reject) => {
            if (!_dotnetRef) {
                console.warn('BmsExport: DotNet reference not registered yet.');
                resolve();
                return;
            }
            _dotnetRef.invokeMethodAsync('ExportExcelAsync')
                .then(() => resolve())
                .catch(err => { console.error('BmsExport error:', err); resolve(); });
        });
    }

    /** Called by Blazor to push a small in-memory byte array as a file download (small sessions only). */
    function downloadFile(fileName, base64) {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const blob = new Blob([bytes], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    /**
     * Opens a server-side download URL in a new tab / via anchor click.
     * Used for large exports so the file streams directly from the server
     * without passing through SignalR / base64 conversion.
     */
    function openDownloadUrl(url) {
        const a = document.createElement('a');
        a.href = url;
        a.target = '_blank';   // new tab — avoids navigating away from the SPA
        a.rel = 'noopener noreferrer';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }

    return { register, trigger, downloadFile, openDownloadUrl };
})();
