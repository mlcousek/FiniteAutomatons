// Modal handling for Generate Input with modern animations
(function() {
    const modal = document.getElementById('generateInputModal');
    const btn = document.getElementById('generateInputModalBtn');
    const close = document.getElementById('generateInputModalClose');

    const openModal = () => {
        if (!modal) return;
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        
        // Trigger reflow for animation
        modal.offsetHeight;
        modal.classList.add('modal-open');
    };

    const closeModal = () => {
        if (!modal) return;
        modal.classList.remove('modal-open');
        
        // Wait for animation to complete
        setTimeout(() => {
            modal.style.display = 'none';
            document.body.style.overflow = '';
        }, 200);
    };

    if (btn && modal) {
        btn.addEventListener('click', openModal);
    }
    
    if (close && modal) {
        close.addEventListener('click', closeModal);
    }
    
    // Close on backdrop click
    window.addEventListener('click', (e) => { 
        if (e.target === modal) closeModal(); 
    });
    
    // Close on Escape key
    window.addEventListener('keydown', (e) => { 
        if (e.key === 'Escape' && modal.style.display === 'flex') closeModal(); 
    });
    
    // Add keyboard focus trap
    if (modal) {
        modal.addEventListener('keydown', (e) => {
            if (e.key === 'Tab') {
                const focusableElements = modal.querySelectorAll(
                    'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
                );
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
    }
})();

