/**
 * Admin Assessment — use with _StudentLayout + #adminAssessmentPage[data-class-code]
 */
function escapeHtmlForMeta(str) {
    if (str == null || str === '') return '';
    const d = document.createElement('div');
    d.textContent = String(str);
    return d.innerHTML;
}

function buildAssessmentMetaRowHtml(posted, edited, deadlineDisplay) {
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
}

function applyAssessmentMetaToDateInfo(el, posted, edited, deadlineDisplay, deadlineIso) {
    if (!el) return;
    el.dataset.posted = posted || '';
    el.dataset.edited = edited || '';
    el.dataset.deadlineDisplay = deadlineDisplay || '';
    el.dataset.deadlineIso = deadlineIso || '';
    el.innerHTML = buildAssessmentMetaRowHtml(posted, edited, deadlineDisplay);
}

class AdminAssessmentManager {
    constructor(deps) {
        this.showToast = deps.showToast;
        this.getClassCode = deps.getClassCode;
        this.currentAssessment = window.assessmentData || null;
        this.isEditing = false;
        this.originalDateHtml = '';
        this.adminMenuOpen = false;
        this.init();
    }

    init() {
        this.bindEvents();
        this.refreshLogCounters();
        try {
            setInterval(() => this.refreshLogCounters(), 10000);
        } catch { /* ignore */ }
    }

    bindEvents() {
        try {
            const makeChangesButton = document.getElementById('makeChangesButton');
            const adminActions = document.getElementById('adminActions');

            makeChangesButton?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.adminMenuOpen = !this.adminMenuOpen;
                adminActions?.classList.toggle('show', this.adminMenuOpen);
            });

            document.addEventListener('click', (e) => {
                if (!adminActions?.contains(e.target) && e.target !== makeChangesButton) {
                    adminActions?.classList.remove('show');
                    this.adminMenuOpen = false;
                }
            });

            adminActions?.querySelectorAll('.admin-action').forEach((action) => {
                action.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const actionType = action.dataset.action;
                    if (actionType === 'edit') {
                        this.startEditing();
                    } else if (actionType === 'delete') {
                        this.showDeleteModal();
                    }
                    adminActions.classList.remove('show');
                    this.adminMenuOpen = false;
                });
            });

            const saveEditBtn = document.getElementById('saveEditBtn');
            const cancelEditBtn = document.getElementById('cancelEditBtn');
            saveEditBtn?.addEventListener('click', () => this.openSaveConfirm());
            cancelEditBtn?.addEventListener('click', () => this.cancelEdit());

            const confirmDelete = document.getElementById('confirmDelete');
            const cancelDelete = document.getElementById('cancelDelete');
            const deleteBackdrop = document.getElementById('deleteAssessmentConfirmBackdrop');
            const deleteModal = document.getElementById('deleteAssessmentConfirmModal');
            confirmDelete?.addEventListener('click', () => this.deleteAssessment());
            cancelDelete?.addEventListener('click', () => this.hideDeleteModal());

            document.getElementById('saveAssessmentConfirmOk')?.addEventListener('click', () => {
                this.closeSaveConfirm();
                void this.saveAssessment();
            });
            document.getElementById('saveAssessmentConfirmCancel')?.addEventListener('click', () => this.closeSaveConfirm());
            document.getElementById('saveAssessmentConfirmBackdrop')?.addEventListener('click', (e) => {
                if (e.target === document.getElementById('saveAssessmentConfirmBackdrop')) this.closeSaveConfirm();
            });

            document.addEventListener('click', (e) => {
                if (deleteBackdrop && e.target === deleteBackdrop) {
                    this.hideDeleteModal();
                }
            });

            document.addEventListener('keydown', (e) => {
                if (e.key !== 'Escape') return;
                const delM = document.getElementById('deleteAssessmentConfirmModal');
                const savM = document.getElementById('saveAssessmentConfirmModal');
                if (delM && !delM.hasAttribute('hidden')) {
                    this.hideDeleteModal();
                    return;
                }
                if (savM && !savM.hasAttribute('hidden')) {
                    this.closeSaveConfirm();
                    return;
                }
                if (this.isEditing) {
                    this.cancelEdit();
                }
            });

            const search = document.getElementById('searchStudents');
            if (search) {
                search.addEventListener('input', (ev) => {
                    this.filterStudents(ev.target.value);
                });
            }

            document.querySelectorAll('.log-box').forEach((box) => {
                box.addEventListener('click', () => {
                    const type = box.dataset.logType;
                    if (type) this.openLogModal(type);
                });
            });

            document.querySelectorAll('.loglist-modal-close').forEach((btn) => {
                btn.addEventListener('click', (ev) => {
                    const t = ev.target.dataset.close;
                    if (t) this.closeLogModal(t);
                });
            });

            document.querySelectorAll('.loglist-modal').forEach((modal) => {
                modal.addEventListener('click', (ev) => {
                    if (ev.target.classList.contains('loglist-modal')) {
                        const id = modal.id.replace('modal-', '');
                        this.closeLogModal(id);
                    }
                });
            });
        } catch (err) {
            console.error('Error binding events:', err);
        }
    }

    placeCaretAtEnd(el) {
        try {
            const range = document.createRange();
            const sel = window.getSelection();
            range.selectNodeContents(el);
            range.collapse(false);
            sel.removeAllRanges();
            sel.addRange(range);
        } catch { /* ignore */ }
    }

    revealEditControls() {
        const editControls = document.getElementById('editControls');
        if (!editControls) return;
        editControls.hidden = false;
        editControls.style.display = 'flex';
    }

    hideEditControls() {
        const editControls = document.getElementById('editControls');
        if (!editControls) return;
        editControls.style.display = 'none';
        editControls.hidden = true;
    }

    startEditing() {
        if (this.isEditing) return;
        this.isEditing = true;

        const assessmentTitle = document.getElementById('assessmentTitle');
        const assessmentDescription = document.getElementById('assessmentDescription');
        const dateInfo = document.getElementById('dateInfo');

        this.showToast('Editing assessment...');

        if (assessmentTitle) {
            assessmentTitle.contentEditable = 'true';
            assessmentTitle.focus();
            this.placeCaretAtEnd(assessmentTitle);
        }
        if (assessmentDescription) {
            assessmentDescription.contentEditable = 'true';
        }

        if (dateInfo) {
            this.originalDateHtml = dateInfo.innerHTML;
            const posted = dateInfo.dataset.posted || '';
            const edited = dateInfo.dataset.edited || '';
            const iso = dateInfo.dataset.deadlineIso || '';
            const editedRow = edited.trim()
                ? `<div class="aae-meta-item"><span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-pen-to-square"></i></span><span class="aae-meta-label">Edited</span><span class="aae-meta-value">${escapeHtmlForMeta(edited)}</span></div>`
                : '';
            dateInfo.classList.add('aae-meta-edit-mode');
            dateInfo.innerHTML = `
                <div class="aae-meta-item">
                    <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-calendar"></i></span>
                    <span class="aae-meta-label">Posted</span>
                    <span class="aae-meta-value">${escapeHtmlForMeta(posted)}</span>
                </div>
                ${editedRow}
                <div class="aae-meta-item">
                    <span class="aae-meta-icon" aria-hidden="true"><i class="fa-regular fa-flag"></i></span>
                    <span class="aae-meta-label">Deadline</span>
                    <input type="date" class="aae-input" id="deadlineInput" value="${escapeHtmlForMeta(iso)}" style="max-width:11rem" />
                </div>`;
        }

        this.revealEditControls();
    }

    cancelEdit() {
        if (!this.isEditing) return;
        this.isEditing = false;

        const assessmentTitle = document.getElementById('assessmentTitle');
        const assessmentDescription = document.getElementById('assessmentDescription');
        const dateInfo = document.getElementById('dateInfo');

        if (assessmentTitle) assessmentTitle.contentEditable = 'false';
        if (assessmentDescription) assessmentDescription.contentEditable = 'false';

        if (dateInfo) {
            dateInfo.innerHTML = this.originalDateHtml;
            dateInfo.classList.remove('aae-meta-edit-mode');
        }

        if (this.currentAssessment) {
            if (assessmentTitle) assessmentTitle.textContent = this.currentAssessment.title;
            if (assessmentDescription) assessmentDescription.textContent = this.currentAssessment.description;
        }

        this.hideEditControls();
        this.showToast('Edit cancelled');
    }

    finishSaveSuccess() {
        this.isEditing = false;

        const assessmentTitle = document.getElementById('assessmentTitle');
        const assessmentDescription = document.getElementById('assessmentDescription');
        const dateInfo = document.getElementById('dateInfo');

        if (assessmentTitle) assessmentTitle.contentEditable = 'false';
        if (assessmentDescription) assessmentDescription.contentEditable = 'false';

        const deadlineInput = document.getElementById('deadlineInput');
        const newDeadline = deadlineInput ? deadlineInput.value : '';
        if (deadlineInput) deadlineInput.remove();

        if (dateInfo) {
            const posted = dateInfo.dataset.posted || '';
            const today = new Date().toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
            });
            const formattedDeadline = newDeadline
                ? new Date(newDeadline).toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                    year: 'numeric',
                })
                : 'N/A';
            const deadlineIso = newDeadline || '';
            applyAssessmentMetaToDateInfo(dateInfo, posted, today, formattedDeadline, deadlineIso);
            dateInfo.classList.remove('aae-meta-edit-mode');
        }

        this.hideEditControls();

        if (window.assessmentData) {
            if (assessmentTitle) window.assessmentData.title = assessmentTitle.textContent;
            if (assessmentDescription) window.assessmentData.description = assessmentDescription.textContent;
        }
        this.currentAssessment = window.assessmentData;
    }

    async saveAssessment() {
        try {
            const titleEl = document.getElementById('assessmentTitle');
            const descEl = document.getElementById('assessmentDescription');
            const container = document.getElementById('assessmentContainer');
            const assessmentId = container?.dataset?.assessmentId || '';
            const deadlineInput = document.getElementById('deadlineInput');
            const deadline = deadlineInput && deadlineInput.value
                ? new Date(deadlineInput.value).toISOString()
                : null;

            const title = titleEl?.textContent.trim() || '';
            const description = descEl?.textContent.trim() || '';

            const response = await fetch('/AdminAssessment/UpdateAssessment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ assessmentId, title, description, deadline }),
            });

            const result = await response.json();

            if (result.success) {
                this.finishSaveSuccess();
                this.showToast('Assessment saved successfully');
            } else {
                throw new Error(result.message || 'Save failed');
            }
        } catch (error) {
            console.error('Error saving assessment:', error);
            this.showToast(error.message || 'Error saving assessment');
        }
    }

    openSaveConfirm() {
        const b = document.getElementById('saveAssessmentConfirmBackdrop');
        const m = document.getElementById('saveAssessmentConfirmModal');
        if (b) {
            b.removeAttribute('hidden');
            b.setAttribute('aria-hidden', 'false');
        }
        if (m) {
            m.removeAttribute('hidden');
            m.setAttribute('aria-hidden', 'false');
        }
        document.body.style.overflow = 'hidden';
    }

    closeSaveConfirm() {
        const b = document.getElementById('saveAssessmentConfirmBackdrop');
        const m = document.getElementById('saveAssessmentConfirmModal');
        if (b) {
            b.setAttribute('hidden', '');
            b.setAttribute('aria-hidden', 'true');
        }
        if (m) {
            m.setAttribute('hidden', '');
            m.setAttribute('aria-hidden', 'true');
        }
        document.body.style.overflow = '';
    }

    showDeleteModal() {
        const deleteBackdrop = document.getElementById('deleteAssessmentConfirmBackdrop');
        const deleteModal = document.getElementById('deleteAssessmentConfirmModal');
        if (deleteBackdrop) {
            deleteBackdrop.removeAttribute('hidden');
            deleteBackdrop.setAttribute('aria-hidden', 'false');
        }
        if (deleteModal) {
            deleteModal.removeAttribute('hidden');
            deleteModal.setAttribute('aria-hidden', 'false');
        }
        document.body.style.overflow = 'hidden';
    }

    hideDeleteModal() {
        const deleteBackdrop = document.getElementById('deleteAssessmentConfirmBackdrop');
        const deleteModal = document.getElementById('deleteAssessmentConfirmModal');
        if (deleteBackdrop) {
            deleteBackdrop.setAttribute('hidden', '');
            deleteBackdrop.setAttribute('aria-hidden', 'true');
        }
        if (deleteModal) {
            deleteModal.setAttribute('hidden', '');
            deleteModal.setAttribute('aria-hidden', 'true');
        }
        document.body.style.overflow = '';
    }

    async deleteAssessment() {
        try {
            const container = document.getElementById('assessmentContainer');
            const assessmentId = container?.dataset?.assessmentId || '';
            const response = await fetch('/AdminAssessment/DeleteAssessment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ assessmentId }),
            });

            const result = await response.json();

            if (result.success) {
                this.showToast('Assessment deleted successfully');
                const classCode = this.getClassCode();
                const url = classCode
                    ? `/AdminClass/${encodeURIComponent(classCode)}`
                    : '/professordb/ProfessorDb';
                setTimeout(() => {
                    if (typeof window.navigateWithProfessorLoading === 'function') {
                        window.navigateWithProfessorLoading(url, null, 600);
                    } else {
                        window.location.href = url;
                    }
                }, 1200);
            } else {
                throw new Error(result.message || 'Delete failed');
            }
        } catch (error) {
            console.error('Error deleting assessment:', error);
            this.showToast(error.message || 'Error deleting assessment');
        } finally {
            this.hideDeleteModal();
        }
    }

    filterStudents(searchTerm) {
        const rows = document.querySelectorAll('.submitted-row:not(.header)');
        const lowerSearchTerm = searchTerm.toLowerCase();

        rows.forEach((row) => {
            const studentName = row.querySelector('.name-col')?.textContent.toLowerCase() || '';
            row.style.display = studentName.includes(lowerSearchTerm) ? 'flex' : 'none';
        });
    }

    async openLogModal(type) {
        try {
            const modal = document.getElementById(`modal-${type}`);
            const title = document.getElementById(`title-${type}`);
            const list = document.getElementById(`list-${type}`);
            if (!modal || !title || !list) return;

            title.textContent = this.getLogTitle(type);
            list.innerHTML = '<div class="loading">Loading logs...</div>';
            modal.style.display = 'flex';

            const logs = await this.fetchLogs(type);
            this.renderLogs(list, logs);

            modal.querySelector('.loglist-download')?.remove();
            let actions = modal.querySelector('.loglist-actions');
            if (!actions) {
                actions = document.createElement('div');
                actions.className = 'loglist-actions';
                actions.setAttribute('role', 'group');
                actions.setAttribute('aria-label', 'Report export');
                const btnPreview = document.createElement('button');
                btnPreview.type = 'button';
                btnPreview.className = 'loglist-btn loglist-preview';
                btnPreview.textContent = 'Preview';
                const btnPdf = document.createElement('button');
                btnPdf.type = 'button';
                btnPdf.className = 'loglist-btn loglist-download-pdf';
                btnPdf.textContent = 'Download PDF';
                actions.appendChild(btnPreview);
                actions.appendChild(btnPdf);
                title.parentElement.insertBefore(actions, title.nextSibling);
            }
            const btnPreview = actions.querySelector('.loglist-preview');
            const btnPdf = actions.querySelector('.loglist-download-pdf');
            btnPreview.onclick = () => void this.previewLogs(type, logs);
            btnPdf.onclick = () => void this.downloadLogsPdf(type, logs);
        } catch (err) {
            console.error('Error opening log modal:', err);
            this.showToast('Error loading logs');
        }
    }

    closeLogModal(type) {
        const modal = document.getElementById(`modal-${type}`);
        if (modal) modal.style.display = 'none';
    }

    getLogTitle(type) {
        switch (type) {
            case 'copy': return 'Copy Logs';
            case 'paste': return 'Paste Logs';
            case 'inspect': return 'Inspect Logs';
            case 'tabswitch': return 'Tab Switch Logs';
            case 'openprograms': return 'Open Programs Logs';
            case 'screenshare': return 'Screen Share Logs';
            default: return 'Logs';
        }
    }

    normalizeStudentName(rawName) {
        const input = String(rawName || '').trim();
        if (!input) return 'Unknown';

        const parts = input.split(/\s+/).filter(Boolean);
        if (!parts.length) return 'Unknown';

        // Case 1: full-name block repeated once: "John Doe John Doe"
        if (parts.length % 2 === 0) {
            const half = parts.length / 2;
            const firstHalf = parts.slice(0, half).join(' ');
            const secondHalf = parts.slice(half).join(' ');
            if (firstHalf.toLowerCase() === secondHalf.toLowerCase()) {
                return firstHalf;
            }
        }

        // Case 2: adjacent duplicate tokens: "John John Doe"
        const deduped = [];
        for (const token of parts) {
            const prev = deduped.length ? deduped[deduped.length - 1] : '';
            if (!prev || prev.toLowerCase() !== token.toLowerCase()) {
                deduped.push(token);
            }
        }

        return deduped.join(' ') || 'Unknown';
    }

    async fetchLogs(type) {
        const { classCode: routeClass, contentId: routeContent } = this.getRouteParams();
        const classCode = routeClass || this.getClassCode() || '';
        const assessmentId = routeContent || document.getElementById('assessmentContainer')?.dataset?.assessmentId || '';
        if (!classCode || !assessmentId) throw new Error('Missing identifiers');
        const url = type
            ? `/AdminAssessment/${encodeURIComponent(classCode)}/${encodeURIComponent(assessmentId)}/Logs?type=${encodeURIComponent(type)}`
            : `/AdminAssessment/${encodeURIComponent(classCode)}/${encodeURIComponent(assessmentId)}/Logs`;
        const res = await fetch(url);
        if (!res.ok) throw new Error('Failed to load logs');
        const json = await res.json();
        if (!json.success) throw new Error(json.message || 'Failed to load logs');
        const rows = Array.isArray(json.logs) ? json.logs : [];
        return rows.map((row) => ({
            ...row,
            student: this.normalizeStudentName(row.student),
        }));
    }

    getRouteParams() {
        try {
            const parts = window.location.pathname.split('/').filter(Boolean);
            const idx = parts.findIndex((p) => p.toLowerCase() === 'adminassessment');
            if (idx >= 0 && parts.length >= idx + 3) {
                return { classCode: parts[idx + 1], contentId: parts[idx + 2] };
            }
        } catch { /* ignore */ }
        return { classCode: '', contentId: '' };
    }

    async refreshLogCounters() {
        try {
            const logs = await this.fetchLogs();
            const counts = { copy: 0, paste: 0, inspect: 0, tabswitch: 0, openprograms: 0, screenshare: 0 };
            logs.forEach((l) => {
                const t = (l.type || '').toLowerCase();
                const c = l.count || 1;
                if (Object.prototype.hasOwnProperty.call(counts, t)) counts[t] += c;
            });
            const apply = (id, val) => {
                const el = document.getElementById(id);
                if (el) el.textContent = String(val);
            };
            apply('logCopy', counts.copy);
            apply('logPaste', counts.paste);
            apply('logInspect', counts.inspect);
            apply('logTabSwitch', counts.tabswitch);
            apply('logOpenPrograms', counts.openprograms);
            apply('logScreenShare', counts.screenshare);

            const flagEl = document.getElementById('assessmentFlag');
            if (flagEl) {
                const total = counts.copy + counts.paste + counts.inspect + counts.tabswitch + counts.openprograms + counts.screenshare;
                const shouldVoid = total >= 20;
                flagEl.textContent = shouldVoid ? 'Void' : 'Valid';
                flagEl.classList.toggle('void', shouldVoid);
            }
        } catch (err) {
            console.warn('Could not refresh log counters:', err);
        }
    }

    renderLogs(container, logs) {
        if (!logs.length) {
            container.innerHTML = '<div class="empty">No logs found for this category.</div>';
            return;
        }
        const groupedMap = new Map();
        const sanitizeDetails = (raw) => {
            try {
                const obj = JSON.parse(raw || '{}');
                if (obj && typeof obj === 'object') {
                    const o = {};
                    Object.keys(obj).forEach((k) => {
                        if (k !== 'count') o[k] = obj[k];
                    });
                    return o;
                }
            } catch { /* ignore */ }
            return { details: String(raw || '') };
        };
        const typeLabel = (rawType) => {
            const t = String(rawType || '').toLowerCase();
            if (t === 'tabswitch') return 'tab switch';
            if (t === 'openprograms') return 'open programs';
            if (t === 'screenshare') return 'screen share';
            return t || 'event';
        };
        const detailsHtml = (obj, countNum, rowType) => {
            const entries = Object.keys(obj).map((k) => ({ k, v: obj[k] }));
            const rowsWithTimes = entries.concat([{ k: `times of ${typeLabel(rowType)}`, v: `${countNum || 0}x` }]);
            if (!rowsWithTimes.length) return `<div class="loglist-details number-only">${countNum || 0}</div>`;
            const rows = rowsWithTimes.map((e) => `<div class="kv-item"><span class="kv-key">${e.k}</span><span class="kv-val">${e.v}</span></div>`).join('');
            return `<div class="loglist-details"><div class="kv-grid">${rows}</div></div>`;
        };

        logs.forEach((l) => {
            const obj = sanitizeDetails(l.details || '');
            const key = [
                String(l.student || 'Unknown').toLowerCase(),
                String(l.email || '').toLowerCase(),
                String(l.type || '').toLowerCase(),
                JSON.stringify(obj),
            ].join('|');

            const existing = groupedMap.get(key);
            if (!existing) {
                groupedMap.set(key, {
                    ...l,
                    _detailsObj: obj,
                    _totalCount: l.count || 1,
                    _latestTimeMs: new Date(l.time).getTime() || 0,
                });
                return;
            }

            existing._totalCount += l.count || 1;
            const ms = new Date(l.time).getTime() || 0;
            if (ms > existing._latestTimeMs) {
                existing._latestTimeMs = ms;
                existing.time = l.time;
            }
        });

        const groupedLogs = Array.from(groupedMap.values()).sort((a, b) => (b._latestTimeMs || 0) - (a._latestTimeMs || 0));

        const frag = document.createDocumentFragment();
        groupedLogs.forEach((l) => {
            const row = document.createElement('div');
            row.className = 'loglist-row';
            const time = new Date(l.time).toLocaleString();
            const det = detailsHtml(l._detailsObj || {}, l._totalCount || 1, l.type || '');
            row.innerHTML = `
                <div class="loglist-left">
                    <div class="loglist-name">${l.student || 'Unknown'}</div>
                    <div class="loglist-email">${l.email || ''}</div>
                </div>
                <div class="loglist-right">
                    <div class="loglist-meta">
                        <span class="badge-type badge-type-${(l.type || '').toLowerCase()}">${l.type}</span>
                        <span class="badge-time">${time}</span>
                    </div>
                    ${det}
                </div>`;
            frag.appendChild(row);
        });

        container.innerHTML = '';
        container.appendChild(frag);
    }

    async fetchLogoDataUrl() {
        try {
            const res = await fetch('/images/SLSHS.png');
            const blob = await res.blob();
            return await new Promise((resolve) => {
                const reader = new FileReader();
                reader.onloadend = () => resolve(reader.result);
                reader.readAsDataURL(blob);
            });
        } catch {
            return '';
        }
    }

    escCell(v) {
        const d = document.createElement('div');
        d.textContent = String(v ?? '');
        return d.innerHTML;
    }

    getReportMeta(type) {
        const schoolName = 'Sta. Lucia Senior Highschool';
        const subjectName = document.querySelector('.aae-subject-title')?.textContent
            || document.querySelector('.subject-name')?.textContent
            || 'Assessment Logs';
        const classCode = (this.getClassCode() || '').trim();
        const teacherName = (document.querySelector('.aae-profile-name')?.textContent
            || document.querySelector('.teacher-name')?.textContent
            || document.querySelector('.user-name')?.textContent || '').trim();
        const reportDate = new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' });
        const assessmentTitle = document.getElementById('assessmentTitle')?.textContent.trim() || 'Assessment';
        const logTitle = this.getLogTitle(type);
        return { schoolName, subjectName, classCode, teacherName, reportDate, assessmentTitle, logTitle };
    }

    async buildReportHtml(type, logs, options = {}) {
        const { includePrintScript = false } = options;
        const logoDataUrl = await this.fetchLogoDataUrl();
        const { schoolName, subjectName, classCode, teacherName, reportDate, assessmentTitle, logTitle } = this.getReportMeta(type);
        const esc = (v) => this.escCell(v);

        const rows = logs.map((l) => {
            let duration = String(l.duration || '');
            duration = duration.replace(/duration:\s*/i, '').replace(/["']/g, '').trim();
            return `
                    <tr>
                        <td>${esc(l.student)}</td>
                        <td>${esc(l.email)}</td>
                        <td>${esc(l.type)}</td>
                        <td>${esc(l.count)}</td>
                        <td>${esc(duration)}</td>
                        <td>${esc(new Date(l.time).toLocaleString())}</td>
                    </tr>
                `;
        }).join('');

        const printBlock = includePrintScript
            ? `<script>
        window.onload = function() { setTimeout(function() { window.print(); }, 500); }
    <\/script>`
            : '';

        return `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>${esc(logTitle)} - ${esc(subjectName)}</title>
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #333; }
        .header { text-align: center; margin-bottom: 30px; position: relative; }
        .logo { position: absolute; left: 0; top: 0; }
        .logo img { height: 80px; width: auto; }
        .school-name { font-size: 24px; font-weight: bold; margin: 0 0 5px 0; color: #000; }
        .subject-name { font-size: 18px; margin: 5px 0; color: #444; }
        .sub-header { font-size: 14px; margin: 5px 0; color: #555; }
        .report-title { font-size: 20px; font-weight: bold; margin: 20px 0 10px 0; text-align: center; border-bottom: 2px solid #eee; padding-bottom: 10px; }
        .meta { display: flex; justify-content: space-between; margin-bottom: 15px; font-size: 14px; }
        table { width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 12px; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f4f4f4; font-weight: bold; }
        tr:nth-child(even) { background-color: #f9f9f9; }
        @media print {
            body { margin: 20px; }
            .no-print { display: none; }
        }
    </style>
</head>
<body>
    <div class="header">
        <div class="logo">${logoDataUrl ? `<img src="${logoDataUrl}" alt="Logo">` : ''}</div>
        <div class="school-name">${esc(schoolName)}</div>
        <div class="subject-name">${esc(subjectName)}</div>
        ${classCode ? `<div class="sub-header">Class code: ${esc(classCode)}</div>` : ''}
        ${teacherName ? `<div class="sub-header">Teacher: ${esc(teacherName)}</div>` : ''}
    </div>

    <div class="report-title">${esc(assessmentTitle)} - ${esc(logTitle)}</div>

    <div class="meta">
        <div>Generated: ${esc(reportDate)}</div>
        <div>Total Records: ${logs.length}</div>
    </div>

    <table>
        <thead>
            <tr>
                <th>Student</th>
                <th>Email</th>
                <th>Type</th>
                <th>Count</th>
                <th>Duration</th>
                <th>Time</th>
            </tr>
        </thead>
        <tbody>
            ${rows}
        </tbody>
    </table>
    ${printBlock}
</body>
</html>
        `;
    }

    async previewLogs(type, logs) {
        try {
            const html = await this.buildReportHtml(type, logs, { includePrintScript: false });
            const blob = new Blob([html], { type: 'text/html' });
            const url = URL.createObjectURL(blob);
            window.open(url, '_blank');
            this.showToast('Preview opened in a new tab.');
        } catch (err) {
            console.error('Preview failed:', err);
            this.showToast('Preview failed');
        }
    }

    async downloadLogsPdf(type, logs) {
        const loadScript = (src) => new Promise((resolve, reject) => {
            if (Array.from(document.scripts).some((s) => s.src && s.src.indexOf(src) !== -1)) {
                resolve();
                return;
            }
            const el = document.createElement('script');
            el.src = src;
            el.onload = () => resolve();
            el.onerror = () => reject(new Error(`Failed to load ${src}`));
            document.head.appendChild(el);
        });
        try {
            if (!(window.pdfMake && window.pdfMake.createPdf)) {
                await loadScript('https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.2.7/pdfmake.min.js');
            }
            if (!(window.pdfMake && window.pdfMake.vfs)) {
                await loadScript('https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.2.7/vfs_fonts.js');
            }
            const logoDataUrl = await this.fetchLogoDataUrl();
            const { schoolName, subjectName, classCode, teacherName, reportDate, assessmentTitle, logTitle } = this.getReportMeta(type);

            const headerStack = [
                { text: schoolName, style: 'school', alignment: 'center' },
                { text: subjectName || '', style: 'subtitle', alignment: 'center', margin: [0, 2, 0, 0] },
            ];
            if (classCode) headerStack.push({ text: `Class code: ${classCode}`, style: 'metaHead', alignment: 'center', margin: [0, 2, 0, 0] });
            if (teacherName) headerStack.push({ text: `Teacher: ${teacherName}`, style: 'metaHead', alignment: 'center', margin: [0, 2, 0, 0] });

            const headerBlock = logoDataUrl
                ? {
                    columns: [
                        { image: logoDataUrl, width: 64, margin: [0, 0, 12, 0] },
                        { width: '*', stack: headerStack },
                    ],
                }
                : { stack: headerStack };

            const tableBody = [
                [
                    { text: 'Student', style: 'tableHeader' },
                    { text: 'Email', style: 'tableHeader' },
                    { text: 'Type', style: 'tableHeader' },
                    { text: 'Count', style: 'tableHeader' },
                    { text: 'Duration', style: 'tableHeader' },
                    { text: 'Time', style: 'tableHeader' },
                ],
            ];
            logs.forEach((l) => {
                let duration = String(l.duration || '');
                duration = duration.replace(/duration:\s*/i, '').replace(/["']/g, '').trim();
                tableBody.push([
                    { text: String(l.student || ''), style: 'cell' },
                    { text: String(l.email || ''), style: 'cell' },
                    { text: String(l.type || ''), style: 'cell' },
                    { text: String(l.count ?? ''), style: 'cell' },
                    { text: duration, style: 'cell' },
                    { text: new Date(l.time).toLocaleString(), style: 'cell' },
                ]);
            });

            const safeClass = String(this.getClassCode() || 'class').replace(/[^a-zA-Z0-9_-]/g, '_');
            const ts = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
            const baseName = `AntiCheat_${type}_${safeClass}_${ts}`;

            const docDefinition = {
                pageOrientation: 'landscape',
                pageSize: 'A4',
                info: { title: `${assessmentTitle} — ${logTitle}` },
                content: [
                    headerBlock,
                    {
                        text: `${assessmentTitle} — ${logTitle}`,
                        style: 'reportTitle',
                        alignment: 'center',
                        margin: [0, 12, 0, 8],
                    },
                    {
                        columns: [
                            { text: `Generated: ${reportDate}`, style: 'metaDate', width: '*' },
                            { text: `Total Records: ${logs.length}`, style: 'metaDate', alignment: 'right', width: 'auto' },
                        ],
                        margin: [0, 0, 0, 12],
                    },
                    {
                        table: {
                            headerRows: 1,
                            widths: ['*', '*', 70, 45, 55, 110],
                            body: tableBody,
                        },
                        layout: {
                            fillColor: (rowIndex) => (rowIndex === 0 ? '#f1f5f9' : null),
                        },
                    },
                ],
                styles: {
                    school: { fontSize: 22, bold: true, color: '#0b213a' },
                    subtitle: { fontSize: 16, bold: true, color: '#334155' },
                    metaHead: { fontSize: 15, bold: true, color: '#334155' },
                    reportTitle: { fontSize: 14, bold: true, color: '#0b213a' },
                    metaDate: { fontSize: 10, color: '#475569' },
                    tableHeader: { bold: true, fontSize: 9, color: '#111827' },
                    cell: { fontSize: 8 },
                },
                defaultStyle: { fontSize: 8 },
            };

            window.pdfMake.createPdf(docDefinition).download(`${baseName}.pdf`);
            this.showToast('Downloading PDF...');
        } catch (err) {
            console.error('PDF export failed:', err);
            this.showToast('Could not generate PDF');
        }
    }
}

function initializeDownloadButtons(showToast) {
    const attachmentList = document.getElementById('attachmentList');
    const downloadButtons = attachmentList?.querySelectorAll('.download-btn');
    downloadButtons?.forEach((btn) => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const fileName = btn.dataset.filename;
            const container = document.getElementById('assessmentContainer');
            const assessmentId = container?.dataset?.assessmentId || '';
            if (!fileName || !assessmentId) {
                if (showToast) showToast('File not available');
                return;
            }
            window.location.href = `/AdminAssessment/DownloadFile/${encodeURIComponent(fileName)}?assessmentId=${encodeURIComponent(assessmentId)}`;
        });
    });
}

function isAssessmentEditing() {
    const el = document.getElementById('assessmentTitle');
    return el && el.getAttribute('contenteditable') === 'true';
}

document.addEventListener('DOMContentLoaded', () => {
    const toastEl = document.getElementById('toast');
    function showToast(message) {
        if (!toastEl) return;
        toastEl.textContent = message;
        toastEl.className = 'toast show';
        setTimeout(() => toastEl.classList.remove('show'), 2500);
    }

    function getClassCode() {
        return (
            document.getElementById('adminAssessmentPage')?.dataset?.classCode ||
            document.body?.dataset?.classCode ||
            ''
        );
    }

    const backButton = document.querySelector('.back-button');
    backButton?.addEventListener('click', () => {
        const cc = getClassCode();
        const dash = typeof window.resolveInstructorDashboardUrl === 'function' ? window.resolveInstructorDashboardUrl() : '/professordb/ProfessorDb';
        const target = cc ? `/AdminClass/${encodeURIComponent(cc)}` : dash;
        const msg = typeof window.resolveAdminNavToastMessage === 'function'
            ? window.resolveAdminNavToastMessage(target)
            : 'Returning...';
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading(target, msg, 600);
        } else {
            showToast(msg);
            setTimeout(() => {
                window.location.href = target;
            }, 600);
        }
    });

    const linkInput = document.getElementById('assessmentLinkInput');
    const linkModal = document.getElementById('linkModal');
    const linkModalInput = document.getElementById('linkModalInput');
    const linkModalSave = document.getElementById('linkModalSave');
    const linkModalCancel = document.getElementById('linkModalCancel');

    function openLinkModal() {
        if (!linkModal) return;
        if (linkModalInput && linkInput) linkModalInput.value = linkInput.value || '';
        linkModal.classList.add('show');
        linkModalInput?.focus();
    }

    function closeLinkModal() {
        linkModal?.classList.remove('show');
    }

    linkInput?.addEventListener('click', () => {
        openLinkModal();
    });

    linkModalCancel?.addEventListener('click', () => {
        closeLinkModal();
    });

    linkModalSave?.addEventListener('click', async () => {
        try {
            const link = (linkModalInput?.value || '').trim();
            const container = document.getElementById('assessmentContainer');
            const assessmentId = container?.dataset?.assessmentId || '';
            if (!assessmentId) {
                showToast('Missing assessment');
                return;
            }
            const resp = await fetch('/AdminAssessment/UpdateAssessment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ assessmentId, link }),
            });
            const json = await resp.json();
            if (json && json.success) {
                if (linkInput) linkInput.value = link;
                showToast('Saved link');
                closeLinkModal();
            } else {
                showToast(json?.message || 'Could not save link');
            }
        } catch (e) {
            console.error(e);
            showToast('Error saving link');
        }
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && linkModal?.classList.contains('show')) {
            closeLinkModal();
        }
    });

    const checkSubmissionBtn = document.getElementById('checkSubmissionBtn');
    checkSubmissionBtn?.addEventListener('click', () => {
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading('adminchecksubmissions.html', 'Opening submissions...', 800);
        } else {
            showToast('Opening submissions...');
            setTimeout(() => {
                window.location.href = 'adminchecksubmissions.html';
            }, 800);
        }
    });

    const replaceFileBtn = document.getElementById('replaceFileBtn');
    const replaceFileInput = document.getElementById('replaceFileInput');

    replaceFileBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!isAssessmentEditing()) {
            showToast('Enable edit mode first');
            return;
        }
        replaceFileInput?.click();
    });

    replaceFileInput?.addEventListener('change', async () => {
        const file = replaceFileInput.files && replaceFileInput.files[0];
        if (!file) return;
        try {
            const classCode = getClassCode();
            const fd = new FormData();
            fd.append('file', file);
            fd.append('classCode', classCode);
            fd.append('type', 'assessment');
            const up = await fetch('/AdminClass/UploadFile', { method: 'POST', body: fd });
            if (!up.ok) throw new Error('Upload failed');
            const upRes = await up.json();
            if (!upRes.success) throw new Error(upRes.message || 'Upload failed');

            const assessmentContainer = document.getElementById('assessmentContainer');
            const assessmentId = assessmentContainer?.dataset?.assessmentId || '';
            const linkRes = await fetch('/AdminAssessment/ReplaceAttachment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ assessmentId, fileName: file.name, fileUrl: upRes.fileUrl }),
            });
            if (!linkRes.ok) throw new Error('Replace failed');
            const linkJson = await linkRes.json();
            if (!linkJson.success) throw new Error(linkJson.message || 'Replace failed');

            const attList = document.getElementById('attachmentList');
            if (attList) {
                attList.innerHTML = '';
                const box = document.createElement('div');
                box.className = 'attachment-box aae-file-chip';
                box.dataset.fileLabel = file.name;
                const span = document.createElement('span');
                span.className = 'attachment-name';
                span.dataset.filename = file.name;
                span.dataset.contentid = assessmentId;
                span.title = `Options for ${file.name}`;
                span.textContent = file.name;
                box.appendChild(span);
                attList.appendChild(box);
                const empty = document.getElementById('attachmentEmptyState');
                if (empty) empty.style.display = 'none';
                document.getElementById('attachmentDropzone')?.classList.add('has-files');
                initializeDownloadButtons(showToast);
            }
            showToast('File replaced');
        } catch (err) {
            console.error(err);
            showToast(err.message || 'Could not replace file');
        } finally {
            try {
                replaceFileInput.value = '';
            } catch { /* ignore */ }
        }
    });

    const editControls = document.getElementById('editControls');
    if (editControls) {
        editControls.style.display = 'none';
        editControls.hidden = true;
    }

    /* Drop zone: drag-and-drop + click (when editing) */
    const attachmentDropzone = document.getElementById('attachmentDropzone');
    attachmentDropzone?.addEventListener('click', (e) => {
        if (e.target.closest('.attachment-name')) return;
        if (!isAssessmentEditing()) return;
        e.preventDefault();
        document.getElementById('replaceFileInput')?.click();
    });
    ['dragenter', 'dragover'].forEach((ev) => {
        attachmentDropzone?.addEventListener(ev, (e) => {
            e.preventDefault();
            e.stopPropagation();
            if (isAssessmentEditing()) attachmentDropzone.classList.add('aae-dropzone-active');
        });
    });
    attachmentDropzone?.addEventListener('dragleave', (e) => {
        e.preventDefault();
        attachmentDropzone.classList.remove('aae-dropzone-active');
    });
    attachmentDropzone?.addEventListener('drop', (e) => {
        e.preventDefault();
        e.stopPropagation();
        attachmentDropzone.classList.remove('aae-dropzone-active');
        if (!isAssessmentEditing()) {
            showToast('Enable edit mode first');
            return;
        }
        const f = e.dataTransfer?.files?.[0];
        const input = document.getElementById('replaceFileInput');
        if (f && input) {
            try {
                const dt = new DataTransfer();
                dt.items.add(f);
                input.files = dt.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            } catch (err) {
                console.error(err);
                showToast('Could not add file');
            }
        }
    });

    initializeDownloadButtons(showToast);

    function escapeHtmlComment(s) {
        try {
            return String(s || '').replace(/[&<>"']/g, function (c) {
                return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
            });
        } catch {
            return '';
        }
    }

    const adminCommentForm = document.getElementById('adminCommentForm');
    const adminCommentText = document.getElementById('adminCommentText');
    const postAdminCommentBtn = document.getElementById('postAdminCommentBtn');
    const adminCommentList = document.getElementById('adminCommentList');

    function getAdminCommentAntiForgeryToken() {
        const el = adminCommentForm ? adminCommentForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    function getAssessmentContentId() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="contentId"]') : null;
        return input ? input.value : '';
    }

    function getAssessmentClassCodeFromForm() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="classCode"]') : null;
        return input ? input.value : '';
    }

    async function loadAdminAssessmentComments() {
        const contentId = getAssessmentContentId();
        if (!contentId || !adminCommentList) return;
        try {
            const res = await fetch('/StudentAssessment/GetComments?contentId=' + encodeURIComponent(contentId), { credentials: 'same-origin' });
            const data = await res.json();
            if (!data || !data.success) return;
            adminCommentList.innerHTML = '';
            data.comments.forEach(renderAdminAssessmentComment);
        } catch {}
    }

    function renderAdminAssessmentComment(c) {
        const box = document.createElement('div');
        box.className = 'comment-box';
        box.dataset.id = c.id;
        const nameHtml = '<div class="student-name">' + escapeHtmlComment(c.authorName) + (c.role ? ' • ' + escapeHtmlComment(c.role) : '') + '</div>';
        const textHtml = '<div class="comment-text">' + escapeHtmlComment(c.text) + '</div>';
        const dateHtml = '<div class="comment-datetime">' + new Date(c.createdAt).toLocaleString() + '</div>';
        box.innerHTML = nameHtml + textHtml + dateHtml;
        if (Array.isArray(c.replies)) {
            c.replies.forEach((r) => {
                const rdiv = document.createElement('div');
                rdiv.className = 'instructor-reply';
                rdiv.innerHTML =
                    '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' +
                    escapeHtmlComment(r.authorName) +
                    (r.role ? ' • ' + escapeHtmlComment(r.role) : '') +
                    '</span></div><div class="reply-text">' +
                    escapeHtmlComment(r.text) +
                    '</div><div class="reply-datetime">' +
                    new Date(r.createdAt).toLocaleString() +
                    '</div>';
                box.appendChild(rdiv);
            });
        }
        const replyToggle = document.createElement('div');
        replyToggle.className = 'reply-option';
        replyToggle.innerHTML = '<i class="fa-solid fa-reply"></i> Reply';
        box.appendChild(replyToggle);

        const replyArea = document.createElement('div');
        replyArea.className = 'reply-box-area';
        replyArea.innerHTML =
            '<textarea class="reply-box" placeholder="Write a reply..."></textarea><button type="button" class="reply-submit-btn">Post Reply</button>';
        box.appendChild(replyArea);

        replyToggle.addEventListener('click', () => {
            replyArea.style.display = replyArea.style.display === 'flex' ? 'none' : 'flex';
        });

        const submitBtn = replyArea.querySelector('.reply-submit-btn');
        submitBtn.addEventListener('click', async () => {
            const text = (replyArea.querySelector('.reply-box').value || '').trim();
            if (!text) return;
            const token = getAdminCommentAntiForgeryToken();
            try {
                const res = await fetch('/StudentAssessment/PostReply', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded', RequestVerificationToken: token },
                    body: new URLSearchParams({ commentId: c.id, text }),
                });
                const data = await res.json();
                if (data && data.success && data.reply) {
                    loadAdminAssessmentComments();
                }
            } catch {}
        });

        adminCommentList.appendChild(box);
    }

    postAdminCommentBtn?.addEventListener('click', async () => {
        const text = (adminCommentText?.value || '').trim();
        if (!text) return;
        const token = getAdminCommentAntiForgeryToken();
        const contentId = getAssessmentContentId();
        const classCode = getAssessmentClassCodeFromForm();
        try {
            const res = await fetch('/StudentAssessment/PostComment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', RequestVerificationToken: token },
                body: new URLSearchParams({ contentId, classCode, text }),
            });
            const data = await res.json();
            if (data && data.success) {
                adminCommentText.value = '';
                loadAdminAssessmentComments();
            }
        } catch {}
    });

    loadAdminAssessmentComments();

    function initAssessmentSubmissionDeadlinePanel(toastFn) {
        const panel = document.getElementById('assessmentSubmissionDeadlinePanel');
        if (!panel) return;
        const assessmentId = panel.dataset.assessmentId || '';
        const allowBtn = document.getElementById('allowLateAssessmentBtn');
        const revokeBtn = document.getElementById('revokeLateAssessmentBtn');
        const statusBadge = document.getElementById('assessmentStatusBadge');
        function syncButtons(data) {
            if (!data) return;
            const locked = !!data.isLockedForStudents;
            const allow = !!data.allowPastDeadline;
            panel.dataset.allowLate = allow ? 'true' : 'false';
            panel.dataset.locked = locked ? 'true' : 'false';
            const state = locked ? 'locked' : allow ? 'late-open' : 'open';
            panel.dataset.state = state;
            const badgeEl = document.getElementById('assessmentSubmissionDeadlineBadge');
            if (badgeEl) {
                if (state === 'locked') {
                    badgeEl.innerHTML = '<i class="fa-solid fa-lock" aria-hidden="true"></i> Closed to students';
                } else if (state === 'late-open') {
                    badgeEl.innerHTML = '<i class="fa-solid fa-unlock-keyhole" aria-hidden="true"></i> Late submissions on';
                } else {
                    badgeEl.innerHTML = '<i class="fa-regular fa-circle-check" aria-hidden="true"></i> Quiz open';
                }
            }
            if (allowBtn) allowBtn.style.display = locked ? 'inline-flex' : 'none';
            const closed = (statusBadge?.textContent || '').trim() === 'Closed';
            if (revokeBtn) revokeBtn.style.display = allow && closed ? 'inline-flex' : 'none';
        }
        allowBtn?.addEventListener('click', async () => {
            try {
                const res = await fetch('/AdminAssessment/SetSubmissionUnlock', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ assessmentId, allowPastDeadline: true }),
                });
                const data = await res.json();
                if (data && data.success) {
                    toastFn('Late submissions enabled');
                    syncButtons(data);
                }
            } catch {
                toastFn('Could not update');
            }
        });
        revokeBtn?.addEventListener('click', async () => {
            try {
                const res = await fetch('/AdminAssessment/SetSubmissionUnlock', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ assessmentId, allowPastDeadline: false }),
                });
                const data = await res.json();
                if (data && data.success) {
                    toastFn('Late submissions stopped');
                    syncButtons(data);
                }
            } catch {
                toastFn('Could not update');
            }
        });
    }
    initAssessmentSubmissionDeadlinePanel(showToast);

    function bindIntegrityLockTable(showToastFn) {
        const tbody = document.getElementById('integrityLockedTableBody');
        if (!tbody) return;
        const page = document.getElementById('adminAssessmentPage');
        const classCode = page?.dataset?.classCode || '';
        const container = document.getElementById('assessmentContainer');
        const assessmentId = container?.dataset?.assessmentId || '';
        tbody.addEventListener('click', async (e) => {
            const restore = e.target.closest('[data-action="restore-integrity"]');
            const revoke = e.target.closest('[data-action="revoke-integrity"]');
            if (!restore && !revoke) return;
            const studentId = restore?.dataset?.studentId || revoke?.dataset?.studentId;
            const studentEmail = restore?.dataset?.studentEmail || '';
            if (!studentId || !classCode || !assessmentId) return;
            const url = restore ? '/AdminAssessment/RestoreIntegrityAccess' : '/AdminAssessment/RevokeIntegrityAccess';
            try {
                const res = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ classCode, assessmentId, studentId, studentEmail }),
                });
                const data = await res.json();
                if (data && data.success) {
                    showToastFn(restore ? 'Access restored for student.' : 'Override removed — student is locked again.');
                    window.location.reload();
                } else {
                    showToastFn((data && data.message) || 'Could not update.');
                }
            } catch {
                showToastFn('Could not update.');
            }
        });
    }
    bindIntegrityLockTable(showToast);

    new AdminAssessmentManager({ showToast, getClassCode });
});
