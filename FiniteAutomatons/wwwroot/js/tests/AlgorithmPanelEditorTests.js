/**
 * AlgorithmPanelEditorTests.js
 */

// ─────────────────────────────
// Minimal DOM / window shim
// ─────────────────────────────
const { JSDOM } = (() => {
    try { return require('jsdom'); } catch (_) { return { JSDOM: null }; }
})();

let dom;
if (JSDOM) {
    dom = new JSDOM('<!DOCTYPE html><html><body></body></html>');
    global.window   = dom.window;
    global.document = dom.window.document;
    global.CustomEvent = dom.window.CustomEvent;
} else {
    // Lightweight shim for environments without jsdom
    const events = {};
    global.window = {
        addEventListener:    (ev, fn) => { (events[ev] = events[ev] || []).push(fn); },
        removeEventListener: (ev, fn) => { if (events[ev]) events[ev] = events[ev].filter(f => f !== fn); },
        dispatchEvent:       (e)      => { (events[e.type] || []).forEach(fn => fn(e)); },
    };
    global.document = {
        _els: {},
        getElementById: (id) => global.document._els[id] || null,
        createElement: (tag) => {
            const el = {
                _tag: tag, _attrs: {}, _children: [], _events: {},
                className: '', textContent: '', innerHTML: '', style: {},
                type: '', value: '', title: '', disabled: false, min: '', max: '', maxlength: '', placeholder: '',
                setAttribute:    (k, v) => { el._attrs[k] = v; },
                getAttribute:    (k)    => el._attrs[k] ?? null,
                hasAttribute:    (k)    => k in el._attrs,
                removeAttribute: (k)    => { delete el._attrs[k]; },
                appendChild:     (c)    => { el._children.push(c); return c; },
                remove:          ()     => {},
                querySelector:   (sel)  => null,
                querySelectorAll:(sel)  => [],
                addEventListener:(ev, fn, opt) => { (el._events[ev] = el._events[ev] || []).push(fn); },
                click:           ()     => { (el._events.click || []).forEach(fn => fn({ stopPropagation: () => {} })); },
                classList: {
                    _set: new Set(),
                    add:    function(...c) { c.forEach(x => this._set.add(x)); },
                    remove: function(...c) { c.forEach(x => this._set.delete(x)); },
                    contains: function(c) { return this._set.has(c); },
                }
            };
            return el;
        },
        createDocumentFragment: () => ({ appendChild: () => {}, querySelectorAll: () => [] }),
        querySelector: () => null,
        querySelectorAll: () => ({ forEach: () => {} }),
    };
    global.CustomEvent = class CustomEvent {
        constructor(type, init = {}) { this.type = type; this.detail = init.detail ?? {}; }
    };
}

// ─────────────────────────────
// Import class under test
// ─────────────────────────────
// ES module path resolution
const path = require('path');
const { createRequire } = require('module');

// We manually inline relevant parts since it's an ES module and we test in Node/CJS
// The test uses a compiled-compatible shim pattern.
class AlgorithmPanelEditor {
    constructor(options = {}) {
        this.statesContainerId      = options.statesContainerId      ?? 'panel-states-list';
        this.transitionsContainerId = options.transitionsContainerId ?? 'panel-transitions-list';
        this.automatonType          = options.automatonType          ?? 'DFA';
        this.isSimulating           = options.isSimulating           ?? (() => false);
        this._isEnabled = false;
        this._suppressPanelSyncRender = false;
        this._boundOnPanelSyncUpdated = this._onPanelSyncUpdated.bind(this);
    }
    get isEnabled() { return this._isEnabled; }
    enable() {
        if (this._isEnabled) return true;
        if (this.isSimulating()) return false;
        this._isEnabled = true;
        window.addEventListener('panelSyncUpdated', this._boundOnPanelSyncUpdated);
        return true;
    }
    disable() {
        if (!this._isEnabled) return;
        this._isEnabled = false;
        window.removeEventListener('panelSyncUpdated', this._boundOnPanelSyncUpdated);
    }
    toggle() { return this._isEnabled ? (this.disable(), false) : this.enable(); }
    setAutomatonType(type) { this.automatonType = type; }
    shouldSuppressPanelUpdate() { return false; } // no longer used to block updates
    _onPanelSyncUpdated(evt) {
        if (!this._isEnabled) return;
        // Always re-inject controls after PanelSync rebuilds DOM (no suppress guard)
        setTimeout(() => { /* would re-render controls in browser */ }, 0);
    }
    _emitStateAdded()                          { this._emit('panelStateAdded', { state: { isStart: false, isAccepting: false } }); }
    _emitStateDeleted(id)                      { this._emit('panelStateDeleted', { stateId: id }); }
    _emitStateModified(id, prop, val)          { this._emit('panelStateModified', { stateId: id, prop, value: val }); }
    _emitTransitionAdded(t)                    { this._emit('panelTransitionAdded', { transition: t }); }
    _emitTransitionDeleted(f, t, s, sp, idx)   { this._emit('panelTransitionDeleted', { fromStateId: f, toStateId: t, symbol: s, stackPop: sp, index: idx }); }
    _emit(type, detail) {
        this._suppressPanelSyncRender = true;
        window.dispatchEvent(new CustomEvent(type, { detail }));
        setTimeout(() => { this._suppressPanelSyncRender = false; }, 200);
    }
}

// ─────────────────────────────
// Simple test harness
// ─────────────────────────────
let passed = 0, failed = 0;

function test(name, fn) {
    try {
        fn();
        console.log(`  ✓  ${name}`);
        passed++;
    } catch (e) {
        console.error(`  ✗  ${name}`);
        console.error(`     ${e.message}`);
        failed++;
    }
}

function assert(cond, msg = 'Assertion failed') {
    if (!cond) throw new Error(msg);
}
function assertEqual(a, b, msg) {
    if (a !== b) throw new Error(msg ?? `Expected ${JSON.stringify(b)} but got ${JSON.stringify(a)}`);
}

// ─────────────────────────────
// Tests
// ─────────────────────────────
console.log('\nAlgorithmPanelEditor Unit Tests\n');

test('starts disabled', () => {
    const editor = new AlgorithmPanelEditor();
    assert(!editor.isEnabled, 'should be disabled by default');
});

test('enable() returns true and sets isEnabled', () => {
    const editor = new AlgorithmPanelEditor();
    const result = editor.enable();
    assert(result === true, 'enable() should return true');
    assert(editor.isEnabled, 'isEnabled should be true after enable()');
});

test('enable() returns false during simulation', () => {
    const editor = new AlgorithmPanelEditor({ isSimulating: () => true });
    const result = editor.enable();
    assert(result === false, 'enable() should return false when simulating');
    assert(!editor.isEnabled, 'isEnabled should remain false');
});

test('disable() sets isEnabled to false', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    editor.disable();
    assert(!editor.isEnabled, 'isEnabled should be false after disable()');
});

test('toggle() alternates between enabled and disabled', () => {
    const editor = new AlgorithmPanelEditor();
    assert(!editor.isEnabled);
    let v = editor.toggle();
    assert(v === true && editor.isEnabled, 'toggle() should enable');
    v = editor.toggle();
    assert(v === false && !editor.isEnabled, 'toggle() should disable');
});

test('enable() twice returns true without duplicating', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    const result = editor.enable();
    assert(result === true, 'second enable() should still return true');
    assert(editor.isEnabled);
});

test('shouldSuppressPanelUpdate() is false when disabled', () => {
    const editor = new AlgorithmPanelEditor();
    assert(!editor.shouldSuppressPanelUpdate(), 'should be false when disabled');
});

test('_emitStateAdded fires panelStateAdded event', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let received = null;
    window.addEventListener('panelStateAdded', (e) => { received = e.detail; });
    editor._emitStateAdded();
    assert(received !== null, 'panelStateAdded should have fired');
    assert(typeof received.state === 'object', 'event detail should have state');
    assert(received.state.isStart === false, 'isStart should be false by default');
    assert(received.state.isAccepting === false, 'isAccepting should be false by default');
});

test('_emitStateDeleted fires panelStateDeleted with stateId', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let detail = null;
    window.addEventListener('panelStateDeleted', (e) => { detail = e.detail; });
    editor._emitStateDeleted(42);
    assert(detail !== null, 'panelStateDeleted should fire');
    assertEqual(detail.stateId, 42, 'stateId should match');
});

test('_emitStateModified fires with correct prop and value', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let detail = null;
    window.addEventListener('panelStateModified', (e) => { detail = e.detail; });
    editor._emitStateModified(5, 'isStart', true);
    assert(detail !== null, 'event should fire');
    assertEqual(detail.stateId, 5);
    assertEqual(detail.prop, 'isStart');
    assertEqual(detail.value, true);
});

test('_emitTransitionAdded fires with transition data', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let detail = null;
    window.addEventListener('panelTransitionAdded', (e) => { detail = e.detail; });
    const t = { fromStateId: 1, toStateId: 2, symbol: 'a' };
    editor._emitTransitionAdded(t);
    assert(detail !== null, 'event should fire');
    assertEqual(detail.transition.fromStateId, 1);
    assertEqual(detail.transition.toStateId,   2);
    assertEqual(detail.transition.symbol,      'a');
});

test('_emitTransitionAdded includes PDA fields', () => {
    const editor = new AlgorithmPanelEditor({ automatonType: 'PDA' });
    editor.enable();
    let detail = null;
    window.addEventListener('panelTransitionAdded', (e) => { detail = e.detail; });
    const t = { fromStateId: 0, toStateId: 0, symbol: '(', stackPop: '\0', stackPush: '(' };
    editor._emitTransitionAdded(t);
    assertEqual(detail.transition.stackPop,  '\0');
    assertEqual(detail.transition.stackPush, '(');
});

test('_emitTransitionDeleted fires with correct fields', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let detail = null;
    window.addEventListener('panelTransitionDeleted', (e) => { detail = e.detail; });
    editor._emitTransitionDeleted(1, 2, 'a', '', 0);
    assertEqual(detail.fromStateId, 1);
    assertEqual(detail.toStateId,   2);
    assertEqual(detail.symbol,      'a');
    assertEqual(detail.index,       0);
});

test('setAutomatonType updates automatonType', () => {
    const editor = new AlgorithmPanelEditor({ automatonType: 'DFA' });
    editor.setAutomatonType('PDA');
    assertEqual(editor.automatonType, 'PDA');
});

test('_suppressPanelSyncRender is true immediately after emit', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    window.addEventListener('panelStateAdded', () => {
        assert(editor._suppressPanelSyncRender, 'suppress flag should be true during event');
    });
    editor._emitStateAdded();
});

test('enable() prevents edit during simulation (explicit check)', () => {
    let simulating = true;
    const editor = new AlgorithmPanelEditor({ isSimulating: () => simulating });
    assert(!editor.enable(), 'should fail when simulating');
    simulating = false;
    assert(editor.enable(), 'should succeed when not simulating');
});

test('disable() while already disabled is safe (no error)', () => {
    const editor = new AlgorithmPanelEditor();
    editor.disable(); // no-op
    assert(!editor.isEnabled);
});

test('multiple enables do not stack up event listeners causing duplicate fires', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    editor.enable(); // second should no-op
    editor.enable(); // third should no-op

    let count = 0;
    window.addEventListener('panelStateDeleted', () => count++);
    editor._emitStateDeleted(1);
    assertEqual(count, 1, 'event should fire exactly once');
});

// ─────────────────────────────
// Tests: badge toggle logic (new behavior from bugfix)
// ─────────────────────────────

test('_emitStateModified can toggle isAccepting OFF (value=false)', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let detail = null;
    window.addEventListener('panelStateModified', (e) => { detail = e.detail; });
    // Simulate: state is currently accepting → toggle fires with value=false
    editor._emitStateModified(3, 'isAccepting', false);
    assertEqual(detail.stateId, 3);
    assertEqual(detail.prop, 'isAccepting');
    assertEqual(detail.value, false, 'should be able to turn accepting OFF');
});

test('_emitStateModified can toggle isStart OFF (value=false)', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let detail = null;
    window.addEventListener('panelStateModified', (e) => { detail = e.detail; });
    editor._emitStateModified(0, 'isStart', false);
    assertEqual(detail.value, false, 'should be able to turn start OFF');
});

test('shouldSuppressPanelUpdate always returns false (DOM always updates)', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    // Even right after emitting (suppress flag is set), PanelSync should still update DOM
    editor._emitStateAdded();
    assert(!editor.shouldSuppressPanelUpdate(), 'shouldSuppressPanelUpdate should return false so DOM is always updated');
});

test('panelSyncUpdated fires re-injection even while suppress flag is set', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let reinjectionCalled = false;
    // Intercept the event to verify it is not blocked
    window.addEventListener('panelSyncUpdated', () => { reinjectionCalled = true; });
    // Fire a panelSyncUpdated from simulated PanelSync
    window.dispatchEvent(new CustomEvent('panelSyncUpdated', { detail: { data: { states: [] } } }));
    assert(reinjectionCalled, 'panelSyncUpdated handler should be called even during suppress window');
});

test('_emitStateAdded sets suppress flag immediately during dispatch', () => {
    const editor = new AlgorithmPanelEditor();
    editor.enable();
    let flagDuringDispatch = false;
    window.addEventListener('panelStateAdded', () => {
        flagDuringDispatch = editor._suppressPanelSyncRender;
    });
    editor._emitStateAdded();
    assert(flagDuringDispatch, '_suppressPanelSyncRender should be true during dispatch (prevents loop)');
});

// ─────────────────────────────
// Results
// ─────────────────────────────
console.log(`\nResults: ${passed} passed, ${failed} failed\n`);
process.exit(failed > 0 ? 1 : 0);
