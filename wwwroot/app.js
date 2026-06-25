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

// Voice capture for the "Build Profile" feature, via the browser Web Speech API.
// Streams the live transcript back to the Profile component and reports the
// final transcript when recognition ends.
window.wcbSpeech = {
    _rec: null,

    supported: function () {
        return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    },

    start: function (dotNetRef) {
        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) {
            dotNetRef.invokeMethodAsync('OnSpeechError', 'unsupported');
            return;
        }
        this.stop();
        const rec = new SR();
        rec.lang = 'en-US';
        rec.interimResults = true;
        rec.continuous = true;
        let finalText = '';

        rec.onresult = function (e) {
            let interim = '';
            for (let i = e.resultIndex; i < e.results.length; i++) {
                const chunk = e.results[i][0].transcript;
                if (e.results[i].isFinal) finalText += chunk + ' ';
                else interim += chunk;
            }
            dotNetRef.invokeMethodAsync('OnTranscript', (finalText + interim).trim());
        };
        rec.onerror = function (e) {
            dotNetRef.invokeMethodAsync('OnSpeechError', e.error || 'error');
        };
        rec.onend = function () {
            dotNetRef.invokeMethodAsync('OnSpeechEnd', finalText.trim());
        };

        rec.start();
        this._rec = rec;
    },

    stop: function () {
        if (this._rec) {
            try { this._rec.stop(); } catch (e) { /* already stopped */ }
            this._rec = null;
        }
    }
};
