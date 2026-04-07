import { AutomatonCanvas } from './canvas/AutomatonCanvas.js';
import { CanvasFormSync } from './canvas/CanvasFormSync.js';
import { PanelSync } from './canvas/PanelSync.js';
import { CanvasLayoutCache } from './canvas/CanvasLayoutCache.js';
import { AlgorithmPanelEditor } from './canvas/AlgorithmPanelEditor.js';

let canvas = null;

let formSync = null;

let panelSync = null;

let algorithmPanelEditor = null;

export function initAutomatonCanvas() {
    try {
        canvas = new AutomatonCanvas('automatonCanvasContainer', {
            readOnly: true,
            enablePanZoom: true,
            layoutName: 'dagre',
            minZoom: 0.3,
            maxZoom: 3,
            wheelSensitivity: 0.2
        });

        canvas.init();

        window.getCanvasInstance = () => canvas;

        const automatonData = parseAutomatonFromForm();
        if (automatonData && automatonData.states && automatonData.states.length > 0) {
            canvas.loadAutomaton(automatonData);

            if (window.__savedLayoutJson) {
                try {
                    const positions = typeof window.__savedLayoutJson === 'string'
                        ? JSON.parse(window.__savedLayoutJson)
                        : window.__savedLayoutJson;

                    if (positions && typeof positions === 'object') {
                        CanvasLayoutCache.applyPositions(canvas.cy, positions);

                        if (canvas._layoutFingerprint) {
                            const entry = { savedAt: Date.now(), positions };
                            try {
                                localStorage.setItem(
                                    'fa-canvas-layout:' + canvas._layoutFingerprint,
                                    JSON.stringify(entry)
                                );
                            } catch (_) { /* quota exceeded — ignore */ }
                        }
                    }
                } catch (e) {
                    console.warn('Could not restore DB layout positions:', e);
                } finally {
                    delete window.__savedLayoutJson;
                }
            }

            highlightActiveStates();
        } else {
            showPlaceholder();
        }

        setupCanvasControls();

        setupFormSync();

    } catch (error) {
        console.error('Failed to initialize canvas:', error);
    }
}

function parseAutomatonFromForm() {
    try {
        const typeInput = document.querySelector('input[name="Type"]');
        if (!typeInput) {
            console.warn('Automaton type input not found');
            return null;
        }

        const automatonType = parseAutomatonType(typeInput.value);

        const states = parseStates();
        if (states.length === 0) {
            console.warn('No states found in form');
            return null;
        }

        const transitions = parseTransitions(automatonType);

        const activeStates = getActiveStateIds();

        const hasExecuted = document.querySelector('input[name="HasExecuted"]')?.value === 'true';
        const position = parseInt(document.querySelector('input[name="Position"]')?.value || '0', 10);

        return {
            type: automatonType,
            states: states,
            transitions: transitions,
            activeStates: activeStates,
            metadata: {
                hasExecuted,
                position
            }
        };
    } catch (error) {
        console.error('Failed to parse automaton from form:', error);
        return null;
    }
}

function parseAutomatonType(typeValue) {
    const typeMap = {
        '0': 'DFA',
        '1': 'NFA',
        '2': 'EpsilonNFA',
        '3': 'DPDA',
        '4': 'NPDA',
        'DFA': 'DFA',
        'NFA': 'NFA',
        'EpsilonNFA': 'EpsilonNFA',
        'DPDA': 'DPDA',
        'NPDA': 'NPDA'
    };

    return typeMap[typeValue] || 'DFA';
}

function parseStates() {
    const states = [];
    const stateIndexInputs = document.querySelectorAll('input[name="States.Index"]');

    stateIndexInputs.forEach(indexInput => {
        const index = indexInput.value;
        const idInput = document.querySelector(`input[name="States[${index}].Id"]`);
        const isStartInput = document.querySelector(`input[name="States[${index}].IsStart"]`);
        const isAcceptingInput = document.querySelector(`input[name="States[${index}].IsAccepting"]`);

        if (idInput) {
            states.push({
                id: parseInt(idInput.value, 10),
                isStart: isStartInput?.value === 'true',
                isAccepting: isAcceptingInput?.value === 'true'
            });
        }
    });

    return states;
}

function parseTransitions(automatonType) {
    const transitions = [];
    const transIndexInputs = document.querySelectorAll('input[name="Transitions.Index"]');

    transIndexInputs.forEach(indexInput => {
        const index = indexInput.value;
        const fromInput = document.querySelector(`input[name="Transitions[${index}].FromStateId"]`);
        const toInput = document.querySelector(`input[name="Transitions[${index}].ToStateId"]`);
        const symbolInput = document.querySelector(`input[name="Transitions[${index}].Symbol"]`);

        if (fromInput && toInput) {
            const transition = {
                fromStateId: parseInt(fromInput.value, 10),
                toStateId: parseInt(toInput.value, 10),
                symbol: parseSymbol(symbolInput?.value)
            };

            if (automatonType === 'DPDA' || automatonType === 'NPDA') {
                const stackPopInput = document.querySelector(`input[name="Transitions[${index}].StackPop"]`);
                const stackPushInput = document.querySelector(`input[name="Transitions[${index}].StackPush"]`);

                transition.stackPop = parseSymbol(stackPopInput?.value);
                transition.stackPush = stackPushInput?.value || '';
            }

            transitions.push(transition);
        }
    });

    return transitions;
}

function parseSymbol(symbolValue) {
    if (!symbolValue || symbolValue === '\\0') {
        return '\0'; // Epsilon
    }
    return symbolValue;
}

function getActiveStateIds() {
    const activeIds = [];

    const currentStateInput = document.querySelector('input[name="CurrentStateId"]');
    if (currentStateInput && currentStateInput.value) {
        const stateId = parseInt(currentStateInput.value, 10);
        if (!isNaN(stateId)) {
            activeIds.push(stateId);
        }
    }

    const currentStatesInputs = document.querySelectorAll('input[name^="CurrentStates["]');
    currentStatesInputs.forEach(input => {
        const stateId = parseInt(input.value, 10);
        if (!isNaN(stateId) && !activeIds.includes(stateId)) {
            activeIds.push(stateId);
        }
    });

    return activeIds;
}

function highlightActiveStates() {
    if (!canvas) return;

    const activeStateIds = getActiveStateIds();
    if (activeStateIds.length > 0) {
        canvas.highlight(activeStateIds);

        const editModeToggleBtn = document.getElementById('editModeToggleBtn');
        if (editModeToggleBtn) {
            editModeToggleBtn.disabled = true;
        }
        // Disable panel edit mode during simulation
        const panelEditBtn = document.getElementById('panelEditModeToggleBtn');
        if (panelEditBtn) panelEditBtn.disabled = true;
        if (algorithmPanelEditor?.isEnabled) algorithmPanelEditor.disable();
    } else {
        const editModeToggleBtn = document.getElementById('editModeToggleBtn');
        if (editModeToggleBtn) {
            editModeToggleBtn.disabled = false;
        }
        const panelEditBtn = document.getElementById('panelEditModeToggleBtn');
        if (panelEditBtn) panelEditBtn.disabled = false;
    }
}

function setupCanvasControls() {
    const zoomInBtn = document.getElementById('zoomInBtn');
    if (zoomInBtn) {
        zoomInBtn.addEventListener('click', () => {
            if (canvas) canvas.zoomIn();
        });
    }

    const zoomOutBtn = document.getElementById('zoomOutBtn');
    if (zoomOutBtn) {
        zoomOutBtn.addEventListener('click', () => {
            if (canvas) canvas.zoomOut();
        });
    }

    const fitBtn = document.getElementById('fitBtn');
    if (fitBtn) {
        fitBtn.addEventListener('click', () => {
            if (canvas) canvas.fit();
        });
    }

    const wheelLockBtn = document.getElementById('wheelLockBtn');
    const wheelLockIcon = document.getElementById('wheelLockIcon');
    if (wheelLockBtn) {
        const LOCAL_KEY = 'fa-wheel-zoom-enabled';

        let wheelEnabled = false;
        try {
            const ls = localStorage.getItem(LOCAL_KEY);
            if (ls !== null) wheelEnabled = ls === 'true';
        } catch (_) { }

        (async function loadServerPrefIfAuth(){
            try {
                if (window.__isAuthenticated) {
                    const resp = await fetch('/api/preferences/canvas-wheel', { credentials: 'same-origin' });
                    if (resp.ok) {
                        const json = await resp.json();
                        if (typeof json.enabled === 'boolean') {
                            wheelEnabled = json.enabled;
                            try { localStorage.setItem(LOCAL_KEY, String(wheelEnabled)); } catch (_) { }
                        }
                    }
                }
            } catch (e) {
                // ignore network errors
            } finally {
                applyWheelState(wheelEnabled);
            }
        })();

        wheelLockBtn.addEventListener('click', async () => {
            if (!canvas) return;
            wheelEnabled = !wheelEnabled;
            applyWheelState(wheelEnabled);

            try { localStorage.setItem(LOCAL_KEY, String(wheelEnabled)); } catch (_) { }

            try {
                if (window.__isAuthenticated) {
                    await fetch('/api/preferences/canvas-wheel', {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ enabled: wheelEnabled })
                    });
                }
            } catch (_) { /* ignore */ }
        });
    }

    function updateWheelButton(enabled) {
        if (!wheelLockBtn) return;
        if (enabled) {
            wheelLockBtn.title = 'Mouse wheel zoom: ON (click to disable)';
            wheelLockBtn.classList.remove('wheel-locked');
            if (wheelLockIcon) wheelLockIcon.className = 'fas fa-mouse';
        } else {
            wheelLockBtn.title = 'Mouse wheel zoom: OFF (click to enable)';
            wheelLockBtn.classList.add('wheel-locked');
            if (wheelLockIcon) wheelLockIcon.className = 'fas fa-mouse fa-slash';
        }
    }

    function applyWheelState(enabled) {
        if (!canvas) return;
        if (enabled) canvas.enableWheelZoom(); else canvas.disableWheelZoom();
        updateWheelButton(enabled);
    }

    const resetLayoutBtn = document.getElementById('resetLayoutBtn');
    if (resetLayoutBtn) {
        resetLayoutBtn.addEventListener('click', () => {
            if (canvas) canvas.relayout(); 
        });
    }

    const newAutomatonBtn = document.getElementById('newAutomatonBtn');
    if (newAutomatonBtn) {
        newAutomatonBtn.addEventListener('click', () => {
            if (canvas) canvas.clearLayoutCache();
        });
    }

    const generateModalBtnEl = document.getElementById('generateModalBtn');
    if (generateModalBtnEl) {
        generateModalBtnEl.addEventListener('click', () => {
        });
    }

    const importUpload = document.getElementById('importUploadInput');
    if (importUpload) {
        importUpload.addEventListener('change', () => {
            if (canvas) CanvasLayoutCache.clearAll(); 
        });
    }
    const editModeToggleBtn = document.getElementById('editModeToggleBtn');
    const editModeIcon = document.getElementById('editModeIcon');
    const editModeText = document.getElementById('editModeText');

    const moveToggleBtn = document.getElementById('moveToggleBtn');
    const moveModeIcon = document.getElementById('moveModeIcon');
    const moveModeText = document.getElementById('moveModeText');

    if (moveToggleBtn) {
        let moveActive = true;
        if (canvas && canvas.enableMoving) canvas.enableMoving();
        updateMoveButton(moveActive);

        moveToggleBtn.addEventListener('click', () => {
            if (!canvas) return;
            moveActive = !moveActive;
            if (moveActive) {
                canvas.enableMoving();
            } else {
                canvas.disableMoving();
            }
            updateMoveButton(moveActive);
        });

        function updateMoveButton(isMoveMode) {
            if (isMoveMode) {
                moveModeIcon.className = 'fas fa-arrows-alt';
                moveModeText.textContent = 'Move';
                moveToggleBtn.classList.add('move-mode-active');
                moveToggleBtn.title = 'Disable Moving';
            } else {
                moveModeIcon.className = 'fas fa-arrows-alt';
                moveModeText.textContent = 'Locked';
                moveToggleBtn.classList.remove('move-mode-active');
                moveToggleBtn.title = 'Enable Moving';
            }
        }
    }

    if (editModeToggleBtn) {
        editModeToggleBtn.disabled = false;

        editModeToggleBtn.addEventListener('click', () => {
            if (!canvas) return;

            const isActive = canvas.toggleEditMode();
            updateEditModeButton(isActive);
        });

        window.addEventListener('canvasEditModeChanged', (e) => {
            updateEditModeButton(e.detail.isEditMode);
        });

        function updateEditModeButton(isEditMode) {
            if (isEditMode) {
                editModeIcon.className = 'fas fa-unlock';
                editModeText.textContent = 'Unlocked';
                editModeToggleBtn.classList.add('edit-mode-active');
                editModeToggleBtn.title = 'Lock Canvas (Disable Editing)';
            } else {
                editModeIcon.className = 'fas fa-lock';
                editModeText.textContent = 'Locked';
                editModeToggleBtn.classList.remove('edit-mode-active');
                editModeToggleBtn.title = 'Unlock Canvas (Enable Editing)';
            }
        }

        updateEditModeButton(false);
    }

    const undoBtn = document.getElementById('undoBtn');
    const redoBtn = document.getElementById('redoBtn');

    if (undoBtn) {
        undoBtn.addEventListener('click', () => {
            if (canvas) canvas.undo();
        });
    }

    if (redoBtn) {
        redoBtn.addEventListener('click', () => {
            if (canvas) canvas.redo();
        });
    }

    document.addEventListener('keydown', (e) => {
        if (!canvas) return;
        const tag = document.activeElement?.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA') return;

        if (e.ctrlKey && !e.shiftKey && e.key === 'z') {
            e.preventDefault();
            canvas.undo();
        } else if (e.ctrlKey && (e.key === 'y' || (e.shiftKey && e.key === 'z'))) {
            e.preventDefault();
            canvas.redo();
        }
    });
}

function setupFormSync() {
    if (!canvas) return;

    const typeInput = document.querySelector('input[name="Type"]');
    const automatonType = parseAutomatonType(typeInput?.value || 'DFA');

    formSync = new CanvasFormSync({
        formId: 'automatonForm',
        automatonType
    });

    const syncCanvas = () => {
        if (canvas && formSync) {
            formSync.syncAll(canvas.getCytoscapeInstance());
        }
    };

    window.addEventListener('canvasStateAdded', syncCanvas);
    window.addEventListener('canvasStateDeleted', syncCanvas);
    window.addEventListener('canvasStateModified', syncCanvas);
    window.addEventListener('canvasTransitionAdded', syncCanvas);
    window.addEventListener('canvasTransitionDeleted', syncCanvas);
    window.addEventListener('canvasTransitionModified', syncCanvas);
    window.addEventListener('canvasHistoryApplied', syncCanvas);

    // — Algorithm Panel Editor setup —
    algorithmPanelEditor = new AlgorithmPanelEditor({
        statesContainerId:      'panel-states-list',
        transitionsContainerId: 'panel-transitions-list',
        automatonType,
        isSimulating: () => (canvas?.activeStateIds ?? []).length > 0
    });

    panelSync = new PanelSync({
        getCanvasInstance: () => canvas,
        isPanelEditMode:   () => algorithmPanelEditor?.isEnabled ?? false
    });
    panelSync.init();

    // Wire panel edit button (in the AUTOMATON header)
    _setupPanelEditButton(automatonType);

    // Handle panel → canvas events
    window.addEventListener('panelStateAdded', (e) => {
        if (!canvas) return;
        canvas.enableEditMode(); // ensure canvas is in edit mode so stateEditor is enabled
        canvas.addStateFromPanel(e.detail.state ?? {});
    });

    window.addEventListener('panelStateDeleted', (e) => {
        if (!canvas) return;
        canvas.enableEditMode();
        canvas.deleteStateFromPanel(e.detail.stateId);
    });

    window.addEventListener('panelStateModified', (e) => {
        if (!canvas) return;
        canvas.enableEditMode();
        canvas.modifyStateFromPanel(e.detail.stateId, e.detail.prop, e.detail.value);
    });

    window.addEventListener('panelTransitionAdded', (e) => {
        if (!canvas) return;
        canvas.enableEditMode();
        canvas.addTransitionFromPanel(e.detail.transition);
    });

    window.addEventListener('panelTransitionDeleted', (e) => {
        if (!canvas) return;
        canvas.enableEditMode();
        canvas.deleteTransitionFromPanel(e.detail);
    });

    console.log('CanvasFormSync + PanelSync + AlgorithmPanelEditor initialized');
}

function observeFormChanges() {
    console.log('Canvas form sync active (Phase 2 interactive mode)');
}

function _setupPanelEditButton(automatonType) {
    const btn = document.getElementById('panelEditModeToggleBtn');
    if (!btn) return;

    btn.addEventListener('click', () => {
        if (!algorithmPanelEditor) return;

        const nowEnabled = algorithmPanelEditor.toggle();
        _updatePanelEditButton(nowEnabled);

        if (panelSync) panelSync.setEditMode(nowEnabled);
    });

    _updatePanelEditButton(false);
}

function _updatePanelEditButton(isEnabled) {
    const btn  = document.getElementById('panelEditModeToggleBtn');
    const icon = document.getElementById('panelEditModeIcon');
    const text = document.getElementById('panelEditModeText');
    if (!btn) return;

    document.body.classList.toggle('panel-editing', isEnabled);

    if (isEnabled) {
        if (icon) icon.className = 'fas fa-pen-alt';
        if (text) text.textContent = 'Editing';
        btn.classList.add('panel-edit-mode-active');
        btn.title = 'Stop Editing Panel (Lock)';
    } else {
        if (icon) icon.className = 'fas fa-pen';
        if (text) text.textContent = 'Edit';
        btn.classList.remove('panel-edit-mode-active');
        btn.title = 'Edit Automaton in Panel';
    }
}

function reloadCanvas() {
    if (!canvas) return;

    try {
        const automatonData = parseAutomatonFromForm();
        if (automatonData && automatonData.states && automatonData.states.length > 0) {
            canvas.loadAutomaton(automatonData);
            highlightActiveStates();
        }
    } catch (error) {
        console.error('Failed to reload canvas:', error);
    }
}

function showPlaceholder() {
    const container = document.getElementById('automatonCanvasContainer');
    if (!container) return;

    const placeholder = document.createElement('div');
    placeholder.className = 'canvas-placeholder';
    placeholder.innerHTML = `
        <div class="canvas-placeholder-icon">
            <i class="fas fa-project-diagram"></i>
        </div>
        <div class="canvas-placeholder-text">
            No automaton loaded<br>
            <small>Generate or import an automaton to visualize</small>
        </div>
    `;

    container.appendChild(placeholder);
}

export function refreshCanvas() {
    reloadCanvas();
}

export function exportCanvasImage() {
    if (!canvas) {
        console.warn('Canvas not initialized');
        return null;
    }

    return canvas.exportAsImage();
}

export function getCanvasInstance() {
    return canvas;
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAutomatonCanvas);
} else {
    initAutomatonCanvas();
}

if (typeof window !== 'undefined') {
    window.refreshCanvas = refreshCanvas;
    window.exportCanvasImage = exportCanvasImage;
    window.getCanvasInstance = getCanvasInstance;
}
