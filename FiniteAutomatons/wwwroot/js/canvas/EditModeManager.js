export class EditModeManager {
    constructor(cy, options = {}) {
        this.cy = cy;
        this.isEditMode = options.enableByDefault || false;
        this.isSimulating = false;
        this.onModeChange = options.onModeChange || (() => {});

        this._applyMode();
    }

    enableEditMode() {
        if (this.isSimulating) {
            console.warn('Cannot enable edit mode during simulation');
            return false;
        }

        if (this.isEditMode) {
            return true;
        }

        this.isEditMode = true;
        this._applyMode();
        this.onModeChange(this.isEditMode);
        console.log('Edit mode enabled');
        return true;
    }

    disableEditMode() {
        if (!this.isEditMode) {
            return true; 
        }

        this.isEditMode = false;
        this._applyMode();
        this.onModeChange(this.isEditMode);
        console.log('Edit mode disabled');
        return true;
    }

    toggleEditMode() {
        if (this.isEditMode) {
            this.disableEditMode();
        } else {
            this.enableEditMode();
        }
        return this.isEditMode;
    }

    setSimulationState(isSimulating) {
        this.isSimulating = isSimulating;
        
        if (isSimulating && this.isEditMode) {
            this.isEditMode = false;
            this._applyMode();
            this.onModeChange(this.isEditMode);
            console.log('Edit mode disabled (simulation started)');
        }
    }

    isActive() {
        return this.isEditMode && !this.isSimulating;
    }

    _applyMode() {
      
        if (this.isEditMode && !this.isSimulating) {
            this._enableSelection();
            this._showEditCursor();
        } else {
            if (this.cy) {
                this.cy.autounselectify(true);
            }
            this._hideEditCursor();
        }
    }

    _enableDragging() {
        
    }

    _disableDragging() {
        
    }

    _enableSelection() {
        this.cy.selectionType('single');
        if (this.cy) {
            this.cy.autounselectify(false);
        }
    }

    _showEditCursor() {
        const container = this.cy.container();
        if (container) {
            container.style.cursor = 'default';
        }
    }

    _hideEditCursor() {
        const container = this.cy.container();
        if (container) {
            container.style.cursor = 'default';
        }
    }

    getStatus() {
        return {
            isEditMode: this.isEditMode,
            isSimulating: this.isSimulating,
            isActive: this.isActive()
        };
    }

    destroy() {
        this.disableEditMode();
        this.originalGrabbableState.clear();
        this.cy = null;
    }
}

if (typeof window !== 'undefined') {
    window.EditModeManager = EditModeManager;
}
