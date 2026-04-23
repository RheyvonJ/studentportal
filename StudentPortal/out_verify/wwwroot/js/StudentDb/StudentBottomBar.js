function setupStudentBottomBar() {
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const backButton = document.querySelector('.back-button');
    const data = window.studentBottomBarData || {};
    const classCode = data.classCode || document.body.dataset?.classCode || '';
    const currentPage = data.currentPage || '';

    // Popup
    userProfile?.addEventListener('click', (e) => {
        e.stopPropagation();
        userPopup?.classList.toggle('show');
    });
    document.addEventListener('click', (e) => {
        if (!userProfile?.contains(e.target)) userPopup?.classList.remove('show');
    });

    // Exit • Sign out
    document.getElementById('signOutBtn')?.addEventListener('click', (e) => {
        e.stopPropagation();
        console.log('Student logout clicked');
        window.showToast ? window.showToast('Signing out...') : null;
        userPopup?.classList.remove('show');
        setTimeout(() => window.location.href = '/Account/Logout', 300);
    });

    // Delegated fallback
    document.addEventListener('click', (e) => {
        const target = e.target;
        if (target && (target.id === 'signOutBtn' || (target.closest && target.closest('#signOutBtn')))) {
            e.stopPropagation();
            console.log('Student logout clicked (delegated)');
            window.showToast ? window.showToast('Signing out...') : null;
            userPopup?.classList.remove('show');
            setTimeout(() => window.location.href = '/Account/Logout', 300);
        }
    }, true);

    let menuOpen = false;
    menuCircle?.addEventListener('click', () => {
        menuOpen = !menuOpen;
        radialActions?.classList.toggle('show', menuOpen);
        menuCircle?.classList.toggle('open', menuOpen);
    });

    function setActive(page) {
        actions.forEach(a => a.classList.toggle('selected', a.dataset.page === page));
    }
    setActive(currentPage);

    actions.forEach(action => {
        action.addEventListener('click', () => {
            if (action.classList.contains('locked')) {
                window.showToast ? window.showToast('Coming soon') : null;
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }
            const page = action.dataset.page;
            const icon = action.querySelector('i');
            if (!icon) return;

            if (
                (icon.classList.contains('fa-house') && currentPage === 'home') ||
                (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
                (icon.classList.contains('fa-pencil') && currentPage === 'todo')
            ) {
                window.showToast ? window.showToast("You're already here.") : null;
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }

            setActive(page);

            if (icon.classList.contains('fa-house')) {
                const d = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
                navigateWithAnimation(d, 'Going to dashboard...');
            } else if (icon.classList.contains('fa-book')) {
                const d = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
                navigateWithAnimation(d, 'Opening classes...');
            } else if (icon.classList.contains('fa-pencil')) {
                navigateWithAnimation('/StudentTodo', 'Opening to-do...');
            }
        });
    });

    backButton?.addEventListener('click', () => {
        const path = (window.location.pathname || '').toLowerCase();
        const isStudentClassPage = /\/studentclass(\/|$)/i.test(path);
        const isSubjectChildPage = [
            /\/studenttask/i,
            /\/studentassessment/i,
            /\/studentmaterial/i,
            /\/studentanswerassessment/i,
            /\/studentannouncement/i
        ].some(rx => rx.test(path));
        let classCode = '';
        const codeInput = document.querySelector('input[name="classCode"]');
        if (codeInput && codeInput.value) classCode = codeInput.value;
        if (!classCode) {
            const openQuiz = document.getElementById('openQuiz');
            const attr = openQuiz && openQuiz.getAttribute('data-class-code');
            if (attr) classCode = attr;
        }
        if (!classCode) {
            const parts = (window.location.pathname || '').split('/').filter(Boolean);
            const pages = ['studenttask','studentassessment','studentmaterial','studentanswerassessment','studentannouncement'];
            const idx = parts.findIndex(p => pages.includes(p.toLowerCase()));
            if (idx >= 0 && parts.length >= idx + 2) classCode = parts[idx + 1];
        }
        const dash = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
        const target = isStudentClassPage
            ? dash
            : (isSubjectChildPage ? (classCode ? `/StudentClass/${encodeURIComponent(classCode)}` : dash) : dash);
        window.showToast ? window.showToast('Going back...') : null;
        setTimeout(() => { window.location.href = target; }, 500);
    });

    function navigateWithAnimation(url, message) {
        window.showToast ? window.showToast(message) : null;
        radialActions?.classList.remove('show');
        menuOpen = false;
        setTimeout(() => window.location.href = url, 600);
    }
}
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupStudentBottomBar);
} else {
    setupStudentBottomBar();
}
