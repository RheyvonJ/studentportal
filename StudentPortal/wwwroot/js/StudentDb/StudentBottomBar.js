// Ensure global toast exists for student pages
if (typeof window.showToast !== 'function') {
    window.showToast = function(message, duration = 2500) {
        try {
            let toast = document.getElementById('toast') || document.querySelector('.toast');
            if (!toast) {
                toast = document.createElement('div');
                toast.id = 'toast';
                toast.className = 'toast';
                toast.setAttribute('aria-live', 'polite');
                toast.setAttribute('role', 'status');
                document.body.appendChild(toast);
            }
            toast.textContent = message;
            toast.classList.add('show');
            clearTimeout(window.__toastTimer);
            window.__toastTimer = setTimeout(() => toast.classList.remove('show'), duration);
        } catch (_) {}
    };
}

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
                navigateWithAnimation('/studentdb/StudentDb', 'Going to dashboard...');
            } else if (icon.classList.contains('fa-book')) {
                navigateWithAnimation('/studentdb/StudentDb', 'Opening classes...');
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
        const target = isStudentClassPage
            ? '/StudentDb/StudentDb'
            : (isSubjectChildPage ? (classCode ? `/StudentClass/${encodeURIComponent(classCode)}` : '/StudentClass') : '/StudentDb/StudentDb');
        window.showToast ? window.showToast('Going back...') : null;
        setTimeout(() => { window.location.href = target; }, 500);
    });

    function navigateWithAnimation(url, message) {
        window.showToast ? window.showToast(message) : null;
        radialActions?.classList.remove('show');
        menuOpen = false;
        setTimeout(() => window.location.href = url, 600);
    }

    const openBtn = document.getElementById('openNotifBtn');
    const modal = document.getElementById('notifModal');
    const backdrop = document.getElementById('notifBackdrop');
    const closeBtn = document.getElementById('closeNotifModal');
    const list = document.getElementById('notifList');
    const markAllReadBtn = document.getElementById('markAllReadBtn');
    const clearAllBtn = document.getElementById('clearAllBtn');

    function shouldToast(message) {
        try {
            const now = Date.now();
            if (!window.__toastSeen) window.__toastSeen = new Map();
            const last = window.__toastSeen.get(message) || 0;
            if (now - last < 60000) return false;
            window.__toastSeen.set(message, now);
            return true;
        } catch (_) { return true; }
    }

    function showToastSafe(message) {
        if (!shouldToast(message)) return;
        if (window.showToast) window.showToast(message);
    }

    function emitNewItemToasts(items) {
        try {
            const listItems = Array.isArray(items) ? items : [];
            let maxTsAll = 0;
            for (const n of listItems) {
                const created = n.createdAt || n.CreatedAt;
                const ts = created ? new Date(created).getTime() : 0;
                if (ts > maxTsAll) maxTsAll = ts;
            }
            // First fetch in this tab: set watermark from existing items only — do not replay old unread toasts
            if (!window.__studentNotifToastBaselineDone) {
                window.__notifLastCreatedAt = maxTsAll;
                window.__studentNotifToastBaselineDone = true;
                return;
            }
            const prevTs = window.__notifLastCreatedAt || 0;
            let maxTs = prevTs;
            for (const n of listItems) {
                const created = n.createdAt || n.CreatedAt;
                const read = n.read || n.Read;
                const deleted = n.deleted || n.Deleted;
                const text = n.text || n.Text || '';
                const type = (n.type || n.Type || '').toString().toLowerCase();
                const ts = created ? new Date(created).getTime() : 0;
                if (!read && !deleted && ts && ts > prevTs && text && type !== 'class-auto-joined') {
                    showToastSafe(text);
                }
                if (ts > maxTs) maxTs = ts;
            }
            window.__notifLastCreatedAt = maxTs;
        } catch (_) {}
    }

    async function fetchNotifications() {
        try {
            const res = await fetch(`/studentdb/StudentDb/Notifications?ts=${Date.now()}`, { credentials: 'same-origin', cache: 'no-store', headers: { 'Cache-Control': 'no-cache' } });
            if (!res.ok) return;
            const data = await res.json();
            if (!data || data.success !== true) return;

            const badge = document.getElementById('notifBadge');
            const badgeBtn = document.getElementById('notifBadgeBtn');
            const total = (data.unread ?? (data.items?.length || 0));
            if (badge && total > 0) {
                badge.textContent = String(total);
                badge.style.display = 'inline-block';
            }
            if (badgeBtn && total > 0) {
                badgeBtn.textContent = String(total);
                badgeBtn.style.display = 'inline-block';
            }

            window.__studentNotifData = data;
            try {
                const d = window.__studentNotifData || {};
                d.items = Array.isArray(d.items) ? d.items : [];
                const nowIso = new Date().toISOString();
                const exists = (type, text) => d.items.some(x => (x.type || x.Type) === type && (x.text || x.Text) === text);
                const pushEphemeral = (type, text) => {
                    if (!exists(type, text)) {
                        d.items.unshift({ Id: `ep-${type}-${Date.now()}`, Type: type, Text: text, CreatedAt: nowIso, Read: false, Deleted: false });
                        showToastSafe(text);
                    }
                };
                if ((d.assessments || 0) > 0) pushEphemeral('assessment', `${d.assessments} new assessment(s) posted`);
                if ((d.tasks || 0) > 0) pushEphemeral('task', `${d.tasks} new task(s) posted`);
                if ((d.materials || 0) > 0) pushEphemeral('material', `${d.materials} new material(s) posted`);
                if ((d.announcements || 0) > 0) pushEphemeral('announcement', `${d.announcements} new announcement(s)`);
                window.__studentNotifData = d;
            } catch (_) {}
            (data.approved || []).forEach(code => { showToastSafe(`Your request to join the class "${code}" has been approved.`); });
            (data.rejected || []).forEach(code => { showToastSafe(`Your request to join class ${code} was rejected.`); });
            (data.unenrolled || []).forEach(code => { showToastSafe(`You have been unenrolled from class ${code}.`); });
            (data.libraApprovedReservations || []).forEach(title => { showToastSafe(`Your book reservation "${title}" has been approved.`); });
            (data.libraRejectedReservations || []).forEach(title => { showToastSafe(`Your book reservation "${title}" was rejected.`); });
            (data.libraCancelledReservations || []).forEach(title => { showToastSafe(`Your book reservation "${title}" was cancelled.`); });
            ((data.libraPenalties || [])).forEach(msg => { showToastSafe(`Library penalty: ${msg}`); });
            emitNewItemToasts((window.__studentNotifData || {}).items);
        } catch (_) {}
    }

    function renderNotifList() {
        const d = window.__studentNotifData || {};
        const items = d.items || [];
        if (!list) return;
        if (!items.length) {
            list.innerHTML = `<div class="notif-empty"><span class="emoji">✨</span>No notifications</div>`;
            return;
        }
        const iconFor = t => {
            if (t === 'class-approved') return 'fa-check';
            if (t === 'class-rejected') return 'fa-xmark';
            if (t === 'class-auto-joined') return 'fa-user-plus';
            if (t === 'assessment') return 'fa-clipboard-question';
            if (t === 'task') return 'fa-list-check';
            if (t === 'class-unenrolled') return 'fa-user-minus';
            if (t === 'announcement') return 'fa-bullhorn';
            if (t === 'libra-approved') return 'fa-book';
            if (t === 'libra-rejected') return 'fa-book';
            if (t === 'libra-cancelled') return 'fa-ban';
            if (t === 'libra-penalty') return 'fa-scale-balanced';
            if (t === 'content-assessment-new') return 'fa-clipboard-question';
            if (t === 'content-task-new') return 'fa-list-check';
            if (t === 'content-material-new') return 'fa-book';
            return 'fa-bell';
        };
        list.innerHTML = items.map(n => {
            const type = n.type || n.Type;
            const text = n.text || n.Text;
            const id = n.id || n.Id;
            const created = n.createdAt || n.CreatedAt;
            const when = created ? new Date(created) : null;
            const rel = when ? timeAgo(when) : '';
            const chip = (type && type.includes('libra')) ? 'Library' : ((type && type.split('-')[0]) || '');
            return `
            <li class="notif-item ${type} ${n.read ? 'read' : ''}" data-id="${id}" data-type="${type}">
                <span class="notif-icon"><i class="fa-solid ${iconFor(type)}"></i></span>
                <span class="notif-text">${text}</span>
                <span class="notif-time">${rel}</span>
                <span class="notif-chip">${chip}</span>
                <button class="notif-action mark" title="Mark as read"><i class="fa-solid fa-envelope-open"></i></button>
                <button class="notif-action delete" title="Delete"><i class="fa-solid fa-trash"></i></button>
            </li>`;
        }).join('');
    }

    function timeAgo(date) {
        const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
        const intervals = [
            { s: 31536000, l: 'y' },
            { s: 2592000, l: 'mo' },
            { s: 604800, l: 'w' },
            { s: 86400, l: 'd' },
            { s: 3600, l: 'h' },
            { s: 60, l: 'm' }
        ];
        for (const i of intervals) {
            const v = Math.floor(seconds / i.s);
            if (v >= 1) return `${v}${i.l}`;
        }
        return 'now';
    }

    function openModal() {
        renderNotifList();
        modal?.classList.add('show');
        backdrop?.classList.add('show');
    }
    function closeModal() {
        modal?.classList.remove('show');
        backdrop?.classList.remove('show');
    }

    openBtn?.addEventListener('click', async (e) => { e.stopPropagation(); await fetchNotifications(); openModal(); });
    closeBtn?.addEventListener('click', (e) => { e.stopPropagation(); closeModal(); });
    backdrop?.addEventListener('click', closeModal);

    markAllReadBtn?.addEventListener('click', async (e) => {
        e.stopPropagation();
        const d = window.__studentNotifData || {};
        const items = d.items || [];
        const ids = items.filter(x => !x.read).map(x => x.id || x.Id).filter(Boolean);
        if (!ids.length) return;
        await Promise.all(ids.map(id => fetch('/studentdb/StudentDb/Notifications/MarkRead', { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ id }) })));
        await fetchNotifications();
        renderNotifList();
        updateBadge();
    });

    clearAllBtn?.addEventListener('click', async (e) => {
        e.stopPropagation();
        const d = window.__studentNotifData || {};
        const items = d.items || [];
        const ids = items.map(x => x.id || x.Id).filter(Boolean);
        if (!ids.length) return;
        await Promise.all(ids.map(id => fetch('/studentdb/StudentDb/Notifications/Delete', { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ id }) })));
        await fetchNotifications();
        renderNotifList();
        updateBadge();
    });

    function updateBadge() {
        const d = window.__studentNotifData || {};
        const total = (d.unread ?? (d.items?.length || 0));
        const badgeBtn = document.getElementById('notifBadgeBtn');
        if (badgeBtn) {
            if (total > 0) {
                badgeBtn.textContent = String(total);
                badgeBtn.style.display = 'inline-block';
            } else {
                badgeBtn.style.display = 'none';
            }
        }
    }

    list?.addEventListener('click', async (e) => {
        const delBtn = e.target.closest('.notif-action.delete');
        const markBtn = e.target.closest('.notif-action.mark');
        const li = e.target.closest('.notif-item');
        if (!li) return;
        const id = li.dataset.id;
        if (delBtn && id) {
            const res = await fetch('/studentdb/StudentDb/Notifications/Delete', { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ id }) });
            if (res && res.ok) {
                try {
                    const d = window.__studentNotifData || {};
                    d.items = (d.items || []).filter(x => (x.id || x.Id) !== id);
                    d.unread = (d.items || []).filter(x => !(x.read || x.Read)).length;
                    window.__studentNotifData = d;
                    renderNotifList();
                    updateBadge();
                } catch (_) {}
                await fetchNotifications();
            }
        } else if (markBtn && id) {
            await fetch('/studentdb/StudentDb/Notifications/MarkRead', { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ id }) });
            await fetchNotifications();
            renderNotifList();
            updateBadge();
        }
    });

    fetchNotifications();
    setInterval(fetchNotifications, 5000);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') fetchNotifications(); });
    window.addEventListener('focus', () => { fetchNotifications(); });
}
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupStudentBottomBar);
} else {
    setupStudentBottomBar();
}
