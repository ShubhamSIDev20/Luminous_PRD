// Short alarm beep generated with WebAudio - no asset file, no autoplay of media elements.
// Browsers block audio before the first user gesture; a blocked beep must never break
// rendering, so every failure is swallowed.
window.AlarmSound = {
    beep: function (severity) {
        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return;

            if (!window.__alarmAudioCtx) {
                window.__alarmAudioCtx = new Ctx();
            }
            const ctx = window.__alarmAudioCtx;
            if (ctx.state === 'suspended') {
                ctx.resume();
            }

            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'sine';
            osc.frequency.value = severity === 'CRITICAL' ? 880 : 620;
            gain.gain.value = 0.08;

            osc.connect(gain);
            gain.connect(ctx.destination);

            const now = ctx.currentTime;
            osc.start(now);
            gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);
            osc.stop(now + 0.36);
        } catch (e) {
            // Deliberately silent.
        }
    }
};
