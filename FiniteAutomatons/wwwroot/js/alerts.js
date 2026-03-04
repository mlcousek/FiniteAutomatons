// Auto-dismiss alerts after 5 seconds and allow click to dismiss
(function() {
    document.addEventListener('DOMContentLoaded', function() {
        const alerts = document.querySelectorAll('.alert');
        
        alerts.forEach(function(alert) {
            setTimeout(function() {
                dismissAlert(alert);
            }, 5000);

            alert.addEventListener('click', function() {
                dismissAlert(alert);
            });

            alert.style.cursor = 'pointer';
        });
    });
    
    function dismissAlert(alert) {
        alert.classList.add('alert-fade-out');

        setTimeout(function() {
            alert.remove();
        }, 400);
    }
})();
