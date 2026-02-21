/**
 * PanelSync.js
 * Listens to canvas edit events and updates the left-side info panels in real time
 * by calling POST /api/canvas/sync with the current graph state.
 *
 * No page reload required — the DOM is updated surgically.
 *
 * @module PanelSync
 */

export class PanelSync {
    /**
     * @param {Object} options
     * @param {string} [options.syncUrl='/api/canvas/sync'] - API endpoint
     * @param {number} [options.debounceMs=250] - Debounce delay for rapid edits
     * @param {Function} options.getCanvasInstance - Returns the AutomatonCanvas instance
     */
    constructor(options = {}) {
        this.syncUrl = options.syncUrl ?? '/api/canvas/sync';
        this.debounceMs = options.debounceMs ?? 250;
        this.getCanvasInstance = options.getCanvasInstance;

        this._debounceTimer = null;
        this._pendingSync = false;
        this._boundSync = this._debouncedSync.bind(this);

        // Element cache (populated lazily)
        this._els = {};
    }

    /**
     * Start listening to canvas events
     */
    init() {
        const events = [
            'canvasStateAdded', 'canvasStateDeleted', 'canvasStateModified',
            'canvasTransitionAdded', 'canvasTransitionDeleted', 'canvasTransitionModified'
        ];
        events.forEach(evt => window.addEventListener(evt, this._boundSync));
    }

    /**
     * Stop listening and cancel pending syncs
     */
    destroy() {
        clearTimeout(this._debounceTimer);
        const events = [
            'canvasStateAdded', 'canvasStateDeleted', 'canvasStateModified',
            'canvasTransitionAdded', 'canvasTransitionDeleted', 'canvasTransitionModified'
        ];
        events.forEach(evt => window.removeEventListener(evt, this._boundSync));
    }

    // ──────────────────────────────────────────────────────────────────
    // Private
    // ──────────────────────────────────────────────────────────────────

    _debouncedSync() {
        clearTimeout(this._debounceTimer);
        this._debounceTimer = setTimeout(() => this._sync(), this.debounceMs);
    }

    async _sync() {
        const canvas = this.getCanvasInstance?.();
        if (!canvas) return;

        // canvas.cy is the public Cytoscape instance (set in AutomatonCanvas.js)
        const cy = canvas.cy;
        if (!cy) return;

        // Build request payload from current Cytoscape graph
        const request = this._buildRequest(canvas, cy);

        try {
            const resp = await fetch(this.syncUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(request)
            });

            if (!resp.ok) {
                console.warn('PanelSync: API error', resp.status);
                return;
            }

            const data = await resp.json();
            this._updatePanels(data);
        } catch (e) {
            console.warn('PanelSync: fetch failed', e);
        }
    }

    /**
     * Serialize the current canvas state to a CanvasSyncRequest object
     * @private
     */
    _buildRequest(canvas, cy) {
        const type = canvas.automatonType || 'DFA';

        const states = cy.nodes()
            .filter(n => !n.hasClass('dummy'))
            .map(n => ({
                id: n.data('stateId'),
                isStart: n.hasClass('start'),
                isAccepting: n.hasClass('accepting')
            })).flat();

        const transitions = cy.edges().map(e => {
            const sym = e.data('rawSymbol') ?? e.data('symbol') ?? '';
            const isPDA = e.data('isPDA') || type === 'PDA';
            const entry = {
                fromStateId: e.source().data('stateId'),
                toStateId: e.target().data('stateId'),
                symbol: sym === '\0' || sym === 'ε' ? '\\0' : sym
            };
            if (isPDA) {
                const sp = e.data('stackPop');
                const spu = e.data('stackPush');
                entry.stackPop = (sp === '\0' || sp === 'ε') ? '\\0' : (sp ?? '\\0');
                entry.stackPush = spu ?? '';
            }
            return entry;
        }).flat();

        return { type, states, transitions };
    }

    /**
     * Update all left-panel sections with data from the sync response
     * @private
     */
    _updatePanels(data) {
        this._updateAlphabet(data);
        this._updateStates(data);
        this._updateTransitions(data);
    }

    /**
     * Update the Alphabet panel
     * @private
     */
    _updateAlphabet(data) {
        const el = document.getElementById('panel-alphabet');
        if (!el) return;

        if (!data.alphabet || data.alphabet.length === 0) {
            el.innerHTML = `<span class="placeholder-text">No symbols defined</span>`;
            return;
        }

        // Include ε if there are epsilon transitions
        const symbols = [...data.alphabet];
        if (data.hasEpsilonTransitions) symbols.push('ε');

        el.innerHTML = `<span class="alphabet-symbols">{ ${symbols.join(', ')} }</span>`;
    }

    /**
     * Update the States panel
     * @private
     */
    _updateStates(data) {
        const el = document.getElementById('panel-states-list');
        if (!el) return;

        if (!data.states || data.states.length === 0) {
            el.innerHTML = `<span class="placeholder-text">No states defined</span>`;
            return;
        }

        const items = data.states.map(s => {
            const badges = [
                s.isStart ? `<span class="state-badge badge-start">Start</span>` : '',
                s.isAccepting ? `<span class="state-badge badge-accepting">Accepting</span>` : ''
            ].join('');
            return `<li class="state-item" data-state-id="${s.id}">
                      <span class="state-id">q${s.id}</span>${badges}
                    </li>`;
        });

        el.innerHTML = `<ul class="states-list">${items.join('')}</ul>`;
    }

    /**
     * Update the Transitions panel
     * @private
     */
    _updateTransitions(data) {
        const el = document.getElementById('panel-transitions-list');
        if (!el) return;

        if (!data.transitions || data.transitions.length === 0) {
            el.innerHTML = `<span class="placeholder-text">No transitions defined</span>`;
            return;
        }

        const items = data.transitions.map(t => {
            let text;
            if (t.isPDA) {
                const pop = t.stackPopDisplay ?? 'ε';
                const push = t.stackPush ? t.stackPush : '-';
                text = `q${t.fromStateId} \u2014 ${t.symbolDisplay}, ${pop}/${push} \u2192 q${t.toStateId}`;
            } else {
                text = `q${t.fromStateId} \u2014 ${t.symbolDisplay} \u2192 q${t.toStateId}`;
            }
            return `<li class="transition-item" data-from="${t.fromStateId}" data-to="${t.toStateId}">
                      <span class="transition-text">${text}</span>
                    </li>`;
        });

        el.innerHTML = `<ul class="transitions-list">${items.join('')}</ul>`;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.PanelSync = PanelSync;
}
