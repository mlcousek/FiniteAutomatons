// Handles opening the save modal, dynamically showing/hiding save options based on
// live input state, syncing live input into the hidden form field, and properly
// setting saveMode before form submission.
(function(){
    const openBtn = document.getElementById('openSaveModalBtn');
    const modal = document.getElementById('saveAutomatonModal');
    const closeBtn = document.getElementById('saveModalClose');
    const cancelBtn = document.getElementById('saveModalCancel');
    const form = document.getElementById('saveAutomatonForm');
    if (!openBtn || !modal) return;

    // ─── Option rows ───────────────────────────────────────────────────────────
    const structureRow   = document.getElementById('saveOptionStructureRow');
    const inputRow       = document.getElementById('saveOptionInputRow');
    const stateRow       = document.getElementById('saveOptionStateRow');
    const structureHint  = document.getElementById('saveStructureOnlyHint');

    // Server-rendered flag: was execution ever started (model.HasExecuted)?
    const serverHasExecutedEl = document.getElementById('serverHasExecuted');
    const serverHasExecuted   = serverHasExecutedEl?.value === 'true';

    // The live input text box on the main page
    function getLiveInput() {
        const el = document.getElementById('inputField');
        return el ? el.value.trim() : '';
    }

    // ─── Update visibility of save option rows ────────────────────────────────
    function updateSaveOptions() {
        const liveInput = getLiveInput();
        // Also check the hidden field in case input was loaded from server
        const hiddenInputEl = document.getElementById('saveInputField');
        const serverInput = hiddenInputEl ? hiddenInputEl.value.trim() : '';
        const hasInput = liveInput.length > 0 || serverInput.length > 0;

        // Show/hide rows
        if (inputRow)  inputRow.style.display  = hasInput ? ''       : 'none';
        if (stateRow)  stateRow.style.display   = serverHasExecuted ? '' : 'none';
        if (structureHint) structureHint.style.display = (!hasInput && !serverHasExecuted) ? '' : 'none';

        // If the currently checked radio is now hidden, fall back to "structure"
        const checkedRadio = form ? form.querySelector('input[name="saveMode"]:checked') : null;
        if (checkedRadio) {
            const row = checkedRadio.closest('[id^="saveOption"]');
            if (row && row.style.display === 'none') {
                const structureRadio = document.getElementById('saveModeStructure');
                if (structureRadio) structureRadio.checked = true;
            }
        }
    }

    // ─── Open / close ─────────────────────────────────────────────────────────
    const openModal = () => {
        // Sync live input into hidden field before showing options
        const liveInput = getLiveInput();
        const hiddenInputEl = document.getElementById('saveInputField');
        if (hiddenInputEl && liveInput.length > 0) {
            hiddenInputEl.value = liveInput;
        }

        updateSaveOptions();

        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        // force reflow for CSS animation
        modal.offsetHeight;
        modal.classList.add('modal-open');
    };

    const closeModal = () => {
        modal.classList.remove('modal-open');
        setTimeout(() => {
            modal.style.display = 'none';
            document.body.style.overflow = '';
        }, 200);
    };

    openBtn.addEventListener('click', openModal);
    closeBtn?.addEventListener('click', closeModal);
    cancelBtn?.addEventListener('click', closeModal);

    // Close on backdrop click
    window.addEventListener('click', (e) => {
        if (e.target === modal) closeModal();
    });

    // Close on Escape key
    window.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && modal.style.display === 'flex') closeModal();
    });

    // Add keyboard focus trap inside modal
    modal.addEventListener('keydown', (e) => {
        if (e.key === 'Tab') {
            const focusableElements = modal.querySelectorAll(
                'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
            );
            if (focusableElements.length === 0) return;
            const firstElement = focusableElements[0];
            const lastElement = focusableElements[focusableElements.length - 1];

            if (e.shiftKey && document.activeElement === firstElement) {
                e.preventDefault();
                lastElement.focus();
            } else if (!e.shiftKey && document.activeElement === lastElement) {
                e.preventDefault();
                firstElement.focus();
            }
        }
    });

    // ─── Capture starting-state thumbnail ─────────────────────────────────────
    // Temporarily removes Cytoscape active/current-state classes from nodes,
    // captures a PNG, then restores the classes.
    function captureStartStateThumbnail(cy) {
        // Collect nodes that currently have active-state styling
        const activeNodes = cy.nodes('.active-state, .current-state, .active-node, .current-node');
        activeNodes.removeClass('active-state current-state active-node current-node');
        let dataUrl = null;
        try {
            dataUrl = cy.png({ output: 'base64uri', scale: 1.5, full: true, maxWidth: 800, maxHeight: 600, bg: '#ffffff' });
        } finally {
            // Always restore
            activeNodes.forEach(n => {
                // Re-add whichever classes it originally had (we removed all, so restore all)
                n.addClass('active-state current-state');
            });
        }
        return dataUrl;
    }

    // ─── Before submit: set saveState + capture canvas ────────────────────────
    form?.addEventListener('submit', function(e){
        // Sync live input value one more time
        const liveInput = getLiveInput();
        const hiddenInputEl = document.getElementById('saveInputField');
        if (hiddenInputEl && liveInput.length > 0) {
            hiddenInputEl.value = liveInput;
        }

        // Determine selected save mode
        let modeEl = form.querySelector('input[name="saveMode"]:checked');
        if (!modeEl) modeEl = form.querySelector('input[name="saveMode"]');
        const mode = modeEl?.value ?? 'structure';

        // saveState hidden field for backward-compat path (only true for full state)
        const saveStateField = document.getElementById('saveStateField');
        if (saveStateField) {
            saveStateField.value = (mode === 'state') ? 'true' : 'false';
        }

        // Capture canvas layout + thumbnail
        try {
            const layoutJsonField = document.getElementById('saveLayoutJsonField');
            const thumbnailField  = document.getElementById('saveThumbnailField');

            if (typeof window.getCanvasInstance === 'function') {
                const canvasInstance = window.getCanvasInstance();
                const cy = canvasInstance?.getCytoscapeInstance?.();

                if (cy) {
                    // --- Layout positions (always) ---
                    if (layoutJsonField) {
                        const positions = {};
                        cy.nodes().forEach(node => {
                            if (!node.hasClass('dummy')) {
                                const pos = node.position();
                                positions[node.id()] = { x: Math.round(pos.x), y: Math.round(pos.y) };
                            }
                        });
                        if (Object.keys(positions).length > 0) {
                            layoutJsonField.value = JSON.stringify(positions);
                        }
                    }

                    // --- Thumbnail: for "input only", show automaton in start state ---
                    if (thumbnailField) {
                        try {
                            let dataUrl;
                            if (mode === 'input') {
                                // Capture with visual reset to start state
                                dataUrl = captureStartStateThumbnail(cy);
                            } else {
                                // Capture current visual state
                                dataUrl = cy.png({ output: 'base64uri', scale: 1.5, full: true, maxWidth: 800, maxHeight: 600, bg: '#ffffff' });
                            }
                            if (dataUrl) {
                                const base64 = dataUrl.startsWith('data:') ? dataUrl.split(',')[1] : dataUrl;
                                if (base64) thumbnailField.value = base64;
                            }
                        } catch (imgErr) {
                            console.warn('Could not capture thumbnail:', imgErr);
                        }
                    }
                }
            }
        } catch (err) {
            // Never block the save on capture failures
            console.warn('Could not capture canvas state for save:', err);
        }
    });
})();
