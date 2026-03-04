
(function(){
    const openBtn = document.getElementById('openSaveModalBtn');
    const modal = document.getElementById('saveAutomatonModal');
    const closeBtn = document.getElementById('saveModalClose');
    const cancelBtn = document.getElementById('saveModalCancel');
    const form = document.getElementById('saveAutomatonForm');
    if (!openBtn || !modal) return;

    const structureRow   = document.getElementById('saveOptionStructureRow');
    const inputRow       = document.getElementById('saveOptionInputRow');
    const stateRow       = document.getElementById('saveOptionStateRow');
    const structureHint  = document.getElementById('saveStructureOnlyHint');

    const serverHasExecutedEl = document.getElementById('serverHasExecuted');
    const serverHasExecuted   = serverHasExecutedEl?.value === 'true';

    function getLiveInput() {
        const el = document.getElementById('inputField');
        return el ? el.value.trim() : '';
    }

    function updateSaveOptions() {
        const liveInput = getLiveInput();
        const hiddenInputEl = document.getElementById('saveInputField');
        const serverInput = hiddenInputEl ? hiddenInputEl.value.trim() : '';
        const hasInput = liveInput.length > 0 || serverInput.length > 0;

        if (inputRow)  inputRow.style.display  = hasInput ? ''       : 'none';
        if (stateRow)  stateRow.style.display   = serverHasExecuted ? '' : 'none';
        if (structureHint) structureHint.style.display = (!hasInput && !serverHasExecuted) ? '' : 'none';

        const checkedRadio = form ? form.querySelector('input[name="saveMode"]:checked') : null;
        if (checkedRadio) {
            const row = checkedRadio.closest('[id^="saveOption"]');
            if (row && row.style.display === 'none') {
                const structureRadio = document.getElementById('saveModeStructure');
                if (structureRadio) structureRadio.checked = true;
            }
        }
    }

    const openModal = () => {
        const liveInput = getLiveInput();
        const hiddenInputEl = document.getElementById('saveInputField');
        if (hiddenInputEl && liveInput.length > 0) {
            hiddenInputEl.value = liveInput;
        }

        updateSaveOptions();

        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
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

    window.addEventListener('click', (e) => {
        if (e.target === modal) closeModal();
    });

    window.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && modal.style.display === 'flex') closeModal();
    });

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

    function captureStartStateThumbnail(cy) {
        const activeNodes = cy.nodes('.active-state, .current-state, .active-node, .current-node');
        activeNodes.removeClass('active-state current-state active-node current-node');
        let dataUrl = null;
        try {
            dataUrl = cy.png({ output: 'base64uri', scale: 1.5, full: true, maxWidth: 800, maxHeight: 600, bg: '#ffffff' });
        } finally {
            activeNodes.forEach(n => {
                n.addClass('active-state current-state');
            });
        }
        return dataUrl;
    }

    form?.addEventListener('submit', function(e){
        const liveInput = getLiveInput();
        const hiddenInputEl = document.getElementById('saveInputField');
        if (hiddenInputEl && liveInput.length > 0) {
            hiddenInputEl.value = liveInput;
        }

        let modeEl = form.querySelector('input[name="saveMode"]:checked');
        if (!modeEl) modeEl = form.querySelector('input[name="saveMode"]');
        const mode = modeEl?.value ?? 'structure';

        const saveStateField = document.getElementById('saveStateField');
        if (saveStateField) {
            saveStateField.value = (mode === 'state') ? 'true' : 'false';
        }

        try {
            const layoutJsonField = document.getElementById('saveLayoutJsonField');
            const thumbnailField  = document.getElementById('saveThumbnailField');

            if (typeof window.getCanvasInstance === 'function') {
                const canvasInstance = window.getCanvasInstance();
                const cy = canvasInstance?.getCytoscapeInstance?.();

                if (cy) {
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

                    if (thumbnailField) {
                        try {
                            let dataUrl;
                            if (mode === 'input') {
                                dataUrl = captureStartStateThumbnail(cy);
                            } else {
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
            console.warn('Could not capture canvas state for save:', err);
        }
    });
})();
