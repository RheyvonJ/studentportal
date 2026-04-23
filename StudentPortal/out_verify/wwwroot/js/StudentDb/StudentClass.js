// StudentClass page interactions for shared student layout
const backButton = document.querySelector('.back-button');
const contentCards = document.querySelectorAll('.content-card');
const shell = document.querySelector('.student-class-shell');
const toast = document.getElementById('toast');

function showToast(message) {
    if (!toast || !message) return;
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

function messageForStudentClassTarget(url, card) {
    if (typeof window.resolveStudentNavToastMessage === 'function' && url) {
        const m = window.resolveStudentNavToastMessage(url);
        if (m && m !== 'Opening...') return m;
    }
    if (card && card.classList.contains('material')) return 'Opening Material';
    if (card && card.classList.contains('task')) return 'Opening Task';
    if (card && card.classList.contains('assessment')) return 'Opening Assessment';
    if (card && card.classList.contains('meeting')) return 'Opening Meeting';
    if (card && card.classList.contains('announcement')) return 'Opening Announcement';
    return 'Opening Class';
}

function navigateWithStudentLoading(url, message, delay) {
    if (!url) return;
    if (delay == null) delay = 600;
    if (typeof window.navigateWithProfessorLoading === 'function') {
        window.navigateWithProfessorLoading(url, message || 'Opening...', delay);
        return;
    }
    if (typeof window.ensureToastInBodyForPortal === 'function') {
        window.ensureToastInBodyForPortal();
    }
    showToast(message || 'Opening...');
    if (typeof window.setProfessorDbShellLoading === 'function') {
        window.setProfessorDbShellLoading(true);
    } else {
        const s = document.querySelector('.slsp-shell');
        if (s) s.classList.add('is-loading');
    }
    setTimeout(() => { window.location.href = url; }, delay);
}

// Run entry animation only when arriving from class selection.
try {
    if (sessionStorage.getItem('studentClassTransition') === '1' && shell) {
        shell.classList.add('class-transition-enter');
        setTimeout(() => shell.classList.remove('class-transition-enter'), 420);
    }
    sessionStorage.removeItem('studentClassTransition');
} catch (_) {}

backButton?.addEventListener('click', () => {
    const dash = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
    navigateWithStudentLoading(dash, 'Redirecting to Dashboard', 600);
});

contentCards.forEach((card) => {
    const isAnnouncement = card.classList.contains('announcement');
    const targetUrl = card.dataset.target;
    if (!targetUrl || isAnnouncement) {
        card.style.cursor = 'default';
        return;
    }
    card.addEventListener('click', () => {
        const msg = messageForStudentClassTarget(targetUrl, card);
        navigateWithStudentLoading(targetUrl, msg, 600);
    });
    card.style.cursor = 'pointer';
});
