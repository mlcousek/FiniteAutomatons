// panelHighlighter.mjs - ES module
'use strict';

function getInputValue(name){
    const el = document.querySelector('input[name="' + name + '"]');
    return el ? el.value : null;
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
