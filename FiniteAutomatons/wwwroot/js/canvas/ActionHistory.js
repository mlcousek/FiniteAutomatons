export class ActionHistory {
    constructor(options = {}) {
        this.maxSize = options.maxSize ?? 100;
        this.onHistoryChanged = options.onHistoryChanged || (() => {});

        this._undoStack = [];

        this._redoStack = [];
    }

    recordAction(action) {
        if (!action || typeof action.undo !== 'function') {
            console.warn('ActionHistory.recordAction: action must have an undo function');
            return;
        }

        this._undoStack.push(action);
        this._redoStack = []; 

        if (this._undoStack.length > this.maxSize) {
            this._undoStack.shift();
        }

        this.onHistoryChanged({ canUndo: this.canUndo(), canRedo: this.canRedo() });
    }

    undo() {
        if (!this.canUndo()) return false;

        const action = this._undoStack.pop();
        try {
            action.undo();
        } catch (e) {
            console.error('ActionHistory.undo: undo function threw an error', e);
        }

        this._redoStack.push(action);
        this.onHistoryChanged({ canUndo: this.canUndo(), canRedo: this.canRedo() });
        try {
            if (typeof window !== 'undefined' && typeof CustomEvent === 'function') {
                window.dispatchEvent(new CustomEvent('canvasHistoryApplied'));
            }
        } catch (err) {
            // ignore
        }
        return true;
    }

    redo() {
        if (!this.canRedo()) return false;

        const action = this._redoStack.pop();
        try {
            if (typeof action.do === 'function') {
                action.do();
            } else {
                console.warn('ActionHistory.redo: action has no do function, skipping redo execution');
            }
        } catch (e) {
            console.error('ActionHistory.redo: do function threw an error', e);
        }

        this._undoStack.push(action);
        this.onHistoryChanged({ canUndo: this.canUndo(), canRedo: this.canRedo() });
        try {
            if (typeof window !== 'undefined' && typeof CustomEvent === 'function') {
                window.dispatchEvent(new CustomEvent('canvasHistoryApplied'));
            }
        } catch (err) {
            // ignore
        }
        return true;
    }

    canUndo() {
        return this._undoStack.length > 0;
    }

    canRedo() {
        return this._redoStack.length > 0;
    }

    getStatus() {
        return {
            undoCount: this._undoStack.length,
            redoCount: this._redoStack.length,
            canUndo: this.canUndo(),
            canRedo: this.canRedo()
        };
    }

    clear() {
        this._undoStack = [];
        this._redoStack = [];
        this.onHistoryChanged({ canUndo: false, canRedo: false });
    }
}

if (typeof window !== 'undefined') {
    window.ActionHistory = ActionHistory;
}
