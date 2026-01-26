// home.mjs - ES module orchestrator
'use strict';

import * as InputOverlay from './inputOverlay.mjs';
import * as PanelHighlighter from './panelHighlighter.mjs';

function parseBool(val){
    if (val === undefined || val === null) return false;
    return String(val).toLowerCase() === 'true';
}

function getInputValue(name){
    var el = document.querySelector('input[name="' + name + '"]');
    return el ? el.value : null;
}

function setStartAsStop(isSimulating){
    var startBtn = document.querySelector('button[title="Start"], button[title="Stop"]');
    if (!startBtn) return;
    if (isSimulating){
        startBtn.dataset.origAction = startBtn.getAttribute('formaction') || '';
        startBtn.setAttribute('formaction', '/Automaton/BackToStart');
        startBtn.title = 'Stop';
        var span = startBtn.querySelector('span'); if (span) span.textContent = 'Stop';
        var icon = startBtn.querySelector('i'); if (icon){ icon.classList.remove('fa-play-circle'); icon.classList.add('fa-stop'); }
    } else {
        if (startBtn.dataset.origAction) startBtn.setAttribute('formaction', startBtn.dataset.origAction);
        startBtn.title = 'Start';
        var span = startBtn.querySelector('span'); if (span) span.textContent = 'Start';
        var icon = startBtn.querySelector('i'); if (icon){ icon.classList.remove('fa-stop'); icon.classList.add('fa-play-circle'); }
    }
}

function disableInput(dis){
    var input = document.getElementById('inputField');
    if (!input) return;
    if (dis){
        input.readOnly = true; // preserve value in POST
        input.classList.add('input-readonly');
    } else {
        input.readOnly = false;
        input.classList.remove('input-readonly');
    }
}

function updateAll(){
    var posVal = parseInt(getInputValue('Position') || '0',10);
    var hasExecuted = parseBool(getInputValue('HasExecuted'));
    var isSimulating = hasExecuted || (posVal && posVal >0);
    
    setStartAsStop(isSimulating);
    disableInput(isSimulating);
    
    // Show/hide overlay based on simulation state
    if (isSimulating) {
        InputOverlay.show();
        InputOverlay.render(posVal);
    } else {
        InputOverlay.hide();
    }
    
    // Update panel highlights
    PanelHighlighter.highlightCanvasState();
    PanelHighlighter.highlightLeftPanel();
}

document.addEventListener('DOMContentLoaded', function(){
    InputOverlay.init();
    
    // Initial update after a short delay to ensure DOM is fully ready
    setTimeout(updateAll,100);
    
    window.addEventListener('resize', function(){ 
        var posVal = parseInt(getInputValue('Position') || '0',10);
        InputOverlay.render(posVal); 
    });
    
    // Poll for updates
    setInterval(updateAll,300);

    var startBtn = document.querySelector('button[title="Start"], button[title="Stop"]');
    if (startBtn){ 
        startBtn.addEventListener('click', function(){ 
            // Overlay will be shown by updateAll when server responds
            setTimeout(updateAll,50);
        }); 
    }
    
    var resetBtn = document.querySelector('button[title="Reset"]');
    if (resetBtn){ 
        resetBtn.addEventListener('click', function(){ 
            InputOverlay.hide();
            setTimeout(updateAll,50);
        }); 
    }

    // Initialize panel drag & lock (left-side panels reordering)
    try{
        const panels = PanelHighlighter.initPanelDragAndLock('panelContainer');
        const lockBtn = document.getElementById('panelsLockBtn');
        if (lockBtn && panels){
            // set initial icon based on stored state
            const locked = (function(){ try{ return localStorage.getItem('panelsLocked') === 'true'; }catch(e){ return false; } })();
            lockBtn.setAttribute('aria-pressed', locked ? 'true' : 'false');
            const icon = lockBtn.querySelector('i');
            if (icon){ icon.classList.toggle('fa-lock', locked); icon.classList.toggle('fa-lock-open', !locked); }

            lockBtn.addEventListener('click', function(){
                const currently = lockBtn.getAttribute('aria-pressed') === 'true';
                const next = !currently;
                lockBtn.setAttribute('aria-pressed', next ? 'true' : 'false');
                if (icon){ icon.classList.toggle('fa-lock', next); icon.classList.toggle('fa-lock-open', !next); }
                panels.setLocked(next);
            });
        }
    }catch(e){ console.warn('panel drag init failed', e); }

    // Enable import button only when file selected
    try {
        const importForm = document.getElementById('importForm');
        if (importForm) {
            const fileInput = importForm.querySelector('input[type="file"][name="upload"]');
            const submitBtn = importForm.querySelector('button[type="submit"]');
            if (fileInput && submitBtn) {
                submitBtn.disabled = true;
                fileInput.addEventListener('change', function(){ submitBtn.disabled = !fileInput.files || fileInput.files.length === 0; });
            }
        }
    } catch(e) { }
});
