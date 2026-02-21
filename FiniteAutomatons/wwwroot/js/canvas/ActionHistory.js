/**
 * ActionHistory.js
 * Undo/Redo system using the Command pattern.
 *
 * Usage:
 *   const history = new ActionHistory({ maxSize: 50 });
 *   history.recordAction({
 *     do: () => { ... perform action ... },
 *     undo: () => { ... reverse action ... }
 *   });
 *   history.undo();
 *   history.redo();
 *
 * @module ActionHistory
 */

export class ActionHistory {
    /**
     * @param {Object} options
     * @param {number} [options.maxSize=100] - Max number of undoable actions
     * @param {Function} [options.onHistoryChanged] - Called whenever history state changes
     */
    constructor(options = {}) {
        this.maxSize = options.maxSize ?? 100;
        this.onHistoryChanged = options.onHistoryChanged || (() => {});

        /** @type {Array<{do: Function, undo: Function}>} */
        this._undoStack = [];

        /** @type {Array<{do: Function, undo: Function}>} */
        this._redoStack = [];
    }

    /**
     * Record a new reversible action.
     * The `do` function is NOT called — it is assumed the action has already been performed.
     * @param {{ do: Function, undo: Function }} action
     */
    recordAction(action) {
        if (!action || typeof action.undo !== 'function') {
            console.warn('ActionHistory.recordAction: action must have an undo function');
            return;
        }

        this._undoStack.push(action);
        this._redoStack = []; // Any new action clears the redo stack

        // Enforce max size
        if (this._undoStack.length > this.maxSize) {
            this._undoStack.shift();
        }

        this.onHistoryChanged({ canUndo: this.canUndo(), canRedo: this.canRedo() });
    }

    /**
     * Undo the last recorded action
     * @returns {boolean} true if an action was undone
     */
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
        // Notify other parts of the app (e.g., form sync) that history was applied
        try {
            if (typeof window !== 'undefined' && typeof CustomEvent === 'function') {
                window.dispatchEvent(new CustomEvent('canvasHistoryApplied'));
            }
        } catch (err) {
            // ignore
        }
        return true;
    }

    /**
     * Redo the last undone action
     * @returns {boolean} true if an action was redone
     */
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
        // Notify other parts of the app (e.g., form sync) that history was applied
        try {
            if (typeof window !== 'undefined' && typeof CustomEvent === 'function') {
                window.dispatchEvent(new CustomEvent('canvasHistoryApplied'));
            }
        } catch (err) {
            // ignore
        }
        return true;
    }

    /**
     * Whether there are actions that can be undone
     * @returns {boolean}
     */
    canUndo() {
        return this._undoStack.length > 0;
    }

    /**
     * Whether there are actions that can be redone
     * @returns {boolean}
     */
    canRedo() {
        return this._redoStack.length > 0;
    }

    /**
     * Get history counts
     * @returns {{ undoCount: number, redoCount: number }}
     */
    getStatus() {
        return {
            undoCount: this._undoStack.length,
            redoCount: this._redoStack.length,
            canUndo: this.canUndo(),
            canRedo: this.canRedo()
        };
    }

    /**
     * Clear all history (e.g., on automaton reload)
     */
    clear() {
        this._undoStack = [];
        this._redoStack = [];
        this.onHistoryChanged({ canUndo: false, canRedo: false });
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.ActionHistory = ActionHistory;
}
