// ==========================================================
// studentmaterial.js — Student Material Page Interactivity
// ==========================================================

// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions?.querySelectorAll('.action') || [];
const backButton = document.querySelector('.back-button');
const toast = document.getElementById('toast');
const materialForm = document.getElementById('materialForm');



// --- RADIAL MENU ---
let menuOpen = false;
menuCircle?.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions?.classList.toggle('show', menuOpen);
});
document.addEventListener('click', (e) => {
    if (!menuCircle?.contains(e.target) && !radialActions?.contains(e.target)) {
        menuOpen = false;
        radialActions?.classList.remove('show');
    }
});

// --- PAGE SELECTION ---
let currentPage = 'subjects';
actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        if (page === currentPage) {
            showToast("📚 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        let url = null;
        switch (page) {
            case 'home':
                url = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
                showToast('Going to dashboard...');
                break;
            case 'subjects':
                url = '/StudentClass'; // ✅ Correct controller path
                showToast('Opening subjects...');
                break;
            case 'todo':
                url = '/StudentTodo'; // ✅ FIXED PATH
                showToast('Opening to-do list...');
                break;
        }

        radialActions.classList.remove('show');
        menuOpen = false;
        if (url) setTimeout(() => (window.location.href = url), 600);
    });
});

// --- BACK BUTTON ---
backButton?.addEventListener('click', () => {
    const input = materialForm ? materialForm.querySelector('input[name="classCode"]') : null;
    const parts = (window.location.pathname || '').split('/').filter(Boolean);
    const i = parts.findIndex(p => p.toLowerCase() === 'studentmaterial');
    const codeFromPath = i >= 0 && parts.length >= i + 2 ? parts[i + 1] : '';
    const code = (input && input.value) || codeFromPath || '';
    const dash = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
    showToast('Returning to class...');
    setTimeout(() => (window.location.href = code ? `/StudentClass/${encodeURIComponent(code)}` : dash), 800);
});

// --- ESC CLOSE ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
    }
});

// --- TOAST ---
function showToast(message) {
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

// Public Comments
const publicCommentText = document.getElementById('publicCommentText');
const postPublicCommentBtn = document.getElementById('postPublicCommentBtn');
const publicCommentList = document.getElementById('publicCommentList');

function getAntiForgeryToken() {
    const el = materialForm ? materialForm.querySelector('input[name="__RequestVerificationToken"]') : null;
    return el ? el.value : '';
}

function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, function(m) { return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m]; });
}

async function loadComments() {
    const idInput = materialForm ? materialForm.querySelector('input[name="contentId"]') : null;
    const contentId = idInput ? idInput.value : '';
    if (!contentId || !publicCommentList) return;
    const res = await fetch('/StudentMaterial/GetComments?contentId=' + encodeURIComponent(contentId), { credentials: 'same-origin' });
    const data = await res.json();
    if (!data || !data.success) return;
    publicCommentList.innerHTML = '';
    data.comments.forEach(renderComment);
}

function renderComment(c) {
    const box = document.createElement('div');
    box.className = 'comment-box';
    const dt = new Date(c.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
    box.innerHTML = '<div class="student-name">' + escapeHtml(c.authorName) + (c.role ? ' • ' + escapeHtml(c.role) : '') + '</div>' +
        '<div class="comment-text">' + escapeHtml(c.text) + '</div>' +
        '<div class="comment-datetime">' + dt + '</div>' +
        '<div class="reply-option" role="button"><i class="fa-solid fa-reply"></i> Reply</div>' +
        '<div class="reply-box-area" hidden>' +
          '<textarea class="reply-box" placeholder="Write a reply..."></textarea>' +
          '<button class="reply-submit-btn">Post Reply</button>' +
        '</div>';

    if (Array.isArray(c.replies)) {
        c.replies.forEach(function(r) {
            const rdt = new Date(r.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            const reply = document.createElement('div');
            reply.className = 'instructor-reply';
            reply.innerHTML = '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(r.authorName) + (r.role ? ' • ' + escapeHtml(r.role) : '') + '</span></div>' +
                '<div class="reply-text">' + escapeHtml(r.text) + '</div>' +
                '<div class="reply-datetime">' + rdt + '</div>';
            const insertBefore = box.querySelector('.reply-option');
            if (insertBefore) insertBefore.insertAdjacentElement('beforebegin', reply);
        });
    }

    publicCommentList.appendChild(box);
    const replyToggle = box.querySelector('.reply-option');
    const replyArea = box.querySelector('.reply-box-area');
    const replyBtn = box.querySelector('.reply-submit-btn');
    const replyBox = box.querySelector('.reply-box');
    replyToggle && replyToggle.addEventListener('click', function() {
        if (!replyArea) return;
        const hidden = replyArea.getAttribute('hidden') !== null || replyArea.style.display === 'none' || replyArea.style.display === '';
        replyArea.style.display = hidden ? 'flex' : 'none';
        if (hidden) replyArea.removeAttribute('hidden');
    });
    replyBtn && replyBtn.addEventListener('click', async function() {
        const val = (replyBox && replyBox.value || '').trim();
        if (!val) return;
        const token = getAntiForgeryToken();
        const fd = new FormData(materialForm || undefined);
        fd.append('commentId', c.id);
        fd.append('text', val);
        const res = await fetch('/StudentMaterial/PostReply', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' });
        const data = await res.json();
        if (data && data.success && data.reply) {
            const rdt = new Date(data.reply.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            const reply = document.createElement('div');
            reply.className = 'instructor-reply';
            reply.innerHTML = '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(data.reply.authorName) + (data.reply.role ? ' • ' + escapeHtml(data.reply.role) : '') + '</span></div>' +
                '<div class="reply-text">' + escapeHtml(data.reply.text) + '</div>' +
                '<div class="reply-datetime">' + rdt + '</div>';
            const insertBefore = box.querySelector('.reply-option');
            if (insertBefore) insertBefore.insertAdjacentElement('beforebegin', reply);
            if (replyBox) replyBox.value = '';
            if (replyArea) replyArea.style.display = 'none';
        }
    });
}

postPublicCommentBtn && postPublicCommentBtn.addEventListener('click', function() {
    const text = (publicCommentText && publicCommentText.value || '').trim();
    const idInput = materialForm ? materialForm.querySelector('input[name="contentId"]') : null;
    const classInput = materialForm ? materialForm.querySelector('input[name="classCode"]') : null;
    const contentId = idInput ? idInput.value : '';
    const classCode = classInput ? classInput.value : '';
    if (!text || !contentId || !classCode || !publicCommentList) return;
    const token = getAntiForgeryToken();
    const fd = new FormData(materialForm || undefined);
    fd.append('text', text);
    fetch('/StudentMaterial/PostComment', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            if (d && d.success && d.comment) {
                renderComment(d.comment);
                if (publicCommentText) publicCommentText.value = '';
            }
        })
        .catch(function() {});
});

loadComments();

// ------------------ In-page attachment viewer ------------------
(function initInPageFileViewer() {
    const viewer = document.getElementById('smFileViewer');
    const body = document.getElementById('smViewerBody');
    const title = document.getElementById('smFileTitle');
    const openNew = document.getElementById('smOpenNewTab');
    const download = document.getElementById('smDownload');
    const closeBtn = document.getElementById('smCloseViewer');
    const openBackdrop = document.getElementById('smOpenConfirmBackdrop');
    const openModal = document.getElementById('smOpenConfirmModal');
    const openCancel = document.getElementById('smOpenConfirmCancel');
    const openOk = document.getElementById('smOpenConfirmOk');
    const openDesc = document.getElementById('smOpenConfirmDesc');
    const dlBackdrop = document.getElementById('smDownloadConfirmBackdrop');
    const dlModal = document.getElementById('smDownloadConfirmModal');
    const dlCancel = document.getElementById('smDownloadConfirmCancel');
    const dlOk = document.getElementById('smDownloadConfirmOk');
    const dlDesc = document.getElementById('smDownloadConfirmDesc');

    if (!viewer || !body || !title || !openNew || !download || !closeBtn) return;

    let currentFileName = '';
    let currentDownloadUrl = '';
    let currentObjectUrl = '';
    let lastPreview = null;
    let resizeTimer = null;

    function close() {
        viewer.style.display = 'none';
        body.innerHTML = '';
        currentFileName = '';
        currentDownloadUrl = '';
        if (currentObjectUrl) {
            try { URL.revokeObjectURL(currentObjectUrl); } catch (_) { }
            currentObjectUrl = '';
        }
    }

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
        wrap.style.cssText = 'height:100%;display:flex;align-items:center;justify-content:center;color:#0b1220;font-weight:700;';
        wrap.textContent = 'Loading preview...';
        setViewerNode(wrap);
    }

    function renderUnsupported(msg) {
        const wrap = document.createElement('div');
        wrap.style.cssText = 'padding:18px;font-family:system-ui,Segoe UI,Arial;color:#0b1220;';
        wrap.innerHTML = `<div style="font-weight:800;margin-bottom:6px;">Preview not available</div><div style="opacity:.85;">${msg}</div>`;
        setViewerNode(wrap);
    }

    function renderIframe(url, titleText) {
        const iframe = document.createElement('iframe');
        iframe.src = url;
        iframe.title = titleText || 'File preview';
        iframe.style.cssText = 'width:100%;height:100%;border:0;background:white;';
        setViewerNode(iframe);
    }

    async function renderPdf(url) {
        // Clean PDF rendering (no browser PDF chrome) using local PDF.js assets.
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
                try {
                    resolved.GlobalWorkerOptions.workerSrc = '/lib/pdfjs/pdf.worker.min.mjs';
                } catch (_) { }
                window.pdfjsLib = resolved;
                return resolved;
            })();
            return await window.__pdfjsLoading;
        }

        const pdfjs = await loadPdfJs();
        const pdf = await pdfjs.getDocument({ data: buf }).promise;

        const wrap = document.createElement('div');
        // Navy background with stacked pages.
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

    function renderImage(url) {
        const img = document.createElement('img');
        img.src = url;
        img.alt = currentFileName || 'image';
        img.style.cssText = 'max-width:100%;max-height:100%;width:auto;height:auto;display:block;object-fit:contain;';
        img.addEventListener('error', () => {
            // If the browser can't load it as an <img> (e.g., wrong content-type),
            // fall back to iframe so the user can still view/download.
            renderIframe(url, 'Image preview');
        });
        const wrap = document.createElement('div');
        wrap.style.cssText = 'height:100%;display:flex;align-items:center;justify-content:center;padding:14px;';
        wrap.appendChild(img);
        setViewerNode(wrap);
    }

    async function renderText(url) {
        const pre = document.createElement('pre');
        pre.style.cssText = 'margin:0;padding:14px;white-space:pre-wrap;word-break:break-word;font:13px/1.5 ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;';
        const resp = await fetch(url, { credentials: 'same-origin' });
        if (!resp.ok) throw new Error('text fetch failed');
        pre.textContent = await resp.text();
        setViewerNode(pre);
    }

    async function getPublicPreviewUrl(fileName, contentId) {
        const fd = new FormData();
        fd.append('fileName', fileName);
        fd.append('contentId', contentId);
        const resp = await fetch('/StudentMaterial/CreatePublicPreviewToken', { method: 'POST', body: fd, credentials: 'same-origin' });
        if (!resp.ok) throw new Error('token request failed');
        const data = await resp.json();
        if (!data || !data.success || !data.token) throw new Error('token missing');
        return `${window.location.origin}/PublicPreview/${encodeURIComponent(data.token)}`;
    }

    document.addEventListener('click', (e) => {
        const btn = e.target && e.target.closest ? e.target.closest('.sm-attachment-open') : null;
        if (!btn) return;
        e.preventDefault();
        e.stopPropagation();
        const fileName = btn.getAttribute('data-filename') || '';
        const contentId = btn.getAttribute('data-contentid') || '';
        if (!fileName || !contentId) return;
        const viewUrl = `/StudentMaterial/ViewFile/${encodeURIComponent(fileName)}?contentId=${encodeURIComponent(contentId)}`;
        const downloadUrl = `/StudentMaterial/DownloadFile/${encodeURIComponent(fileName)}?contentId=${encodeURIComponent(contentId)}`;
        title.textContent = fileName || 'File';
        openNew.href = viewUrl;
        download.href = '#';
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

                // Try default iframe preview as a last resort.
                renderIframe(viewUrl, 'File preview');
                lastPreview = { type: 'iframe', url: viewUrl };
            } catch (err) {
                console.error(err);
                // Fallback to iframe if custom preview fails (e.g., module blocked by CSP).
                try {
                    renderIframe(viewUrl, 'File preview');
                    lastPreview = { type: 'iframe', url: viewUrl };
                } catch (_) { }
                renderUnsupported('Use “Open in new tab” or “Download”.');
            }
        })();
    });

    window.addEventListener('resize', () => {
        if (resizeTimer) window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(async () => {
            if (!viewer || viewer.style.display !== 'block' || !lastPreview) return;
            if (lastPreview.type === 'pdf' && lastPreview.url) {
                try {
                    renderLoading();
                    await renderPdf(lastPreview.url);
                } catch (_) { }
            }
        }, 140);
    });

    openNew.addEventListener('click', async (e) => {
        e.preventDefault();
        e.stopPropagation();
        const href = openNew.getAttribute('href') || '';
        if (!href || href === '#') return;

        const ok = await (async function confirmOpen() {
            if (!openBackdrop || !openModal || !openCancel || !openOk) {
                return window.confirm(`Open "${currentFileName}" in a new tab?`);
            }

            if (openDesc) {
                openDesc.textContent = `Do you want to open "${currentFileName}" in a new tab?`;
            }

            openBackdrop.removeAttribute('hidden');
            openBackdrop.setAttribute('aria-hidden', 'false');
            openModal.removeAttribute('hidden');
            openModal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            openCancel.focus();

            return await new Promise((resolve) => {
                function close(result) {
                    openBackdrop.setAttribute('hidden', '');
                    openBackdrop.setAttribute('aria-hidden', 'true');
                    openModal.setAttribute('hidden', '');
                    openModal.setAttribute('aria-hidden', 'true');
                    document.body.style.overflow = '';
                    cleanup();
                    resolve(result);
                }
                function onCancel(ev) { ev.preventDefault(); close(false); }
                function onOk(ev) { ev.preventDefault(); close(true); }
                function onBackdrop(ev) { ev.preventDefault(); close(false); }
                function onKey(ev) {
                    if (ev.key === 'Escape' && !openModal.hasAttribute('hidden')) close(false);
                }
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
        })();

        if (!ok) return;
        window.open(href, '_blank', 'noopener');
    });

    download.addEventListener('click', async (e) => {
        e.preventDefault();
        e.stopPropagation();

        if (!currentDownloadUrl || !currentFileName) return;

        const ok = await (async function confirmDownload() {
            if (!dlBackdrop || !dlModal || !dlCancel || !dlOk) {
                return window.confirm(`Download "${currentFileName}"?`);
            }

            if (dlDesc) {
                dlDesc.textContent = `Do you want to download "${currentFileName}"?`;
            }

            dlBackdrop.removeAttribute('hidden');
            dlBackdrop.setAttribute('aria-hidden', 'false');
            dlModal.removeAttribute('hidden');
            dlModal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            dlCancel.focus();

            return await new Promise((resolve) => {
                function close(result) {
                    dlBackdrop.setAttribute('hidden', '');
                    dlBackdrop.setAttribute('aria-hidden', 'true');
                    dlModal.setAttribute('hidden', '');
                    dlModal.setAttribute('aria-hidden', 'true');
                    document.body.style.overflow = '';
                    cleanup();
                    resolve(result);
                }
                function onCancel(ev) { ev.preventDefault(); close(false); }
                function onOk(ev) { ev.preventDefault(); close(true); }
                function onBackdrop(ev) { ev.preventDefault(); close(false); }
                function onKey(ev) {
                    if (ev.key === 'Escape' && !dlModal.hasAttribute('hidden')) close(false);
                }
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
        })();

        if (!ok) return;

        // Use a normal navigation-triggered download. This handles redirects and avoids fetch/CORS issues.
        const a = document.createElement('a');
        a.href = currentDownloadUrl;
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        a.remove();
    });

    closeBtn.addEventListener('click', (e) => {
        e.preventDefault();
        close();
    });

    viewer.addEventListener('click', (e) => {
        // click outside the modal closes
        if (e.target === viewer) close();
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') close();
    });
})();
