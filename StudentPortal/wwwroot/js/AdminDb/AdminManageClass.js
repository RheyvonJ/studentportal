// Verify script is loaded
console.log('AdminManageClass.js loaded');

const studentListContainer = document.getElementById("student-list");
const studentCountEl = document.getElementById("studentCount");
const classSearchInput = document.getElementById("classSearch");
let studentsData = [];
const joinRequestContainer = document.getElementById("join-request-list") || document.getElementById("requestList");
const exportBtn = document.getElementById("export-btn");
const attendanceBtn = document.getElementById("attendance-btn");
const createMeetBtn = document.getElementById("create-meet-btn");
const joinMeetBtn = document.getElementById("join-meet-btn");
const toast = document.getElementById("toast");
const backButton = document.querySelector('.back-button');
const focusModal = document.getElementById('focus-modal');
const focusContent = document.querySelector('#focus-modal .focus-modal-content');
const closeFocusModal = document.getElementById('close-focus-modal');
let studentMenuEl = null;
let attendanceMenuEl = null;
const seatingGridEl = document.getElementById('seating-grid');
const seatRowsInput = document.getElementById('seat-rows');
const seatColsInput = document.getElementById('seat-cols');
const focusProgress = document.getElementById('focus-progress');
const progressText = document.getElementById('progress-text');
const progressFill = document.getElementById('progress-fill');
const focusPhoto = document.getElementById('focus-photo');
const focusInitials = document.getElementById('focus-initials');
const focusName = document.getElementById('focus-name');
const btnAbsent = document.getElementById('mark-absent');
const btnLate = document.getElementById('mark-late');
const btnPresent = document.getElementById('mark-present');
const meetModal = document.getElementById('meet-modal');
const closeMeetModal = document.getElementById('close-meet-modal');
const meetTitleInput = document.getElementById('meet-title');
const meetScheduleInput = document.getElementById('meet-schedule');
const confirmMeetBtn = document.getElementById('confirm-meet');
const cancelMeetBtn = document.getElementById('cancel-meet');
const manageMeetingModal = document.getElementById('manage-meeting-modal');
const closeManageMeetingModal = document.getElementById('close-manage-meeting-modal');
const meetingDurationValue = document.getElementById('meeting-duration-value');
const manageMeetingJoinBtn = document.getElementById('manage-meeting-join-btn');
const manageMeetingStopBtn = document.getElementById('manage-meeting-stop-btn');
let focusIndex = -1;
let meetingDurationInterval = null;
let currentMeetingState = { meetingId: '', joinUrl: '', startTime: null };

function clamp(n, min, max) { n = parseInt(n, 10) || 0; return Math.max(min, Math.min(max, n)); }

function getGridSize() {
    const classCode = document.body.dataset.classCode || '';
    const lsRows = localStorage.getItem(`seatRows_${classCode}`);
    const lsCols = localStorage.getItem(`seatCols_${classCode}`);
    const rows = clamp(seatRowsInput?.value || lsRows || 4, 1, 20);
    const cols = clamp(seatColsInput?.value || lsCols || 6, 1, 20);
    return { rows, cols };
}

function initialsFrom(name) {
    const parts = (name || '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '';
    const first = parts[0]?.slice(0,1) || '';
    const last = parts.length > 1 ? parts[parts.length - 1].slice(0,1) : '';
    return (first + last).toUpperCase();
}

function renderSeatGridFromData() {
    if (!seatingGridEl) return;
    const { rows, cols } = getGridSize();
    seatingGridEl.style.setProperty('--seat-cols', cols);
    const total = rows * cols;
    seatingGridEl.innerHTML = '';
    const frag = document.createDocumentFragment();
    for (let i = 0; i < total; i++) {
        const seat = document.createElement('div');
        seat.className = 'seat';
        const student = studentsData[i];
        if (student) {
            seat.classList.add('filled');
            seat.setAttribute('data-student-id', student.id || '');
            seat.setAttribute('data-student-email', student.studentEmail || '');
            seat.setAttribute('data-student-name', student.studentName || '');
            const name = student.studentName || '';
            const init = initialsFrom(name);
            const avatar = document.createElement('div');
            avatar.className = 'avatar';
            avatar.textContent = init;
            seat.appendChild(avatar);
            // status subheader bar
            const statusLower = (student.status || '').toLowerCase();
            const bar = document.createElement('div');
            bar.className = 'seat-status';
            bar.textContent = student.status || '';
            if (statusLower === 'present') seat.classList.add('present');
            else if (statusLower === 'late') seat.classList.add('late');
            else if (statusLower === 'absent') seat.classList.add('absent');
            seat.appendChild(bar);
            seat.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                openStudentMenu(seat, seat);
            });
        } else {
            seat.classList.add('empty');
        }
        frag.appendChild(seat);
    }
    seatingGridEl.appendChild(frag);
}

function persistGridSize() {
    const classCode = document.body.dataset.classCode || '';
    if (seatRowsInput) localStorage.setItem(`seatRows_${classCode}`, clamp(seatRowsInput.value, 1, 20));
    if (seatColsInput) localStorage.setItem(`seatCols_${classCode}`, clamp(seatColsInput.value, 1, 20));
}

// Dummy data removed - now using real data from database via ExportData endpoint

// Back button - go back to class page
backButton?.addEventListener('click', () => { 
    if (typeof window.showToast === 'function') {
        window.showToast('Returning...');
    }
    setTimeout(() => window.location.href = '/professordb/ProfessorDb', 600);
});

function renderStudents(list) {
    if (!studentListContainer) return;
    studentListContainer.innerHTML = "";
    const arr = Array.isArray(list) ? list : [];

    if (arr.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'no-students';
        empty.textContent = 'No approved students yet.';
        studentListContainer.appendChild(empty);
    } else {
        arr.forEach(student => {
            const name = student.studentName || '';
            const parts = name.split(/\s+/).filter(Boolean);
            const initials = parts.length ? (parts[0].slice(0,1) + (parts.length > 1 ? parts[parts.length-1].slice(0,1) : '')) .toUpperCase() : '';

            const card = document.createElement('div');
            card.classList.add('student-card');
            card.setAttribute('data-student-id', student.id || '');
            card.innerHTML = `
                <div class="student-photo avatar">${initials}</div>
                <div class="student-info">
                    <div class="student-name">${name}</div>
                    <div class="student-email">${student.studentEmail || ''}</div>
                    <div class="student-id">ID: ${student.id || ''}</div>
                </div>`;
            studentListContainer.appendChild(card);
        });
    }

    if (studentCountEl) {
        studentCountEl.textContent = `Students: ${arr.length}`;
    }
}

async function loadStudents(classCode) {
    try {
        const res = await fetch(`/AdminManageClass/GetStudentsByClassCode/${encodeURIComponent(classCode)}`);
        if (!res.ok) throw new Error(`Failed to fetch students (${res.status})`);
        const data = await res.json();
        studentsData = Array.isArray(data) ? data : [];
        renderStudents(studentsData);
        renderSeatGridFromData();
    } catch (err) {
        console.error('Error loading students:', err);
        if (typeof window.showToast === 'function') window.showToast("⚠️ Could not load students.");
    }
}
 
async function loadJoinRequests(classCode) { 
     try { 
        const url = `/AdminManageClass/GetJoinRequests/${encodeURIComponent(classCode)}`;
        const res = await fetch(url); 
        if (!res.ok) throw new Error(`Failed to fetch join requests (${res.status})`); 
        const data = await res.json(); 
        console.debug('JoinRequests fetched:', { url, count: Array.isArray(data) ? data.length : 'n/a', data });
        if (!joinRequestContainer) return;
        joinRequestContainer.innerHTML = ""; 
        if (!Array.isArray(data) || data.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'no-requests';
            empty.textContent = 'No pending join requests.';
            joinRequestContainer.appendChild(empty);
            return;
        }
        data.forEach(req => { 
            const row = document.createElement("div"); 
            row.classList.add("join-request"); 
            const dateStr = req.requestedAt ? new Date(req.requestedAt).toLocaleString() : ""; 
            const sectionLabel = req.sectionDisplay || req.classCode || "";
            
            // Create elements manually to attach event listeners
            const joinInfo = document.createElement("div");
            joinInfo.className = "join-info";
            joinInfo.innerHTML = `<span class="join-name">${req.studentName || ''}</span> 
                                  <span class="join-class">(${sectionLabel})</span> 
                                  <span class="join-time">${dateStr}</span>`;
            
            const joinActions = document.createElement("div");
            joinActions.className = "join-actions";
            
            const approveBtn = document.createElement("button");
            approveBtn.type = "button";
            approveBtn.className = "approve-btn";
            approveBtn.setAttribute("data-id", req.id || '');
            approveBtn.setAttribute("data-name", req.studentName || '');
            approveBtn.setAttribute("data-class-code", req.classCode || '');
            approveBtn.setAttribute("data-student-email", req.studentEmail || '');
            approveBtn.setAttribute("data-section-label", sectionLabel);
            approveBtn.textContent = "✅ Approve";
            approveBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                e.stopPropagation();
                const requestId = approveBtn.dataset.id;
                const classCode = approveBtn.dataset.classCode;
                const studentEmail = approveBtn.dataset.studentEmail;
                console.log('Approve button clicked (direct):', { requestId, classCode, studentEmail });
                if (requestId) {
                    await handleJoinAction(requestId, classCode, studentEmail, true);
                } else {
                    console.error('Approve button missing requestId');
                    if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                }
            });
            
            const rejectBtn = document.createElement("button");
            rejectBtn.type = "button";
            rejectBtn.className = "reject-btn";
            rejectBtn.setAttribute("data-id", req.id || '');
            rejectBtn.textContent = "❌ Cancel";
            rejectBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                e.stopPropagation();
                const requestId = rejectBtn.dataset.id;
                console.log('Reject button clicked (direct):', { requestId });
                if (requestId) {
                    await handleJoinAction(requestId, null, null, false);
                } else {
                    console.error('Reject button missing requestId');
                    if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                }
            });
            
            joinActions.appendChild(approveBtn);
            joinActions.appendChild(rejectBtn);
            
            row.appendChild(joinInfo);
            row.appendChild(joinActions);
            joinRequestContainer.appendChild(row);
            
            // Attach listeners to the newly created buttons
            attachButtonListeners(); 
        }); 
     } catch (err) { 
        console.error('Error loading join requests:', err); 
        if (typeof window.showToast === 'function') window.showToast("⚠️ Could not load join requests."); 
     } 
}

async function handleJoinAction(requestId, classCode, studentEmail, approve = true) {
    const endpoint = approve ? "/AdminManageClass/ApproveJoin" : "/AdminManageClass/RejectJoin";
    console.log('handleJoinAction called:', { requestId, classCode, studentEmail, approve, endpoint });
    
    if (!requestId) {
        console.error('Missing requestId');
        if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
        return;
    }

    try {
        const requestBody = { 
            RequestId: requestId, 
            ClassCode: classCode || '', 
            StudentEmail: studentEmail || '' 
        };
        console.log('Sending request:', { endpoint, body: requestBody });

        const res = await fetch(endpoint, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(requestBody),
        });

        console.log('Response status:', res.status, res.statusText);
        const data = await res.json();
        console.log('Response data:', data);

        if (!res.ok) {
            const errorMsg = data.message || data.Message || "Request failed";
            console.error('Request failed:', errorMsg);
            throw new Error(errorMsg);
        }

        const successMsg = data.message || data.Message || (approve ? "Request approved." : "Request rejected.");
        if (typeof window.showToast === 'function') window.showToast(successMsg);
        
        // Reload data - reload join requests first (to remove approved/rejected from pending list)
        // then reload students (to show newly approved students in class list)
        if (currentClassCode) {
            // Small delay to ensure database updates are complete
            await new Promise(resolve => setTimeout(resolve, 300));
            await loadJoinRequests(currentClassCode);
            await loadStudents(currentClassCode);
        }
    } catch (err) {
        console.error('Error in handleJoinAction:', err);
        const errorMsg = err.message || "An error occurred";
        if (typeof window.showToast === 'function') window.showToast(`⚠️ ${errorMsg}`);
    }
}

async function markAttendance(studentId, status) {
    try {
        const res = await fetch("/AdminManageClass/MarkAttendance", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ studentId, status, classCode: currentClassCode }),
            credentials: 'same-origin',
        });
        const data = await res.json();
        if (typeof window.showToast === 'function') window.showToast(data.message || 'Attendance updated');
        // Optimistically update the row's status label
        const row = document.querySelector(`.student-card .actions [data-id='${studentId}']`)?.closest('.student-card');
        if (row) {
            const label = row.querySelector('.student-status');
            if (label) {
                const statusLower = (status || '').toLowerCase();
                // update class to match server-rendered style: status-<lower>
                label.className = `student-status status-${statusLower}`;

                // update inner att-text if present
                const att = label.querySelector('.att-text');
                if (att) att.textContent = status;

                // update icon if present
                const iconEl = label.querySelector('i');
                if (iconEl) {
                    iconEl.className = `fa-solid ${statusLower === 'present' ? 'fa-circle-check' : statusLower === 'late' ? 'fa-clock' : 'fa-circle-xmark'}`;
                }
            }
        }
        // Do not reload students; attendance is stored separately from StudentRecord list
    } catch (err) { console.error(err); if (typeof window.showToast === 'function') window.showToast("⚠️ Could not update attendance."); }
}

function computeStanding(attStatusList, taskAttained, taskTotal) {
    // Attendance grade: Present = 1, Late = 0.5, Absent = 0 (averaged then scaled to 100)
    let attPct = 0;
    if (Array.isArray(attStatusList) && attStatusList.length > 0) {
        let score = 0;
        attStatusList.forEach(st => {
            const s = (st || "").toLowerCase();
            if (s === "present") score += 1;
            else if (s === "late") score += 0.5;
        });
        attPct = (score / attStatusList.length) * 100;
    }

    // Task grade out of 100
    let taskPct = 0;
    if (taskTotal > 0) {
        taskPct = (taskAttained / taskTotal) * 100;
    }

    // Assessment left blank (manual input), treat as 0 for now
    const assessPct = 0;

    // Overall grade: Attendance 20%, Task 50%, Assessment 30%
    const finalScore = attPct * 0.20 + taskPct * 0.50 + assessPct * 0.30;
    const standing = finalScore >= 75 ? "Passed" : "Failed";
    return { finalScore, standing };
}

async function exportData() {
    try {
        const classCode = document.body?.dataset?.classCode || "CLASSCODE";
        if (typeof window.showToast === 'function') {
            window.showToast("📥 Loading data from database...");
        }
        const response = await fetch(`/AdminManageClass/ExportData/${encodeURIComponent(classCode)}`, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' }
        });
        if (!response.ok) {
            throw new Error(`Failed to fetch export data: ${response.status} ${response.statusText}`);
        }
        const data = await response.json();
        if (!data.success || !data.students || !Array.isArray(data.students)) {
            throw new Error(data.message || "Invalid data received from server");
        }
        const latestLabel = Array.isArray(data.attendanceLabels) && data.attendanceLabels.length ? data.attendanceLabels[data.attendanceLabels.length - 1] : null;
        let reportDate = new Date();
        if (latestLabel && latestLabel.startsWith("Attendance_")) {
            const dt = latestLabel.replace("Attendance_","").trim();
            const parts = dt.split("-");
            if (parts.length === 3) {
                const y = parseInt(parts[0],10);
                const m = parseInt(parts[1],10) - 1;
                const d = parseInt(parts[2],10);
                const parsed = new Date(y, m, d);
                if (!isNaN(parsed.getTime())) reportDate = parsed;
            }
        }
        const subjectName = data.subjectName || "";
        const schoolName = "Sta. Lucia Senior Highschool";
        async function toDataUrl(url) {
            const res = await fetch(url, { cache: "no-cache" });
            const blob = await res.blob();
            return await new Promise((resolve) => {
                const reader = new FileReader();
                reader.onloadend = () => resolve(reader.result);
                reader.readAsDataURL(blob);
            });
        }
        const logoDataUrl = await toDataUrl("/images/SLSHS.png").catch(() => "");
        const rows = data.students.map(s => {
            const fullName = s.fullName || s.studentName || "";
            let status = "Absent";
            if (latestLabel && s.attendanceStatuses && s.attendanceStatuses[latestLabel]) {
                status = s.attendanceStatuses[latestLabel];
            } else if (s.attendanceStatuses) {
                const keys = Object.keys(s.attendanceStatuses);
                if (keys.length) {
                    const k = keys.sort().pop();
                    status = s.attendanceStatuses[k] || "Absent";
                }
            }
            return { fullName, status };
        });
        const dateStr = reportDate.toLocaleDateString(undefined, { year: "numeric", month: "long", day: "numeric" });
        const sectionName = ((document.querySelector('.section-name')?.textContent) || '').replace(/^Section:\s*/i,'').trim();
        const teacherName = (document.querySelector('.bottom-bar .user-name')?.textContent || '').trim();
        const html = `
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Attendance Report</title>
<style>
body { font-family: Arial, 'Segoe UI', Roboto, sans-serif; margin: 40px; color: #0b213a; }
.header { position: relative; text-align: center; margin-bottom: 30px; }
.logo { position: absolute; top: 0; left: 0; }
.logo img { height: 64px; width: auto; }
.school-name { font-size: 22px; font-weight: 700; margin: 0; }
.subject-name { font-size: 16px; font-weight: 600; margin: 2px 0 10px 0; color: #334155; }
.section-header { font-size: 15px; font-weight: 600; margin: 2px 0 6px 0; color: #334155; }
.teacher-header { font-size: 15px; font-weight: 600; margin: 2px 0 18px 0; color: #334155; }
.meta { display: flex; justify-content: space-between; align-items: center; margin: 26px 0 24px 0; font-size: 14px; }
.meta .left { font-weight: 700; }
.table { width: 100%; border-collapse: collapse; margin-top: 14px; }
.table th, .table td { border: 1px solid #475569; padding: 8px 10px; font-size: 14px; }
.table th { background: #f1f5f9; text-align: left; }
.status-present { color: #16a34a; font-weight: 600; }
.status-absent { color: #ef4444; font-weight: 600; }
.status-late { color: #eab308; font-weight: 600; }
@media print { body { margin: 20mm; } }
</style>
</head>
<body>
  <div class="header">
    <div class="logo">${logoDataUrl ? `<img src="${logoDataUrl}" alt="Logo">` : ""}</div>
    <h1 class="school-name">${schoolName}</h1>
    <h2 class="subject-name">${subjectName}</h2>
    ${sectionName ? `<h3 class="section-header">Section: ${sectionName}</h3>` : ``}
    ${teacherName ? `<h3 class="teacher-header">Teacher: ${teacherName}</h3>` : ``}
  </div>
  <div class="meta"><div class="left">Attendance</div><div class="right">${dateStr}</div></div>
  <table class="table">
    <thead><tr><th>Student Name</th><th>Status</th></tr></thead>
    <tbody>
      ${rows.map(r => {
          const sl = String(r.status || "").toLowerCase();
          const cls = sl === "present" ? "status-present" : sl === "late" ? "status-late" : "status-absent";
          return `<tr><td>${r.fullName}</td><td class="${cls}">${r.status || ""}</td></tr>`;
      }).join("")}
    </tbody>
  </table>
</body>
</html>`;
        async function loadScript(src) {
            return new Promise((resolve, reject) => {
                const exists = Array.from(document.scripts).some(s => s.src && s.src.indexOf(src) !== -1);
                if (exists) { resolve(); return; }
                const s = document.createElement('script');
                s.src = src;
                s.onload = () => resolve();
                s.onerror = () => reject(new Error(`Failed to load ${src}`));
                document.head.appendChild(s);
            });
        }
        async function ensurePdfMake() {
            if (!(window.pdfMake && window.pdfMake.createPdf)) {
                await loadScript('https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.2.7/pdfmake.min.js');
            }
            if (!(window.pdfMake && window.pdfMake.vfs)) {
                await loadScript('https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.2.7/vfs_fonts.js');
            }
        }
        async function downloadAttendancePdf() {
            try {
                await ensurePdfMake();
                const body = [
                    [{ text: 'Student Name', style: 'tableHeader' }, { text: 'Status', style: 'tableHeader' }]
                ];
                rows.forEach(r => {
                    const sl = String(r.status || "").toLowerCase();
                    const stStyle = sl === 'present' ? 'statusPresent' : sl === 'late' ? 'statusLate' : 'statusAbsent';
                    body.push([{ text: r.fullName || '', style: 'cell' }, { text: r.status || '', style: stStyle }]);
                });
                const docDefinition = {
                    info: { title: `${subjectName} — Attendance ${dateStr}` },
                    content: [
                        { text: 'Sta. Lucia Senior Highschool', style: 'school' },
                        { text: subjectName || 'Subject', style: 'title', margin: [0, 2, 0, 0] },
                        { text: (sectionName ? `Section: ${sectionName}` : ''), style: 'meta' },
                        { text: (teacherName ? `Teacher: ${teacherName}` : ''), style: 'meta' },
                        { text: dateStr, style: 'meta', margin: [0, 0, 0, 8] },
                        {
                            table: {
                                headerRows: 1,
                                widths: ['*', 120],
                                body
                            },
                            layout: {
                                fillColor: (rowIndex) => rowIndex === 0 ? '#f3f6fb' : null
                            }
                        }
                    ],
                    styles: {
                        school: { fontSize: 12, bold: true, color: '#1e293b' },
                        title: { fontSize: 14, bold: true, color: '#0b213a' },
                        meta: { fontSize: 10, color: '#475569' },
                        tableHeader: { bold: true, fontSize: 11, color: '#111827' },
                        cell: { fontSize: 10 },
                        statusPresent: { fontSize: 10, color: '#16a34a' },
                        statusLate: { fontSize: 10, color: '#eab308' },
                        statusAbsent: { fontSize: 10, color: '#ef4444' }
                    },
                    defaultStyle: { fontSize: 10 }
                };
                window.pdfMake.createPdf(docDefinition).download(`${baseName}.pdf`);
            } catch (e) {
                console.error('PDF download failed:', e);
                if (typeof window.showToast === 'function') window.showToast('⚠️ Could not generate PDF. Check console.');
            }
        }
        function downloadDoc(htmlContent, baseName) {
            const blob = new Blob([htmlContent], { type: "application/msword" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = `${baseName}.doc`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }
        function openPdf(htmlContent) {
            const w = window.open("", "_blank");
            if (!w) return;
            w.document.open();
            w.document.write(htmlContent);
            w.document.close();
        }
        function downloadPdf(htmlContent, baseName) {
            const w = window.open("", "_blank");
            if (!w) return;
            w.document.open();
            w.document.write(`<title>${baseName}</title>${htmlContent}`);
            w.document.close();
            const tryPrint = () => {
                try { w.focus(); w.print(); } catch (e) {}
            };
            w.onload = () => {
                setTimeout(tryPrint, 300);
            };
            w.onafterprint = () => {
                try { w.close(); } catch (e) {}
            };
        }
        const ts = new Date().toISOString().replace(/[:.]/g, "-").slice(0, -5);
        const baseName = `Class_${classCode}_Attendance_${ts}`;
        const overlay = document.createElement("div");
        overlay.style.position = "fixed";
        overlay.style.right = "20px";
        overlay.style.bottom = "20px";
        overlay.style.background = "#fff";
        overlay.style.border = "1px solid rgba(16,24,40,0.12)";
        overlay.style.borderRadius = "10px";
        overlay.style.boxShadow = "0 10px 24px rgba(16,24,40,0.12)";
        overlay.style.padding = "10px";
        overlay.style.display = "flex";
        overlay.style.gap = "8px";
        overlay.style.zIndex = "9999";
        const btnPdf = document.createElement("button");
        btnPdf.textContent = "Preview";
        btnPdf.className = "btn small primary";
        const btnDoc = document.createElement("button");
        btnDoc.textContent = "Download PDF";
        btnDoc.className = "btn small";
        const btnClose = document.createElement("button");
        btnClose.textContent = "Close";
        btnClose.className = "btn small";
        overlay.appendChild(btnPdf);
        overlay.appendChild(btnDoc);
        overlay.appendChild(btnClose);
        document.body.appendChild(overlay);
        btnPdf.addEventListener("click", () => { openPdf(html); if (typeof window.showToast === 'function') window.showToast("Preview opened in a new tab."); });
        btnDoc.addEventListener("click", async () => { await downloadAttendancePdf(); if (typeof window.showToast === 'function') window.showToast("Downloading PDF..."); });
        btnClose.addEventListener("click", () => { document.body.removeChild(overlay); });
        if (typeof window.showToast === 'function') {
            window.showToast(`✅ Prepared ${data.students.length} student(s) attendance report.`);
        }
    } catch (err) {
        console.error("Export error:", err);
        if (typeof window.showToast === 'function') {
            window.showToast(`⚠️ Export failed: ${err.message || "Unknown error"}`);
        }
    }
}

function parseGradePercent(v) {
    if (v == null) return NaN;
    const s = String(v).trim();
    if (!s) return NaN;
    if (/^\d+(\.\d+)?%$/.test(s)) {
        return parseFloat(s.replace('%',''));
    }
    const m = s.match(/^(\d+(?:\.\d+)?)\s*\/\s*(\d+(?:\.\d+)?)$/);
    if (m) {
        const num = parseFloat(m[1]);
        const den = parseFloat(m[2]);
        if (den > 0) return (num / den) * 100;
        return NaN;
    }
    const n = parseFloat(s);
    return isNaN(n) ? NaN : n;
}

let currentClassCode = ''; // set this on page load from view

// Function to attach event listeners to buttons
function attachButtonListeners() {
    // Attach to approve buttons
    const approveButtons = document.querySelectorAll('.approve-btn');
    console.log(`Found ${approveButtons.length} approve buttons`);
    
    approveButtons.forEach((btn, index) => {
        if (btn.hasAttribute('data-listener-attached')) {
            console.log(`Approve button ${index} already has listener`);
            return;
        }
        
        btn.setAttribute('data-listener-attached', 'true');
        console.log(`Attaching listener to approve button ${index}:`, btn);
        
        btn.addEventListener('click', async function(e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('✅ APPROVE BUTTON CLICKED!', this);
            const requestId = this.dataset.id || this.getAttribute('data-id');
            const classCode = this.dataset.classCode || this.getAttribute('data-class-code');
            const studentEmail = this.dataset.studentEmail || this.getAttribute('data-student-email');
            console.log('Approve button data:', { requestId, classCode, studentEmail, allAttributes: Array.from(this.attributes).map(a => `${a.name}=${a.value}`) });
            
            if (!requestId) {
                console.error('❌ Approve button missing requestId');
                alert('Missing request ID! Check console for details.');
                if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                return;
            }
            
            await handleJoinAction(requestId, classCode, studentEmail, true);
        });
    });
    
    // Attach to reject buttons
    const rejectButtons = document.querySelectorAll('.reject-btn');
    console.log(`Found ${rejectButtons.length} reject buttons`);
    
    rejectButtons.forEach((btn, index) => {
        if (btn.hasAttribute('data-listener-attached')) {
            console.log(`Reject button ${index} already has listener`);
            return;
        }
        
        btn.setAttribute('data-listener-attached', 'true');
        console.log(`Attaching listener to reject button ${index}:`, btn);
        
        btn.addEventListener('click', async function(e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('✅ REJECT BUTTON CLICKED!', this);
            const requestId = this.dataset.id || this.getAttribute('data-id');
            console.log('Reject button data:', { requestId, allAttributes: Array.from(this.attributes).map(a => `${a.name}=${a.value}`) });
            
            if (!requestId) {
                console.error('❌ Reject button missing requestId');
                alert('Missing request ID! Check console for details.');
                if (typeof window.showToast === 'function') window.showToast('⚠️ Missing request ID.');
                return;
            }
            
            await handleJoinAction(requestId, null, null, false);
        });
    });
}

// Event delegation for dynamically added buttons
document.addEventListener("click", async (e) => {
    // Handle approve button clicks
    if (e.target.closest && e.target.closest(".approve-btn")) {
        const approveBtn = e.target.closest(".approve-btn");
        if (!approveBtn.hasAttribute('data-listener-attached')) {
            e.preventDefault();
            e.stopPropagation();
            const requestId = approveBtn.dataset.id || approveBtn.getAttribute('data-id');
            const classCode = approveBtn.dataset.classCode || approveBtn.getAttribute('data-class-code');
            const studentEmail = approveBtn.dataset.studentEmail || approveBtn.getAttribute('data-student-email');
            console.log('Approve button clicked (delegation fallback):', { requestId, classCode, studentEmail });
            
            if (requestId) {
                await handleJoinAction(requestId, classCode, studentEmail, true);
            }
            return;
        }
    }

    // Handle reject button clicks
    if (e.target.closest && e.target.closest(".reject-btn")) {
        const rejectBtn = e.target.closest(".reject-btn");
        if (!rejectBtn.hasAttribute('data-listener-attached')) {
            e.preventDefault();
            e.stopPropagation();
            const requestId = rejectBtn.dataset.id || rejectBtn.getAttribute('data-id');
            console.log('Reject button clicked (delegation fallback):', { requestId });
            
            if (requestId) {
                await handleJoinAction(requestId, null, null, false);
            }
            return;
        }
    }

    // Handle attendance buttons robustly
    const attBtn = e.target.closest && e.target.closest('button.mark-present, button.mark-absent, button.mark-late');
    if (attBtn) {
        const id = attBtn.dataset.id;
        const status = attBtn.dataset.status;
        if (id && status) await markAttendance(id, status);
        return;
    }
    const unenrollBtn = e.target.closest && e.target.closest('button.unenroll');
    if (unenrollBtn) {
        const id = unenrollBtn.dataset.id;
        const email = unenrollBtn.dataset.email || '';
        const name = unenrollBtn.dataset.name || '';
        openUnenrollModal(id, email, name);
        return;
    }
    if (e.target.closest && e.target.closest('#export-btn')) { await exportData(); return; }
    if (e.target.closest && e.target.closest('#attendance-btn')) {
        if (typeof window.showToast === 'function') window.showToast("Opening attendance...");
        setTimeout(() => startFocusCycle(), 1000);
        return;
    }

    if (e.target.closest && e.target.closest('#create-meet-btn')) {
        openMeetModal();
        return;
    }

    if (e.target.closest && e.target.closest('#join-meet-btn')) {
        const meetingId = document.body.dataset.meetingId || '';
        const joinUrl = document.body.dataset.meetingJoinUrl || '';
        const createdAt = document.body.dataset.meetingCreatedAt || '';
        if (joinUrl && (meetingId || createdAt)) {
            openManageMeetingModal(joinUrl, meetingId, createdAt || new Date().toISOString());
        } else if (typeof window.showToast === 'function') {
            window.showToast('⚠️ Meeting link not found.');
        }
        return;
    }

    const moreBtn = e.target.closest && e.target.closest('.unenroll-btn');
    if (moreBtn) {
            // removed: ellipsis menu from class-list
            return;
    }
});

function openAttendanceModal() {
    if (!focusModal) return;
    focusModal.style.display = 'flex';
    document.body.classList.add('focus-mode-active');
}

function closeAttendanceModal() {
    if (!focusModal) return;
    focusModal.style.display = 'none';
    document.body.classList.remove('focus-mode-active');
    focusIndex = -1;
    if (focusProgress) focusProgress.style.display = 'none';
}

function openMeetModal() {
    if (!meetModal) return;
    if (meetTitleInput) meetTitleInput.value = '';
    if (meetScheduleInput) meetScheduleInput.value = '';
    meetModal.style.display = 'flex';
    document.body.classList.add('focus-mode-active');
}

function closeMeetModalInternal() {
    if (!meetModal) return;
    meetModal.style.display = 'none';
    document.body.classList.remove('focus-mode-active');
}

function formatDuration(ms) {
    const totalSec = Math.floor(ms / 1000);
    const h = Math.floor(totalSec / 3600);
    const m = Math.floor((totalSec % 3600) / 60);
    const s = totalSec % 60;
    return [h, m, s].map(v => String(v).padStart(2, '0')).join(':');
}

function openManageMeetingModal(joinUrl, meetingId, startTimeIso) {
    currentMeetingState = { meetingId: meetingId || '', joinUrl: joinUrl || '', startTime: startTimeIso ? new Date(startTimeIso) : new Date() };
    if (manageMeetingModal) {
        manageMeetingModal.classList.add('show');
        manageMeetingModal.style.display = 'flex';
        document.body.classList.add('focus-mode-active');
    }
    if (meetingDurationValue) meetingDurationValue.textContent = '00:00:00';
    if (manageMeetingJoinBtn) manageMeetingJoinBtn.dataset.joinUrl = joinUrl || '';
    if (manageMeetingStopBtn) manageMeetingStopBtn.dataset.meetingId = meetingId || '';

    if (meetingDurationInterval) clearInterval(meetingDurationInterval);
    function tick() {
        if (!currentMeetingState.startTime || !meetingDurationValue) return;
        const elapsed = Date.now() - currentMeetingState.startTime.getTime();
        meetingDurationValue.textContent = formatDuration(Math.max(0, elapsed));
    }
    tick();
    meetingDurationInterval = setInterval(tick, 1000);
}

function closeManageMeetingModalInternal() {
    if (meetingDurationInterval) {
        clearInterval(meetingDurationInterval);
        meetingDurationInterval = null;
    }
    if (manageMeetingModal) {
        manageMeetingModal.classList.remove('show');
        manageMeetingModal.style.display = 'none';
        document.body.classList.remove('focus-mode-active');
    }
}


focusModal?.addEventListener('click', (e) => {
    if (e.target === focusModal) closeAttendanceModal();
});
closeFocusModal?.addEventListener('click', closeAttendanceModal);
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeAttendanceModal();
        closeMeetModalInternal();
        closeManageMeetingModalInternal();
        closeMenus();
    }
});

meetModal?.addEventListener('click', (e) => {
    if (e.target === meetModal) closeMeetModalInternal();
});
closeMeetModal?.addEventListener('click', closeMeetModalInternal);
cancelMeetBtn?.addEventListener('click', () => closeMeetModalInternal());

manageMeetingModal?.addEventListener('click', (e) => {
    if (e.target === manageMeetingModal) closeManageMeetingModalInternal();
});
closeManageMeetingModal?.addEventListener('click', closeManageMeetingModalInternal);

manageMeetingJoinBtn?.addEventListener('click', () => {
    const url = manageMeetingJoinBtn?.dataset?.joinUrl || currentMeetingState.joinUrl;
    if (url) window.location.href = url;
});

manageMeetingStopBtn?.addEventListener('click', async () => {
    const meetingId = manageMeetingStopBtn?.dataset?.meetingId || currentMeetingState.meetingId;
    if (!meetingId) {
        if (typeof window.showToast === 'function') window.showToast('⚠️ Meeting ID not found.');
        return;
    }
    try {
        const res = await fetch('/AdminManageClass/DeleteMeeting', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ MeetingId: meetingId })
        });
        const data = await res.json();
        if (data && data.success !== false) {
            closeManageMeetingModalInternal();
            if (typeof window.showToast === 'function') window.showToast('Meeting ended.');
            window.location.reload();
        } else {
            if (typeof window.showToast === 'function') window.showToast(data?.message || 'Failed to stop meeting.');
        }
    } catch (err) {
        console.error('Stop meeting error:', err);
        if (typeof window.showToast === 'function') window.showToast('⚠️ Could not stop meeting.');
    }
});

async function createMeet() {
    const classCode = document.body.dataset.classCode || '';
    if (!classCode) {
        if (typeof window.showToast === 'function') window.showToast('⚠️ Missing class code.');
        return;
    }
    const title = meetTitleInput?.value || '';
    const scheduledAt = meetScheduleInput?.value || '';
    try {
        const res = await fetch('/AdminManageClass/CreateMeet', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ classCode, title, scheduledAt })
        });
        const data = await res.json();
        if (!res.ok || !data.success) {
            const msg = data.message || 'Failed to create meeting.';
            if (typeof window.showToast === 'function') window.showToast(`⚠️ ${msg}`);
            return;
        }
        closeMeetModalInternal();
        if (typeof window.showToast === 'function') {
            window.showToast(data.message || '✅ Meeting created and emails sent.');
        }
        const joinAppUrl = data.joinAppUrl || data.joinUrl || '';
        const contentId = data.contentId || '';
        const createdAt = data.createdAt ? (typeof data.createdAt === 'string' ? data.createdAt : new Date(data.createdAt).toISOString()) : new Date().toISOString();
        if (joinAppUrl) {
            openManageMeetingModal(joinAppUrl, contentId, createdAt);
        } else {
            setTimeout(() => window.location.reload(), 800);
        }
    } catch (err) {
        console.error('CreateMeet error:', err);
        if (typeof window.showToast === 'function') window.showToast('⚠️ Could not create meeting.');
    }
}

confirmMeetBtn?.addEventListener('click', async (e) => {
    e.preventDefault();
    await createMeet();
});

function ensureStudentMenu() {
    if (!studentMenuEl) {
        studentMenuEl = document.createElement('div');
        studentMenuEl.className = 'student-menu';
        studentMenuEl.style.display = 'none';
        document.body.appendChild(studentMenuEl);
    }
    if (!attendanceMenuEl) {
        attendanceMenuEl = document.createElement('div');
        attendanceMenuEl.className = 'attendance-menu';
        attendanceMenuEl.style.display = 'none';
        document.body.appendChild(attendanceMenuEl);
    }
}

function closeMenus() {
    if (studentMenuEl) studentMenuEl.style.display = 'none';
    if (attendanceMenuEl) attendanceMenuEl.style.display = 'none';
}

function updateFocusProgress() {
    const total = studentsData.length || 0;
    const current = Math.min(Math.max(focusIndex + 1, 0), total);
    if (progressText) progressText.textContent = `${current} / ${total}`;
    if (progressFill) progressFill.style.width = total ? `${(current / total) * 100}%` : '0%';
}

function showFocusStudent(idx) {
    const s = studentsData[idx];
    if (!s) return;
    if (focusName) focusName.textContent = s.studentName || 'Student';
    const url = s.photoUrl || '';
    if (url && focusPhoto) {
        focusPhoto.src = url;
        focusPhoto.style.display = 'block';
        if (focusInitials) focusInitials.style.display = 'none';
    } else {
        if (focusPhoto) {
            focusPhoto.src = '';
            focusPhoto.style.display = 'none';
        }
        if (focusInitials) {
            const init = initialsFrom(s.studentName || '');
            focusInitials.textContent = init || 'NA';
            focusInitials.style.display = 'flex';
        }
    }
    updateFocusProgress();
}

async function confirmFocus(status) {
    const s = studentsData[focusIndex];
    if (s && s.id) {
        await markAttendance(s.id, status);
    }
    focusIndex += 1;
    if (focusIndex >= studentsData.length) {
        if (typeof window.showToast === 'function') window.showToast('Attendance confirmed for all students');
        closeAttendanceModal();
        return;
    }
    showFocusStudent(focusIndex);
}

btnAbsent?.addEventListener('click', () => confirmFocus('Absent'));
btnLate?.addEventListener('click', () => confirmFocus('Late'));
btnPresent?.addEventListener('click', () => confirmFocus('Present'));

function startFocusCycle() {
    if (!Array.isArray(studentsData) || studentsData.length === 0) {
        if (typeof window.showToast === 'function') window.showToast('No students to confirm');
        return;
    }
    if (focusProgress) focusProgress.style.display = 'block';
    focusIndex = 0;
    openAttendanceModal();
    showFocusStudent(focusIndex);
}

function openStudentMenu(button, card) {
    ensureStudentMenu();
    const r = button.getBoundingClientRect();
    studentMenuEl.innerHTML =
        '<div class="menu-item edit-attendance"><i class="fa-solid fa-user-pen"></i> Edit attendance</div>' +
        '<div class="menu-divider"></div>' +
        '<div class="menu-item unenroll"><i class="fa-solid fa-user-minus"></i> Unenroll student</div>';
    studentMenuEl.style.left = Math.min(r.right + 8, window.innerWidth - 200) + 'px';
    studentMenuEl.style.top = Math.max(10, r.top - 4) + 'px';
    studentMenuEl.style.display = 'block';

    studentMenuEl.querySelector('.edit-attendance')?.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        openAttendanceMenu(button, card);
    });
    studentMenuEl.querySelector('.unenroll')?.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const id = (button.dataset && (button.dataset.id || button.dataset.studentId)) || card?.getAttribute('data-student-id') || '';
        const email = (button.dataset && button.dataset.studentEmail) || card?.getAttribute('data-student-email') || card?.querySelector('.student-email')?.textContent?.trim() || '';
        const name = (button.dataset && button.dataset.studentName) || card?.getAttribute('data-student-name') || card?.querySelector('.student-name')?.textContent?.trim() || '';
        openUnenrollModal(id, email, name);
        closeMenus();
    });
}

function openAttendanceMenu(button, card) {
    ensureStudentMenu();
    const r = studentMenuEl.getBoundingClientRect();
    attendanceMenuEl.innerHTML =
        '<div class="menu-item present"><i class="fa-solid fa-user-check"></i> Present</div>' +
        '<div class="menu-item absent"><i class="fa-solid fa-user-slash"></i> Absent</div>' +
        '<div class="menu-item late"><i class="fa-solid fa-clock"></i> Late</div>';
    attendanceMenuEl.style.left = Math.min(r.right + 8, window.innerWidth - 180) + 'px';
    attendanceMenuEl.style.top = r.top + 'px';
    attendanceMenuEl.style.display = 'block';

    const statusEl = card.querySelector('.student-status') || card.querySelector('.seat-status');
    async function setStatus(type, label) {
        if (statusEl) {
            statusEl.textContent = label;
            statusEl.classList.remove('status-present', 'status-absent', 'status-late');
            if (type === 'present') statusEl.classList.add('status-present');
            if (type === 'absent') statusEl.classList.add('status-absent');
            if (type === 'late') statusEl.classList.add('status-late');
            if (card && card.classList) {
                card.classList.remove('present', 'absent', 'late');
                if (type === 'present') card.classList.add('present');
                if (type === 'absent') card.classList.add('absent');
                if (type === 'late') card.classList.add('late');
            }
        }
        const id = card?.getAttribute('data-student-id') || (button.dataset && (button.dataset.id || button.dataset.studentId));
        if (id) await markAttendance(id, label);
        if (typeof window.showToast === 'function') window.showToast('Marked ' + label);
        closeMenus();
    }

    attendanceMenuEl.querySelector('.present')?.addEventListener('click', () => setStatus('present', 'Present'));
    attendanceMenuEl.querySelector('.absent')?.addEventListener('click', () => setStatus('absent', 'Absent'));
    attendanceMenuEl.querySelector('.late')?.addEventListener('click', () => setStatus('late', 'Late'));
}

document.addEventListener('click', (ev) => {
    const target = ev.target;
    const menuOpen = studentMenuEl && studentMenuEl.style.display === 'block';
    const subOpen = attendanceMenuEl && attendanceMenuEl.style.display === 'block';
    const inStudentMenu = menuOpen && studentMenuEl.contains(target);
    const inAttendanceMenu = subOpen && attendanceMenuEl.contains(target);
    const onAnchor = !!(target.closest && (target.closest('.seat.filled') || target.closest('.unenroll-btn')));

    if (subOpen && !inAttendanceMenu && !inStudentMenu && !onAnchor) {
        attendanceMenuEl.style.display = 'none';
    }
    if (menuOpen && !inStudentMenu && !onAnchor && !inAttendanceMenu) {
        closeMenus();
    }
});

// Initialize on page load
document.addEventListener("DOMContentLoaded", async () => {
    currentClassCode = document.body.dataset.classCode || '';
    console.log('AdminManageClass initialized with classCode:', currentClassCode);
    
    attachButtonListeners();
    wireInlineButtons(document);
    
    if (currentClassCode) {
        // initialize seat grid inputs from localStorage
        const classCode = currentClassCode;
        const rs = localStorage.getItem(`seatRows_${classCode}`);
        const cs = localStorage.getItem(`seatCols_${classCode}`);
        if (seatRowsInput && rs) seatRowsInput.value = rs;
        if (seatColsInput && cs) seatColsInput.value = cs;
        seatRowsInput?.addEventListener('change', () => { persistGridSize(); renderSeatGridFromData(); });
        seatColsInput?.addEventListener('change', () => { persistGridSize(); renderSeatGridFromData(); });
        seatRowsInput?.addEventListener('input', () => { renderSeatGridFromData(); });
        seatColsInput?.addEventListener('input', () => { renderSeatGridFromData(); });

        await loadStudents(currentClassCode);
        await loadJoinRequests(currentClassCode);
        
        setTimeout(() => {
            attachButtonListeners();
            wireInlineButtons(document);
            renderSeatGridFromData();
        }, 100);
    }

    // Wire search filtering
    if (classSearchInput) {
        classSearchInput.addEventListener('input', () => {
            const q = classSearchInput.value.trim().toLowerCase();
            const filtered = studentsData.filter(s =>
                (s.studentName || '').toLowerCase().includes(q) ||
                (s.studentEmail || '').toLowerCase().includes(q)
            );
            renderStudents(filtered);
        });
    }

    const list = document.getElementById('join-request-list');
    if (list && 'MutationObserver' in window) {
        new MutationObserver(function () { wireInlineButtons(list); }).observe(list, { childList: true, subtree: true });
    }
});

function wireInlineButtons(root) {
    (root || document).querySelectorAll('.approve-btn').forEach(function (btn) {
        if (btn.hasAttribute('data-listener-attached')) return;
        btn.setAttribute('data-listener-attached', 'true');
        btn.addEventListener('click', async function (e) {
            e.preventDefault();
            e.stopPropagation();
            const id = btn.getAttribute('data-id');
            const code = btn.getAttribute('data-class-code');
            const email = btn.getAttribute('data-student-email');
            if (!id) return;
            await handleJoinAction(id, code, email, true);
        });
    });
    (root || document).querySelectorAll('.reject-btn').forEach(function (btn) {
        if (btn.hasAttribute('data-listener-attached')) return;
        btn.setAttribute('data-listener-attached', 'true');
        btn.addEventListener('click', async function (e) {
            e.preventDefault();
            e.stopPropagation();
            const id = btn.getAttribute('data-id');
            if (!id) return;
            await handleJoinAction(id, null, null, false);
        });
    });
}

// removed grade chip behavior

async function unenrollStudent(studentId, studentEmail) {
    try {
        const body = { studentId: studentId || '', classCode: currentClassCode, studentEmail: studentEmail || '' };
        const res = await fetch('/AdminManageClass/Unenroll', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
            credentials: 'same-origin'
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Failed to un-enroll');
        if (typeof window.showToast === 'function') window.showToast(data.message || 'Student un-enrolled');
        if (currentClassCode) {
            await loadStudents(currentClassCode);
        }
    } catch (err) {
        console.error('Unenroll error:', err);
        if (typeof window.showToast === 'function') window.showToast(`⚠️ ${err.message || 'Could not un-enroll student'}`);
    }
}

let currentUnenroll = null;
const unenrollModal = document.getElementById('unenroll-modal');
const unenrollNameEl = document.getElementById('unenroll-name');
const unenrollEmailEl = document.getElementById('unenroll-email');
const confirmUnenrollBtn = document.getElementById('confirm-unenroll');
const cancelUnenrollBtn = document.getElementById('cancel-unenroll');
const closeUnenrollModalBtn = document.getElementById('close-unenroll-modal');
const unenrollPhoto = document.getElementById('unenroll-photo');
const unenrollInitials = document.getElementById('unenroll-initials');

function openUnenrollModal(studentId, studentEmail, studentName) {
    currentUnenroll = { id: studentId || '', email: studentEmail || '', name: studentName || '' };
    if (unenrollNameEl) unenrollNameEl.textContent = currentUnenroll.name;
    if (unenrollEmailEl) unenrollEmailEl.textContent = currentUnenroll.email;
    if (unenrollModal) {
        unenrollModal.style.display = 'flex';
        document.body.classList.add('focus-mode-active');
    }
    const s = Array.isArray(studentsData) ? studentsData.find(x => (x.id && x.id === currentUnenroll.id) || (x.studentEmail && x.studentEmail === currentUnenroll.email)) : null;
    const url = s && s.photoUrl ? s.photoUrl : '';
    if (url && unenrollPhoto) {
        unenrollPhoto.src = url;
        unenrollPhoto.style.display = 'block';
        if (unenrollInitials) unenrollInitials.style.display = 'none';
    } else {
        if (unenrollPhoto) {
            unenrollPhoto.src = '';
            unenrollPhoto.style.display = 'none';
        }
        if (unenrollInitials) {
            const init = initialsFrom(currentUnenroll.name || '');
            unenrollInitials.textContent = init || 'NA';
            unenrollInitials.style.display = 'flex';
        }
    }
}

function closeUnenrollModal() {
    if (unenrollModal) unenrollModal.style.display = 'none';
    document.body.classList.remove('focus-mode-active');
    currentUnenroll = null;
}

confirmUnenrollBtn?.addEventListener('click', async () => {
    if (!currentUnenroll) return;
    await unenrollStudent(currentUnenroll.id, currentUnenroll.email);
    closeUnenrollModal();
});

cancelUnenrollBtn?.addEventListener('click', () => closeUnenrollModal());
closeUnenrollModalBtn?.addEventListener('click', () => closeUnenrollModal());
unenrollModal?.addEventListener('click', (e) => {
    if (e.target === unenrollModal) closeUnenrollModal();
});
