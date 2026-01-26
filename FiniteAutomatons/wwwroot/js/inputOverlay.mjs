// inputOverlay.mjs - ES module with horizontal scrollbar
'use strict';

let overlayEl = null;
let inputEl = null;
let isActive = false;
let scrollbarEl = null;
let scrollThumbEl = null;
let isDragging = false;
let dragStartX = 0;
let dragStartScrollLeft = 0;

export function init() {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', createOverlay);
    } else {
        createOverlay();
    }

    // Dynamic padding for inputField
    const input = document.getElementById('inputField');
    if (input) {
        function updatePadding() {
            input.style.padding = input.value.length > 0 ? '0.75rem 7rem' : '0.75rem 1.25rem';
        }
        input.addEventListener('input', updatePadding);
        updatePadding();
    }
}

function createOverlay() {
    if (overlayEl) return;
    inputEl = document.getElementById('inputField');
    if (!inputEl) { setTimeout(createOverlay, 100); return; }
    const parent = inputEl.parentNode; if (!parent) { setTimeout(createOverlay,100); return; }

    const wrapper = document.createElement('div');
    wrapper.className = 'input-overlay-wrapper';
    wrapper.style.position = 'relative';
    wrapper.style.display = 'inline-block';
    wrapper.style.width = '100%';
    parent.insertBefore(wrapper, inputEl);
    wrapper.appendChild(inputEl);

    overlayEl = document.createElement('div');
    overlayEl.id = 'inputOverlay';
    overlayEl.style.position = 'absolute';
    overlayEl.style.left = '0';
    overlayEl.style.top = '0';
    overlayEl.style.width = '100%';
    overlayEl.style.height = '100%';
    overlayEl.style.pointerEvents = 'none';
    overlayEl.style.display = 'none';
    overlayEl.style.boxSizing = 'border-box';
    overlayEl.style.userSelect = 'none';
    overlayEl.style.overflow = 'hidden';

    const inner = document.createElement('div');
    inner.id = 'inputOverlayInner';
    inner.style.position = 'absolute';
    inner.style.left = '0';
    inner.style.top = '50%';
    inner.style.transform = 'translateY(-50%)';
    inner.style.whiteSpace = 'pre';
    inner.style.willChange = 'transform';
    overlayEl.appendChild(inner);
    wrapper.appendChild(overlayEl);

    // Create horizontal scrollbar
    scrollbarEl = document.createElement('div');
    scrollbarEl.id = 'inputScrollbar';
    scrollbarEl.style.position = 'absolute';
    scrollbarEl.style.left = '0';
    scrollbarEl.style.bottom = '-16px';
    scrollbarEl.style.width = '100%';
    scrollbarEl.style.height = '12px';
    scrollbarEl.style.background = 'rgba(0,0,0,0.08)';
    scrollbarEl.style.borderRadius = '6px';
    scrollbarEl.style.display = 'none';
    scrollbarEl.style.cursor = 'pointer';
    scrollbarEl.style.zIndex = '10';

    scrollThumbEl = document.createElement('div');
    scrollThumbEl.id = 'inputScrollThumb';
    scrollThumbEl.style.position = 'absolute';
    scrollThumbEl.style.left = '0';
    scrollThumbEl.style.top = '0';
    scrollThumbEl.style.height = '100%';
    scrollThumbEl.style.background = 'rgba(0,0,0,0.3)';
    scrollThumbEl.style.borderRadius = '6px';
    scrollThumbEl.style.cursor = 'grab';
    scrollThumbEl.style.transition = 'background 0.2s';
    scrollbarEl.appendChild(scrollThumbEl);
    wrapper.appendChild(scrollbarEl);

    scrollThumbEl.addEventListener('mousedown', startDrag);
    scrollbarEl.addEventListener('click', scrollbarClick);
    document.addEventListener('mousemove', onDrag);
    document.addEventListener('mouseup', endDrag);

    if (!document.getElementById('inputOverlayStyles')) {
        const style = document.createElement('style');
        style.id = 'inputOverlayStyles';
        style.textContent = `
            .input-overlay-wrapper { position: relative; display: inline-block; width: 100%; margin-bottom: 18px; }
            #inputOverlay { overflow: hidden; }
            #inputOverlayInner { display: inline-block; }
            .input-overlay-char { opacity: 0.75; color: #000; display: inline; padding: 0 1px; transition: opacity .15s; }
            .input-overlay-char.current { opacity: 1; background: #90EE90; border-radius: 3px; font-weight: 500; }
            #inputScrollThumb:hover { background: rgba(0,0,0,0.5); }
            #inputScrollThumb:active { cursor: grabbing; background: rgba(0,0,0,0.6); }
        `;
        document.head.appendChild(style);
    }

    attachInputHandlers();
}

export function show() { 
    if (!overlayEl) createOverlay(); 
    if (!overlayEl) return; 
    isActive = true; 
    overlayEl.style.display='block'; 
    render(inputEl?.selectionStart || 0); 
}

export function hide() { 
    if (!overlayEl) return; 
    isActive=false; 
    overlayEl.style.display='none'; 
    if (scrollbarEl) scrollbarEl.style.display='none';
    if (inputEl) inputEl.style.color=''; 
}

export function render(position) {
    if (!overlayEl) createOverlay();
    if (!overlayEl || !inputEl) return;
    if (!isActive) { inputEl.style.color=''; return; }

    const value = inputEl.value || '';
    const comp = window.getComputedStyle(inputEl);
    overlayEl.style.padding = comp.padding;
    overlayEl.style.font = comp.font;
    overlayEl.style.lineHeight = comp.lineHeight;
    overlayEl.style.textAlign = comp.textAlign;
    overlayEl.style.color = comp.color || '#000';
    overlayEl.style.border = comp.border;
    overlayEl.style.borderRadius = comp.borderRadius;

    const inner = overlayEl.querySelector('#inputOverlayInner');
    if (!inner) return;
    inner.innerHTML = '';
    for (let i=0;i<value.length;i++) {
        const span = document.createElement('span');
        span.className = 'input-overlay-char' + (i===position ? ' current' : '');
        span.textContent = value[i];
        inner.appendChild(span);
    }

    // Set inner width to match full scrollable content width + padding for last chars
    const extraPadding = 20000; // extra pixels to ensure last characters are visible
    inner.style.minWidth = (inputEl.scrollWidth + extraPadding) + 'px';
    inner.style.width = 'max-content';

    inputEl.style.color='transparent';
    inputEl.style.caretColor='#000';

    const scroll = inputEl.scrollLeft;
    inner.style.transform = `translateY(-50%) translateX(${-scroll}px)`;

    updateScrollbar();
}

function attachInputHandlers() {
    if (!inputEl || !overlayEl) return;
    const inner = overlayEl.querySelector('#inputOverlayInner');
    inputEl.addEventListener('input', () => render(inputEl.selectionStart || 0));
    inputEl.addEventListener('click', () => render(inputEl.selectionStart || 0));
    inputEl.addEventListener('keyup', () => render(inputEl.selectionStart || 0));
    inputEl.addEventListener('scroll', () => {
        if (inner) {
            inner.style.transform = `translateY(-50%) translateX(${-inputEl.scrollLeft}px)`;
            updateScrollbar();
        }
    });
}

function updateScrollbar() {
    if (!inputEl || !scrollbarEl || !scrollThumbEl) return;
    const scrollWidth = inputEl.scrollWidth;
    const clientWidth = inputEl.clientWidth;

    if (scrollWidth <= clientWidth) {
        scrollbarEl.style.display = 'none';
        return;
    }

    scrollbarEl.style.display = 'block';
    const thumbWidth = Math.max(30, (clientWidth / scrollWidth) * clientWidth);
    scrollThumbEl.style.width = thumbWidth + 'px';

    const scrollRatio = inputEl.scrollLeft / (scrollWidth - clientWidth);
    const maxThumbLeft = clientWidth - thumbWidth;
    scrollThumbEl.style.left = (scrollRatio * maxThumbLeft) + 'px';
}

function startDrag(e) {
    isDragging = true;
    dragStartX = e.clientX;
    dragStartScrollLeft = inputEl.scrollLeft;
    e.preventDefault();
    e.stopPropagation();
}

function onDrag(e) {
    if (!isDragging || !inputEl || !scrollThumbEl) return;
    const deltaX = e.clientX - dragStartX;
    const scrollWidth = inputEl.scrollWidth;
    const clientWidth = inputEl.clientWidth;
    const thumbWidth = parseFloat(scrollThumbEl.style.width || '0');
    const maxThumbLeft = clientWidth - thumbWidth;
    const ratio = deltaX / maxThumbLeft;
    const newScrollLeft = dragStartScrollLeft + ratio * (scrollWidth - clientWidth);
    inputEl.scrollLeft = Math.max(0, Math.min(scrollWidth - clientWidth, newScrollLeft));

    const inner = overlayEl?.querySelector('#inputOverlayInner');
    if (inner) inner.style.transform = `translateY(-50%) translateX(${-inputEl.scrollLeft}px)`;
    updateScrollbar();
}

function endDrag() {
    isDragging = false;
}

function scrollbarClick(e) {
    if (!inputEl || !scrollbarEl || !scrollThumbEl) return;
    if (e.target === scrollThumbEl) return;
    const rect = scrollbarEl.getBoundingClientRect();
    const clickX = e.clientX - rect.left;
    const scrollWidth = inputEl.scrollWidth;
    const clientWidth = inputEl.clientWidth;
    const thumbWidth = parseFloat(scrollThumbEl.style.width || '0');
    const maxThumbLeft = clientWidth - thumbWidth;
    const targetThumbLeft = clickX - thumbWidth / 2;
    const ratio = Math.max(0, Math.min(1, targetThumbLeft / maxThumbLeft));
    inputEl.scrollLeft = ratio * (scrollWidth - clientWidth);

    const inner = overlayEl?.querySelector('#inputOverlayInner');
    if (inner) inner.style.transform = `translateY(-50%) translateX(${-inputEl.scrollLeft}px)`;
    updateScrollbar();
}
