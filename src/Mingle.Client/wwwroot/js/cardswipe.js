// Cards you swipe instead of tapping buttons: drag a ".swipe-card" left, right or up past a threshold and it flies
// off, then the page is told which way ("left" / "right" / "up") and the next card slides in. The card tilts while
// dragged. Arrow keys do the same on a computer. Dragging down
// still scrolls the page; dragging up scrolls it too when "up" is not allowed.
// Touch uses touch events and stops the page from scrolling once the finger clearly moves sideways (the only reliable
// way on iPhone); a mouse uses pointer events. A side that is not allowed (data-left / data-right empty) springs back.
(function () {
    const threshold = 90;

    function attach(el, ref) {
        let startX = 0, startY = 0, dx = 0, dy = 0, axis = null, active = false, dragging = false, busy = false;

        const allowed = dir => el.dataset[dir] === 'true';

        function begin(x, y) {
            if (busy) return;
            startX = x; startY = y; dx = 0; dy = 0; axis = null; active = true; dragging = false;
            el.style.transition = '';
        }

        function move(x, y) {
            if (!active) return false;
            const mx = x - startX, my = y - startY;
            if (!dragging) {
                if (Math.abs(mx) < 10 && Math.abs(my) < 10) return false;
                if (Math.abs(my) > Math.abs(mx)) {
                    // Up is a swipe when allowed; down (or up when not allowed) is a scroll.
                    if (my > 0 || !allowed('up')) { active = false; return false; }
                    axis = 'y';
                } else {
                    axis = 'x';
                }
                dragging = true;
                el.classList.add('dragging');
            }
            if (axis === 'x') {
                dx = mx;
                el.style.transform = `translateX(${dx}px) rotate(${dx / 22}deg)`;
            } else {
                dy = Math.min(0, my);
                el.style.transform = `translateY(${dy}px)`;
            }
            const dir = axis === 'x' ? (dx > 40 ? 'right' : dx < -40 ? 'left' : '') : (dy < -40 ? 'up' : '');
            el.dataset.dir = dir && allowed(dir) ? dir : '';
            return true;
        }

        function finish() {
            if (!active) return;
            active = false;
            if (!dragging) return;
            el.classList.remove('dragging');
            const dir = axis === 'x' ? (dx > threshold ? 'right' : dx < -threshold ? 'left' : null) : (dy < -threshold ? 'up' : null);
            if (dir && allowed(dir)) fly(dir);
            else springBack();
        }

        function springBack() {
            el.style.transition = 'transform 0.2s ease-out';
            el.style.transform = '';
            el.dataset.dir = '';
        }

        async function fly(dir) {
            busy = true;
            el.dataset.dir = dir;
            el.style.transition = 'transform 0.2s ease-in, opacity 0.2s ease-in';
            el.style.transform = dir === 'up' ? 'translateY(-120vh)' : `translateX(${dir === 'right' ? 140 : -140}%) rotate(${dir === 'right' ? 18 : -18}deg)`;
            el.style.opacity = '0';
            await new Promise(r => setTimeout(r, 190));
            try { await ref.invokeMethodAsync('Swiped', dir); } catch { /* the page moved on */ }
            // The next card (Blazor reuses the element) comes in from below.
            el.style.transition = '';
            el.style.transform = '';
            el.style.opacity = '';
            el.dataset.dir = '';
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
        const onKey = e => {
            if (busy || e.target.closest?.('input, textarea, select')) return;
            const dir = e.key === 'ArrowLeft' ? 'left' : e.key === 'ArrowRight' ? 'right' : e.key === 'ArrowUp' ? 'up' : null;
            if (dir && allowed(dir)) { e.preventDefault(); fly(dir); }
        };

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
                window.removeEventListener('pointermove', onPointerMove);
                window.removeEventListener('pointerup', onPointerUp);
                document.removeEventListener('keydown', onKey);
            },
        };
    }

    window.mingleCards = { attach };
})();
