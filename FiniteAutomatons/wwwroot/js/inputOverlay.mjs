// inputOverlay.mjs - ES module
'use strict';

let overlayEl = null;
let infoEl = null;
let inputEl = null;
let isActive = false;

export function init() {
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
    overlayEl.style.alignItems = 'center';
    overlayEl.style.whiteSpace = 'pre';
    overlayEl.style.overflow = 'hidden';
    overlayEl.style.boxSizing = 'border-box';
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
        // Green highlight like in the image
        style.textContent = `
            .input-overlay-char {
                display: inline-block;
                padding: 0;
                margin: 0;
                color: inherit;
                background: transparent;
                font: inherit;
            }
            .input-overlay-highlight {
                background: #90EE90 !important;
                color: #000 !important;
                border-radius: 3px;
                padding: 2px 1px;
                font-weight: 500;
            }
            .input-overlay-wrapper {
                position: relative;
                display: inline-block;
                width: 100%;
            }
        `;
        document.head.appendChild(style);
    }
}

export function show() {
    if (!overlayEl) createOverlay();
    if (!overlayEl) return;
    isActive = true;
    overlayEl.style.display = 'flex';
}

export function hide() {
    if (!overlayEl) return;
    isActive = false;
    overlayEl.style.display = 'none';
    if (inputEl) inputEl.style.color = '';
    if (infoEl) infoEl.textContent = '';
}

export function render(position) {
    if (!overlayEl) createOverlay();
    if (!overlayEl || !inputEl) return;

    if (!isActive) {
        inputEl.style.color = '';
        return;
    }

    const value = inputEl.value || '';
    const comp = window.getComputedStyle(inputEl);
    
    // Match input field styling exactly
    overlayEl.style.padding = comp.padding;
    overlayEl.style.paddingLeft = comp.paddingLeft;
    overlayEl.style.paddingRight = comp.paddingRight;
    overlayEl.style.paddingTop = comp.paddingTop;
    overlayEl.style.paddingBottom = comp.paddingBottom;
    overlayEl.style.font = comp.font;
    overlayEl.style.fontSize = comp.fontSize;
    overlayEl.style.fontFamily = comp.fontFamily;
    overlayEl.style.fontWeight = comp.fontWeight;
    overlayEl.style.lineHeight = comp.lineHeight;
    overlayEl.style.letterSpacing = comp.letterSpacing;
    overlayEl.style.textAlign = comp.textAlign;
    overlayEl.style.color = comp.color || '#000';
    overlayEl.style.border = comp.border;
    overlayEl.style.borderRadius = comp.borderRadius;

    // Build overlay content
    overlayEl.innerHTML = '';
    for (let i = 0; i < value.length; i++) {
        const span = document.createElement('span');
        span.className = 'input-overlay-char' + (i === position ? ' input-overlay-highlight' : '');
        span.textContent = value[i];
        overlayEl.appendChild(span);
    }

    // Update info
    if (infoEl) {
        const ch = (position >= 0 && position < value.length) ? value[position] : '';
        infoEl.textContent = 'Reading position ' + position + (ch ? ': "' + ch + '"' : ' (end)');
    }

    // Make input transparent so overlay shows through
    inputEl.style.color = 'transparent';
    inputEl.style.caretColor = '#000'; // Keep cursor visible
}
