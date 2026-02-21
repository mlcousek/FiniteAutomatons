/**
 * TransitionDialog.js
 * Custom HTML modal dialog for entering transition symbols and stack operations.
 * Replaces native window.prompt/confirm for a professional UX.
 *
 * Supports:
 *   - DFA/NFA/ε-NFA: single symbol input with ε button
 *   - PDA: symbol + stack pop + stack push inputs
 *
 * @module TransitionDialog
 */

export class TransitionDialog {
    /**
     * @param {HTMLElement} containerEl - The canvas container element (for positioning reference)
     */
    constructor(containerEl) {
        this.containerEl = containerEl;
        this._dialog = null;
        this._backdrop = null;
        this._activeResolve = null;
        this._keyHandler = null;
    }

    /**
     * Show the dialog for creating a new transition
     * @param {Object} sourceNode - Cytoscape source node
     * @param {Object} targetNode - Cytoscape target node
     * @param {string} automatonType - 'DFA' | 'NFA' | 'EpsilonNFA' | 'PDA'
     * @returns {Promise<Object|null>} Resolves with { symbol, stackPop?, stackPush? } or null if cancelled
     */
    show(sourceNode, targetNode, automatonType) {
        const title = `Add Transition`;
        const subtitle = `${sourceNode.data('label')} → ${targetNode.data('label')}`;
        const isPDA = automatonType === 'PDA';
        const allowEpsilon = automatonType !== 'DFA';

        return this._showDialog({ title, subtitle, isPDA, allowEpsilon, initialValues: {} });
    }

    /**
     * Show the dialog for editing an existing edge
     * @param {Object} edge - Cytoscape edge
     * @param {string} automatonType - automaton type string
     * @returns {Promise<Object|null>} Resolves with updated values or null if cancelled
     */
    showEdit(edge, automatonType) {
        const sourceLabel = edge.source().data('label');
        const targetLabel = edge.target().data('label');
        const title = `Edit Transition`;
        const subtitle = `${sourceLabel} → ${targetLabel}`;
        const isPDA = automatonType === 'PDA';
        const allowEpsilon = automatonType !== 'DFA';

        const initialValues = {
            symbol: edge.data('rawSymbol') || edge.data('label') || '',
            stackPop: edge.data('stackPop') || '',
            stackPush: edge.data('stackPush') || ''
        };

        return this._showDialog({ title, subtitle, isPDA, allowEpsilon, initialValues });
    }

    /**
     * Show a simple property dialog for a node (start/accepting toggle)
     * @param {Object} node - Cytoscape node
     * @returns {Promise<string|null>} Resolves with 'start' | 'accepting' | null
     */
    showNodeProperties(node) {
        const label = node.data('label');
        const isStart = node.hasClass('start');
        const isAccepting = node.hasClass('accepting');

        return new Promise((resolve) => {
            const { dialog, backdrop } = this._createBackdrop();

            dialog.innerHTML = `
                <div class="cd-header">
                    <div class="cd-icon"><i class="fas fa-cog"></i></div>
                    <div>
                        <h3 class="cd-title">State Properties</h3>
                        <p class="cd-subtitle">${label}</p>
                    </div>
                    <button class="cd-close" type="button" aria-label="Close"><i class="fas fa-times"></i></button>
                </div>
                <div class="cd-body">
                    <p class="cd-state-info">
                        <span class="cd-badge ${isStart ? 'cd-badge-active' : 'cd-badge-inactive'}">
                            <i class="fas fa-play"></i> Start
                        </span>
                        <span class="cd-badge ${isAccepting ? 'cd-badge-active' : 'cd-badge-inactive'}">
                            <i class="fas fa-check-circle"></i> Accepting
                        </span>
                    </p>
                    <div class="cd-prop-buttons">
                        <button class="cd-prop-btn ${isStart ? 'cd-prop-btn-danger' : 'cd-prop-btn-primary'}" 
                                id="cdToggleStart" type="button">
                            <i class="fas fa-${isStart ? 'minus' : 'plus'}-circle"></i>
                            ${isStart ? 'Remove' : 'Set as'} Start State
                        </button>
                        <button class="cd-prop-btn ${isAccepting ? 'cd-prop-btn-danger' : 'cd-prop-btn-success'}" 
                                id="cdToggleAccepting" type="button">
                            <i class="fas fa-${isAccepting ? 'minus' : 'plus'}-circle"></i>
                            ${isAccepting ? 'Remove' : 'Set as'} Accepting State
                        </button>
                    </div>
                </div>
                <div class="cd-footer">
                    <button class="cd-btn cd-btn-secondary" id="cdCancel" type="button">Cancel</button>
                </div>
            `;

            this._attachKeyHandler((key) => {
                if (key === 'Escape') { cleanup(); resolve(null); }
            });

            const cleanup = () => {
                this._detachKeyHandler();
                this._removeDialog(dialog, backdrop);
            };

            dialog.querySelector('#cdToggleStart').addEventListener('click', () => {
                cleanup(); resolve('start');
            });
            dialog.querySelector('#cdToggleAccepting').addEventListener('click', () => {
                cleanup(); resolve('accepting');
            });
            dialog.querySelector('#cdCancel').addEventListener('click', () => {
                cleanup(); resolve(null);
            });
            dialog.querySelector('.cd-close').addEventListener('click', () => {
                cleanup(); resolve(null);
            });
            backdrop.addEventListener('click', (e) => {
                if (e.target === backdrop) { cleanup(); resolve(null); }
            });

            document.body.appendChild(backdrop);
            document.body.appendChild(dialog);
            // Focus first button
            setTimeout(() => dialog.querySelector('#cdToggleStart')?.focus(), 50);
        });
    }

    // ==================== PRIVATE ====================

    /**
     * Core dialog builder for transition inputs
     * @private
     */
    _showDialog({ title, subtitle, isPDA, allowEpsilon, initialValues }) {
        return new Promise((resolve) => {
            const { dialog, backdrop } = this._createBackdrop();

            dialog.innerHTML = `
                <div class="cd-header">
                    <div class="cd-icon"><i class="fas fa-arrow-right"></i></div>
                    <div>
                        <h3 class="cd-title">${title}</h3>
                        <p class="cd-subtitle">${subtitle}</p>
                    </div>
                    <button class="cd-close" type="button" aria-label="Close"><i class="fas fa-times"></i></button>
                </div>
                <div class="cd-body">
                    <div class="cd-field-group">
                        <label class="cd-label" for="cdSymbol">
                            <i class="fas fa-font"></i> Symbol
                            ${allowEpsilon ? '<span class="cd-hint">(or ε for epsilon)</span>' : ''}
                        </label>
                        <div class="cd-input-row">
                            <input type="text" id="cdSymbol" class="cd-input" 
                                   maxlength="1" placeholder="${allowEpsilon ? 'a or ε' : 'a'}"
                                   value="${this._escHtml((initialValues.symbol || '').charAt(0))}" autocomplete="off" />
                            ${allowEpsilon ? '<button class="cd-epsilon-btn" id="cdEpsilonBtn" type="button" title="Insert epsilon (ε)">ε</button>' : ''}
                        </div>
                    </div>
                    ${isPDA ? `
                    <div class="cd-field-group">
                        <label class="cd-label" for="cdStackPop">
                            <i class="fas fa-layer-group"></i> Stack Pop
                            <span class="cd-hint">(symbol to pop, or ε)</span>
                        </label>
                        <div class="cd-input-row">
                            <input type="text" id="cdStackPop" class="cd-input" 
                                   maxlength="1" placeholder="ε (pop nothing)"
                                   value="${this._escHtml((initialValues.stackPop || '').charAt(0))}" autocomplete="off" />
                            <button class="cd-epsilon-btn" id="cdEpsilonPopBtn" type="button" title="Insert epsilon (ε)">ε</button>
                        </div>
                    </div>
                    <div class="cd-field-group">
                        <label class="cd-label" for="cdStackPush">
                            <i class="fas fa-layer-group"></i> Stack Push
                            <span class="cd-hint">(symbols to push, or leave empty)</span>
                        </label>
                        <input type="text" id="cdStackPush" class="cd-input" 
                               maxlength="20" placeholder="e.g. XY (push Y then X)"
                               value="${this._escHtml(initialValues.stackPush || '')}" autocomplete="off" />
                    </div>
                    ` : ''}
                </div>
                <div class="cd-footer">
                    <button class="cd-btn cd-btn-secondary" id="cdCancel" type="button">Cancel</button>
                    <button class="cd-btn cd-btn-primary" id="cdConfirm" type="button">
                        <i class="fas fa-check"></i> Confirm
                    </button>
                </div>
            `;

            const symbolInput = dialog.querySelector('#cdSymbol');
            const stackPopInput = dialog.querySelector('#cdStackPop');
            const stackPushInput = dialog.querySelector('#cdStackPush');

            // Epsilon buttons
            dialog.querySelector('#cdEpsilonBtn')?.addEventListener('click', () => {
                symbolInput.value = 'ε';
                symbolInput.focus();
            });
            dialog.querySelector('#cdEpsilonPopBtn')?.addEventListener('click', () => {
                stackPopInput.value = 'ε';
                stackPopInput.focus();
            });

            const confirm = () => {
                let rawSymbol = symbolInput.value.trim();
                if (!rawSymbol) { symbolInput.classList.add('cd-input-error'); symbolInput.focus(); return; }

                // Enforce single-character symbol (except epsilon)
                if (rawSymbol.length > 1 && rawSymbol !== 'ε') {
                    rawSymbol = rawSymbol.charAt(0);
                }

                const result = { symbol: this._parseSymbol(rawSymbol) };
                if (isPDA) {
                    let rawPop = stackPopInput?.value.trim() || '';
                    if (rawPop.length > 1 && rawPop !== 'ε') rawPop = rawPop.charAt(0);
                    result.stackPop = this._parseSymbol(rawPop);
                    result.stackPush = stackPushInput?.value.trim() || '';
                    result.rawSymbol = rawSymbol;
                    result.rawStackPop = rawPop;
                } else {
                    result.rawSymbol = rawSymbol;
                }

                cleanup(); resolve(result);
            };

            const cancel = () => { cleanup(); resolve(null); };

            const cleanup = () => {
                this._detachKeyHandler();
                this._removeDialog(dialog, backdrop);
            };

            this._attachKeyHandler((key) => {
                if (key === 'Enter') confirm();
                else if (key === 'Escape') cancel();
            });

            dialog.querySelector('#cdConfirm').addEventListener('click', confirm);
            dialog.querySelector('#cdCancel').addEventListener('click', cancel);
            dialog.querySelector('.cd-close').addEventListener('click', cancel);
            backdrop.addEventListener('click', (e) => { if (e.target === backdrop) cancel(); });

            // Enter key on inputs also confirms
            dialog.querySelectorAll('.cd-input').forEach(input => {
                input.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter') { e.preventDefault(); confirm(); }
                    if (e.key === 'Escape') { e.preventDefault(); cancel(); }
                    input.classList.remove('cd-input-error');
                });
            });

            document.body.appendChild(backdrop);
            document.body.appendChild(dialog);
            // Auto-focus symbol input
            setTimeout(() => symbolInput?.select(), 50);
        });
    }

    /**
     * Create the dialog and backdrop elements
     * @private
     */
    _createBackdrop() {
        const backdrop = document.createElement('div');
        backdrop.className = 'cd-backdrop';

        const dialog = document.createElement('div');
        dialog.className = 'cd-dialog';
        dialog.setAttribute('role', 'dialog');
        dialog.setAttribute('aria-modal', 'true');

        this._dialog = dialog;
        this._backdrop = backdrop;
        return { dialog, backdrop };
    }

    /**
     * Remove dialog and backdrop from DOM
     * @private
     */
    _removeDialog(dialog, backdrop) {
        dialog?.remove();
        backdrop?.remove();
        this._dialog = null;
        this._backdrop = null;
    }

    /**
     * Attach global key handler
     * @private
     */
    _attachKeyHandler(handler) {
        this._keyHandler = (e) => handler(e.key);
        document.addEventListener('keydown', this._keyHandler);
    }

    /**
     * Detach global key handler
     * @private
     */
    _detachKeyHandler() {
        if (this._keyHandler) {
            document.removeEventListener('keydown', this._keyHandler);
            this._keyHandler = null;
        }
    }

    /**
     * Parse symbol input (handle ε → \0)
     * @private
     */
    _parseSymbol(raw) {
        if (!raw || raw === 'ε' || raw === 'epsilon' || raw === '\\0') {
            return '\0';
        }
        return raw.charAt(0); // Only first character for symbol
    }

    /**
     * HTML-escape a string for safe innerHTML insertion
     * @private
     */
    _escHtml(str) {
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    /**
     * Destroy the dialog manager (cleanup any open dialogs)
     */
    destroy() {
        this._detachKeyHandler();
        this._removeDialog(this._dialog, this._backdrop);
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.TransitionDialog = TransitionDialog;
}
