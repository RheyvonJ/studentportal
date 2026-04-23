(function () {
// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
const createBtn = document.getElementById('createBtn');
const modalBackdrop = document.getElementById('modalBackdrop');
const subjectNameInput = document.getElementById('subjectName');
const subjectCodeInput = document.getElementById('subjectCode');
const createForm = document.getElementById('createForm');
const cancelCreate = document.getElementById('cancelCreate');
const sectionInput = document.getElementById('section');
const createModal = document.getElementById('createModal');
const schoolYearInput = document.getElementById('schoolYear');
const sectionTextInput = document.getElementById('sectionText');
const subjectNameTextInput = document.getElementById('subjectNameText');
const subjectCodeTextInput = document.getElementById('subjectCodeText');
const schoolYearTextInput = document.getElementById('schoolYearText');
const profEmailInput = document.getElementById('profEmail');
const profSubjectSelect = document.getElementById('profSubjectSelect');
const profSubjectValueInput = document.getElementById('profSubjectValue');
const professorIdInput = document.getElementById('professorId');
const deleteBackdrop = document.getElementById('deleteClassConfirmBackdrop');
const deleteModal = document.getElementById('deleteClassConfirmModal');
const deleteText = document.getElementById('deleteClassText');
const createClassConfirmBackdrop = document.getElementById('createClassConfirmBackdrop');
const createClassConfirmModal = document.getElementById('createClassConfirmModal');
const createClassConfirmTitle = document.getElementById('createClassConfirmTitle');
const createClassConfirmDesc = document.getElementById('createClassConfirmDesc');
const createClassConfirmOk = document.getElementById('createClassConfirmOk');
const createClassConfirmCancel = document.getElementById('createClassConfirmCancel');
const allowDuplicateInput = document.getElementById('allowDuplicate');
const confirmDeleteClass = document.getElementById('confirmDeleteClass');
const cancelDeleteClass = document.getElementById('cancelDeleteClass');
let deleteTarget = null;

// --- USER PROFILE DROPDOWN ---
userProfile?.addEventListener('click', () => {
    userPopup?.classList.toggle('show');
});
document.addEventListener('click', (e) => {
    if (!userProfile?.contains(e.target)) {
        userPopup?.classList.remove('show');
    }
});

// --- RADIAL MENU TOGGLE ---
let menuOpen = false;
menuCircle?.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions?.classList.toggle('show', menuOpen);
});

// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'home';

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
            (icon.classList.contains('fa-clipboard-question') && currentPage === 'assessment')
        ) {
            showToast("🏠 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        // --- Otherwise navigate normally ---
        setActivePage(page);

        if (icon.classList.contains('fa-house')) {
            navigateWithAnimation('/AdminDb', 'Going to dashboard...');
        }
        else if (icon.classList.contains('fa-book')) {
            navigateWithAnimation('/AdminSubject', 'Opening subjects...');
        }
        else if (icon.classList.contains('fa-clipboard-question')) {
            navigateWithAnimation('/AdminAssessmentList', 'Opening assessments...');
        }
    });
});

// --- NAVIGATION FUNCTION ---
function navigateWithAnimation(url, message) {
    showToast(message);
    radialActions?.classList.remove('show');
    menuOpen = false;
    setTimeout(() => {
        window.location.href = url;
    }, 600);
}

// --- HIDE RADIAL WHEN CLICKING OUTSIDE ---
document.addEventListener('click', (e) => {
    if (!menuCircle?.contains(e.target) && !radialActions?.contains(e.target)) {
        menuOpen = false;
        radialActions?.classList.remove('show');
    }
});

// --- Clickable class cards (data-href) ---
document.querySelectorAll('.class-left[data-href]').forEach(el => {
    el.style.cursor = 'pointer';
    el.addEventListener('click', () => {
        const href = el.getAttribute('data-href');
        if (href) {
            showToast('Opening class...');
            setTimeout(() => window.location.href = href, 350);
        }
    });
});

// --- CREATE CLASS MODAL ---
function openCreateModal(event) {
    event?.preventDefault();
    event?.stopPropagation();
    if (!createBtn || !modalBackdrop) {
        return;
    }
    createBtn.classList.add('selected');
    modalBackdrop?.classList.add('show');
    modalBackdrop?.setAttribute('aria-hidden', 'false');
}
window.openCreateModal = openCreateModal;

// Ensure the button opens the modal even without inline onclick
createBtn?.addEventListener('click', openCreateModal);

modalBackdrop?.addEventListener('click', (e) => {
    if (e.target === modalBackdrop) {
        modalBackdrop.classList.remove('show');
        createBtn.classList.remove('selected');
        modalBackdrop?.setAttribute('aria-hidden', 'true');
    }
});

createModal?.addEventListener('click', (event) => {
    event.stopPropagation();
});

cancelCreate?.addEventListener('click', () => {
    modalBackdrop.classList.remove('show');
    createBtn.classList.remove('selected');
    modalBackdrop?.setAttribute('aria-hidden', 'true');
});

let skipCreateClassConfirm = false;
let pendingAllowDuplicate = false;

function normalizeClassFormField(val) {
    const t = (val ?? '').toString().trim();
    return t.length ? t : 'N/A';
}

/** Which dashboard posted the create form (AdminDb.js is shared). */
function detectCreateFormKind() {
    const fa = (createForm?.action || '').toLowerCase();
    if (fa.includes('/admindb/')) return 'admin';
    if (fa.includes('/professordb/')) return 'professor';
    return 'other';
}

// client-side validation, then check duplicate, then confirm, then submit form to server
createForm?.addEventListener('submit', async function (e) {
    const name = subjectNameInput?.value?.trim();
    const code = subjectCodeInput?.value?.trim();
    let section = sectionInput?.value?.trim();

    if (sectionInput) {
        sectionInput.value = section ?? '';
    }

    if (!section) {
        e.preventDefault();
        showToast('⚠️ Please select an assigned class.', 'warning');
        return;
    }

    if (!name || !code) {
        e.preventDefault();
        showToast('⚠️ Please fill in both fields.', 'warning');
        return;
    }

    if (skipCreateClassConfirm) {
        skipCreateClassConfirm = false;
        if (allowDuplicateInput) allowDuplicateInput.value = pendingAllowDuplicate ? 'true' : 'false';
        showToast(`Creating "${name}"...`);
        return;
    }

    e.preventDefault();
    if (allowDuplicateInput) allowDuplicateInput.value = 'false';
    pendingAllowDuplicate = false;

    const formKind = detectCreateFormKind();
    let exists = false;
    /** @type {{ subjectName?: string, section?: string } | null} */
    let professorDuplicateInfo = null;

    try {
        if (formKind === 'admin') {
            const year = normalizeClassFormField(document.getElementById('yearValue')?.value);
            const course = normalizeClassFormField(document.getElementById('courseValue')?.value);
            const semester = normalizeClassFormField(document.getElementById('semesterValue')?.value);
            const q = new URLSearchParams({
                subjectName: name,
                section,
                year,
                course,
                semester
            });
            const res = await fetch(`/admindb/AdminDb/CheckClassExists?${q.toString()}`, {
                headers: { Accept: 'application/json' }
            });
            const data = await res.json();
            if (!data || data.success !== true) {
                showToast('Could not verify whether this class already exists. Please try again.', 'warning');
                return;
            }
            exists = data.exists === true;
        } else if (formKind === 'professor') {
            const sid = (document.getElementById('scheduleId')?.value || '').trim();
            if (!sid) {
                exists = false;
            } else {
                const res = await fetch(
                    `/professordb/ProfessorDb/CheckScheduleClassDuplicate?scheduleId=${encodeURIComponent(sid)}`,
                    { headers: { Accept: 'application/json' } }
                );
                const data = await res.json();
                if (!data || data.success !== true) {
                    showToast(
                        'Could not verify whether you already have a class for this schedule. Please try again.',
                        'warning'
                    );
                    return;
                }
                exists = data.exists === true;
                if (exists) {
                    professorDuplicateInfo = {
                        subjectName: data.subjectName || name,
                        section: data.section || section
                    };
                }
            }
        } else {
            // Teacher dashboard and others: no server duplicate rule here
            exists = false;
        }
    } catch {
        showToast('Could not verify whether this class already exists. Please try again.', 'warning');
        return;
    }

    pendingAllowDuplicate = exists;

    if (!createClassConfirmModal || !createClassConfirmDesc) {
        skipCreateClassConfirm = true;
        if (allowDuplicateInput) allowDuplicateInput.value = exists ? 'true' : 'false';
        createForm.requestSubmit();
        return;
    }

    if (exists) {
        if (createClassConfirmTitle) createClassConfirmTitle.textContent = 'Class already exists';
        if (formKind === 'professor' && professorDuplicateInfo) {
            const subj = professorDuplicateInfo.subjectName || name;
            const sec = professorDuplicateInfo.section || section;
            createClassConfirmDesc.textContent =
                `You already have a class for this assignment: "${subj}" (Section: ${sec}). ` +
                'Do you still want to continue? A new class code will be generated.';
        } else if (formKind === 'professor') {
            createClassConfirmDesc.textContent =
                'You already have a class for this assignment. Do you still want to continue? A new class code will be generated.';
        } else {
            const year = normalizeClassFormField(document.getElementById('yearValue')?.value);
            const course = normalizeClassFormField(document.getElementById('courseValue')?.value);
            const semester = normalizeClassFormField(document.getElementById('semesterValue')?.value);
            createClassConfirmDesc.textContent =
                `A class for "${name}" is already on file for section ${section}, ${course}, year ${year}, ${semester} semester. ` +
                'A new class will get a new class code. Do you want to create it anyway?';
        }
        if (createClassConfirmOk) createClassConfirmOk.textContent = 'Continue anyway';
    } else {
        if (createClassConfirmTitle) createClassConfirmTitle.textContent = 'Create class';
        createClassConfirmDesc.textContent =
            `Continue creating "${name}" (${code}) for section ${section}?`;
        if (createClassConfirmOk) createClassConfirmOk.textContent = 'Continue';
    }

    openSlspConfirm(createClassConfirmBackdrop, createClassConfirmModal);
});

createClassConfirmOk?.addEventListener('click', () => {
    closeSlspConfirm(createClassConfirmBackdrop, createClassConfirmModal);
    if (allowDuplicateInput) allowDuplicateInput.value = pendingAllowDuplicate ? 'true' : 'false';
    skipCreateClassConfirm = true;
    createForm?.requestSubmit();
});

createClassConfirmCancel?.addEventListener('click', () => {
    closeSlspConfirm(createClassConfirmBackdrop, createClassConfirmModal);
});

// --- ESC KEY CLOSE ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        modalBackdrop?.classList.remove('show');
        createBtn?.classList.remove('selected');
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
        closeSlspConfirm(deleteBackdrop, deleteModal);
        closeSlspConfirm(createClassConfirmBackdrop, createClassConfirmModal);
        deleteTarget = null;
    }
});

// --- TOAST NOTIFICATION ---
function showToast(message, type = '') {
    const t = document.getElementById('toast');
    if (!t) return;
    t.textContent = message;
    t.className = `toast show ${type}`;
    setTimeout(() => {
        t.classList.remove('show');
    }, 2800);
}

// Auto-show toast if server provided TempData content
(() => {
    const t = document.getElementById('toast');
    if (t && (t.textContent || '').trim().length > 0) {
        t.classList.add('show');
        setTimeout(() => { t.classList.remove('show'); }, 3200);
    }
})();

function escapeHtml(str) {
    if (str == null || str === undefined) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function isTeacherDashboardRole() {
    const r = (document.body?.dataset?.role || '').toLowerCase();
    return r === 'professor' || r === 'teacher';
}

function openSlspConfirm(backdrop, modal) {
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

function closeSlspConfirm(backdrop, modal) {
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

function buildDeleteClassMessage(card, code) {
    const subject = (card?.dataset?.subjectName || card?.querySelector('.class-title, .class-card-title')?.textContent || '').trim();
    let section = (card?.dataset?.sectionLabel || '').trim();
    if (!section) {
        const rows = card?.querySelectorAll('.detail-row') || [];
        rows.forEach((r) => {
            const lab = (r.querySelector('.detail-label')?.textContent || '').trim().toLowerCase();
            if (lab.includes('section')) {
                section = (r.querySelector('.detail-value')?.textContent || '').trim();
            }
        });
    }
    if (isTeacherDashboardRole()) {
        return `You are about to archive or delete ${subject ? `"${subject}"` : 'this class'}${section ? ` — Section ${section}` : ''}. Class code: ${code}. This cannot be undone.`;
    }
    return `Delete ${subject ? `"${subject}"` : 'this class'} (${code})? This cannot be undone.`;
}

// Use capturing phase so we handle delete button before the card's onclick (which would open the class)
document.addEventListener('click', (e) => {
    const btn = e.target.closest && e.target.closest('.delete-class-btn');
    if (btn) {
        e.preventDefault();
        e.stopPropagation();
        deleteTarget = btn;
        const card = btn.closest('.class-card');
        const code = btn.dataset.classCode || '';
        if (deleteText) deleteText.textContent = buildDeleteClassMessage(card, code);
        openSlspConfirm(deleteBackdrop, deleteModal);
        return;
    }
    if (deleteBackdrop && e.target === deleteBackdrop) {
        closeSlspConfirm(deleteBackdrop, deleteModal);
        deleteTarget = null;
    }
    if (createClassConfirmBackdrop && e.target === createClassConfirmBackdrop) {
        closeSlspConfirm(createClassConfirmBackdrop, createClassConfirmModal);
    }
}, true);

confirmDeleteClass?.addEventListener('click', async () => {
    if (!deleteTarget) return;
    const id = deleteTarget.dataset.classId || '';
    const code = deleteTarget.dataset.classCode || '';
    const deleteUrl = isTeacherDashboardRole() ? '/professordb/ProfessorDb/DeleteClass' : '/admindb/AdminDb/DeleteClass';
    try {
        const res = await fetch(deleteUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ classId: id, classCode: code })
        });
        const data = await res.json();
        if (data && data.success) {
            showToast('Class deleted', 'success');
            const card = deleteTarget.closest('.class-card');
            if (card) card.remove();
        } else {
            showToast(data?.message || 'Delete failed', 'error');
        }
    } catch (_) {
        showToast('Delete error', 'error');
    } finally {
        closeSlspConfirm(deleteBackdrop, deleteModal);
        deleteTarget = null;
    }
});

cancelDeleteClass?.addEventListener('click', () => {
    closeSlspConfirm(deleteBackdrop, deleteModal);
    deleteTarget = null;
});

function closeOtherSelects(except) {
    const sectionSelectEl = document.getElementById('sectionSelect');
    if (profSubjectSelect && profSubjectSelect !== except) {
        profSubjectSelect.classList.remove('open');
    }
    if (sectionSelectEl && sectionSelectEl !== except) {
        sectionSelectEl.classList.remove('open');
    }
}

async function fetchProfessorSubjects(email) {
    try {
        const url = `/admindb/AdminDb/GetProfessorSubjects?email=${encodeURIComponent(email)}`;
        const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
        const data = await res.json();
        if (!data || !data.success) return [];
        return Array.isArray(data.subjects) ? data.subjects : [];
    } catch (_) {
        return [];
    }
}

async function fetchProfessorAssignedSubjects(professorId) {
    try {
        const url = `/admindb/AdminDb/GetProfessorAssignedSubjects?professorId=${encodeURIComponent(professorId)}`;
        const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
        const data = await res.json();
        if (!data || !data.success) return [];
        return Array.isArray(data.subjects) ? data.subjects : [];
    } catch (_) {
        return [];
    }
}

async function fetchMyAssignedSubjects() {
    try {
        const url = `/professordb/ProfessorDb/GetMyAssignedSubjects`;
        const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
        const data = await res.json();
        if (!data || !data.success) return [];
        return Array.isArray(data.subjects) ? data.subjects : [];
    } catch (_) {
        return [];
    }
}

async function fetchMyAssignedSections() {
    try {
        const url = `/professordb/ProfessorDb/GetMyAssignedSections`;
        const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
        const data = await res.json();
        if (!data || !data.success) return [];
        return Array.isArray(data.sections) ? data.sections : [];
    } catch (_) {
        return [];
    }
}

function setupSectionSelect() {
    const sectionSelectEl = document.getElementById('sectionSelect');
    const sectionInputHidden = document.getElementById('section');
    const sectionIdInput = document.getElementById('sectionId');
    if (!sectionSelectEl || !sectionInputHidden || !sectionIdInput) return;
    const selectedDisplay = sectionSelectEl.querySelector('.selected');
    const optionsContainer = sectionSelectEl.querySelector('.options');

    const setSelected = (sectionId, sectionName, label) => {
        sectionSelectEl.dataset.value = sectionId || '';
        sectionIdInput.value = sectionId || '';
        sectionInputHidden.value = sectionName || '';
        if (selectedDisplay) selectedDisplay.textContent = label || 'Select Section';
    };

    selectedDisplay?.addEventListener('click', (e) => {
        e.preventDefault();
        closeOtherSelects(sectionSelectEl);
        sectionSelectEl.classList.toggle('open');
    });

    optionsContainer?.addEventListener('click', (e) => {
        const opt = e.target.closest('div[data-section-id]');
        if (!opt) return;
        const id = opt.getAttribute('data-section-id') || '';
        const name = opt.getAttribute('data-section-name') || id;
        setSelected(id, name, name);
        sectionSelectEl.classList.remove('open');
    });

    async function populateOptionsFromAssignedSubjects(professorId) {
        if (!professorId || !optionsContainer) return;
        const subjects = await fetchProfessorAssignedSubjects(professorId);
        optionsContainer.innerHTML = '';
        if (!subjects || subjects.length === 0) {
            setSelected('', '', 'No sections found');
            return;
        }

        const seen = new Set();
        subjects.forEach(s => {
            const sectionName = (s.section || '').trim();
            if (!sectionName) return;
            const key = sectionName.toLowerCase();
            if (seen.has(key)) return;
            seen.add(key);

            const div = document.createElement('div');
            div.setAttribute('data-section-id', sectionName);
            div.setAttribute('data-section-name', sectionName);
            div.textContent = sectionName;
            optionsContainer.appendChild(div);
        });
        setSelected('', '', 'Select Section');
    }

    (async function load() {
        if (!isTeacherDashboardRole()) return;
        setSelected('', '', 'Loading...');
        if (optionsContainer) optionsContainer.innerHTML = '';
        const sections = await fetchMyAssignedSections();
        if (!sections || sections.length === 0) {
            setSelected('', '', 'No sections found');
            return;
        }
        sections.forEach(s => {
            const div = document.createElement('div');
            div.setAttribute('data-section-id', (s.sectionId || '').trim());
            div.setAttribute('data-section-name', (s.sectionName || s.sectionId || '').trim());
            div.textContent = (s.sectionName || s.sectionId || '').trim();
            optionsContainer.appendChild(div);
        });
        setSelected('', '', 'Select Section');
    })();

    const refreshSectionsFromProfessorId = async () => {
        if (isTeacherDashboardRole()) return;
        const pid = (document.getElementById('professorId')?.value || '').trim();
        if (!pid) {
            setSelected('', '', 'Select Section');
            if (optionsContainer) optionsContainer.innerHTML = '';
            return;
        }
        setSelected('', '', 'Loading...');
        await populateOptionsFromAssignedSubjects(pid);
    };

    const professorIdField = document.getElementById('professorId');
    const debounce = (fn, ms = 250) => {
        let t = null;
        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn(...args), ms);
        };
    };
    const debouncedRefresh = debounce(refreshSectionsFromProfessorId, 300);
    professorIdField?.addEventListener('change', refreshSectionsFromProfessorId);
    professorIdField?.addEventListener('input', debouncedRefresh);
}

function setupProfessorSubjectSelect() {
    if (!profSubjectSelect || !profSubjectValueInput) return;
    const selectedDisplay = profSubjectSelect.querySelector('.selected');
    const optionsContainer = profSubjectSelect.querySelector('.options');

    const setSelected = (value, label) => {
        profSubjectSelect.dataset.value = value || '';
        profSubjectValueInput.value = value || '';
        if (selectedDisplay) selectedDisplay.textContent = label || 'Select Assigned Class';
    };

    selectedDisplay?.addEventListener('click', (event) => {
        event.preventDefault();
        const isOpen = profSubjectSelect.classList.contains('open');
        closeOtherSelects(profSubjectSelect);
        profSubjectSelect.classList.toggle('open', !isOpen);
    });

        optionsContainer?.addEventListener('click', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            const opt = target.closest('div[data-subject-code], div[data-value]');
            if (!opt) return;
            const code = opt.getAttribute('data-subject-code') || (opt.getAttribute('data-value') || '').split('|')[1] || '';
            const section = opt.getAttribute('data-section') || (opt.getAttribute('data-value') || '').split('|')[0] || '';
            const nameResolved = opt.getAttribute('data-subject-name') || '';
            const schedId = opt.getAttribute('data-schedule-id') || (opt.getAttribute('data-value') || '').split('|')[3] || '';
            const secId = opt.getAttribute('data-section-id') || '';
            const label = `${nameResolved || code}`;
            setSelected(code, label);
            profSubjectSelect.classList.remove('open');
            if (subjectNameInput) subjectNameInput.value = nameResolved || code;
            if (subjectCodeInput) subjectCodeInput.value = code;
            if (sectionInput) sectionInput.value = section;
            if (sectionTextInput) sectionTextInput.value = section || '';
            if (subjectNameTextInput) subjectNameTextInput.value = nameResolved || code || '';
            if (subjectCodeTextInput) subjectCodeTextInput.value = code || '';
            const scheduleIdInput = document.getElementById('scheduleId');
            if (scheduleIdInput) scheduleIdInput.value = schedId || '';
            const sectionIdInputEl = document.getElementById('sectionId');
            if (sectionIdInputEl) sectionIdInputEl.value = secId;
            const schoolYear = opt.getAttribute('data-school-year') || '';
            if (schoolYearInput) schoolYearInput.value = schoolYear || '';
            if (schoolYearTextInput) schoolYearTextInput.value = schoolYear || '';

            // No course/semester selection required anymore
        });

    async function populateFromEmail() {
        const email = profEmailInput?.value?.trim() || '';
        if (!email) return;
        setSelected('', 'Loading...');
        if (optionsContainer) optionsContainer.innerHTML = '';
        const subjects = await fetchProfessorSubjects(email);
        if (!subjects || subjects.length === 0) {
            setSelected('', 'No subjects found');
            return;
        }
        const fr = document.createDocumentFragment();
        subjects.forEach(s => {
            const vName = (s.subjectName || '').trim();
            const vCode = (s.subjectCode || '').trim();
            const label = vCode ? `${vName} (${vCode})` : vName;
            const div = document.createElement('div');
            div.setAttribute('data-value', `${vName}|${vCode}`);
            div.textContent = label;
            fr.appendChild(div);
        });
        optionsContainer?.appendChild(fr);
        setSelected('', 'Select Assigned Class');
    }

    async function populateFromProfessorId() {
        const pid = professorIdInput?.value?.trim() || '';
        if (!pid) return;
        setSelected('', 'Loading...');
        if (optionsContainer) optionsContainer.innerHTML = '';
        const subjects = await fetchProfessorAssignedSubjects(pid);
        if (!subjects || subjects.length === 0) {
            setSelected('', 'No assignments found');
            return;
        }
        const fr = document.createDocumentFragment();
        subjects.forEach(s => {
            const section = (s.section || '').trim();
            const code = (s.subjectCode || '').trim();
            const units = (s.units || '').trim();
            const scheduleId = (s.scheduleId || '').trim();
            const name = (s.subjectName || '').trim();
            const secIdAdmin = (s.sectionId || '').trim();
            const label = `${section} • ${name ? name + ' ' : ''}(${code}) • ${units} unit(s) • sched ${scheduleId}`;
            const div = document.createElement('div');
            div.setAttribute('data-value', `${section}|${code}|${units}|${scheduleId}|${name}`);
            div.setAttribute('data-section', section);
            div.setAttribute('data-subject-code', code);
            div.setAttribute('data-subject-name', name);
            if (scheduleId) div.setAttribute('data-schedule-id', scheduleId);
            if (secIdAdmin) div.setAttribute('data-section-id', secIdAdmin);
            div.textContent = label;
            fr.appendChild(div);
        });
        optionsContainer?.appendChild(fr);
        setSelected('', 'Select Assigned Class');
    }

    async function populateMyAssignedSubjects() {
        setSelected('', 'Loading...');
        if (optionsContainer) optionsContainer.innerHTML = '';
        const subjects = await fetchMyAssignedSubjects();
        if (!subjects || subjects.length === 0) {
            setSelected('', 'No assignments found');
            return;
        }

        const syInputEls = [
            document.getElementById('schoolYear'),
            document.getElementById('schoolYearText')
        ].filter(Boolean);

        const computeCurrentSchoolYearLabel = () => {
            const now = new Date();
            const y = now.getFullYear();
            return `${y}-${y + 1}`;
        };
        const allSchoolYears = Array.from(
            new Set(subjects.map(s => String(s.schoolYear || '').trim()).filter(Boolean))
        ).sort((a, b) => b.localeCompare(a, undefined, { sensitivity: 'base' }));
        const currentSY = computeCurrentSchoolYearLabel();
        let selectedSchoolYear = allSchoolYears.includes(currentSY) ? currentSY : (allSchoolYears[0] || currentSY);

        const schoolYearSelect = document.getElementById('schoolYear');
        if (schoolYearSelect instanceof HTMLSelectElement) {
            schoolYearSelect.innerHTML = '';
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = 'Select School Year';
            schoolYearSelect.appendChild(placeholder);

            const yearValues = allSchoolYears.length ? allSchoolYears : [selectedSchoolYear];
            yearValues.forEach((sy) => {
                const option = document.createElement('option');
                option.value = sy;
                option.textContent = sy;
                schoolYearSelect.appendChild(option);
            });
            schoolYearSelect.value = selectedSchoolYear || '';
        }

        syInputEls.forEach(el => { try { el.value = selectedSchoolYear || ''; } catch { } });

        const rebuildOptions = () => {
            const fr = document.createDocumentFragment();
            const seen = new Set();
            subjects.forEach(s => {
                const section = (s.section || '').trim();
                const code = (s.subjectCode || '').trim();
                const units = (s.units || '').trim();
                const scheduleId = (s.scheduleId || '').trim();
                const name = (s.subjectName || '').trim();
                const classCode = (s.classCode || '').trim();
                const schoolYear = ((s.schoolYear || '').trim() || selectedSchoolYear);
                const timeSlot = (s.timeSlotDisplay || '').trim();
                const room = (s.roomName || '').trim();

                if (!code) return;
                if (selectedSchoolYear && schoolYear && schoolYear !== selectedSchoolYear) return;

                // de-dupe per school year so "Test Subject 1" and "Test Subject 1.2" (different SY) can both exist
                const key = `${schoolYear}|${section}|${code}`;
                if (seen.has(key)) return;
                seen.add(key);

                const labelParts = [name || code];
                if (section) labelParts.push(section);
                if (schoolYear) labelParts.push(schoolYear);
                if (timeSlot) labelParts.push(timeSlot);
                if (room) labelParts.push(room);
                const label = labelParts.join(' • ');

                const div = document.createElement('div');
                div.setAttribute('data-section', section);
                div.setAttribute('data-subject-code', code);
                div.setAttribute('data-subject-name', name);
                if (scheduleId) div.setAttribute('data-schedule-id', scheduleId);
                const sid = (s.sectionId || '').trim();
                if (sid) div.setAttribute('data-section-id', sid);
                if (schoolYear) div.setAttribute('data-school-year', schoolYear);
                if (classCode) div.setAttribute('data-class-code', classCode);
                div.textContent = label;
                fr.appendChild(div);
            });

            if (optionsContainer) {
                optionsContainer.innerHTML = '';
                optionsContainer.appendChild(fr);
            }
            setSelected('', 'Select Assigned Class');
            syInputEls.forEach(el => { try { el.value = selectedSchoolYear || ''; } catch { } });
        };
        rebuildOptions();

        if (schoolYearSelect instanceof HTMLSelectElement) {
            schoolYearSelect.addEventListener('change', () => {
                selectedSchoolYear = (schoolYearSelect.value || '').trim();
                syInputEls.forEach(el => { try { el.value = selectedSchoolYear || ''; } catch { } });
                rebuildOptions();
            });
        }
    }

    // Trigger on both change and input for better UX
    const debounce = (fn, ms = 300) => {
        let t = null;
        return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
    };
    const debouncedEmail = debounce(populateFromEmail, 300);
    const debouncedProfessorId = debounce(populateFromProfessorId, 300);
    profEmailInput?.addEventListener('change', populateFromEmail);
    profEmailInput?.addEventListener('input', debouncedEmail);
    professorIdInput?.addEventListener('change', populateFromProfessorId);
    professorIdInput?.addEventListener('input', debouncedProfessorId);

    // Auto-populate for logged-in Professor or teacher pages without admin filters
    const hasAdminInputs = !!professorIdInput || !!profEmailInput;
    if (isTeacherDashboardRole() || (!hasAdminInputs && profSubjectSelect)) {
        populateMyAssignedSubjects();
    }
}

setupSectionSelect();
setupProfessorSubjectSelect();

/** Same UX as StudentDb class-card navigation: overlay + brief card motion, then navigate. */
function navigateToUrlWithClassCardTransition(url) {
    if (!url) return;
    if (typeof window.ensureToastInBodyForPortal === 'function') {
        window.ensureToastInBodyForPortal();
    }
    if (typeof window.showToast === 'function') {
        window.showToast('Opening Class');
    } else {
        const toastEl = document.getElementById('toast');
        if (toastEl) {
            toastEl.textContent = 'Opening Class';
            toastEl.classList.add('show');
            setTimeout(() => toastEl.classList.remove('show'), 2800);
        }
    }

    const shell = document.querySelector('.slsp-shell');
    const cards = document.querySelectorAll('.class-card');
    const reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!shell || reduced) {
        window.location.href = url;
        return;
    }

    shell.classList.add('is-loading');

    const opts = { duration: 280, easing: 'cubic-bezier(.2,.9,.3,1)', fill: 'forwards' };
    try {
        const cardAnim = Array.from(cards).map((card) =>
            card.animate(
                [
                    { transform: 'translateY(0)', opacity: 1 },
                    { transform: 'translateY(4px)', opacity: 0.85 }
                ],
                { ...opts, duration: 240 }
            ).finished
        );
        Promise.allSettled(cardAnim).finally(() => {
            setTimeout(() => { window.location.href = url; }, 50);
        });
    } catch (_) {
        setTimeout(() => { window.location.href = url; }, 220);
    }
}

const professorDashboard = document.querySelector('main#dashboard.professor-db-dashboard');
professorDashboard?.addEventListener('click', (e) => {
    const article = e.target.closest && e.target.closest('.class-card.active');
    if (!article || !professorDashboard.contains(article)) return;
    if (e.target.closest('.delete-class-btn') || e.target.closest('.prof-class-actions')) return;
    const href = article.getAttribute('data-href');
    if (!href) return;
    e.preventDefault();
    navigateToUrlWithClassCardTransition(href);
});

professorDashboard?.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter' && e.key !== ' ') return;
    const article = e.target.closest && e.target.closest('.class-card.active');
    if (!article || e.target !== article) return;
    if (e.target.closest('.delete-class-btn') || e.target.closest('.prof-class-actions')) return;
    const href = article.getAttribute('data-href');
    if (!href) return;
    e.preventDefault();
    navigateToUrlWithClassCardTransition(href);
});
})();
