// Swipe left on any ".swipe-row" (touch or mouse): its ".swipe-content" slides open to uncover the
// row's ".swipe-actions" buttons (Edit, Delete) and stays open until you tap elsewhere.
// Vertical drags scroll the page as usual, and the click that ends a drag never opens the row.
// Touch uses touch events: once a finger clearly moves sideways, the page is told not to scroll, which is the
// only reliable way on iPhone (WebKit cancels pointer-based swipes as soon as it starts scrolling).
// A mouse uses pointer events.
(function () {
    let row = null, content = null, startX = 0, startY = 0, startOffset = 0, dx = 0, dragging = false;
    let pointerId = null;   // the mouse pointer being tracked, if any
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

    // A finger or the mouse goes down. Returns false when the gesture is not ours.
    function start(target, x, y) {
        // A tap anywhere but on an open row's buttons closes it (and the tap does nothing else).
        if (openRow && !target.closest('.swipe-actions')) {
            const wasOpen = openRow;
            close(openRow);
            if (wasOpen.contains(target)) { suppressUntil = Date.now() + 600; suppressIn = wasOpen; reset(); return false; }
        }
        const r = target.closest('.swipe-row');
        if (!r || target.closest('.swipe-actions')) return false;
        row = r; content = r.querySelector('.swipe-content');
        startX = x; startY = y; dx = 0; dragging = false;
        startOffset = r.classList.contains('open') ? -actionsWidth(r) : 0;
        return true;
    }

    // The finger or mouse moves. Returns true while the row is being dragged sideways.
    function move(x, y) {
        if (!row || !content) return false;
        const mx = x - startX, my = y - startY;
        if (!dragging) {
            if (Math.abs(my) > 12 && Math.abs(my) > Math.abs(mx) * 1.2) { reset(); return false; } // a scroll, not a swipe
            if (mx > -8 || Math.abs(mx) < Math.abs(my) * 1.2) return false; // mostly sideways, a little slant is fine
            dragging = true;
            row.classList.add('swiping');
        }
        // The card stops where the buttons end, so no gap ever opens between them.
        dx = Math.max(-actionsWidth(row), Math.min(0, startOffset + mx));
        content.style.transform = `translateX(${dx}px)`;
        return true;
    }

    function end() {
        if (!row) return;
        const r = row, moved = dx, wasDragging = dragging;
        reset();
        if (!wasDragging) return;
        suppressUntil = Date.now() + 400; suppressIn = r;

        if (-moved > Math.min(60, actionsWidth(r) / 2)) {
            if (openRow && openRow !== r) close(openRow);
            openRow = r;
            r.classList.add('open');
            slide(r, -actionsWidth(r), true);
        } else {
            close(r);
        }
    }

    // Touch (phones).
    document.addEventListener('touchstart', e => {
        if (e.touches.length !== 1) { if (row) end(); return; }
        start(e.target, e.touches[0].clientX, e.touches[0].clientY);
    }, { passive: true });
    document.addEventListener('touchmove', e => {
        if (!row || e.touches.length !== 1) return;
        // Once the drag is sideways, keep the browser from scrolling or taking the gesture over.
        if (move(e.touches[0].clientX, e.touches[0].clientY) && e.cancelable) e.preventDefault();
    }, { passive: false });
    document.addEventListener('touchend', end);
    document.addEventListener('touchcancel', end);

    // Mouse (and pen). Touch is left to the handlers above.
    document.addEventListener('pointerdown', e => {
        if (e.pointerType === 'touch' || e.button > 0) return;
        if (start(e.target, e.clientX, e.clientY)) pointerId = e.pointerId;
    });
    document.addEventListener('pointermove', e => {
        if (e.pointerType === 'touch' || e.pointerId !== pointerId) return;
        const wasDragging = dragging;
        if (move(e.clientX, e.clientY) && !wasDragging) {
            try { row.setPointerCapture(pointerId); } catch { }
        }
    });
    const pointerEnd = e => { if (e.pointerType !== 'touch' && e.pointerId === pointerId) end(); };
    document.addEventListener('pointerup', pointerEnd);
    document.addEventListener('pointercancel', pointerEnd);
    // The browser can drop the pointer without a pointerup (released outside the window): end the swipe there too.
    document.addEventListener('lostpointercapture', pointerEnd);
    window.addEventListener('blur', () => {
        if (row && dragging) { const r = row; reset(); close(r); }
    });

    // Dragging a link or text with a mouse would start the browser's own drag and cancel the swipe.
    document.addEventListener('dragstart', e => {
        if (e.target.closest?.('.swipe-row')) e.preventDefault();
    });

    // The click that ends a drag, or closes an open row, must not also open the row.
    window.addEventListener('click', e => {
        if (Date.now() < suppressUntil && suppressIn?.contains(e.target)
            && !e.target.closest('.swipe-actions')) {
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
        const pager = pages.closest('.pager');
        pager.querySelectorAll('.pager-dot').forEach((dot, i) => dot.classList.toggle('on', i === index));
        pager.querySelectorAll('.pager-tab').forEach((tab, i) => {
            tab.classList.toggle('on', i === index);
            tab.setAttribute('aria-selected', i === index ? 'true' : 'false');
            if (i === index) tab.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        });
    }, true);

    document.addEventListener('click', e => {
        const dot = e.target.closest?.('.pager-dot, .pager-tab');
        if (!dot) return;
        const pages = dot.closest('.pager')?.querySelector('.pager-pages');
        pages?.scrollTo({ left: Number(dot.dataset.page) * pages.clientWidth, behavior: 'smooth' });
    });
})();
