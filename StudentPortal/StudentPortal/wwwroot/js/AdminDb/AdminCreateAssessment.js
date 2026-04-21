// Create Assessment page — back to professor dashboard
(function () {
    'use strict';
    document.getElementById('backBtn')?.addEventListener('click', function () {
        const u = this.getAttribute('data-dashboard-url') || '/professordb/ProfessorDb';
        window.location.href = u;
    });
})();
