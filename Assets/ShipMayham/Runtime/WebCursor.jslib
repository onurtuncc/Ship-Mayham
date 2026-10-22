// Own only the game's canvas, never the browser prototype or global error handler.
mergeInto(LibraryManager.library, {
    ShipCursor_Install: function () {
        var canvas = Module.canvas;
        if (!canvas || canvas.__shipCursorGuard || !canvas.requestPointerLock) return;
        var originalRequest = canvas.requestPointerLock;
        canvas.requestPointerLock = function () {
            function report(error) {
                console.warn("[ShipMayham] Pointer lock was not granted. Click the game center to retry.", error);
            }
            try {
                var request = originalRequest.apply(this, arguments);
                // Older browsers return void; modern browsers reject a Promise when
                // a request is interrupted, denied, or the native lock fails.
                return request && typeof request.catch === "function" ? request.catch(report) : request;
            } catch (error) {
                report(error);
            }
        };
        canvas.__shipCursorGuard = true;
    }
});
