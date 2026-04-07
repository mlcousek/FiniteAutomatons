import { TransitionDialog } from './TransitionDialog.js';

export class TransitionEditor {
    constructor(cy, containerEl, options = {}) {
        this.cy = cy;
        this.containerEl = containerEl;
        this.automatonType = options.automatonType || 'DFA';
        this.onTransitionAdded = options.onTransitionAdded || (() => {});
        this.onTransitionDeleted = options.onTransitionDeleted || (() => {});
        this.onTransitionModified = options.onTransitionModified || (() => {});
        this.actionHistory = options.actionHistory || null;

        this._sourceNode = null;

        this._nodeClickHandler = null;
        this._edgeDblClickHandler = null;
        this._keyHandler = null;
        this._canvasClickHandler = null;

        this._dialog = new TransitionDialog(containerEl);

        this._hint = null;

        this._clickTimer = null;
        this._clickDelay = 320; 
        this._multiClickHandler = null; 
        this._suppressUntil = 0; 

        this.isEnabled = false;
        this._isShowingDialog = false;
    }

    enable() {
        if (this.isEnabled) return;
        this.isEnabled = true;
        this._setupEventHandlers();

        this._multiClickHandler = (event) => {
            if (this._clickTimer) {
                clearTimeout(this._clickTimer);
                this._clickTimer = null;
            }

            this._suppressUntil = Date.now() + this._clickDelay + 50;
            console.log('TransitionEditor: Suppressing source-selection due to StateEditor multi-click');
        };
        window.addEventListener('stateMultiClickHandled', this._multiClickHandler);
    }

    disable() {
        if (!this.isEnabled) return;
        this.isEnabled = false;
        this._cancelTransitionInProgress();
        this._removeEventHandlers();

        if (this._clickTimer) {
            clearTimeout(this._clickTimer);
            this._clickTimer = null;
        }

        if (this._multiClickHandler) {
            window.removeEventListener('stateMultiClickHandled', this._multiClickHandler);
            this._multiClickHandler = null;
        }
    }

    setAutomatonType(type) {
        this.automatonType = type;
    }

    addTransitionDirect(sourceNode, targetNode, symbol, stackPop = null, stackPush = null) {
        const fromId = sourceNode.data('stateId');
        const toId   = targetNode.data('stateId');
        const edgeId = this._generateEdgeId(fromId, toId);
        const isSelfLoop = fromId === toId;
        const isPDA = this.automatonType === 'PDA';

        const rawSymbol = (symbol === '\0' || symbol === '\\0') ? 'ε' : String(symbol);
        const rawPop    = (!stackPop  || stackPop  === '\0' || stackPop  === '\\0') ? 'ε' : String(stackPop);
        const rawPush   = stackPush || 'ε';

        const result = {
            symbol:      symbol === '\0' ? '\0' : symbol,
            rawSymbol,
            stackPop:    stackPop  ?? '\0',
            stackPush:   stackPush ?? '',
            rawStackPop: rawPop
        };

        const label = this._buildEdgeLabel(result, isPDA);

        const classes = [
            isSelfLoop ? 'self-loop' : '',
            isPDA ? 'pda' : ''
        ].filter(Boolean).join(' ');

        const existingEdge = this.cy.getElementById(edgeId);
        const reverseEdge  = this.cy.getElementById(`edge-${toId}-${fromId}`);

        if (existingEdge && existingEdge.length > 0) {
            // Append symbol to existing edge label
            const prevLabel = existingEdge.data('label');
            const separator = isPDA ? '\n' : ', ';
            const newLabel  = prevLabel + separator + label;
            existingEdge.data('label', newLabel);

            if (this.actionHistory) {
                this.actionHistory.recordAction({
                    do:   () => existingEdge.data('label', newLabel),
                    undo: () => existingEdge.data('label', prevLabel)
                });
            }

            const transition = this._buildTransitionData(fromId, toId, result, isPDA);
            this.onTransitionAdded(transition);
            return;
        }

        const edgeData = {
            group: 'edges',
            data: {
                id: edgeId,
                source: `state-${fromId}`,
                target: `state-${toId}`,
                label,
                symbol:     result.symbol,
                rawSymbol:  result.rawSymbol,
                isPDA,
                stackPop:   result.stackPop,
                stackPush:  result.stackPush,
                rawStackPop: result.rawStackPop
            },
            classes: classes + (reverseEdge?.length ? ' parallel' : '')
        };

        if (reverseEdge?.length) {
            reverseEdge.addClass('parallel');
        }

        this.cy.add(edgeData);

        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do:   () => { if (!this.cy.getElementById(edgeId).length) this.cy.add(edgeData); },
                undo: () => { this.cy.getElementById(edgeId).remove(); }
            });
        }

        const transition = this._buildTransitionData(fromId, toId, result, isPDA);
        this.onTransitionAdded(transition);
        console.log(`Transition added (direct): ${sourceNode.data('label')} → ${targetNode.data('label')} [${label}]`);
    }

    isTransitionInProgress() {
        return this._sourceNode !== null;
    }

    // ==================== PRIVATE ====================

    _setupEventHandlers() {

        this._nodeClickHandler = (event) => {
            if (this._isShowingDialog) return;
            this._handleNodeClick(event.target);
        };
        this.cy.on('tap', 'node', this._nodeClickHandler);

        this._edgeDblClickHandler = (event) => {
            if (this._isShowingDialog) return;
            this._handleEdgeDoubleClick(event.target);
        };
        this.cy.on('dbltap', 'edge', this._edgeDblClickHandler);

        this._canvasClickHandler = (event) => {
            if (event.target === this.cy && this._sourceNode) {
                this._cancelTransitionInProgress();
            }
        };
        this.cy.on('tap', this._canvasClickHandler);

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

    _handleNodeClick(node) {
        if (this._sourceNode) {
            if (this._clickTimer) {
                clearTimeout(this._clickTimer);
                this._clickTimer = null;
            }

            if (this._sourceNode.id() === node.id()) {
                this._createTransition(this._sourceNode, node);
            } else {
                this._createTransition(this._sourceNode, node);
            }
            return;
        }

        if (this._clickTimer) {
            clearTimeout(this._clickTimer);
        }

        this._clickTimer = setTimeout(() => {
            this._clickTimer = null;
            if (this.isEnabled && !this._isShowingDialog && Date.now() >= this._suppressUntil) {
                this._selectSource(node);
            }
        }, this._clickDelay);
    }

    _selectSource(node) {
        this._sourceNode = node;
        node.addClass('source-selected');

        this._showHint(`Click target state to complete transition from <strong>${node.data('label')}</strong>, press Esc to cancel or press Del/Backspace for delete`);
    }

    _cancelTransitionInProgress() {
        if (this._clickTimer) {
            clearTimeout(this._clickTimer);
            this._clickTimer = null;
        }

        if (this._sourceNode) {
            this._sourceNode.removeClass('source-selected');
            this._sourceNode = null;
        }
        this._removeHint();
    }

    async _createTransition(sourceNode, targetNode) {
        sourceNode.removeClass('source-selected');
        this._sourceNode = null;
        this._removeHint();

        this._isShowingDialog = true;
        let results;
        try {
            results = await this._dialog.show(sourceNode, targetNode, this.automatonType);
        } finally {
            this._isShowingDialog = false;
        }

        if (!results || results.length === 0) return; // Cancelled

        const fromId = sourceNode.data('stateId');
        const toId = targetNode.data('stateId');
        const edgeId = this._generateEdgeId(fromId, toId);
        const isSelfLoop = fromId === toId;
        const isPDA = this.automatonType === 'PDA';

        const labels = results.map(r => this._buildEdgeLabel(r, isPDA));
        const additions = results.map(r => this._buildTransitionData(fromId, toId, r, isPDA));
        const separator = isPDA ? '\n' : ', ';
        const addedLabelStr = labels.join(separator);

        const classes = [
            isSelfLoop ? 'self-loop' : '',
            isPDA ? 'pda' : ''
        ].filter(Boolean).join(' ');

        const reverseEdge = this.cy.getElementById(`edge-${toId}-${fromId}`);
        const existingEdge = this.cy.getElementById(edgeId);

        if (existingEdge && existingEdge.length > 0) {
            const prevLabel = existingEdge.data('label');
            const newLabel = (prevLabel ? prevLabel + separator : '') + addedLabelStr;
            existingEdge.data('label', newLabel);

            if (this.actionHistory) {
                this.actionHistory.recordAction({
                    do: () => existingEdge.data('label', newLabel),
                    undo: () => existingEdge.data('label', prevLabel)
                });
            }

            additions.forEach(t => this.onTransitionAdded(t));
            return;
        }

        const edgeData = {
            group: 'edges',
            data: {
                id: edgeId,
                source: `state-${fromId}`,
                target: `state-${toId}`,
                label: addedLabelStr,
                symbol: results[0].symbol,
                rawSymbol: results.map(r => r.rawSymbol).join(' '),
                isPDA,
                stackPop: results[0].stackPop,
                stackPush: results[0].stackPush,
                rawStackPop: results[0].rawStackPop
            },
            classes
        };

        if (reverseEdge && reverseEdge.length > 0) {
            edgeData.classes += (edgeData.classes ? ' ' : '') + 'parallel';
            reverseEdge.addClass('parallel');
        }

        this.cy.add(edgeData);

        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => { if (!this.cy.getElementById(edgeId).length) this.cy.add(edgeData); },
                undo: () => { this.cy.getElementById(edgeId).remove(); }
            });
        }

        additions.forEach(t => this.onTransitionAdded(t));
        console.log(`Transition added: ${sourceNode.data('label')} → ${targetNode.data('label')} [${addedLabelStr}]`);
    }

    async _handleEdgeDoubleClick(edge) {
        this._isShowingDialog = true;
        let results;
        try {
            results = await this._dialog.showEdit(edge, this.automatonType);
        } finally {
            this._isShowingDialog = false;
        }

        if (!results || results.length === 0) return; // Cancelled

        if (results === 'DELETE') {
            const edgeSnapshot = {
                data: { ...edge.data() },
                classes: edge.className()
            };
            const transitionData = {
                fromStateId: edge.source().data('stateId'),
                toStateId: edge.target().data('stateId'),
                symbol: edge.data('symbol'),
                stackPop: edge.data('stackPop'),
                stackPush: edge.data('stackPush')
            };

            this.cy.remove(edge);
            this.onTransitionDeleted(transitionData);
            
            if (this.actionHistory) {
                this.actionHistory.recordAction({
                    do: () => { this.cy.getElementById(edgeSnapshot.data.id).remove(); },
                    undo: () => { 
                        if (!this.cy.getElementById(edgeSnapshot.data.id).length) {
                            this.cy.add({ group: 'edges', data: edgeSnapshot.data, classes: edgeSnapshot.classes });
                        }
                    }
                });
            }
            console.log(`Transition deleted via dialog: ${edgeSnapshot.data.id}`);
            return;
        }

        const isPDA = this.automatonType === 'PDA';
        const labels = results.map(r => this._buildEdgeLabel(r, isPDA));
        const newLabel = labels.join(isPDA ? '\n' : ', ');

        const prevLabel = edge.data('label');
        const prevSymbol = edge.data('symbol');
        const prevRawSymbol = edge.data('rawSymbol');
        const prevStackPop = edge.data('stackPop');
        const prevStackPush = edge.data('stackPush');
        const prevRawStackPop = edge.data('rawStackPop');

        edge.data('label', newLabel);
        edge.data('symbol', results[0].symbol);
        edge.data('rawSymbol', results.map(r => r.rawSymbol).join(' '));
        if (isPDA) {
            edge.data('stackPop', results[0].stackPop);
            edge.data('stackPush', results[0].stackPush);
            edge.data('rawStackPop', results[0].rawStackPop);
        }

        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => {
                    edge.data('label', newLabel);
                    edge.data('symbol', results[0].symbol);
                    edge.data('rawSymbol', results.map(r => r.rawSymbol).join(' '));
                    if (isPDA) {
                        edge.data('stackPop', results[0].stackPop);
                        edge.data('stackPush', results[0].stackPush);
                        edge.data('rawStackPop', results[0].rawStackPop);
                    }
                },
                undo: () => {
                    edge.data('label', prevLabel);
                    edge.data('symbol', prevSymbol);
                    edge.data('rawSymbol', prevRawSymbol);
                    if (isPDA) {
                        edge.data('stackPop', prevStackPop);
                        edge.data('stackPush', prevStackPush);
                        edge.data('rawStackPop', prevRawStackPop);
                    }
                }
            });
        }

        const transition = {
            fromStateId: edge.source().data('stateId'),
            toStateId: edge.target().data('stateId'),
            ...results[0]
        };
        this.onTransitionModified(transition);
        console.log(`Transition modified: ${edge.id()} → [${newLabel}]`);
    }

    _handleDeleteKey() {
        if (this._sourceNode) {
            this._cancelTransitionInProgress();
            return;
        }

        const selected = this.cy.$(':selected');
        const selectedEdges = selected.filter('edge');

        if (selectedEdges.length > 0) {
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

            if (this.actionHistory) {
                this.actionHistory.recordAction({
                    do: () => edgeSnapshots.forEach(snap => {
                        this.cy.getElementById(snap.data.id).remove();
                    }),
                    undo: () => edgeSnapshots.forEach(snap => {
                        if (!this.cy.getElementById(snap.data.id).length) {
                            this.cy.add({ group: 'edges', data: snap.data, classes: snap.classes });
                        }
                    })
                });
            }
        }
    }

    _buildEdgeLabel(result, isPDA) {
        if (!isPDA) {
            return this._formatSymbol(result.rawSymbol || result.symbol);
        }
        const sym = this._formatSymbol(result.rawSymbol || result.symbol);
        const pop = result.rawStackPop || (result.stackPop === '\0' ? 'ε' : result.stackPop || 'ε');
        const push = result.stackPush || 'ε';
        return `${sym}, ${pop}/${push}`;
    }

    _formatSymbol(raw) {
        if (!raw || raw === '\0' || raw === '\\0') return 'ε';
        if (raw === 'ε') return 'ε';
        return raw;
    }

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

    _generateEdgeId(fromId, toId) {
        return `edge-${fromId}-${toId}`;
    }

    _showHint(html) {
        this._removeHint();
        const hint = document.createElement('div');
        hint.className = 'canvas-edit-hint';
        hint.innerHTML = html;
        this.containerEl.appendChild(hint);
        this._hint = hint;
    }

    _removeHint() {
        if (this._hint) {
            this._hint.remove();
            this._hint = null;
        }
    }

    destroy() {
        this.disable();
        this._dialog.destroy();
        this._removeHint();
        this.cy = null;
    }
}

if (typeof window !== 'undefined') {
    window.TransitionEditor = TransitionEditor;
}
