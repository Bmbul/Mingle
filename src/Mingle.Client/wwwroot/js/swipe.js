// Swipe left on any ".swipe-row" (touch or mouse). Its ".swipe-content" follows the finger.
// - Plain rows: past a third of the width (at most 120px) the row's hidden [data-swipe-remove]
//   button is clicked, so Blazor removes it like any other click.
// - ".reveal" rows: the content slides open to uncover the row's ".swipe-actions" buttons and stays
//   open until you tap elsewhere.
// Vertical drags scroll the page as usual, and the click that ends a drag never opens the row.
(function () {
    let row = null, content = null, pointerId = null, startX = 0, startY = 0, startOffset = 0, dx = 0, dragging = false;
    let openRow = null;
    let suppressUntil = 0, suppressIn = null;

    const actionsWidth = r => r.querySelector('.swipe-actions')?.offsetWidth ?? 0;

    function slide(r, x, animate, then) {
        const c = r.querySelector('.swipe-content');
        if (!c) return;
        c.style.transition = animate ? 'transform 0.15s ease-out' : '';
        c.style.transform = x ? `translateX(${x}px)` : '';
        if (animate) setTimeout(() => { c.style.transition = ''; if (then) then(); }, 150);
        else if (then) then();
    }

    function close(r, animate = true) {
        if (!r) return;
        if (openRow === r) openRow = null;
        slide(r, 0, animate, () => r.classList.remove('open', 'swiping'));
    }

    function reset() { row = null; content = null; pointerId = null; dx = 0; dragging = false; }

    document.addEventListener('pointerdown', e => {
        // A tap anywhere but on an open row's buttons closes it (and the tap does nothing else).
        if (openRow && !e.target.closest('.swipe-actions')) {
            const wasOpen = openRow;
            close(openRow);
            if (wasOpen.contains(e.target)) { suppressUntil = Date.now() + 600; suppressIn = wasOpen; reset(); return; }
        }
        const r = e.target.closest('.swipe-row');
        if (!r || e.button > 0 || e.target.closest('.swipe-actions')) return;
        row = r; content = r.querySelector('.swipe-content'); pointerId = e.pointerId;
        startX = e.clientX; startY = e.clientY; dx = 0; dragging = false;
        startOffset = r.classList.contains('open') ? -actionsWidth(r) : 0;
    });

    document.addEventListener('pointermove', e => {
        if (!row || e.pointerId !== pointerId || !content) return;
        const mx = e.clientX - startX, my = e.clientY - startY;
        if (!dragging) {
            if (Math.abs(my) > 12 && Math.abs(my) > Math.abs(mx) * 1.2) { reset(); return; } // a scroll, not a swipe
            if (mx > -8 || Math.abs(mx) < Math.abs(my) * 1.2) return; // mostly sideways, a little slant is fine
            dragging = true;
            row.classList.add('swiping');
            try { row.setPointerCapture(pointerId); } catch { }
        }
        const limit = row.classList.contains('reveal') ? -(actionsWidth(row) + 40) : -Infinity;
        dx = Math.max(limit, Math.min(0, startOffset + mx));
        content.style.transform = `translateX(${dx}px)`;
    });

    function end(e) {
        if (!row || e.pointerId !== pointerId) return;
        const r = row, moved = dx, wasDragging = dragging;
        reset();
        if (!wasDragging) return;
        suppressUntil = Date.now() + 400; suppressIn = r;

        if (r.classList.contains('reveal')) {
            if (-moved > Math.min(60, actionsWidth(r) / 2)) {
                if (openRow && openRow !== r) close(openRow);
                openRow = r;
                r.classList.add('open');
                slide(r, -actionsWidth(r), true);
            } else {
                close(r);
            }
            return;
        }

        if (-moved > Math.min(120, r.offsetWidth / 3)) {
            slide(r, -r.offsetWidth, true, () => {
                r.classList.remove('swiping');
                slide(r, 0, false);
                r.querySelector('[data-swipe-remove]')?.click();
            });
        } else {
            slide(r, 0, true, () => r.classList.remove('swiping'));
        }
    }
    document.addEventListener('pointerup', end);
    document.addEventListener('pointercancel', end);

    // Dragging a link or text with a mouse would start the browser's own drag and cancel the swipe.
    document.addEventListener('dragstart', e => {
        if (e.target.closest?.('.swipe-row')) e.preventDefault();
    });

    // The click that ends a drag, or closes an open row, must not also open the row.
    window.addEventListener('click', e => {
        if (Date.now() < suppressUntil && suppressIn?.contains(e.target)
            && !e.target.closest('[data-swipe-remove]') && !e.target.closest('.swipe-actions')) {
            e.stopPropagation();
            e.preventDefault();
        }
    }, true);

    // Blazor re-renders rows; an action button leaves the page, so forget a row that is gone.
    window.addEventListener('click', e => {
        if (e.target.closest('.swipe-actions')) setTimeout(() => { if (openRow && !openRow.isConnected) openRow = null; }, 0);
    });
})();

// Pages with dots (".pager"): the dots follow the scroll position, and tapping a dot scrolls there.
(function () {
    document.addEventListener('scroll', e => {
        const pages = e.target;
        if (!(pages instanceof Element) || !pages.classList.contains('pager-pages')) return;
        const index = Math.round(pages.scrollLeft / Math.max(1, pages.clientWidth));
        pages.parentElement.querySelectorAll('.pager-dot').forEach((dot, i) => dot.classList.toggle('on', i === index));
    }, true);

    document.addEventListener('click', e => {
        const dot = e.target.closest?.('.pager-dot');
        if (!dot) return;
        const pages = dot.closest('.pager')?.querySelector('.pager-pages');
        pages?.scrollTo({ left: Number(dot.dataset.page) * pages.clientWidth, behavior: 'smooth' });
    });
})();
