// Modal handling for Generate Automaton
(function() {
    const modal = document.getElementById('generateModal');
    const btn = document.getElementById('generateModalBtn');
    const close = document.getElementById('generateModalClose');
    const toggles = document.querySelectorAll('.accordion-toggle');

    const openModal = () => {
        if (!modal) return;
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden'; // prevent background scroll
    };

    const closeModal = () => {
        if (!modal) return;
        modal.style.display = 'none';
        document.body.style.overflow = ''; // restore
    };

    if (btn && modal) {
        btn.addEventListener('click', openModal);
    }
    if (close && modal) {
        close.addEventListener('click', closeModal);
    }
    window.addEventListener('click', (e) => { if (e.target === modal) closeModal(); });
    window.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeModal(); });

    toggles.forEach(t => {
        t.addEventListener('click', () => {
            const panel = t.nextElementSibling;
            if (!panel) return;
            panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
        });
    });
})();
