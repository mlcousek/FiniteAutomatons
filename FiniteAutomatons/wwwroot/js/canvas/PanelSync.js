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
        // Timer used for the initial sync scheduled in `init()` so we can cancel it in `destroy()`.
        this._initialTimer = null;
        // Abort controller for the currently in-flight sync request. If a new sync starts
        // we abort the previous request to avoid out-of-order responses / race conditions.
        this._currentAbort = null;
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
        // Also listen for undo/redo being applied so we re-sync panels
        events.push('canvasHistoryApplied');
        events.forEach(evt => window.addEventListener(evt, this._boundSync));

        // Do an initial save shortly after init so the session is populated from
        // the server-rendered canvas even before the user makes any edits.
        this._initialTimer = setTimeout(() => this._sync(), 800);
    }

    /**
     * Stop listening and cancel pending syncs
     */
    destroy() {
        clearTimeout(this._debounceTimer);
        clearTimeout(this._initialTimer);
        // Abort any in-flight sync request
        if (this._currentAbort) {
            try { this._currentAbort.abort(); } catch (e) { /* swallow */ }
            this._currentAbort = null;
        }
        const events = [
            'canvasStateAdded', 'canvasStateDeleted', 'canvasStateModified',
            'canvasTransitionAdded', 'canvasTransitionDeleted', 'canvasTransitionModified'
        ];
        // Keep in sync with init list
        events.push('canvasHistoryApplied');
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
        const body = JSON.stringify(request);

        // Cancel any previous in-flight sync to avoid out-of-order responses.
        if (this._currentAbort) {
            try { this._currentAbort.abort(); } catch (e) { /* ignore */ }
            this._currentAbort = null;
        }

        const controller = new AbortController();
        this._currentAbort = controller;
        // Safety timeout to avoid hanging requests
        const abortAfterMs = 10000;
        const abortTimer = setTimeout(() => controller.abort(), abortAfterMs);

        try {
            const resp = await fetch(this.syncUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body,
                signal: controller.signal
            });

            if (!resp.ok) {
                console.warn('PanelSync: sync API error', resp.status);
                return;
            }

            const data = await resp.json();
            this._updatePanels(data);

            // Fire-and-forget: persist canvas state to server session so page reloads restore it
            // Use a separate fetch so it isn't tied to the main controller.
            fetch('/api/canvas/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body
            }).catch(e => console.warn('PanelSync: save failed', e));

            clearTimeout(abortTimer);
            this._currentAbort = null;

        } catch (e) {
            if (e.name === 'AbortError') {
                // Aborts are expected when a newer sync supersedes an older one.
                console.debug('PanelSync: sync aborted');
            } else {
                console.warn('PanelSync: fetch failed', e);
            }
            clearTimeout(abortTimer);
            this._currentAbort = null;
        }
    }

    /**
     * Serialize the current canvas state to a CanvasSyncRequest object.
     * Expands multi-symbol edges into individual transition records so the
     * server receives one entry per symbol (same as CanvasFormSync does for form inputs).
     * @private
     */
    _buildRequest(canvas, cy) {
        const type = canvas.automatonType || 'DFA';
        const isPDA = (type === 'PDA');

        const states = cy.nodes()
            .filter(n => !n.hasClass('dummy'))
            .map(n => ({
                id: n.data('stateId'),
                isStart: n.hasClass('start'),
                isAccepting: n.hasClass('accepting')
            }));

        const transitions = [];

        cy.edges().forEach(e => {
            const fromId = e.source().data('stateId');
            const toId   = e.target().data('stateId');
            const edgePDA = e.data('isPDA') || isPDA;

            // Always derive transitions from the label — it is the canonical source of truth
            // for what is displayed on the edge, and is updated whenever symbols are merged.
            // rawSymbol/symbol edge-data fields are only the *first* symbol and must not be
            // used as the sole source when multiple symbols share an edge.
            const label = e.data('label');
            if (label) {
                if (edgePDA) {
                    // PDA: each line is one entry "sym, pop/push"
                    const lines = label.split('\n').map(l => l.trim()).filter(Boolean);
                    for (const line of lines) {
                        const m = line.match(/^(.+?),\s*(.+?)\/(.*)$/);
                        if (m) {
                            transitions.push({
                                fromStateId: fromId, toStateId: toId,
                                symbol:    this._normalizeSymbol(m[1].trim()),
                                stackPop:  this._normalizeSymbol(m[2].trim()),
                                stackPush: m[3].trim()
                            });
                        } else {
                            transitions.push({
                                fromStateId: fromId, toStateId: toId,
                                symbol: this._normalizeSymbol(line), stackPop: '\\0', stackPush: ''
                            });
                        }
                    }
                } else {
                    // DFA/NFA/ε-NFA: symbols separated by ', '
                    const syms = label.split(', ').map(s => s.trim()).filter(Boolean);
                    for (const sym of syms) {
                        transitions.push({
                            fromStateId: fromId, toStateId: toId,
                            symbol: this._normalizeSymbol(sym)
                        });
                    }
                }
                return;
            }

            // Fallback: label is empty — read from structured edge data fields
            const rawSymbol = e.data('rawSymbol');
            const symbol    = e.data('symbol');
            const sym = rawSymbol ?? symbol ?? '';
            if (!sym) return;
            const normalized = (sym === '\0' || sym === 'ε' || sym === '\\0') ? '\\0' : sym;
            const entry = { fromStateId: fromId, toStateId: toId, symbol: normalized };
            if (edgePDA) {
                const sp  = e.data('stackPop');
                const spu = e.data('stackPush');
                entry.stackPop  = (sp === '\0' || sp === 'ε') ? '\\0' : (sp ?? '\\0');
                entry.stackPush = spu ?? '';
            }
            transitions.push(entry);
        });

        return { type, states, transitions };
    }

    /**
     * Normalize a display symbol to the internal wire format.
     * @private
     */
    _normalizeSymbol(sym) {
        if (!sym || sym === 'ε' || sym === '\\0' || sym === '\0') return '\\0';
        return sym;
    }

    /**
     * Update all left-panel sections with data from the sync response
     * @private
     */
    _updatePanels(data) {
        this._updateAlphabet(data);
        this._updateStates(data);
        this._updateTransitions(data);
        // Update minimize button if server provided minimization analysis (JSON uses camelCase)
        if (data?.minimizationAnalysis) {
            this._updateMinimizeButton(data.minimizationAnalysis);
        }
    }

    /**
     * Update the minimize UI based on analysis DTO from server
     * @private
     */
    _updateMinimizeButton(analysis) {
        try {
            const section = document.querySelector('.minimize-section');
            if (!section) return;
            const btn = section.querySelector('.minimize-btn');
            if (!btn) return;

            // analysis received from server is camelCase (supportsMinimization, isMinimal, minimizedStateCount,...)
            const supports = !!analysis.supportsMinimization;
            const isMinimal = !!analysis.isMinimal;
            const minimizedCount = analysis.minimizedStateCount ?? null;
            const originalCount = analysis.originalStateCount ?? null;

            // If minimization unsupported, hide or disable the section
            if (!supports) {
                btn.textContent = 'Minimalize (Unsupported)';
                btn.disabled = true;
                btn.type = 'button';
                btn.removeAttribute('formaction');
                btn.removeAttribute('formmethod');
                return;
            }

            const executionStarted = document.querySelector('input[name="HasExecuted"]')?.value === 'true' || Number(document.querySelector('input[name="Position"]')?.value ?? 0) > 0;

            if (isMinimal) {
                btn.textContent = 'Minimalize (Already Minimal)';
                btn.disabled = true;
                btn.type = 'button';
                btn.removeAttribute('formaction');
                btn.removeAttribute('formmethod');
            } else if (executionStarted) {
                btn.textContent = 'Minimalize (Disabled - Execution Started)';
                btn.disabled = true;
                btn.type = 'button';
                btn.removeAttribute('formaction');
                btn.removeAttribute('formmethod');
            } else {
                // Enable button and update text to show target size
                btn.textContent = `Minimalize (→ ${minimizedCount})`;
                btn.disabled = false;
                btn.type = 'submit';
                btn.setAttribute('formaction', '/AutomatonExecution/Minimalize');
                btn.setAttribute('formmethod', 'post');
                btn.title = 'Minimalize DFA';
            }
        } catch (e) {
            // swallow - non-critical UI update
            console.warn('PanelSync: failed to update minimize button', e);
        }
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
