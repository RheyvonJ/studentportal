document.addEventListener('DOMContentLoaded', () => {
    const adminMaterialPage = () => document.getElementById('adminMaterialPage');
    // ------------------ ELEMENT REFERENCES -------------------
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const toast = document.getElementById('toast');
    const backButton = document.querySelector('.back-button');

    const materialContainer = document.getElementById('materialContainer');
    const materialName = document.getElementById('materialName');
    const materialDescription = document.getElementById('materialDescription');
    const attachmentList = document.getElementById('attachmentList');
    const editControls = document.getElementById('editControls');
    const saveEditBtn = document.getElementById('saveEditBtn');
    const cancelEditBtn = document.getElementById('cancelEditBtn');
    const makeChangesButton = document.getElementById('makeChangesButton');
    const adminActions = document.getElementById('adminActions');
    const deleteBackdrop = document.getElementById('deleteMaterialConfirmBackdrop');
    const deleteModal = document.getElementById('deleteMaterialConfirmModal');
    const confirmDelete = document.getElementById('confirmDelete');
    const cancelDelete = document.getElementById('cancelDelete');
    const dateInfo = document.getElementById('dateInfo');
    const escapeHtmlForMeta = (str) => {
        if (str == null || str === '') return '';
        const d = document.createElement('div');
        d.textContent = String(str);
        return d.innerHTML;
    };
    const buildMaterialMetaRowHtml = (posted, edited) => {
        const editedBlock = edited && String(edited).trim()
            ? `<div class="aae-meta-item"><span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-pen-to-square"></i></span><span class="aae-meta-label">Edited</span><span class="aae-meta-value">${escapeHtmlForMeta(edited)}</span></div>`
            : '';
        return `
            <div class="aae-meta-item">
                <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-calendar"></i></span>
                <span class="aae-meta-label">Posted</span>
                <span class="aae-meta-value">${escapeHtmlForMeta(posted || '—')}</span>
            </div>
            ${editedBlock}`;
    };
    const applyMaterialMetaToDateInfo = (el, posted, edited) => {
        if (!el) return;
        el.dataset.posted = posted || '';
        el.dataset.edited = edited || '';
        el.innerHTML = buildMaterialMetaRowHtml(posted, edited);
    };

    // ------------------ STATE VARIABLES -------------------
    let isEditing = false;
    let originalName = '';
    let originalDescription = '';
    let menuOpen = false;
    let adminMenuOpen = false;

    // ------------------ INITIAL SETUP -------------------
    if (editControls) {
        editControls.style.display = 'none';
        editControls.classList.remove('visible');
    }

    // Initialize download buttons for existing files
    initializeDownloadButtons();

    // In-page attachment viewer (same UX as StudentMaterial)
    initAdminInPageFileViewer();

    // Bottom bar interactions are managed by AdminBottomBar.js

    // ------------------ BACK BUTTON ---------------------------
    backButton?.addEventListener('click', () => {
        const cc = adminMaterialPage()?.dataset?.classCode || document.body?.dataset?.classCode || '';
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
    
    // manage-buttons removed

    // ------------------ ADMIN ACTIONS MENU --------------------
    makeChangesButton?.addEventListener('click', (e) => {
        e.stopPropagation();
        adminMenuOpen = !adminMenuOpen;
        adminActions?.classList.toggle('show', adminMenuOpen);
    });

    document.addEventListener('click', (e) => {
        if (!adminActions?.contains(e.target) && e.target !== makeChangesButton) {
            adminActions?.classList.remove('show');
            adminMenuOpen = false;
        }
    });

    // Admin Actions
    adminActions?.querySelectorAll('.admin-action').forEach(action => {
        action.addEventListener('click', (e) => {
            e.stopPropagation();
            const actionType = action.dataset.action;

            if (actionType === 'edit') {
                startEditing();
            } else if (actionType === 'delete') {
                showDeleteModal();
            }

            adminActions.classList.remove('show');
            adminMenuOpen = false;
        });
    });

    // ------------------ EDITING FUNCTIONALITY -----------------
    function startEditing() {
        isEditing = true;
        originalName = materialName.textContent;
        originalDescription = materialDescription.textContent;

        materialName.contentEditable = true;
        materialDescription.contentEditable = true;

        materialName.focus();
        placeCaretAtEnd(materialName);
        revealEditControls();

        showToast('✏️ Editing mode enabled');
    }

    function cancelEditing() {
        isEditing = false;
        materialName.textContent = originalName;
        materialDescription.textContent = originalDescription;

        materialName.contentEditable = false;
        materialDescription.contentEditable = false;

        hideEditControls();
        showToast('Editing cancelled');
    }

    async function saveEditing() {
        const newName = materialName.textContent.trim();
        const newDescription = materialDescription.textContent.trim();

        if (!newName) {
            showToast('⚠️ Material name cannot be empty', 'warning');
            materialName.textContent = originalName;
            return;
        }

        try {
            const materialId = materialContainer.dataset.materialId;
            const response = await fetch('/AdminMaterial/UpdateMaterial', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    materialId: materialId,
                    title: newName,
                    description: newDescription
                })
            });

            if (!response.ok) {
                throw new Error('Failed to update material');
            }

            const result = await response.json();

            if (result.success) {
                isEditing = false;
                materialName.contentEditable = false;
                materialDescription.contentEditable = false;

                hideEditControls();

                // Update date info
                const today = new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                if (dateInfo) {
                    const postedPart = dateInfo.dataset.posted || '';
                    applyMaterialMetaToDateInfo(dateInfo, postedPart, today);
                }

                showToast('✅ Material updated successfully');
            } else {
                throw new Error(result.message);
            }
        } catch (error) {
            console.error('Error updating material:', error);
            showToast('❌ Failed to update material: ' + error.message, 'error');
            cancelEditing();
        }
    }

    function revealEditControls() {
        if (!editControls) return;
        editControls.style.display = 'flex';
        requestAnimationFrame(() => editControls.classList.add('visible'));
    }

    function hideEditControls() {
        if (!editControls) return;
        editControls.classList.remove('visible');
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
        } catch (err) { /* ignore */ }
    }

    // ------------------ FILE DOWNLOAD FUNCTIONALITY ------------
    function initializeDownloadButtons() {
        const downloadButtons = attachmentList?.querySelectorAll('.download-btn');
        downloadButtons?.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const fileName = btn.dataset.filename;
                const materialId = materialContainer.dataset.materialId;
                window.location.href = `/AdminMaterial/DownloadFile/${encodeURIComponent(fileName)}?contentId=${materialId}`;
            });
        });
    }

    function initAdminInPageFileViewer() {
        const viewer = document.getElementById('amFileViewer');
        const body = document.getElementById('amViewerBody');
        const title = document.getElementById('amFileTitle');
        const openNew = document.getElementById('amOpenNewTab');
        const download = document.getElementById('amDownload');
        const closeBtn = document.getElementById('amCloseViewer');

        const openBackdrop = document.getElementById('amOpenConfirmBackdrop');
        const openModal = document.getElementById('amOpenConfirmModal');
        const openCancel = document.getElementById('amOpenConfirmCancel');
        const openOk = document.getElementById('amOpenConfirmOk');
        const openDesc = document.getElementById('amOpenConfirmDesc');

        const dlBackdrop = document.getElementById('amDownloadConfirmBackdrop');
        const dlModal = document.getElementById('amDownloadConfirmModal');
        const dlCancel = document.getElementById('amDownloadConfirmCancel');
        const dlOk = document.getElementById('amDownloadConfirmOk');
        const dlDesc = document.getElementById('amDownloadConfirmDesc');

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

        async function getPublicPreviewUrl(fileName, contentId) {
            // Reuse StudentMaterial token endpoint so Google viewer can fetch.
            const fd = new FormData();
            fd.append('fileName', fileName);
            fd.append('contentId', contentId);
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
            if (!openBackdrop || !openModal || !openCancel || !openOk) {
                return window.confirm(`Open "${currentFileName}" in a new tab?`);
            }
            if (openDesc) openDesc.textContent = `Do you want to open "${currentFileName}" in a new tab?`;
            openBackdrop.removeAttribute('hidden');
            openBackdrop.setAttribute('aria-hidden', 'false');
            openModal.removeAttribute('hidden');
            openModal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            openCancel.focus();

            return await new Promise((resolve) => {
                function closeModal(result) {
                    openBackdrop.setAttribute('hidden', '');
                    openBackdrop.setAttribute('aria-hidden', 'true');
                    openModal.setAttribute('hidden', '');
                    openModal.setAttribute('aria-hidden', 'true');
                    document.body.style.overflow = '';
                    cleanup();
                    resolve(result);
                }
                function onCancel(ev) { ev.preventDefault(); closeModal(false); }
                function onOk(ev) { ev.preventDefault(); closeModal(true); }
                function onBackdrop(ev) { ev.preventDefault(); closeModal(false); }
                function onKey(ev) { if (ev.key === 'Escape' && !openModal.hasAttribute('hidden')) closeModal(false); }
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
            if (!dlBackdrop || !dlModal || !dlCancel || !dlOk) {
                return window.confirm(`Download "${currentFileName}"?`);
            }
            if (dlDesc) dlDesc.textContent = `Do you want to download "${currentFileName}"?`;
            dlBackdrop.removeAttribute('hidden');
            dlBackdrop.setAttribute('aria-hidden', 'false');
            dlModal.removeAttribute('hidden');
            dlModal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            dlCancel.focus();

            return await new Promise((resolve) => {
                function closeModal(result) {
                    dlBackdrop.setAttribute('hidden', '');
                    dlBackdrop.setAttribute('aria-hidden', 'true');
                    dlModal.setAttribute('hidden', '');
                    dlModal.setAttribute('aria-hidden', 'true');
                    document.body.style.overflow = '';
                    cleanup();
                    resolve(result);
                }
                function onCancel(ev) { ev.preventDefault(); closeModal(false); }
                function onOk(ev) { ev.preventDefault(); closeModal(true); }
                function onBackdrop(ev) { ev.preventDefault(); closeModal(false); }
                function onKey(ev) { if (ev.key === 'Escape' && !dlModal.hasAttribute('hidden')) closeModal(false); }
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
            const t = e.target;
            const btn = t && t.closest ? t.closest('.attachment-name') : null;
            if (!btn) return;
            e.preventDefault();
            e.stopPropagation();

            const fileName = btn.getAttribute('data-filename') || '';
            const contentId = btn.getAttribute('data-contentid') || (materialContainer?.dataset?.materialId || '');
            if (!fileName || !contentId) return;

            const viewUrl = `/AdminMaterial/ViewFile/${encodeURIComponent(fileName)}?contentId=${encodeURIComponent(contentId)}`;
            const downloadUrl = `/AdminMaterial/DownloadFile/${encodeURIComponent(fileName)}?contentId=${encodeURIComponent(contentId)}`;

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
                    if (isPdf) {
                        await renderPdf(viewUrl);
                        lastPreview = { type: 'pdf', url: viewUrl };
                        return;
                    }
                    if (isImg) {
                        renderImage(viewUrl);
                        lastPreview = { type: 'img', url: viewUrl };
                        return;
                    }
                    if (isText) {
                        await renderText(viewUrl);
                        lastPreview = { type: 'text', url: viewUrl };
                        return;
                    }
                    if (isOffice) {
                        const publicUrl = await getPublicPreviewUrl(fileName, contentId);
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

    // ------------------ REPLACE ATTACHMENT (upload + link) -----
    const replaceFileBtn = document.getElementById('replaceFileBtn');
    const replaceFileInput = document.getElementById('replaceFileInput');

    replaceFileBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!isEditing) { showToast('Enable edit mode first'); return; }
        replaceFileInput?.click();
    });

    replaceFileInput?.addEventListener('change', async () => {
        const file = replaceFileInput.files && replaceFileInput.files[0];
        if (!file) return;
        try {
            const classCode = adminMaterialPage()?.dataset?.classCode || document.body?.dataset?.classCode || '';
            const fd = new FormData();
            fd.append('file', file);
            fd.append('classCode', classCode);
            fd.append('type', 'material');
            const up = await fetch('/AdminClass/UploadFile', { method: 'POST', body: fd });
            if (!up.ok) throw new Error('Upload failed');
            const upRes = await up.json();
            if (!upRes.success) throw new Error(upRes.message || 'Upload failed');

            const materialId = materialContainer?.dataset?.materialId || '';
            const linkRes = await fetch('/AdminMaterial/ReplaceAttachment', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ materialId, fileName: file.name, fileUrl: upRes.fileUrl })
            });
            if (!linkRes.ok) throw new Error('Replace failed');
            const linkJson = await linkRes.json();
            if (!linkJson.success) throw new Error(linkJson.message || 'Replace failed');

            // Update UI: show single attachment
            if (attachmentList) {
                attachmentList.innerHTML = '';
                const box = document.createElement('div');
                box.className = 'attachment-box aae-file-chip';
                box.dataset.fileLabel = file.name;
                const mid = materialContainer?.dataset?.materialId || '';
                const icon = document.createElement('i');
                icon.className = 'fa-solid fa-file';
                const span = document.createElement('span');
                span.className = 'attachment-name';
                span.dataset.filename = file.name;
                span.dataset.contentid = mid;
                span.title = 'Options for ' + file.name;
                span.textContent = file.name;
                const dlBtn = document.createElement('button');
                dlBtn.type = 'button';
                dlBtn.className = 'download-btn';
                dlBtn.dataset.filename = file.name;
                dlBtn.title = 'Download ' + file.name;
                dlBtn.innerHTML = '<i class="fa-solid fa-download"></i>';
                box.appendChild(icon);
                box.appendChild(span);
                box.appendChild(dlBtn);
                attachmentList.appendChild(box);
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

    

    function openSlspModal(backdrop, modal) {
        if (backdrop) {
            backdrop.removeAttribute('hidden');
            backdrop.setAttribute('aria-hidden', 'false');
        }
        if (modal) {
            modal.removeAttribute('hidden');
            modal.setAttribute('aria-hidden', 'false');
        }
        document.body.style.overflow = 'hidden';
    }

    function closeSlspModal(backdrop, modal) {
        if (backdrop) {
            backdrop.setAttribute('hidden', '');
            backdrop.setAttribute('aria-hidden', 'true');
        }
        if (modal) {
            modal.setAttribute('hidden', '');
            modal.setAttribute('aria-hidden', 'true');
        }
        document.body.style.overflow = '';
    }

    // ------------------ DELETE FUNCTIONALITY -----------------
    function showDeleteModal() {
        openSlspModal(deleteBackdrop, deleteModal);
    }

    function hideDeleteModal() {
        closeSlspModal(deleteBackdrop, deleteModal);
    }

    function showSaveConfirm() {
        const b = document.getElementById('saveMaterialConfirmBackdrop');
        const m = document.getElementById('saveMaterialConfirmModal');
        openSlspModal(b, m);
    }

    function hideSaveConfirm() {
        const b = document.getElementById('saveMaterialConfirmBackdrop');
        const m = document.getElementById('saveMaterialConfirmModal');
        closeSlspModal(b, m);
    }

    async function deleteMaterial() {
        try {
            const materialId = materialContainer.dataset.materialId;
            const response = await fetch('/AdminMaterial/DeleteMaterial', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ materialId: materialId })
            });

            if (!response.ok) {
                throw new Error('Failed to delete material');
            }

            const result = await response.json();

            if (result.success) {
                showToast('✅ Material deleted successfully');
                setTimeout(() => {
                    const classCode = adminMaterialPage()?.dataset?.classCode || document.body?.dataset?.classCode || '';
                    const url = classCode ? `/AdminClass/${encodeURIComponent(classCode)}` : '/AdminClass';
                    if (typeof window.navigateWithProfessorLoading === 'function') {
                        window.navigateWithProfessorLoading(url, null, 600);
                    } else {
                        window.location.href = url;
                    }
                }, 1000);
            } else {
                throw new Error(result.message);
            }
        } catch (error) {
            console.error('Error deleting material:', error);
            showToast('❌ Failed to delete material: ' + error.message, 'error');
        } finally {
            hideDeleteModal();
        }
    }

    // ------------------ EVENT LISTENERS ----------------------
    saveEditBtn?.addEventListener('click', showSaveConfirm);
    document.getElementById('saveMaterialConfirmOk')?.addEventListener('click', () => {
        hideSaveConfirm();
        void saveEditing();
    });
    document.getElementById('saveMaterialConfirmCancel')?.addEventListener('click', hideSaveConfirm);
    document.getElementById('saveMaterialConfirmBackdrop')?.addEventListener('click', (e) => {
        if (e.target === document.getElementById('saveMaterialConfirmBackdrop')) hideSaveConfirm();
    });
    cancelEditBtn?.addEventListener('click', cancelEditing);
    confirmDelete?.addEventListener('click', deleteMaterial);
    cancelDelete?.addEventListener('click', hideDeleteModal);

    // ------------------ TOAST FUNCTION -----------------------
    function showToast(message, type = '') {
        if (!toast) return;
        toast.textContent = message;
        toast.className = `toast show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    // ------------------ EVENT HANDLERS -----------------------
    deleteBackdrop?.addEventListener('click', (e) => {
        if (e.target === deleteBackdrop) hideDeleteModal();
    });

    // Escape key handling
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            if (deleteModal && !deleteModal.hasAttribute('hidden')) hideDeleteModal();
            const saveMat = document.getElementById('saveMaterialConfirmModal');
            if (saveMat && !saveMat.hasAttribute('hidden')) hideSaveConfirm();
            if (isEditing) {
                cancelEditing();
            }
            radialActions?.classList.remove('show');
            adminActions?.classList.remove('show');
            menuOpen = false;
            adminMenuOpen = false;
        }
    });

    const adminCommentContainer = document.getElementById('adminCommentContainer');
    const adminCommentForm = document.getElementById('adminCommentForm');
    const adminCommentText = document.getElementById('adminCommentText');
    const postAdminCommentBtn = document.getElementById('postAdminCommentBtn');
    const adminCommentList = document.getElementById('adminCommentList');

    function getAdminAntiForgeryToken() {
        const el = adminCommentForm ? adminCommentForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    function getContentId() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="contentId"]') : null;
        return input ? input.value : '';
    }

    function getClassCode() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="classCode"]') : null;
        return input ? input.value : '';
    }

    async function loadAdminComments() {
        const contentId = getContentId();
        if (!contentId || !adminCommentList) return;
        try {
            const res = await fetch('/StudentMaterial/GetComments?contentId=' + encodeURIComponent(contentId), { credentials: 'same-origin' });
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
        const nameHtml = '<div class="student-name">' + escapeHtml(c.authorName) + (c.role ? ' • ' + escapeHtml(c.role) : '') + '</div>';
        const textHtml = '<div class="comment-text">' + escapeHtml(c.text) + '</div>';
        const dateHtml = '<div class="comment-datetime">' + new Date(c.createdAt).toLocaleString() + '</div>';
        box.innerHTML = nameHtml + textHtml + dateHtml;
        if (Array.isArray(c.replies)) {
            c.replies.forEach(r => {
                const rdiv = document.createElement('div');
                rdiv.className = 'instructor-reply';
                rdiv.innerHTML = '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(r.authorName) + (r.role ? ' • ' + escapeHtml(r.role) : '') + '</span></div><div class="reply-text">' + escapeHtml(r.text) + '</div><div class="reply-datetime">' + new Date(r.createdAt).toLocaleString() + '</div>';
                box.appendChild(rdiv);
            });
        }
        const replyToggle = document.createElement('div');
        replyToggle.className = 'reply-option';
        replyToggle.innerHTML = '<i class="fa-solid fa-reply"></i> Reply';
        box.appendChild(replyToggle);

        const replyArea = document.createElement('div');
        replyArea.className = 'reply-box-area';
        replyArea.innerHTML = '<textarea class="reply-box" placeholder="Write a reply..."></textarea><button type="button" class="reply-submit-btn">Post Reply</button>';
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
                const res = await fetch('/StudentMaterial/PostReply', {
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
        const contentId = getContentId();
        const classCode = getClassCode();
        try {
            const res = await fetch('/StudentMaterial/PostComment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: new URLSearchParams({ contentId, classCode, text })
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

function escapeHtml(s) {
    try { return s.replace(/[&<>"']/g, function(c){ return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]; }); } catch { return s; }
}
