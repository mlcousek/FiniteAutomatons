/**
 * Canvas initialization and management for Automaton Graph
 * This file is prepared for future integration with canvas libraries (Fabric.js, Konva.js, etc.)
 */

(function () {
    'use strict';

    // Canvas state
    let canvas = null;
    let ctx = null;
    let canvasWrapper = null;
    let nodes = [];
    let transitions = [];

    function initCanvas() {
        canvas = document.getElementById('automatonCanvas');
        if (!canvas) {
            console.warn('Canvas element not found');
            return;
        }

        canvasWrapper = canvas.parentElement;
        ctx = canvas.getContext('2d');

        resizeCanvas();

        window.addEventListener('resize', resizeCanvas);

        readModelFromForm();
        renderCanvas();
    }

    function resizeCanvas() {
        if (!canvas || !canvasWrapper) return;
        const rect = canvasWrapper.getBoundingClientRect();
        canvas.width = Math.max(300, rect.width - 32);
        canvas.height = Math.max(200, rect.height - 32);

        layoutNodes();
        renderCanvas();
    }

    function readModelFromForm() {
        nodes = [];
        transitions = [];

        const stateIndexEls = Array.from(document.querySelectorAll('input[name="States.Index"]'));
        const stateIndices = stateIndexEls.map(e => e.value);
        for (const idx of stateIndices) {
            const idEl = document.querySelector('input[name="States[' + idx + '].Id"]');
            const isStartEl = document.querySelector('input[name="States[' + idx + '].IsStart"]');
            const isAcceptEl = document.querySelector('input[name="States[' + idx + '].IsAccepting"]');
            if (!idEl) continue;
            const id = parseInt(idEl.value, 10);
            const isStart = isStartEl ? String(isStartEl.value).toLowerCase() === 'true' : false;
            const isAccepting = isAcceptEl ? String(isAcceptEl.value).toLowerCase() === 'true' : false;
            nodes.push({ id, isStart, isAccepting, x: 0, y: 0, r: 24 });
        }

        const transIndexEls = Array.from(document.querySelectorAll('input[name="Transitions.Index"]'));
        const transIndices = transIndexEls.map(e => e.value);
        for (const idx of transIndices) {
            const fromEl = document.querySelector('input[name="Transitions[' + idx + '].FromStateId"]');
            const toEl = document.querySelector('input[name="Transitions[' + idx + '].ToStateId"]');
            const symEl = document.querySelector('input[name="Transitions[' + idx + '].Symbol"]');
            if (!fromEl || !toEl) continue;
            const from = parseInt(fromEl.value, 10);
            const to = parseInt(toEl.value, 10);
            const symbol = symEl ? symEl.value : '';
            transitions.push({ from, to, symbol });
        }

        layoutNodes();
    }

    function layoutNodes() {
        if (!canvas) return;
        if (nodes.length === 0) return;
        const margin = 40;
        const availableWidth = canvas.width - margin * 2;
        const gap = availableWidth / Math.max(1, nodes.length - 1);
        const centerY = canvas.height / 2;
        for (let i = 0; i < nodes.length; i++) {
            nodes[i].x = margin + i * gap;
            nodes[i].y = centerY;
            nodes[i].r = Math.min(28, Math.max(18, canvas.width / 60));
        }
    }

    function findNodeById(id) {
        return nodes.find(n => n.id === id);
    }

    function renderCanvas(highlightStateId) {
        if (!ctx || !canvas) return;
        readModelFromForm();

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        transitions.forEach(t => drawTransition(t, false));

        nodes.forEach(n => drawState(n, n.id === parseInt(highlightStateId || '0', 10)));

        if (highlightStateId) {
            const sid = parseInt(highlightStateId, 10);
            const outs = transitions.filter(t => t.from === sid);
            outs.forEach(t => drawTransition(t, true));
        }
    }

    function drawState(node, highlighted) {
        const x = node.x, y = node.y, r = node.r;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fillStyle = '#fff';
        ctx.fill();
        ctx.lineWidth = highlighted ? 4 : 2;
        ctx.strokeStyle = highlighted ? '#2a9d8f' : '#333';
        ctx.stroke();
        if (node.isAccepting) {
            ctx.beginPath();
            ctx.arc(x, y, r - 6, 0, Math.PI * 2);
            ctx.strokeStyle = highlighted ? '#2a9d8f' : '#333';
            ctx.lineWidth = highlighted ? 3 : 1.5;
            ctx.stroke();
        }
        ctx.fillStyle = '#000';
        ctx.font = (r * 0.7) + 'px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('q' + node.id, x, y);

        if (node.isStart) {
            ctx.beginPath();
            ctx.moveTo(x - r - 12, y);
            ctx.lineTo(x - r, y - 8);
            ctx.lineTo(x - r, y + 8);
            ctx.closePath();
            ctx.fillStyle = '#333';
            ctx.fill();
        }
    }

    function drawTransition(t, highlighted) {
        const fromNode = findNodeById(t.from);
        const toNode = findNodeById(t.to);
        if (!fromNode || !toNode) return;
        const { x: x1, y: y1, r: r1 } = fromNode;
        const { x: x2, y: y2, r: r2 } = toNode;
        const angle = Math.atan2(y2 - y1, x2 - x1);
        const startX = x1 + Math.cos(angle) * r1;
        const startY = y1 + Math.sin(angle) * r1;
        const endX = x2 - Math.cos(angle) * r2;
        const endY = y2 - Math.sin(angle) * r2;

        ctx.beginPath();
        ctx.moveTo(startX, startY);
        ctx.lineTo(endX, endY);
        ctx.strokeStyle = highlighted ? '#f28c28' : '#555';
        ctx.lineWidth = highlighted ? 3 : 1.5;
        ctx.stroke();

        const headlen = 8;
        const hx = endX - Math.cos(angle) * headlen;
        const hy = endY - Math.sin(angle) * headlen;
        ctx.beginPath();
        ctx.moveTo(endX, endY);
        ctx.lineTo(hx + Math.sin(angle) * 4, hy - Math.cos(angle) * 4);
        ctx.lineTo(hx - Math.sin(angle) * 4, hy + Math.cos(angle) * 4);
        ctx.closePath();
        ctx.fillStyle = highlighted ? '#f28c28' : '#555';
        ctx.fill();

        const midX = (startX + endX) / 2;
        const midY = (startY + endY) / 2;
        ctx.fillStyle = '#000';
        ctx.font = '12px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        const sym = t.symbol === '\0' ? 'ε' : (t.symbol || '');
        ctx.fillText(sym, midX, midY - 10);
    }

    window.AutomatonCanvas = {
        init: initCanvas,
        resize: resizeCanvas,
        render: renderCanvas
    };

    window.highlightAutomatonExecution = function (currentStateId) {
        try {
            renderCanvas(currentStateId);
        } catch (e) {
            console.warn('highlightAutomatonExecution failed', e);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCanvas);
    } else {
        initCanvas();
    }
})();
