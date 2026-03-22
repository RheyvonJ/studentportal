// StudentClass page interactions for shared student layout
const backButton = document.querySelector('.back-button');
const contentCards = document.querySelectorAll('.content-card');
const shell = document.querySelector('.student-class-shell');

// Run entry animation only when arriving from class selection.
try {
    if (sessionStorage.getItem('studentClassTransition') === '1' && shell) {
        shell.classList.add('class-transition-enter');
        setTimeout(() => shell.classList.remove('class-transition-enter'), 420);
    }
    sessionStorage.removeItem('studentClassTransition');
} catch (_) {}

backButton?.addEventListener('click', () => {
    window.location.href = '/StudentDb/StudentDb';
});

contentCards.forEach((card) => {
    card.addEventListener('click', () => {
        const targetUrl = card.dataset.target;
        if (!targetUrl) return;
        window.location.href = targetUrl;
    });
    card.style.cursor = 'pointer';
});
