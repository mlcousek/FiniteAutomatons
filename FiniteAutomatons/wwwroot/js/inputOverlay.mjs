// inputOverlay.mjs - ES module
'use strict';

let overlayEl = null;
let infoEl = null;
let inputEl = null;

export function init() {
    // Wait for DOM if not ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', createOverlay);
    } else {
        createOverlay();
    }
}

function createOverlay() {
    if (overlayEl) return;
    inputEl = document.getElementById('inputField');
    if (!inputEl) {
        setTimeout(createOverlay, 100);
        return;
    }

    const parent = inputEl.parentNode;
    if (!parent) {
        setTimeout(createOverlay, 100);
        return;
    }

    const wrapper = document.createElement('div');
    wrapper.className = 'input-overlay-wrapper';
    wrapper.style.display = 'inline-block';
    wrapper.style.position = 'relative';
    parent.insertBefore(wrapper, inputEl);
    wrapper.appendChild(inputEl);

    overlayEl = document.createElement('div');
    overlayEl.id = 'inputOverlay';
    overlayEl.style.position = 'absolute';
    overlayEl.style.left = '0';
    overlayEl.style.top = '0';
    overlayEl.style.height = '100%';
    overlayEl.style.pointerEvents = 'none';
    overlayEl.style.display = 'none';
    overlayEl.style.alignItems = 'center';
    overlayEl.style.whiteSpace = 'pre';
    overlayEl.style.overflow = 'hidden';
    wrapper.appendChild(overlayEl);

    infoEl = document.createElement('div');
    infoEl.id = 'inputPositionInfo';
    infoEl.style.fontSize = '0.85em';
    infoEl.style.marginTop = '6px';
    infoEl.style.color = '#333';
    parent.insertBefore(infoEl, wrapper.nextSibling);

    if (!document.getElementById('inputOverlayStyles')) {
        const style = document.createElement('style');
        style.id = 'inputOverlayStyles';
        style.textContent = '.input-overlay-char{background: transparent; padding:2px; color: inherit; opacity:0.95; font: inherit; display:inline-block;} .input-overlay-highlight{background: #90ee90; border-radius:2px;} .input-overlay-wrapper input{background:transparent;}';
        document.head.appendChild(style);
    }
}

export function show() {
    if (!overlayEl) createOverlay();
    if (!overlayEl) return;
    overlayEl.style.display = 'flex';
}

export function hide() {
    if (!overlayEl) return;
    overlayEl.style.display = 'none';
    if (inputEl) inputEl.style.color = '';
    if (infoEl) infoEl.textContent = '';
}

export function render(position) {
    if (!overlayEl) createOverlay();
    if (!overlayEl || !inputEl) return;

    const value = inputEl.value || '';
    const comp = window.getComputedStyle(inputEl);
    overlayEl.style.paddingLeft = comp.paddingLeft;
    overlayEl.style.paddingTop = comp.paddingTop;
    overlayEl.style.font = comp.font;
    overlayEl.style.lineHeight = comp.lineHeight;
    overlayEl.style.width = inputEl.offsetWidth + 'px';
    overlayEl.style.height = inputEl.offsetHeight + 'px';

    overlayEl.innerHTML = '';
    for (let i = 0; i < value.length; i++) {
        const span = document.createElement('span');
        span.className = 'input-overlay-char' + (i === position ? ' input-overlay-highlight' : '');
        span.textContent = value[i];
        overlayEl.appendChild(span);
    }

    if (infoEl) {
        const ch = (position >= 0 && position < value.length) ? value[position] : '';
        infoEl.textContent = 'Pos: ' + position + (ch ? (', Char: "' + ch + '"') : '');
    }

    inputEl.style.color = 'transparent';
}
