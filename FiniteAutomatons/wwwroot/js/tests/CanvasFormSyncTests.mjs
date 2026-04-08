import assert from 'assert';
import { CanvasFormSync } from '../canvas/CanvasFormSync.js';

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

function makeEdge(label, data = {}) {
    return {
        data: (key) => {
            if (key === 'label') return label;
            return data[key];
        }
    };
}

function runTests() {
    console.log('\nCanvasFormSync Unit Tests\n');

    test('parses grouped PDA format a (b/c)', () => {
        const sync = new CanvasFormSync({ automatonType: 'DPDA' });
        const edge = makeEdge('a (b/c)');

        const parsed = sync._extractRawSymbols(edge);

        assert.strictEqual(parsed.length, 1);
        assert.strictEqual(parsed[0].symbol, 'a');
        assert.strictEqual(parsed[0].stackPop, 'b');
        assert.strictEqual(parsed[0].stackPush, 'c');
    });

    test('parses grouped multi-line PDA format', () => {
        const sync = new CanvasFormSync({ automatonType: 'NPDA' });
        const edge = makeEdge('a (b/c)\nd (e/f)');

        const parsed = sync._extractRawSymbols(edge);

        assert.strictEqual(parsed.length, 2);
        assert.strictEqual(parsed[0].symbol, 'a');
        assert.strictEqual(parsed[0].stackPop, 'b');
        assert.strictEqual(parsed[0].stackPush, 'c');
        assert.strictEqual(parsed[1].symbol, 'd');
        assert.strictEqual(parsed[1].stackPop, 'e');
        assert.strictEqual(parsed[1].stackPush, 'f');
    });

    test('parses legacy PDA format a, b/c for backward compatibility', () => {
        const sync = new CanvasFormSync({ automatonType: 'DPDA' });
        const edge = makeEdge('a, b/c');

        const parsed = sync._extractRawSymbols(edge);

        assert.strictEqual(parsed.length, 1);
        assert.strictEqual(parsed[0].symbol, 'a');
        assert.strictEqual(parsed[0].stackPop, 'b');
        assert.strictEqual(parsed[0].stackPush, 'c');
    });

    test('uses explicit edge stack fields if label format is unexpected', () => {
        const sync = new CanvasFormSync({ automatonType: 'NPDA' });
        const edge = makeEdge('x ??? y', { stackPop: 'Z', stackPush: 'TT' });

        const parsed = sync._extractRawSymbols(edge);

        assert.strictEqual(parsed.length, 1);
        assert.strictEqual(parsed[0].symbol, 'x ??? y');
        assert.strictEqual(parsed[0].stackPop, 'Z');
        assert.strictEqual(parsed[0].stackPush, 'TT');
    });

    console.log(`\nCanvasFormSyncTests: ${passed} passed, ${failed} failed\n`);

    process.exit(failed > 0 ? 1 : 0);
}

runTests();
