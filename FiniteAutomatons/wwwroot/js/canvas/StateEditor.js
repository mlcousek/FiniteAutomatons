/**
 * StateEditor.js
 * Handles state creation, deletion, and property editing for automaton canvas
 * Provides interactive state management functionality (Phase 2)
 * 
 * @module StateEditor
 */

import { TransitionDialog } from './TransitionDialog.js';

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

        // Action history for undo/redo
        this.actionHistory = options.actionHistory || null;

        // Event handlers
        this.clickHandler = null;
        this.keyHandler = null;
        this.contextMenuHandler = null;
        this.domContextMenuHandler = null;
        this.nodeClickHandler = null;

        // Dialog instance
        this._dialog = new TransitionDialog(cy.container());
        this._isShowingDialog = false;

        // Multi-click tracking for double/triple click detection
        this._clickTracker = {
            nodeId: null,
            count: 0,
            timer: null,
            clickDelay: 300 // ms between clicks to count as multi-click
        };

        // State
        this.isEnabled = false;
    }

    /**
     * Delete a transition (edge) by id
     * @param {string} edgeId - Edge id (e.g., "edge-1")
     */
    deleteTransition(edgeId) {
        const edge = this.cy.getElementById(edgeId);
        if (!edge || !edge.isEdge()) {
            console.warn('Edge not found:', edgeId);
            return;
        }

        const data = { ...edge.data() };
        const classes = edge.className();

        // Remove edge
        this.cy.remove(edge);

        // Record undo/redo
        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => { this.cy.getElementById(edgeId).remove(); },
                undo: () => {
                    if (!this.cy.getElementById(edgeId).length) {
                        this.cy.add({ group: 'edges', data, classes });
                    }
                }
            });
        }

        console.log(`Transition deleted: ${edgeId}`);
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
            if (event.target === this.cy && !this._isShowingDialog) {
                this._handleCanvasClick(event);
            }
        };
        this.cy.on('tap', this.clickHandler);

        // Click on node for multi-click detection (double/triple click)
        this.nodeClickHandler = (event) => {
            if (event.target.isNode() && !this._isShowingDialog) {
                this._handleNodeClick(event);
            }
        };
        this.cy.on('tap', 'node', this.nodeClickHandler);

        // Delete key to remove selected state
        this.keyHandler = (e) => {
            if ((e.key === 'Delete' || e.key === 'Backspace') && !this._isShowingDialog) {
                this._handleDeleteKey();
            }
        };
        document.addEventListener('keydown', this.keyHandler);

        // Right-click for context menu
        this.contextMenuHandler = (event) => {
            if (event.target.isNode() && !this._isShowingDialog) {
                this._handleContextMenu(event);
            }
        };
        this.cy.on('cxttap', this.contextMenuHandler);

        // Prevent native browser context menu inside the Cytoscape container when editor is enabled.
        // Use document-level listener in capture phase so we catch the event before other handlers/renderer.
        const container = this.cy && this.cy.container && this.cy.container();
        if (container) {
            this.domContextMenuHandler = (e) => {
                try {
                    if (!this.isEnabled) return;
                    const rect = container.getBoundingClientRect();
                    const x = e.clientX;
                    const y = e.clientY;
                    const inside = x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
                    if (inside) {
                        // Prevent native menu for anything inside the canvas while editor is enabled
                        e.preventDefault();
                        e.stopPropagation();
                    }
                } catch (err) {
                    // swallow - don't break page if boundingClientRect fails
                }
            };
            document.addEventListener('contextmenu', this.domContextMenuHandler, true);
        }
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

        if (this.nodeClickHandler) {
            this.cy.off('tap', 'node', this.nodeClickHandler);
            this.nodeClickHandler = null;
        }

        if (this.keyHandler) {
            document.removeEventListener('keydown', this.keyHandler);
            this.keyHandler = null;
        }

        if (this.contextMenuHandler) {
            this.cy.off('cxttap', this.contextMenuHandler);
            this.contextMenuHandler = null;
        }

        if (this.domContextMenuHandler) {
            // We attached the listener to document in capture phase; remove it the same way.
            try {
                document.removeEventListener('contextmenu', this.domContextMenuHandler, true);
            } catch (err) {
                // fallback: try removing from container
                const container = this.cy && this.cy.container && this.cy.container();
                if (container) {
                    container.removeEventListener('contextmenu', this.domContextMenuHandler);
                }
            }
            this.domContextMenuHandler = null;
        }

        // Clear click tracker
        if (this._clickTracker.timer) {
            clearTimeout(this._clickTracker.timer);
            this._clickTracker.timer = null;
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
     * Handle click on node - detect double/triple click for state property toggling
     * Double-click: Toggle accepting state
     * Triple-click: Toggle start state
     * @private
     * @param {Object} event - Cytoscape tap event
     */
    _handleNodeClick(event) {
        const node = event.target;
        const nodeId = node.id();
        const tracker = this._clickTracker;

        // If clicking a different node, reset counter
        if (tracker.nodeId !== nodeId) {
            tracker.nodeId = nodeId;
            tracker.count = 1;

            // Clear previous timer
            if (tracker.timer) {
                clearTimeout(tracker.timer);
            }

            // Start timer for click sequence
            tracker.timer = setTimeout(() => {
                // Single click - reset tracker
                tracker.count = 0;
                tracker.nodeId = null;
                tracker.timer = null;
            }, tracker.clickDelay);

            return;
        }

        // Same node clicked again within delay - increment counter
        tracker.count++;

        // Clear previous timer
        if (tracker.timer) {
            clearTimeout(tracker.timer);
        }

        // Handle based on click count
        if (tracker.count === 2) {
            // Double-click: Toggle accepting state — but delay to check if triple-click follows
            console.log('Double-click detected: waiting for possible triple-click');

            // Notify TransitionEditor to suppress the blue overlay
            window.dispatchEvent(new CustomEvent('stateMultiClickHandled', { 
                detail: { nodeId: node.id(), clickCount: 2 } 
            }));

            // Delay action: only toggle accepting state if no 3rd click arrives in time
            tracker.timer = setTimeout(() => {
                console.log('Double-click confirmed: toggling accepting state');
                this.toggleAcceptingState(node);
                tracker.count = 0;
                tracker.nodeId = null;
                tracker.timer = null;
            }, tracker.clickDelay);

        } else if (tracker.count === 3) {
            // Triple-click: cancel pending double-click action, only toggle start state
            if (tracker.timer) {
                clearTimeout(tracker.timer);
                tracker.timer = null;
            }

            console.log('Triple-click detected: toggling start state only');
            this.toggleStartState(node);

            // Notify TransitionEditor to suppress the blue overlay
            window.dispatchEvent(new CustomEvent('stateMultiClickHandled', { 
                detail: { nodeId: node.id(), clickCount: 3 } 
            }));

            // Reset tracker
            tracker.count = 0;
            tracker.nodeId = null;
            tracker.timer = null;

        } else if (tracker.count > 3) {
            // More than 3 clicks - reset
            if (tracker.timer) {
                clearTimeout(tracker.timer);
                tracker.timer = null;
            }
            tracker.count = 0;
            tracker.nodeId = null;
        }
    }

    /**
     * Handle Delete/Backspace key - remove selected state
     * @private
     */
    _handleDeleteKey() {
        const selected = this.cy.$(':selected');
        if (!selected || selected.length === 0) return;

        // Delete nodes and edges. Deleting a node will also remove connected edges
        selected.forEach(elem => {
            if (elem.isNode && elem.isNode()) {
                this.deleteState(elem.id());
            } else if (elem.isEdge && elem.isEdge()) {
                this.deleteTransition(elem.id());
            }
        });
    }

    /**
     * Handle right-click on node - show custom property dialog
     * @private
     * @param {Object} event - Cytoscape context tap event
     */
    async _handleContextMenu(event) {
        // Prevent native browser context menu (use the original DOM event if present)
        if (event.originalEvent && typeof event.originalEvent.preventDefault === 'function') {
            event.originalEvent.preventDefault();
        }
        // Also call Cytoscape event preventDefault for completeness
        if (typeof event.preventDefault === 'function') {
            event.preventDefault();
        }
        const node = event.target;

        this._isShowingDialog = true;
        let action;
        try {
            action = await this._dialog.showNodeProperties(node);
        } finally {
            this._isShowingDialog = false;
        }

        if (!action) return;

        if (action === 'start') {
            this.toggleStartState(node);
        } else if (action === 'accepting') {
            this.toggleAcceptingState(node);
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

        // Record for undo
        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => {
                    if (!this.cy.getElementById(nodeId).length) {
                        this.cy.add({ group: 'nodes', data: { id: nodeId, stateId: newStateId, label: `q${newStateId}`, isStart: false, isAccepting: false }, position: { x, y } });
                    }
                },
                undo: () => {
                    this.cy.getElementById(nodeId).remove();
                }
            });
        }

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
        const position = { ...node.position() };
        const classes = node.className();
        const data = { ...node.data() };
        // Snapshot connected edges for undo
        const connectedEdges = node.connectedEdges().map(e => ({ data: { ...e.data() }, classes: e.className() }));

        // Remove node (this also removes connected edges)
        this.cy.remove(node);

        // Record for undo
        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => { this.cy.getElementById(nodeId).remove(); },
                undo: () => {
                    if (!this.cy.getElementById(nodeId).length) {
                        this.cy.add({ group: 'nodes', data, position, classes });
                        connectedEdges.forEach(e => {
                            if (!this.cy.getElementById(e.data.id).length) {
                                this.cy.add({ group: 'edges', data: e.data, classes: e.classes });
                            }
                        });
                    }
                }
            });
        }

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
        const wasStart = node.hasClass('start');
        // Capture previous start node for undo
        const prevStartNode = wasStart ? null : this.cy.nodes('.start').first();
        const prevStartId = prevStartNode?.id();

        if (!wasStart) {
            // Remove start class from all other nodes (only one start state)
            this.cy.nodes().removeClass('start');
            this.cy.nodes().forEach(n => n.data('isStart', false));
            
            // Add start class to this node
            node.addClass('start');
            node.data('isStart', true);
        } else {
            // Remove start state
            node.removeClass('start');
            node.data('isStart', false);
        }

        // Record for undo
        if (this.actionHistory) {
            const nodeId = node.id();
            this.actionHistory.recordAction({
                do: () => {
                    this.cy.nodes().removeClass('start');
                    this.cy.nodes().forEach(n => n.data('isStart', false));
                    if (!wasStart) { this.cy.getElementById(nodeId).addClass('start').data('isStart', true); }
                },
                undo: () => {
                    this.cy.nodes().removeClass('start');
                    this.cy.nodes().forEach(n => n.data('isStart', false));
                    if (wasStart) { this.cy.getElementById(nodeId).addClass('start').data('isStart', true); }
                    else if (prevStartId) { this.cy.getElementById(prevStartId).addClass('start').data('isStart', true); }
                }
            });
        }

        this._notifyStateModified(node);
    }

    /**
     * Toggle a state as accepting state
     * @param {Object} node - Cytoscape node
     */
    toggleAcceptingState(node) {
        const wasAccepting = node.hasClass('accepting');

        if (!wasAccepting) {
            node.addClass('accepting');
            node.data('isAccepting', true);
        } else {
            node.removeClass('accepting');
            node.data('isAccepting', false);
        }

        // Record for undo
        if (this.actionHistory) {
            const nodeId = node.id();
            this.actionHistory.recordAction({
                do: () => {
                    const n = this.cy.getElementById(nodeId);
                    if (!wasAccepting) { n.addClass('accepting').data('isAccepting', true); }
                    else { n.removeClass('accepting').data('isAccepting', false); }
                },
                undo: () => {
                    const n = this.cy.getElementById(nodeId);
                    if (wasAccepting) { n.addClass('accepting').data('isAccepting', true); }
                    else { n.removeClass('accepting').data('isAccepting', false); }
                }
            });
        }

        this._notifyStateModified(node);
    }

    /**
     * Show property dialog for a node (used for programmatic access; right-click uses _handleContextMenu)
     * @param {Object} node - Cytoscape node
     * @deprecated Use right-click context menu (_handleContextMenu) instead
     */
    showPropertyDialog(node) {
        this._handleContextMenu({ target: node, preventDefault: () => {} });
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
        if (this._dialog) {
            this._dialog.destroy();
            this._dialog = null;
        }
        this.cy = null;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.StateEditor = StateEditor;
}
