// ==========================================================
// studentassessment.js — Handles StudentAssessment/Index.cshtml (MVC)
// ==========================================================

// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
let menuOpen = false;
const actions = radialActions?.querySelectorAll('.action') || [];
const backButton = document.querySelector('.back-button');
const toast = document.getElementById('toast');
const markDoneBtn = document.getElementById('markDoneBtn');
const confirmModal = document.getElementById('confirmModal');
const confirmYes = document.getElementById('confirmYes');
const confirmNo = document.getElementById('confirmNo');

// NEW: Open quiz element that should redirect to the answering page
const openQuiz = document.getElementById('openQuiz');

// --- PRIVATE COMMENT ELEMENTS (added) ---
const privateComment = document.getElementById('privateComment');
const commentPopup = document.getElementById('commentPopup');
const cancelComment = document.getElementById('cancelComment');
const addComment = document.getElementById('addComment');
const commentInput = document.getElementById('commentInput');
const commentPreview = document.getElementById('commentPreview');

let privateCommentData = "";

// submitted param toast
try {
    const params = new URLSearchParams(window.location.search || "");
    if (params.get('submitted') === '1') {
        if (toast) {
            toast.textContent = 'You already answered this assessment.';
            toast.className = 'toast show';
            setTimeout(() => toast.classList.remove('show'), 2500);
        }
    }
} catch (_) {}


// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'subjects';

function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        if (page === currentPage) {
            showToast("📝 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        // --- LOCKED BUTTON BEHAVIOR ---
        if (action.classList.contains('locked')) {
            showToast("Coming soon");
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
            case 'todo':
                url = '/StudentDb/StudentTodo';
                showToast('Opening to-do list...');
                break;
        }

        radialActions.classList.remove('show');
        menuOpen = false;
        if (url) setTimeout(() => (window.location.href = url), 600);
    });
});

// --- OPEN QUIZ: redirect to StudentAnswerAssessment ---
openQuiz?.addEventListener('click', () => {
    const blocked = openQuiz?.dataset.blocked === 'true';
    if (blocked) {
        showToast('Assessment locked by admin');
        return;
    }
    const presetUrl = openQuiz?.dataset.url || '';
    if (presetUrl) {
        showToast('Opening assessment...');
        setTimeout(() => { window.location.href = presetUrl; }, 600);
        return;
    }

    let classCode = openQuiz?.dataset.classCode || '';
    let contentId = openQuiz?.dataset.contentId || '';

    if (!classCode || !contentId) {
        // Fallback: parse current path /StudentAssessment/{classCode}/{contentId}
        const parts = (window.location.pathname || '').split('/').filter(Boolean);
        const idx = parts.findIndex(p => p.toLowerCase() === 'studentassessment');
        if (idx >= 0 && parts.length >= idx + 3) {
            classCode = parts[idx + 1];
            contentId = parts[idx + 2];
        }
    }

    if (!classCode || !contentId) {
        showToast('Cannot open quiz: missing identifiers.');
        return;
    }

    showToast('Opening assessment...');
    setTimeout(() => { window.location.href = `/StudentAnswerAssessment/${classCode}/${contentId}`; }, 600);
});

// Flag-based lock from URL param
try {
    const params = new URLSearchParams(window.location.search || "");
    if (params.get('flag') === 'void') {
        const el = document.getElementById('openQuiz');
        if (el) {
            el.classList.add('locked');
            el.setAttribute('data-blocked', 'true');
            const label = el.querySelector('span');
            if (label) label.textContent = 'Assessment Locked';
        }
        showToast('Assessment locked by admin');
    }
} catch (_) {}

// ==========================================================
// PRIVATE COMMENT POPUP (added)
// ==========================================================
privateComment?.addEventListener('click', () => {
    // load existing data, open popup and focus input
    if (commentInput) commentInput.value = privateCommentData;
    commentPopup?.classList.add('show');
    commentInput?.focus();
});

// close popup when clicking the overlay backdrop
commentPopup?.addEventListener('click', (e) => {
    if (e.target === commentPopup) commentPopup.classList.remove('show');
});

// cancel button
cancelComment?.addEventListener('click', () => {
    commentPopup?.classList.remove('show');
});

// save comment (Add)
addComment?.addEventListener('click', () => {
    const text = commentInput?.value?.trim() || "";
    if (text) {
        privateCommentData = text;
        if (commentPreview) commentPreview.textContent = text;
        privateComment?.classList.add('saved');
    } else {
        privateCommentData = "";
        if (commentPreview) commentPreview.textContent = "Add Private Comment";
        privateComment?.classList.remove('saved');
    }
    commentPopup?.classList.remove('show');
});

// ==========================================================
// BACK BUTTON
// ==========================================================
backButton?.addEventListener('click', () => {
    const el = document.getElementById('openQuiz');
    const codeAttr = el && el.getAttribute('data-class-code');
    const parts = (window.location.pathname || '').split('/').filter(Boolean);
    const i = parts.findIndex(p => p.toLowerCase() === 'studentassessment');
    const codeFromPath = i >= 0 && parts.length >= i + 2 ? parts[i + 1] : '';
    const code = codeAttr || codeFromPath || '';
    const dash = typeof window.resolveStudentDashboardUrl === 'function' ? window.resolveStudentDashboardUrl() : '/StudentDb/StudentDb';
    showToast('Returning to class...');
    setTimeout(() => (window.location.href = code ? `/StudentClass/${encodeURIComponent(code)}` : dash), 800);
});

// --- TOAST FUNCTION ---
function showToast(message) {
    if (!toast) return;
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

// --- MARK AS DONE (modal) ---
markDoneBtn?.addEventListener('click', () => {
    confirmModal?.classList.add('show');
});

confirmYes?.addEventListener('click', () => {
    confirmModal?.classList.remove('show');
    // submit the surrounding form (the form in the view uses asp-action="MarkAsDone")
    const form = document.querySelector('form');
    if (form) form.submit();
});

confirmNo?.addEventListener('click', () => confirmModal?.classList.remove('show'));
confirmModal?.addEventListener('click', e => {
    if (e.target === confirmModal) confirmModal.classList.remove('show');
});

// --- ESCAPE KEY: close popups & menus ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        commentPopup?.classList.remove('show');
        confirmModal?.classList.remove('show');
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
        menuOpen = false;
    }
});

// Public Comments
const publicCommentText = document.getElementById('publicCommentText');
const postPublicCommentBtn = document.getElementById('postPublicCommentBtn');
const publicCommentList = document.getElementById('publicCommentList');
const assessmentForm = document.getElementById('assessmentForm');

function getAntiForgeryToken() {
    const el = assessmentForm ? assessmentForm.querySelector('input[name="__RequestVerificationToken"]') : null;
    return el ? el.value : '';
}

function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, function (m) { return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m]; });
}

async function loadComments() {
    const idInput = assessmentForm ? assessmentForm.querySelector('input[name="contentId"]') : null;
    const contentId = idInput ? idInput.value : '';
    if (!contentId || !publicCommentList) return;
    const res = await fetch('/StudentAssessment/GetComments?contentId=' + encodeURIComponent(contentId), { credentials: 'same-origin' });
    const data = await res.json();
    if (!data || !data.success) return;
    publicCommentList.innerHTML = '';
    data.comments.forEach(renderComment);
}

function renderComment(c) {
    if (!publicCommentList) return;
    const box = document.createElement('div');
    box.className = 'comment-box';
    const dt = new Date(c.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
    box.innerHTML =
        '<div class="student-name">' + escapeHtml(c.authorName) + (c.role ? ' • ' + escapeHtml(c.role) : '') + '</div>' +
        '<div class="comment-text">' + escapeHtml(c.text) + '</div>' +
        '<div class="comment-datetime">' + dt + '</div>' +
        '<div class="reply-option" role="button"><i class="fa-solid fa-reply"></i> Reply</div>' +
        '<div class="reply-box-area" hidden>' +
        '<textarea class="reply-box" placeholder="Write a reply..."></textarea>' +
        '<button class="reply-submit-btn">Post Reply</button>' +
        '</div>';

    if (Array.isArray(c.replies)) {
        c.replies.forEach(function (r) {
            const rdt = new Date(r.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            const reply = document.createElement('div');
            reply.className = 'instructor-reply';
            reply.innerHTML =
                '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(r.authorName) + (r.role ? ' • ' + escapeHtml(r.role) : '') + '</span></div>' +
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

    replyToggle && replyToggle.addEventListener('click', function () {
        if (!replyArea) return;
        const hidden = replyArea.getAttribute('hidden') !== null || replyArea.style.display === 'none' || replyArea.style.display === '';
        replyArea.style.display = hidden ? 'flex' : 'none';
        if (hidden) replyArea.removeAttribute('hidden');
    });

    replyBtn && replyBtn.addEventListener('click', async function () {
        const val = (replyBox && replyBox.value || '').trim();
        if (!val) return;
        const token = getAntiForgeryToken();
        const fd = new FormData(assessmentForm || undefined);
        fd.append('commentId', c.id);
        fd.append('text', val);
        const res = await fetch('/StudentAssessment/PostReply', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' });
        const data = await res.json();
        if (data && data.success && data.reply) {
            const rdt = new Date(data.reply.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            const reply = document.createElement('div');
            reply.className = 'instructor-reply';
            reply.innerHTML =
                '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(data.reply.authorName) + (data.reply.role ? ' • ' + escapeHtml(data.reply.role) : '') + '</span></div>' +
                '<div class="reply-text">' + escapeHtml(data.reply.text) + '</div>' +
                '<div class="reply-datetime">' + rdt + '</div>';
            const insertBefore = box.querySelector('.reply-option');
            if (insertBefore) insertBefore.insertAdjacentElement('beforebegin', reply);
            if (replyBox) replyBox.value = '';
            if (replyArea) replyArea.style.display = 'none';
        }
    });
}

postPublicCommentBtn && postPublicCommentBtn.addEventListener('click', function () {
    const text = (publicCommentText && publicCommentText.value || '').trim();
    const idInput = assessmentForm ? assessmentForm.querySelector('input[name="contentId"]') : null;
    const classInput = assessmentForm ? assessmentForm.querySelector('input[name="classCode"]') : null;
    const contentId = idInput ? idInput.value : '';
    const classCode = classInput ? classInput.value : '';
    if (!text || !contentId || !classCode || !publicCommentList) return;
    const token = getAntiForgeryToken();
    const fd = new FormData(assessmentForm || undefined);
    fd.append('text', text);
    fetch('/StudentAssessment/PostComment', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d && d.success && d.comment) {
                renderComment(d.comment);
                if (publicCommentText) publicCommentText.value = '';
            }
        })
        .catch(function () { });
});

loadComments();

