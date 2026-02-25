/**
 * AutomatonCanvas.js
 * Main canvas class for rendering and managing automaton visualization
 * Uses Cytoscape.js for graph rendering and interaction
 * 
 * @module AutomatonCanvas
 * @requires cytoscape
 * @requires AutomatonRenderer
 * @requires LayoutEngine
 * @requires CanvasInteractionHandler
 * @requires EditModeManager
 * @requires StateEditor
 */

import { AutomatonRenderer } from './AutomatonRenderer.js';
import { LayoutEngine } from './LayoutEngine.js';
import { CanvasInteractionHandler } from './CanvasInteractionHandler.js';
import { EditModeManager } from './EditModeManager.js';
import { StateEditor } from './StateEditor.js';
import { TransitionEditor } from './TransitionEditor.js';
import { ActionHistory } from './ActionHistory.js';
import { CanvasLayoutCache } from './CanvasLayoutCache.js';

/**
 * Main class for automaton canvas visualization
 * Handles initialization, rendering, and state management
 */
export class AutomatonCanvas {
    /**
     * @param {string} containerId - DOM element ID for canvas container
     * @param {Object} options - Configuration options
     * @param {boolean} [options.readOnly=true] - Whether canvas is read-only
     * @param {boolean} [options.enablePanZoom=true] - Enable pan/zoom interactions
     * @param {string} [options.layoutName='dagre'] - Default layout algorithm
     * @param {Object} [options.styleOverrides={}] - Custom style overrides
     */
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.container = document.getElementById(containerId);
        
        if (!this.container) {
            throw new Error(`Container element with ID '${containerId}' not found`);
        }

        // Configuration
        this.options = {
            readOnly: options.readOnly ?? true,
            enablePanZoom: options.enablePanZoom ?? true,
            // By default do not enable wheel-to-zoom; allow panning via drag.
            zoomOnWheel: options.zoomOnWheel ?? false,
            layoutName: options.layoutName ?? 'dagre',
            styleOverrides: options.styleOverrides ?? {},
            minZoom: options.minZoom ?? 0.3,
            maxZoom: options.maxZoom ?? 3,
            wheelSensitivity: options.wheelSensitivity ?? 0.2,
            ...options
        };

        // State
        this.cy = null; // Cytoscape instance
        this.automatonType = null;
        this.currentData = null;
        this.activeStateIds = [];
        this.interactionHandler = null;
        this.editModeManager = null; // Edit mode manager (Phase 2)
        this.stateEditor = null; // State editor (Phase 2)
        this.transitionEditor = null; // Transition editor (Phase 2)
        this.actionHistory = null; // Undo/redo history (Phase 2)
        this.moveEnabled = true; // Controls whether nodes can be moved (separate from edit mode)
        this.isInitialized = false;

        // Layout cache
        this._layoutFingerprint = null; // fingerprint for the currently-loaded automaton
        this._savePositionsDebounceTimer = null; // debounce timer for position saves

        // Bind methods
        this._handleResize = this._handleResize.bind(this);
        this._onNodeFreed = this._onNodeFreed.bind(this);
    }

    /**
     * Initialize the canvas and Cytoscape instance
     * Sets up styling, event listeners, and interaction handlers
     */
    init() {
        if (this.isInitialized) {
            console.warn('Canvas already initialized');
            return;
        }

        try {
            // Check if Cytoscape is available
            if (typeof cytoscape === 'undefined') {
                throw new Error('Cytoscape.js library not loaded. Include cytoscape.min.js before this script.');
            }

            // Initialize Cytoscape
            this.cy = cytoscape({
                container: this.container,
                layout: { name: 'preset' }, // We'll handle layout manually
                style: AutomatonRenderer.getStylesheet(this.automatonType, this.options.styleOverrides),
                wheelSensitivity: this.options.wheelSensitivity,
                minZoom: this.options.minZoom,
                maxZoom: this.options.maxZoom,
                boxSelectionEnabled: false,
                autounselectify: this.options.readOnly,
                // Do not set autoungrabify here; control grabbing per-node via
                // EditModeManager and AutomatonCanvas._applyDraggingState. Setting
                // autoungrabify globally here prevents later calls to node.grabify()
                // from taking effect.
                autoungrabify: false,
                // Only enable Cytoscape's built-in user zooming when both
                // pan/zoom is allowed and wheel zoom is explicitly enabled.
                userZoomingEnabled: !!(this.options.enablePanZoom && this.options.zoomOnWheel),
                userPanningEnabled: this.options.enablePanZoom,
                pixelRatio: 'auto'
            });

            // Set up interaction handler
            if (this.options.enablePanZoom) {
                this.interactionHandler = new CanvasInteractionHandler(this.cy, this.options);
                this.interactionHandler.enable();
            }

            // Set up edit mode manager (Phase 2)
            this.editModeManager = new EditModeManager(this.cy, {
                enableByDefault: false, // Start in view mode
                onModeChange: (isEditMode) => {
                    this._onEditModeChange(isEditMode);
                }
            });

            // Set up action history for undo/redo (Phase 2)
            this.actionHistory = new ActionHistory({
                maxSize: 100,
                onHistoryChanged: ({ canUndo, canRedo }) => {
                    const undoBtn = document.getElementById('undoBtn');
                    const redoBtn = document.getElementById('redoBtn');
                    if (undoBtn) undoBtn.disabled = !canUndo;
                    if (redoBtn) redoBtn.disabled = !canRedo;
                }
            });

            // Set up state editor (Phase 2)
            this.stateEditor = new StateEditor(this.cy, {
                onStateAdded: (state) => this._onStateAdded(state),
                onStateDeleted: (state) => this._onStateDeleted(state),
                onStateModified: (state) => this._onStateModified(state),
                actionHistory: this.actionHistory
            });

            // Set up transition editor (Phase 2)
            this.transitionEditor = new TransitionEditor(this.cy, this.container, {
                automatonType: this.automatonType || 'DFA',
                onTransitionAdded: (transition) => this._onTransitionAdded(transition),
                onTransitionDeleted: (transition) => this._onTransitionDeleted(transition),
                onTransitionModified: (transition) => this._onTransitionModified(transition),
                actionHistory: this.actionHistory
            });

            // Set up event listeners
            this._setupEventListeners();

            // Listen for node drag-end → persist positions to cache
            this.cy.on('free', 'node', this._onNodeFreed);

            // Add resize observer
            window.addEventListener('resize', this._handleResize);

            this.isInitialized = true;
            // Initialization successful (logging minimal to reduce console noise)
        } catch (error) {
            console.error('Failed to initialize AutomatonCanvas:', error);
            this._showErrorMessage('Failed to initialize canvas: ' + error.message);
            throw error;
        }
    }

    /**
     * Load automaton data and render it
     * @param {Object} data - Automaton data object
     * @param {string} data.type - Automaton type (DFA, NFA, EpsilonNFA, PDA)
     * @param {Array} data.states - Array of state objects
     * @param {Array} data.transitions - Array of transition objects
     * @param {Array} [data.activeStates] - Currently active state IDs
     * @param {Object} [data.metadata] - Additional metadata
     */
    loadAutomaton(data) {
        if (!this.isInitialized) {
            console.warn('Canvas not initialized. Calling init()...');
            this.init();
        }

        if (!data || !data.states || !data.transitions) {
            console.warn('Invalid automaton data provided');
            this._showErrorMessage('No automaton data to display');
            return;
        }

        try {
            this.currentData = data;
            this.automatonType = data.type;
            this.activeStateIds = data.activeStates || [];

            // Update layout fingerprint
            if (data.states && data.states.length > 0) {
                const stateIds = data.states.map(s => s.id);
                this._layoutFingerprint = CanvasLayoutCache.buildFingerprint(data.type, stateIds);
            } else {
                this._layoutFingerprint = null;
            }

            // Clear existing elements
            this.cy.elements().remove();

            // Render automaton based on type
            AutomatonRenderer.render(this.cy, data);

            // Apply layout — use saved positions from cache if available, otherwise run algorithm
            const cachedPositions = this._layoutFingerprint
                ? CanvasLayoutCache.load(this._layoutFingerprint)
                : null;

            if (cachedPositions && Object.keys(cachedPositions).length > 0) {
                // Restore cached node positions (use preset layout so Cytoscape respects them)
                LayoutEngine.applyLayout(this.cy, 'preset', {
                    automatonType: this.automatonType,
                    stateCount: data.states.length
                });
                CanvasLayoutCache.applyPositions(this.cy, cachedPositions);
            } else {
                // No cache hit — run the configured layout algorithm
                const layoutName = data.layoutName || this.options.layoutName;
                LayoutEngine.applyLayout(this.cy, layoutName, {
                    automatonType: this.automatonType,
                    stateCount: data.states.length
                });
            }

            // IMPORTANT: Reapply edit mode state to new nodes
            // Re-apply dragging state (controls move vs edit dragging policy)
            this._applyDraggingState();

            // Update transition editor's automaton type and clear undo history
            if (this.transitionEditor) {
                this.transitionEditor.setAutomatonType(data.type);
            }
            if (this.actionHistory) {
                this.actionHistory.clear();
            }

            // Highlight active states if any
            if (this.activeStateIds.length > 0) {
                this.highlight(this.activeStateIds);
            }

            // Fit to viewport
            this._fitToViewport();

            // Only log on first load or when automaton type changes
            if (!this._lastLoadedType || this._lastLoadedType !== this.automatonType) {
                console.log(`Loaded ${this.automatonType} with ${data.states.length} states`);
                this._lastLoadedType = this.automatonType;
            }
        } catch (error) {
            console.error('Failed to load automaton:', error);
            this._showErrorMessage('Failed to render automaton: ' + error.message);
        }
    }

    /**
     * Highlight specific states (e.g., current execution state)
     * @param {Array<number>} stateIds - Array of state IDs to highlight
     */
    highlight(stateIds) {
        if (!this.cy) return;

        // Remove previous highlights and any branch-specific classes/styles
        this.cy.nodes().removeClass('active');
        this.cy.edges().removeClass('active');
        // Clear branch classes and inline styles for a reasonable palette size
        const PALETTE_SIZE = 16;
        for (let i = 0; i < PALETTE_SIZE; i++) {
            this.cy.nodes().removeClass(`active-branch-${i}`);
            this.cy.edges().removeClass(`active-branch-${i}`);
        }
        // Also clear any inline styles previously set on nodes/edges (background/line colors)
        this.cy.nodes().forEach(n => {
            n.style('background-color', '');
            n.style('border-color', '');
            n.style('color', '');
        });
        this.cy.edges().forEach(e => {
            e.style('line-color', '');
            e.style('target-arrow-color', '');
            e.style('width', '');
        });

        if (!stateIds || stateIds.length === 0) {
            this.activeStateIds = [];
            // No active states = simulation not running
            this.setSimulationState(false);
            return;
        }

        this.activeStateIds = stateIds;

        // Simulation is running (disable edit mode)
        this.setSimulationState(true);

        // IMPORTANT: Ensure all nodes are not draggable during simulation
        // This is a safety check to prevent dragging during simulation
        this.cy.nodes().forEach(node => {
            node.ungrabify();
        });

        // Highlight states
        // If this is an NFA-like automaton with multiple active states, give each active
        // state a distinct branch color so nondeterministic paths are visually distinct.
        const isNondet = (this.automatonType === 'NFA' || this.automatonType === 'EpsilonNFA') && stateIds.length > 1;

        // Palette (mirrors AutomatonRenderer.js selectors for first N entries)
        const palette = [
            '#e63946', '#2a9d8f', '#f4a261', '#6a4c93', '#1d3557', '#ffb703',
            '#8ac926', '#1982c4', '#ff6b6b', '#4cc9f0', '#f72585', '#7209b7',
            '#3a0ca3', '#4361ee', '#2ec4b6', '#ffd166'
        ];

        stateIds.forEach((stateId, idx) => {
            const node = this.cy.getElementById(`state-${stateId}`);
            if (!node) return;

            if (isNondet) {
                const branchIndex = idx; // keep index so different sets map consistently within this highlight call
                const paletteIndex = branchIndex % palette.length;

                if (branchIndex < palette.length) {
                    // Use predefined stylesheet classes for the first palette.length branches
                    const cls = `active-branch-${paletteIndex}`;
                    node.addClass('active');
                    node.addClass(cls);

                    node.outgoers('edge').forEach(e => {
                        e.addClass('active');
                        e.addClass(cls);
                    });
                } else {
                    // For branches beyond the stylesheet-defined palette, compute color dynamically
                    const hue = (branchIndex * 47) % 360; // spread hues
                    const color = `hsl(${hue} 70% 50%)`;
                    const borderColor = `hsl(${hue} 60% 40%)`;

                    node.addClass('active');
                    node.style('background-color', color);
                    node.style('border-color', borderColor);
                    node.style('color', '#ffffff');

                    node.outgoers('edge').forEach(e => {
                        e.addClass('active');
                        e.style('line-color', color);
                        e.style('target-arrow-color', color);
                        e.style('width', 4);
                    });
                }
            } else {
                node.addClass('active');
                node.outgoers('edge').addClass('active');
            }
        });
    }

    /**
     * Update stylesheet (useful for theme changes)
     * @param {Object} styleOverrides - Style overrides to apply
     */
    updateStyle(styleOverrides = {}) {
        if (!this.cy) return;

        const newStyle = AutomatonRenderer.getStylesheet(
            this.automatonType, 
            { ...this.options.styleOverrides, ...styleOverrides }
        );
        
        this.cy.style(newStyle);
    }

    /**
     * Zoom controls
     */
    zoomIn() {
        if (!this.cy) return;
        const currentZoom = this.cy.zoom();
        this.cy.zoom({
            level: Math.min(currentZoom * 1.2, this.options.maxZoom),
            renderedPosition: { 
                x: this.container.clientWidth / 2, 
                y: this.container.clientHeight / 2 
            }
        });
    }

    zoomOut() {
        if (!this.cy) return;
        const currentZoom = this.cy.zoom();
        this.cy.zoom({
            level: Math.max(currentZoom / 1.2, this.options.minZoom),
            renderedPosition: { 
                x: this.container.clientWidth / 2, 
                y: this.container.clientHeight / 2 
            }
        });
    }

    /**
     * Fit canvas to viewport
     */
    fit() {
        this._fitToViewport();
    }

    /**
     * Reset view to original state
     */
    resetView() {
        if (!this.cy) return;
        this._fitToViewport();
    }

    /**
     * Re-apply layout
     * Clears the position cache for the current automaton so the algorithmic
     * layout is used on the next load (i.e. the user explicitly asked to reset).
     * @param {string} [layoutName] - Layout algorithm to use
     */
    relayout(layoutName) {
        if (!this.cy || !this.currentData) return;

        // Drop cached positions so subsequent reloads also use the fresh layout
        this.clearLayoutCache();

        const layout = layoutName || this.options.layoutName;
        LayoutEngine.applyLayout(this.cy, layout, {
            automatonType: this.automatonType,
            stateCount: this.currentData.states.length
        });

        this._fitToViewport();
    }

    /**
     * Set the layout fingerprint used for the cache key.
     * Typically called by the integration layer when a specific automaton is known.
     * @param {string|null} fingerprint
     */
    setLayoutFingerprint(fingerprint) {
        this._layoutFingerprint = fingerprint;
    }

    /**
     * Clear the position cache for the currently loaded automaton.
     */
    clearLayoutCache() {
        if (this._layoutFingerprint) {
            CanvasLayoutCache.clear(this._layoutFingerprint);
        }
    }

    /**
     * Expose the underlying Cytoscape instance for external use
     * (e.g. for non-module scripts that need to read node positions).
     * @returns {Object|null} Cytoscape instance or null
     */
    getCytoscapeInstance() {
        return this.cy || null;
    }

    /**
     * Export canvas as image (PNG)
     * @returns {string} Data URL of the canvas image
     */
    exportAsImage() {
        if (!this.cy) return null;
        
        return this.cy.png({
            output: 'blob',
            bg: 'white',
            full: true,
            scale: 2
        });
    }

    /**
     * Get current automaton data
     * @returns {Object} Current automaton data
     */
    getData() {
        return this.currentData;
    }

    /**
     * Get Cytoscape instance (for advanced usage)
     * @returns {Object} Cytoscape instance
     */
    getCytoscapeInstance() {
        return this.cy;
    }

    /**
     * ====================
     * EDIT MODE METHODS (Phase 2)
     * ====================
     */

    /**
     * Enable edit mode - allows interactive editing of the automaton
     * @returns {boolean} Success status
     */
    enableEditMode() {
        if (!this.editModeManager) {
            console.warn('EditModeManager not initialized');
            return false;
        }
        return this.editModeManager.enableEditMode();
    }

    /**
     * Disable edit mode - makes canvas read-only
     * @returns {boolean} Success status
     */
    disableEditMode() {
        if (!this.editModeManager) {
            console.warn('EditModeManager not initialized');
            return false;
        }
        return this.editModeManager.disableEditMode();
    }

    /**
     * Toggle edit mode
     * @returns {boolean} New edit mode state
     */
    toggleEditMode() {
        if (!this.editModeManager) {
            console.warn('EditModeManager not initialized');
            return false;
        }
        return this.editModeManager.toggleEditMode();
    }

    /**
     * Check if edit mode is active
     * @returns {boolean} True if edit mode is active
     */
    isEditModeActive() {
        return this.editModeManager ? this.editModeManager.isActive() : false;
    }

    /**
     * Set simulation state (disables edit mode during simulation)
     * @param {boolean} isSimulating - Whether simulation is running
     */
    setSimulationState(isSimulating) {
        if (this.editModeManager) {
            this.editModeManager.setSimulationState(isSimulating);
        }
        // Re-apply dragging state to respect simulation lock
        this._applyDraggingState();
    }

    /**
     * Private: Handle edit mode changes
     * @private
     * @param {boolean} isEditMode - New edit mode state
     */
    _onEditModeChange(isEditMode) {
        // Enable/disable state editor
        if (this.stateEditor) {
            if (isEditMode) {
                this.stateEditor.enable();
            } else {
                this.stateEditor.disable();
            }
        }

        // Enable/disable transition editor
        if (this.transitionEditor) {
            if (isEditMode) {
                this.transitionEditor.enable();
            } else {
                this.transitionEditor.disable();
            }
        }

        // Update button state (will be implemented in UI integration)
        const event = new CustomEvent('canvasEditModeChanged', {
            detail: { isEditMode }
        });
        window.dispatchEvent(event);

        console.log(`Canvas edit mode: ${isEditMode ? 'ENABLED' : 'DISABLED'}`);
        // Re-apply dragging state when edit mode changes
        this._applyDraggingState();
    }

    /**
     * Private: Handle state added event
     * @private
     * @param {Object} state - Added state data
     */
    _onStateAdded(state) {
        console.log('State added:', state);

        // Dispatch event for form synchronization (Phase 2)
        const event = new CustomEvent('canvasStateAdded', {
            detail: { state }
        });
        window.dispatchEvent(event);
    }

    /**
     * Enable node moving (dragging) separately from edit mode
     */
    enableMoving() {
        this.moveEnabled = true;
        this._applyDraggingState();
    }

    /**
     * Disable node moving (dragging) separately from edit mode
     */
    disableMoving() {
        this.moveEnabled = false;
        this._applyDraggingState();
    }

    /**
     * Toggle moving state
     * @returns {boolean} New moving state
     */
    toggleMoving() {
        this.moveEnabled = !this.moveEnabled;
        this._applyDraggingState();
        return this.moveEnabled;
    }

    /**
     * Check whether moving is enabled
     * @returns {boolean}
     */
    isMovingActive() {
        return !!this.moveEnabled;
    }

    /**
     * Enable wheel-to-zoom behavior (mouse wheel zooming)
     */
    enableWheelZoom() {
        if (this.interactionHandler) {
            // Enable both the custom wheel listener and Cytoscape's user zooming
            this.interactionHandler.updateOptions({ zoomOnWheel: true, enableZoom: true });
            try {
                if (this.cy && typeof this.cy.userZoomingEnabled === 'function') {
                    this.cy.userZoomingEnabled(true);
                }
            } catch (_) { }
            return true;
        }
        return false;
    }

    /**
     * Disable wheel-to-zoom behavior (mouse wheel zooming)
     */
    disableWheelZoom() {
        if (this.interactionHandler) {
            // Disable both the custom wheel listener and Cytoscape's user zooming
            this.interactionHandler.updateOptions({ zoomOnWheel: false, enableZoom: false });
            try {
                if (this.cy && typeof this.cy.userZoomingEnabled === 'function') {
                    this.cy.userZoomingEnabled(false);
                }
            } catch (_) { }
            return true;
        }
        return false;
    }

    /**
     * Check whether wheel-to-zoom is enabled
     * @returns {boolean}
     */
    isWheelZoomEnabled() {
        if (!this.interactionHandler) return false;
        const handlerFlag = !!(this.interactionHandler.options && this.interactionHandler.options.zoomOnWheel);
        let cyFlag = true;
        try {
            if (this.cy && typeof this.cy.userZoomingEnabled === 'function') {
                cyFlag = !!this.cy.userZoomingEnabled();
            }
        } catch (_) { /* ignore */ }
        return handlerFlag && cyFlag;
    }

    /**
     * Private: Apply dragging state to nodes based on move/edit/simulation flags
     * @private
     */
    _applyDraggingState() {
        if (!this.cy) return;

        // Moving is controlled only by the move toggle. Allow moving during simulation if moveEnabled is true.
        const shouldAllowDrag = !!this.moveEnabled;

        if (shouldAllowDrag) {
            this.cy.nodes().forEach(n => n.grabify());
        } else {
            this.cy.nodes().forEach(n => n.ungrabify());
        }
    }

    /**
     * Undo last action
     * @returns {boolean} True if an action was undone
     */
    undo() {
        return this.actionHistory ? this.actionHistory.undo() : false;
    }

    /**
     * Redo last undone action
     * @returns {boolean} True if an action was redone 
     */
    redo() {
        return this.actionHistory ? this.actionHistory.redo() : false;
    }

    /**
     * Get history status (for button enable/disable)
     * @returns {{ canUndo: boolean, canRedo: boolean }}
     */
    getHistoryStatus() {
        return this.actionHistory ? this.actionHistory.getStatus() : { canUndo: false, canRedo: false };
    }

    /**
     * Private: Handle transition added event
     * @private
     */
    _onTransitionAdded(transition) {
        window.dispatchEvent(new CustomEvent('canvasTransitionAdded', { detail: { transition } }));
    }

    /**
     * Private: Handle transition deleted event
     * @private
     */
    _onTransitionDeleted(transition) {
        window.dispatchEvent(new CustomEvent('canvasTransitionDeleted', { detail: { transition } }));
    }

    /**
     * Private: Handle transition modified event
     * @private
     */
    _onTransitionModified(transition) {
        window.dispatchEvent(new CustomEvent('canvasTransitionModified', { detail: { transition } }));
    }

    /**
     * Private: Handle state deleted event
     * @private
     * @param {Object} state - Deleted state data
     */
    _onStateDeleted(state) {
        console.log('State deleted:', state);

        // Dispatch event for form synchronization (Phase 2)
        const event = new CustomEvent('canvasStateDeleted', {
            detail: { state }
        });
        window.dispatchEvent(event);
    }

    /**
     * Private: Handle state modified event
     * @private
     * @param {Object} state - Modified state data
     */
    _onStateModified(state) {
        console.log('State modified:', state);

        // Dispatch event for form synchronization (Phase 2)
        const event = new CustomEvent('canvasStateModified', {
            detail: { state }
        });
        window.dispatchEvent(event);
    }

    /**
     * Destroy canvas and clean up resources
     */
    destroy() {
        if (this.interactionHandler) {
            this.interactionHandler.disable();
            this.interactionHandler = null;
        }

        if (this.editModeManager) {
            this.editModeManager.destroy();
            this.editModeManager = null;
        }

        if (this.stateEditor) {
            this.stateEditor.destroy();
            this.stateEditor = null;
        }

        if (this.transitionEditor) {
            this.transitionEditor.destroy();
            this.transitionEditor = null;
        }

        if (this.actionHistory) {
            this.actionHistory.clear();
            this.actionHistory = null;
        }

        if (this._savePositionsDebounceTimer) {
            clearTimeout(this._savePositionsDebounceTimer);
            this._savePositionsDebounceTimer = null;
        }

        if (this.cy) {
            this.cy.off('free', 'node', this._onNodeFreed);
            this.cy.destroy();
            this.cy = null;
        }

        window.removeEventListener('resize', this._handleResize);

        this.isInitialized = false;
        console.log('AutomatonCanvas destroyed');
    }

    /**
     * Private: Set up event listeners
     * @private
     */
    _setupEventListeners() {
        // Node hover tooltips
        this.cy.on('mouseover', 'node', (evt) => {
            const node = evt.target;
            const stateId = node.data('stateId');
            const isStart = node.data('isStart');
            const isAccepting = node.data('isAccepting');

            let tooltip = `State q${stateId}`;
            if (isStart) tooltip += ' (Start)';
            if (isAccepting) tooltip += ' (Accepting)';

            // Show tooltip (you can enhance this with a proper tooltip library)
            node.data('tooltip', tooltip);
        });

        // Edge hover tooltips
        this.cy.on('mouseover', 'edge', (evt) => {
            const edge = evt.target;
            const symbol = edge.data('symbol');
            const isPDA = edge.data('isPDA');

            if (isPDA) {
                const stackPop = edge.data('stackPop') || 'ε';
                const stackPush = edge.data('stackPush') || 'ε';
                edge.data('tooltip', `${symbol}, ${stackPop}/${stackPush}`);
            } else {
                edge.data('tooltip', symbol);
            }
        });
    }

    /**
     * Private: Handler called every time a node is released after dragging.
     * Debounces position saves to localStorage to avoid thundering-write issues.
     * @private
     */
    _onNodeFreed() {
        if (!this._layoutFingerprint || !this.cy) return;

        // Debounce: only write after user stops dragging for 600 ms
        if (this._savePositionsDebounceTimer) {
            clearTimeout(this._savePositionsDebounceTimer);
        }
        this._savePositionsDebounceTimer = setTimeout(() => {
            if (this.cy && this._layoutFingerprint) {
                CanvasLayoutCache.save(this._layoutFingerprint, this.cy);
            }
        }, 600);
    }

    /**
     * Private: Fit canvas to viewport with padding
     * @private
     */
    _fitToViewport() {
        if (!this.cy) return;

        const padding = 50;
        
        // Only fit if there are elements
        if (this.cy.nodes().length > 0) {
            this.cy.fit(this.cy.elements(), padding);
        } else {
            this.cy.reset();
        }
    }

    /**
     * Private: Handle window resize
     * @private
     */
    _handleResize() {
        if (!this.cy) return;
        
        // Debounce resize
        clearTimeout(this._resizeTimeout);
        this._resizeTimeout = setTimeout(() => {
            this.cy.resize();
            this._fitToViewport();
        }, 250);
    }

    /**
     * Private: Show error message
     * @private
     * @param {string} message - Error message to display
     */
    _showErrorMessage(message) {
        if (!this.container) return;

        const errorDiv = document.createElement('div');
        errorDiv.className = 'canvas-error-message';
        errorDiv.textContent = message;
        errorDiv.style.cssText = `
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            padding: 1rem 2rem;
            background: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
            border-radius: 4px;
            z-index: 1000;
        `;

        this.container.appendChild(errorDiv);

        // Auto-remove after 5 seconds
        setTimeout(() => {
            if (errorDiv.parentNode) {
                errorDiv.remove();
            }
        }, 5000);
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.AutomatonCanvas = AutomatonCanvas;
}
