import { AutomatonRenderer } from './AutomatonRenderer.js';
import { LayoutEngine } from './LayoutEngine.js';
import { CanvasInteractionHandler } from './CanvasInteractionHandler.js';
import { EditModeManager } from './EditModeManager.js';
import { StateEditor } from './StateEditor.js';
import { TransitionEditor } from './TransitionEditor.js';
import { ActionHistory } from './ActionHistory.js';
import { CanvasLayoutCache } from './CanvasLayoutCache.js';

export class AutomatonCanvas {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.container = document.getElementById(containerId);
        
        if (!this.container) {
            throw new Error(`Container element with ID '${containerId}' not found`);
        }

        this.options = {
            readOnly: options.readOnly ?? true,
            enablePanZoom: options.enablePanZoom ?? true,
            zoomOnWheel: options.zoomOnWheel ?? false,
            layoutName: options.layoutName ?? 'dagre',
            styleOverrides: options.styleOverrides ?? {},
            useBranchColorsForNondeterminism: options.useBranchColorsForNondeterminism ?? true,
            minZoom: options.minZoom ?? 0.3,
            maxZoom: options.maxZoom ?? 3,
            wheelSensitivity: options.wheelSensitivity ?? 0.2,
            ...options
        };

        this.cy = null; 
        this.automatonType = null;
        this.currentData = null;
        this.activeStateIds = [];
        this.interactionHandler = null;
        this.editModeManager = null; 
        this.stateEditor = null; 
        this.transitionEditor = null; 
        this.actionHistory = null; 
        this.moveEnabled = true; 
        this.isInitialized = false;

        this._layoutFingerprint = null; 
        this._savePositionsDebounceTimer = null;

        this._handleResize = this._handleResize.bind(this);
        this._onNodeFreed = this._onNodeFreed.bind(this);
    }

    init() {
        if (this.isInitialized) {
            console.warn('Canvas already initialized');
            return;
        }

        try {
            if (typeof cytoscape === 'undefined') {
                throw new Error('Cytoscape.js library not loaded. Include cytoscape.min.js before this script.');
            }

            this.cy = cytoscape({
                container: this.container,
                layout: { name: 'preset' }, 
                style: AutomatonRenderer.getStylesheet(this.automatonType, this.options.styleOverrides),
                wheelSensitivity: this.options.wheelSensitivity,
                minZoom: this.options.minZoom,
                maxZoom: this.options.maxZoom,
                boxSelectionEnabled: false,
                autounselectify: this.options.readOnly,
                autoungrabify: false,
                userZoomingEnabled: !!(this.options.enablePanZoom && this.options.zoomOnWheel),
                userPanningEnabled: this.options.enablePanZoom,
                pixelRatio: 'auto'
            });

            if (this.options.enablePanZoom) {
                this.interactionHandler = new CanvasInteractionHandler(this.cy, this.options);
                this.interactionHandler.enable();
            }

            this.editModeManager = new EditModeManager(this.cy, {
                enableByDefault: false, // Start in view mode
                onModeChange: (isEditMode) => {
                    this._onEditModeChange(isEditMode);
                }
            });

            this.actionHistory = new ActionHistory({
                maxSize: 100,
                onHistoryChanged: ({ canUndo, canRedo }) => {
                    const undoBtn = document.getElementById('undoBtn');
                    const redoBtn = document.getElementById('redoBtn');
                    if (undoBtn) undoBtn.disabled = !canUndo;
                    if (redoBtn) redoBtn.disabled = !canRedo;
                }
            });

            this.stateEditor = new StateEditor(this.cy, {
                onStateAdded: (state) => this._onStateAdded(state),
                onStateDeleted: (state) => this._onStateDeleted(state),
                onStateModified: (state) => this._onStateModified(state),
                actionHistory: this.actionHistory
            });

            this.transitionEditor = new TransitionEditor(this.cy, this.container, {
                automatonType: this.automatonType || 'DFA',
                onTransitionAdded: (transition) => this._onTransitionAdded(transition),
                onTransitionDeleted: (transition) => this._onTransitionDeleted(transition),
                onTransitionModified: (transition) => this._onTransitionModified(transition),
                actionHistory: this.actionHistory
            });

            this._setupEventListeners();

            this.cy.on('free', 'node', this._onNodeFreed);

            window.addEventListener('resize', this._handleResize);

            this.isInitialized = true;
        } catch (error) {
            console.error('Failed to initialize AutomatonCanvas:', error);
            this._showErrorMessage('Failed to initialize canvas: ' + error.message);
            throw error;
        }
    }

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

            if (data.states && data.states.length > 0) {
                const stateIds = data.states.map(s => s.id);
                this._layoutFingerprint = CanvasLayoutCache.buildFingerprint(data.type, stateIds);
            } else {
                this._layoutFingerprint = null;
            }

            this.cy.elements().remove();

            AutomatonRenderer.render(this.cy, data);

            const cachedPositions = this._layoutFingerprint
                ? CanvasLayoutCache.load(this._layoutFingerprint)
                : null;

            if (cachedPositions && Object.keys(cachedPositions).length > 0) {
                LayoutEngine.applyLayout(this.cy, 'preset', {
                    automatonType: this.automatonType,
                    stateCount: data.states.length
                });
                CanvasLayoutCache.applyPositions(this.cy, cachedPositions);
            } else {
                const layoutName = data.layoutName || this.options.layoutName;
                LayoutEngine.applyLayout(this.cy, layoutName, {
                    automatonType: this.automatonType,
                    stateCount: data.states.length
                });
            }

            this._applyDraggingState();

            if (this.transitionEditor) {
                this.transitionEditor.setAutomatonType(data.type);
            }
            if (this.actionHistory) {
                this.actionHistory.clear();
            }

            if (this.activeStateIds.length > 0) {
                this.highlight(this.activeStateIds);
            }

            this._fitToViewport();

            if (!this._lastLoadedType || this._lastLoadedType !== this.automatonType) {
                console.log(`Loaded ${this.automatonType} with ${data.states.length} states`);
                this._lastLoadedType = this.automatonType;
            }
        } catch (error) {
            console.error('Failed to load automaton:', error);
            this._showErrorMessage('Failed to render automaton: ' + error.message);
        }
    }

    highlight(stateIds) {
        if (!this.cy) return;

        this.cy.nodes().removeClass('active');
        this.cy.edges().removeClass('active');
        const PALETTE_SIZE = 16;
        for (let i = 0; i < PALETTE_SIZE; i++) {
            this.cy.nodes().removeClass(`active-branch-${i}`);
            this.cy.edges().removeClass(`active-branch-${i}`);
        }
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
            this.setSimulationState(false);
            return;
        }

        this.activeStateIds = stateIds;

        this.setSimulationState(true);

        this.cy.nodes().forEach(node => {
            node.ungrabify();
        });

        const automatonTypeUpper = (this.automatonType || '').toUpperCase();
        const isNondet = this.options.useBranchColorsForNondeterminism !== false
            && (automatonTypeUpper === 'NFA' || automatonTypeUpper === 'EPSILONNFA' || automatonTypeUpper === 'NPDA')
            && stateIds.length > 1;

        const palette = [
            '#e63946', '#2a9d8f', '#f4a261', '#6a4c93', '#1d3557', '#ffb703',
            '#8ac926', '#1982c4', '#ff6b6b', '#4cc9f0', '#f72585', '#7209b7',
            '#3a0ca3', '#4361ee', '#2ec4b6', '#ffd166'
        ];

        stateIds.forEach((stateId, idx) => {
            const node = this.cy.getElementById(`state-${stateId}`);
            if (!node) return;

            if (isNondet) {
                const branchIndex = idx; 
                const paletteIndex = branchIndex % palette.length;

                if (branchIndex < palette.length) {
                    const cls = `active-branch-${paletteIndex}`;
                    node.addClass('active');
                    node.addClass(cls);

                    node.outgoers('edge').forEach(e => {
                        e.addClass('active');
                        e.addClass(cls);
                    });
                } else {
                    const hue = (branchIndex * 47) % 360; 
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

    updateStyle(styleOverrides = {}) {
        if (!this.cy) return;

        const newStyle = AutomatonRenderer.getStylesheet(
            this.automatonType, 
            { ...this.options.styleOverrides, ...styleOverrides }
        );
        
        this.cy.style(newStyle);
    }

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

    fit() {
        this._fitToViewport();
    }

    resetView() {
        if (!this.cy) return;
        this._fitToViewport();
    }

    relayout(layoutName) {
        if (!this.cy || !this.currentData) return;

        this.clearLayoutCache();

        const layout = layoutName || this.options.layoutName;
        LayoutEngine.applyLayout(this.cy, layout, {
            automatonType: this.automatonType,
            stateCount: this.currentData.states.length
        });

        this._fitToViewport();
    }

    setLayoutFingerprint(fingerprint) {
        this._layoutFingerprint = fingerprint;
    }

    clearLayoutCache() {
        if (this._layoutFingerprint) {
            CanvasLayoutCache.clear(this._layoutFingerprint);
        }
    }

    getCytoscapeInstance() {
        return this.cy || null;
    }

    exportAsImage() {
        if (!this.cy) return null;
        
        return this.cy.png({
            output: 'blob',
            bg: 'white',
            full: true,
            scale: 2
        });
    }

    getData() {
        return this.currentData;
    }

    getCytoscapeInstance() {
        return this.cy;
    }

    enableEditMode() {
        if (!this.editModeManager) {
            console.warn('EditModeManager not initialized');
            return false;
        }
        return this.editModeManager.enableEditMode();
    }

    disableEditMode() {
        if (!this.editModeManager) {
            console.warn('EditModeManager not initialized');
            return false;
        }
        return this.editModeManager.disableEditMode();
    }

    toggleEditMode() {
        if (!this.editModeManager) {
            console.warn('EditModeManager not initialized');
            return false;
        }
        return this.editModeManager.toggleEditMode();
    }

    isEditModeActive() {
        return this.editModeManager ? this.editModeManager.isActive() : false;
    }

    setSimulationState(isSimulating) {
        if (this.editModeManager) {
            this.editModeManager.setSimulationState(isSimulating);
        }
        this._applyDraggingState();
    }

    _onEditModeChange(isEditMode) {
        if (this.stateEditor) {
            if (isEditMode) {
                this.stateEditor.enable();
            } else {
                this.stateEditor.disable();
            }
        }

        if (this.transitionEditor) {
            if (isEditMode) {
                this.transitionEditor.enable();
            } else {
                this.transitionEditor.disable();
            }
        }

        const event = new CustomEvent('canvasEditModeChanged', {
            detail: { isEditMode }
        });
        window.dispatchEvent(event);

        console.log(`Canvas edit mode: ${isEditMode ? 'ENABLED' : 'DISABLED'}`);
        this._applyDraggingState();
    }

    _onStateAdded(state) {
        console.log('State added:', state);

        const event = new CustomEvent('canvasStateAdded', {
            detail: { state }
        });
        window.dispatchEvent(event);
    }

    enableMoving() {
        this.moveEnabled = true;
        this._applyDraggingState();
    }

    disableMoving() {
        this.moveEnabled = false;
        this._applyDraggingState();
    }

    toggleMoving() {
        this.moveEnabled = !this.moveEnabled;
        this._applyDraggingState();
        return this.moveEnabled;
    }

    isMovingActive() {
        return !!this.moveEnabled;
    }

    enableWheelZoom() {
        if (this.interactionHandler) {
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

    disableWheelZoom() {
        if (this.interactionHandler) {
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

    _applyDraggingState() {
        if (!this.cy) return;

        const shouldAllowDrag = !!this.moveEnabled;

        if (shouldAllowDrag) {
            this.cy.nodes().forEach(n => n.grabify());
        } else {
            this.cy.nodes().forEach(n => n.ungrabify());
        }
    }

    undo() {
        return this.actionHistory ? this.actionHistory.undo() : false;
    }

    redo() {
        return this.actionHistory ? this.actionHistory.redo() : false;
    }

    getHistoryStatus() {
        return this.actionHistory ? this.actionHistory.getStatus() : { canUndo: false, canRedo: false };
    }

    _onTransitionAdded(transition) {
        window.dispatchEvent(new CustomEvent('canvasTransitionAdded', { detail: { transition } }));
    }

    _onTransitionDeleted(transition) {
        window.dispatchEvent(new CustomEvent('canvasTransitionDeleted', { detail: { transition } }));
    }

    _onTransitionModified(transition) {
        window.dispatchEvent(new CustomEvent('canvasTransitionModified', { detail: { transition } }));
    }

    _onStateDeleted(state) {
        console.log('State deleted:', state);

        const event = new CustomEvent('canvasStateDeleted', {
            detail: { state }
        });
        window.dispatchEvent(event);
    }

    _onStateModified(state) {
        console.log('State modified:', state);

        const event = new CustomEvent('canvasStateModified', {
            detail: { state }
        });
        window.dispatchEvent(event);
    }

    // ─────────────────────────────────────────────
    // Panel → Canvas bridge methods
    // ─────────────────────────────────────────────

    addStateFromPanel(options = {}) {
        if (!this.stateEditor) return null;
        // Choose a position that doesn't overlap existing nodes
        const pos = this._findFreePosition();
        const node = this.stateEditor.addState(pos.x, pos.y);
        if (!node) return null;

        // Apply optional flags
        if (options.isStart) this.stateEditor.toggleStartState(node);
        if (options.isAccepting) this.stateEditor.toggleAcceptingState(node);

        return node;
    }

    deleteStateFromPanel(stateId) {
        if (!this.stateEditor) return;
        const nodeId = `state-${stateId}`;
        this.stateEditor.deleteState(nodeId);
    }

    modifyStateFromPanel(stateId, prop, value) {
        if (!this.stateEditor || !this.cy) return;
        const node = this.cy.getElementById(`state-${stateId}`);
        if (!node || !node.length) {
            console.warn(`modifyStateFromPanel: state ${stateId} not found`);
            return;
        }
        if (prop === 'isStart') {
            const isCurrentlyStart = node.hasClass('start');
            if (isCurrentlyStart !== value) {
                this.stateEditor.toggleStartState(node);
            }
        } else if (prop === 'isAccepting') {
            const isCurrentlyAccepting = node.hasClass('accepting');
            if (isCurrentlyAccepting !== value) {
                this.stateEditor.toggleAcceptingState(node);
            }
        }
    }

    addTransitionFromPanel(transition) {
        if (!this.transitionEditor || !this.cy) return;

        const sourceNode = this.cy.getElementById(`state-${transition.fromStateId}`);
        const targetNode = this.cy.getElementById(`state-${transition.toStateId}`);

        if (!sourceNode?.length || !targetNode?.length) {
            console.warn('addTransitionFromPanel: source or target state not found', transition);
            return;
        }

        const symbol   = transition.symbol ?? '\0';
        const stackPop  = transition.stackPop  ?? null;
        const stackPush = transition.stackPush ?? null;
        const isPDA = ['PDA', 'DPDA', 'NPDA'].includes(this.automatonType);

        this.transitionEditor.addTransitionDirect(
            sourceNode,
            targetNode,
            symbol,
            isPDA ? stackPop : null,
            isPDA ? stackPush : null
        );
    }

    deleteTransitionFromPanel(transition) {
        if (!this.stateEditor || !this.cy) return;

        const getPdaSymbolFromLine = (line) => {
            const groupedMatch = line.match(/^(.+?)\s*\(\s*(.+?)\s*\/\s*(.*?)\s*\)$/);
            if (groupedMatch) return groupedMatch[1].trim();
            const legacyMatch = line.match(/^(.+?),\s*(.+?)\s*\/\s*(.*)$/);
            if (legacyMatch) return legacyMatch[1].trim();
            return line.trim();
        };

        const edges = this.cy.edges().filter(e => {
            if (e.source().data('stateId') !== transition.fromStateId) return false;
            if (e.target().data('stateId') !== transition.toStateId)   return false;
            // For multi-symbol edges we match by symbol in the label
            const label = e.data('label') ?? '';
            const sym   = transition.symbol ?? '';
            const normalSym = (sym === '\0' || sym === 'ε' || sym === '\\0') ? 'ε' : sym;
            if (label) {
                const isPDA = this.automatonType === 'PDA' || this.automatonType === 'DPDA' || this.automatonType === 'NPDA';
                if (isPDA) {
                    return label.split('\n').some(l => getPdaSymbolFromLine(l) === normalSym);
                } else {
                    return label.split(', ').some(s => s.trim() === normalSym);
                }
            }
            const edgeSym = e.data('rawSymbol') ?? e.data('symbol') ?? '';
            return String(edgeSym) === String(sym);
        });

        if (!edges.length) {
            console.warn('deleteTransitionFromPanel: edge not found', transition);
            return;
        }

        const edge = edges.first();
        const label = edge.data('label') ?? '';
        const sym = transition.symbol ?? '';
        const normalSym = (sym === '\0' || sym === 'ε' || sym === '\\0') ? 'ε' : sym;
        const isPDA = this.automatonType === 'PDA' || this.automatonType === 'DPDA' || this.automatonType === 'NPDA';

        let newLabels = [];
        let removed = false;

        if (label && label.length > 0) {
            if (isPDA) {
                const lines = label.split('\n').map(l=>l.trim()).filter(Boolean);
                for (const line of lines) {
                    if (!removed && getPdaSymbolFromLine(line) === normalSym) removed = true;
                    else newLabels.push(line);
                }
            } else {
                const syms = label.split(', ').map(s=>s.trim()).filter(Boolean);
                for (const s of syms) {
                    if (!removed && s === normalSym) removed = true;
                    else newLabels.push(s);
                }
            }
        }

        if (newLabels.length > 0 && removed) {
            const oldLabel = label;
            const oldDisplayLabel = edge.data('displayLabel') || this._toDisplayLabel(oldLabel);
            const newLabelStr = newLabels.join(isPDA ? '\n' : ', ');
            edge.data('label', newLabelStr);
            edge.data('displayLabel', this._toDisplayLabel(newLabelStr));
            edge.data('labelSizeHint', this._estimateLabelSizeHint(newLabelStr));

            if (this.actionHistory) {
                this.actionHistory.recordAction({
                    do: () => {
                        edge.data('label', newLabelStr);
                        edge.data('displayLabel', this._toDisplayLabel(newLabelStr));
                        edge.data('labelSizeHint', this._estimateLabelSizeHint(newLabelStr));
                    },
                    undo: () => {
                        edge.data('label', oldLabel);
                        edge.data('displayLabel', oldDisplayLabel);
                        edge.data('labelSizeHint', this._estimateLabelSizeHint(oldLabel));
                    }
                });
            }

            if (this._onTransitionDeleted) {
                this._onTransitionDeleted(transition);
            }
        } else {
            this.stateEditor.deleteTransition(edge.id());
            if (this._onTransitionDeleted) {
                this._onTransitionDeleted(transition);
            }
        }
    }

    _findFreePosition() {
        if (!this.cy || !this.cy.nodes().length) {
            return { x: 150, y: 150 };
        }
        const existingPositions = this.cy.nodes().map(n => n.position());
        const stepX = 120;
        let candidate = { x: 150, y: 150 };
        for (let attempt = 0; attempt < 30; attempt++) {
            const overlaps = existingPositions.some(p =>
                Math.abs(p.x - candidate.x) < 80 && Math.abs(p.y - candidate.y) < 80
            );
            if (!overlaps) return candidate;
            candidate = { x: candidate.x + stepX, y: candidate.y };
            // Wrap to next row after a while
            if (attempt > 0 && attempt % 5 === 0) {
                candidate = { x: 150, y: candidate.y + 120 };
            }
        }
        return candidate;
    }

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

    _setupEventListeners() {
        this.cy.on('mouseover', 'node', (evt) => {
            const node = evt.target;
            const stateId = node.data('stateId');
            const isStart = node.data('isStart');
            const isAccepting = node.data('isAccepting');

            let tooltip = `State q${stateId}`;
            if (isStart) tooltip += ' (Start)';
            if (isAccepting) tooltip += ' (Accepting)';

            node.data('tooltip', tooltip);
        });

        this.cy.on('mouseover', 'edge', (evt) => {
            const edge = evt.target;
            const symbol = edge.data('symbol');
            const isPDA = edge.data('isPDA');

            if (isPDA) {
                const stackPop = edge.data('stackPop') || 'ε';
                const stackPush = edge.data('stackPush') || 'ε';
                edge.data('tooltip', `${symbol} (${stackPop}/${stackPush})`);
            } else {
                edge.data('tooltip', symbol);
            }
        });
    }

    _onNodeFreed() {
        if (!this._layoutFingerprint || !this.cy) return;

        if (this._savePositionsDebounceTimer) {
            clearTimeout(this._savePositionsDebounceTimer);
        }
        this._savePositionsDebounceTimer = setTimeout(() => {
            if (this.cy && this._layoutFingerprint) {
                CanvasLayoutCache.save(this._layoutFingerprint, this.cy);
            }
        }, 600);
    }

    _estimateLabelSizeHint(label) {
        if (!label || typeof label !== 'string') return 8;
        const lines = label.split('\n').map(l => l.trim()).filter(Boolean);
        const longest = lines.length ? Math.max(...lines.map(l => l.length)) : label.length;
        return Math.max(8, Math.min(40, longest));
    }

    _toDisplayLabel(label) {
        return String(label || '').replace(/\n+/g, '   ');
    }

    _fitToViewport() {
        if (!this.cy) return;

        const padding = 50;
 
        if (this.cy.nodes().length > 0) {
            this.cy.fit(this.cy.elements(), padding);
        } else {
            this.cy.reset();
        }
    }

    _handleResize() {
        if (!this.cy) return;

        clearTimeout(this._resizeTimeout);
        this._resizeTimeout = setTimeout(() => {
            this.cy.resize();
            this._fitToViewport();
        }, 250);
    }

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

        setTimeout(() => {
            if (errorDiv.parentNode) {
                errorDiv.remove();
            }
        }, 5000);
    }
}

if (typeof window !== 'undefined') {
    window.AutomatonCanvas = AutomatonCanvas;
}
