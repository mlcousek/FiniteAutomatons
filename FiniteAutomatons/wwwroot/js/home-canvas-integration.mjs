/**
 * home-canvas-integration.mjs
 * Integration module for connecting the new canvas with the existing Home page
 * Handles data parsing from form, state updates, and control button interactions
 * 
 * @module HomeCanvasIntegration
 */

import { AutomatonCanvas } from './canvas/AutomatonCanvas.js';
import { CanvasFormSync } from './canvas/CanvasFormSync.js';
import { PanelSync } from './canvas/PanelSync.js';

/**
 * Global canvas instance
 */
let canvas = null;

/**
 * Global form sync instance
 */
let formSync = null;

/**
 * Global panel sync instance (real-time left-panel updates)
 */
let panelSync = null;

/**
 * Initialize the automaton canvas on page load
 */
export function initAutomatonCanvas() {
    try {
        // Create canvas instance
        canvas = new AutomatonCanvas('automatonCanvasContainer', {
            readOnly: true,
            enablePanZoom: true,
            layoutName: 'dagre',
            minZoom: 0.3,
            maxZoom: 3,
            wheelSensitivity: 0.2
        });

        // Initialize
        canvas.init();

        // Load automaton data from form
        const automatonData = parseAutomatonFromForm();
        if (automatonData && automatonData.states && automatonData.states.length > 0) {
            canvas.loadAutomaton(automatonData);

            // Highlight active states if execution started
            highlightActiveStates();
        } else {
            showPlaceholder();
        }

        // Set up control buttons
        setupCanvasControls();

        // Set up form synchronization (Phase 2)
        setupFormSync();

        // Canvas ready (minimal logging)
    } catch (error) {
        console.error('Failed to initialize canvas:', error);
    }
}

/**
 * Parse automaton data from hidden form inputs
 * @returns {Object} Automaton data object
 */
function parseAutomatonFromForm() {
    try {
        // Get automaton type
        const typeInput = document.querySelector('input[name="Type"]');
        if (!typeInput) {
            console.warn('Automaton type input not found');
            return null;
        }

        const automatonType = parseAutomatonType(typeInput.value);

        // Parse states
        const states = parseStates();
        if (states.length === 0) {
            console.warn('No states found in form');
            return null;
        }

        // Parse transitions
        const transitions = parseTransitions(automatonType);

        // Get active states
        const activeStates = getActiveStateIds();

        // Get metadata
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

/**
 * Parse automaton type from string/number
 * @param {string|number} typeValue - Type value from form
 * @returns {string} Automaton type string
 */
function parseAutomatonType(typeValue) {
    const typeMap = {
        '0': 'DFA',
        '1': 'NFA',
        '2': 'EpsilonNFA',
        '3': 'PDA',
        'DFA': 'DFA',
        'NFA': 'NFA',
        'EpsilonNFA': 'EpsilonNFA',
        'PDA': 'PDA'
    };

    return typeMap[typeValue] || 'DFA';
}

/**
 * Parse states from form inputs
 * @returns {Array} Array of state objects
 */
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

/**
 * Parse transitions from form inputs
 * @param {string} automatonType - Type of automaton
 * @returns {Array} Array of transition objects
 */
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

            // PDA-specific fields
            if (automatonType === 'PDA') {
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

/**
 * Parse symbol (handle epsilon notation)
 * @param {string} symbolValue - Symbol value from form
 * @returns {string} Parsed symbol
 */
function parseSymbol(symbolValue) {
    if (!symbolValue || symbolValue === '\\0') {
        return '\0'; // Epsilon
    }
    return symbolValue;
}

/**
 * Get currently active state IDs
 * @returns {Array<number>} Array of active state IDs
 */
function getActiveStateIds() {
    const activeIds = [];

    // Check for single current state (DFA/PDA)
    const currentStateInput = document.querySelector('input[name="CurrentStateId"]');
    if (currentStateInput && currentStateInput.value) {
        const stateId = parseInt(currentStateInput.value, 10);
        if (!isNaN(stateId)) {
            activeIds.push(stateId);
        }
    }

    // Check for multiple current states (NFA/ε-NFA)
    const currentStatesInputs = document.querySelectorAll('input[name^="CurrentStates["]');
    currentStatesInputs.forEach(input => {
        const stateId = parseInt(input.value, 10);
        if (!isNaN(stateId) && !activeIds.includes(stateId)) {
            activeIds.push(stateId);
        }
    });

    return activeIds;
}

/**
 * Highlight currently active states on canvas
 */
function highlightActiveStates() {
    if (!canvas) return;

    const activeStateIds = getActiveStateIds();
    if (activeStateIds.length > 0) {
        canvas.highlight(activeStateIds);

        // Disable edit mode button during simulation (moving remains available)
        const editModeToggleBtn = document.getElementById('editModeToggleBtn');
        if (editModeToggleBtn) {
            editModeToggleBtn.disabled = true;
        }
    } else {
        // Re-enable edit mode button when simulation ends
        const editModeToggleBtn = document.getElementById('editModeToggleBtn');
        if (editModeToggleBtn) {
            editModeToggleBtn.disabled = false;
        }
    }
}

/**
 * Set up canvas control buttons
 */
function setupCanvasControls() {
    // Zoom in button
    const zoomInBtn = document.getElementById('zoomInBtn');
    if (zoomInBtn) {
        zoomInBtn.addEventListener('click', () => {
            if (canvas) canvas.zoomIn();
        });
    }

    // Zoom out button
    const zoomOutBtn = document.getElementById('zoomOutBtn');
    if (zoomOutBtn) {
        zoomOutBtn.addEventListener('click', () => {
            if (canvas) canvas.zoomOut();
        });
    }

    // Fit to view button
    const fitBtn = document.getElementById('fitBtn');
    if (fitBtn) {
        fitBtn.addEventListener('click', () => {
            if (canvas) canvas.fit();
        });
    }

    // Reset layout button
    const resetLayoutBtn = document.getElementById('resetLayoutBtn');
    if (resetLayoutBtn) {
        resetLayoutBtn.addEventListener('click', () => {
            if (canvas) canvas.relayout();
        });
    }
    // Edit & Move mode toggles (Phase 2)
    const editModeToggleBtn = document.getElementById('editModeToggleBtn');
    const editModeIcon = document.getElementById('editModeIcon');
    const editModeText = document.getElementById('editModeText');

    const moveToggleBtn = document.getElementById('moveToggleBtn');
    const moveModeIcon = document.getElementById('moveModeIcon');
    const moveModeText = document.getElementById('moveModeText');

    // MOVE toggle: controls node dragging separately from edit mode
    if (moveToggleBtn) {
        // Default: moving enabled
        let moveActive = true;
        // Ensure canvas moving state matches
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

    // EDIT toggle: controls add/delete/edit actions
    if (editModeToggleBtn) {
        // Enable button (disabled by default in HTML)
        editModeToggleBtn.disabled = false;

        // Click handler
        editModeToggleBtn.addEventListener('click', () => {
            if (!canvas) return;

            const isActive = canvas.toggleEditMode();
            updateEditModeButton(isActive);
        });

        // Listen for edit mode changes from canvas
        window.addEventListener('canvasEditModeChanged', (e) => {
            updateEditModeButton(e.detail.isEditMode);
        });

        // Function to update button state
        function updateEditModeButton(isEditMode) {
            if (isEditMode) {
                // Edit mode ON (unlocked)
                editModeIcon.className = 'fas fa-unlock';
                editModeText.textContent = 'Unlocked';
                editModeToggleBtn.classList.add('edit-mode-active');
                editModeToggleBtn.title = 'Lock Canvas (Disable Editing)';
            } else {
                // Edit mode OFF (locked)
                editModeIcon.className = 'fas fa-lock';
                editModeText.textContent = 'Locked';
                editModeToggleBtn.classList.remove('edit-mode-active');
                editModeToggleBtn.title = 'Unlock Canvas (Enable Editing)';
            }
        }

        // Initialize button state
        updateEditModeButton(false);
    }

    // Undo/Redo buttons (Phase 2)
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

    // Keyboard shortcuts: Ctrl+Z (undo), Ctrl+Y / Ctrl+Shift+Z (redo)
    document.addEventListener('keydown', (e) => {
        if (!canvas) return;
        // Only fire if not typing in an input/textarea
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

/**
 * Set up form synchronization — listens to canvas events and rebuilds form inputs
 */
function setupFormSync() {
    if (!canvas) return;

    // Determine automaton type for PDA detection
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

    // Listen for all canvas edit events
    window.addEventListener('canvasStateAdded', syncCanvas);
    window.addEventListener('canvasStateDeleted', syncCanvas);
    window.addEventListener('canvasStateModified', syncCanvas);
    window.addEventListener('canvasTransitionAdded', syncCanvas);
    window.addEventListener('canvasTransitionDeleted', syncCanvas);
    window.addEventListener('canvasTransitionModified', syncCanvas);
    // When undo/redo is applied via ActionHistory, re-sync the form
    window.addEventListener('canvasHistoryApplied', syncCanvas);

    // Initialize PanelSync for real-time left-panel updates
    panelSync = new PanelSync({
        getCanvasInstance: () => canvas
    });
    panelSync.init();

    console.log('CanvasFormSync + PanelSync initialized');
}

/**
 * Observe form changes and update canvas (legacy — replaced by setupFormSync)
 */
function observeFormChanges() {
    console.log('Canvas form sync active (Phase 2 interactive mode)');
}

/**
 * Reload canvas with current form data
 */
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

/**
 * Show placeholder when no automaton is loaded
 */
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

/**
 * Public API: Force refresh canvas
 */
export function refreshCanvas() {
    reloadCanvas();
}

/**
 * Public API: Export canvas as image
 */
export function exportCanvasImage() {
    if (!canvas) {
        console.warn('Canvas not initialized');
        return null;
    }

    return canvas.exportAsImage();
}

/**
 * Public API: Get canvas instance
 */
export function getCanvasInstance() {
    return canvas;
}

/**
 * Initialize on DOM ready
 */
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAutomatonCanvas);
} else {
    initAutomatonCanvas();
}

// Expose to window for compatibility
if (typeof window !== 'undefined') {
    window.refreshCanvas = refreshCanvas;
    window.exportCanvasImage = exportCanvasImage;
    window.getCanvasInstance = getCanvasInstance;
}
