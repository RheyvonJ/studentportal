// admintask.js — client logic adapted for MVC view

document.addEventListener('DOMContentLoaded', () => {
    const adminTaskPage = () => document.getElementById('adminTaskPage');
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
  const toast = document.getElementById('toast');
  const backButton = document.querySelector('.back-button');

    const makeChangesButton = document.getElementById('makeChangesButton');
    const adminActions = document.getElementById('adminActions');
    const deleteModal = document.getElementById('deleteModal');
    const confirmDelete = document.getElementById('confirmDelete');
    const cancelDelete = document.getElementById('cancelDelete');

    const taskTitle = document.getElementById('taskTitle');
    const taskDescription = document.getElementById('taskDescription');
    const attachmentList = document.getElementById('attachmentList');
    const editControls = document.getElementById('editControls');
    const saveEditBtn = document.getElementById('saveEditBtn');
    const cancelEditBtn = document.getElementById('cancelEditBtn');
    const dateInfo = document.getElementById('dateInfo');

    // submissions list refs
    const searchStudents = document.getElementById('searchStudents');
    const submittedList = document.getElementById('submittedList');
    const submittedCountEl = document.getElementById('submittedCount');
    const totalStudentsEl = document.getElementById('totalStudents');

    // --- initial
    if (editControls) { editControls.style.display = 'none'; editControls.classList.remove('show'); }

    // In-page attachment viewer for task attachments (same UX as StudentMaterial)
    initAdminTaskInPageFileViewer();

    // Bottom bar interactions are managed by AdminBottomBar.js

    // back button -> class page, then professor dashboard
    backButton?.addEventListener('click', () => {
        const cc = adminTaskPage()?.dataset?.classCode || document.body?.dataset?.classCode || '';
        const dash = typeof window.resolveInstructorDashboardUrl === 'function' ? window.resolveInstructorDashboardUrl() : '/professordb/ProfessorDb';
        const target = cc ? `/AdminClass/${encodeURIComponent(cc)}` : dash;
        const msg = typeof window.resolveAdminNavToastMessage === 'function'
            ? window.resolveAdminNavToastMessage(target)
            : 'Returning...';
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading(target, msg, 600);
        } else {
            showToast(msg);
            setTimeout(() => { window.location.href = target; }, 600);
        }
    });

  // toast
  function showToast(message) {
        if (!toast) return;
        toast.textContent = message;
        toast.className = 'toast show';
        setTimeout(() => toast.classList.remove('show'), 2500);
    }

    function initAdminTaskInPageFileViewer() {
        const viewer = document.getElementById('atFileViewer');
        const body = document.getElementById('atViewerBody');
        const title = document.getElementById('atFileTitle');
        const openNew = document.getElementById('atOpenNewTab');
        const download = document.getElementById('atDownload');
        const closeBtn = document.getElementById('atCloseViewer');

        const openBackdrop = document.getElementById('atOpenConfirmBackdrop');
        const openModal = document.getElementById('atOpenConfirmModal');
        const openCancel = document.getElementById('atOpenConfirmCancel');
        const openOk = document.getElementById('atOpenConfirmOk');
        const openDesc = document.getElementById('atOpenConfirmDesc');

        const dlBackdrop = document.getElementById('atDownloadConfirmBackdrop');
        const dlModal = document.getElementById('atDownloadConfirmModal');
        const dlCancel = document.getElementById('atDownloadConfirmCancel');
        const dlOk = document.getElementById('atDownloadConfirmOk');
        const dlDesc = document.getElementById('atDownloadConfirmDesc');

        if (!viewer || !body || !title || !openNew || !download || !closeBtn) return;

        let currentFileName = '';
        let currentDownloadUrl = '';
        let lastPreview = null;
        let resizeTimer = null;

        function extOf(name) {
            const s = (name || '').toLowerCase();
            const i = s.lastIndexOf('.');
            return i >= 0 ? s.slice(i + 1) : '';
        }
        function setViewerNode(node) {
            body.innerHTML = '';
            body.appendChild(node);
        }
        function renderLoading() {
            const wrap = document.createElement('div');
            wrap.style.cssText = 'height:100%;display:flex;align-items:center;justify-content:center;color:#e5e7eb;font-weight:700;';
            wrap.textContent = 'Loading preview...';
            setViewerNode(wrap);
        }
        function renderUnsupported(msg) {
            const wrap = document.createElement('div');
            wrap.style.cssText = 'padding:18px;font-family:system-ui,Segoe UI,Arial;color:#e5e7eb;';
            wrap.innerHTML = `<div style="font-weight:800;margin-bottom:6px;">Preview not available</div><div style="opacity:.9;">${msg}</div>`;
            setViewerNode(wrap);
        }
        function renderIframe(url, titleText) {
            const iframe = document.createElement('iframe');
            iframe.src = url;
            iframe.title = titleText || 'File preview';
            iframe.style.cssText = 'width:100%;height:100%;border:0;background:white;';
            setViewerNode(iframe);
        }
        function renderImage(url) {
            const img = document.createElement('img');
            img.src = url;
            img.alt = currentFileName || 'image';
            img.style.cssText = 'max-width:100%;max-height:100%;width:auto;height:auto;display:block;object-fit:contain;';
            img.addEventListener('error', () => renderIframe(url, 'Image preview'));
            const wrap = document.createElement('div');
            wrap.style.cssText = 'height:100%;display:flex;align-items:center;justify-content:center;padding:14px;';
            wrap.appendChild(img);
            setViewerNode(wrap);
        }
        async function renderText(url) {
            const pre = document.createElement('pre');
            pre.style.cssText = 'margin:0;padding:14px;white-space:pre-wrap;word-break:break-word;color:#0b1220;background:#fff;font:13px/1.5 ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;';
            const resp = await fetch(url, { credentials: 'same-origin' });
            if (!resp.ok) throw new Error('text fetch failed');
            pre.textContent = await resp.text();
            setViewerNode(pre);
        }
        async function getPublicPreviewUrl(fileName, taskId) {
            // Reuse StudentMaterial token endpoint (task uploads live in same uploads folder).
            const fd = new FormData();
            fd.append('fileName', fileName);
            fd.append('contentId', taskId);
            const resp = await fetch('/StudentMaterial/CreatePublicPreviewToken', { method: 'POST', body: fd, credentials: 'same-origin' });
            if (!resp.ok) throw new Error('token request failed');
            const data = await resp.json();
            if (!data || !data.success || !data.token) throw new Error('token missing');
            return `${window.location.origin}/PublicPreview/${encodeURIComponent(data.token)}`;
        }
        async function renderPdf(url) {
            const resp = await fetch(url, { credentials: 'same-origin' });
            if (!resp.ok) throw new Error('pdf fetch failed');
            const buf = await resp.arrayBuffer();

            async function loadPdfJs() {
                if (window.pdfjsLib && window.pdfjsLib.getDocument) return window.pdfjsLib;
                if (window.__pdfjsLoading) return await window.__pdfjsLoading;
                window.__pdfjsLoading = (async () => {
                    const mod = await import('/lib/pdfjs/pdf.min.mjs');
                    const lib = mod && (mod.GlobalWorkerOptions ? mod : mod.pdfjsLib);
                    const resolved = lib && lib.getDocument ? lib : (mod && mod.pdfjsLib);
                    if (!resolved || !resolved.getDocument) throw new Error('pdfjs not available');
                    try { resolved.GlobalWorkerOptions.workerSrc = '/lib/pdfjs/pdf.worker.min.mjs'; } catch (_) { }
                    window.pdfjsLib = resolved;
                    return resolved;
                })();
                return await window.__pdfjsLoading;
            }

            const pdfjs = await loadPdfJs();
            const pdf = await pdfjs.getDocument({ data: buf }).promise;

            const wrap = document.createElement('div');
            wrap.style.cssText = 'padding:18px;display:flex;flex-direction:column;gap:18px;align-items:center;background:#0b1220;';

            const availableW = Math.max(280, (body.clientWidth || 0) - 40);
            const targetW = Math.min(1200, availableW);

            for (let p = 1; p <= pdf.numPages; p++) {
                const page = await pdf.getPage(p);
                const vp1 = page.getViewport({ scale: 1 });
                const scale = targetW / vp1.width;
                const viewport = page.getViewport({ scale });

                const canvas = document.createElement('canvas');
                const ctx = canvas.getContext('2d', { alpha: false });
                canvas.width = Math.floor(viewport.width);
                canvas.height = Math.floor(viewport.height);
                canvas.style.cssText =
                    'display:block;max-width:100%;height:auto;border-radius:12px;background:#fff;' +
                    'box-shadow:0 14px 35px rgba(0,0,0,.25);';

                const holder = document.createElement('div');
                holder.style.cssText = 'width:100%;display:flex;justify-content:center;';
                holder.appendChild(canvas);
                wrap.appendChild(holder);

                await page.render({ canvasContext: ctx, viewport }).promise;
            }
            setViewerNode(wrap);
        }
        function close() {
            viewer.style.display = 'none';
            body.innerHTML = '';
            currentFileName = '';
            currentDownloadUrl = '';
            lastPreview = null;
        }
        async function confirmOpenNewTab() {
            if (!openBackdrop || !openModal || !openCancel || !openOk) return window.confirm(`Open "${currentFileName}" in a new tab?`);
            if (openDesc) openDesc.textContent = `Do you want to open "${currentFileName}" in a new tab?`;
            openBackdrop.removeAttribute('hidden');
            openBackdrop.setAttribute('aria-hidden', 'false');
            openModal.removeAttribute('hidden');
            openModal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            openCancel.focus();
            return await new Promise((resolve) => {
                function finish(v) {
                    openBackdrop.setAttribute('hidden', '');
                    openBackdrop.setAttribute('aria-hidden', 'true');
                    openModal.setAttribute('hidden', '');
                    openModal.setAttribute('aria-hidden', 'true');
                    document.body.style.overflow = '';
                    cleanup();
                    resolve(v);
                }
                function onCancel(ev) { ev.preventDefault(); finish(false); }
                function onOk(ev) { ev.preventDefault(); finish(true); }
                function onBackdrop(ev) { ev.preventDefault(); finish(false); }
                function onKey(ev) { if (ev.key === 'Escape' && !openModal.hasAttribute('hidden')) finish(false); }
                function cleanup() {
                    openCancel.removeEventListener('click', onCancel);
                    openOk.removeEventListener('click', onOk);
                    openBackdrop.removeEventListener('click', onBackdrop);
                    document.removeEventListener('keydown', onKey);
                }
                openCancel.addEventListener('click', onCancel);
                openOk.addEventListener('click', onOk);
                openBackdrop.addEventListener('click', onBackdrop);
                document.addEventListener('keydown', onKey);
            });
        }
        async function confirmDownload() {
            if (!dlBackdrop || !dlModal || !dlCancel || !dlOk) return window.confirm(`Download "${currentFileName}"?`);
            if (dlDesc) dlDesc.textContent = `Do you want to download "${currentFileName}"?`;
            dlBackdrop.removeAttribute('hidden');
            dlBackdrop.setAttribute('aria-hidden', 'false');
            dlModal.removeAttribute('hidden');
            dlModal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            dlCancel.focus();
            return await new Promise((resolve) => {
                function finish(v) {
                    dlBackdrop.setAttribute('hidden', '');
                    dlBackdrop.setAttribute('aria-hidden', 'true');
                    dlModal.setAttribute('hidden', '');
                    dlModal.setAttribute('aria-hidden', 'true');
                    document.body.style.overflow = '';
                    cleanup();
                    resolve(v);
                }
                function onCancel(ev) { ev.preventDefault(); finish(false); }
                function onOk(ev) { ev.preventDefault(); finish(true); }
                function onBackdrop(ev) { ev.preventDefault(); finish(false); }
                function onKey(ev) { if (ev.key === 'Escape' && !dlModal.hasAttribute('hidden')) finish(false); }
                function cleanup() {
                    dlCancel.removeEventListener('click', onCancel);
                    dlOk.removeEventListener('click', onOk);
                    dlBackdrop.removeEventListener('click', onBackdrop);
                    document.removeEventListener('keydown', onKey);
                }
                dlCancel.addEventListener('click', onCancel);
                dlOk.addEventListener('click', onOk);
                dlBackdrop.addEventListener('click', onBackdrop);
                document.addEventListener('keydown', onKey);
            });
        }

        document.addEventListener('click', (e) => {
            const a = e.target && e.target.closest ? e.target.closest('.attachment-name') : null;
            if (!a || !a.getAttribute('data-taskid')) return;
            e.preventDefault();
            e.stopPropagation();
            // Prevent other document-level handlers (legacy attachment menus) from also firing.
            if (typeof e.stopImmediatePropagation === 'function') e.stopImmediatePropagation();
            const fileName = a.getAttribute('data-filename') || '';
            const taskId = a.getAttribute('data-taskid') || '';
            if (!fileName || !taskId) return;

            const viewUrl = `/AdminTask/ViewFile/${encodeURIComponent(fileName)}?taskId=${encodeURIComponent(taskId)}`;
            const downloadUrl = `/AdminTask/DownloadFile/${encodeURIComponent(fileName)}?taskId=${encodeURIComponent(taskId)}`;
            title.textContent = fileName || 'File';
            openNew.href = viewUrl;
            currentFileName = fileName;
            currentDownloadUrl = downloadUrl;
            viewer.style.display = 'block';
            renderLoading();

            const ext = extOf(fileName);
            const isImg = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg'].includes(ext);
            const isText = ['txt', 'csv', 'log', 'json', 'xml'].includes(ext);
            const isPdf = ext === 'pdf';
            const isOffice = ['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx'].includes(ext);

            (async () => {
                try {
                    if (isPdf) { await renderPdf(viewUrl); lastPreview = { type: 'pdf', url: viewUrl }; return; }
                    if (isImg) { renderImage(viewUrl); lastPreview = { type: 'img', url: viewUrl }; return; }
                    if (isText) { await renderText(viewUrl); lastPreview = { type: 'text', url: viewUrl }; return; }
                    if (isOffice) {
                        const publicUrl = await getPublicPreviewUrl(fileName, taskId);
                        const gview = `https://docs.google.com/gview?embedded=1&url=${encodeURIComponent(publicUrl)}`;
                        renderIframe(gview, 'Document preview');
                        lastPreview = { type: 'iframe', url: gview };
                        return;
                    }
                    renderIframe(viewUrl, 'File preview');
                    lastPreview = { type: 'iframe', url: viewUrl };
                } catch (err) {
                    console.error(err);
                    renderUnsupported('Use “Open in new tab” or “Download”.');
                }
            })();
        });

        openNew.addEventListener('click', async (e) => {
            e.preventDefault();
            e.stopPropagation();
            const href = openNew.getAttribute('href') || '';
            if (!href || href === '#') return;
            const ok = await confirmOpenNewTab();
            if (!ok) return;
            window.open(href, '_blank', 'noopener');
        });

        download.addEventListener('click', async (e) => {
            e.preventDefault();
            e.stopPropagation();
            if (!currentDownloadUrl || !currentFileName) return;
            const ok = await confirmDownload();
            if (!ok) return;
            const a = document.createElement('a');
            a.href = currentDownloadUrl;
            a.style.display = 'none';
            document.body.appendChild(a);
            a.click();
            a.remove();
        });

        closeBtn.addEventListener('click', (e) => { e.preventDefault(); close(); });
        viewer.addEventListener('click', (e) => { if (e.target === viewer) close(); });
        document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && viewer.style.display === 'block') close(); });

        window.addEventListener('resize', () => {
            if (resizeTimer) window.clearTimeout(resizeTimer);
            resizeTimer = window.setTimeout(async () => {
                if (viewer.style.display !== 'block' || !lastPreview) return;
                if (lastPreview.type === 'pdf' && lastPreview.url) {
                    try { renderLoading(); await renderPdf(lastPreview.url); } catch (_) { }
                }
            }, 140);
        });
    }

  // admin actions menu
    let adminMenuOpen = false;
    makeChangesButton?.addEventListener('click', (e) => {
        adminMenuOpen = !adminMenuOpen;
        adminActions?.classList.toggle('show', adminMenuOpen);
        e.stopPropagation();
    });

    document.addEventListener('click', (e) => {
        if (!adminActions?.contains(e.target) && !makeChangesButton?.contains(e.target)) {
            adminActions?.classList.remove('show');
            adminMenuOpen = false;
        }
    });

    adminActions?.addEventListener('click', (e) => {
        const action = e.target.closest('.admin-action');
        if (!action) return;

        // LOCKED BUTTON FOR ADMIN ACTIONS
        if (action.classList.contains('locked')) {
            showToast("Coming soon");
            adminActions?.classList.remove('show');
            adminMenuOpen = false;
            return;
        }

        const type = action.dataset.action;
        adminActions.classList.remove('show');
        adminMenuOpen = false;

        if (type === 'edit') enterEditMode();
        else if (type === 'delete') showDeleteModal();
    });

    // delete flow
    function showDeleteModal() { if (!deleteModal) return; deleteModal.classList.add('show'); }
    function getTaskIdFromForm() {
        const form = document.getElementById('adminCommentForm');
        const input = form ? form.querySelector('input[name="taskId"]') : null;
        return input ? input.value : '';
    }

    confirmDelete?.addEventListener('click', async () => {
        if (!deleteModal) return;
        deleteModal.classList.remove('show');
        try {
            const taskId = getTaskIdFromForm();
            const res = await fetch('/AdminTask/DeleteTask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ taskId })
            });
            if (!res.ok) throw new Error('Delete request failed');
            const data = await res.json();
            if (data && data.success) {
                showToast('🗑️ Task deleted.');
                const classCode = adminTaskPage()?.dataset?.classCode || document.body?.dataset?.classCode || '';
                const dash = typeof window.resolveInstructorDashboardUrl === 'function' ? window.resolveInstructorDashboardUrl() : '/professordb/ProfessorDb';
                const url = classCode ? `/AdminClass/${encodeURIComponent(classCode)}` : dash;
                setTimeout(() => {
                    if (typeof window.navigateWithProfessorLoading === 'function') {
                        window.navigateWithProfessorLoading(url, null, 600);
                    } else {
                        window.location.href = url;
                    }
                }, 800);
            } else {
                throw new Error(data?.message || 'Delete failed');
            }
        } catch (e) {
            showToast('❌ Failed to delete task');
        }
    });
    cancelDelete?.addEventListener('click', () => { if (!deleteModal) return; deleteModal.classList.remove('show'); });
    document.addEventListener('click', (e) => {
        if (deleteModal && deleteModal.classList.contains('show') && e.target === deleteModal) deleteModal.classList.remove('show');
    });

    // edit mode
    const escapeHtmlForMeta = (str) => {
        if (str == null || str === '') return '';
        const d = document.createElement('div');
        d.textContent = String(str);
        return d.innerHTML;
    };
    const buildTaskMetaRowHtml = (posted, edited, deadlineDisplay) => {
        const dd = deadlineDisplay && deadlineDisplay !== 'N/A' ? deadlineDisplay : '—';
        const editedBlock = edited && String(edited).trim()
            ? `<div class="aae-meta-item"><span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-pen-to-square"></i></span><span class="aae-meta-label">Edited</span><span class="aae-meta-value">${escapeHtmlForMeta(edited)}</span></div>`
            : '';
        return `
            <div class="aae-meta-item">
                <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-calendar"></i></span>
                <span class="aae-meta-label">Posted</span>
                <span class="aae-meta-value">${escapeHtmlForMeta(posted || '—')}</span>
            </div>
            ${editedBlock}
            <div class="aae-meta-item">
                <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-flag"></i></span>
                <span class="aae-meta-label">Deadline</span>
                <span class="aae-meta-value">${escapeHtmlForMeta(dd)}</span>
            </div>`;
    };
    const applyTaskMetaToDateInfo = (el, posted, edited, deadlineDisplay, deadlineIso) => {
        if (!el) return;
        el.dataset.posted = posted || '';
        el.dataset.edited = edited || '';
        el.dataset.deadlineDisplay = deadlineDisplay || '';
        el.dataset.deadlineIso = deadlineIso || '';
        el.innerHTML = buildTaskMetaRowHtml(posted, edited, deadlineDisplay);
        el.classList.remove('aae-meta-edit-mode');
    };

    let inEditMode = false;
    let originalDateHtml = '';

    function enterEditMode() {
        if (inEditMode) return;
        inEditMode = true;
        showToast('✏️ Editing task...');

        if (taskTitle) { taskTitle.contentEditable = 'true'; taskTitle.focus(); placeCaretAtEnd(taskTitle); }
        if (taskDescription) taskDescription.contentEditable = 'true';

        originalDateHtml = dateInfo.innerHTML;
        const postedText = dateInfo.dataset.posted || '';
        const editedText = dateInfo.dataset.edited || '';
        const currentDeadlineIso = dateInfo.dataset.deadlineIso || '';
        const editedRow = editedText.trim()
            ? `<div class="aae-meta-item"><span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-pen-to-square"></i></span><span class="aae-meta-label">Edited</span><span class="aae-meta-value">${escapeHtmlForMeta(editedText)}</span></div>`
            : '';

        dateInfo.classList.add('aae-meta-edit-mode');
        dateInfo.innerHTML = `
            <div class="aae-meta-item">
                <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-calendar"></i></span>
                <span class="aae-meta-label">Posted</span>
                <span class="aae-meta-value">${escapeHtmlForMeta(postedText)}</span>
            </div>
            ${editedRow}
            <div class="aae-meta-item">
                <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-flag"></i></span>
                <span class="aae-meta-label">Deadline</span>
                <input type="date" class="aae-input" id="deadlineInput" value="${escapeHtmlForMeta(currentDeadlineIso)}" style="max-width:11rem" />
            </div>`;

        revealEditControls();
    }

    function revealEditControls() {
        if (!editControls) return;
        editControls.style.display = 'flex';
        requestAnimationFrame(() => editControls.classList.add('show'));
    }

    function hideEditControls() {
        if (!editControls) return;
        editControls.classList.remove('show');
        setTimeout(() => editControls.style.display = 'none', 320);
    }

    function placeCaretAtEnd(el) {
        try {
            const range = document.createRange();
            const sel = window.getSelection();
            range.selectNodeContents(el);
            range.collapse(false);
            sel.removeAllRanges();
            sel.addRange(range);
        } catch (err) { }
    }

    function exitEditMode(saveChanges = false) {
        if (!inEditMode) return;
        inEditMode = false;

        if (taskTitle) taskTitle.contentEditable = 'false';
        if (taskDescription) taskDescription.contentEditable = 'false';

        const deadlineInput = document.getElementById('deadlineInput');
        const newDeadline = deadlineInput ? deadlineInput.value : '';
        if (deadlineInput) deadlineInput.remove();

        if (saveChanges) {
            (async () => {
                try {
                    const taskId = getTaskIdFromForm();
                    const title = taskTitle?.textContent?.trim() || '';
                    const description = taskDescription?.textContent?.trim() || '';
                    const deadline = newDeadline ? new Date(newDeadline).toISOString() : null;
                    const res = await fetch('/AdminTask/UpdateTask', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ taskId, title, description, deadline })
                    });
                    if (!res.ok) throw new Error('Update request failed');
                    const data = await res.json();
                    if (data && data.success) {
                        const today = new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                        const formattedDeadline = newDeadline ? new Date(newDeadline).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : 'N/A';
                        const posted = dateInfo.dataset.posted || '';
                        applyTaskMetaToDateInfo(dateInfo, posted, today, formattedDeadline, newDeadline || '');
                        const statusBadge = document.getElementById('taskStatusBadge');
                        if (statusBadge) {
                            statusBadge.classList.remove('aae-status-active', 'aae-status-draft', 'aae-status-closed');
                            if (newDeadline) {
                                const daysLeft = Math.floor((new Date(newDeadline).setHours(0, 0, 0, 0) - new Date().setHours(0, 0, 0, 0)) / (1000 * 60 * 60 * 24));
                                if (daysLeft < 0) {
                                    statusBadge.classList.add('aae-status-closed');
                                    statusBadge.textContent = 'Overdue';
                                } else if (daysLeft <= 3) {
                                    statusBadge.classList.add('aae-status-draft');
                                    statusBadge.textContent = 'Due Soon';
                                } else {
                                    statusBadge.classList.add('aae-status-active');
                                    statusBadge.textContent = 'Active';
                                }
                            } else {
                                statusBadge.classList.add('aae-status-active');
                                statusBadge.textContent = 'Active';
                            }
                        }
                        showToast('✅ Changes saved.');
                    } else {
                        throw new Error(data?.message || 'Update failed');
                    }
                } catch (e) {
                    showToast('❌ Failed to save changes');
                }
            })();
        } else {
            dateInfo.innerHTML = originalDateHtml;
            dateInfo.classList.remove('aae-meta-edit-mode');
            showToast('✖️ Edit cancelled.');
        }

        hideEditControls();
    }

    // ------------------ REPLACE ATTACHMENT (upload + link) -----
    const replaceFileBtn = document.getElementById('replaceFileBtn');
    const replaceFileInput = document.getElementById('replaceFileInput');

    replaceFileBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!inEditMode) { showToast('Enable edit mode first'); return; }
        replaceFileInput?.click();
    });

    replaceFileInput?.addEventListener('change', async () => {
        const file = replaceFileInput.files && replaceFileInput.files[0];
        if (!file) return;
        try {
            const classCode = adminTaskPage()?.dataset?.classCode || document.body?.dataset?.classCode || '';
            const fd = new FormData();
            fd.append('file', file);
            fd.append('classCode', classCode);
            fd.append('type', 'task');
            const up = await fetch('/AdminClass/UploadFile', { method: 'POST', body: fd });
            if (!up.ok) throw new Error('Upload failed');
            const upRes = await up.json();
            if (!upRes.success) throw new Error(upRes.message || 'Upload failed');

            const taskId = getTaskIdFromForm();
            const linkRes = await fetch('/AdminTask/ReplaceAttachment', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ taskId, fileName: file.name, fileUrl: upRes.fileUrl })
            });
            if (!linkRes.ok) throw new Error('Replace failed');
            const linkJson = await linkRes.json();
            if (!linkJson.success) throw new Error(linkJson.message || 'Replace failed');

            // Update UI attachment list if present
            const attList = document.getElementById('attachmentList');
            if (attList) {
                attList.innerHTML = '';
                const box = document.createElement('div');
                box.className = 'attachment-box aae-file-chip';
                box.dataset.fileLabel = file.name;
                box.innerHTML = `<i class=\"fa-solid fa-file\"></i> <span class=\"attachment-name\">${file.name}</span> <button class=\"download-btn\" data-filename=\"${file.name}\" title=\"Download ${file.name}\"><i class=\"fa-solid fa-download\"></i></button>`;
                attList.appendChild(box);
                const dropzone = document.getElementById('attachmentDropzone');
                dropzone?.classList.add('has-files');
                const empty = document.getElementById('attachmentEmptyState');
                if (empty) empty.style.display = 'none';
                initializeDownloadButtons();
            }
            showToast('✅ File replaced');
        } catch (err) {
            console.error(err);
            showToast('❌ ' + (err.message || 'Could not replace file'));
        } finally {
            try { replaceFileInput.value = ''; } catch {}
        }
    });

    saveEditBtn?.addEventListener('click', () => exitEditMode(true));
    cancelEditBtn?.addEventListener('click', () => exitEditMode(false));

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && inEditMode) {
            exitEditMode(false);
        }
    });

    // --- submissions rendering & search ---
    function parseSubmissionsFromDom() {
        const rows = Array.from(document.querySelectorAll('.submitted-row')).filter(r => !r.classList.contains('header'));
        return rows.map(r => {
            const id = r.dataset.id;
            const name = (r.querySelector('.name-col') || r.querySelector('.col.name-col')).textContent.trim();
            const statusText = (r.querySelector('.status-col') || r.querySelector('.col.status-col')).textContent.trim();
            return { id, name, statusText, row: r };
        });
    }

    let submissionsData = parseSubmissionsFromDom();
    function updateSubmissionCounters() {
        const submittedCount = submissionsData.filter(s => /submitted/i.test(s.statusText)).length;
        const total = submissionsData.length;
        if (submittedCountEl) submittedCountEl.textContent = submittedCount;
        if (totalStudentsEl) totalStudentsEl.textContent = total;
    }
    updateSubmissionCounters();

    const submissionDeadlinePanel = document.getElementById('submissionDeadlinePanel');
    if (submissionDeadlinePanel) {
        const taskIdSd = submissionDeadlinePanel.dataset.taskId || '';
        const allowLateSubmissionBtn = document.getElementById('allowLateSubmissionBtn');
        const revokeLateSubmissionBtn = document.getElementById('revokeLateSubmissionBtn');
        const taskStatusBadgeSd = document.getElementById('taskStatusBadge');
        function syncTaskDeadlineButtons(data) {
            if (!data) return;
            const locked = !!data.isLockedForStudents;
            const allow = !!data.allowPastDeadline;
            submissionDeadlinePanel.dataset.allowLate = allow ? 'true' : 'false';
            submissionDeadlinePanel.dataset.locked = locked ? 'true' : 'false';
            const state = locked ? 'locked' : allow ? 'late-open' : 'open';
            submissionDeadlinePanel.dataset.state = state;
            const badgeEl = document.getElementById('submissionDeadlineBadge');
            if (badgeEl) {
                if (state === 'locked') {
                    badgeEl.innerHTML = '<i class="fa-solid fa-lock" aria-hidden="true"></i> Closed to students';
                } else if (state === 'late-open') {
                    badgeEl.innerHTML = '<i class="fa-solid fa-unlock-keyhole" aria-hidden="true"></i> Late submissions on';
                } else {
                    badgeEl.innerHTML = '<i class="fa-regular fa-circle-check" aria-hidden="true"></i> Accepting work';
                }
            }
            if (allowLateSubmissionBtn) allowLateSubmissionBtn.style.display = locked ? 'inline-flex' : 'none';
            const overdue = (taskStatusBadgeSd?.textContent || '').trim() === 'Overdue';
            if (revokeLateSubmissionBtn) revokeLateSubmissionBtn.style.display = allow && overdue ? 'inline-flex' : 'none';
        }
        allowLateSubmissionBtn?.addEventListener('click', async () => {
            try {
                const res = await fetch('/AdminTask/SetSubmissionUnlock', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ taskId: taskIdSd, allowPastDeadline: true }),
                });
                const data = await res.json();
                if (data && data.success) {
                    showToast('Late submissions enabled');
                    syncTaskDeadlineButtons(data);
                }
            } catch {
                showToast('Could not update');
            }
        });
        revokeLateSubmissionBtn?.addEventListener('click', async () => {
            try {
                const res = await fetch('/AdminTask/SetSubmissionUnlock', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ taskId: taskIdSd, allowPastDeadline: false }),
                });
                const data = await res.json();
                if (data && data.success) {
                    showToast('Late submissions stopped');
                    syncTaskDeadlineButtons(data);
                }
            } catch {
                showToast('Could not update');
            }
        });
    }

    searchStudents?.addEventListener('input', (e) => {
        const q = (e.target.value || '').trim().toLowerCase();
        submissionsData.forEach(item => {
            const visible = item.name.toLowerCase().includes(q);
            item.row.style.display = visible ? '' : 'none';
        });
    });

    submittedList?.addEventListener('click', async (e) => {
        const row = e.target.closest('.submitted-row');
        if (!row || row.classList.contains('header')) return;
        const id = row.dataset.id;
        await openSubmissionModal(id, row.querySelector('.name-col')?.textContent?.trim() || 'Student');
    });

    // ==========================
    // ADMIN PUBLIC COMMENTS
    // ==========================
    const adminCommentContainer = document.getElementById('adminCommentContainer');
    const adminCommentForm = document.getElementById('adminCommentForm');
    const adminCommentText = document.getElementById('adminCommentText');
    const postAdminCommentBtn = document.getElementById('postAdminCommentBtn');
    const adminCommentList = document.getElementById('adminCommentList');

    function getAdminAntiForgeryToken() {
        const el = adminCommentForm ? adminCommentForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    function getTaskId() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="taskId"]') : null;
        return input ? input.value : '';
    }

    function getClassCode() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="classCode"]') : null;
        return input ? input.value : '';
    }

    async function loadAdminComments() {
        const taskId = getTaskId();
        if (!taskId || !adminCommentList) return;
        try {
            const res = await fetch('/AdminTask/GetComments?taskId=' + encodeURIComponent(taskId), { credentials: 'same-origin' });
            const data = await res.json();
            if (!data || !data.success) return;
            adminCommentList.innerHTML = '';
            data.comments.forEach(renderAdminComment);
        } catch {}
    }

    function renderAdminComment(c) {
        const box = document.createElement('div');
        box.className = 'comment-box';
        box.dataset.id = c.id;
        const nameHtml = `<div class="student-name">${escapeHtml(c.authorName)}${c.role ? ` • ${escapeHtml(c.role)}` : ''}</div>`;
        const textHtml = `<div class="comment-text">${escapeHtml(c.text)}</div>`;
        const dateHtml = `<div class="comment-datetime">${new Date(c.createdAt).toLocaleString()}</div>`;
        box.innerHTML = `${nameHtml}${textHtml}${dateHtml}`;
        if (Array.isArray(c.replies)) {
            c.replies.forEach(r => {
                const rdiv = document.createElement('div');
                rdiv.className = 'instructor-reply';
                rdiv.innerHTML = `<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${escapeHtml(r.authorName)}${r.role ? ` • ${escapeHtml(r.role)}` : ''}</span></div><div class="reply-text">${escapeHtml(r.text)}</div><div class="reply-datetime">${new Date(r.createdAt).toLocaleString()}</div>`;
                box.appendChild(rdiv);
            });
        }
        const replyToggle = document.createElement('div');
        replyToggle.className = 'reply-option';
        replyToggle.innerHTML = '<i class="fa-solid fa-reply"></i> Reply';
        box.appendChild(replyToggle);

        const replyArea = document.createElement('div');
        replyArea.className = 'reply-box-area';
        replyArea.innerHTML = `<textarea class="reply-box" placeholder="Write a reply..."></textarea><button type="button" class="reply-submit-btn">Post Reply</button>`;
        box.appendChild(replyArea);

        replyToggle.addEventListener('click', () => {
            replyArea.style.display = (replyArea.style.display === 'none' || replyArea.style.display === '') ? 'flex' : 'none';
        });

        const submitBtn = replyArea.querySelector('.reply-submit-btn');
        submitBtn.addEventListener('click', async () => {
            const text = (replyArea.querySelector('.reply-box').value || '').trim();
            if (!text) return;
            const token = getAdminAntiForgeryToken();
            try {
                const res = await fetch('/AdminTask/PostReply', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                    body: new URLSearchParams({ commentId: c.id, text })
                });
                const data = await res.json();
                if (data && data.success && data.reply) {
                    loadAdminComments();
                }
            } catch {}
        });

        adminCommentList.appendChild(box);
    }

    postAdminCommentBtn?.addEventListener('click', async () => {
        const text = (adminCommentText?.value || '').trim();
        if (!text) return;
        const token = getAdminAntiForgeryToken();
        const taskId = getTaskId();
        const classCode = getClassCode();
        try {
            const res = await fetch('/AdminTask/PostComment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: new URLSearchParams({ taskId, classCode, text })
            });
            const data = await res.json();
            if (data && data.success) {
                adminCommentText.value = '';
                loadAdminComments();
            }
        } catch {}
    });

    loadAdminComments();
});

// ==========================
// CHECK SUBMISSION MODAL
// ==========================
const checkSubmissionModal = document.getElementById('checkSubmissionModal');
const modalStudentName = document.getElementById('modalStudentName');
const modalAttachments = document.getElementById('modalAttachments');
const privateCommentDisplay = document.getElementById('privateCommentDisplay');
const closeSubmissionModal = document.getElementById('closeSubmissionModal');
const pointsEarnedInput = document.getElementById('pointsEarned');
const pointsMaxSpan = document.getElementById('pointsMax');
const remarksInput = document.getElementById('remarks');
const adminGradeForm = document.getElementById('adminGradeForm');
let submissionAttachmentMenuEl = null;

function ensureSubmissionAttachmentMenu() {
    if (!submissionAttachmentMenuEl) {
        submissionAttachmentMenuEl = document.createElement('div');
        submissionAttachmentMenuEl.className = 'attachment-menu';
        submissionAttachmentMenuEl.style.display = 'none';
        document.body.appendChild(submissionAttachmentMenuEl);
    }
}

function closeSubmissionAttachmentMenu() {
    if (submissionAttachmentMenuEl) submissionAttachmentMenuEl.style.display = 'none';
}

function triggerFileDownload(fileUrl, fileName) {
    if (!fileUrl) return;
    const a = document.createElement('a');
    a.href = fileUrl;
    a.download = fileName || '';
    document.body.appendChild(a);
    try { a.click(); } catch {}
    a.remove();
}

function openSubmissionAttachmentMenu(target) {
    ensureSubmissionAttachmentMenu();
    const fileName = target.getAttribute('data-filename') || '';
    const fileUrl = target.getAttribute('data-fileurl') || '';
    const box = target.closest('.attachment-box') || target;
    const r = box.getBoundingClientRect();
    submissionAttachmentMenuEl.innerHTML =
        '<div class="menu-item open-new"><i class="fa-solid fa-up-right-from-square"></i><span>Open in new tab</span></div>' +
        '<div class="menu-divider"></div>' +
        '<div class="menu-item download"><i class="fa-solid fa-download"></i><span>Download file</span></div>';
    submissionAttachmentMenuEl.style.left = Math.min(r.right + 18, window.innerWidth - 240) + 'px';
    submissionAttachmentMenuEl.style.top = Math.max(10, r.top - 4) + 'px';
    submissionAttachmentMenuEl.style.display = 'block';
    const openEl = submissionAttachmentMenuEl.querySelector('.open-new');
    const dlEl = submissionAttachmentMenuEl.querySelector('.download');
    openEl?.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        if (fileUrl) window.open(fileUrl, '_blank', 'noopener');
        closeSubmissionAttachmentMenu();
    });
    dlEl?.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        triggerFileDownload(fileUrl, fileName);
        closeSubmissionAttachmentMenu();
    });
}

function mapSubmissionFiles(s) {
    const files = [];
    if (Array.isArray(s?.files)) {
        s.files.forEach((f) => {
            if (!f) return;
            const name = (f.fileName || f.name || '').trim();
            if (!name) return;
            files.push({
                name,
                url: f.fileUrl || f.url || '',
                size: Number(f.fileSize || f.size || 0) || 0
            });
        });
    }
    if (Array.isArray(s?.attachments)) {
        s.attachments.forEach((f) => {
            if (!f) return;
            const name = (f.fileName || f.name || '').trim();
            if (!name) return;
            files.push({
                name,
                url: f.fileUrl || f.url || '',
                size: Number(f.fileSize || f.size || 0) || 0
            });
        });
    }
    if (s?.fileName) {
        files.push({
            name: String(s.fileName),
            url: s.fileUrl || '',
            size: Number(s.fileSize || 0) || 0
        });
    }
    const seen = new Set();
    return files.filter((f) => {
        const key = `${f.name}__${f.url}__${f.size}`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
    });
}

// Free typing for scores (no type=number max clamp); keep digits + one decimal separator
pointsEarnedInput?.addEventListener('input', function () {
    let v = this.value;
    v = v.replace(/[^\d.]/g, '');
    const dot = v.indexOf('.');
    if (dot !== -1) {
        v = v.slice(0, dot + 1) + v.slice(dot + 1).replace(/\./g, '');
    }
    if (v !== this.value) this.value = v;
});

function showGradeToast(message) {
    const t = document.getElementById('toast');
    if (!t) return;
    t.textContent = message;
    t.className = 'toast show';
    setTimeout(() => t.classList.remove('show'), 2500);
}

/** Resolves max points from the page (must not use adminTaskPage from inside DOMContentLoaded — it is out of scope here). */
function readTaskMaxPoints() {
    const main = document.getElementById('adminTaskPage');
    const fromData = main?.dataset?.taskMax;
    if (fromData != null && String(fromData).trim() !== '') {
        const n = parseFloat(String(fromData).trim());
        if (!isNaN(n) && n > 0) return n;
    }
    const pm = document.getElementById('pointsMax');
    if (pm?.textContent) {
        const n = parseFloat(pm.textContent.trim());
        if (!isNaN(n) && n > 0) return n;
    }
    return 100;
}

/** Match server-side average: extract numeric points from stored grade string (x/y, %, or raw). */
function parsePointsFromStoredGrade(gradeStr, maxGrade) {
    const g = (gradeStr || '').trim();
    if (!g) return null;
    if (g.includes('/')) {
        const parts = g.split('/');
        const got = parseFloat(parts[0]);
        return Number.isNaN(got) ? null : got;
    }
    if (g.endsWith('%')) {
        const pct = parseFloat(g.slice(0, -1));
        if (Number.isNaN(pct)) return null;
        return maxGrade * (pct / 100);
    }
    const raw = parseFloat(g);
    return Number.isNaN(raw) ? null : raw;
}

/** Recompute Submissions header "Average" from each row's data-grade (after grading without reload). */
function refreshSubmissionGradeAverage() {
    const maxEl = document.getElementById('gradeAvgMax');
    let maxGrade = readTaskMaxPoints();
    if (maxEl?.textContent) {
        const m = parseFloat(maxEl.textContent.trim());
        if (!Number.isNaN(m) && m > 0) maxGrade = m;
    }
    const rows = document.querySelectorAll('.submitted-row[data-id]:not(.header)');
    const pts = [];
    rows.forEach((row) => {
        const g = row.getAttribute('data-grade') || '';
        const p = parsePointsFromStoredGrade(g, maxGrade);
        if (p !== null) pts.push(p);
    });
    const avgEl = document.getElementById('gradeAvg');
    if (!avgEl) return;
    if (!pts.length) {
        avgEl.textContent = '0';
        return;
    }
    const avg = pts.reduce((a, b) => a + b, 0) / pts.length;
    avgEl.textContent = parseFloat(avg.toFixed(2)).toString();
}

function showGradeConfirmModal({ studentName, taskName, grade, taskMaxResolved }) {
    const modal = document.getElementById('gradeConfirmModal');
    const elStudent = document.getElementById('gradeConfirmStudent');
    const elTask = document.getElementById('gradeConfirmTask');
    const elScore = document.getElementById('gradeConfirmScore');
    const btnOk = document.getElementById('gradeConfirmOk');
    const btnCancel = document.getElementById('gradeConfirmCancel');
    if (!modal || !elStudent || !elTask || !elScore || !btnOk || !btnCancel) {
        return Promise.resolve(window.confirm(`Confirm score ${grade || '0'} / ${taskMaxResolved} for ${studentName} on ${taskName}?`));
    }

    const g = (grade || '').trim();
    const scoreShown = g === '' ? '0' : g;
    elStudent.textContent = studentName;
    elTask.textContent = taskName || 'Task';
    elScore.textContent = `${scoreShown} / ${taskMaxResolved}`;

    return new Promise((resolve) => {
        const finish = (v) => {
            modal.classList.remove('show');
            btnOk.removeEventListener('click', onOk);
            btnCancel.removeEventListener('click', onNo);
            modal.removeEventListener('click', onBackdrop);
            document.removeEventListener('keydown', onKey);
            resolve(v);
        };
        const onOk = () => finish(true);
        const onNo = () => finish(false);
        const onBackdrop = (e) => { if (e.target === modal) finish(false); };
        const onKey = (e) => { if (e.key === 'Escape') finish(false); };
        modal.classList.add('show');
        btnOk.addEventListener('click', onOk);
        btnCancel.addEventListener('click', onNo);
        modal.addEventListener('click', onBackdrop);
        document.addEventListener('keydown', onKey);
    });
}

async function openSubmissionModal(submissionId, studentName) {
    try {
        modalStudentName.textContent = studentName || 'Student';
        const res = await fetch('/AdminTask/GetSubmission?submissionId=' + encodeURIComponent(submissionId), { credentials: 'same-origin' });
        const data = await res.json();
        if (!data || !data.success || !data.submission) { checkSubmissionModal.classList.add('show'); return; }

        const s = data.submission;
        let taskMax = readTaskMaxPoints();
        if (typeof data.taskMaxGrade === 'number' && data.taskMaxGrade > 0) {
            taskMax = data.taskMaxGrade;
        }
        const mainEl = document.getElementById('adminTaskPage');
        if (mainEl) mainEl.dataset.taskMax = String(taskMax);
        pointsMaxSpan.textContent = String(taskMax);
        // Normalize grade value into points for input: support "x/y" and "%"
        let gradeText = (s.grade || '').trim();
        let pointsValue = '';
        if (gradeText) {
            if (gradeText.includes('/')) {
                const parts = gradeText.split('/');
                if (parts.length === 2) { pointsValue = parts[0].trim(); }
            } else if (gradeText.endsWith('%')) {
                const pct = parseFloat(gradeText.slice(0, -1));
                if (!isNaN(pct)) { pointsValue = String(Math.round(taskMax * (pct / 100))); }
            } else {
                pointsValue = gradeText;
            }
        }
        pointsEarnedInput.value = pointsValue;
        remarksInput.value = s.feedback || '';

        privateCommentDisplay.textContent = s.privateComment ? escapeHtml(s.privateComment) : 'No comment provided.';

        modalAttachments.innerHTML = '';
        const files = mapSubmissionFiles(s);
        if (files.length) {
            files.forEach((f) => {
                const file = document.createElement('div');
                file.className = 'attachment-box aae-file-chip';
                file.dataset.fileLabel = f.name || 'Attachment';
                const size = f.size > 0 ? ` (${Math.round(f.size / 1024)} KB)` : '';
                file.innerHTML = `<i class="fa-solid fa-file"></i><span class="attachment-name" data-filename="${escapeHtml(f.name)}" data-fileurl="${escapeHtml(f.url || '')}" title="Options for ${escapeHtml(f.name)}">${escapeHtml(f.name)}</span>${size ? `<span class="file-size">${size}</span>` : ''}`;
                modalAttachments.appendChild(file);
            });
            modalAttachments.classList.remove('empty');
            modalAttachments.classList.add('has-files');
        } else {
            modalAttachments.classList.add('empty');
            modalAttachments.classList.remove('has-files');
        }

        checkSubmissionModal.classList.add('show');

        const saveBtn = document.querySelector('.save-grade');
        saveBtn.onclick = async () => {
            const tokenEl = adminGradeForm ? adminGradeForm.querySelector('input[name="__RequestVerificationToken"]') : null;
            const token = tokenEl ? tokenEl.value : '';
            const grade = (pointsEarnedInput.value || '').trim();
            const feedback = (remarksInput.value || '').trim();
            const approve = true;
            const taskMaxResolved = readTaskMaxPoints();
            const n = parseFloat(grade);
            if (grade !== '' && !isNaN(n) && (n < 0 || n > taskMaxResolved)) {
                showGradeToast(`Enter a score from 0 to ${taskMaxResolved}.`);
                return;
            }
            const pass = !isNaN(n) && taskMaxResolved > 0
                ? ((n / taskMaxResolved) * 100) >= 75 - 1e-9
                : false;
            const studentLabel = modalStudentName?.textContent?.trim() || 'this student';
            const taskLabel = (document.getElementById('taskTitle')?.textContent || '').trim() || 'this task';
            const confirmed = await showGradeConfirmModal({
                studentName: studentLabel,
                taskName: taskLabel,
                grade,
                taskMaxResolved
            });
            if (!confirmed) return;
            const res2 = await fetch('/AdminTask/GradeSubmission', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: new URLSearchParams({ submissionId, grade, feedback, approve: String(approve), pass: String(pass) })
            });
            const d2 = await res2.json();
            if (d2 && d2.success) {
                const row = document.querySelector(`.submitted-row[data-id="${submissionId}"]`);
                if (row) {
                    row.setAttribute('data-grade', typeof d2.finalGrade === 'string' ? d2.finalGrade : '');
                }
                refreshSubmissionGradeAverage();
                showGradeToast('✅ Grade saved');
                checkSubmissionModal.classList.remove('show');
            } else {
                showGradeToast('Failed to save');
            }
        };
    } catch (err) {
        checkSubmissionModal.classList.add('show');
    }
}

closeSubmissionModal?.addEventListener('click', () => {
    checkSubmissionModal.classList.remove('show');
});

checkSubmissionModal?.addEventListener('click', (e) => {
    if (e.target === checkSubmissionModal) checkSubmissionModal.classList.remove('show');
});

function escapeHtml(s) {
    try { return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); } catch { return s; }
}

document.addEventListener('click', function (ev) {
    const t = ev.target;
    // Only show the legacy attachment menu for submission attachments (they have data-fileurl).
    // Main task attachments are handled by the in-page viewer.
    if (t && t.classList && t.classList.contains('attachment-name') && t.getAttribute('data-filename') && t.getAttribute('data-fileurl')) {
        ev.preventDefault();
        ev.stopPropagation();
        openSubmissionAttachmentMenu(t);
        return;
    }
    if (submissionAttachmentMenuEl && submissionAttachmentMenuEl.style.display === 'block' && !submissionAttachmentMenuEl.contains(t)) {
        closeSubmissionAttachmentMenu();
    }
});
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closeSubmissionAttachmentMenu();
});
  // Library modal elements (same as Student Task)
  const libraryBtn = document.getElementById('libraryBtn');
  const libraryBackdrop = document.getElementById('libraryBackdrop');
  const closeLibraryBtn = document.getElementById('closeLibraryBtn');
  const librarySearch = document.getElementById('librarySearch');
  const libraryListEl = document.getElementById('libraryList');
  const bookInfoDefault = document.getElementById('bookInfoDefault');
  const bookDetail = document.getElementById('bookDetail');
  const detailTitle = document.getElementById('detailTitle');
  const detailAuthor = document.getElementById('detailAuthor');
  const detailCategory = document.getElementById('detailCategory');
  const detailDescription = document.getElementById('detailDescription');
  const reserveBtn = document.getElementById('reserveBtn');
  const reserveBackdrop = document.getElementById('reserveBackdrop');
  const reserveYesBtn = document.getElementById('reserveYesBtn');
  const reserveNoBtn = document.getElementById('reserveNoBtn');
  // ==========================
  // Library Module
  // ==========================
  (function libraryModule() {
    const books = [
      { id: 1, title: 'Introduction to Algorithms', author: 'Thomas H. Cormen', category: 'Computer Science', description: 'A comprehensive introduction to modern algorithm design and analysis...' },
      { id: 2, title: 'Clean Code', author: 'Robert C. Martin', category: 'Software Engineering', description: 'Guidelines and best practices for writing clean, maintainable, and testable code...' },
      { id: 3, title: 'Database System Concepts', author: 'Abraham Silberschatz', category: 'Databases', description: 'Core concepts of relational databases, SQL, query optimization, and database design.' }
    ];

    let filtered = [...books];
    let selectedId = null;

    function renderList() {
      if (!libraryListEl) return;
      libraryListEl.innerHTML = '';
      if (!filtered.length) {
        const li = document.createElement('li');
        li.textContent = 'No books found.';
        li.className = 'empty';
        libraryListEl.appendChild(li);
        return;
      }
      filtered.forEach(book => {
        const li = document.createElement('li');
        li.dataset.id = book.id;
        li.setAttribute('role', 'option');
        li.className = selectedId === book.id ? 'selected' : '';
        li.innerHTML = `<div class="book-title">${escapeHtml(book.title)}</div><div class="book-category">Category: ${escapeHtml(book.category)}</div>`;
        li.addEventListener('click', () => selectBook(book.id));
        libraryListEl.appendChild(li);
      });
    }

    function selectBook(id) {
      const book = books.find(b => b.id === id);
      if (!book) return;
      selectedId = id;
      libraryListEl?.querySelectorAll('li').forEach(it => { it.classList.toggle('selected', Number(it.dataset.id) === id); });
      if (bookInfoDefault) bookInfoDefault.hidden = true;
      if (bookDetail) bookDetail.hidden = false;
      if (detailTitle) detailTitle.textContent = book.title;
      if (detailAuthor) detailAuthor.textContent = `Author: ${book.author}`;
      if (detailCategory) detailCategory.textContent = book.category;
      if (detailDescription) detailDescription.value = book.description;
      if (reserveBtn) reserveBtn.disabled = false;
    }

    function filterBooks(query) {
      query = (query || '').trim().toLowerCase();
      filtered = query ? books.filter(b => b.title.toLowerCase().includes(query) || b.category.toLowerCase().includes(query) || b.description.toLowerCase().includes(query)) : [...books];
      if (selectedId && !filtered.find(b => b.id === selectedId)) {
        selectedId = null;
        if (bookInfoDefault) bookInfoDefault.hidden = false;
        if (bookDetail) bookDetail.hidden = true;
        if (reserveBtn) reserveBtn.disabled = true;
      }
      renderList();
    }

    function openLibrary() {
      showToast('Opening Library…');
      setTimeout(() => {
        if (libraryBackdrop) libraryBackdrop.hidden = false;
        filtered = [...books];
        renderList();
        librarySearch?.focus();
      }, 500);
    }

    function closeLibrary() { if (libraryBackdrop) libraryBackdrop.hidden = true; }

    libraryBtn?.addEventListener('click', (e) => { e.preventDefault(); openLibrary(); });
    closeLibraryBtn?.addEventListener('click', closeLibrary);
    libraryBackdrop?.addEventListener('click', (e) => { if (e.target === libraryBackdrop) closeLibrary(); });
    librarySearch?.addEventListener('input', (e) => filterBooks(e.target.value));
    reserveBtn?.addEventListener('click', () => { if (!selectedId) return; reserveBackdrop.hidden = false; });
    reserveYesBtn?.addEventListener('click', () => {
        reserveBackdrop.hidden = true;
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading('/Library/ReserveSuccess', 'Redirecting to Library…', 900);
        } else {
            showToast('Redirecting to Library…');
            setTimeout(() => { window.location.href = '/Library/ReserveSuccess'; }, 900);
        }
    });
    reserveNoBtn?.addEventListener('click', () => { reserveBackdrop.hidden = true; });
    reserveBackdrop?.addEventListener('click', (e) => { if (e.target === reserveBackdrop) reserveBackdrop.hidden = true; });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') { if (!libraryBackdrop?.hidden) closeLibrary(); if (!reserveBackdrop?.hidden) reserveBackdrop.hidden = true; } });
  })();
    // Initialize download buttons for existing files
    initializeDownloadButtons();

    // ------------------ FILE DOWNLOAD FUNCTIONALITY ------------
    function initializeDownloadButtons() {
        const downloadButtons = attachmentList?.querySelectorAll('.download-btn');
        downloadButtons?.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const fileName = btn.dataset.filename;
                const taskContainer = document.getElementById('taskContainer');
                const taskId = taskContainer?.dataset?.taskId || '';
                if (!fileName || !taskId) { showToast('File not available'); return; }
                window.location.href = `/AdminTask/DownloadFile/${encodeURIComponent(fileName)}?taskId=${encodeURIComponent(taskId)}`;
            });
        });
    }
