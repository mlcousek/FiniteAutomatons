/**
 * EditModeManager.js
 * Manages edit mode state for the automaton canvas
 * Handles enabling/disabling interactive editing features
 * 
 * @module EditModeManager
 */

/**
 * Manages edit mode state and controls for canvas editing
 */
export class EditModeManager {
    /**
     * @param {Object} cy - Cytoscape instance
     * @param {Object} options - Configuration options
     * @param {boolean} options.enableByDefault - Whether edit mode is enabled by default
     * @param {Function} options.onModeChange - Callback when mode changes
     */
    constructor(cy, options = {}) {
        this.cy = cy;
        this.isEditMode = options.enableByDefault || false;
        this.isSimulating = false;
        this.onModeChange = options.onModeChange || (() => {});
        
        // Store original grabbable state
        this.originalGrabbableState = new Map();
        
        // Initialize mode
        this._applyMode();
    }

    /**
     * Enable edit mode (allow editing)
     */
    enableEditMode() {
        if (this.isSimulating) {
            console.warn('Cannot enable edit mode during simulation');
            return false;
        }

        if (this.isEditMode) {
            return true; // Already in edit mode
        }

        this.isEditMode = true;
        this._applyMode();
        this.onModeChange(this.isEditMode);
        console.log('Edit mode enabled');
        return true;
    }

    /**
     * Disable edit mode (view-only)
     */
    disableEditMode() {
        if (!this.isEditMode) {
            return true; // Already disabled
        }

        this.isEditMode = false;
        this._applyMode();
        this.onModeChange(this.isEditMode);
        console.log('Edit mode disabled');
        return true;
    }

    /**
     * Toggle edit mode
     * @returns {boolean} New edit mode state
     */
    toggleEditMode() {
        if (this.isEditMode) {
            this.disableEditMode();
        } else {
            this.enableEditMode();
        }
        return this.isEditMode;
    }

    /**
     * Set simulation state (disables edit mode during simulation)
     * @param {boolean} isSimulating - Whether simulation is running
     */
    setSimulationState(isSimulating) {
        this.isSimulating = isSimulating;
        
        if (isSimulating && this.isEditMode) {
            // Force disable edit mode during simulation
            this.isEditMode = false;
            this._applyMode();
            this.onModeChange(this.isEditMode);
            console.log('Edit mode disabled (simulation started)');
        }
    }

    /**
     * Check if edit mode is currently active
     * @returns {boolean} True if edit mode is active
     */
    isActive() {
        return this.isEditMode && !this.isSimulating;
    }

    /**
     * Apply current mode settings to the canvas
     * @private
     */
    _applyMode() {
        if (this.isEditMode && !this.isSimulating) {
            this._enableDragging();
            this._enableSelection();
            this._showEditCursor();
        } else {
            this._disableDragging();
            // When not in edit mode we disable selection to keep canvas read-only.
            // However keep selection enabled when simulation is running.
            if (this.cy) {
                this.cy.autounselectify(true);
            }
            // Keep selection for tooltips when appropriate
            this._hideEditCursor();
        }
    }

    /**
     * Enable node dragging
     * @private
     */
    _enableDragging() {
        this.cy.nodes().forEach(node => {
            // Store original state
            if (!this.originalGrabbableState.has(node.id())) {
                this.originalGrabbableState.set(node.id(), node.grabbable());
            }
            node.grabify();
        });
    }

    /**
     * Disable node dragging
     * @private
     */
    _disableDragging() {
        this.cy.nodes().forEach(node => {
            node.ungrabify();
        });
    }

    /**
     * Enable element selection
     * @private
     */
    _enableSelection() {
        this.cy.selectionType('single');
        // Allow selection when edit mode is active
        if (this.cy) {
            this.cy.autounselectify(false);
        }
    }

    /**
     * Show edit mode cursor
     * @private
     */
    _showEditCursor() {
        const container = this.cy.container();
        if (container) {
            container.style.cursor = 'default';
        }
    }

    /**
     * Hide edit mode cursor (reset to normal)
     * @private
     */
    _hideEditCursor() {
        const container = this.cy.container();
        if (container) {
            container.style.cursor = 'default';
        }
    }

    /**
     * Get current mode status
     * @returns {Object} Status object with mode flags
     */
    getStatus() {
        return {
            isEditMode: this.isEditMode,
            isSimulating: this.isSimulating,
            isActive: this.isActive()
        };
    }

    /**
     * Destroy and cleanup
     */
    destroy() {
        this.disableEditMode();
        this.originalGrabbableState.clear();
        this.cy = null;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.EditModeManager = EditModeManager;
}
