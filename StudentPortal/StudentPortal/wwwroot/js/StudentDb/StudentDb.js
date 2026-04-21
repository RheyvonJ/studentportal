// ==========================================================
// StudentDb.js — handles UI interactions only
// Business logic moved to StudentDbController.cs
// ==========================================================

// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
const joinBtn = document.getElementById('joinBtn');
const modalBackdrop = document.getElementById('modalBackdrop');
const toast = document.getElementById('toast');
const requestJoinBtn = document.getElementById('requestJoin');
const classCodeInput = document.getElementById('classCode');

// --- RADIAL MENU TOGGLE ---
let menuOpen = false;
menuCircle?.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions.classList.toggle('show', menuOpen);
});

// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'home'; // default from server

function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

// --- RADIAL ACTION CLICK HANDLER ---
actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        const icon = action.querySelector('i');

        // --- VALIDATION: prevent navigating to same page ---
        if (
            (icon.classList.contains('fa-house') && currentPage === 'home') ||
            (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
            (icon.classList.contains('fa-pencil') && currentPage === 'todo')
        ) {
            showToast("🏠 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        // --- Otherwise navigate normally ---
        setActivePage(page);

        if (icon.classList.contains('fa-house')) {
            navigateWithAnimation('/studentdb/StudentDb', 'Going to dashboard...');
        }
        else if (icon.classList.contains('fa-book')) {
            navigateWithAnimation('/studentdb/StudentDb', 'Opening classes...');
        }
        else if (icon.classList.contains('fa-pencil')) {
            navigateWithAnimation('/StudentTodo', 'Opening to-do list...');
        }
    });
});

// --- NAVIGATION FUNCTION ---
function navigateWithAnimation(url, message) {
    showToast(message);
    radialActions.classList.remove('show');
    menuOpen = false;
    
    // Add global loading state for radial menu navigation
    const shell = document.querySelector('.slsp-shell');
    if (shell) shell.classList.add('is-loading');

    setTimeout(() => {
        window.location.href = url;
    }, 600);
}

// --- HIDE RADIAL WHEN CLICKING OUTSIDE ---
document.addEventListener('click', (e) => {
    if (!menuCircle.contains(e.target) && !radialActions.contains(e.target)) {
        menuOpen = false;
        radialActions.classList.remove('show');
    }
});

// --- JOIN CLASS MODAL ---
joinBtn?.addEventListener('click', () => {
    joinBtn.classList.add('selected');
    modalBackdrop.classList.add('show');
});

modalBackdrop?.addEventListener('click', (e) => {
    if (e.target === modalBackdrop) {
        modalBackdrop.classList.remove('show');
        joinBtn.classList.remove('selected');
    }
});

// --- REQUEST TO JOIN ---
const classContainer = document.getElementById('classContainer');

function navigateToClassWithTransition(classCode) {
    const url = `/StudentClass/${encodeURIComponent(classCode)}`;
    try { sessionStorage.setItem('studentClassTransition', '1'); } catch (_) {}

    if (typeof window.ensureToastInBodyForPortal === 'function') {
        window.ensureToastInBodyForPortal();
    }
    showToast('Opening Class');

    const shell = document.querySelector('.slsp-shell');
    const cards = document.querySelectorAll('.class-card');
    const reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!shell || reduced) {
        window.location.href = url;
        return;
    }

    // Trigger global loading state (grey-out + spinner)
    shell.classList.add('is-loading');

    // Minimal card-only animation to give tactile feedback
    const opts = { duration: 280, easing: 'cubic-bezier(.2,.9,.3,1)', fill: 'forwards' };
    try {
        const cardAnim = Array.from(cards).map((card, i) =>
            card.animate(
                [
                    { transform: 'translateY(0)', opacity: 1 },
                    { transform: `translateY(4px)`, opacity: 0.85 }
                ],
                { ...opts, duration: 240 }
            ).finished
        );
        Promise.allSettled([...cardAnim]).finally(() => { 
            setTimeout(() => { window.location.href = url; }, 50); 
        });
    } catch (_) {
        setTimeout(() => { window.location.href = url; }, 220);
    }
}

// Navigate to class on click for approved classes only (delegated)
classContainer?.addEventListener('click', (e) => {
    const article = e.target.closest && e.target.closest('.class-card.active');
    if (!article) return;
    if (article.getAttribute('data-pending') === 'true') return; // Pending join requests are not clickable
    const classCode = article.getAttribute('data-class-code');
    if (!classCode) return;
    navigateToClassWithTransition(classCode);
});

requestJoinBtn?.addEventListener('click', async () => {
    const code = classCodeInput.value.trim();
    if (!code) return showToast('⚠️ Please enter a class code.', 'warning');

    try {
        const response = await fetch(routes.requestJoin, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ classCode: code })
        });
        const data = await response.json();

        if (response.ok) {
            showToast(`✅ ${data.message}`, 'success');

            // Fetch updated dashboard HTML
            const dashboardResponse = await fetch(routes.home);
            const htmlText = await dashboardResponse.text();
            const parser = new DOMParser();
            const doc = parser.parseFromString(htmlText, "text/html");
            const newContainer = doc.querySelector('.dashboard');
            if (newContainer && classContainer) {
                classContainer.innerHTML = newContainer.innerHTML;
                // no need to rebind as we use delegated listener above
            }
        } else {
            showToast(`❌ ${data.message}`, 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('⚠️ Server unreachable. Try again later.', 'warning');
    }

    modalBackdrop.classList.remove('show');
    joinBtn.classList.remove('selected');
    classCodeInput.value = '';
});


// --- ESC KEY CLOSE ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        modalBackdrop.classList.remove('show');
        joinBtn.classList.remove('selected');
        userPopup.classList.remove('show');
        radialActions.classList.remove('show');
    }
});

// --- TOAST NOTIFICATION ---
function showToast(message, type = '') {
    toast.textContent = message;
    toast.className = `toast show ${type}`;
    setTimeout(() => {
        toast.classList.remove('show');
    }, 2800);
}

// --- ACCESSIBILITY (optional focus for menu) ---
menuCircle?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        menuCircle.click();
    }
});
