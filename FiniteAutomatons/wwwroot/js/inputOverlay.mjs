// inputOverlay.mjs - ES module (info panel removed)
'use strict';

let overlayEl = null;
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
    overlayEl.style.whiteSpace = 'pre';
    overlayEl.style.overflow = 'visible';
    overlayEl.style.boxSizing = 'border-box';
    overlayEl.style.userSelect = 'none';
    wrapper.appendChild(overlayEl);

    if (!document.getElementById('inputOverlayStyles')) {
        const style = document.createElement('style');
        style.id = 'inputOverlayStyles';
        style.textContent = `
            .input-overlay-wrapper { position: relative; display: inline-block; width: 100%; }
            .input-overlay-char { opacity: 0.75; color: #000; display: inline; padding: 0 1px; transition: opacity .15s; }
            .input-overlay-char.current { opacity: 1; background: #90EE90; border-radius: 3px; font-weight: 500; }
        `;
        document.head.appendChild(style);
    }
}

export function show() { if (!overlayEl) createOverlay(); if (!overlayEl) return; isActive = true; overlayEl.style.display='block'; }
export function hide() { if (!overlayEl) return; isActive=false; overlayEl.style.display='none'; if (inputEl) inputEl.style.color=''; }

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

    overlayEl.innerHTML = '';
    for (let i=0;i<value.length;i++) {
        const span = document.createElement('span');
        span.className = 'input-overlay-char' + (i===position ? ' current' : '');
        span.textContent = value[i];
        overlayEl.appendChild(span);
    }

    inputEl.style.color='transparent';
    inputEl.style.caretColor='#000';
}
