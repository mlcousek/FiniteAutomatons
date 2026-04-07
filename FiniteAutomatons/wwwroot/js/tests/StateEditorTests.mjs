import assert from 'assert';
import { JSDOM } from 'jsdom';

const dom = new JSDOM('<!DOCTYPE html><html><body></body></html>');
global.window = dom.window;
global.document = dom.window.document;
global.CustomEvent = dom.window.CustomEvent;

import { StateEditor } from '../canvas/StateEditor.js';

let passed = 0;
let failed = 0;

function test(name, fn) {
    try {
        fn();
        console.log(`  ✓  ${name}`);
        passed++;
    } catch (e) {
        console.error(`  ✗  ${name}`);
        console.error(e.stack);
        failed++;
    }
}

console.log('\nStateEditor Unit Tests\n');

test('StateEditor - Cannot toggle off start state (triple click logic)', () => {
    const cyMock = {
        container: () => document.body,
        nodes: () => {
            const arr = [];
            arr.removeClass = () => {};
            arr.forEach = () => {};
            arr.first = () => null;
            return arr;
        }
    };
    const editor = new StateEditor(cyMock);

    let removeClassCalled = false;
    let dataCalled = false;

    const mockNode = {
        hasClass: (cls) => cls === 'start',
        removeClass: (cls) => { if (cls === 'start') removeClassCalled = true; },
        data: (k, v) => { if (v === false) dataCalled = true; return 'state-1'; },
        id: () => 'state-1',
        position: () => ({x: 0, y: 0})
    };

    let modifiedFired = false;
    editor.onStateModified = () => { modifiedFired = true; };

    editor.toggleStartState(mockNode);

    assert.strictEqual(removeClassCalled, false, 'Should not remove start class if it is already start state');
    assert.strictEqual(dataCalled, false, 'Should not set isStart to false');
    assert.strictEqual(modifiedFired, false, 'Should not trigger onStateModified');
});

console.log(`\nStateEditorTests: ${passed} passed, ${failed} failed\n`);
if (failed > 0) process.exit(1);
