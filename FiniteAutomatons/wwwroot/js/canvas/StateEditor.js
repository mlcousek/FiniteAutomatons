/**
 * StateEditor.js
 * Handles state creation, deletion, and property editing for automaton canvas
 * Provides interactive state management functionality (Phase 2)
 * 
 * @module StateEditor
 */

/**
 * Manages state editing operations on the canvas
 */
export class StateEditor {
    /**
     * @param {Object} cy - Cytoscape instance
     * @param {Object} options - Configuration options
     * @param {Function} options.onStateAdded - Callback when state is added
     * @param {Function} options.onStateDeleted - Callback when state is deleted
     * @param {Function} options.onStateModified - Callback when state is modified
     */
    constructor(cy, options = {}) {
        this.cy = cy;
        this.options = options;
        
        // Callbacks
        this.onStateAdded = options.onStateAdded || (() => {});
        this.onStateDeleted = options.onStateDeleted || (() => {});
        this.onStateModified = options.onStateModified || (() => {});
        
        // Event handlers
        this.clickHandler = null;
        this.keyHandler = null;
        this.contextMenuHandler = null;
        
        // State
        this.isEnabled = false;
    }

    /**
     * Enable state editing
     */
    enable() {
        if (this.isEnabled) return;
        
        this.isEnabled = true;
        this._setupEventHandlers();
        console.log('StateEditor enabled');
    }

    /**
     * Disable state editing
     */
    disable() {
        if (!this.isEnabled) return;
        
        this.isEnabled = false;
        this._removeEventHandlers();
        console.log('StateEditor disabled');
    }

    /**
     * Setup event handlers for state editing
     * @private
     */
    _setupEventHandlers() {
        // Click empty space to add state
        this.clickHandler = (event) => {
            if (event.target === this.cy) {
                this._handleCanvasClick(event);
            }
        };
        this.cy.on('tap', this.clickHandler);

        // Delete key to remove selected state
        this.keyHandler = (e) => {
            if (e.key === 'Delete' || e.key === 'Backspace') {
                this._handleDeleteKey();
            }
        };
        document.addEventListener('keydown', this.keyHandler);

        // Right-click for context menu
        this.contextMenuHandler = (event) => {
            if (event.target.isNode()) {
                this._handleContextMenu(event);
            }
        };
        this.cy.on('cxttap', this.contextMenuHandler);
    }

    /**
     * Remove event handlers
     * @private
     */
    _removeEventHandlers() {
        if (this.clickHandler) {
            this.cy.off('tap', this.clickHandler);
            this.clickHandler = null;
        }

        if (this.keyHandler) {
            document.removeEventListener('keydown', this.keyHandler);
            this.keyHandler = null;
        }

        if (this.contextMenuHandler) {
            this.cy.off('cxttap', this.contextMenuHandler);
            this.contextMenuHandler = null;
        }
    }

    /**
     * Handle click on empty canvas - add new state
     * @private
     * @param {Object} event - Cytoscape tap event
     */
    _handleCanvasClick(event) {
        const position = event.position;
        this.addState(position.x, position.y);
    }

    /**
     * Handle Delete/Backspace key - remove selected state
     * @private
     */
    _handleDeleteKey() {
        const selected = this.cy.$(':selected');
        if (selected.isNode() && selected.length > 0) {
            selected.forEach(node => {
                this.deleteState(node.id());
            });
        }
    }

    /**
     * Handle right-click on node - show context menu
     * @private
     * @param {Object} event - Cytoscape context tap event
     */
    _handleContextMenu(event) {
        event.preventDefault();
        const node = event.target;
        
        // Show native context menu (simple implementation)
        // In a production app, you'd use a custom context menu library
        const isStart = node.hasClass('start');
        const isAccepting = node.hasClass('accepting');
        
        const action = window.confirm(
            `State: ${node.data('label')}\n\n` +
            `Current properties:\n` +
            `- Start: ${isStart ? 'Yes' : 'No'}\n` +
            `- Accepting: ${isAccepting ? 'Yes' : 'No'}\n\n` +
            `Click OK to toggle properties, Cancel to close`
        );
        
        if (action) {
            this.showPropertyDialog(node);
        }
    }

    /**
     * Add a new state at the specified position
     * @param {number} x - X coordinate
     * @param {number} y - Y coordinate
     * @returns {Object} The created node
     */
    addState(x, y) {
        const newStateId = this._getNextStateId();
        const nodeId = `state-${newStateId}`;
        
        const node = this.cy.add({
            group: 'nodes',
            data: {
                id: nodeId,
                stateId: newStateId,
                label: `q${newStateId}`,
                isStart: false,
                isAccepting: false
            },
            position: { x, y },
            classes: ''
        });

        // Callback
        this.onStateAdded({
            id: newStateId,
            label: `q${newStateId}`,
            isStart: false,
            isAccepting: false,
            position: { x, y }
        });

        console.log(`State added: q${newStateId}`);
        return node;
    }

    /**
     * Delete a state by node ID
     * @param {string} nodeId - Node ID (e.g., "state-1")
     */
    deleteState(nodeId) {
        const node = this.cy.getElementById(nodeId);
        if (!node || !node.isNode()) {
            console.warn('Node not found:', nodeId);
            return;
        }

        const stateId = node.data('stateId');
        const label = node.data('label');

        // Remove node (this also removes connected edges)
        this.cy.remove(node);

        // Callback
        this.onStateDeleted({
            id: stateId,
            label: label
        });

        console.log(`State deleted: ${label}`);
    }

    /**
     * Toggle a state as start state
     * @param {Object} node - Cytoscape node
     */
    toggleStartState(node) {
        const isCurrentlyStart = node.hasClass('start');

        if (!isCurrentlyStart) {
            // Remove start class from all other nodes (only one start state)
            this.cy.nodes().removeClass('start');
            node.data('isStart', false);
            
            // Add start class to this node
            node.addClass('start');
            node.data('isStart', true);
        } else {
            // Remove start state
            node.removeClass('start');
            node.data('isStart', false);
        }

        this._notifyStateModified(node);
    }

    /**
     * Toggle a state as accepting state
     * @param {Object} node - Cytoscape node
     */
    toggleAcceptingState(node) {
        const isCurrentlyAccepting = node.hasClass('accepting');

        if (!isCurrentlyAccepting) {
            node.addClass('accepting');
            node.data('isAccepting', true);
        } else {
            node.removeClass('accepting');
            node.data('isAccepting', false);
        }

        this._notifyStateModified(node);
    }

    /**
     * Show property dialog for a node
     * @param {Object} node - Cytoscape node
     */
    showPropertyDialog(node) {
        const label = node.data('label');
        const isStart = node.hasClass('start');
        const isAccepting = node.hasClass('accepting');

        // Simple dialog (in production, use a proper UI component)
        const options = [
            `1. ${isStart ? 'Remove' : 'Set as'} Start State`,
            `2. ${isAccepting ? 'Remove' : 'Set as'} Accepting State`,
            `0. Cancel`
        ].join('\n');

        const choice = window.prompt(
            `Edit properties for ${label}:\n\n${options}\n\nEnter choice:`,
            '0'
        );

        switch (choice) {
            case '1':
                this.toggleStartState(node);
                break;
            case '2':
                this.toggleAcceptingState(node);
                break;
            default:
                // Cancel
                break;
        }
    }

    /**
     * Get next available state ID
     * @private
     * @returns {number} Next state ID
     */
    _getNextStateId() {
        const existingIds = this.cy.nodes().map(n => n.data('stateId')).filter(id => id !== undefined);
        if (existingIds.length === 0) {
            return 1;
        }
        return Math.max(...existingIds) + 1;
    }

    /**
     * Notify that a state was modified
     * @private
     * @param {Object} node - Modified node
     */
    _notifyStateModified(node) {
        this.onStateModified({
            id: node.data('stateId'),
            label: node.data('label'),
            isStart: node.hasClass('start'),
            isAccepting: node.hasClass('accepting'),
            position: node.position()
        });

        console.log(`State modified: ${node.data('label')}`);
    }

    /**
     * Get all states
     * @returns {Array} Array of state data
     */
    getAllStates() {
        return this.cy.nodes().map(node => ({
            id: node.data('stateId'),
            label: node.data('label'),
            isStart: node.hasClass('start'),
            isAccepting: node.hasClass('accepting'),
            position: node.position()
        }));
    }

    /**
     * Destroy and cleanup
     */
    destroy() {
        this.disable();
        this.cy = null;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.StateEditor = StateEditor;
}
