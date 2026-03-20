/**
 * AlgorithmPanelEditor.js
 */
export class AlgorithmPanelEditor {

    /**
     * @param {object} options
     * @param {string}  options.statesContainerId      - id of the states list container
     * @param {string}  options.transitionsContainerId - id of the transitions list container
     * @param {string}  options.automatonType          - 'DFA' | 'NFA' | 'EpsilonNFA' | 'PDA'
     * @param {() => boolean} options.isSimulating     - returns true when simulation is running
     */
    constructor(options = {}) {
        this.statesContainerId      = options.statesContainerId      ?? 'panel-states-list';
        this.transitionsContainerId = options.transitionsContainerId ?? 'panel-transitions-list';
        this.automatonType          = options.automatonType          ?? 'DFA';
        this.isSimulating           = options.isSimulating           ?? (() => false);

        this._isEnabled = false;
        /** Suppresses re-render from PanelSync while this editor is source of change */
        this._suppressPanelSyncRender = false;
        /** Next state id hint (canvas will override, but we use for the display) */
        this._nextStateIdHint = null;

        this._boundOnPanelSyncUpdated = this._onPanelSyncUpdated.bind(this);
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    get isEnabled() { return this._isEnabled; }

    enable() {
        if (this._isEnabled) return true;
        if (this.isSimulating()) {
            console.warn('AlgorithmPanelEditor: cannot enable during simulation');
            return false;
        }
        this._isEnabled = true;
        this._renderEditControls();
        window.addEventListener('panelSyncUpdated', this._boundOnPanelSyncUpdated);
        console.log('AlgorithmPanelEditor: enabled');
        return true;
    }

    disable() {
        if (!this._isEnabled) return;
        this._isEnabled = false;
        this._removeEditControls();
        window.removeEventListener('panelSyncUpdated', this._boundOnPanelSyncUpdated);
        console.log('AlgorithmPanelEditor: disabled');
    }

    toggle() {
        return this._isEnabled ? (this.disable(), false) : this.enable();
    }

    setAutomatonType(type) {
        this.automatonType = type;
        if (this._isEnabled) {
            this._removeEditControls();
            this._renderEditControls();
        }
    }

    shouldSuppressPanelUpdate() {
        return this._isEnabled && this._suppressPanelSyncRender;
    }

    refreshFromCanvasData(data) {
        if (!this._isEnabled) return;
        this._suppressPanelSyncRender = true;
        this._renderStatesEditUI(data);
        this._renderTransitionsEditUI(data);
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 50);
    }

    destroy() {
        this.disable();
    }

    // ─────────────────────────────────────────────
    // Internal – rendering
    // ─────────────────────────────────────────────

    _renderEditControls() {
        this._renderStatesEditUI(null);
        this._renderTransitionsEditUI(null);
    }

    _removeEditControls() {
        const sel = document.querySelectorAll('[data-panel-edit-control]');
        sel.forEach(el => el.remove());


        document.querySelectorAll('.panel-delete-btn').forEach(b => b.remove());
        document.querySelectorAll('.panel-badge-btn').forEach(b => {
        });
    }

    _renderStatesEditUI(data) {
        const container = document.getElementById(this.statesContainerId);
        if (!container) return;

        const existingItems = container.querySelectorAll('li.state-item');

        existingItems.forEach(li => {
            if (li.querySelector('[data-panel-edit-control]')) return;

            const stateId = parseInt(li.getAttribute('data-state-id') ?? '0', 10);

            const isStart     = li.getAttribute('data-is-start') === 'true';
            const isAccepting = li.getAttribute('data-is-accepting') === 'true';

            const startBadge = li.querySelector('.badge-start');
            if (startBadge) {
                startBadge.classList.toggle('badge-active', isStart);
                startBadge.classList.add('badge-clickable');
                startBadge.title = isStart ? 'Click to remove Start' : 'Click to set as Start';

                const freshStart = startBadge.cloneNode(true);
                startBadge.replaceWith(freshStart);

                freshStart.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const currentVal = li.getAttribute('data-is-start') === 'true';
                    this._emitStateModified(stateId, 'isStart', !currentVal);
                });
            }

            const acceptingBadge = li.querySelector('.badge-accepting');
            if (acceptingBadge) {
                acceptingBadge.classList.toggle('badge-active', isAccepting);
                acceptingBadge.classList.add('badge-clickable');
                acceptingBadge.title = isAccepting ? 'Click to remove Accepting' : 'Click to set as Accepting';

                const freshAccepting = acceptingBadge.cloneNode(true);
                acceptingBadge.replaceWith(freshAccepting);

                freshAccepting.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const currentVal = li.getAttribute('data-is-accepting') === 'true';
                    this._emitStateModified(stateId, 'isAccepting', !currentVal);
                });
            }

            const delBtn = document.createElement('button');
            delBtn.type = 'button';
            delBtn.className = 'panel-delete-btn panel-edit-ctrl';
            delBtn.setAttribute('data-panel-edit-control', 'true');
            delBtn.title = 'Delete state';
            delBtn.innerHTML = '<i class="fas fa-times"></i>';
            delBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this._emitStateDeleted(stateId);
            });
            li.appendChild(delBtn);
        });

        if (!container.querySelector('[data-panel-add-state]')) {
            const addBtn = document.createElement('button');
            addBtn.type = 'button';
            addBtn.className = 'panel-add-btn panel-edit-ctrl';
            addBtn.setAttribute('data-panel-edit-control', 'true');
            addBtn.setAttribute('data-panel-add-state', 'true');
            addBtn.innerHTML = '<i class="fas fa-plus"></i> Add State';
            addBtn.addEventListener('click', () => this._emitStateAdded());
            container.appendChild(addBtn);
        }
    }

    _renderTransitionsEditUI(data) {
        const container = document.getElementById(this.transitionsContainerId);
        if (!container) return;

        const isPDA = this.automatonType === 'PDA';

        const existingItems = container.querySelectorAll('li.transition-item');
        existingItems.forEach((li, index) => {
            if (li.querySelector('[data-panel-edit-control]')) return;

            const fromId  = parseInt(li.getAttribute('data-from')   ?? '0', 10);
            const toId    = parseInt(li.getAttribute('data-to')     ?? '0', 10);
            const symbol  = li.getAttribute('data-symbol') ?? '';
            const stackPop = li.getAttribute('data-stack-pop') ?? '';

            const delBtn = document.createElement('button');
            delBtn.type = 'button';
            delBtn.className = 'panel-delete-btn panel-edit-ctrl';
            delBtn.setAttribute('data-panel-edit-control', 'true');
            delBtn.title = 'Delete transition';
            delBtn.innerHTML = '<i class="fas fa-times"></i>';
            delBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this._emitTransitionDeleted(fromId, toId, symbol, stackPop, index);
            });
            li.appendChild(delBtn);
        });

        // Add "Add Transition" form at bottom if not already present
        if (!container.querySelector('[data-panel-add-transition]')) {
            const form = this._buildAddTransitionForm(isPDA);
            container.appendChild(form);
        }
    }

    _buildAddTransitionForm(isPDA) {
        const wrapper = document.createElement('div');
        wrapper.className = 'panel-add-transition-form panel-edit-ctrl';
        wrapper.setAttribute('data-panel-edit-control', 'true');
        wrapper.setAttribute('data-panel-add-transition', 'true');

        const isEpsilonNFA = this.automatonType === 'EpsilonNFA';

        wrapper.innerHTML = `
            <div class="panel-add-transition-fields">
                <input type="number" class="panel-field" id="panelFromState"
                       placeholder="From" min="0" title="From State ID"
                       style="min-width:0;flex:1 1 55px;" />
                <input type="number" class="panel-field" id="panelToState"
                       placeholder="To"   min="0" title="To State ID"
                       style="min-width:0;flex:1 1 55px;" />
                <div style="display:flex;gap:2px;flex:1 1 60px;min-width:0;">
                    <input type="text" class="panel-field" id="panelSymbol"
                           placeholder="Symbol" maxlength="4" title="Symbol (leave empty for ε)"
                           style="min-width:0;flex:1 1 0;" />
                    ${isEpsilonNFA ? `<button type="button" class="panel-epsilon-btn" id="panelEpsilonBtn"
                            title="Insert epsilon (ε) transition" style="
                            flex:0 0 auto;padding:0.2rem 0.45rem;font-size:0.8rem;
                            border:1px solid var(--bs-border-color);border-radius:4px;
                            background:rgba(var(--bs-info-rgb),0.1);color:var(--bs-body-color);
                            cursor:pointer;line-height:1;">ε</button>` : ''}
                </div>
                ${isPDA ? `
                <input type="text" class="panel-field" id="panelStackPop"
                       placeholder="Pop (ε=∅)" maxlength="4" title="Stack pop symbol"
                       style="min-width:0;flex:1 1 60px;" />
                <input type="text" class="panel-field" id="panelStackPush"
                       placeholder="Push"       maxlength="10" title="Stack push string"
                       style="min-width:0;flex:1 1 60px;" />
                ` : ''}
                <button type="button" class="panel-add-btn" id="panelAddTransitionBtn"
                        title="Add transition" style="flex:0 0 auto;padding:0.25rem 0.6rem;">
                    <i class="fas fa-plus"></i> Add
                </button>
            </div>
        `;

        // Epsilon button fills the symbol field
        const epsBtnEl = wrapper.querySelector('#panelEpsilonBtn');
        if (epsBtnEl) {
            epsBtnEl.addEventListener('click', () => {
                const symInput = wrapper.querySelector('#panelSymbol');
                if (symInput) { symInput.value = 'ε'; symInput.focus(); }
            });
        }

        wrapper.querySelector('#panelAddTransitionBtn').addEventListener('click', () => {
            const fromId = parseInt(wrapper.querySelector('#panelFromState')?.value ?? '', 10);
            const toId   = parseInt(wrapper.querySelector('#panelToState')?.value   ?? '', 10);
            const symRaw = (wrapper.querySelector('#panelSymbol')?.value ?? '').trim();

            if (isNaN(fromId) || isNaN(toId)) {
                this._showValidationError(wrapper, 'Please enter valid From and To state IDs.');
                return;
            }

            // ε or empty → epsilon symbol
            const symbol = (symRaw === '' || symRaw === 'ε') ? '\0' : symRaw;

            const transition = { fromStateId: fromId, toStateId: toId, symbol };

            if (isPDA) {
                const popRaw  = (wrapper.querySelector('#panelStackPop')?.value  ?? '').trim();
                const pushRaw = (wrapper.querySelector('#panelStackPush')?.value ?? '').trim();
                transition.stackPop  = popRaw  === '' ? '\0' : popRaw;
                transition.stackPush = pushRaw;
            }

            this._emitTransitionAdded(transition);

            // Clear input fields after submit
            wrapper.querySelectorAll('input').forEach(inp => { inp.value = ''; });
        });

        return wrapper;
    }

    _makeBadge(className, label) {
        const span = document.createElement('span');
        span.className = `state-badge ${className}`;
        span.textContent = label;
        return span;
    }

    _showValidationError(container, message) {
        let err = container.querySelector('.panel-validation-error');
        if (!err) {
            err = document.createElement('div');
            err.className = 'panel-validation-error';
            container.appendChild(err);
        }
        err.textContent = message;
        setTimeout(() => { if (err.parentNode) err.remove(); }, 3000);
    }

    // ─────────────────────────────────────────────
    // Internal – event emitters
    // ─────────────────────────────────────────────

    _emitStateAdded() {
        this._suppressPanelSyncRender = true;
        window.dispatchEvent(new CustomEvent('panelStateAdded', {
            detail: { state: { isStart: false, isAccepting: false } }
        }));
        // The integration layer will call refreshFromCanvasData after canvas responds
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 200);
    }

    _emitStateDeleted(stateId) {
        this._suppressPanelSyncRender = true;
        window.dispatchEvent(new CustomEvent('panelStateDeleted', {
            detail: { stateId }
        }));
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 200);
    }

    _emitStateModified(stateId, prop, value) {
        this._suppressPanelSyncRender = true;
        window.dispatchEvent(new CustomEvent('panelStateModified', {
            detail: { stateId, prop, value }
        }));
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 200);
    }

    _emitTransitionAdded(transition) {
        this._suppressPanelSyncRender = true;
        window.dispatchEvent(new CustomEvent('panelTransitionAdded', {
            detail: { transition }
        }));
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 200);
    }

    _emitTransitionDeleted(fromStateId, toStateId, symbol, stackPop, index) {
        this._suppressPanelSyncRender = true;
        window.dispatchEvent(new CustomEvent('panelTransitionDeleted', {
            detail: { fromStateId, toStateId, symbol, stackPop, index }
        }));
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 200);
    }

    // ─────────────────────────────────────────────
    // Internal – event handlers
    // ─────────────────────────────────────────────

    /** Re-render edit UI when PanelSync finishes a data update */
    _onPanelSyncUpdated(evt) {
        if (!this._isEnabled) return;
        setTimeout(() => {
            if (this._isEnabled) {
                this._renderStatesEditUI(evt.detail?.data ?? null);
                this._renderTransitionsEditUI(evt.detail?.data ?? null);
            }
        }, 0);
    }
}

if (typeof window !== 'undefined') {
    window.AlgorithmPanelEditor = AlgorithmPanelEditor;
}
