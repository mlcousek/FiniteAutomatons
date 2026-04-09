// panelHighlighter.mjs - ES module
'use strict';

function getInputValue(name){
    const el = document.querySelector('input[name="' + name + '"]');
    return el ? el.value : null;
}

export function initPanelDragAndLock(containerId){
    const container = document.getElementById(containerId);
    if (!container) return null;

    let dragEl = null;
    let lockState = false;

    try{ lockState = localStorage.getItem('panelsLocked') === 'true'; }catch(e){ }

    function updateLockedClass(){
        container.querySelectorAll('.automaton-detail-section').forEach(s => {
            s.classList.toggle('locked', lockState);
            if (!lockState){ s.setAttribute('draggable','true'); } else { s.removeAttribute('draggable'); }
        });
    }

    updateLockedClass();

    container.addEventListener('dragstart', function(e){
        if (lockState) { e.preventDefault(); return; }
        const target = e.target.closest('.automaton-detail-section');
        if (!target) { e.preventDefault(); return; }
        dragEl = target;
        target.classList.add('dragging');
        e.dataTransfer.effectAllowed = 'move';
        try{ e.dataTransfer.setData('text/plain','drag'); }catch(_){ }
    });

    container.addEventListener('dragend', function(e){
        if (dragEl) dragEl.classList.remove('dragging');
        dragEl = null;
        savePanelOrder();
    });

    container.addEventListener('dragover', function(e){
        if (lockState) return;
        e.preventDefault();
        const after = getDragAfterElement(container, e.clientY);
        const dragging = container.querySelector('.dragging');
        if (!dragging) return;
        if (after == null) container.appendChild(dragging);
        else container.insertBefore(dragging, after);
    });

    function getDragAfterElement(container, y){
        const els = [...container.querySelectorAll('.automaton-detail-section:not(.dragging)')];
        return els.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height/2;
            if (offset < 0 && (closest == null || offset > closest.offset)) {
                return { offset: offset, element: child };
            } else return closest;
        }, null)?.element || null;
    }

    function savePanelOrder() {
        const order = Array.from(container.querySelectorAll('.automaton-detail-section'))
                           .map(el => el.getAttribute('data-panel-id'))
                           .filter(Boolean);

        try {
            const allPrefs = JSON.parse(localStorage.getItem('panelOrderPrefs') || '{}');
            allPrefs.automatonSidebar = order;
            localStorage.setItem('panelOrderPrefs', JSON.stringify(allPrefs));
        } catch(e) {}

        fetch('/api/preferences/panel-order', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                preferences: localStorage.getItem('panelOrderPrefs') || '{}'
            })
        }).catch(() => {}); 
    }

    async function restorePanelOrder() {
        let prefsJSON = localStorage.getItem('panelOrderPrefs');
        try {
            const res = await fetch('/api/preferences/panel-order');
            if (res.ok) {
                const data = await res.json();
                if (data.preferences) {
                    prefsJSON = data.preferences;
                    localStorage.setItem('panelOrderPrefs', prefsJSON);
                }
            }
        } catch(e) {}

        if (prefsJSON) {
            try {
                const prefs = JSON.parse(prefsJSON);
                const order = prefs.automatonSidebar;
                if (Array.isArray(order) && order.length > 0) {
                    const sections = Array.from(container.querySelectorAll('.automaton-detail-section'));
                    order.forEach(id => {
                        const el = sections.find(s => s.getAttribute('data-panel-id') === id);
                        if (el) container.appendChild(el);
                    });
                }
            } catch(e) {}
        }
    }

    restorePanelOrder();
    function setLocked(v){ lockState = !!v; updateLockedClass(); try{ localStorage.setItem('panelsLocked', lockState ? 'true' : 'false'); }catch(e){} }

    return { setLocked };
}

function collectCurrentStates(){
    const idsMap = new Map();

    const cur = getInputValue('CurrentStateId');
    if (cur) idsMap.set(String(cur).trim(), true);

    document.querySelectorAll('input[name^="CurrentStates["]').forEach(inp => {
        if (inp && inp.value) idsMap.set(String(inp.value).trim(), true);
    });

    if (idsMap.size === 0) {
        document.querySelectorAll('.state-item.state-active').forEach(el => {
            if (el.dataset && el.dataset.stateId) idsMap.set(String(el.dataset.stateId).trim(), true);
        });
    }

    return Array.from(idsMap.keys());
}

export function highlightCanvasState(){
    const canvas = document.getElementById('automatonCanvas');
    if (!canvas) return;
    let curId = getInputValue('CurrentStateId');
    if (!curId){
        const csInputs = document.querySelectorAll('input[name^="CurrentStates["]');
        if (csInputs && csInputs.length > 0) curId = csInputs[0].value;
    }
    if (!curId) {
        const activeState = document.querySelector('.state-item.state-active');
        if (activeState && activeState.dataset.stateId) curId = activeState.dataset.stateId;
    }
    if (curId) canvas.dataset.currentState = String(curId).trim(); else delete canvas.dataset.currentState;
    if (window.highlightAutomatonExecution && typeof window.highlightAutomatonExecution === 'function'){
        try{ window.highlightAutomatonExecution(canvas.dataset.currentState); }catch(e){ console.warn('highlightAutomatonExecution error', e); }
    }
}

function valuesEqual(a, b){
    if (a == null || b == null) return false;
    const ta = String(a).trim();
    const tb = String(b).trim();
    const na = Number(ta);
    const nb = Number(tb);
    if (!Number.isNaN(na) && !Number.isNaN(nb)) return na === nb;
    return ta === tb;
}

export function highlightLeftPanel(){
    document.querySelectorAll('.transition-item.active').forEach(el => el.classList.remove('active'));
    document.querySelectorAll('.state-item.state-active').forEach(el => el.classList.remove('state-active'));

    for (let i = 0; i < 16; i++) {
        document.querySelectorAll(`.transition-item.active-branch-${i}`).forEach(el => el.classList.remove(`active-branch-${i}`));
        document.querySelectorAll(`.state-item.active-branch-${i}`).forEach(el => el.classList.remove(`active-branch-${i}`));
    }

    const activeIds = collectCurrentStates();
    if (activeIds.length === 0) return;

    const isNonted = activeIds.length > 1;

    activeIds.forEach((id, idx) => {
        const cls = isNonted ? `active-branch-${idx % 16}` : null;

        document.querySelectorAll(`.state-item[data-state-id="${id}"]`).forEach(el => {
            el.classList.add('state-active');
            if (cls) el.classList.add(cls);
        });

        document.querySelectorAll('.transition-item').forEach(t => {
            const fromRaw = t.getAttribute('data-from');
            if (fromRaw && valuesEqual(id, fromRaw)) {
                t.classList.add('active');
                if (cls) t.classList.add(cls);
            }
        });
    });
}
