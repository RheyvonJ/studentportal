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
const profEmailInput = document.getElementById('profEmail');
const profSubjectSelect = document.getElementById('profSubjectSelect');
const profSubjectValueInput = document.getElementById('profSubjectValue');
const professorIdInput = document.getElementById('professorId');
const deleteBackdrop = document.getElementById('deleteClassBackdrop');
const deleteModal = document.getElementById('deleteClassModal');
const deleteText = document.getElementById('deleteClassText');
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

// client-side validation, then submit form to server
createForm?.addEventListener('submit', function (e) {
    const name = subjectNameInput?.value?.trim();
    const code = subjectCodeInput?.value?.trim();
    let section = sectionInput?.value?.trim();

    // Require a selected section from dropdown for Professors (so students in that section receive the email)
    if (document.body?.dataset?.role === 'Professor') {
        const sectionIdInput = document.getElementById('sectionId');
        const sid = sectionIdInput?.value?.trim();
        if (!sid || !section) {
            e.preventDefault();
            showToast('⚠️ Please select a section from the dropdown.', 'warning');
            return;
        }
    }

    if (sectionInput) {
        sectionInput.value = section ?? '';
    }

    if (!section) {
        e.preventDefault();
        showToast('⚠️ Please select a section.', 'warning');
        return;
    }

    if (!name || !code) {
        e.preventDefault();
        showToast('⚠️ Please fill in both fields.', 'warning');
        return;
    }
    // optionally show immediate feedback before redirect
    showToast(`Creating "${name}"...`);
    // let the form submit naturally (server will redirect and set TempData)
});

// --- ESC KEY CLOSE ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        modalBackdrop?.classList.remove('show');
        createBtn?.classList.remove('selected');
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
        deleteBackdrop?.classList.remove('show');
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

// Use capturing phase so we handle delete button before the card's onclick (which would open the class)
document.addEventListener('click', (e) => {
    const btn = e.target.closest && e.target.closest('.delete-class-btn');
    if (btn) {
        e.preventDefault();
        e.stopPropagation();
        deleteTarget = btn;
        const card = btn.closest('.class-card');
        const name = card?.querySelector('.class-card-title')?.textContent || card?.querySelector('.class-name')?.textContent || card?.querySelector('.class-subcode')?.textContent || '';
        const code = btn.dataset.classCode || '';
        if (deleteText) deleteText.textContent = document.body?.dataset?.role === 'Professor' ? `Archive / delete "${name}" (${code})? This cannot be undone.` : `Delete "${name}" (${code})?`;
        deleteBackdrop?.classList.add('show');
        deleteBackdrop?.setAttribute('aria-hidden', 'false');
        return;
    }
    if (deleteBackdrop && e.target === deleteBackdrop) {
        deleteBackdrop.classList.remove('show');
        deleteBackdrop?.setAttribute('aria-hidden', 'true');
        deleteTarget = null;
    }
}, true);

confirmDeleteClass?.addEventListener('click', async () => {
    if (!deleteTarget) return;
    const id = deleteTarget.dataset.classId || '';
    const code = deleteTarget.dataset.classCode || '';
    const isProfessor = document.body?.dataset?.role === 'Professor';
    const deleteUrl = isProfessor ? '/professordb/professordb/DeleteClass' : '/admindb/admindb/DeleteClass';
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
        deleteBackdrop?.classList.remove('show');
        deleteBackdrop?.setAttribute('aria-hidden', 'true');
        deleteTarget = null;
    }
});

cancelDeleteClass?.addEventListener('click', () => {
    deleteBackdrop?.classList.remove('show');
    deleteBackdrop?.setAttribute('aria-hidden', 'true');
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
        const url = `/admindb/admindb/GetProfessorSubjects?email=${encodeURIComponent(email)}`;
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
        const url = `/admindb/admindb/GetProfessorAssignedSubjects?professorId=${encodeURIComponent(professorId)}`;
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
        const url = `/professordb/professordb/GetMyAssignedSubjects`;
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
        const url = `/professordb/professordb/GetMyAssignedSections`;
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
        if (document.body?.dataset?.role !== 'Professor') return;
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
        if (document.body?.dataset?.role === 'Professor') return;
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
        if (selectedDisplay) selectedDisplay.textContent = label || 'Select Professor Subject';
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
            const label = `${nameResolved || code}`;
            setSelected(code, label);
            profSubjectSelect.classList.remove('open');
            if (subjectNameInput) subjectNameInput.value = nameResolved || code;
            if (subjectCodeInput) subjectCodeInput.value = code;
            if (sectionInput) sectionInput.value = section;
            const sectionIdInput = document.getElementById('sectionId');
            if (sectionIdInput) sectionIdInput.value = section;
            const scheduleIdInput = document.getElementById('scheduleId');
            if (scheduleIdInput) scheduleIdInput.value = schedId || '';
            const schoolYear = opt.getAttribute('data-school-year') || '';
            if (schoolYearInput) schoolYearInput.value = schoolYear || '';

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
        setSelected('', 'Select Professor Subject');
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
            const label = `${section} • ${name ? name + ' ' : ''}(${code}) • ${units} unit(s) • sched ${scheduleId}`;
            const div = document.createElement('div');
            div.setAttribute('data-value', `${section}|${code}|${units}|${scheduleId}|${name}`);
            div.setAttribute('data-section', section);
            div.setAttribute('data-subject-code', code);
            div.setAttribute('data-subject-name', name);
            if (scheduleId) div.setAttribute('data-schedule-id', scheduleId);
            div.textContent = label;
            fr.appendChild(div);
        });
        optionsContainer?.appendChild(fr);
        setSelected('', 'Select Professor Subject');
    }

    async function populateMyAssignedSubjects() {
        setSelected('', 'Loading...');
        if (optionsContainer) optionsContainer.innerHTML = '';
        const subjects = await fetchMyAssignedSubjects();
        if (!subjects || subjects.length === 0) {
            setSelected('', 'No assignments found');
            return;
        }
        const fr = document.createDocumentFragment();
        const seen = new Set();
        subjects.forEach(s => {
            const section = (s.section || '').trim();
            const code = (s.subjectCode || '').trim();
            const units = (s.units || '').trim();
            const scheduleId = (s.scheduleId || '').trim();
            const name = (s.subjectName || '').trim();
            const classCode = (s.classCode || '').trim();
            const schoolYear = (s.schoolYear || '').trim();
            if (!code || seen.has(section + '|' + code)) return; seen.add(section + '|' + code);
            const timeSlot = (s.timeSlotDisplay || '').trim();
            const room = (s.roomName || '').trim();
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
            if (schoolYear) div.setAttribute('data-school-year', schoolYear);
            if (classCode) div.setAttribute('data-class-code', classCode);
            div.textContent = label;
            fr.appendChild(div);
        });
        optionsContainer?.appendChild(fr);
        setSelected('', 'Select Professor Subject');
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
    if (document.body?.dataset?.role === 'Professor' || (!hasAdminInputs && profSubjectSelect)) {
        populateMyAssignedSubjects();
    }
}

setupSectionSelect();
setupProfessorSubjectSelect();
})();
