document.addEventListener('DOMContentLoaded', () => {

    // ------------------ ELEMENT REFERENCES -------------------
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const addContentBtn = document.getElementById('addContentBtn');
    const contentRadial = document.getElementById('contentRadial');
    const contentActions = contentRadial ? Array.from(contentRadial.querySelectorAll('.content-action')) : [];
    const contentTooltip = contentRadial ? contentRadial.querySelector('.content-tooltip') : null;
    const modalBackdrop = document.getElementById('modalBackdrop');
    const contentModal = document.getElementById('contentModal');
    const modalBody = document.getElementById('modalBody');
    const modalSubmit = document.getElementById('modalSubmit');
    const modalCancel = document.getElementById('modalCancel');
    const deleteBackdrop = document.getElementById('deleteBackdrop');
    const deleteModal = document.getElementById('deleteModal');
    const deleteBody = document.getElementById('deleteBody');
    const deleteSubmit = document.getElementById('deleteSubmit');
    const deleteCancel = document.getElementById('deleteCancel');
    const sideModal = document.getElementById('contentSideModal');
    const sideModalTitle = document.getElementById('sideModalTitle');
    const sideModalBody = document.getElementById('sideModalBody');
    const sideModalSubmit = document.getElementById('sideModalSubmit');
    const sideModalCancel = document.getElementById('sideModalCancel');
    const classContent = document.getElementById('classContent');
    const toast = document.getElementById('toast');
    const announcementCard = document.getElementById('createAnnouncementCard');
    const announcementInput = document.getElementById('announcementInput');
    const announceBtn = document.getElementById('announceBtn');
    const backButton = document.querySelector('.back-button');
    const manageButtons = document.querySelectorAll('.manage-btn');
    const mobileToggleBtn = document.getElementById('mobileToggleBtn');
    const classInfo = document.querySelector('.class-info');

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
                radialActions.classList.remove('show');
                menuOpen = false;
                return;
            }

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

    function navigateWithAnimation(url, message) {
        showToast(message);
        radialActions?.classList.remove('show');
        menuOpen = false;
        setTimeout(() => { window.location.href = url; }, 600);
    }

    // ------------------ CONTENT RADIAL -----------------------
    let contentRadialOpen = false;
    let radialMode = 'base'; // base | upload | delete
    let isAction2Active = false; // delete mode gate for outside-click collapse
    const uploadTrigger = contentRadial ? contentRadial.querySelector('.content-radial-btn.upload') : null;
    const deleteTrigger = contentRadial ? contentRadial.querySelector('.content-radial-btn.delete') : null;
    const confirmBtn = contentRadial ? contentRadial.querySelector('.content-action.confirm') : null;
    const cancelBtn = contentRadial ? contentRadial.querySelector('.content-action.cancel') : null;
    const materialBtn = contentRadial ? contentRadial.querySelector('.content-action.material') : null;
    const taskBtn = contentRadial ? contentRadial.querySelector('.content-action.task') : null;
    const assessmentBtn = contentRadial ? contentRadial.querySelector('.content-action.assessment') : null;
    addContentBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        // Always dismiss upload modal when clicking add-content-button
        const modalOpen = modalBackdrop && modalBackdrop.getAttribute('aria-hidden') === 'false';
        if (modalOpen) {
            closeModal();
            setBaseState();
            contentRadialOpen = true;
            contentRadial?.classList.add('show');
            addContentBtn?.classList.add('selected');
            return;
        }
        // Default toggle behavior
        contentRadialOpen = !contentRadialOpen;
        contentRadial?.classList.toggle('show', contentRadialOpen);
        addContentBtn?.classList.toggle('selected', contentRadialOpen);
        if (!contentRadialOpen) {
            closeModal();
            setBaseState();
        } else {
            // Entering base open state; user can then choose upload or delete
            setBaseState();
        }
    });
    document.addEventListener('click', (e) => {
        const modalOpen = modalBackdrop && modalBackdrop.getAttribute('aria-hidden') === 'false';
        const deleteOpen = deleteBackdrop && deleteBackdrop.getAttribute('aria-hidden') === 'false';
        const sideOpen = sideModal && sideModal.getAttribute('aria-hidden') === 'false';
        const clickedInsideRadial = contentRadial && contentRadial.contains(e.target);
        const clickedAddButton = e.target === addContentBtn;
        const clickedInsideModal = (contentModal && contentModal.contains(e.target)) || (deleteModal && deleteModal.contains(e.target));
        // suspend outside-click collapse while in delete (Action 2)
        if (isAction2Active) return;
        if (!clickedInsideRadial && !clickedAddButton && !clickedInsideModal && !modalOpen && !deleteOpen && !sideOpen) {
            contentRadialOpen = false;
            contentRadial?.classList.remove('show');
            addContentBtn?.classList.remove('selected');
        }
    });

    contentActions.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            if (btn.dataset.type) {
                openContentModal(btn.dataset.type);
                return;
            }
            if (btn.dataset.action === 'confirm-delete') {
                openDeleteConfirmModal();
                return;
            }
            if (btn.dataset.action === 'cancel-delete') {
                setBaseState();
                return;
            }
        });
        btn.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                btn.click();
            }
        });
    });

    uploadTrigger?.addEventListener('click', (e) => {
        e.stopPropagation();
        if (radialMode === 'upload') {
            setBaseState();
        } else {
            setUploadState();
        }
    });
    deleteTrigger?.addEventListener('click', (e) => {
        e.stopPropagation();
        setDeleteState();
    });

    function setBaseState() {
        radialMode = 'base';
        isAction2Active = false;
        contentTooltip && (contentTooltip.textContent = 'what would you like to do?');
        uploadTrigger?.classList.remove('selected');
        deleteTrigger?.classList.remove('selected');
        toggleElement(confirmBtn, false);
        toggleElement(cancelBtn, false);
        toggleElement(materialBtn, false);
        toggleElement(taskBtn, false);
        toggleElement(assessmentBtn, false);
        toggleElement(uploadTrigger, true);
        toggleElement(deleteTrigger, true);
        clearDeleteSelection();
    }
    function setUploadState() {
        radialMode = 'upload';
        isAction2Active = false;
        contentTooltip && (contentTooltip.textContent = 'what would you like to upload?');
        uploadTrigger?.classList.add('selected');
        deleteTrigger?.classList.remove('selected');
        toggleElement(confirmBtn, false);
        toggleElement(cancelBtn, false);
        toggleElement(materialBtn, true);
        toggleElement(taskBtn, true);
        toggleElement(assessmentBtn, true);
        toggleElement(uploadTrigger, true);
        toggleElement(deleteTrigger, false);
    }
    function setDeleteState() {
        radialMode = 'delete';
        isAction2Active = true;
        contentTooltip && (contentTooltip.textContent = 'delete content?');
        deleteTrigger?.classList.add('selected');
        uploadTrigger?.classList.remove('selected');
        toggleElement(confirmBtn, true);
        toggleElement(cancelBtn, true);
        toggleElement(materialBtn, false);
        toggleElement(taskBtn, false);
        toggleElement(assessmentBtn, false);
        toggleElement(uploadTrigger, true);
        toggleElement(deleteTrigger, true);
        enableDeleteSelection();
    }
    function toggleElement(el, show) {
        if (!el) return;
        el.style.display = show ? 'flex' : 'none';
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

    function openContentModal(type) {
        // keep radial open and in upload mode while switching types
        radialMode = 'upload';
        contentRadialOpen = true;
        contentRadial?.classList.add('show');
        addContentBtn?.classList.add('selected');
        const wasOpen = (modalBackdrop && modalBackdrop.getAttribute('aria-hidden') === 'false');
        currentCreateType = type;
        const mt = document.getElementById('modalTitle');
        mt.style.display = '';
        mt.textContent = {
            material: 'Upload Material',
            task: 'Upload Task',
            assessment: 'Upload Assessment'
        }[type] || 'Upload';

        // preserve existing input values when switching type
        const prevTitle = (document.getElementById('c_title') || {}).value || '';
        const prevDesc = (document.getElementById('c_desc') || {}).value || '';
        const prevDeadline = (document.getElementById('c_deadline') || {}).value || '';
        const prevLink = (document.getElementById('c_link') || {}).value || '';
        const prevMax = (document.getElementById('c_maxgrade') || {}).value || '';
        modalBody.innerHTML = '';
        const titleInput = createElement('input', { type: 'text', id: 'c_title', placeholder: 'Content title' });
        const desc = createElement('textarea', { id: 'c_desc', placeholder: 'Content description (optional)' });
        const file = createElement('input', { type: 'file', id: 'c_file', multiple: true });
        const fileList = createElement('div', { id: 'c_file_list', className: 'file-list' });
        modalBody.append(titleInput, desc, file, fileList);
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
            modalBody.appendChild(deadline);
        }

        if (type === 'task') {
            const maxGradeLabel = createElement('label', { htmlFor: 'c_maxgrade' });
            maxGradeLabel.textContent = 'Maximum grade';
            const maxGradeInput = createElement('input', { type: 'text', id: 'c_maxgrade', placeholder: 'Enter max grade (e.g., 10 or 10/10)' });
            modalBody.append(maxGradeLabel, maxGradeInput);
        }

        if (type === 'assessment') {
            const link = createElement('input', { type: 'url', id: 'c_link', placeholder: 'Link to assessment (optional)' });
            modalBody.appendChild(link);
        }

        // Show legacy upload modal near radial; use transparent backdrop
        if (!wasOpen) {
            modalBackdrop.hidden = false;
            modalBackdrop.style.display = 'flex';
            modalBackdrop.classList.add('transparent');
            modalBackdrop.setAttribute('aria-hidden', 'false');
            contentModal.classList.add('create-content');
        }
        modalSubmit.textContent = 'Upload';
        modalSubmit.dataset.type = type;
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
        modalBackdrop.hidden = true;
        modalBackdrop.style.display = 'none';
        modalBackdrop.setAttribute('aria-hidden', 'true');
        modalBackdrop.classList.remove('transparent');
        modalBody.innerHTML = '';
        currentCreateType = null;
        contentModal.classList.remove('create-content');
    }
    function closeSideModal() { /* no-op (legacy modal in use) */ }
    function closeDeleteModal() {
        deleteBackdrop.hidden = true;
        deleteBackdrop.style.display = 'none';
        deleteBackdrop.setAttribute('aria-hidden', 'true');
        deleteBody.innerHTML = '';
    }

    modalBackdrop?.addEventListener('click', (ev) => {
        if (ev.target === modalBackdrop) closeModal();
    });
    deleteBackdrop?.addEventListener('click', (ev) => {
        if (ev.target === deleteBackdrop) closeDeleteModal();
    });

    modalCancel?.addEventListener('click', closeModal);
    sideModalCancel?.addEventListener('click', closeSideModal);
    deleteCancel?.addEventListener('click', closeDeleteModal);

    // ------------------ CREATE / UPLOAD CONTENT -------------
    modalSubmit?.addEventListener('click', async () => {
        const t = (modalSubmit.dataset.type || '').toString();
        const title = (document.getElementById('c_title') || {}).value || '';
        const desc = (document.getElementById('c_desc') || {}).value || '';
        const deadlineVal = (document.getElementById('c_deadline') || {}).value || '';
        const linkVal = (document.getElementById('c_link') || {}).value || '';
        const files = currentSelectedFiles || [];

        if (!title.trim()) {
            showToast('⚠️ Please add a title.', 'warning');
            return;
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
        closeModal();
    });
    deleteSubmit?.addEventListener('click', async () => {
        const selected = Array.from(document.querySelectorAll('.content-card.selected-for-delete'));
        if (selected.length === 0) {
            showToast('⚠️ Nothing selected.', 'warning');
            closeDeleteModal();
            return;
        }
        const results = await Promise.all(selected.map(async (card) => {
            const type = Array.from(card.classList).find(c => ['material', 'task', 'assessment', 'meeting'].includes(c)) || '';
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

    // ------------------ ANNOUNCEMENTS ------------------------
    announceBtn?.addEventListener('click', async () => {
        const txt = announcementInput.value.trim();
        if (!txt) { showToast('⚠️ Please type an announcement.', 'warning'); return; }

        try {
            // FIX: Get class code from URL (same method as above)
            const pathParts = window.location.pathname.split('/').filter(part => part);
            const classCode = pathParts[pathParts.length - 1];

            console.log('Creating announcement for class:', classCode);

            if (!classCode) {
                showToast('❌ Cannot find class code for announcement.', 'error');
                return;
            }

            const res = await fetch('/AdminClass/AddAnnouncement', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    classId: classCode,  // Send Class Code, not Class ID
                    text: txt
                })
            });

            console.log('Announcement response status:', res.status);

            if (!res.ok) {
                const errorText = await res.text();
                throw new Error(`Failed to save announcement: ${res.status} ${errorText}`);
            }

            const data = await res.json();
            console.log('Announcement saved:', data);

            showToast('📢 Announcement posted.', 'success');
            collapseAnnouncement();

            // Refresh to show the new announcement
            setTimeout(() => {
                window.location.reload();
            }, 1000);

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
                assessment: 'fa-solid fa-circle-question'
            }[type]
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
            showToast('Opening...');
            navigateWithDelay(targetPage);
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

    // ------------------ CONTENT-CARD NAVIGATION --------------
    const contentCards = document.querySelectorAll('.content-card');
    contentCards.forEach(card => {
        card.style.cursor = 'pointer';
        card.addEventListener('click', () => {
            if (radialMode === 'delete') {
                card.classList.toggle('selected-for-delete');
                return;
            }
            const targetPage = card.dataset.target;
            if (!targetPage) return;
            showToast('Opening...');
            navigateWithDelay(targetPage);
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
            typeCell.textContent = type.toUpperCase();
            row.append(numCell, nameCell, typeCell);
            tbody.appendChild(row);
        });
        table.append(thead, tbody);
        deleteBody.append(table);
        deleteBackdrop.hidden = false;
        deleteBackdrop.style.display = 'flex';
        deleteBackdrop.setAttribute('aria-hidden', 'false');
        // keep radial visible during Action 2 operations
        contentRadialOpen = true;
        contentRadial?.classList.add('show');
        addContentBtn?.classList.add('selected');
    }
    // ------------------ ANNOUNCEMENTS ------------------------
    function autoResize(el) { el.style.height = 'auto'; el.style.height = (el.scrollHeight) + 'px'; }
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

    announceBtn?.addEventListener('click', async () => {
        const txt = announcementInput.value.trim();
        if (!txt) { showToast('⚠️ Please type an announcement.', 'warning'); return; }

        try {
            // FIX: Use the same method to get classId
            let classId = null;
            const hiddenClassId = document.querySelector('input[name="classId"], input[id="classId"]');
            if (hiddenClassId) {
                classId = hiddenClassId.value;
            }
            if (!classId && document.body.dataset.classId) {
                classId = document.body.dataset.classId;
            }
            if (!classId) {
                const classContentElement = document.getElementById('classContent');
                classId = classContentElement?.dataset.classId;
            }

            console.log('Creating announcement for class:', classId);

            if (!classId) {
                showToast('❌ Cannot find class ID for announcement.', 'error');
                return;
            }

            const res = await fetch('/AdminClass/AddAnnouncement', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ classId, text: txt })
            });

            console.log('Announcement response status:', res.status);

            if (!res.ok) {
                const errorText = await res.text();
                throw new Error(`Failed to save announcement: ${res.status} ${errorText}`);
            }

            const data = await res.json();
            console.log('Announcement saved:', data);

            // Safely get values
            const description = data.Description || txt;
            const uploadedBy = data.UploadedBy || 'Admin';
            const createdAt = data.CreatedAt ? new Date(data.CreatedAt) : new Date();

            const card = document.createElement('article');
            card.className = 'content-card announcement';
            card.innerHTML = `
    <div class="content-left">
        <i class="fa-solid fa-bullhorn"></i>
        <div class="content-info">
            <h4 class="content-title">Announcement</h4>
            <p class="content-meta">${description}</p>
            <p class="content-meta-small">By ${uploadedBy} | ${createdAt.toLocaleString()}</p>
        </div>
    </div>`;

            insertCard(card);

            showToast('📢 Announcement posted.', 'success');
            collapseAnnouncement();
        } catch (err) {
            console.error(err);
            showToast('❌ Could not save announcement: ' + err.message, 'error');
        }
    });

    // ------------------ GLOBAL ESC HANDLING ------------------
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeModal();
            radialActions?.classList.remove('show');
            if (!isAction2Active) {
                contentRadial?.classList.remove('show');
                addContentBtn?.classList.remove('selected');
                contentRadialOpen = false;
            }
            menuOpen = false;
            setBaseState();
        }
    });

    // expose minimal API for tests
    window.__AdminClassAPI = {
        setBaseState,
        setDeleteState,
        getState: () => ({ radialMode, isAction2Active, contentRadialOpen }),
        contentRadial,
        addContentBtn
    };

    // ------------------ BACK BUTTON ---------------------------
    backButton?.addEventListener('click', () => {
        window.showToast ? window.showToast('Returning...') : null;
        const target = '/professordb/ProfessorDb';
        setTimeout(() => { window.location.href = target; }, 600);
    });

    // ------------------ MANAGE BUTTONS -------------------------
    manageButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetPage = btn.getAttribute('data-target');
            if (!targetPage) return;

            if (btn.classList.contains('manage-class')) {
                showToast('🧑‍🏫 Opening Manage Class...');
            } else {
                showToast('Opening page...');
            }

            setTimeout(() => {
                window.location.href = targetPage;
            }, 600);
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

    function navigateWithDelay(url, delay = 600) {
        setTimeout(() => window.location.href = url, delay);
    }

});

