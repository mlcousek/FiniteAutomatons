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

    // Future: These will be managed by a canvas library
  let zoom = 1.0;
    let panX = 0;
    let panY = 0;

    /**
     * Initialize the canvas when DOM is ready
     */
    function initCanvas() {
        canvas = document.getElementById('automatonCanvas');
        if (!canvas) {
            console.warn('Canvas element not found');
            return;
        }

    canvasWrapper = canvas.parentElement;
    ctx = canvas.getContext('2d');

        // Set canvas size to match container
      resizeCanvas();

        // Add event listeners
  window.addEventListener('resize', resizeCanvas);

        // Initial render
        renderCanvas();

        console.log('Canvas initialized successfully');
    }

    /**
     * Resize canvas to match container size
     */
    function resizeCanvas() {
   if (!canvas || !canvasWrapper) return;

        const rect = canvasWrapper.getBoundingClientRect();
canvas.width = rect.width - 32; // Account for padding
        canvas.height = rect.height - 32;

        renderCanvas();
    }

    /**
     * Render the canvas
     * Future: This will be handled by a canvas library
     */
    function renderCanvas() {
        if (!ctx) return;

 // Clear canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Draw placeholder grid (will be removed when using a library)
        drawGrid();

        // Draw placeholder text
        drawPlaceholder();
    }

    /**
     * Draw a light grid on the canvas
     * This is just for visual reference and will be replaced
     */
    function drawGrid() {
        const gridSize = 50;
        ctx.strokeStyle = '#e0e0e0';
      ctx.lineWidth = 1;

        // Vertical lines
      for (let x = 0; x <= canvas.width; x += gridSize) {
       ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, canvas.height);
      ctx.stroke();
        }

        // Horizontal lines
    for (let y = 0; y <= canvas.height; y += gridSize) {
 ctx.beginPath();
     ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
            ctx.stroke();
        }
    }

    /**
     * Draw placeholder text in the center of canvas
     */
    function drawPlaceholder() {
        ctx.font = '20px Arial';
        ctx.fillStyle = '#999';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
     ctx.fillText(
          'Canvas ready for automaton visualization',
         canvas.width / 2,
      canvas.height / 2 - 20
        );
      ctx.font = '14px Arial';
        ctx.fillText(
      '(Will be interactive with zoom/pan capabilities)',
            canvas.width / 2,
            canvas.height / 2 + 20
   );
    }

    /**
     * Public API for future use
     */
  window.AutomatonCanvas = {
    init: initCanvas,
     resize: resizeCanvas,
        render: renderCanvas,
 // Future methods will be added here:
    // addState: function(x, y, label) {},
        // addTransition: function(from, to, symbol) {},
        // clear: function() {},
        // zoom: function(factor) {},
        // pan: function(dx, dy) {}
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCanvas);
    } else {
        initCanvas();
    }
})();
