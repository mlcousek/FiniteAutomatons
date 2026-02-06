// Export Automaton Modal Handler
document.addEventListener('DOMContentLoaded', () => {
    const exportModal = document.getElementById('exportAutomatonModal');
    if (!exportModal) return;

    let currentAutomatonId = null;
    let currentAutomatonName = '';

    exportModal.addEventListener('show.bs.modal', (event) => {
        const button = event.relatedTarget;
        currentAutomatonId = button.getAttribute('data-automaton-id');
        currentAutomatonName = button.getAttribute('data-automaton-name') || 'automaton';

        const modalTitle = exportModal.querySelector('#exportModalLabel');
        if (modalTitle) {
            modalTitle.textContent = `Export "${currentAutomatonName}"`;
        }
    });

    // Handle export format selection
    const exportButtons = exportModal.querySelectorAll('[data-export-format]');
    exportButtons.forEach(button => {
        button.addEventListener('click', () => {
            const format = button.getAttribute('data-export-format');
            if (!currentAutomatonId) return;

            let url;
            switch (format) {
                case 'structure-json':
                    url = `/ImportExport/ExportSaved?id=${currentAutomatonId}&format=json&mode=structure`;
                    break;
                case 'structure-txt':
                    url = `/ImportExport/ExportSaved?id=${currentAutomatonId}&format=txt&mode=structure`;
                    break;
                case 'input':
                    url = `/ImportExport/ExportSaved?id=${currentAutomatonId}&format=json&mode=input`;
                    break;
                case 'state':
                    url = `/ImportExport/ExportSaved?id=${currentAutomatonId}&format=json&mode=state`;
                    break;
                default:
                    console.error('Unknown export format:', format);
                    return;
            }

            // Trigger download
            window.location.href = url;

            // Close modal
            const modalInstance = bootstrap.Modal.getInstance(exportModal);
            if (modalInstance) {
                modalInstance.hide();
            }
        });
    });
});
