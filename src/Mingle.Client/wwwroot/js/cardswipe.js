// Cards you swipe instead of tapping a button: drag a ".swipe-card" any way and it follows the finger, tilting as it
// goes; let go far enough from where it started and it flies off the way it was thrown, then the page is told (the
// next card slides in). A short drag springs back. Arrow keys do the same on a computer.
// A drag that starts mostly downward scrolls the page instead, when the page is long enough to scroll.
// Touch uses touch events and stops the page from scrolling once the card is being dragged (the only reliable way on
// iPhone); a mouse uses pointer events. data-enabled="false" turns swiping off.
// data-mode="sides": only left and right count (Blitz: right = got it, left = skip); the card shows data-dir while
// dragged, and the page is told which side.
(function () {
    const threshold = 100;
    let holders = 0; // swipe cards on screen; the page holds still while there is one

    function attach(el, ref) {
        let startX = 0, startY = 0, dx = 0, dy = 0, active = false, dragging = false, busy = false;

        const enabled = () => el.dataset.enabled !== 'false';
        const sides = () => el.dataset.mode === 'sides';
        const pageScrolls = () => document.scrollingElement.scrollHeight > window.innerHeight + 8;

        function begin(x, y) {
            if (busy || !enabled()) return;
            startX = x; startY = y; dx = 0; dy = 0; active = true; dragging = false;
            el.classList.remove('hint');
            el.style.transition = '';
        }

        function move(x, y) {
            if (!active) return false;
            const mx = x - startX, my = y - startY;
            if (!dragging) {
                if (Math.hypot(mx, my) < 10) return false;
                // Mostly downward on a page that scrolls: that is a scroll, not a swipe.
                if (my > 0 && my > Math.abs(mx) && pageScrolls()) { active = false; return false; }
                dragging = true;
                el.classList.add('dragging');
            }
            dx = mx; dy = my;
            el.style.transform = `translate(${dx}px, ${dy}px) rotate(${dx / 22}deg)`;
            pull(Math.min(1, Math.hypot(dx, dy) / (threshold * 1.6)));
            if (sides()) el.dataset.dir = dx > 40 ? 'right' : dx < -40 ? 'left' : '';
            return true;
        }

        function finish() {
            if (!active) return;
            active = false;
            if (!dragging) return;
            el.classList.remove('dragging');
            const far = sides() ? Math.abs(dx) > threshold && Math.abs(dx) > Math.abs(dy) : Math.hypot(dx, dy) > threshold;
            if (far) fly(dx, sides() ? dy / 3 : dy);
            else springBack();
        }

        // How far the next card (the stack behind) has grown into place, from 0 to 1.
        function pull(amount) { el.parentElement?.style.setProperty('--pull', amount); }

        function springBack() {
            el.style.transition = 'transform 0.2s ease-out';
            el.style.transform = '';
            el.dataset.dir = '';
            pull(0);
        }

        // Off the screen along the way it was thrown (vx, vy), then tell the page.
        async function fly(vx, vy) {
            busy = true;
            el.classList.remove('hint');
            try { localStorage.setItem('mingle.swiped', '1'); } catch { }
            const length = Math.hypot(vx, vy) || 1;
            const far = Math.max(window.innerWidth, window.innerHeight) * 1.3;
            el.style.transition = 'transform 0.22s ease-in, opacity 0.22s ease-in';
            el.style.transform = `translate(${vx / length * far}px, ${vy / length * far}px) rotate(${vx / length * 18}deg)`;
            el.style.opacity = '0';
            pull(1);
            await new Promise(r => setTimeout(r, 200));
            const side = Math.abs(vx) >= Math.abs(vy) ? (vx > 0 ? 'right' : 'left') : (vy > 0 ? 'down' : 'up');
            try { await ref.invokeMethodAsync('Swiped', side); } catch { /* the page moved on */ }
            // The next card (Blazor reuses the element) comes in from below.
            el.style.transition = '';
            el.style.transform = '';
            el.style.opacity = '';
            el.dataset.dir = '';
            pull(0);
            el.classList.remove('entering');
            void el.offsetWidth;
            el.classList.add('entering');
            busy = false;
        }

        const onTouchStart = e => { if (e.touches.length === 1) begin(e.touches[0].clientX, e.touches[0].clientY); };
        const onTouchMove = e => {
            if (e.touches.length !== 1) return;
            if (move(e.touches[0].clientX, e.touches[0].clientY) && e.cancelable) e.preventDefault();
        };
        const onPointerDown = e => {
            if (e.pointerType === 'touch' || e.button !== 0) return;
            if (e.target.closest('input, textarea, select, button')) return;
            begin(e.clientX, e.clientY);
        };
        const onPointerMove = e => { if (e.pointerType !== 'touch' && move(e.clientX, e.clientY)) e.preventDefault(); };
        const onPointerUp = e => { if (e.pointerType !== 'touch') finish(); };
        const keys = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1] };
        const onKey = e => {
            if (busy || !enabled() || e.target.closest?.('input, textarea, select')) return;
            const way = keys[e.key];
            if (way && sides() && way[0] === 0) return;
            if (way) { e.preventDefault(); fly(way[0], way[1]); }
        };

        // The first time on this phone, the card nudges side to side to show it can be swiped.
        let seen = false;
        try { seen = localStorage.getItem('mingle.swiped') === '1'; } catch { }
        if (!seen) el.classList.add('hint');

        // Hold the page still while this card is on screen; outside the card, a drag on a page that fits the
        // screen would only bounce it or pull it down to reload.
        holders++;
        document.documentElement.classList.add('swipe-page');
        const onOutsideMove = e => {
            if (el.contains(e.target) || pageScrolls() || !e.cancelable) return;
            if (e.target.closest?.('input, textarea, select, .sheet, .confirm')) return;
            e.preventDefault();
        };
        document.addEventListener('touchmove', onOutsideMove, { passive: false });

        el.addEventListener('touchstart', onTouchStart, { passive: true });
        el.addEventListener('touchmove', onTouchMove, { passive: false });
        el.addEventListener('touchend', finish);
        el.addEventListener('touchcancel', finish);
        el.addEventListener('pointerdown', onPointerDown);
        window.addEventListener('pointermove', onPointerMove);
        window.addEventListener('pointerup', onPointerUp);
        document.addEventListener('keydown', onKey);

        return {
            detach() {
                if (--holders <= 0) { holders = 0; document.documentElement.classList.remove('swipe-page'); }
                document.removeEventListener('touchmove', onOutsideMove);
                window.removeEventListener('pointermove', onPointerMove);
                window.removeEventListener('pointerup', onPointerUp);
                document.removeEventListener('keydown', onKey);
            },
        };
    }

    window.mingleCards = { attach };
})();
