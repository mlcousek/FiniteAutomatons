import assert from 'assert';
import { JSDOM } from 'jsdom';
import { AutomatonCanvas } from '../canvas/AutomatonCanvas.js';

const dom = new JSDOM('<!DOCTYPE html><html><body><div id="container"></div></body></html>');
global.window = dom.window;
global.document = dom.window.document;
global.CustomEvent = dom.window.CustomEvent;

// Mock cytoscape
const nodes = [];
const edges = [];
let styleArgs = [];
let classAdds = [];
let classRemoves = [];
let styleRemoves = [];

class MockElement {
    constructor(id) {
        this._id = id;
        this._classes = new Set();
        this._outgoers = [];
        this._style = {};
    }
    id() { return this._id; }
    addClass(cls) { this._classes.add(cls); classAdds.push({ id: this._id, cls }); }
    removeClass(cls) { this._classes.delete(cls); classRemoves.push({ id: this._id, cls }); }
    hasClass(cls) { return this._classes.has(cls); }
    style(prop, val) { 
        if (val === undefined) return this._style[prop];
        if (val === '') {
            delete this._style[prop];
            styleRemoves.push({ id: this._id, prop });
        } else {
            this._style[prop] = val;
            styleArgs.push({ id: this._id, prop, val });
        }
    }
    ungrabify() {}
    grabify() {}
    outgoers(selector) {
        if (selector === 'edge') return {
            forEach: (cb) => this._outgoers.forEach(cb),
            addClass: (cls) => this._outgoers.forEach(o => o.addClass(cls))
        };
        return this._outgoers;
    }
    _setOutgoers(outgoers) {
        this._outgoers = outgoers;
    }
}

const mockCy = {
    _nodes: [],
    _edges: [],
    getElementById(id) {
        return [...this._nodes, ...this._edges].find(e => e.id() === id) || null;
    },
    nodes() {
        return {
            removeClass: (cls) => this._nodes.forEach(n => n.removeClass(cls)),
            forEach: (cb) => this._nodes.forEach(cb)
        };
    },
    edges() {
        return {
            removeClass: (cls) => this._edges.forEach(n => n.removeClass(cls)),
            forEach: (cb) => this._edges.forEach(cb)
        };
    },
    style(newStyle) {},
    on() {},
    off() {},
    destroy() {}
};

global.cytoscape = function() {
    return mockCy;
};

// Dummy AutomatonRenderer
global.jest = { fn: () => {} };
export const AutomatonRenderer = {
    getStylesheet: () => []
};

// Provide minimum required mock exports if needed, or rely on internal canvas flow
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
    console.log('Running AutomatonCanvasHighlightTests...');

    test('AutomatonCanvas highlight adds branch colors for NFA with multiple active states', () => {
        const canvas = new AutomatonCanvas('container', { readOnly: true, useBranchColorsForNondeterminism: true });
        
        // By bypassing init's require to real files, we inject our mockCy directly
        canvas.cy = mockCy;
        canvas.isInitialized = true;
        canvas.automatonType = 'NFA';

        // Setup graph
        const node1 = new MockElement('state-1');
        const node2 = new MockElement('state-2');
        const edge1 = new MockElement('edge-1-3');
        const edge2 = new MockElement('edge-2-4');

        node1._setOutgoers([edge1]);
        node2._setOutgoers([edge2]);

        mockCy._nodes = [node1, node2];
        mockCy._edges = [edge1, edge2];

        classAdds = [];
        
        canvas.highlight([1, 2]);

        // node1 should get active-branch-0
        assert.ok(classAdds.some(a => a.id === 'state-1' && a.cls === 'active'), "state-1 should be active");
        assert.ok(classAdds.some(a => a.id === 'state-1' && a.cls === 'active-branch-0'), "state-1 should have active-branch-0");
        
        // edge1 should get active-branch-0
        assert.ok(classAdds.some(a => a.id === 'edge-1-3' && a.cls === 'active'), "edge-1-3 should be active");
        assert.ok(classAdds.some(a => a.id === 'edge-1-3' && a.cls === 'active-branch-0'), "edge-1-3 should have active-branch-0");

        // node2 should get active-branch-1
        assert.ok(classAdds.some(a => a.id === 'state-2' && a.cls === 'active-branch-1'), "state-2 should have active-branch-1");
        assert.ok(classAdds.some(a => a.id === 'edge-2-4' && a.cls === 'active-branch-1'), "edge-2-4 should have active-branch-1");
    });

    test('AutomatonCanvas highlight does NOT add branch colors if useBranchColorsForNondeterminism is false', () => {
        const canvas = new AutomatonCanvas('container', { readOnly: true, useBranchColorsForNondeterminism: false });
        
        canvas.cy = mockCy;
        canvas.isInitialized = true;
        canvas.automatonType = 'NFA';

        const node1 = new MockElement('state-1');
        const node2 = new MockElement('state-2');
        const edge1 = new MockElement('edge-1-3');

        node1._setOutgoers([edge1]);
        mockCy._nodes = [node1, node2];
        mockCy._edges = [edge1];

        classAdds = [];
        
        canvas.highlight([1, 2]);

        assert.ok(!classAdds.some(a => a.cls === 'active-branch-0'), "active-branch-0 should not be added");
        assert.ok(classAdds.some(a => a.id === 'state-1' && a.cls === 'active'), "state-1 should still be active");
    });

    console.log(`\nTests finished: ${passed} passed, ${failed} failed`);
    process.exit(failed > 0 ? 1 : 0);
}

runTests();
