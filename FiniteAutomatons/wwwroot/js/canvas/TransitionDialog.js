export class TransitionDialog {
    constructor(containerEl) {
        this.containerEl = containerEl;
        this._dialog = null;
        this._backdrop = null;
        this._activeResolve = null;
        this._keyHandler = null;
    }

    show(sourceNode, targetNode, automatonType) {
        const title = `Add Transition`;
        const subtitle = `${sourceNode.data('label')} → ${targetNode.data('label')}`;
        const isPDA = automatonType === 'PDA' || automatonType === 'DPDA' || automatonType === 'NPDA';
        const allowEpsilon = automatonType === 'EpsilonNFA' || isPDA;

        return this._showDialog({ title, subtitle, isPDA, allowEpsilon, initialValues: {} });
    }

    showEdit(edge, automatonType) {
        const sourceLabel = edge.source().data('label');
        const targetLabel = edge.target().data('label');
        const title = `Edit Transition`;
        const subtitle = `${sourceLabel} → ${targetLabel}`;
        const isPDA = automatonType === 'PDA' || automatonType === 'DPDA' || automatonType === 'NPDA';
        const allowEpsilon = automatonType === 'EpsilonNFA' || isPDA;

        const initialValues = this._buildInitialValuesForEdit(edge, isPDA);

        return this._showDialog({ title, subtitle, isPDA, allowEpsilon, initialValues, isEdit: true });
    }

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
            setTimeout(() => dialog.querySelector('#cdToggleStart')?.focus(), 50);
        });
    }

    // ==================== PRIVATE ====================

    _showDialog({ title, subtitle, isPDA, allowEpsilon, initialValues, isEdit = false }) {
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
                                   maxlength="20" placeholder="${allowEpsilon ? 'a b c or ε' : 'a b c'}"
                                   value="${this._escHtml((initialValues.symbol || ''))}" autocomplete="off" />
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
                    ${isEdit ? '<button class="cd-btn cd-btn-danger" id="cdDelete" type="button" style="margin-right: auto"><i class="fas fa-trash"></i> Delete</button>' : ''}
                    <button class="cd-btn cd-btn-secondary" id="cdCancel" type="button">Cancel</button>
                    <button class="cd-btn cd-btn-primary" id="cdConfirm" type="button">
                        <i class="fas fa-check"></i> Confirm
                    </button>
                </div>
            `;

            const symbolInput = dialog.querySelector('#cdSymbol');
            const stackPopInput = dialog.querySelector('#cdStackPop');
            const stackPushInput = dialog.querySelector('#cdStackPush');

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

                const symbols = rawSymbol === 'ε' ? ['ε'] : rawSymbol.split(/[\s,]+/).filter(Boolean);

                // Block epsilon when not allowed (DFA, NFA)
                if (!allowEpsilon) {
                    const hasEpsilon = symbols.some(s => s === 'ε' || s === 'epsilon' || s === '\\0');
                    if (hasEpsilon) {
                        symbolInput.classList.add('cd-input-error');
                        let errorHint = dialog.querySelector('#cdSymbolError');
                        if (!errorHint) {
                            errorHint = document.createElement('div');
                            errorHint.id = 'cdSymbolError';
                            errorHint.className = 'cd-hint';
                            errorHint.style.color = '#e63946';
                            errorHint.style.marginTop = '4px';
                            errorHint.innerHTML = "ε transitions are not allowed for this automaton type";
                            symbolInput.parentElement.appendChild(errorHint);
                        }
                        symbolInput.focus();
                        return;
                    }
                }
                
                const invalidSymbol = symbols.find(s => s.length > 1 && s !== 'ε' && s !== 'epsilon' && s !== '\\0');
                if (invalidSymbol) {
                    symbolInput.classList.add('cd-input-error');
                    let errorHint = dialog.querySelector('#cdSymbolError');
                    if (!errorHint) {
                        errorHint = document.createElement('div');
                        errorHint.id = 'cdSymbolError';
                        errorHint.className = 'cd-hint';
                        errorHint.style.color = '#e63946';
                        errorHint.style.marginTop = '4px';
                        errorHint.innerHTML = "Use single characters separated by spaces (e.g. <strong>a b c</strong>)";
                        symbolInput.parentElement.appendChild(errorHint);
                    }
                    symbolInput.focus(); 
                    return;
                }

                const results = symbols.map(sym => {
                    return { symbol: this._parseSymbol(sym), rawSymbol: sym };
                });

                if (isPDA) {
                    let rawPop = stackPopInput?.value.trim() || '';
                    if (rawPop.length > 1 && rawPop !== 'ε') rawPop = rawPop.charAt(0);
                    const parsedPop = this._parseSymbol(rawPop);
                    const pushTrimmed = stackPushInput?.value.trim() || '';
                    results.forEach(r => {
                        r.stackPop = parsedPop;
                        r.stackPush = pushTrimmed;
                        r.rawStackPop = rawPop;
                    });
                }

                cleanup(); resolve(results);
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
            dialog.querySelector('#cdDelete')?.addEventListener('click', () => {
                cleanup(); resolve('DELETE');
            });
            dialog.querySelector('.cd-close').addEventListener('click', cancel);
            backdrop.addEventListener('click', (e) => { if (e.target === backdrop) cancel(); });

            dialog.querySelectorAll('.cd-input').forEach(input => {
                input.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter') { e.preventDefault(); confirm(); }
                    if (e.key === 'Escape') { e.preventDefault(); cancel(); }
                    input.classList.remove('cd-input-error');
                    if (input.id === 'cdSymbol') {
                        const errorHint = dialog.querySelector('#cdSymbolError');
                        if (errorHint) errorHint.remove();
                    }
                });
            });

            document.body.appendChild(backdrop);
            document.body.appendChild(dialog);
            setTimeout(() => symbolInput?.select(), 50);
        });
    }

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

    _removeDialog(dialog, backdrop) {
        dialog?.remove();
        backdrop?.remove();
        this._dialog = null;
        this._backdrop = null;
    }

    _attachKeyHandler(handler) {
        this._keyHandler = (e) => handler(e.key);
        document.addEventListener('keydown', this._keyHandler);
    }

    _detachKeyHandler() {
        if (this._keyHandler) {
            document.removeEventListener('keydown', this._keyHandler);
            this._keyHandler = null;
        }
    }

    _parseSymbol(raw) {
        if (!raw || raw === 'ε' || raw === 'epsilon' || raw === '\\0') {
            return '\0';
        }
        return raw.charAt(0); 
    }

    _buildInitialValuesForEdit(edge, isPDA) {
        const symbol = this._extractEditSymbols(edge, isPDA);
        if (!isPDA) {
            return { symbol, stackPop: '', stackPush: '' };
        }

        let pop = edge.data('rawStackPop') || edge.data('stackPop');
        let push = edge.data('stackPush') || '';

        if ((!pop || pop === '\\0' || pop === '\0') && edge.data('label')) {
            const firstLine = String(edge.data('label')).split('\n').map(l => l.trim()).find(Boolean) || '';
            const match = firstLine.match(/^(.+?)\s*\(\s*(.+?)\s*\/\s*(.*?)\s*\)$/)
                || firstLine.match(/^(.+?),\s*(.+?)\s*\/\s*(.*)$/);
            if (match) {
                pop = match[2].trim();
                push = match[3].trim();
            }
        }

        const normalizedPop = this._normalizeSymbolForInput(pop);
        const normalizedPush = (!push || push === 'ε' || push === '\\0' || push === '\0') ? '' : String(push);
        return { symbol, stackPop: normalizedPop, stackPush: normalizedPush };
    }

    _extractEditSymbols(edge, isPDA) {
        const rawSymbol = edge.data('rawSymbol');
        if (rawSymbol && String(rawSymbol).trim()) {
            return String(rawSymbol).trim();
        }

        const directSymbol = edge.data('symbol');
        if (directSymbol !== undefined && directSymbol !== null && String(directSymbol).trim() !== '') {
            return this._normalizeSymbolForInput(directSymbol);
        }

        const label = String(edge.data('label') || '').trim();
        if (!label) return '';

        if (!isPDA) {
            return label.split(',').map(s => s.trim()).filter(Boolean).join(' ');
        }

        const lines = label.split('\n').map(l => l.trim()).filter(Boolean);
        const symbols = lines.map(line => {
            const match = line.match(/^(.+?)\s*\(\s*(.+?)\s*\/\s*(.*?)\s*\)$/)
                || line.match(/^(.+?),\s*(.+?)\s*\/\s*(.*)$/);
            return this._normalizeSymbolForInput(match ? match[1].trim() : line);
        });

        return [...new Set(symbols)].join(' ');
    }

    _normalizeSymbolForInput(raw) {
        if (!raw || raw === 'ε' || raw === '\\0' || raw === '\0') return 'ε';
        const str = String(raw);
        if (str.length === 1 && str.charCodeAt(0) === 0) return 'ε';
        return str;
    }

    _escHtml(str) {
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    destroy() {
        this._detachKeyHandler();
        this._removeDialog(this._dialog, this._backdrop);
    }
}

if (typeof window !== 'undefined') {
    window.TransitionDialog = TransitionDialog;
}
