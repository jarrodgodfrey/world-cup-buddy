// Splash-screen countdown driven from Blazor via IJSRuntime.
// Calls back into the Home component each tick and on completion.
window.wcbSplash = {
    _interval: null,

    start: function (dotNetRef, durationMs) {
        this.cancel();
        const startedAt = performance.now();
        this._interval = setInterval(() => {
            const elapsed = performance.now() - startedAt;
            const pct = Math.min(100, (elapsed / durationMs) * 100);
            dotNetRef.invokeMethodAsync('OnTick', pct);
            if (elapsed >= durationMs) {
                this.cancel();
                dotNetRef.invokeMethodAsync('OnComplete');
            }
        }, 50);
    },

    cancel: function () {
        if (this._interval) {
            clearInterval(this._interval);
            this._interval = null;
        }
    }
};
