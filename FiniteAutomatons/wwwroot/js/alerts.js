// Auto-dismiss alerts after 5 seconds and allow click to dismiss
(function() {
    document.addEventListener('DOMContentLoaded', function() {
        const alerts = document.querySelectorAll('.alert');
        
        alerts.forEach(function(alert) {
            // Auto-dismiss after 5 seconds
            setTimeout(function() {
                dismissAlert(alert);
            }, 5000);
            
            // Click to dismiss immediately
            alert.addEventListener('click', function() {
                dismissAlert(alert);
            });
            
            // Add cursor pointer to indicate clickable
            alert.style.cursor = 'pointer';
        });
    });
    
    function dismissAlert(alert) {
        // Add fade-out class
        alert.classList.add('alert-fade-out');
        
        // Remove from DOM after animation completes
        setTimeout(function() {
            alert.remove();
        }, 400);
    }
})();
