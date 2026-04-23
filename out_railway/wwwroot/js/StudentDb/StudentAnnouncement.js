/**
 * Teacher-only: delete announcement from StudentAnnouncement view (same slsp-confirm UI as sign out).
 */
(function () {
    const deleteBtn = document.getElementById('teacherDeleteAnnouncementBtn');
    const backdrop = document.getElementById('teacherAnnDeleteConfirmBackdrop');
    const modal = document.getElementById('teacherAnnDeleteConfirmModal');
    const cancelBtn = document.getElementById('teacherAnnDeleteConfirmCancel');
    const okBtn = document.getElementById('teacherAnnDeleteConfirmOk');
    const container = document.getElementById('announcementContainer');
    const toast = document.getElementById('toast');

    if (!deleteBtn || !backdrop || !modal || !container) return;

    const contentId = container.getAttribute('data-content-id') || '';
    const classCode = container.getAttribute('data-class-code') || '';
    const returnUrl = classCode ? `/AdminClass/${encodeURIComponent(classCode)}` : '/professordb/ProfessorDb';

    function showToast(msg, type) {
        if (!toast) return;
        toast.textContent = msg;
        toast.className = `toast show ${type || ''}`;
        setTimeout(() => toast.classList.remove('show'), 3200);
    }

    function openConfirm() {
        backdrop.removeAttribute('hidden');
        backdrop.setAttribute('aria-hidden', 'false');
        modal.removeAttribute('hidden');
        modal.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
        cancelBtn?.focus();
    }

    function closeConfirm() {
        backdrop.setAttribute('hidden', '');
        backdrop.setAttribute('aria-hidden', 'true');
        modal.setAttribute('hidden', '');
        modal.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
    }

    function isOpen() {
        return modal && !modal.hasAttribute('hidden');
    }

    deleteBtn.addEventListener('click', () => openConfirm());

    cancelBtn?.addEventListener('click', closeConfirm);

    backdrop.addEventListener('click', (e) => {
        if (e.target === backdrop) closeConfirm();
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && isOpen()) {
            closeConfirm();
        }
    });

    okBtn?.addEventListener('click', async () => {
        if (!contentId) {
            showToast('Missing announcement id.', 'error');
            closeConfirm();
            return;
        }

        closeConfirm();
        try {
            const res = await fetch('/AdminClass/DeleteAnnouncement', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ContentId: contentId })
            });
            const data = await res.json().catch(() => ({}));
            if (!res.ok || data.success === false) {
                throw new Error(data.message || res.statusText || 'Delete failed');
            }
            showToast('Announcement deleted.', 'success');
            setTimeout(() => {
                window.location.href = returnUrl;
            }, 500);
        } catch (err) {
            console.error(err);
            showToast(err.message || 'Could not delete announcement.', 'error');
        }
    });
})();
