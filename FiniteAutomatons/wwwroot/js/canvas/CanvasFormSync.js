/**
 * CanvasFormSync.js
 * Synchronizes the Cytoscape canvas state back to the hidden HTML form inputs.
 *
 * The hidden form inputs are used for server-side form submission (simulation, minimization etc.)
 * When the user edits the automaton on the canvas, this class ensures those inputs
 * are kept up-to-date so the server sees the correct automaton.
 *
 * Usage:
 *   const sync = new CanvasFormSync({ formId: 'automatonForm', automatonType: 'DFA' });
 *   sync.syncAll(cy); // call after any edit
 *
 * @module CanvasFormSync
 */

export class CanvasFormSync {
    /**
     * @param {Object} options
     * @param {string} [options.formId='automatonForm'] - The form element ID
     * @param {string} [options.automatonType='DFA'] - Current automaton type
     */
    constructor(options = {}) {
        this.formId = options.formId || 'automatonForm';
        this.automatonType = options.automatonType || 'DFA';

        // Attribute used to mark dynamically-added inputs (for cleanup)
        this.DYNAMIC_ATTR = 'data-canvas-sync';

        this._form = null; // Lazy-initialized
    }

    /**
     * Get (or lazily initialize) the form element reference
     * @returns {HTMLFormElement|null}
     * @private
     */
    _getForm() {
        if (!this._form) {
            this._form = document.getElementById(this.formId);
        }
        return this._form;
    }

    /**
     * Update the automaton type (affects PDA field handling)
     * @param {string} type
     */
    setAutomatonType(type) {
        this.automatonType = type;
    }

    /**
     * Synchronize all form inputs from current Cytoscape graph state.
     * Should be called after every edit operation (add/delete/modify state or transition).
     *
     * @param {Object} cy - Cytoscape instance
     */
    syncAll(cy) {
        if (!cy) return;

        const form = this._getForm();
        if (!form) {
            console.warn('CanvasFormSync: form not found:', this.formId);
            return;
        }

        // Remove previously dynamically generated inputs
        this._clearDynamic(form);

        // Get current graph data
        const nodes = cy.nodes().filter(n => !n.hasClass('dummy'));
        const edges = cy.edges();

        // Build state inputs
        this._buildStateInputs(form, nodes);

        // Build transition inputs
        this._buildTransitionInputs(form, edges);

        console.log(`CanvasFormSync: synced ${nodes.length} states and ${edges.length} edges`);
    }

    /**
     * Sync only state data (for performance when only states changed)
     * @param {Object} cy - Cytoscape instance
     */
    syncStates(cy) {
        if (!cy) return;
        const form = this._getForm();
        if (!form) return;

        this._clearDynamicByType(form, 'states');
        const nodes = cy.nodes().filter(n => !n.hasClass('dummy'));
        this._buildStateInputs(form, nodes);
    }

    /**
     * Sync only transition data (for performance when only transitions changed)
     * @param {Object} cy - Cytoscape instance
     */
    syncTransitions(cy) {
        if (!cy) return;
        const form = this._getForm();
        if (!form) return;

        this._clearDynamicByType(form, 'transitions');
        const edges = cy.edges();
        this._buildTransitionInputs(form, edges);
    }

    // ==================== PRIVATE ====================

    /**
     * Remove all dynamically-added inputs from the form
     * @private
     */
    _clearDynamic(form) {
        const existing = form.querySelectorAll(`[${this.DYNAMIC_ATTR}]`);
        existing.forEach(el => el.remove());
    }

    /**
     * Remove dynamically-added inputs of a specific type
     * @private
     */
    _clearDynamicByType(form, type) {
        const existing = form.querySelectorAll(`[${this.DYNAMIC_ATTR}="${type}"]`);
        existing.forEach(el => el.remove());
    }

    /**
     * Build hidden inputs for states (States[i].Id, States[i].IsStart, States[i].IsAccepting)
     * @private
     */
    _buildStateInputs(form, nodes) {
        const fragment = document.createDocumentFragment();

        // Remove old static state inputs (the ones coming from server-side razor rendering)
        // We target the pattern States[i].* and States.Index
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

    /**
     * Build hidden inputs for transitions
     * Handles multi-symbol edges (newline-separated labels) by expanding them to individual records.
     * @private
     */
    _buildTransitionInputs(form, edges) {
        const fragment = document.createDocumentFragment();
        this._removeStaticInputsByPattern(form, /^Transitions(\[|\.)/, 'transitions');

        let i = 0;
        const isPDA = this.automatonType === 'PDA';

        edges.each((edge) => {
            const fromId = edge.source().data('stateId');
            const toId = edge.target().data('stateId');

            // An edge may have been created from multiple symbol additions
            // In that case, the raw label may contain newlines
            // We need to expand each symbol row separately
            const rawSymbols = this._extractRawSymbols(edge);

            rawSymbols.forEach(sym => {
                fragment.appendChild(this._makeInput(`Transitions.Index`, String(i), 'transitions'));
                fragment.appendChild(this._makeInput(`Transitions[${i}].FromStateId`, String(fromId), 'transitions'));
                fragment.appendChild(this._makeInput(`Transitions[${i}].ToStateId`, String(toId), 'transitions'));

                // Symbol: \0 for epsilon represented as \\0 in form
                const symbolVal = (sym.symbol === '\0' || sym.symbol === 'ε') ? '\\0' : sym.symbol;
                fragment.appendChild(this._makeInput(`Transitions[${i}].Symbol`, symbolVal, 'transitions'));

                if (isPDA) {
                    if (sym.stackPop !== undefined) {
                        const popVal = (sym.stackPop === '\0' || sym.stackPop === 'ε') ? '\\0' : (sym.stackPop || '\\0');
                        fragment.appendChild(this._makeInput(`Transitions[${i}].StackPop`, popVal, 'transitions'));
                    }
                    if (sym.stackPush !== undefined && sym.stackPush !== '') {
                        fragment.appendChild(this._makeInput(`Transitions[${i}].StackPush`, sym.stackPush, 'transitions'));
                    }
                }

                i++;
            });
        });

        form.appendChild(fragment);
    }

    /**
     * Extract an array of symbol records from an edge's data.
     * Handles the case where multiple symbols share the same edge (label has newlines).
     * @private
     * @returns {Array<{symbol: string, stackPop?: string, stackPush?: string}>}
     */
    _extractRawSymbols(edge) {
        const isPDA = edge.data('isPDA') || this.automatonType === 'PDA';
        const label = edge.data('label') || '';
        const symbol = edge.data('symbol');
        const rawSymbol = edge.data('rawSymbol');
        const stackPop = edge.data('stackPop');
        const stackPush = edge.data('stackPush');

        // If edge only has one symbol stored (no multi-symbol), just use it directly
        if (symbol !== undefined) {
            const result = { symbol: this._normalizeSymbol(symbol) };
            if (isPDA) {
                result.stackPop = this._normalizeSymbol(stackPop || '\0');
                result.stackPush = stackPush || '';
            }
            return [result];
        }

        // Fallback: parse label string for symbol. Each line is a separate transition.
        if (!label) return [];

        const lines = label.split('\n').filter(l => l.trim());
        return lines.map(line => {
            if (isPDA) {
                // Format: "symbol, pop/push"
                const match = line.match(/^(.+?),\s*(.+?)\/(.*)$/);
                if (match) {
                    return {
                        symbol: this._normalizeSymbol(match[1].trim()),
                        stackPop: this._normalizeSymbol(match[2].trim()),
                        stackPush: match[3].trim()
                    };
                }
            }
            return { symbol: this._normalizeSymbol(line.trim()) };
        });
    }

    /**
     * Normalize a symbol string to internal representation
     * @private
     */
    _normalizeSymbol(sym) {
        if (!sym || sym === 'ε' || sym === '\\0') return '\0';
        return sym;
    }

    /**
     * Remove existing static (server-rendered) inputs matching a pattern.
     * This prevents duplicates when we add our own.
     * @private
     */
    _removeStaticInputsByPattern(form, namePattern, type) {
        // Only remove inputs that were previously added by us (has our DYNAMIC_ATTR)
        // The server-rendered ones stay until we replace them completely.
        // Actually: we DO replace them — so first remove dynamic, then remove static ones:
        const all = form.querySelectorAll('input[type="hidden"]');
        all.forEach(input => {
            const name = input.getAttribute('name') || '';
            if (namePattern.test(name) && !input.hasAttribute(this.DYNAMIC_ATTR)) {
                // This is a server-rendered input — mark for removal (we'll re-add via canvas)
                input.setAttribute(this.DYNAMIC_ATTR, type);
                input.setAttribute('data-was-static', 'true');
            }
        });
        // Now remove all with our attribute (both static-marked and previously dynamic)
        const toRemove = form.querySelectorAll(`[${this.DYNAMIC_ATTR}="${type}"]`);
        toRemove.forEach(el => el.remove());
    }

    /**
     * Create a hidden input element
     * @private
     */
    _makeInput(name, value, type) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = name;
        input.value = value;
        input.setAttribute(this.DYNAMIC_ATTR, type);
        return input;
    }

    /**
     * Destroy the sync manager (cleanup references)
     */
    destroy() {
        this._form = null;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.CanvasFormSync = CanvasFormSync;
}
