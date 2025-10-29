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
    input.disabled = !!dis;
    InputOverlay.render(parseInt(getInputValue('Position') || '0',10));
}

function updateAll(){
    var posVal = parseInt(getInputValue('Position') || '0',10);
    var hasExecuted = parseBool(getInputValue('HasExecuted'));
    var isSimulating = hasExecuted || (posVal && posVal >0);
    setStartAsStop(isSimulating);
    disableInput(isSimulating);
    if (isSimulating) InputOverlay.show(); else InputOverlay.hide();
    InputOverlay.render(parseInt(getInputValue('Position') || '0',10));
    PanelHighlighter.highlightCanvasState();
    PanelHighlighter.highlightLeftPanel();
}

document.addEventListener('DOMContentLoaded', function(){
    InputOverlay.init();
    updateAll();
    window.addEventListener('resize', function(){ InputOverlay.render(parseInt(getInputValue('Position') || '0',10)); });
    setInterval(updateAll,300);

    var startBtn = document.querySelector('button[title="Start"], button[title="Stop"]');
    if (startBtn){ startBtn.addEventListener('click', function(){ InputOverlay.show(); }); }
    var resetBtn = document.querySelector('button[title="Reset"]');
    if (resetBtn){ resetBtn.addEventListener('click', function(){ InputOverlay.hide(); }); }
});
