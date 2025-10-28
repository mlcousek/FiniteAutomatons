(function(){
 // Helper to parse boolean-like values returned from hidden inputs
 function parseBool(val){
 if (val === undefined || val === null) return false;
 return String(val).toLowerCase() === 'true';
 }

 function getInputValue(name){
 var el = document.querySelector('input[name="' + name + '"]');
 return el ? el.value : null;
 }

 function setStartAsStop(isSimulating){
 var startBtn = document.querySelector('button[title="Start"]');
 if (!startBtn) return;
 if (isSimulating){
 startBtn.dataset.origAction = startBtn.getAttribute('formaction') || '';
 // change to BackToStart endpoint so clicking acts as Stop
 startBtn.setAttribute('formaction', '/Automaton/BackToStart');
 startBtn.title = 'Stop';
 // update label/icon if present
 var span = startBtn.querySelector('span');
 if (span) span.textContent = 'Stop';
 var icon = startBtn.querySelector('i');
 if (icon){ icon.classList.remove('fa-play-circle'); icon.classList.add('fa-stop'); }
 } else {
 // restore
 if (startBtn.dataset.origAction) startBtn.setAttribute('formaction', startBtn.dataset.origAction);
 startBtn.title = 'Start';
 var span = startBtn.querySelector('span');
 if (span) span.textContent = 'Start';
 var icon = startBtn.querySelector('i');
 if (icon){ icon.classList.remove('fa-stop'); icon.classList.add('fa-play-circle'); }
 }
 }

 function disableInput(dis){
 var input = document.getElementById('inputField');
 if (!input) return;
 input.disabled = !!dis;
 // show overlay for highlighting characters
 ensureOverlay();
 renderOverlay();
 }

 function ensureOverlay(){
 if (document.getElementById('inputOverlay')) return;
 var input = document.getElementById('inputField');
 if (!input) return;
 var wrapper = document.createElement('div');
 wrapper.style.position = 'relative';
 wrapper.style.display = 'inline-block';
 wrapper.className = 'input-overlay-wrapper';
 input.parentNode.insertBefore(wrapper, input);
 wrapper.appendChild(input);

 var overlay = document.createElement('div');
 overlay.id = 'inputOverlay';
 overlay.style.position = 'absolute';
 overlay.style.left = '0';
 overlay.style.top = '0';
 overlay.style.height = '100%';
 overlay.style.pointerEvents = 'none';
 overlay.style.display = 'flex';
 overlay.style.alignItems = 'center';
 overlay.style.paddingLeft = window.getComputedStyle(input).paddingLeft;
 overlay.style.color = 'transparent'; // hide underlying text
 overlay.style.font = window.getComputedStyle(input).font;
 overlay.style.whiteSpace = 'pre';
 wrapper.appendChild(overlay);

 // inject highlight style
 var style = document.createElement('style');
 style.id = 'inputOverlayStyles';
 style.textContent = '.input-overlay-char{color:inherit; background: transparent; padding:02px; color: black; opacity:0.9;} .input-overlay-highlight{background: #90ee90; border-radius:2px;}';
 document.head.appendChild(style);
 }

 function renderOverlay(){
 var overlay = document.getElementById('inputOverlay');
 var input = document.getElementById('inputField');
 if (!overlay || !input) return;
 var val = input.value || '';
 var position = parseInt(getInputValue('Position') || '0',10);
 if (isNaN(position)) position =0;
 overlay.innerHTML = '';
 for (var i=0;i<val.length;i++){
 var span = document.createElement('span');
 span.className = 'input-overlay-char' + (i===position ? ' input-overlay-highlight' : '');
 span.textContent = val[i];
 overlay.appendChild(span);
 }
 // if empty, keep overlay blank
 }

 function highlightCanvasState(){
 // Data attributes for external canvas script
 var canvas = document.getElementById('automatonCanvas');
 if (!canvas) return;
 var curId = getInputValue('CurrentStateId');
 if (!curId){
 // try CurrentStates first element
 var cs = document.querySelector('input[name^="CurrentStates["]');
 if (cs) curId = cs.value;
 }
 if (curId){
 canvas.dataset.currentState = curId;
 } else {
 delete canvas.dataset.currentState;
 }
 // transitions highlight could be added similarly if you compute next transition info
 // If canvas.js exposes global function, call it
 if (window.highlightAutomatonExecution && typeof window.highlightAutomatonExecution === 'function'){
 try{ window.highlightAutomatonExecution(canvas.dataset.currentState); }catch(e){}
 }
 }

 function highlightLeftPanel(){
 // clear previous
 document.querySelectorAll('.state-item.active').forEach(el => el.classList.remove('active'));
 document.querySelectorAll('.transition-item.active').forEach(el => el.classList.remove('active'));
 var curId = getInputValue('CurrentStateId');
 if (!curId){
 // try CurrentStates first element
 var cs = document.querySelector('input[name^="CurrentStates["]');
 if (cs) curId = cs.value;
 }
 if (!curId) return;
 // highlight state element
 var stateEl = document.querySelector('.state-item[data-state-id="' + curId + '"]');
 if (stateEl) stateEl.classList.add('active');
 // highlight transitions from this state
 document.querySelectorAll('.transition-item').forEach(t => {
 if (t.dataset.from === curId) t.classList.add('active');
 });
 }

 // Re-render overlay on input changes (but input is usually disabled during sim)
 document.addEventListener('input', function(e){ renderOverlay(); });

 // observe form changes (simple polling fallback)
 function updateAll(){
 var posVal = parseInt(getInputValue('Position') || '0',10);
 var hasExecuted = parseBool(getInputValue('HasExecuted'));
 var isSimulating = hasExecuted || (posVal && posVal >0);
 disableInput(isSimulating);
 setStartAsStop(isSimulating);
 renderOverlay();
 highlightCanvasState();
 highlightLeftPanel();
 }

 document.addEventListener('DOMContentLoaded', function(){
 var posVal = parseInt(getInputValue('Position') || '0',10);
 var hasExecuted = parseBool(getInputValue('HasExecuted'));
 var isSimulating = hasExecuted || (posVal && posVal >0);
 // disable input while simulating
 disableInput(isSimulating);
 setStartAsStop(isSimulating);
 highlightCanvasState();

 // ensure overlay updates after page resize (layout changes)
 window.addEventListener('resize', function(){ renderOverlay(); });

 // periodic update to reflect model changes after form posts
 setInterval(updateAll,300);
 });

})();