
import { TransitionDialog } from './TransitionDialog.js';

export class StateEditor {

    constructor(cy, options = {}) {
        this.cy = cy;
        this.options = options;

        this.onStateAdded = options.onStateAdded || (() => {});
        this.onStateDeleted = options.onStateDeleted || (() => {});
        this.onStateModified = options.onStateModified || (() => {});

        this.actionHistory = options.actionHistory || null;

        this.clickHandler = null;
        this.keyHandler = null;
        this.contextMenuHandler = null;
        this.domContextMenuHandler = null;
        this.nodeClickHandler = null;

        this._dialog = new TransitionDialog(cy.container());
        this._isShowingDialog = false;

        this._clickTracker = {
            nodeId: null,
            count: 0,
            timer: null,
            clickDelay: 300 
        };

        this.isEnabled = false;
    }

    deleteTransition(edgeId) {
        const edge = this.cy.getElementById(edgeId);
        if (!edge || !edge.isEdge()) {
            console.warn('Edge not found:', edgeId);
            return;
        }

        const data = { ...edge.data() };
        const classes = edge.className();

        this.cy.remove(edge);

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

    enable() {
        if (this.isEnabled) return;
        
        this.isEnabled = true;
        this._setupEventHandlers();
        console.log('StateEditor enabled');
    }

    disable() {
        if (!this.isEnabled) return;
        
        this.isEnabled = false;
        this._removeEventHandlers();
        console.log('StateEditor disabled');
    }

    _setupEventHandlers() {
        this.clickHandler = (event) => {
            if (event.target === this.cy && !this._isShowingDialog) {
                if (this.cy.$('.source-selected').length > 0) return;
                this._handleCanvasClick(event);
            }
        };
        this.cy.on('tap', this.clickHandler);

        this.nodeClickHandler = (event) => {
            if (event.target.isNode() && !this._isShowingDialog) {
                this._handleNodeClick(event);
            }
        };
        this.cy.on('tap', 'node', this.nodeClickHandler);

        this.keyHandler = (e) => {
            if (e.key !== 'Delete' || this._isShowingDialog || this._isTypingTarget(e)) return;
            e.preventDefault();
            this._handleDeleteKey();
        };
        document.addEventListener('keydown', this.keyHandler);

        this.contextMenuHandler = (event) => {
            if (event.target.isNode() && !this._isShowingDialog) {
                this._handleContextMenu(event);
            }
        };
        this.cy.on('cxttap', this.contextMenuHandler);

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
                        e.preventDefault();
                        e.stopPropagation();
                    }
                } catch (err) {

                }
            };
            document.addEventListener('contextmenu', this.domContextMenuHandler, true);
        }
    }

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
            try {
                document.removeEventListener('contextmenu', this.domContextMenuHandler, true);
            } catch (err) {
                const container = this.cy && this.cy.container && this.cy.container();
                if (container) {
                    container.removeEventListener('contextmenu', this.domContextMenuHandler);
                }
            }
            this.domContextMenuHandler = null;
        }

        if (this._clickTracker.timer) {
            clearTimeout(this._clickTracker.timer);
            this._clickTracker.timer = null;
        }
    }

    _handleCanvasClick(event) {
        const position = event.position;
        this.addState(position.x, position.y);
    }

    _handleNodeClick(event) {
        const node = event.target;
        const nodeId = node.id();
        const tracker = this._clickTracker;

        if (tracker.nodeId !== nodeId) {
            tracker.nodeId = nodeId;
            tracker.count = 1;

            if (tracker.timer) {
                clearTimeout(tracker.timer);
            }

            tracker.timer = setTimeout(() => {
                tracker.count = 0;
                tracker.nodeId = null;
                tracker.timer = null;
            }, tracker.clickDelay);

            return;
        }

        tracker.count++;

        if (tracker.timer) {
            clearTimeout(tracker.timer);
        }

        if (tracker.count === 2) {
            console.log('Double-click detected: waiting for possible triple-click');

            window.dispatchEvent(new CustomEvent('stateMultiClickHandled', { 
                detail: { nodeId: node.id(), clickCount: 2 } 
            }));

            tracker.timer = setTimeout(() => {
                console.log('Double-click confirmed: toggling accepting state');
                this.toggleAcceptingState(node);
                tracker.count = 0;
                tracker.nodeId = null;
                tracker.timer = null;
            }, tracker.clickDelay);

        } else if (tracker.count === 3) {
            if (tracker.timer) {
                clearTimeout(tracker.timer);
                tracker.timer = null;
            }

            console.log('Triple-click detected: toggling start state only');
            this.toggleStartState(node);

            window.dispatchEvent(new CustomEvent('stateMultiClickHandled', { 
                detail: { nodeId: node.id(), clickCount: 3 } 
            }));

            tracker.count = 0;
            tracker.nodeId = null;
            tracker.timer = null;

        } else if (tracker.count > 3) {
            if (tracker.timer) {
                clearTimeout(tracker.timer);
                tracker.timer = null;
            }
            tracker.count = 0;
            tracker.nodeId = null;
        }
    }

    _handleDeleteKey() {
        const selected = this.cy.$(':selected');
        if (!selected || selected.length === 0) return;

        selected.forEach(elem => {
            if (elem.isNode && elem.isNode()) {
                this.deleteState(elem.id());
            } else if (elem.isEdge && elem.isEdge()) {
                this.deleteTransition(elem.id());
            }
        });
    }

    _isTypingTarget(event) {
        const isEditableElement = (element) => {
            if (!element || !(element instanceof HTMLElement)) return false;
            if (element.isContentEditable) return true;

            const tag = element.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || tag === 'OPTION') {
                return true;
            }

            return !!element.closest('input, textarea, select, [contenteditable="true"], .cd-dialog');
        };

        return isEditableElement(event?.target) || isEditableElement(document.activeElement);
    }

    async _handleContextMenu(event) {
        if (event.originalEvent && typeof event.originalEvent.preventDefault === 'function') {
            event.originalEvent.preventDefault();
        }
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

    addState(x, y) {
        const newStateId = this._getNextStateId();
        const nodeId = `state-${newStateId}`;

        // First state on an empty canvas → automatically becomes start
        const isFirstState = this.cy.nodes().filter(n => !n.hasClass('dummy')).length === 0;

        const node = this.cy.add({
            group: 'nodes',
            data: {
                id: nodeId,
                stateId: newStateId,
                label: `q${newStateId}`,
                isStart: isFirstState,
                isAccepting: false
            },
            position: { x, y },
            classes: isFirstState ? 'start' : ''
        });

        if (isFirstState) {
            console.log(`First state added — automatically set as start state: q${newStateId}`);
        }

        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => {
                    if (!this.cy.getElementById(nodeId).length) {
                        this.cy.add({ group: 'nodes', data: { id: nodeId, stateId: newStateId, label: `q${newStateId}`, isStart: isFirstState, isAccepting: false }, position: { x, y }, classes: isFirstState ? 'start' : '' });
                    }
                },
                undo: () => {
                    this.cy.getElementById(nodeId).remove();
                }
            });
        }

        this.onStateAdded({
            id: newStateId,
            label: `q${newStateId}`,
            isStart: isFirstState,
            isAccepting: false,
            position: { x, y }
        });

        console.log(`State added: q${newStateId}`);
        return node;
    }


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
        const connectedEdges = node.connectedEdges().map(e => ({ data: { ...e.data() }, classes: e.className() }));

        const wasStart = node.hasClass('start');

        this.cy.remove(node);

        // If the deleted node was the start state, promote another node
        let promotedNodeId = null;
        if (wasStart) {
            const remaining = this.cy.nodes().filter(n => !n.hasClass('dummy'));
            if (remaining.length > 0) {
                const newStart = remaining.first();
                promotedNodeId = newStart.id();
                newStart.addClass('start');
                newStart.data('isStart', true);
                this.onStateModified({
                    id: newStart.data('stateId'),
                    label: newStart.data('label'),
                    isStart: true,
                    isAccepting: newStart.hasClass('accepting')
                });
                console.log(`Start state was deleted — promoted ${newStart.data('label')} to start state.`);
            }
        }

        if (this.actionHistory) {
            this.actionHistory.recordAction({
                do: () => {
                    this.cy.getElementById(nodeId).remove();
                    if (wasStart && promotedNodeId) {
                        const n = this.cy.getElementById(promotedNodeId);
                        if (n.length) { n.addClass('start').data('isStart', true); }
                    }
                },
                undo: () => {
                    if (!this.cy.getElementById(nodeId).length) {
                        // Remove promoted start before restoring
                        if (wasStart && promotedNodeId) {
                            const promoted = this.cy.getElementById(promotedNodeId);
                            if (promoted.length) {
                                promoted.removeClass('start').data('isStart', false);
                                this.onStateModified({
                                    id: promoted.data('stateId'),
                                    label: promoted.data('label'),
                                    isStart: false,
                                    isAccepting: promoted.hasClass('accepting')
                                });
                            }
                        }
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

        this.onStateDeleted({
            id: stateId,
            label: label
        });

        console.log(`State deleted: ${label}`);
    }

    toggleStartState(node) {
        const wasStart = node.hasClass('start');
        const prevStartNode = wasStart ? null : this.cy.nodes('.start').first();
        const prevStartId = prevStartNode?.id();

        if (!wasStart) {
            this.cy.nodes().removeClass('start');
            this.cy.nodes().forEach(n => n.data('isStart', false));
            
            node.addClass('start');
            node.data('isStart', true);
        } else {
            console.log('Cannot remove start state. Automaton must have at least one starting state.');
            return;
        }

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

    toggleAcceptingState(node) {
        const wasAccepting = node.hasClass('accepting');

        if (!wasAccepting) {
            node.addClass('accepting');
            node.data('isAccepting', true);
        } else {
            node.removeClass('accepting');
            node.data('isAccepting', false);
        }

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

    showPropertyDialog(node) {
        this._handleContextMenu({ target: node, preventDefault: () => {} });
    }

    _getNextStateId() {
        const existingIds = this.cy.nodes().map(n => n.data('stateId')).filter(id => id !== undefined);
        if (existingIds.length === 0) {
            return 1;
        }
        return Math.max(...existingIds) + 1;
    }

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

    getAllStates() {
        return this.cy.nodes().map(node => ({
            id: node.data('stateId'),
            label: node.data('label'),
            isStart: node.hasClass('start'),
            isAccepting: node.hasClass('accepting'),
            position: node.position()
        }));
    }

    destroy() {
        this.disable();
        if (this._dialog) {
            this._dialog.destroy();
            this._dialog = null;
        }
        this.cy = null;
    }
}

if (typeof window !== 'undefined') {
    window.StateEditor = StateEditor;
}
