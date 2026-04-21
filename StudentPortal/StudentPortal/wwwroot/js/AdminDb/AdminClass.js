document.addEventListener('DOMContentLoaded', () => {

    // ------------------ ELEMENT REFERENCES -------------------
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const acUploadModalBackdrop = document.getElementById('acUploadModalBackdrop');
    const acUploadModal = document.getElementById('acUploadModal');
    const acModalBody = document.getElementById('acModalBody');
    const acModalTitle = document.getElementById('acModalTitle');
    const acModalSubmit = document.getElementById('acModalSubmit');
    const acModalCancel = document.getElementById('acModalCancel');
    const acModalCloseX = document.getElementById('acModalCloseX');
    const adminClassDeleteToolbar = document.getElementById('adminClassDeleteToolbar');
    const acDeleteCancelBtn = document.getElementById('acDeleteCancelBtn');
    const acDeleteConfirmBtn = document.getElementById('acDeleteConfirmBtn');
    const deleteQuickBtn = document.querySelector('.ac-quick-btn[data-ac-action="delete"]');
    const deleteBackdrop = document.getElementById('deleteContentConfirmBackdrop');
    const deleteModal = document.getElementById('deleteContentConfirmModal');
    const deleteBody = document.getElementById('deleteBody');
    const deleteSubmit = document.getElementById('deleteSubmit');
    const deleteCancel = document.getElementById('deleteCancel');
    const classContent = document.getElementById('classContent');
    const toast = document.getElementById('toast');
    const announcementCard = document.getElementById('createAnnouncementCard');
    const announcementInput = document.getElementById('announcementInput');
    const announceBtn = document.getElementById('announceBtn');
    const announcePostConfirmBackdrop = document.getElementById('announcePostConfirmBackdrop');
    const announcePostConfirmModal = document.getElementById('announcePostConfirmModal');
    const announcePostConfirmCancel = document.getElementById('announcePostConfirmCancel');
    const announcePostConfirmOk = document.getElementById('announcePostConfirmOk');
    const backButton = document.querySelector('.back-button');
    const manageButtons = document.querySelectorAll('.manage-btn');
    const mobileToggleBtn = document.getElementById('mobileToggleBtn');
    const classInfo = document.querySelector('.class-info');

    if (toast && toast.textContent && toast.textContent.trim()) {
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 2800);
    }

    // ------------------ MOBILE TOGGLE ------------------------
    if (mobileToggleBtn && classInfo && classContent) {
        // Start with class-info visible on mobile, content hidden (or vice versa)
        // Let's start with content visible on mobile for better UX
        const isMobile = () => window.innerWidth <= 768;

        const updateMobileView = () => {
            if (isMobile()) {
                // By default on mobile, show content, hide info
                if (!classInfo.classList.contains('mobile-hide') && !classContent.classList.contains('mobile-hide')) {
                    classInfo.classList.add('mobile-hide');
                    mobileToggleBtn.querySelector('i').className = 'fa-solid fa-circle-info';
                }
            } else {
                // On desktop, ensure both are visible
                classInfo.classList.remove('mobile-hide');
                classContent.classList.remove('mobile-hide');
            }
        };

        // Initial check
        updateMobileView();
        window.addEventListener('resize', updateMobileView);

        mobileToggleBtn.addEventListener('click', () => {
            const infoHidden = classInfo.classList.toggle('mobile-hide');
            const contentHidden = classContent.classList.toggle('mobile-hide');
            
            // Update icon based on what is NOW visible
            const icon = mobileToggleBtn.querySelector('i');
            if (infoHidden) {
                // Content is visible
                icon.className = 'fa-solid fa-circle-info';
                showToast('Viewing Class Content');
            } else {
                // Info is visible
                icon.className = 'fa-solid fa-list';
                showToast('Viewing Class Info');
            }
            mobileToggleBtn.classList.toggle('active', !infoHidden);
        });
    }

   
    let currentPage = 'class';
    function setActivePage(page) {
        actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
        currentPage = page;
    }
    setActivePage(currentPage);

    actions.forEach((action) => {
        action.addEventListener('click', () => {
            const page = action.dataset.page;
            const icon = action.querySelector('i');

            if (
                (icon.classList.contains('fa-house') && currentPage === 'home') ||
                (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
                (icon.classList.contains('fa-clipboard-question') && currentPage === 'assessment')
            ) {
                showToast("🏠 You're already here.");
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }

            setActivePage(page);

            if (icon.classList.contains('fa-house')) {
                const dash = typeof window.resolveInstructorDashboardUrl === 'function' ? window.resolveInstructorDashboardUrl() : '/professordb/ProfessorDb';
                navigateWithAnimation(dash, 'Redirecting to Dashboard');
            }
            else if (icon.classList.contains('fa-book')) {
                navigateWithAnimation('/AdminSubject', 'Opening subjects');
            }
            else if (icon.classList.contains('fa-clipboard-question')) {
                navigateWithAnimation('/AdminAssessmentList', 'Opening assessments');
            }
        });
    });

    function navigateWithAnimation(url, message) {
        radialActions?.classList.remove('show');
        menuOpen = false;
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading(url, message, 600);
        } else {
            showToast(message);
            if (typeof window.setProfessorDbShellLoading === 'function') window.setProfessorDbShellLoading(true);
            setTimeout(() => { window.location.href = url; }, 600);
        }
    }

    // ------------------ QUICK ACTIONS + DELETE MODE ----------
    let radialMode = 'base'; // base | delete (kept for content-card click behavior)
    let isAction2Active = false;

    document.querySelectorAll('.ac-quick-btn[data-ac-action]').forEach((btn) => {
        const action = btn.getAttribute('data-ac-action');
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            if (action === 'delete') {
                if (radialMode === 'delete') {
                    setBaseState();
                } else {
                    closeModal();
                    setDeleteState();
                }
                return;
            }
            setBaseState();
            openContentModal(action);
        });
    });

    acDeleteCancelBtn?.addEventListener('click', () => setBaseState());
    acDeleteConfirmBtn?.addEventListener('click', () => openDeleteConfirmModal());

    function setBaseState() {
        radialMode = 'base';
        isAction2Active = false;
        deleteQuickBtn?.classList.remove('is-mode-active');
        adminClassDeleteToolbar?.setAttribute('hidden', '');
        adminClassDeleteToolbar?.setAttribute('aria-hidden', 'true');
        clearDeleteSelection();
    }
    function setDeleteState() {
        radialMode = 'delete';
        isAction2Active = true;
        deleteQuickBtn?.classList.add('is-mode-active');
        adminClassDeleteToolbar?.removeAttribute('hidden');
        adminClassDeleteToolbar?.setAttribute('aria-hidden', 'false');
        enableDeleteSelection();
    }

    // ------------------ MODAL HANDLING -----------------------
    let currentCreateType = null;
    let currentSelectedFiles = [];
    let currentFileInput = null;

    function renderSelectedFiles() {
        const list = document.getElementById('c_file_list');
        if (!list) return;
        list.innerHTML = '';
        if (!currentSelectedFiles || currentSelectedFiles.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'file-empty';
            empty.textContent = 'No files selected';
            list.appendChild(empty);
            return;
        }
        currentSelectedFiles.forEach((file, idx) => {
            const item = document.createElement('div');
            item.className = 'file-item';
            const name = document.createElement('span');
            name.className = 'file-name';
            name.textContent = file.name;
            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'remove-file';
            removeBtn.textContent = 'Remove';
            removeBtn.addEventListener('click', () => {
                currentSelectedFiles.splice(idx, 1);
                const dt = new DataTransfer();
                currentSelectedFiles.forEach(f => dt.items.add(f));
                if (currentFileInput) currentFileInput.files = dt.files;
                renderSelectedFiles();
            });
            item.append(name, removeBtn);
            list.appendChild(item);
        });
    }

    function wrapLabeledField(labelText, el) {
        const wrap = document.createElement('div');
        wrap.className = 'ac-field-wrap';
        if (labelText) {
            const lab = document.createElement('label');
            lab.className = 'ac-field-label';
            lab.textContent = labelText;
            if (el.id) lab.setAttribute('for', el.id);
            wrap.appendChild(lab);
        }
        wrap.appendChild(el);
        return wrap;
    }

    function openContentModal(type) {
        const wasOpen = acUploadModalBackdrop && !acUploadModalBackdrop.hasAttribute('hidden');
        currentCreateType = type;
        if (acModalTitle) {
            acModalTitle.textContent = {
                material: 'Upload material',
                task: 'Upload task',
                assessment: 'Upload assessment'
            }[type] || 'Upload';
        }

        // preserve existing input values when switching type
        const prevTitle = (document.getElementById('c_title') || {}).value || '';
        const prevDesc = (document.getElementById('c_desc') || {}).value || '';
        const prevDeadline = (document.getElementById('c_deadline') || {}).value || '';
        const prevLink = (document.getElementById('c_link') || {}).value || '';
        const prevMax = (document.getElementById('c_maxgrade') || {}).value || '';
        if (acModalBody) acModalBody.innerHTML = '';
        const titleInput = createElement('input', { type: 'text', id: 'c_title', placeholder: 'Content title' });
        const desc = createElement('textarea', { id: 'c_desc', placeholder: 'Description (optional)' });
        const file = createElement('input', { type: 'file', id: 'c_file', multiple: true });
        const fileList = createElement('div', { id: 'c_file_list', className: 'file-list' });
        if (acModalBody) {
            acModalBody.append(
                wrapLabeledField('Content title', titleInput),
                wrapLabeledField('Content description', desc),
                wrapLabeledField('Files', file),
                fileList
            );
        }
        currentFileInput = file;
        // do not reset selected files if already open; re-bind into new input
        if (!wasOpen) {
            currentSelectedFiles = [];
        }
        file.addEventListener('change', () => {
            const newFiles = Array.from(file.files || []);
            const existing = new Set(currentSelectedFiles.map(f => f.name + ':' + f.size));
            newFiles.forEach(f => {
                const key = f.name + ':' + f.size;
                if (!existing.has(key)) {
                    currentSelectedFiles.push(f);
                    existing.add(key);
                }
            });
            const dt = new DataTransfer();
            currentSelectedFiles.forEach(f => dt.items.add(f));
            if (currentFileInput) currentFileInput.files = dt.files;
            renderSelectedFiles();
        });
        // restore previously selected files if switching type
        if (currentSelectedFiles && currentSelectedFiles.length > 0) {
            const dt = new DataTransfer();
            currentSelectedFiles.forEach(f => dt.items.add(f));
            if (currentFileInput) currentFileInput.files = dt.files;
        }
        renderSelectedFiles();

        if (type === 'task' || type === 'assessment') {
            const deadline = createElement('input', { type: 'date', id: 'c_deadline' });
            acModalBody?.appendChild(wrapLabeledField('Deadline', deadline));
        }

        if (type === 'task') {
            const maxGradeInput = createElement('input', { type: 'text', id: 'c_maxgrade', placeholder: 'e.g. 10 or 10/10' });
            acModalBody?.appendChild(wrapLabeledField('Maximum grade', maxGradeInput));
        }

        if (type === 'assessment') {
            const link = createElement('input', {
                type: 'url',
                id: 'c_link',
                placeholder: 'https://… (required)',
                required: true,
                'aria-required': 'true'
            });
            acModalBody?.appendChild(wrapLabeledField('Google Forms link (required)', link));
        }

        if (!wasOpen && acUploadModalBackdrop) {
            acUploadModalBackdrop.removeAttribute('hidden');
            acUploadModalBackdrop.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
        }
        if (acModalSubmit) {
            acModalSubmit.textContent = 'Upload';
            acModalSubmit.dataset.type = type;
        }
        // restore previous values
        try { titleInput.value = prevTitle; } catch {}
        try { desc.value = prevDesc; } catch {}
        const dl = document.getElementById('c_deadline'); if (dl) try { dl.value = prevDeadline; } catch {}
        const ln = document.getElementById('c_link'); if (ln) try { ln.value = prevLink; } catch {}
        const mg = document.getElementById('c_maxgrade'); if (mg) try { mg.value = prevMax; } catch {}
    }

    function createElement(tag, attrs = {}) {
        const el = document.createElement(tag);
        Object.entries(attrs).forEach(([k, v]) => {
            if (k in el) el[k] = v;
            else el.setAttribute(k, v);
        });
        return el;
    }

    function closeModal() {
        if (acUploadModalBackdrop) {
            acUploadModalBackdrop.setAttribute('hidden', '');
            acUploadModalBackdrop.setAttribute('aria-hidden', 'true');
        }
        if (acModalBody) acModalBody.innerHTML = '';
        currentCreateType = null;
        document.body.style.overflow = '';
    }
    function closeDeleteModal() {
        if (deleteBackdrop) {
            deleteBackdrop.setAttribute('hidden', '');
            deleteBackdrop.setAttribute('aria-hidden', 'true');
        }
        if (deleteModal) {
            deleteModal.setAttribute('hidden', '');
            deleteModal.setAttribute('aria-hidden', 'true');
        }
        if (deleteBody) deleteBody.innerHTML = '';
        document.body.style.overflow = '';
    }

    function removeRecentUploadRow(contentId) {
        if (!contentId) return;
        document.querySelectorAll('.recent-upload-row').forEach((row) => {
            if (row.getAttribute('data-content-id') === contentId) row.remove();
        });
    }

    acUploadModalBackdrop?.addEventListener('click', (ev) => {
        if (ev.target === acUploadModalBackdrop) closeModal();
    });
    acModalCloseX?.addEventListener('click', closeModal);
    deleteBackdrop?.addEventListener('click', (ev) => {
        if (ev.target === deleteBackdrop) closeDeleteModal();
    });

    acModalCancel?.addEventListener('click', closeModal);
    deleteCancel?.addEventListener('click', closeDeleteModal);

    // ------------------ CREATE / UPLOAD CONTENT -------------
    acModalSubmit?.addEventListener('click', async () => {
        const t = (acModalSubmit.dataset.type || '').toString();
        const title = (document.getElementById('c_title') || {}).value || '';
        const desc = (document.getElementById('c_desc') || {}).value || '';
        const deadlineVal = (document.getElementById('c_deadline') || {}).value || '';
        const linkVal = (document.getElementById('c_link') || {}).value || '';
        const files = currentSelectedFiles || [];

        if (!title.trim()) {
            showToast('⚠️ Please add a title.', 'warning');
            return;
        }

        if (t === 'assessment') {
            const linkInput = document.getElementById('c_link');
            const raw = (linkVal || '').trim();
            if (!raw) {
                showToast('⚠️ Please add a link for the assessment.', 'warning');
                linkInput?.focus();
                return;
            }
            try {
                void new URL(raw);
            } catch {
                showToast('⚠️ Enter a valid assessment link (e.g. https://…).', 'warning');
                linkInput?.focus();
                return;
            }
        }

        try {
            // Get class code from URL
            const pathParts = window.location.pathname.split('/').filter(part => part);
            const classCode = pathParts[pathParts.length - 1];

            console.log('Using Class Code from URL:', classCode);

            if (!classCode) {
                showToast('❌ Cannot find class code. Please refresh the page.', 'error');
                return;
            }

            // If there are files, upload them first, then create content
            if (files.length > 0) {
                await uploadFilesAndCreateContent(files, t, title, desc, deadlineVal, linkVal, classCode);
            } else {
                // No file, just create content
                await createContentWithoutFile(t, title, desc, deadlineVal, linkVal, classCode);
            }

        } catch (error) {
            console.error('Error creating content:', error);
            showToast('❌ Failed to save content to database: ' + error.message, 'error');
        }
    });
    deleteSubmit?.addEventListener('click', async () => {
        const selected = Array.from(document.querySelectorAll('.content-card.selected-for-delete'));
        if (selected.length === 0) {
            showToast('⚠️ Nothing selected.', 'warning');
            closeDeleteModal();
            return;
        }
        const results = await Promise.all(selected.map(async (card) => {
            const type = Array.from(card.classList).find(c => ['material', 'task', 'assessment', 'meeting', 'announcement'].includes(c)) || '';
            const contentId = card.getAttribute('data-content-id') || '';
            if (!contentId) return { ok: false };
            try {
                let url = '';
                let payload = {};
                if (type === 'material') {
                    url = '/AdminMaterial/DeleteMaterial';
                    payload = { MaterialId: contentId };
                } else if (type === 'task') {
                    url = '/AdminTask/DeleteTask';
                    payload = { TaskId: contentId };
                } else if (type === 'assessment') {
                    url = '/AdminAssessment/DeleteAssessment';
                    payload = { AssessmentId: contentId };
                } else if (type === 'meeting') {
                    url = '/AdminManageClass/DeleteMeeting';
                    payload = { MeetingId: contentId };
                } else if (type === 'announcement') {
                    url = '/AdminClass/DeleteAnnouncement';
                    payload = { ContentId: contentId };
                } else {
                    return { ok: false };
                }
                const res = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                if (!res.ok) return { ok: false };
                const data = await res.json();
                if (data && (data.success === true || data.success === 'true')) {
                    removeRecentUploadRow(contentId);
                    card.remove();
                    return { ok: true };
                }
                return { ok: false };
            } catch {
                return { ok: false };
            }
        }));
        const okCount = results.filter(r => r.ok).length;
        const failCount = results.length - okCount;
        if (okCount > 0) showToast(`🗑️ Deleted ${okCount} item(s).`, 'success');
        if (failCount > 0) showToast(`❌ Failed to delete ${failCount} item(s).`, 'error');
        closeDeleteModal();
        setBaseState();
    });

    async function uploadFilesAndCreateContent(files, type, title, desc, deadlineVal, linkVal, classCode) {
        const uploadedNames = [];
        for (const file of files) {
            const fileFormData = new FormData();
            fileFormData.append('file', file);
            fileFormData.append('classCode', classCode);
            fileFormData.append('type', type);
            const uploadResponse = await fetch('/AdminClass/UploadFile', {
                method: 'POST',
                body: fileFormData
            });
            if (!uploadResponse.ok) {
                const errorText = await uploadResponse.text();
                throw new Error(`Failed to upload file: ${uploadResponse.status} ${errorText}`);
            }
            const uploadResult = await uploadResponse.json();
            if (!uploadResult.success) {
                throw new Error(uploadResult.message);
            }
            uploadedNames.push(file.name);
        }

        const contentData = {
            type: type,
            title: title,
            description: desc,
            deadline: deadlineVal,
            link: linkVal,
            classId: classCode,
            fileNames: uploadedNames,
            maxGrade: type === 'task' ? (function(){
                const raw = ((document.getElementById('c_maxgrade') || {}).value || '100').trim();
                const num = (raw.includes('/') ? raw.split('/')[0].trim() : raw);
                const parsed = parseInt(num, 10);
                return isNaN(parsed) ? 100 : parsed;
            })() : 0
        };
        await createContentInDatabase(contentData);
    }

    async function createContentWithoutFile(type, title, desc, deadlineVal, linkVal, classCode) {
        const contentData = {
            type: type,
            title: title,
            description: desc,
            deadline: deadlineVal,
            link: linkVal,
            classId: classCode,
            maxGrade: type === 'task' ? (function(){
                const raw = ((document.getElementById('c_maxgrade') || {}).value || '100').trim();
                const num = (raw.includes('/') ? raw.split('/')[0].trim() : raw);
                const parsed = parseInt(num, 10);
                return isNaN(parsed) ? 100 : parsed;
            })() : 0
        };

        await createContentInDatabase(contentData);
    }

    async function createContentInDatabase(contentData) {
        console.log('Sending content data:', contentData);

        const response = await fetch('/AdminClass/CreateContent', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(contentData)
        });

        console.log('Response status:', response.status);

        if (!response.ok) {
            const errorText = await response.text();
            console.error('Server response:', errorText);
            throw new Error(`Failed to save content: ${response.status} ${errorText}`);
        }

        const savedContent = await response.json();
        console.log('Saved content:', savedContent);

        showToast('✅ Content created and saved.', 'success');
        closeModal();

        // Refresh the page to show the new content
        setTimeout(() => {
            window.location.reload();
        }, 1000);
    }

    // ------------------ ANNOUNCEMENTS (slsp-confirm UI, same pattern as sign out) ------------------------
    function openAnnouncePostConfirm() {
        if (announcePostConfirmBackdrop) {
            announcePostConfirmBackdrop.removeAttribute('hidden');
            announcePostConfirmBackdrop.setAttribute('aria-hidden', 'false');
        }
        if (announcePostConfirmModal) {
            announcePostConfirmModal.removeAttribute('hidden');
            announcePostConfirmModal.setAttribute('aria-hidden', 'false');
        }
        document.body.style.overflow = 'hidden';
        announcePostConfirmCancel?.focus();
    }

    function closeAnnouncePostConfirm() {
        if (announcePostConfirmBackdrop) {
            announcePostConfirmBackdrop.setAttribute('hidden', '');
            announcePostConfirmBackdrop.setAttribute('aria-hidden', 'true');
        }
        if (announcePostConfirmModal) {
            announcePostConfirmModal.setAttribute('hidden', '');
            announcePostConfirmModal.setAttribute('aria-hidden', 'true');
        }
        document.body.style.overflow = '';
    }

    function isAnnouncePostConfirmOpen() {
        return announcePostConfirmModal && !announcePostConfirmModal.hasAttribute('hidden');
    }

    async function submitAnnouncementPost() {
        const txt = announcementInput.value.trim();
        if (!txt) {
            showToast('⚠️ Please type an announcement.', 'warning');
            return;
        }

        const pathParts = window.location.pathname.split('/').filter(part => part);
        const classCode = pathParts[pathParts.length - 1];

        if (!classCode) {
            showToast('❌ Cannot find class code for announcement.', 'error');
            return;
        }

        const res = await fetch('/AdminClass/AddAnnouncement', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                classId: classCode,
                text: txt
            })
        });

        if (!res.ok) {
            const errorText = await res.text();
            throw new Error(`Failed to save announcement: ${res.status} ${errorText}`);
        }

        await res.json();

        showToast('📢 Announcement posted.', 'success');
        collapseAnnouncement();

        setTimeout(() => {
            window.location.reload();
        }, 600);
    }

    announceBtn?.addEventListener('click', () => {
        const txt = announcementInput.value.trim();
        if (!txt) {
            showToast('⚠️ Please type an announcement.', 'warning');
            return;
        }
        if (!announcePostConfirmModal) {
            void submitAnnouncementPost();
            return;
        }
        openAnnouncePostConfirm();
    });

    announcePostConfirmCancel?.addEventListener('click', closeAnnouncePostConfirm);
    announcePostConfirmBackdrop?.addEventListener('click', (e) => {
        if (e.target === announcePostConfirmBackdrop) closeAnnouncePostConfirm();
    });
    announcePostConfirmOk?.addEventListener('click', async () => {
        closeAnnouncePostConfirm();
        try {
            await submitAnnouncementPost();
        } catch (err) {
            console.error(err);
            showToast('❌ Could not save announcement: ' + err.message, 'error');
        }
    });

    function createContentCard(type, title, desc, deadline, link) {
        const card = createElement('article', { className: `content-card ${type}` });
        const left = createElement('div', { className: 'content-left' });
        const icon = createElement('i', {
            className: {
                material: 'fa-solid fa-book-open-reader',
                task: 'fa-solid fa-file-pen',
                assessment: 'fa-solid fa-circle-question',
                meeting: 'fa-solid fa-chalkboard-user'
            }[type] || 'fa-solid fa-file'
        });
        left.appendChild(icon);

        const info = createElement('div', { className: 'content-info' });
        const h = createElement('h4', { className: 'content-title', textContent: title });
        const meta = createElement('p', { className: 'content-meta', textContent: generateMeta(desc, deadline, link) });
        info.append(h, meta);
        left.appendChild(info);
        card.appendChild(left);

        if ((type === 'task' || type === 'assessment') && deadline) {
            const rightFlag = createElement('div', { className: 'content-right urgency' });
            const days = diffDays(new Date(), new Date(deadline));
            if (days <= 2) rightFlag.classList.add('red');
            else if (days <= 7) rightFlag.classList.add('yellow');
            else rightFlag.classList.add('green');
            card.appendChild(rightFlag);
        }

        // give new card same click behavior
        card.addEventListener('click', () => {
            const targetPage = card.dataset.target;
            if (!targetPage) return;
            if (String(type).toLowerCase() === 'meeting') {
                openMeetingInNewTab(targetPage);
                return;
            }
            const msg = openingMessageForContentType(type);
            if (typeof window.navigateWithProfessorLoading === 'function') {
                window.navigateWithProfessorLoading(targetPage, msg, 600);
            } else {
                showToast(msg);
                if (typeof window.setProfessorDbShellLoading === 'function') window.setProfessorDbShellLoading(true);
                setTimeout(() => { window.location.href = targetPage; }, 600);
            }
        });

        return card;
    }

    function generateMeta(desc, deadline, link) {
        let text = `Posted: ${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`;
        if (deadline) text += ` | Deadline: ${new Date(deadline).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`;
        return text;
    }

    function insertCard(card) {
        const firstInsert = document.querySelector('.create-announcement');
        if (firstInsert && firstInsert.parentNode) firstInsert.parentNode.insertBefore(card, firstInsert.nextSibling);
        else classContent.prepend(card);
    }

    function openingMessageForContentType(type) {
        const map = {
            material: 'Opening Material',
            task: 'Opening Task',
            assessment: 'Opening Assessment',
            meeting: 'Opening Meeting'
        };
        return map[type] || 'Opening...';
    }

    function openMeetingInNewTab(url) {
        if (!url) return;
        const w = window.open(url, '_blank', 'noopener,noreferrer');
        if (!w) showToast('Allow pop-ups to open the meeting.', 'warning');
    }

    function isExternalHttpUrl(url) {
        if (!url || typeof url !== 'string') return false;
        try {
            const u = new URL(url, window.location.href);
            if (u.protocol !== 'http:' && u.protocol !== 'https:') return false;
            return u.origin !== window.location.origin;
        } catch (_) {
            return false;
        }
    }

    function openingMessageForContentCard(card) {
        if (card.classList.contains('material')) return 'Opening Material';
        if (card.classList.contains('task')) return 'Opening Task';
        if (card.classList.contains('assessment')) return 'Opening Assessment';
        if (card.classList.contains('meeting')) return 'Opening Meeting';
        const t = card.dataset.target;
        if (typeof window.resolveAdminNavToastMessage === 'function' && t) {
            return window.resolveAdminNavToastMessage(t);
        }
        return 'Opening...';
    }

    // ------------------ CONTENT-CARD NAVIGATION --------------
    const contentCards = document.querySelectorAll('.content-card:not(.create-announcement)');
    contentCards.forEach(card => {
        const isAnnouncement = card.classList.contains('announcement');
        card.style.cursor = isAnnouncement ? 'default' : 'pointer';
        card.addEventListener('click', () => {
            if (radialMode === 'delete') {
                card.classList.toggle('selected-for-delete');
                return;
            }
            if (isAnnouncement) return;
            const targetPage = card.dataset.target;
            if (!targetPage) return;
            const typeClass = Array.from(card.classList).find((c) =>
                ['material', 'task', 'assessment', 'meeting', 'announcement'].includes(c.toLowerCase())
            );
            if (typeClass && typeClass.toLowerCase() === 'meeting') {
                openMeetingInNewTab(targetPage);
                return;
            }
            const msg = openingMessageForContentCard(card);
            if (typeof window.navigateWithProfessorLoading === 'function') {
                window.navigateWithProfessorLoading(targetPage, msg, 600);
            } else {
                showToast(msg);
                if (typeof window.setProfessorDbShellLoading === 'function') window.setProfessorDbShellLoading(true);
                setTimeout(() => { window.location.href = targetPage; }, 600);
            }
        });
    });

    // ------------------ RECENT UPLOADS NAVIGATION -------------
    // Convert default anchor navigation into "loading overlay + delayed navigate" so it matches ProfessorDb UX.
    document.querySelectorAll('.recent-uploads a.recent-upload-link').forEach((a) => {
        a.addEventListener('click', (e) => {
            if (radialMode === 'delete') return; // keep delete mode behavior consistent
            const href = a.getAttribute('href');
            if (!href || href === '#') return;
            if (isExternalHttpUrl(href)) {
                e.preventDefault();
                e.stopPropagation();
                openMeetingInNewTab(href);
                return;
            }
            e.preventDefault();
            e.stopPropagation();
            const msg = typeof window.resolveAdminNavToastMessage === 'function'
                ? window.resolveAdminNavToastMessage(href)
                : 'Opening...';
            if (typeof window.navigateWithProfessorLoading === 'function') {
                window.navigateWithProfessorLoading(href, msg, 600);
            } else {
                showToast(msg);
                if (typeof window.setProfessorDbShellLoading === 'function') window.setProfessorDbShellLoading(true);
                setTimeout(() => { window.location.href = href; }, 600);
            }
        });
    });

    function enableDeleteSelection() {
        // Visual feedback handled by click handler above
    }
    function clearDeleteSelection() {
        document.querySelectorAll('.content-card.selected-for-delete').forEach(c => c.classList.remove('selected-for-delete'));
    }

    function openDeleteConfirmModal() {
        const selected = Array.from(document.querySelectorAll('.content-card.selected-for-delete'));
        if (selected.length === 0) {
            showToast('⚠️ Select content to delete.', 'warning');
            return;
        }
        deleteBody.innerHTML = '';
        const summary = document.createElement('div');
        summary.className = 'modal-delete-summary';
        summary.textContent = `You are about to delete ${selected.length} item${selected.length > 1 ? 's' : ''}. This cannot be undone.`;
        deleteBody.append(summary);

        const table = document.createElement('table');
        table.className = 'modal-delete-table';
        const thead = document.createElement('thead');
        const headRow = document.createElement('tr');
        ['#', 'Content name', 'Type'].forEach(text => {
            const th = document.createElement('th');
            th.textContent = text;
            headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        const tbody = document.createElement('tbody');
        selected.forEach((card, idx) => {
            const row = document.createElement('tr');
            const numCell = document.createElement('td');
            numCell.textContent = String(idx + 1);
            const nameCell = document.createElement('td');
            const nameSpan = document.createElement('span');
            nameSpan.className = 'cell-ellipsis';
            nameSpan.textContent = card.querySelector('.content-title')?.textContent || 'Untitled';
            nameCell.appendChild(nameSpan);
            const typeCell = document.createElement('td');
            const type = Array.from(card.classList).find(c => ['material', 'task', 'assessment', 'meeting', 'announcement'].includes(c)) || 'content';
            const typeBadge = document.createElement('span');
            typeBadge.className = `delete-type-badge type-${String(type).toLowerCase()}`;
            typeBadge.textContent = type.charAt(0).toUpperCase() + type.slice(1).toLowerCase();
            typeCell.appendChild(typeBadge);
            row.append(numCell, nameCell, typeCell);
            tbody.appendChild(row);
        });
        table.append(thead, tbody);
        deleteBody.append(table);
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
    // ------------------ ANNOUNCEMENTS ------------------------
    const ANNOUNCEMENT_TEXTAREA_MIN_PX = 40;
    function autoResize(el) {
        el.style.height = 'auto';
        el.style.height = Math.max(ANNOUNCEMENT_TEXTAREA_MIN_PX, el.scrollHeight) + 'px';
    }
    function expandAnnouncement() {
        announcementCard.classList.add('active');
        announcementCard.querySelector('.announcement-actions').hidden = false;
        autoResize(announcementInput);
        announcementInput.focus();
    }
    function collapseAnnouncement() {
        announcementCard.classList.remove('active');
        announcementCard.querySelector('.announcement-actions').hidden = true;
        announcementInput.style.height = 'auto';
        announcementInput.value = '';
    }

    announcementInput?.addEventListener('focus', expandAnnouncement);
    announcementInput?.addEventListener('click', expandAnnouncement);
    announcementInput?.addEventListener('input', () => autoResize(announcementInput));

    // ------------------ GLOBAL ESC HANDLING ------------------
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            if (deleteModal && !deleteModal.hasAttribute('hidden')) {
                closeDeleteModal();
                return;
            }
            if (acUploadModalBackdrop && !acUploadModalBackdrop.hasAttribute('hidden')) {
                closeModal();
                return;
            }
            if (isAnnouncePostConfirmOpen()) {
                closeAnnouncePostConfirm();
                return;
            }
            radialActions?.classList.remove('show');
            menuOpen = false;
            setBaseState();
        }
    });

    // expose minimal API for tests
    window.__AdminClassAPI = {
        setBaseState,
        setDeleteState,
        getState: () => ({ radialMode, isAction2Active }),
        openContentModal,
        closeModal
    };

    // ------------------ BACK BUTTON ---------------------------
    backButton?.addEventListener('click', () => {
        const dash = typeof window.resolveInstructorDashboardUrl === 'function' ? window.resolveInstructorDashboardUrl() : '/professordb/ProfessorDb';
        const msg = typeof window.resolveAdminNavToastMessage === 'function'
            ? window.resolveAdminNavToastMessage(dash)
            : 'Redirecting to Dashboard';
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading(dash, msg, 600);
        } else {
            if (window.showToast) window.showToast(msg);
            if (typeof window.setProfessorDbShellLoading === 'function') window.setProfessorDbShellLoading(true);
            setTimeout(() => { window.location.href = dash; }, 600);
        }
    });

    // ------------------ MANAGE BUTTONS -------------------------
    manageButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetPage = btn.getAttribute('data-target');
            if (!targetPage) return;
            const msg = btn.classList.contains('manage-class')
                ? 'Opening Class Management'
                : 'Opening page...';
            if (typeof window.navigateWithProfessorLoading === 'function') {
                window.navigateWithProfessorLoading(targetPage, msg, 600);
            } else {
                showToast(msg);
                if (typeof window.setProfessorDbShellLoading === 'function') window.setProfessorDbShellLoading(true);
                setTimeout(() => { window.location.href = targetPage; }, 600);
            }
        });
    });

    // ------------------ TOAST --------------------------------
    function showToast(message, type = '') {
        if (!toast) return;
        toast.textContent = message;
        toast.className = `toast show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 2800);
    }

    // ------------------ HELPERS ------------------------------
    function diffDays(d1, d2) {
        const one = 24 * 60 * 60 * 1000;
        return Math.round((+d2 - +d1) / one);
    }

});


