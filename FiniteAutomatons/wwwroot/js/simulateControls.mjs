// simulateControls.mjs
// Dynamically manages enable/disable state of simulation buttons to avoid server-side duplication.
'use strict';

function qs(sel){ return document.querySelector(sel); }
function qsa(sel){ return Array.from(document.querySelectorAll(sel)); }

function parseBool(val){
    if (val === undefined || val === null) return false;
    return String(val).toLowerCase() === 'true';
}

function getHidden(name){
    const el = qs('input[name="' + name + '"]');
    return el ? el.value : null;
}

function updateButtons(){
    const hasExecuted = parseBool(getHidden('HasExecuted'));
    const pos = parseInt(getHidden('Position') || '0', 10);
    const inputEl = qs('#inputField');
    const inputLen = inputEl ? inputEl.value.length : 0;

    // Base rule: buttons require execution started
    qsa('[data-sim-action]').forEach(btn => {
        if (!hasExecuted){
            btn.setAttribute('disabled','');
        } else {
            btn.removeAttribute('disabled');
        }
    });

    // When at first position (0) disable BackToStart & StepBackward
    if (hasExecuted && pos <= 0){
        const backToStart = qs('[data-sim-action="backToStart"]');
        const stepBackward = qs('[data-sim-action="stepBackward"]');
        if (backToStart) backToStart.setAttribute('disabled','');
        if (stepBackward) stepBackward.setAttribute('disabled','');
    }

    // When at or past end of input disable StepForward & ExecuteAll
    if (hasExecuted && pos >= inputLen){
        const stepForward = qs('[data-sim-action="stepForward"]');
        const executeAll = qs('[data-sim-action="executeAll"]');
        if (stepForward) stepForward.setAttribute('disabled','');
        if (executeAll) executeAll.setAttribute('disabled','');
    }
}

export function init(){
    // Initial state
    updateButtons();
    // Poll (cheap) because page re-posts for state changes
    setInterval(updateButtons, 500);
}

document.addEventListener('DOMContentLoaded', init);
