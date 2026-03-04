/**
 * CanvasInteractionHandler.js
 * Handles user interactions with the canvas (pan, zoom, selection, etc.)
 * Provides enhanced interaction capabilities beyond default Cytoscape behavior
 * 
 * @module CanvasInteractionHandler
 */

/**
 * Class for handling canvas interactions
 */
export class CanvasInteractionHandler {
    /**
     * @param {Object} cy - Cytoscape instance
     * @param {Object} options - Configuration options
     */
    constructor(cy, options = {}) {
        this.cy = cy;
        this.options = {
            enablePan: options.enablePan ?? true,
            enableZoom: options.enableZoom ?? true,
            enableBoxSelection: options.enableBoxSelection ?? false,
            enableTooltips: options.enableTooltips ?? true,
            panOnDrag: options.panOnDrag ?? true,
            zoomOnWheel: options.zoomOnWheel ?? false,
            doubleClickZoom: options.doubleClickZoom ?? true,
            ...options
        };

        this.isEnabled = false;
        this.isPanning = false;
        this.panStartPosition = null;
        this.tooltipElement = null;

        // Bind methods
        this._handleMouseDown = this._handleMouseDown.bind(this);
        this._handleMouseMove = this._handleMouseMove.bind(this);
        this._handleMouseUp = this._handleMouseUp.bind(this);
        this._handleWheel = this._handleWheel.bind(this);
        this._handleDoubleClick = this._handleDoubleClick.bind(this);
        this._handleKeyDown = this._handleKeyDown.bind(this);
    }

    /**
     * Enable interaction handlers
     */
    enable() {
        if (this.isEnabled) return;

        // Enable Cytoscape built-in interactions
        this.cy.userPanningEnabled(this.options.enablePan);
        this.cy.userZoomingEnabled(this.options.enableZoom);
        this.cy.boxSelectionEnabled(this.options.enableBoxSelection);

        // Set up custom event listeners
        this._setupEventListeners();

        // Create tooltip element
        if (this.options.enableTooltips) {
            this._createTooltip();
        }

        this.isEnabled = true;
        console.log('Canvas interactions enabled');
    }

    /**
     * Disable interaction handlers
     */
    disable() {
        if (!this.isEnabled) return;

        // Disable Cytoscape interactions
        this.cy.userPanningEnabled(false);
        this.cy.userZoomingEnabled(false);

        // Remove custom event listeners
        this._removeEventListeners();

        // Remove tooltip
        if (this.tooltipElement) {
            this.tooltipElement.remove();
            this.tooltipElement = null;
        }

        this.isEnabled = false;
        console.log('Canvas interactions disabled');
    }

    /**
     * Private: Set up event listeners
     * @private
     */
    _setupEventListeners() {
        // Mouse events for panning
        if (this.options.panOnDrag) {
            this.cy.on('mousedown', this._handleMouseDown);
            this.cy.on('mousemove', this._handleMouseMove);
            this.cy.on('mouseup', this._handleMouseUp);
        }

        // Wheel event for zooming
        if (this.options.zoomOnWheel) {
            const container = this.cy.container();
            container.addEventListener('wheel', this._handleWheel, { passive: false });
        }

        // Double-click zoom
        if (this.options.doubleClickZoom) {
            this.cy.on('doubleTap', this._handleDoubleClick);
        }

        // Keyboard shortcuts
        document.addEventListener('keydown', this._handleKeyDown);

        // Tooltip events
        if (this.options.enableTooltips) {
            this.cy.on('mouseover', 'node', this._showNodeTooltip.bind(this));
            this.cy.on('mouseout', 'node', this._hideTooltip.bind(this));
            this.cy.on('mouseover', 'edge', this._showEdgeTooltip.bind(this));
            this.cy.on('mouseout', 'edge', this._hideTooltip.bind(this));
        }

        // Note: Cytoscape handles cursor styles internally, so we don't need to set them manually
    }

    /**
     * Private: Remove event listeners
     * @private
     */
    _removeEventListeners() {
        this.cy.off('mousedown', this._handleMouseDown);
        this.cy.off('mousemove', this._handleMouseMove);
        this.cy.off('mouseup', this._handleMouseUp);
        this.cy.off('doubleTap', this._handleDoubleClick);

        const container = this.cy.container();
        container.removeEventListener('wheel', this._handleWheel);

        document.removeEventListener('keydown', this._handleKeyDown);

        this.cy.off('mouseover', 'node');
        this.cy.off('mouseout', 'node');
        this.cy.off('mouseover', 'edge');
        this.cy.off('mouseout', 'edge');
    }

    /**
     * Private: Handle mouse down (start panning)
     * @private
     */
    _handleMouseDown(evt) {
        // Only pan when clicking on background (not on nodes/edges)
        if (evt.target === this.cy) {
            this.isPanning = true;
            this.panStartPosition = {
                x: evt.originalEvent.clientX,
                y: evt.originalEvent.clientY,
                pan: { ...this.cy.pan() }
            };
            this.cy.container().style.cursor = 'grabbing';
        }
    }

    /**
     * Private: Handle mouse move (panning)
     * @private
     */
    _handleMouseMove(evt) {
        if (this.isPanning && this.panStartPosition) {
            const dx = evt.originalEvent.clientX - this.panStartPosition.x;
            const dy = evt.originalEvent.clientY - this.panStartPosition.y;

            this.cy.pan({
                x: this.panStartPosition.pan.x + dx,
                y: this.panStartPosition.pan.y + dy
            });
        }
    }

    /**
     * Private: Handle mouse up (stop panning)
     * @private
     */
    _handleMouseUp() {
        if (this.isPanning) {
            this.isPanning = false;
            this.panStartPosition = null;
            this.cy.container().style.cursor = 'default';
        }
    }

    /**
     * Private: Handle mouse wheel (zooming)
     * @private
     */
    _handleWheel(evt) {
        // Ignore wheel events that occur outside the canvas container bounds.
        // In some layouts an invisible overlay or adjacent element can cause
        // wheel events to be dispatched to ancestor elements; ensure the
        // pointer is actually over the canvas before acting.
        const container = this.cy.container();
        const rect = container.getBoundingClientRect();

        // If clientX/Y are not present (rare), fall back to handling the event.
        if (typeof evt.clientX === 'number' && typeof evt.clientY === 'number') {
            if (evt.clientX < rect.left || evt.clientX > rect.right || evt.clientY < rect.top || evt.clientY > rect.bottom) {
                return; // ignore wheel outside container
            }
        }

        evt.preventDefault();

        const mouseX = (evt.clientX - rect.left) || (container.clientWidth / 2);
        const mouseY = (evt.clientY - rect.top) || (container.clientHeight / 2);

        const zoom = this.cy.zoom();
        const deltaZoom = evt.deltaY > 0 ? 0.9 : 1.1;
        const newZoom = zoom * deltaZoom;

        // Zoom towards mouse position
        this.cy.zoom({
            level: newZoom,
            renderedPosition: { x: mouseX, y: mouseY }
        });
    }

    /**
     * Private: Handle double-click (zoom to node)
     * @private
     */
    _handleDoubleClick(evt) {
        // Skip double-click zoom on nodes if they have a specific handler (like in edit mode)
        // This prevents conflicts with StateEditor's multi-click feature
        if (evt.target.isNode && evt.target.isNode()) {
            // Node double-click - don't zoom, let StateEditor handle it
            return;
        }

        if (evt.target !== this.cy) {
            // Zoom to clicked element (edges, etc.)
            this.cy.animate({
                fit: {
                    eles: evt.target,
                    padding: 100
                },
                duration: 500,
                easing: 'ease-out'
            });
        }
    }

    /**
     * Private: Handle keyboard shortcuts
     * @private
     */
    _handleKeyDown(evt) {
        // Only handle if canvas container is focused or no input is focused
        const activeElement = document.activeElement;
        if (activeElement && (activeElement.tagName === 'INPUT' || activeElement.tagName === 'TEXTAREA')) {
            return;
        }

        switch (evt.key) {
            case '+':
            case '=':
                // Zoom in
                evt.preventDefault();
                this._zoomIn();
                break;
            case '-':
            case '_':
                // Zoom out
                evt.preventDefault();
                this._zoomOut();
                break;
            case '0':
                // Fit to view
                evt.preventDefault();
                this._fitToView();
                break;
            case 'ArrowUp':
                // Pan up
                evt.preventDefault();
                this._panBy(0, 50);
                break;
            case 'ArrowDown':
                // Pan down
                evt.preventDefault();
                this._panBy(0, -50);
                break;
            case 'ArrowLeft':
                // Pan left
                evt.preventDefault();
                this._panBy(50, 0);
                break;
            case 'ArrowRight':
                // Pan right
                evt.preventDefault();
                this._panBy(-50, 0);
                break;
        }
    }

    /**
     * Private: Zoom in
     * @private
     */
    _zoomIn() {
        const zoom = this.cy.zoom();
        const newZoom = Math.min(zoom * 1.2, this.cy.maxZoom());
        this.cy.zoom(newZoom);
    }

    /**
     * Private: Zoom out
     * @private
     */
    _zoomOut() {
        const zoom = this.cy.zoom();
        const newZoom = Math.max(zoom / 1.2, this.cy.minZoom());
        this.cy.zoom(newZoom);
    }

    /**
     * Private: Fit to view
     * @private
     */
    _fitToView() {
        this.cy.fit(this.cy.elements(), 50);
    }

    /**
     * Private: Pan by offset
     * @private
     */
    _panBy(dx, dy) {
        const pan = this.cy.pan();
        this.cy.pan({ x: pan.x + dx, y: pan.y + dy });
    }

    /**
     * Private: Create tooltip element
     * @private
     */
    _createTooltip() {
        this.tooltipElement = document.createElement('div');
        this.tooltipElement.className = 'canvas-tooltip';
        this.tooltipElement.style.cssText = `
            position: absolute;
            background: rgba(0, 0, 0, 0.85);
            color: white;
            padding: 6px 10px;
            border-radius: 4px;
            font-size: 12px;
            font-family: Arial, sans-serif;
            pointer-events: none;
            z-index: 9999;
            display: none;
            white-space: nowrap;
            box-shadow: 0 2px 8px rgba(0,0,0,0.3);
        `;
        document.body.appendChild(this.tooltipElement);
    }

    /**
     * Private: Show node tooltip
     * @private
     */
    _showNodeTooltip(evt) {
        if (!this.tooltipElement) return;

        const node = evt.target;
        const stateId = node.data('stateId');
        const isStart = node.data('isStart');
        const isAccepting = node.data('isAccepting');

        let text = `State q${stateId}`;
        const badges = [];
        if (isStart) badges.push('Start');
        if (isAccepting) badges.push('Accepting');
        if (badges.length > 0) {
            text += ` (${badges.join(', ')})`;
        }

        this._showTooltip(evt.originalEvent, text);
    }

    /**
     * Private: Show edge tooltip
     * @private
     */
    _showEdgeTooltip(evt) {
        if (!this.tooltipElement) return;

        const edge = evt.target;
        const label = edge.data('label');

        if (label) {
            this._showTooltip(evt.originalEvent, label);
        }
    }

    /**
     * Private: Show tooltip at position
     * @private
     */
    _showTooltip(event, text) {
        if (!this.tooltipElement) return;

        this.tooltipElement.textContent = text;
        this.tooltipElement.style.display = 'block';
        this.tooltipElement.style.left = (event.pageX + 15) + 'px';
        this.tooltipElement.style.top = (event.pageY + 15) + 'px';
    }

    /**
     * Private: Hide tooltip
     * @private
     */
    _hideTooltip() {
        if (this.tooltipElement) {
            this.tooltipElement.style.display = 'none';
        }
    }

    /**
     * Update options dynamically
     * @param {Object} newOptions - New options to merge
     */
    updateOptions(newOptions) {
        this.options = { ...this.options, ...newOptions };

        // Re-enable with new options
        if (this.isEnabled) {
            this.disable();
            this.enable();
        }
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.CanvasInteractionHandler = CanvasInteractionHandler;
}
