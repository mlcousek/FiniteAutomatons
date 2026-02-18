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
 */

import { AutomatonRenderer } from './AutomatonRenderer.js';
import { LayoutEngine } from './LayoutEngine.js';
import { CanvasInteractionHandler } from './CanvasInteractionHandler.js';

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
        this.isInitialized = false;

        // Bind methods
        this._handleResize = this._handleResize.bind(this);
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
                autoungrabify: this.options.readOnly,
                userZoomingEnabled: this.options.enablePanZoom,
                userPanningEnabled: this.options.enablePanZoom,
                pixelRatio: 'auto'
            });

            // Set up interaction handler
            if (this.options.enablePanZoom) {
                this.interactionHandler = new CanvasInteractionHandler(this.cy, this.options);
                this.interactionHandler.enable();
            }

            // Set up event listeners
            this._setupEventListeners();

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

            // Clear existing elements
            this.cy.elements().remove();

            // Render automaton based on type
            AutomatonRenderer.render(this.cy, data);

            // Apply layout
            const layoutName = data.layoutName || this.options.layoutName;
            LayoutEngine.applyLayout(this.cy, layoutName, {
                automatonType: this.automatonType,
                stateCount: data.states.length
            });

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

        // Remove previous highlights
        this.cy.nodes().removeClass('active');
        this.cy.edges().removeClass('active');

        if (!stateIds || stateIds.length === 0) {
            this.activeStateIds = [];
            return;
        }

        this.activeStateIds = stateIds;

        // Highlight states
        stateIds.forEach(stateId => {
            const node = this.cy.getElementById(`state-${stateId}`);
            if (node) {
                node.addClass('active');
                
                // Also highlight outgoing transitions
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
     * @param {string} [layoutName] - Layout algorithm to use
     */
    relayout(layoutName) {
        if (!this.cy || !this.currentData) return;

        const layout = layoutName || this.options.layoutName;
        LayoutEngine.applyLayout(this.cy, layout, {
            automatonType: this.automatonType,
            stateCount: this.currentData.states.length
        });

        this._fitToViewport();
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
     * Destroy canvas and clean up resources
     */
    destroy() {
        if (this.interactionHandler) {
            this.interactionHandler.disable();
            this.interactionHandler = null;
        }

        if (this.cy) {
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
