export class CanvasFormSync {
    constructor(options = {}) {
        this.formId = options.formId || 'automatonForm';
        this.automatonType = options.automatonType || 'DFA';

        this.DYNAMIC_ATTR = 'data-canvas-sync';

        this._form = null; 
    }

    _getForm() {
        if (!this._form) {
            this._form = document.getElementById(this.formId);
        }
        return this._form;
    }

    setAutomatonType(type) {
        this.automatonType = type;
    }

    syncAll(cy) {
        if (!cy) return;

        const form = this._getForm();
        if (!form) {
            console.warn('CanvasFormSync: form not found:', this.formId);
            return;
        }

        this._clearDynamic(form);

        const nodes = cy.nodes().filter(n => !n.hasClass('dummy'));
        const edges = cy.edges();

        this._buildStateInputs(form, nodes);

        this._buildTransitionInputs(form, edges);

        console.log(`CanvasFormSync: synced ${nodes.length} states and ${edges.length} edges`);
    }

    syncStates(cy) {
        if (!cy) return;
        const form = this._getForm();
        if (!form) return;

        this._clearDynamicByType(form, 'states');
        const nodes = cy.nodes().filter(n => !n.hasClass('dummy'));
        this._buildStateInputs(form, nodes);
    }

    syncTransitions(cy) {
        if (!cy) return;
        const form = this._getForm();
        if (!form) return;

        this._clearDynamicByType(form, 'transitions');
        const edges = cy.edges();
        this._buildTransitionInputs(form, edges);
    }

    _clearDynamic(form) {
        const existing = form.querySelectorAll(`[${this.DYNAMIC_ATTR}]`);
        existing.forEach(el => el.remove());
    }

    _clearDynamicByType(form, type) {
        const existing = form.querySelectorAll(`[${this.DYNAMIC_ATTR}="${type}"]`);
        existing.forEach(el => el.remove());
    }

    _buildStateInputs(form, nodes) {
        const fragment = document.createDocumentFragment();

        this._removeStaticInputsByPattern(form, /^States(\[|\.)/, 'states');

        nodes.each((node, i) => {
            const stateId = node.data('stateId');
            const isStart = node.hasClass('start');
            const isAccepting = node.hasClass('accepting');

            fragment.appendChild(this._makeInput(`States.Index`, String(i), 'states'));
            fragment.appendChild(this._makeInput(`States[${i}].Id`, String(stateId), 'states'));
            fragment.appendChild(this._makeInput(`States[${i}].IsStart`, String(isStart).toLowerCase(), 'states'));
            fragment.appendChild(this._makeInput(`States[${i}].IsAccepting`, String(isAccepting).toLowerCase(), 'states'));
        });

        form.appendChild(fragment);
    }

    _buildTransitionInputs(form, edges) {
        const fragment = document.createDocumentFragment();
        this._removeStaticInputsByPattern(form, /^Transitions(\[|\.)/, 'transitions');

        let i = 0;
        const isPDA = this.automatonType === 'PDA' || this.automatonType === 'DPDA' || this.automatonType === 'NPDA';

        edges.each((edge) => {
            const fromId = edge.source().data('stateId');
            const toId = edge.target().data('stateId');

            const rawSymbols = this._extractRawSymbols(edge);

            rawSymbols.forEach(sym => {
                fragment.appendChild(this._makeInput(`Transitions.Index`, String(i), 'transitions'));
                fragment.appendChild(this._makeInput(`Transitions[${i}].FromStateId`, String(fromId), 'transitions'));
                fragment.appendChild(this._makeInput(`Transitions[${i}].ToStateId`, String(toId), 'transitions'));

                const symbolVal = (sym.symbol === '\0' || sym.symbol === 'ε') ? '\\0' : sym.symbol;
                fragment.appendChild(this._makeInput(`Transitions[${i}].Symbol`, symbolVal, 'transitions'));

                if (isPDA) {
                    const normalizedPop = (sym.stackPop === undefined) ? '\0' : sym.stackPop;
                    const popVal = (normalizedPop === '\0' || normalizedPop === 'ε') ? '\\0' : (normalizedPop || '\\0');
                    fragment.appendChild(this._makeInput(`Transitions[${i}].StackPop`, popVal, 'transitions'));

                    const pushVal = (sym.stackPush === '\0' || sym.stackPush === 'ε') ? '' : (sym.stackPush ?? '');
                    if (pushVal !== '') {
                        fragment.appendChild(this._makeInput(`Transitions[${i}].StackPush`, pushVal, 'transitions'));
                    }
                }

                i++;
            });
        });

        form.appendChild(fragment);
    }

    _extractRawSymbols(edge) {
        const isPDA = edge.data('isPDA') || this.automatonType === 'PDA' || this.automatonType === 'DPDA' || this.automatonType === 'NPDA';
        const label = edge.data('label') || '';

        if (!label) {
            const symbol = edge.data('symbol');
            if (symbol !== undefined) {
                const result = { symbol: this._normalizeSymbol(symbol) };
                if (isPDA) {
                    result.stackPop = this._normalizeSymbol(edge.data('stackPop') || '\0');
                    result.stackPush = edge.data('stackPush') || '';
                }
                return [result];
            }
            return [];
        }

        if (isPDA) {
            const lines = label.split('\n').filter(l => l.trim());
            return lines.map(line => {
                const groupedMatch = line.match(/^(.+?)\s*\(\s*(.+?)\s*\/\s*(.*?)\s*\)$/);
                const legacyMatch = line.match(/^(.+?),\s*(.+?)\s*\/\s*(.*)$/);
                const match = groupedMatch || legacyMatch;
                if (match) {
                    const push = match[3].trim();
                    return {
                        symbol: this._normalizeSymbol(match[1].trim()),
                        stackPop: this._normalizeSymbol(match[2].trim()),
                        stackPush: (push === 'ε' || push === '\\0') ? '' : push
                    };
                }

                // Fallback for unexpected label formats: keep explicit PDA fields.
                return {
                    symbol: this._normalizeSymbol(line.trim()),
                    stackPop: this._normalizeSymbol(edge.data('stackPop') || '\0'),
                    stackPush: edge.data('stackPush') || ''
                };
            });
        } else {
            const symbols = label.split(', ').map(s => s.trim()).filter(s => s);
            return symbols.map(sym => ({ symbol: this._normalizeSymbol(sym) }));
        }
    }

    _normalizeSymbol(sym) {
        if (!sym || sym === 'ε' || sym === '\\0') return '\0';
        return sym;
    }

    _removeStaticInputsByPattern(form, namePattern, type) {

        const all = form.querySelectorAll('input[type="hidden"]');
        all.forEach(input => {
            const name = input.getAttribute('name') || '';
            if (namePattern.test(name) && !input.hasAttribute(this.DYNAMIC_ATTR)) {
                input.setAttribute(this.DYNAMIC_ATTR, type);
                input.setAttribute('data-was-static', 'true');
            }
        });
        const toRemove = form.querySelectorAll(`[${this.DYNAMIC_ATTR}="${type}"]`);
        toRemove.forEach(el => el.remove());
    }

    _makeInput(name, value, type) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = name;
        input.value = value;
        input.setAttribute(this.DYNAMIC_ATTR, type);
        return input;
    }

    destroy() {
        this._form = null;
    }
}

if (typeof window !== 'undefined') {
    window.CanvasFormSync = CanvasFormSync;
}
