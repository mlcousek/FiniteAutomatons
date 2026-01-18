// Modal handling for Generate Automaton
(function() {
    const modal = document.getElementById('generateModal');
    const btn = document.getElementById('generateModalBtn');
    const close = document.getElementById('generateModalClose');
    const toggles = document.querySelectorAll('.accordion-toggle');

    if (btn && modal) {
        btn.addEventListener('click', () => { modal.style.display = 'block'; });
    }
    if (close && modal) {
        close.addEventListener('click', () => { modal.style.display = 'none'; });
    }
    window.addEventListener('click', (e) => { if (e.target === modal) modal.style.display = 'none'; });

    toggles.forEach(t => {
        t.addEventListener('click', () => {
            const panel = t.nextElementSibling;
            if (!panel) return;
            panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
        });
    });
})();
