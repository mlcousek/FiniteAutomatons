// Handles opening the save modal and setting save options
(function(){
    const openBtn = document.getElementById('openSaveModalBtn');
    const modal = document.getElementById('saveAutomatonModal');
    const closeBtn = document.getElementById('saveModalClose');
    const cancelBtn = document.getElementById('saveModalCancel');
    const form = document.getElementById('saveAutomatonForm');
    if (!openBtn || !modal) return;

    const openModal = () => {
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

    // set saveState hidden field based on selected option before submit
    form?.addEventListener('submit', function(e){
        // Prefer checked radio (when options shown), fallback to any input named saveMode (hidden input)
        let modeEl = form.querySelector('input[name="saveMode"]:checked');
        if (!modeEl) modeEl = form.querySelector('input[name="saveMode"]');
        const mode = modeEl?.value;
        const saveStateField = document.getElementById('saveStateField');
        if (mode === 'state' || mode === 'input') {
            saveStateField.value = 'true';
        } else {
            saveStateField.value = 'false';
        }

        // Capture current Cytoscape node positions and write them to the hidden layoutJson field,
        // and capture a PNG thumbnail and write it to the thumbnailBase64 field.
        try {
            const layoutJsonField = document.getElementById('saveLayoutJsonField');
            const thumbnailField = document.getElementById('saveThumbnailField');

            if (typeof window.getCanvasInstance === 'function') {
                const canvasInstance = window.getCanvasInstance();
                const cy = canvasInstance?.getCytoscapeInstance?.();

                if (cy) {
                    // --- Layout positions ---
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

                    // --- PNG thumbnail (white background) ---
                    if (thumbnailField) {
                        try {
                            const dataUrl = cy.png({ output: 'base64uri', scale: 1.5, full: true, maxWidth: 800, maxHeight: 600, bg: '#ffffff' });
                            const base64 = dataUrl.startsWith('data:') ? dataUrl.split(',')[1] : dataUrl;
                            if (base64) thumbnailField.value = base64;
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
