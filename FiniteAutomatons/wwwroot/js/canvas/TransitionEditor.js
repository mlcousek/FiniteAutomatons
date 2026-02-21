/**
 * TransitionEditor.js
 * Handles interactive transition (edge) creation, deletion, and editing on the automaton canvas.
 *
 * Workflow:
 *   1. User clicks source node (highlights with .source-selected class + shown hint)
 *   2. User clicks target node → TransitionDialog opens for symbol/stack input
 *   3. Edge added to Cytoscape graph + callbacks fired
 *
 * Also handles:
 *   - Double-click edge to edit symbol
 *   - Delete/Backspace key to remove selected edge
 *   - Escape key to cancel transition in progress
 *
 * @module TransitionEditor
 */

import { TransitionDialog } from './TransitionDialog.js';

/**
 * Manages transition (edge) editing operations on the canvas
 */
export class TransitionEditor {
    /**
     * @param {Object} cy - Cytoscape instance
     * @param {HTMLElement} containerEl - Canvas container element
     * @param {Object} options - Configuration options
     * @param {string} [options.automatonType='DFA'] - Current automaton type
     * @param {Function} [options.onTransitionAdded] - Callback when transition is added
     * @param {Function} [options.onTransitionDeleted] - Callback when transition is deleted
     * @param {Function} [options.onTransitionModified] - Callback when transition is modified
     * @param {Object} [options.actionHistory] - ActionHistory instance for undo/redo
     */
    constructor(cy, containerEl, options = {}) {
        this.cy = cy;
        this.containerEl = containerEl;
        this.automatonType = options.automatonType || 'DFA';
        this.onTransitionAdded = options.onTransitionAdded || (() => {});
        this.onTransitionDeleted = options.onTransitionDeleted || (() => {});
        this.onTransitionModified = options.onTransitionModified || (() => {});
        this.actionHistory = options.actionHistory || null;

        // Two-click state machine
        this._sourceNode = null;

        // Event handlers (stored for clean removal)
        this._nodeClickHandler = null;
        this._edgeDblClickHandler = null;
        this._keyHandler = null;
        this._canvasClickHandler = null;

        // Dialog instance
        this._dialog = new TransitionDialog(containerEl);

        // Hint overlay element
        this._hint = null;

        this.isEnabled = false;
        this._isShowingDialog = false;
    }

    /**
     * Enable transition editing — attaches all event handlers
     */
    enable() {
        if (this.isEnabled) return;
        this.isEnabled = true;
        this._setupEventHandlers();
    }

    /**
     * Disable transition editing — remove handlers, cancel pending state
     */
    disable() {
        if (!this.isEnabled) return;
        this.isEnabled = false;
        this._cancelTransitionInProgress();
        this._removeEventHandlers();
    }

    /**
     * Update the automaton type (for dialog PDA vs standard input)
     * @param {string} type - New automaton type
     */
    setAutomatonType(type) {
        this.automatonType = type;
    }

    /**
     * Check if a transition creation is in progress
     * @returns {boolean}
     */
    isTransitionInProgress() {
        return this._sourceNode !== null;
    }

    // ==================== PRIVATE ====================

    /**
     * Set up all event handlers
     * @private
     */
    _setupEventHandlers() {
        // Node click — two-click transition creation
        this._nodeClickHandler = (event) => {
            // Don't interfere if a dialog is already open
            if (this._isShowingDialog) return;
            this._handleNodeClick(event.target);
        };
        this.cy.on('tap', 'node', this._nodeClickHandler);

        // Edge double-click — edit symbol
        this._edgeDblClickHandler = (event) => {
            if (this._isShowingDialog) return;
            this._handleEdgeDoubleClick(event.target);
        };
        this.cy.on('dbltap', 'edge', this._edgeDblClickHandler);

        // Canvas tap — cancel transition in progress if clicked on empty space
        this._canvasClickHandler = (event) => {
            if (event.target === this.cy && this._sourceNode) {
                this._cancelTransitionInProgress();
            }
        };
        this.cy.on('tap', this._canvasClickHandler);

        // Keyboard — Delete removes selected edge, Escape cancels transition
        this._keyHandler = (e) => {
            if (!this.isEnabled || this._isShowingDialog) return;
            if (e.key === 'Delete' || e.key === 'Backspace') {
                this._handleDeleteKey();
            } else if (e.key === 'Escape') {
                this._cancelTransitionInProgress();
            }
        };
        document.addEventListener('keydown', this._keyHandler);
    }

    /**
     * Remove all event handlers
     * @private
     */
    _removeEventHandlers() {
        if (this._nodeClickHandler) {
            this.cy.off('tap', 'node', this._nodeClickHandler);
            this._nodeClickHandler = null;
        }
        if (this._edgeDblClickHandler) {
            this.cy.off('dbltap', 'edge', this._edgeDblClickHandler);
            this._edgeDblClickHandler = null;
        }
        if (this._canvasClickHandler) {
            this.cy.off('tap', this._canvasClickHandler);
            this._canvasClickHandler = null;
        }
        if (this._keyHandler) {
            document.removeEventListener('keydown', this._keyHandler);
            this._keyHandler = null;
        }
    }

    /**
     * Handle node click in two-click scheme
     * @private
     * @param {Object} node - Clicked Cytoscape node
     */
    _handleNodeClick(node) {
        if (!this._sourceNode) {
            // First click: select source
            this._selectSource(node);
        } else if (this._sourceNode.id() === node.id()) {
            // Clicked same node again — deselect (create self-loop after confirmation)
            // For self-loops we treat it as a normal flow (same source & target)
            this._createTransition(this._sourceNode, node);
        } else {
            // Second click: create transition to target
            this._createTransition(this._sourceNode, node);
        }
    }

    /**
     * Select source node for transition
     * @private
     */
    _selectSource(node) {
        this._sourceNode = node;
        node.addClass('source-selected');

        // Show hint
        this._showHint(`Click target state to complete transition from <strong>${node.data('label')}</strong>, or press Esc to cancel`);
    }

    /**
     * Cancel an in-progress transition (no edge created)
     * @private
     */
    _cancelTransitionInProgress() {
        if (this._sourceNode) {
            this._sourceNode.removeClass('source-selected');
            this._sourceNode = null;
        }
        this._removeHint();
    }

    /**
     * Create transition after both nodes are selected
     * @private
     */
    async _createTransition(sourceNode, targetNode) {
        // Clear selection visuals first
        sourceNode.removeClass('source-selected');
        this._sourceNode = null;
        this._removeHint();

        this._isShowingDialog = true;
        let result;
        try {
            result = await this._dialog.show(sourceNode, targetNode, this.automatonType);
        } finally {
            this._isShowingDialog = false;
        }

        if (!result) return; // Cancelled

        // Build the edge data
        const fromId = sourceNode.data('stateId');
        const toId = targetNode.data('stateId');
        const edgeId = this._generateEdgeId(fromId, toId);
        const isSelfLoop = fromId === toId;
        const isPDA = this.automatonType === 'PDA';

        const label = this._buildEdgeLabel(result, isPDA);
        const classes = [
            isSelfLoop ? 'self-loop' : '',
            isPDA ? 'pda' : ''
        ].filter(Boolean).join(' ');

        // Check for parallel edge
        const reverseEdge = this.cy.getElementById(`edge-${toId}-${fromId}`);
        const existingEdge = this.cy.getElementById(edgeId);

        const edgeData = {
            group: 'edges',
            data: {
                id: edgeId,
                source: `state-${fromId}`,
                target: `state-${toId}`,
                label: label,
                symbol: result.symbol,
                rawSymbol: result.rawSymbol || label,
                isPDA,
                stackPop: result.stackPop,
                stackPush: result.stackPush,
                rawStackPop: result.rawStackPop
            },
            classes
        };

        // If edge between this pair already exists, we need to merge labels or add separate
        if (existingEdge && existingEdge.length > 0) {
            // Append to existing edge label (multiple symbols on same edge)
            const newLabel = existingEdge.data('label') + '\n' + label;
            existingEdge.data('label', newLabel);

            // Record for undo
            if (this.actionHistory) {
                const prevLabel = existingEdge.data('label').replace('\n' + label, '');
                this.actionHistory.recordAction({
                    do: () => existingEdge.data('label', newLabel),
                    undo: () => existingEdge.data('label', prevLabel)
                });
            }

            const transition = this._buildTransitionData(fromId, toId, result, isPDA);
            this.onTransitionAdded(transition);
            return;
        }

        // Auto-detect parallel edge — add .parallel class if reverse edge exists
        if (reverseEdge && reverseEdge.length > 0) {
            edgeData.classes += (edgeData.classes ? ' ' : '') + 'parallel';
            reverseEdge.addClass('parallel');
        }

        // Add edge to graph
        const addedEdge = this.cy.add(edgeData);

        // Record for undo
        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => { if (!this.cy.getElementById(edgeId).length) this.cy.add(edgeData); },
                undo: () => { this.cy.getElementById(edgeId).remove(); }
            });
        }

        const transition = this._buildTransitionData(fromId, toId, result, isPDA);
        this.onTransitionAdded(transition);
        console.log(`Transition added: ${sourceNode.data('label')} → ${targetNode.data('label')} [${label}]`);
    }

    /**
     * Handle double-click on an edge to edit its symbol
     * @private
     */
    async _handleEdgeDoubleClick(edge) {
        this._isShowingDialog = true;
        let result;
        try {
            result = await this._dialog.showEdit(edge, this.automatonType);
        } finally {
            this._isShowingDialog = false;
        }

        if (!result) return; // Cancelled

        const isPDA = this.automatonType === 'PDA';
        const newLabel = this._buildEdgeLabel(result, isPDA);
        const prevLabel = edge.data('label');
        const prevStackPop = edge.data('stackPop');
        const prevStackPush = edge.data('stackPush');

        edge.data('label', newLabel);
        edge.data('symbol', result.symbol);
        edge.data('rawSymbol', result.rawSymbol);
        if (isPDA) {
            edge.data('stackPop', result.stackPop);
            edge.data('stackPush', result.stackPush);
            edge.data('rawStackPop', result.rawStackPop);
        }

        // Record for undo
        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => {
                    edge.data('label', newLabel);
                    edge.data('symbol', result.symbol);
                },
                undo: () => {
                    edge.data('label', prevLabel);
                    edge.data('symbol', prevStackPop); // restore
                    if (isPDA) {
                        edge.data('stackPop', prevStackPop);
                        edge.data('stackPush', prevStackPush);
                    }
                }
            });
        }

        const transition = {
            fromStateId: edge.source().data('stateId'),
            toStateId: edge.target().data('stateId'),
            ...result
        };
        this.onTransitionModified(transition);
        console.log(`Transition modified: ${edge.id()} → [${newLabel}]`);
    }

    /**
     * Handle Delete/Backspace key — remove selected edges
     * @private
     */
    _handleDeleteKey() {
        if (this._sourceNode) {
            // Cancel transition creation first
            this._cancelTransitionInProgress();
            return;
        }

        const selected = this.cy.$(':selected');
        const selectedEdges = selected.filter('edge');

        if (selectedEdges.length > 0) {
            // Collect data for undo
            const edgeSnapshots = selectedEdges.map(e => ({
                data: { ...e.data() },
                classes: e.className()
            }));

            selectedEdges.forEach(edge => {
                const transitionData = {
                    fromStateId: edge.source().data('stateId'),
                    toStateId: edge.target().data('stateId'),
                    symbol: edge.data('symbol'),
                    stackPop: edge.data('stackPop'),
                    stackPush: edge.data('stackPush')
                };

                this.cy.remove(edge);
                this.onTransitionDeleted(transitionData);
                console.log(`Transition deleted: ${edge.id()}`);
            });

            // Record for undo
            if (this.actionHistory) {
                this.actionHistory.recordAction({
                    do: () => edgeSnapshots.forEach(snap => {
                        if (!this.cy.getElementById(snap.data.id).length) {
                            this.cy.add({ group: 'edges', data: snap.data, classes: snap.classes });
                        }
                    }),
                    undo: () => edgeSnapshots.forEach(snap => {
                        if (!this.cy.getElementById(snap.data.id).length) {
                            this.cy.add({ group: 'edges', data: snap.data, classes: snap.classes });
                        }
                    })
                });
                // Fix: actual do is remove, undo is restore
                // Swap the last action's do/undo (the above is backwards, correct below)
                const last = this.actionHistory._undoStack[this.actionHistory._undoStack.length - 1];
                if (last) {
                    const restore = last.do;
                    last.do = last.undo;
                    last.undo = restore;
                }
            }
        }
    }

    /**
     * Build edge label string from dialog result
     * @private
     */
    _buildEdgeLabel(result, isPDA) {
        if (!isPDA) {
            return this._formatSymbol(result.rawSymbol || result.symbol);
        }
        const sym = this._formatSymbol(result.rawSymbol || result.symbol);
        const pop = result.rawStackPop || (result.stackPop === '\0' ? 'ε' : result.stackPop || 'ε');
        const push = result.stackPush || 'ε';
        return `${sym}, ${pop}/${push}`;
    }

    /**
     * Format symbol for display
     * @private
     */
    _formatSymbol(raw) {
        if (!raw || raw === '\0' || raw === '\\0') return 'ε';
        if (raw === 'ε') return 'ε';
        return raw;
    }

    /**
     * Build transition data object for callbacks
     * @private
     */
    _buildTransitionData(fromId, toId, result, isPDA) {
        const trans = {
            fromStateId: fromId,
            toStateId: toId,
            symbol: result.symbol
        };
        if (isPDA) {
            trans.stackPop = result.stackPop;
            trans.stackPush = result.stackPush;
        }
        return trans;
    }

    /**
     * Generate a unique edge ID for a from-to pair
     * @private
     */
    _generateEdgeId(fromId, toId) {
        return `edge-${fromId}-${toId}`;
    }

    /**
     * Show the transition hint overlay
     * @private
     */
    _showHint(html) {
        this._removeHint();
        const hint = document.createElement('div');
        hint.className = 'canvas-edit-hint';
        hint.innerHTML = html;
        this.containerEl.appendChild(hint);
        this._hint = hint;
    }

    /**
     * Remove the hint overlay
     * @private
     */
    _removeHint() {
        if (this._hint) {
            this._hint.remove();
            this._hint = null;
        }
    }

    /**
     * Destroy and cleanup all resources
     */
    destroy() {
        this.disable();
        this._dialog.destroy();
        this._removeHint();
        this.cy = null;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.TransitionEditor = TransitionEditor;
}
