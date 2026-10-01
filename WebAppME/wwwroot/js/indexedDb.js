// Place this file in wwwroot/js/notifications-db.js

window.BtsDb = {
    dbName: "Btsapp-db",
    storeName: "notifications",
    config: "config",
    version: 1,

    openDB: function () {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.version);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);

            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this.storeName)) {
                    db.createObjectStore(this.storeName, { keyPath: "id" });
                }
            };
        });
    },

    // -----------------------------
    // User configuration actions
    // -----------------------------

    saveConfig: async function (key, configObject) {
        if (!key || typeof key !== "string") {
            console.warn("saveConfig: invalid key");
            return false;
        }

        if (configObject === undefined || configObject === null) {
            console.warn("saveConfig: configObject is null or undefined");
            return false;
        }

        let db;
        try {
            db = await this.openDB();
            const tx = db.transaction(this.configStore, "readwrite");
            const store = tx.objectStore(this.configStore);

            store.put({
                key: key,
                value: configObject,
                updatedAt: Date.now()
            });

            return await new Promise((resolve, reject) => {
                tx.oncomplete = () => resolve(true);
                tx.onerror = () => reject(tx.error);
                tx.onabort = () => reject(tx.error || "Transaction aborted");
            });
        } catch (error) {
            console.error("Error saving config:", error);
            return false;
        } finally {
            if (db) db.close();
        }
    },

    getConfig: async function (key) {
        if (!key || typeof key !== "string") {
            console.warn("getConfig: invalid key");
            return null;
        }

        let db;
        try {
            db = await this.openDB();
            const tx = db.transaction(this.configStore, "readonly");
            const store = tx.objectStore(this.configStore);
            const request = store.get(key);

            return await new Promise((resolve, reject) => {
                request.onsuccess = () => {
                    resolve(request.result?.value ?? null);
                };
                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error("Error getting config:", error);
            return null;
        } finally {
            if (db) db.close();
        }
    }

};