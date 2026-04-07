import assert from 'assert';
import { JSDOM } from 'jsdom';
import { TransitionDialog } from '../canvas/TransitionDialog.js';

const dom = new JSDOM('<!DOCTYPE html><html><body></body></html>');
global.window = dom.window;
global.document = dom.window.document;
global.CustomEvent = dom.window.CustomEvent;

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

async function runTests() {
    console.log('\nTransitionEditing Unit Tests\n');

    await new Promise(r => {
        test('TransitionDialog parsing space-separated symbols', async () => {
            const dialog = new TransitionDialog(document.body);
            const sourceNode = { data: () => 'q1' };
            const targetNode = { data: () => 'q2' };

            const promise = dialog.show(sourceNode, targetNode, 'DFA');
            
            // Wait for next tick so DOM is created
            await new Promise(r => setTimeout(r, 100));

            const symbolInput = document.getElementById('cdSymbol');
            symbolInput.value = 'a b c';
            
            const confirmBtn = document.getElementById('cdConfirm');
            confirmBtn.click();

            const results = await promise;
            
            assert.ok(Array.isArray(results), 'Expected results to be an array');
            assert.strictEqual(results.length, 3, 'Expected 3 parsed symbols');
            assert.strictEqual(results[0].symbol, 'a');
            assert.strictEqual(results[1].symbol, 'b');
            assert.strictEqual(results[2].symbol, 'c');
        });
        r();
    });

    await new Promise(r => {
        test('TransitionDialog PDA parsing space-separated symbols but same push/pop', async () => {
            const dialog = new TransitionDialog(document.body);
            const sourceNode = { data: () => 'q1' };
            const targetNode = { data: () => 'q2' };

            const promise = dialog.showEdit(
                { source: () => sourceNode, target: () => targetNode, data: () => '' }, 
                'PDA'
            );
            
            await new Promise(r => setTimeout(r, 100));

            const symbolInput = document.getElementById('cdSymbol');
            symbolInput.value = 'a b';
            
            const popInput = document.getElementById('cdStackPop');
            popInput.value = 'X';
            
            const pushInput = document.getElementById('cdStackPush');
            pushInput.value = 'YZ';
            
            const confirmBtn = document.getElementById('cdConfirm');
            confirmBtn.click();

            const results = await promise;
            
            assert.strictEqual(results.length, 2, 'Expected 2 parsed symbols');
            assert.strictEqual(results[0].symbol, 'a');
            assert.strictEqual(results[0].stackPop, 'X');
            assert.strictEqual(results[0].stackPush, 'YZ');
            
            assert.strictEqual(results[1].symbol, 'b');
            assert.strictEqual(results[1].stackPop, 'X');
            assert.strictEqual(results[1].stackPush, 'YZ');
        });
        r();
    });

    await new Promise(r => {
        test('TransitionDialog validation blocks unspaced multiple characters', async () => {
            const dialog = new TransitionDialog(document.body);
            const sourceNode = { data: () => 'q1' };
            const targetNode = { data: () => 'q2' };

            let resolved = false;
            const promise = dialog.show(sourceNode, targetNode, 'DFA').then(() => { resolved = true; });
            
            await new Promise(r => setTimeout(r, 100));

            const symbolInput = document.getElementById('cdSymbol');
            symbolInput.value = 'abcd';
            
            const confirmBtn = document.getElementById('cdConfirm');
            confirmBtn.click();

            await new Promise(r => setTimeout(r, 100));

            const errorHint = document.getElementById('cdSymbolError');
            
            assert.ok(!resolved, 'Promise should not resolve if input is invalid');
            assert.ok(errorHint, 'Error hint should be present');
            assert.ok(symbolInput.classList.contains('cd-input-error'), 'Input should have error class');
            
            // Clean up to prevent hanging
            const cancelBtn = document.getElementById('cdCancel');
            if (cancelBtn) cancelBtn.click();
        });
        r();
    });

    await new Promise(r => {
        test('TransitionDialog validation blocks epsilon for DFA/NFA', async () => {
            const dialog = new TransitionDialog(document.body);
            const sourceNode = { data: () => 'q1' };
            const targetNode = { data: () => 'q2' };

            let resolved = false;
            // Using DFA - should block epsilon
            const promise = dialog.show(sourceNode, targetNode, 'DFA').then(() => { resolved = true; });
            
            await new Promise(r => setTimeout(r, 100));

            const symbolInput = document.getElementById('cdSymbol');
            symbolInput.value = 'ε'; 
            
            const confirmBtn = document.getElementById('cdConfirm');
            confirmBtn.click();

            await new Promise(r => setTimeout(r, 100));

            assert.ok(!resolved, 'Promise should not resolve if epsilon is used for DFA/NFA');
            assert.ok(symbolInput.classList.contains('cd-input-error'), 'Input should have error class');
            
            // Clean up
            const cancelBtn = document.getElementById('cdCancel');
            if (cancelBtn) cancelBtn.click();
        });
        r();
    });

    console.log(`\nTransitionsEditingTests: ${passed} passed, ${failed} failed\n`);

    if (failed > 0) process.exit(1);
    else process.exit(0);
}

runTests();
