/**
 * home-canvas-integration.mjs
 * Integration module for connecting the new canvas with the existing Home page
 * Handles data parsing from form, state updates, and control button interactions
 * 
 * @module HomeCanvasIntegration
 */

import { AutomatonCanvas } from './canvas/AutomatonCanvas.js';

/**
 * Global canvas instance
 */
let canvas = null;

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

        // Observe form changes for real-time updates (disabled in Phase 1)
        observeFormChanges();

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
}

/**
 * Observe form changes and update canvas
 * NOTE: Disabled in Phase 1 (read-only mode) to prevent infinite reload loops.
 * Canvas updates happen on page load and after form submissions (page reloads).
 * Will be re-enabled in Phase 2 with proper change detection for interactive editing.
 */
function observeFormChanges() {
    // DISABLED: MutationObserver was causing infinite reload loops
    // Phase 1 is read-only, so we only need to load canvas once on page load
    // Phase 2 will implement proper change detection for interactive editing

    // const automatonForm = document.getElementById('automatonForm');
    // if (!automatonForm) return;

    // For Phase 1, we rely on page reloads after form submissions
    // No need for real-time updates since canvas is view-only

    console.log('Canvas change observer disabled (Phase 1 - read-only mode)');
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
