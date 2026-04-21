// ==========================================================
// studenttask.js — Cleaned & Optimized
// Handles: Submit / Mark Done / Unsubmit flows,
// Attachment List Modal, Reply Boxes
// ==========================================================

document.addEventListener('DOMContentLoaded', () => {

    // --- GLOBAL ELEMENTS ---
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const backButton = document.querySelector('.back-button');
    const toast = document.getElementById('toast');

    const fileInput = document.getElementById('fileInput');
    const markDoneBtn = document.querySelector('.mark-done-btn');
    const confirmModal = document.getElementById('confirmModal');
    const confirmYes = document.getElementById('confirmYes');
    const confirmNo = document.getElementById('confirmNo');

    const attachmentListBtn = document.getElementById('attachmentListBtn');
    const attachmentListModal = document.getElementById('attachmentListModal');
    const attachmentListContent = document.getElementById('attachmentListContent');
    const closeAttachmentList = document.getElementById('closeAttachmentList');

    const submitBtn = document.getElementById('submitBtn');
    const submitConfirmModal = document.getElementById('submitConfirmModal');
    const submitConfirmYes = document.getElementById('submitConfirmYes');
    const submitConfirmNo = document.getElementById('submitConfirmNo');

    const submitContainer = document.getElementById('submitContainer');
    const formEl = document.getElementById('taskForm');

    function getAntiForgeryToken() {
        const el = formEl ? formEl.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    const unsubmitConfirmModal = document.getElementById('unsubmitConfirmModal');
    const unsubmitYes = document.getElementById('unsubmitYes');
    const unsubmitNo = document.getElementById('unsubmitNo');

    const submittedFileData = window.__submittedFileData || null;
    let clientObjectUrls = [];

    // --- VARIABLES ---
    let uploadedFiles = [];
    let originalSubmitHTML = submitContainer ? submitContainer.innerHTML : null;
    const totalPoints = window.__TOTAL_POINTS || 'N';
    const instructorRemarks = window.__INSTRUCTOR_REMARKS || '';


    // ==========================================================
    // Toast
    // ==========================================================
    let toastTimeout = null;
    function showToast(message, duration = 1500) {
        toast.textContent = message;
        toast.classList.add('show');
        clearTimeout(toastTimeout);
        toastTimeout = setTimeout(() => toast.classList.remove('show'), duration);
    }

    function getClassCodeFromPath() {
        const parts = (window.location.pathname || '').split('/').filter(Boolean);
        const i = parts.findIndex(p => p.toLowerCase() === 'studenttask');
        return i >= 0 && parts.length >= i + 2 ? parts[i + 1] : '';
    }
    function getClassCode() {
        const input = formEl ? formEl.querySelector('input[name="classCode"]') : null;
        return (input && input.value) || getClassCodeFromPath() || '';
    }

    // ==========================================================
    // Back button
    // ==========================================================
    backButton?.addEventListener('click', () => {
        const code = getClassCode();
        const dash = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
        showToast('Returning to class...');
        setTimeout(() => (window.location.href = code ? `/StudentClass/${encodeURIComponent(code)}` : dash), 800);
    });

    // ===== SHOW / HIDE REPLY BOX =====
    document.querySelectorAll(".reply-option").forEach(button => {
        button.addEventListener("click", () => {
            const commentBox = button.closest(".comment-box");
            const replyArea = commentBox.querySelector(".reply-box-area");

            if (!replyArea) return;

            replyArea.style.display =
                (replyArea.style.display === "none" || replyArea.style.display === "")
                    ? "flex"
                    : "none";
        });
    });

    // ===== SUBMIT A REPLY =====
    document.querySelectorAll(".reply-submit-btn").forEach(btn => {
        btn.addEventListener("click", () => {

            const replyArea = btn.closest(".reply-box-area");
            const commentBox = btn.closest(".comment-box");
            const textarea = replyArea.querySelector(".reply-box");

            let replyText = textarea.value.trim();
            if (replyText === "") return;

            // Placeholder instructor name (replace if dynamic)
            const instructorName = "Instructor Santos";

            // Timestamp
            const now = new Date();
            const dateTimeString = now.toLocaleString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
                hour: "numeric",
                minute: "2-digit"
            });

            // ===== INSERT REPLY INTO COMMENT =====
            let replyHtml = `
            <div class="instructor-reply">
                <div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${instructorName}</span></div>
                <div class="reply-text">${replyText}</div>
                <div class="reply-datetime">${dateTimeString}</div>
            </div>
        `;

            // Insert reply BEFORE the reply-option (so structure stays clean)
            const replyBtn = commentBox.querySelector(".reply-option");
            replyBtn.insertAdjacentHTML("beforebegin", replyHtml);

            // Reset input
            textarea.value = "";
            replyArea.style.display = "none";
        });
    });




    // ==========================================================
    // Attachment Handling
    // ==========================================================
    fileInput?.addEventListener('change', (e) => {
        uploadedFiles = Array.from(e.target.files || []);
    });

    function populateAttachmentList() {
        attachmentListContent.innerHTML = '';

        if (submittedFileData) {
            const div = document.createElement('div');
            div.className = 'attachment-box';
            const name = submittedFileData.fileName || '';
            div.dataset.fileLabel = name;
            const url = submittedFileData.fileUrl || '';
            const sizeText = submittedFileData.sizeText || '';
            div.innerHTML = `<i class="fa-solid fa-file"></i> ${escapeHtml(name)}${url ? ` <a href="${escapeHtml(url)}" class="download-link" style="margin-left:auto" download title="Download"><i class="fa-solid fa-download"></i></a>` : ''}${sizeText ? ` <span class="file-size" style="margin-left:8px; color:#64748b;">${escapeHtml(sizeText)}</span>` : ''}`;
            attachmentListContent.appendChild(div);
        }

        clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
        clientObjectUrls = [];

        uploadedFiles.forEach(f => {
            const div = document.createElement('div');
            div.className = 'attachment-box';
            div.dataset.fileLabel = f.name;
            const sizeText = typeof f.size === 'number' ? formatSize(f.size) : '';
            const url = URL.createObjectURL(f);
            clientObjectUrls.push(url);
            div.innerHTML = `<i class="fa-solid fa-file"></i> ${escapeHtml(f.name)} <a href="${url}" class="download-link client-file" style="margin-left:auto" download title="Download"><i class="fa-solid fa-download"></i></a>${sizeText ? ` <span class="file-size" style="margin-left:8px; color:#64748b;">${escapeHtml(sizeText)}</span>` : ''}`;
            attachmentListContent.appendChild(div);
        });

        if (!attachmentListContent.children.length) {
            const p = document.createElement('div');
            p.style.color = '#475569';
            p.style.fontSize = '0.95rem';
            p.textContent = 'No attachments';
            attachmentListContent.appendChild(p);
        }
    }

    function downloadAttachments() {
        const toClick = [];
        if (submittedFileData && submittedFileData.fileUrl) {
            const a = document.createElement('a');
            a.href = submittedFileData.fileUrl;
            a.download = submittedFileData.fileName || '';
            document.body.appendChild(a);
            toClick.push(a);
        }
        clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
        clientObjectUrls = [];
        uploadedFiles.forEach(f => {
            const url = URL.createObjectURL(f);
            clientObjectUrls.push(url);
            const a = document.createElement('a');
            a.href = url;
            a.download = f.name || 'file';
            document.body.appendChild(a);
            toClick.push(a);
        });
        if (!toClick.length) return false;
        toClick.forEach(a => { try { a.click(); } catch {} a.remove(); });
        setTimeout(() => { clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} }); clientObjectUrls = []; }, 1500);
        return true;
    }

    attachmentListBtn?.addEventListener('click', () => {
        const ok = downloadAttachments();
        if (!ok) {
            showToast('there is no file');
            return;
        }
    });
    closeAttachmentList?.addEventListener('click', () => {
        attachmentListModal.hidden = true;
        clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
        clientObjectUrls = [];
    });
    attachmentListModal?.addEventListener('click', (e) => {
        if (e.target === attachmentListModal) {
            attachmentListModal.hidden = true;
            clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
            clientObjectUrls = [];
        }
    });


    // ==========================================================
    // Mark Done Logic
    // ==========================================================
    markDoneBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!fileInput.files.length) {
            if (confirmModal) {
                confirmModal.removeAttribute('hidden');
                confirmModal.setAttribute('aria-hidden', 'false');
            }
        } else {
            submitMarkAsDone();
        }
    });

    confirmYes?.addEventListener('click', () => {
        if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
        submitMarkAsDone();
    });

    confirmNo?.addEventListener('click', () => {
        if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
    });

    confirmModal?.addEventListener('click', (e) => {
        if (e.target === confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
    });
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && confirmModal && confirmModal.getAttribute('aria-hidden') === 'false') {
            confirmModal.setAttribute('aria-hidden', 'true');
        }
    });

    async function submitMarkAsDone() {
        if (!formEl) return;
        const fd = new FormData(formEl);
        const token = getAntiForgeryToken();
        try {
            const res = await fetch('/StudentTask/MarkAsDone', {
                method: 'POST',
                body: fd,
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token }
            });
            const data = await res.json();
            if (data && data.success) {
                showToast("Marked as done");
                setTimeout(() => location.reload(), 800);
            } else {
                showToast((data && data.message) || 'Action failed');
            }
        } catch (err) {
            showToast('Network error');
        }
    }


    // ==========================================================
    // Submit Workflow → Inject Result Container
    // ==========================================================
    submitBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        submitConfirmModal.hidden = false;
    });

    submitConfirmNo?.addEventListener('click', () => {
        submitConfirmModal.hidden = true;
    });

    submitConfirmModal?.addEventListener('click', (e) => {
        if (e.target === submitConfirmModal) submitConfirmModal.hidden = true;
    });

    submitConfirmYes?.addEventListener('click', async () => {
        submitConfirmModal.hidden = true;
        if (!formEl) return;
        const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
        if (!hasFile) { showToast('Please attach a file to submit'); return; }
        const fd = new FormData(formEl);
        const token = getAntiForgeryToken();
        try {
            const res = await fetch('/StudentTask/SubmitTask', {
                method: 'POST',
                body: fd,
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token }
            });
            const data = await res.json();
            if (data && data.success) {
                showToast('Submitted');
                setTimeout(() => location.reload(), 800);
            } else {
                showToast((data && data.message) || 'Submission failed');
            }
        } catch (err) {
            showToast('Network error');
        }
    });


    function wireResultEvents() {
        const btnList = document.getElementById('resultAttachmentListBtn');
        const unsubmitBtn = document.getElementById('unsubmitBtn');
        const backBtn = document.getElementById('resultBackButton');

        btnList?.addEventListener('click', () => {
            const ok = downloadAttachments();
            if (!ok) {
                showToast('there is no file');
                return;
            }
        });

        unsubmitBtn?.addEventListener('click', () => {
            unsubmitConfirmModal.hidden = false;
        });

        unsubmitYes?.addEventListener('click', () => {
            if (originalSubmitHTML) {
                submitContainer.innerHTML = originalSubmitHTML;
                location.reload(); // easiest way to rebind
            } else {
                showToast("Cannot unsubmit (client error).");
                unsubmitConfirmModal.hidden = true;
            }
        });

        unsubmitNo?.addEventListener('click', () => {
            unsubmitConfirmModal.hidden = true;
        });

        backBtn?.addEventListener('click', () => {
            showToast('Returning to class...');
            setTimeout(() => (window.location.href = '/StudentClass'), 800);
        });
    }

    // ==========================================================
    // Escape HTML
    // ==========================================================
    function escapeHtml(s) {
        return String(s || '').replace(/[&<>"']/g, m => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[m]));
    }

});
document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('taskForm');
  const submissionForm = document.getElementById('submissionForm');
  const resubmissionSection = document.getElementById('resubmissionSection');
  const resubmitBtn = document.getElementById('resubmitBtn');
  const submitBtn = document.getElementById('submitBtn');
  const markDoneBtn = document.getElementById('markDoneBtn');
  const confirmModal = document.getElementById('confirmModal');
  const confirmYes = document.getElementById('confirmYes');
  const confirmNo = document.getElementById('confirmNo');
  const fileInput = document.getElementById('fileInput');
  const toast = document.getElementById('toast');
  const publicCommentText = document.getElementById('publicCommentText');
  const postPublicCommentBtn = document.getElementById('postPublicCommentBtn');
  const publicCommentList = document.getElementById('publicCommentList');
  const taskIdInput = document.querySelector('#taskForm input[name="taskId"]');
  const classCodeInput = document.querySelector('#taskForm input[name="classCode"]');
  const serverAttachments = document.getElementById('serverAttachments');
  const attachmentArea = document.querySelector('.attachment-area');
  const selectedFileList = document.getElementById('selectedFileList');
  let selectedFiles = [];

  let toastTimeout2 = null;
  function showToast2(message, duration = 1500) {
    if (!toast) return;
    toast.textContent = message;
    toast.classList.add('show');
    clearTimeout(toastTimeout2);
    toastTimeout2 = setTimeout(() => toast.classList.remove('show'), duration);
  }

  function getAntiForgeryToken() {
    const el = form ? form.querySelector('input[name="__RequestVerificationToken"]') : null;
    return el ? el.value : '';
  }

  function formatSize(bytes) {
    if (!bytes && bytes !== 0) return '';
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    return `${(kb / 1024).toFixed(2)} MB`;
  }

  function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, m => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[m]));
  }

  if (confirmModal) confirmModal.style.display = 'none';
  function fileKey(f) {
    return `${f.name}__${f.size}__${f.lastModified}`;
  }

  function syncInputWithSelectedFiles() {
    if (!fileInput) return;
    const dt = new DataTransfer();
    selectedFiles.forEach(f => dt.items.add(f));
    fileInput.files = dt.files;
  }

  function renderSelectedFiles() {
    if (!selectedFileList) return;
    if (!selectedFiles.length) {
      selectedFileList.innerHTML = '';
      selectedFileList.style.display = 'none';
      return;
    }
    selectedFileList.style.display = 'flex';
    selectedFileList.innerHTML = selectedFiles.map((f, idx) => `
      <div class="st-file-row" data-index="${idx}" data-file-label="${escapeHtml(f.name)}">
        <i class="fa-solid fa-file"></i>
        <span class="st-file-name" title="${escapeHtml(f.name)}">${escapeHtml(f.name)}</span>
        <span class="st-file-size">${formatSize(f.size)}</span>
        <button type="button" class="st-file-remove" aria-label="Remove ${escapeHtml(f.name)}" title="Remove file">
          <i class="fa-solid fa-xmark"></i>
        </button>
      </div>
    `).join('');
  }

  if (submitBtn) {
    const newSubmit = submitBtn.cloneNode(true);
    submitBtn.parentNode.replaceChild(newSubmit, submitBtn);
    newSubmit.addEventListener('click', async (e) => {
      e.preventDefault();
      if (!form) return;
      const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
      if (!hasFile) { showToast2('Please attach a file to submit'); return; }
      const fd = new FormData(form);
      const token = getAntiForgeryToken();
      try {
        const res = await fetch('/StudentTask/SubmitTask', {
          method: 'POST',
          body: fd,
          credentials: 'same-origin',
          headers: { 'RequestVerificationToken': token }
        });
        const data = await res.json();
        if (data && data.success) {
          showToast2('Submitted');
          setTimeout(() => location.reload(), 800);
        } else {
          showToast2((data && data.message) || 'Submission failed');
        }
      } catch (err) {
        showToast2('Network error');
      }
    });
  }

  if (markDoneBtn) {
    const newMark = markDoneBtn.cloneNode(true);
    markDoneBtn.parentNode.replaceChild(newMark, markDoneBtn);
    newMark.addEventListener('click', (e) => {
      e.preventDefault();
      const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
      if (!hasFile) {
        if (confirmModal) {
          confirmModal.removeAttribute('hidden');
          confirmModal.setAttribute('aria-hidden', 'false');
        }
      } else {
        postMarkAsDone2();
      }
    });
  }

  confirmYes?.addEventListener('click', () => {
    if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
    postMarkAsDone2();
  });

  confirmNo?.addEventListener('click', () => {
    if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
  });

  async function postMarkAsDone2() {
    if (!form) return;
    const fd = new FormData(form);
    const token = getAntiForgeryToken();
    try {
      const res = await fetch('/StudentTask/MarkAsDone', {
        method: 'POST',
        body: fd,
        credentials: 'same-origin',
        headers: { 'RequestVerificationToken': token }
      });
      const data = await res.json();
      if (data && data.success) {
        showToast2('Marked as done');
        setTimeout(() => location.reload(), 800);
      } else {
        showToast2((data && data.message) || 'Action failed');
      }
    } catch (err) {
      showToast2('Network error');
    }
  }

  fileInput?.addEventListener('change', () => {
    const incoming = Array.from(fileInput.files || []);
    if (!incoming.length) return;
    const existing = new Set(selectedFiles.map(fileKey));
    incoming.forEach(f => {
      const key = fileKey(f);
      if (!existing.has(key)) {
        selectedFiles.push(f);
        existing.add(key);
      }
    });
    syncInputWithSelectedFiles();
    renderSelectedFiles();
  });

  selectedFileList?.addEventListener('click', (e) => {
    const removeBtn = e.target instanceof HTMLElement ? e.target.closest('.st-file-remove') : null;
    if (!removeBtn) return;
    const row = removeBtn.closest('.st-file-row');
    if (!row) return;
    const idx = Number(row.getAttribute('data-index'));
    if (Number.isNaN(idx) || idx < 0 || idx >= selectedFiles.length) return;
    selectedFiles.splice(idx, 1);
    syncInputWithSelectedFiles();
    renderSelectedFiles();
  });

  // Make the whole attachment box clickable, not only the label text.
  attachmentArea?.addEventListener('click', (e) => {
    const target = e.target;
    if (target instanceof HTMLElement && (target.closest('label[for="fileInput"]') || target.id === 'fileInput')) return;
    fileInput?.click();
  });

  resubmitBtn?.addEventListener('click', () => {
    if (submissionForm) submissionForm.style.display = '';
    if (resubmissionSection) resubmissionSection.style.display = 'none';
    document.getElementById('submitContainer')?.classList.add('resubmitting');
  });

  async function loadComments() {
    const taskId = taskIdInput?.value || '';
    if (!taskId || !publicCommentList) return;
    const res = await fetch(`/StudentTask/GetComments?taskId=${encodeURIComponent(taskId)}`, { credentials: 'same-origin' });
    const data = await res.json();
    if (!data?.success) return;
    publicCommentList.innerHTML = '';
    data.comments.forEach(renderComment);
  }

  function renderComment(c) {
    const box = document.createElement('div');
    box.className = 'comment-box';
    const dt = new Date(c.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
    box.innerHTML = `
      <div class="student-name">${escapeHtml(c.authorName)}${c.role ? ' • ' + escapeHtml(c.role) : ''}</div>
      <div class="comment-text">${escapeHtml(c.text)}</div>
      <div class="comment-datetime">${dt}</div>
      <div class="reply-option" role="button"><i class="fa-solid fa-reply"></i> Reply</div>
      <div class="reply-box-area" hidden>
        <textarea class="reply-box" placeholder="Write a reply..."></textarea>
        <button class="reply-submit-btn">Post Reply</button>
      </div>
    `;
    if (Array.isArray(c.replies)) {
      c.replies.forEach(r => {
        const rdt = new Date(r.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
        const reply = document.createElement('div');
        reply.className = 'instructor-reply';
        reply.innerHTML = `
          <div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${escapeHtml(r.authorName)}${r.role ? ' • ' + escapeHtml(r.role) : ''}</span></div>
          <div class="reply-text">${escapeHtml(r.text)}</div>
          <div class="reply-datetime">${rdt}</div>
        `;
        const insertBefore = box.querySelector('.reply-option');
        insertBefore?.insertAdjacentElement('beforebegin', reply);
      });
    }
    publicCommentList.appendChild(box);
    const replyToggle = box.querySelector('.reply-option');
    const replyArea = box.querySelector('.reply-box-area');
    const replyBtn = box.querySelector('.reply-submit-btn');
    const replyBox = box.querySelector('.reply-box');
    replyToggle?.addEventListener('click', () => {
      if (!replyArea) return;
      const hidden = replyArea.getAttribute('hidden') !== null || replyArea.style.display === 'none' || replyArea.style.display === '';
      replyArea.style.display = hidden ? 'flex' : 'none';
      if (hidden) replyArea.removeAttribute('hidden');
    });
    replyBtn?.addEventListener('click', async () => {
      const val = (replyBox?.value || '').trim();
      if (!val) return;
      const token = getAntiForgeryToken();
      const fd = new FormData(form);
      fd.append('commentId', c.id);
      fd.append('text', val);
      const res = await fetch('/StudentTask/PostReply', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' });
      const data = await res.json();
      if (data?.success && data.reply) {
        const rdt = new Date(data.reply.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
        const reply = document.createElement('div');
        reply.className = 'instructor-reply';
        reply.innerHTML = `
          <div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${escapeHtml(data.reply.authorName)}${data.reply.role ? ' • ' + escapeHtml(data.reply.role) : ''}</span></div>
          <div class="reply-text">${escapeHtml(data.reply.text)}</div>
          <div class="reply-datetime">${rdt}</div>
        `;
        const insertBefore = box.querySelector('.reply-option');
        insertBefore?.insertAdjacentElement('beforebegin', reply);
        if (replyBox) replyBox.value = '';
        if (replyArea) replyArea.style.display = 'none';
      }
    });
  }

  postPublicCommentBtn?.addEventListener('click', () => {
    const text = (publicCommentText?.value || '').trim();
    const taskId = taskIdInput?.value || '';
    const classCode = classCodeInput?.value || '';
    if (!text || !taskId || !classCode || !publicCommentList) return;
    const token = getAntiForgeryToken();
    const fd = new FormData(form);
    fd.append('text', text);
    try {
      fetch('/StudentTask/PostComment', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' })
        .then(r => r.json())
        .then(d => {
          if (d?.success && d.comment) {
            renderComment(d.comment);
            publicCommentText.value = '';
          }
        });
    } catch {}
  });

  loadComments();

  // ====================== WORK REFERENCES ======================
  const referencesContainer = document.getElementById('referencesContainer');
  if (referencesContainer) {
    const referenceList = document.getElementById('referenceList');
    const addReferenceBtn = document.getElementById('addReferenceBtn');
    const referenceTitle = document.getElementById('referenceTitle');
    const taskIdInput = document.querySelector('#taskForm input[name="taskId"]');
    const classCodeInput = document.querySelector('#taskForm input[name="classCode"]');

    const refsKey = (() => {
      const tid = taskIdInput?.value || 'task';
      const cls = classCodeInput?.value || 'class';
      return `refs:${cls}:${tid}`;
    })();

    function getRefs() {
      try {
        const raw = localStorage.getItem(refsKey);
        const arr = raw ? JSON.parse(raw) : [];
        return Array.isArray(arr) ? arr : [];
      } catch { return []; }
    }

    function setRefs(list) {
      try { localStorage.setItem(refsKey, JSON.stringify(list || [])); } catch {}
    }

    function renderRefs() {
      if (!referenceList) return;
      const refs = getRefs();
      referenceList.innerHTML = '';
      if (!refs.length) {
        const empty = document.createElement('div');
        empty.style.color = '#64748b';
        empty.textContent = 'No references yet';
        referenceList.appendChild(empty);
        return;
      }
      refs.forEach((r, idx) => {
        const row = document.createElement('div');
        row.className = 'reference-item';
        const title = document.createElement('span');
        title.textContent = r.title || 'Reference';
        row.appendChild(title);
        if (r.url) {
          const link = document.createElement('a');
          link.href = r.url;
          link.target = '_blank';
          link.rel = 'noopener noreferrer';
          link.textContent = 'Open';
          row.appendChild(link);
        }
        const rm = document.createElement('button');
        rm.className = 'reference-remove';
        rm.innerHTML = '<i class="fa-solid fa-trash"></i>';
        rm.addEventListener('click', () => {
          const list = getRefs();
          list.splice(idx, 1);
          setRefs(list);
          renderRefs();
        });
        row.appendChild(rm);
        referenceList.appendChild(row);
      });
    }

    addReferenceBtn?.addEventListener('click', () => {
      const t = (referenceTitle?.value || '').trim();
      if (!t) return;
      const list = getRefs();
      list.push({ title: t });
      setRefs(list);
      referenceTitle && (referenceTitle.value = '');
      renderRefs();
    });

    renderRefs();
  }
});
