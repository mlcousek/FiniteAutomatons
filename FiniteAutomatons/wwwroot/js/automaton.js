// automaton.js: Handles UI and drawing for the automaton simulator
// This is a placeholder for the interactive logic. The backend should provide endpoints for automaton logic.

const canvas = document.getElementById('automaton-canvas');
const ctx = canvas.getContext('2d');
let states = [];
let transitions = [];
let isDrawingTransition = false;
let fromState = null;

canvas.addEventListener('click', function(e) {
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    // Add a new state
    states.push({ x, y, id: states.length });
    draw();
});

document.getElementById('clearGraphBtn').onclick = function() {
    states = [];
    transitions = [];
    draw();
};

document.getElementById('addStateBtn').onclick = function() {
    // Next click on canvas will add a state
    isDrawingTransition = false;
};

document.getElementById('addTransitionBtn').onclick = function() {
    isDrawingTransition = true;
};

function draw() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    // Draw transitions
    transitions.forEach(t => {
        const from = states.find(s => s.id === t.from);
        const to = states.find(s => s.id === t.to);
        if (from && to) {
            ctx.beginPath();
            ctx.moveTo(from.x, from.y);
            ctx.lineTo(to.x, to.y);
            ctx.strokeStyle = '#888';
            ctx.stroke();
            ctx.closePath();
            // Draw symbol
            ctx.fillStyle = '#000';
            ctx.fillText(t.symbol, (from.x + to.x) / 2, (from.y + to.y) / 2);
        }
    });
    // Draw states
    states.forEach(s => {
        ctx.beginPath();
        ctx.arc(s.x, s.y, 25, 0, 2 * Math.PI);
        ctx.fillStyle = '#fff';
        ctx.fill();
        ctx.strokeStyle = '#000';
        ctx.stroke();
        ctx.closePath();
        ctx.fillStyle = '#000';
        ctx.fillText('q' + s.id, s.x - 10, s.y + 5);
    });
}

draw();

// TODO: Add logic for transitions, running automaton, step, minimize, load/save, etc.
