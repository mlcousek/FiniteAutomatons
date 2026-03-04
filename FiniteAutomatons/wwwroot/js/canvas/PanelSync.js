export class PanelSync {
 
    constructor(options = {}) {
        this.syncUrl = options.syncUrl ?? '/api/canvas/sync';
        this.debounceMs = options.debounceMs ?? 250;
        this.getCanvasInstance = options.getCanvasInstance;

        this._debounceTimer = null;
        this._initialTimer = null;
        this._currentAbort = null;
        this._boundSync = this._debouncedSync.bind(this);

        this._els = {};
    }

    init() {
        const events = [
            'canvasStateAdded', 'canvasStateDeleted', 'canvasStateModified',
            'canvasTransitionAdded', 'canvasTransitionDeleted', 'canvasTransitionModified'
        ];
        events.push('canvasHistoryApplied');
        events.forEach(evt => window.addEventListener(evt, this._boundSync));

        this._initialTimer = setTimeout(() => this._sync(), 800);
    }

    destroy() {
        clearTimeout(this._debounceTimer);
        clearTimeout(this._initialTimer);
        if (this._currentAbort) {
            try { this._currentAbort.abort(); } catch (e) { /* swallow */ }
            this._currentAbort = null;
        }
        const events = [
            'canvasStateAdded', 'canvasStateDeleted', 'canvasStateModified',
            'canvasTransitionAdded', 'canvasTransitionDeleted', 'canvasTransitionModified'
        ];
        events.push('canvasHistoryApplied');
        events.forEach(evt => window.removeEventListener(evt, this._boundSync));
    }

    _debouncedSync() {
        clearTimeout(this._debounceTimer);
        this._debounceTimer = setTimeout(() => this._sync(), this.debounceMs);
    }

    async _sync() {
        const canvas = this.getCanvasInstance?.();
        if (!canvas) return;

        const cy = canvas.cy;
        if (!cy) return;

        const request = this._buildRequest(canvas, cy);
        const body = JSON.stringify(request);

        if (this._currentAbort) {
            try { this._currentAbort.abort(); } catch (e) { /* ignore */ }
            this._currentAbort = null;
        }

        const controller = new AbortController();
        this._currentAbort = controller;
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

            fetch('/api/canvas/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body
            }).catch(e => console.warn('PanelSync: save failed', e));

            clearTimeout(abortTimer);
            this._currentAbort = null;

        } catch (e) {
            if (e.name === 'AbortError') {
                console.debug('PanelSync: sync aborted');
            } else {
                console.warn('PanelSync: fetch failed', e);
            }
            clearTimeout(abortTimer);
            this._currentAbort = null;
        }
    }

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

            const label = e.data('label');
            if (label) {
                if (edgePDA) {
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

    _normalizeSymbol(sym) {
        if (!sym || sym === 'ε' || sym === '\\0' || sym === '\0') return '\\0';
        return sym;
    }

    _updatePanels(data) {
        this._updateAlphabet(data);
        this._updateStates(data);
        this._updateTransitions(data);
        if (data?.minimizationAnalysis) {
            this._updateMinimizeButton(data.minimizationAnalysis);
        }
    }

    _updateMinimizeButton(analysis) {
        try {
            const section = document.querySelector('.minimize-section');
            if (!section) return;
            const btn = section.querySelector('.minimize-btn');
            if (!btn) return;

            const supports = !!analysis.supportsMinimization;
            const isMinimal = !!analysis.isMinimal;
            const minimizedCount = analysis.minimizedStateCount ?? null;
            const originalCount = analysis.originalStateCount ?? null;

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
                btn.textContent = `Minimalize (→ ${minimizedCount})`;
                btn.disabled = false;
                btn.type = 'submit';
                btn.setAttribute('formaction', '/AutomatonExecution/Minimalize');
                btn.setAttribute('formmethod', 'post');
                btn.title = 'Minimalize DFA';
            }
        } catch (e) {
            console.warn('PanelSync: failed to update minimize button', e);
        }
    }

    _updateAlphabet(data) {
        const el = document.getElementById('panel-alphabet');
        if (!el) return;

        if (!data.alphabet || data.alphabet.length === 0) {
            el.innerHTML = `<span class="placeholder-text">No symbols defined</span>`;
            return;
        }

        const symbols = [...data.alphabet];
        if (data.hasEpsilonTransitions) symbols.push('ε');

        el.innerHTML = `<span class="alphabet-symbols">{ ${symbols.join(', ')} }</span>`;
    }

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

if (typeof window !== 'undefined') {
    window.PanelSync = PanelSync;
}
