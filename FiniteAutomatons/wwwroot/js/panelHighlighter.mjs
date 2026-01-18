// panelHighlighter.mjs - ES module
'use strict';

function getInputValue(name){
    const el = document.querySelector('input[name="' + name + '"]');
    return el ? el.value : null;
}

// DRAG & DROP helpers for left side panels order and locking
export function initPanelDragAndLock(containerId){
    const container = document.getElementById(containerId);
    if (!container) return null;

    let dragEl = null;
    let lockState = false;

    // Restore lock state from localStorage
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

    // toggle lock via external control
    function setLocked(v){ lockState = !!v; updateLockedClass(); try{ localStorage.setItem('panelsLocked', lockState ? 'true' : 'false'); }catch(e){} }

    return { setLocked };
}

function collectCurrentStates(){
    const ids = new Set();
    const cur = getInputValue('CurrentStateId');
    if (cur) ids.add(String(cur).trim());

    // gather CurrentStates[...] inputs (only bracket-style names to avoid CurrentStates.Index entries)
    document.querySelectorAll('input[name^="CurrentStates["]').forEach(inp => {
        if (inp && inp.value) ids.add(String(inp.value).trim());
    });

    // ALSO collect from server-rendered state-active elements if no inputs found
    if (ids.size === 0) {
        document.querySelectorAll('.state-item.state-active').forEach(el => {
            if (el.dataset && el.dataset.stateId) ids.add(String(el.dataset.stateId).trim());
        });
    }

    return ids;
}

export function highlightCanvasState(){
    const canvas = document.getElementById('automatonCanvas');
    if (!canvas) return;
    let curId = getInputValue('CurrentStateId');
    if (!curId){
        const csInputs = document.querySelectorAll('input[name^="CurrentStates["]');
        if (csInputs && csInputs.length > 0) curId = csInputs[0].value;
    }
    // Fallback to server-rendered active state
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
    // ONLY clear transition highlighting, preserve state-active from server
    document.querySelectorAll('.transition-item.active').forEach(el => el.classList.remove('active'));

    const activeIds = collectCurrentStates();
    if (activeIds.size === 0) return;

    // highlight transitions whose data-from matches any active id
    document.querySelectorAll('.transition-item').forEach(t => {
        const fromRaw = t.getAttribute('data-from');
        if (!fromRaw) return;
        
        for (const id of activeIds){
            if (valuesEqual(id, fromRaw)) {
                t.classList.add('active');
                break;
            }
        }
    });
}
