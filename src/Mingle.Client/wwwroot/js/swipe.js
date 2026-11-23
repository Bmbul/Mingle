// Swipe left to remove, for any ".swipe-row". The row's ".swipe-content" follows the finger (or mouse);
// past a third of the width (at most 120px) the row's hidden [data-swipe-remove] button is clicked,
// so Blazor handles the removal like any other click. Vertical drags scroll the page as usual.
(function () {
    let row = null, content = null, pointerId = null, startX = 0, startY = 0, dx = 0, dragging = false;
    let lastSwipe = { row: null, at: 0 };

    function reset() { row = null; content = null; pointerId = null; dx = 0; dragging = false; }

    function animateTo(el, x, then) {
        el.style.transition = 'transform 0.15s ease-out';
        el.style.transform = x ? `translateX(${x}px)` : '';
        setTimeout(() => { el.style.transition = ''; if (then) then(); }, 150);
    }

    document.addEventListener('pointerdown', e => {
        const r = e.target.closest('.swipe-row');
        if (!r || e.button > 0) return;
        row = r; content = r.querySelector('.swipe-content'); pointerId = e.pointerId;
        startX = e.clientX; startY = e.clientY; dx = 0; dragging = false;
    });

    document.addEventListener('pointermove', e => {
        if (!row || e.pointerId !== pointerId || !content) return;
        const mx = e.clientX - startX, my = e.clientY - startY;
        if (!dragging) {
            if (Math.abs(my) > 10 && Math.abs(my) > Math.abs(mx)) { reset(); return; } // a scroll, not a swipe
            if (mx > -10 || Math.abs(mx) < Math.abs(my) * 1.5) return;
            dragging = true;
            row.classList.add('swiping');
            try { row.setPointerCapture(pointerId); } catch { }
        }
        dx = Math.min(0, mx);
        content.style.transform = `translateX(${dx}px)`;
    });

    function end(e) {
        if (!row || e.pointerId !== pointerId) return;
        const r = row, c = content, moved = dx, wasDragging = dragging;
        reset();
        if (!wasDragging || !c) return;
        lastSwipe = { row: r, at: Date.now() };
        if (-moved > Math.min(120, r.offsetWidth / 3)) {
            animateTo(c, -r.offsetWidth, () => {
                r.classList.remove('swiping');
                c.style.transform = '';
                r.querySelector('[data-swipe-remove]')?.click();
            });
        } else {
            animateTo(c, 0, () => r.classList.remove('swiping'));
        }
    }
    document.addEventListener('pointerup', end);
    document.addEventListener('pointercancel', end);

    // The click that ends a drag must not also open the card.
    window.addEventListener('click', e => {
        if (Date.now() - lastSwipe.at < 400 && lastSwipe.row?.contains(e.target) && !e.target.closest('[data-swipe-remove]')) {
            e.stopPropagation();
            e.preventDefault();
        }
    }, true);
})();
