// antiCheat.js
// Unified AntiCheat combining mouse.js, interact.js, fullscreen.js, focus.js
// Exposes window.AntiCheat for diagnostics. Each subsystem is isolated.

(function (global) {
    if (!global) return;
    const NS = global.AntiCheat = global.AntiCheat || {};
    if (NS.__initialized) return;
    NS.__initialized = true;

    function isStudentAnswerAssessmentPage() {
        try {
            const p = (global.location && global.location.pathname ? global.location.pathname : '').toLowerCase();
            return p.indexOf('studentanswerassessment') !== -1;
        } catch (_) {
            return false;
        }
    }

    function parseStudentAnswerAssessmentIds() {
        try {
            const path = (global.location.pathname || '').split('/').filter(Boolean);
            const idx = path.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
            if (idx < 0 || path.length < idx + 3) return null;
            return { classCode: path[idx + 1], contentId: path[idx + 2] };
        } catch (_) {
            return null;
        }
    }

    async function primeServerTotalsSeed() {
        try {
            if (!isStudentAnswerAssessmentPage()) return;
            const ids = parseStudentAnswerAssessmentIds();
            if (!ids) return;
            const ctrl = new AbortController();
            const t = setTimeout(() => { try { ctrl.abort(); } catch (_) { } }, 2500);
            const res = await fetch(`/StudentAnswerAssessment/${ids.classCode}/${ids.contentId}/anti-cheat-totals?ts=${Date.now()}`, {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'Cache-Control': 'no-cache' },
                signal: ctrl.signal
            }).catch(() => null);
            clearTimeout(t);
            if (!res || !res.ok) return;
            const data = await res.json().catch(() => null);
            if (!data || data.success !== true || !data.totals) return;
            if (!global.__ac) global.__ac = {};
            global.__ac.seedTotals = data.totals;
        } catch (_) { /* ignore */ }
    }

    /* ------------------ Shared utilities & styles ------------------ */
    const utils = {
        nowMs: () => Date.now(),
        formatTimeMs: (ms) => {
            const s = Math.floor(ms / 1000);
            const m = Math.floor(s / 60);
            const remS = s % 60;
            return `${m}m, ${remS}s`;
        },
        formatTimeSec: (sec) => {
            const m = Math.floor(sec / 60);
            const s = sec % 60;
            return `${m}m, ${s}s`;
        },
        attachStyles(cssText) {
            try {
                const st = document.createElement('style');
                st.dataset.ac = 'true';
                st.innerHTML = cssText;
                document.head.appendChild(st);
            } catch (e) {
                // silently fail - styles not critical
                console.warn('AntiCheat: failed to attach styles', e);
            }
        },
        safeAppend(el) {
            try {
                document.body.appendChild(el);
            } catch (e) {
                console.warn('AntiCheat: append failed', e);
            }
        }
    };

    // Combined CSS (keeps original look but scoped with ac- prefixes to reduce collision)
    utils.attachStyles(`
    /* General */
    .ac-scoreboard, .ac-status-scoreboard { position: fixed; z-index: 10040; font-family: sans-serif; color: #fff; box-shadow: 0 4px 12px rgba(0,0,0,0.3); }
    /* mouse.js styles */
    body { cursor: default; }
    .ac-afk-radius, .ac-afk-confirmed { position: fixed; border-radius: 50%; pointer-events: none; z-index: 9998; display: none; visibility: hidden; }
    .ac-afk-radius { border: 3px dashed transparent; }
    .ac-afk-confirmed { border: 3px dashed transparent; }
    .ac-status-scoreboard { bottom: 100px; right: 20px; padding: 16px 24px; border-radius: 12px; background: #222; font-size: 14px; line-height: 1.5; white-space: pre-line; }
  
    /* interact.js popup */
    .ac-interact-popup { position: fixed; top: 10px; right: 10px; background: rgba(0,0,0,0.8); color: white; padding: 20px; border-radius: 10px; z-index: 9999; font-family: Arial, sans-serif; font-size: 14px; box-shadow: 0 0 10px rgba(0,0,0,0.5); }
  
    /* While locked, hide every other AC layer so nothing (esp. iOS compositing) steals taps from the modal */
    body.ac-locked .ac-fullscreen-stats,
    body.ac-locked .ac-logsBox,
    body.ac-locked .ac-omniFocusBox,
    body.ac-locked .ac-cheatMetrics,
    body.ac-locked .ac-status-scoreboard,
    body.ac-locked .ac-studentStatus,
    body.ac-locked .ac-screenPromptOverlay,
    body.ac-locked .ac-interact-popup,
    body.ac-locked #ac-screenVideo { display: none !important; pointer-events: none !important; visibility: hidden !important; }
    /* Above student shell (sidebar ~12k) and bottom nav */
    .ac-unlock-screen { position: fixed; top:0; left:0; right:0; bottom:0; background: rgba(15,23,42,0.45); backdrop-filter: blur(7px); -webkit-backdrop-filter: blur(7px); display: none; z-index: 9999999; align-items: center; justify-content: center; pointer-events: auto; touch-action: manipulation; -webkit-overflow-scrolling: touch; }
    .ac-fullscreen-stats { position: fixed; top: 20px; left: 20px; background: rgba(0,0,0,0.7); color: white; padding: 10px 15px; font-size: 14px; border-radius: 10px; z-index: 10040; font-family: sans-serif; }
    .ac-unlock-screen__card { position: relative; z-index: 1; pointer-events: auto; background: #fff; padding: 1.35rem 1.25rem 1.5rem; border-radius: 16px; box-shadow: 0 20px 50px rgba(0,0,0,0.22); max-width: min(420px, 92vw); width: 100%; box-sizing: border-box; text-align: center; }
    .ac-unlock-screen .ac-button { background-color: #1f2937; color: white; padding: .85rem 1.5rem; border-radius: 12px; border: none; cursor: pointer; font-weight:600; min-height: 48px; min-width: min(280px, 85vw); max-width: 100%; touch-action: manipulation; -webkit-tap-highlight-color: rgba(255,255,255,0.2); font-size: 1rem; position: relative; z-index: 2; }
  
    /* focus.js overlay + boxes — above diagnostic HUD, below fullscreen lock */
    .ac-screenPromptOverlay { position: fixed; top:0; left:0; width:100vw; height:100vh; min-height: 100dvh; background:#333340; z-index:10150; display:flex; align-items:center; justify-content:center; flex-direction:column; color:white; font-family:sans-serif; text-align:center; overflow:hidden; padding: 12px; box-sizing: border-box; pointer-events: auto; touch-action: manipulation; }
    .ac-screenPromptOverlay--mobile { align-items: stretch; justify-content: center; }
    .ac-screen-prompt-stack { position: relative; z-index: 1; width: 100%; max-width: 22rem; margin: 0 auto; box-sizing: border-box; }
    .ac-screen-prompt-panel { display: flex; flex-direction: column; align-items: stretch; gap: 12px; text-align: center; }
    /* Author display:flex wins over the hidden attribute otherwise — both panels showed on desktop. */
    .ac-screenPromptOverlay [hidden] { display: none !important; }
    .ac-screen-prompt-hint { margin: 0; font-size: 0.95rem; line-height: 1.45; color: rgba(255,255,255,0.9); }
    .ac-screen-prompt-lead { font-size: 1.02rem; }
    .ac-btn-screen-primary { padding: 12px 20px; font-size: 1.05rem; font-weight: 600; background: #fff; color: #1a1a1a; border: none; border-radius: 12px; cursor: pointer; }
    .ac-btn-screen-secondary { padding: 10px 16px; font-size: 0.95rem; font-weight: 600; background: transparent; color: #fff; border: 1px solid rgba(255,255,255,0.45); border-radius: 12px; cursor: pointer; }
    .ac-screen-prompt-error { margin: 0; font-size: 0.85rem; color: #fecaca; line-height: 1.35; }
    .ac-studentStatus { position: fixed; top: 20px; right: 20px; padding: 0.8em 1.2em; border-left: 8px solid; border-radius: 8px; font-weight: bold; font-size: 1.1em; box-shadow: 0 0 8px rgba(0,0,0,0.2); z-index: 10040; opacity: 1; transition: opacity 1s ease-in-out; }
    .ac-green { background: #d4edda; color: #155724; border-color: green; }
    .ac-blue { background: #cce5ff; color: #004085; border-color: blue; }
    .ac-yellow { background: #fff3cd; color: #856404; border-color: gold; }
    .ac-orange { background: #ffe5b4; color: #a05a00; border-color: orange; }
    .ac-red { background: #f8d7da; color: #721c24; border-color: red; }
    .ac-logsBox { position: fixed; top: 150px; right: 20px; width: 360px; max-height: 300px; overflow-y: auto; background: rgba(0,0,0,0.85); color: white; font-family: monospace; font-size: 0.85em; padding: 12px; border-radius: 6px; z-index: 10040; }
    .ac-cheatMetrics { position: fixed; bottom: 20px; right: 20px; background: rgba(0,0,0,0.7); color: white; padding: 10px; border-radius: 8px; z-index: 10040; }
    .ac-omniFocusBox { position: fixed; top: 150px; right: 400px; width: 260px; background: rgba(0,0,0,0.85); color: white; font-family: sans-serif; font-size: 0.9em; padding: 12px; border-radius: 6px; z-index: 10040; box-shadow: 0 0 10px rgba(0,0,0,0.5); }
    @media (max-width: 900px) {
      /* Don’t intercept taps — student must reach iframe + fullscreen / screen-share UI */
      .ac-logsBox, .ac-omniFocusBox, .ac-fullscreen-stats, .ac-cheatMetrics, .ac-status-scoreboard, .ac-studentStatus { pointer-events: none; }
      .ac-interact-popup { pointer-events: auto; }
      /* Small strips above bottom nav; keep center of screen clear */
      .ac-fullscreen-stats { display: none; }
      .ac-studentStatus { top: 6px; left: 8px; right: 8px; max-width: none; width: auto; font-size: 0.68rem; padding: 0.35em 0.5em; text-align: center; }
      .ac-logsBox { top: auto; bottom: calc(200px + env(safe-area-inset-bottom, 0px)); left: 8px; right: 8px; width: auto; max-height: 52px; overflow: hidden; font-size: 9px; padding: 6px 8px; line-height: 1.25; opacity: 0.9; }
      .ac-omniFocusBox { top: auto; bottom: calc(148px + env(safe-area-inset-bottom, 0px)); left: 8px; right: 8px; width: auto; max-height: 48px; overflow: hidden; font-size: 0.72em; padding: 6px 8px; line-height: 1.25; opacity: 0.9; }
      .ac-cheatMetrics { bottom: calc(96px + env(safe-area-inset-bottom, 0px)); left: 8px; right: 8px; top: auto; max-width: none; font-size: 10px; padding: 6px 8px; max-height: 44px; overflow: hidden; }
      .ac-status-scoreboard { bottom: calc(56px + env(safe-area-inset-bottom, 0px)); left: 8px; right: 8px; width: auto; max-width: none; font-size: 10px; padding: 8px 10px; white-space: pre-line; max-height: 64px; overflow: hidden; }
      .ac-interact-popup { left: 8px; right: 8px; top: 8px; max-width: none; width: auto; padding: 12px; font-size: 13px; z-index: 10160; }
      /* backdrop-filter often breaks hit-testing on iOS; solid dim + card already provides contrast */
      body.ac-locked .ac-unlock-screen { backdrop-filter: none; -webkit-backdrop-filter: none; background: rgba(15,23,42,0.55); }
    }
    `);

    /* ------------------ Subsystems ------------------ */

    let __lastEventKey = '';
    let __lastEventAt = 0;
    function acSendEvent(type, obj) {
        try {
            const key = `${type}|${JSON.stringify(obj || {})}`;
            const now = Date.now();
            // Ignore immediate duplicate dispatches from overlapping listeners.
            if (key === __lastEventKey && now - __lastEventAt < 800) return;
            __lastEventKey = key;
            __lastEventAt = now;

            const path = (window.location.pathname || '').split('/').filter(Boolean);
            const idx = path.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
            if (idx < 0 || path.length < idx + 3) return;
            const classCode = path[idx + 1];
            const contentId = path[idx + 2];
            const payload = {
                resultId: window.__ac?.resultId || '',
                eventType: type,
                details: JSON.stringify(obj || {}),
                // Store per-event increment for server totals/lockouts (avoid cumulative counts exploding).
                eventCount: (obj && typeof obj.delta === 'number') ? obj.delta : 1,
                eventDuration: (obj && obj.duration) ? obj.duration : 0,
                severity: 'low',
                flagged: false
            };
            fetch(`/StudentAnswerAssessment/${classCode}/${contentId}/log-event`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
                credentials: 'same-origin'
            })
                .then(async (res) => {
                    try {
                        const data = await res.json().catch(() => null);
                        if (data && data.status === 'locked') {
                            const t = typeof data.total === 'number' ? data.total : null;
                            lockAssessmentDueToIntegrity(t);
                        }
                    } catch { /* ignore */ }
                })
                .catch(() => {});
        } catch (_) {}
    }

    function lockAssessmentDueToIntegrity(total) {
        try {
            if (window.__acIntegrityLocked) return;
            window.__acIntegrityLocked = true;

            // Hard-stop the exam UI without marking the assessment submitted.
            try { document.body.classList.add('ac-locked'); } catch (_) {}

            // Persist the lock on the server even if the student immediately navigates away.
            // We use sendBeacon/keepalive so the request can complete during page unload.
            try {
                const parts = (window.location.pathname || '').split('/').filter(Boolean);
                const idx = parts.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
                if (idx >= 0 && parts.length >= idx + 3) {
                    const classCode = parts[idx + 1];
                    const contentId = parts[idx + 2];
                    const url = `/StudentAnswerAssessment/${classCode}/${contentId}/lock-now`;
                    if (navigator && typeof navigator.sendBeacon === 'function') {
                        navigator.sendBeacon(url);
                    } else {
                        fetch(url, { method: 'POST', credentials: 'same-origin', keepalive: true }).catch(() => {});
                    }
                }
            } catch (_) {}

            const overlay = document.createElement('div');
            overlay.className = 'ac-unlock-screen';
            overlay.style.display = 'flex';
            overlay.style.alignItems = 'center';
            overlay.style.justifyContent = 'center';
            const totalText = (total == null || Number.isNaN(total)) ? 'repeated integrity alerts' : `${total} integrity alerts`;
            overlay.innerHTML = `
              <div class="ac-unlock-screen__card" role="alertdialog" aria-modal="true">
                <p style="font-size:clamp(16px,4.2vw,20px); font-weight:800; color:#0f172a; margin:0 0 0.75rem; line-height:1.35;">
                  Assessment locked
                </p>
                <p style="margin:0 0 1rem; color:#334155; line-height:1.45; font-weight:600;">
                  Too many integrity alerts were detected (${totalText}). Your answers will not be submitted automatically.
                </p>
                <p style="margin:0; color:#64748b; line-height:1.45; font-size:0.95rem;">
                  Please contact your instructor if you believe this is a mistake.
                </p>
              </div>`;
            document.body.appendChild(overlay);

            if (typeof window.showToast === 'function') {
                window.showToast('Assessment locked due to integrity alerts.');
            }

            // Return the student to the assessment details page where Open Quiz is locked.
            try {
                const parts = (window.location.pathname || '').split('/').filter(Boolean);
                const idx = parts.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
                if (idx >= 0 && parts.length >= idx + 3) {
                    const classCode = parts[idx + 1];
                    const contentId = parts[idx + 2];
                    setTimeout(() => {
                        window.location.href = `/StudentAssessment/${encodeURIComponent(classCode)}/${encodeURIComponent(contentId)}?flag=void`;
                    }, 900);
                }
            } catch (_) { /* ignore */ }
        } catch (_) {}
    }

    function checkViolationThresholdAndCloseIfNeeded() {
        try {
            const copy = window.__ac?.copy || 0;
            const paste = window.__ac?.paste || 0;
            const inspect = window.__ac?.inspect || 0;
            const printScreen = window.__ac?.print || 0;
            const tsEl = document.getElementById('ac-tabSwitchCount');
            const opEl = document.getElementById('ac-focusLossCount');
            const ssEl = document.querySelector('.ac-screen-turnoff-count');
            const tabswitch = tsEl ? parseInt(tsEl.textContent || '0', 10) : 0;
            const openprograms = opEl ? parseInt(opEl.textContent || '0', 10) : 0;
            const screenshare = ssEl ? parseInt(ssEl.textContent || '0', 10) : 0;
            // Lockout tally should match server rules.
            const total = copy + paste + inspect + printScreen + tabswitch + openprograms + screenshare;
            // Must match server rule: lock at IntegrityLockThreshold weighted events (currently 20).
            if (total >= 20) {
                lockAssessmentDueToIntegrity(total);
            }
        } catch (_) {}
    }

    /* ------------------ 1) Mouse & AFK subsystem (from mouse.js) ------------------ */
    function initMouseModule() {
        try {
            // Scoreboard
            const scoreboard = document.createElement('div');
            scoreboard.className = 'ac-status-scoreboard';
            scoreboard.setAttribute('aria-live', 'polite');
            utils.safeAppend(scoreboard);

            let mouseOffCount = 0;
            let mouseOffTime = 0;
            let mouseOffStart = null;

            let afkCount = 0;
            let afkTime = 0;
            let afkStart = null;

            let isInside = true;
            let currentMousePos = { x: 0, y: 0 };
            let lastMousePos = { x: 0, y: 0 };
            let afkRadius = 0;
            let afkCheckTimer = null;
            let afkWarningShown = false;
            let redCircleVisible = false;
            let hasMouseMoved = false;
            const AFK_WAIT_TIME = 5000;

            // create invisible circles
            const orangeCircle = document.createElement('div');
            orangeCircle.className = 'ac-afk-radius';
            document.body.appendChild(orangeCircle);

            const redCircle = document.createElement('div');
            redCircle.className = 'ac-afk-confirmed';
            document.body.appendChild(redCircle);

            function showCircle(circle, x, y, radius) {
                circle.style.width = `${radius * 2}px`;
                circle.style.height = `${radius * 2}px`;
                circle.style.left = `${x - radius}px`;
                circle.style.top = `${y - radius}px`;
                circle.style.display = "block";
            }
            function hideCircle(circle) { circle.style.display = "none"; }

            function updateScoreboard() {
                const totalMouseOff = mouseOffTime + (mouseOffStart ? Date.now() - mouseOffStart : 0);
                const totalAfk = afkTime + (afkStart ? Date.now() - afkStart : 0);
                scoreboard.textContent = `Mouse ON/OFF Score
  • Mouse left for ${utils.formatTimeMs(totalMouseOff)} For ${mouseOffCount} times 
  • Afk for ${utils.formatTimeMs(totalAfk)} For ${afkCount} times`;
            }

            setInterval(updateScoreboard, 1000);

            // detect mouse leaving page
            window.addEventListener('mouseout', (e) => {
                // similar heuristics as original
                if ((!e.relatedTarget && e.clientY <= 0) || (!e.relatedTarget || !e.relatedTarget.nodeName)) {
                    if (isInside) {
                        isInside = false;
                        mouseOffStart = Date.now();
                        mouseOffCount++;
                        updateScoreboard();
                    }
                }
            });

            window.addEventListener('mouseover', () => {
                if (!isInside) {
                    isInside = true;
                    if (mouseOffStart) {
                        mouseOffTime += Date.now() - mouseOffStart;
                        mouseOffStart = null;
                        updateScoreboard();
                    }
                }
            });

            function startAfkTimer(pos) {
                clearTimeout(afkCheckTimer);
                afkRadius = Math.max(1, Math.min(window.innerWidth, window.innerHeight)) * 0.1;
                lastMousePos = { x: pos.x, y: pos.y };
                showCircle(orangeCircle, pos.x, pos.y, afkRadius);
                hideCircle(redCircle);
                redCircleVisible = false;

                afkCheckTimer = setTimeout(() => {
                    if (afkWarningShown) return;
                    const dx = currentMousePos.x - lastMousePos.x;
                    const dy = currentMousePos.y - lastMousePos.y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    if (dist <= afkRadius) {
                        afkStart = Date.now();
                        afkCount++;
                        afkWarningShown = true;
                        const redRadius = afkRadius * 1.05;
                        showCircle(redCircle, currentMousePos.x, currentMousePos.y, redRadius);
                        redCircleVisible = true;
                        updateScoreboard();
                    } else {
                        startAfkTimer(currentMousePos);
                    }
                }, AFK_WAIT_TIME);
            }

            document.addEventListener('mousemove', (e) => {
                if (!hasMouseMoved) {
                    hasMouseMoved = true;
                    isInside = true;
                }
                currentMousePos = { x: e.clientX, y: e.clientY };
                const dx = currentMousePos.x - lastMousePos.x;
                const dy = currentMousePos.y - lastMousePos.y;
                const dist = Math.sqrt(dx * dx + dy * dy);

                if (dist > afkRadius) {
                    if (afkStart) {
                        afkTime += Date.now() - afkStart;
                        afkStart = null;
                    }
                    afkWarningShown = false;
                    hideCircle(orangeCircle);
                    if (redCircleVisible) {
                        const redRadius = afkRadius * 1.05;
                        const dxRed = currentMousePos.x - lastMousePos.x;
                        const dyRed = currentMousePos.y - lastMousePos.y;
                        const distRed = Math.sqrt(dxRed * dxRed + dyRed * dyRed);
                        if (distRed > redRadius) {
                            hideCircle(redCircle);
                            redCircleVisible = false;
                        }
                    }
                    updateScoreboard();
                    startAfkTimer(currentMousePos);
                }
            });

            // init: seed timer at center
            startAfkTimer({ x: window.innerWidth / 2, y: window.innerHeight / 2 });

            // expose minimal debug
            return { name: 'mouse', scoreboard, orangeCircle, redCircle };
        } catch (err) {
            console.error('AntiCheat.mouse module failed to init', err);
            return null;
        }
    }

    /* ------------------ 2) Interaction popup (copy/inspect/printscreen & mouse behavior) (from interact.js) ------------------ */
    function initInteractModule() {
        try {
            // Create popup
            const popup = document.createElement('div');
            popup.className = 'ac-interact-popup';
            popup.innerHTML = `
          <h3 style="margin:0 0 8px 0;">Interaction Score</h3>
          <p class="ac-scoreText" style="margin:0 0 10px 0; font-weight:700;">• Score: 0</p>
          <p class="ac-copyText">• Copy paste record: 0</p>
          <p class="ac-inspectText">• Inspect record: 0</p>
          <p class="ac-printScreenText">• Print Screen record: 0</p>
          <p class="ac-mouseBehaviorText">• Mouse Behavior: 0</p>
        `;
            utils.safeAppend(popup);
            if (!window.__ac) window.__ac = {};
            window.__ac.startTs = Date.now();

            // Counters
            const seed = (window.__ac && window.__ac.seedTotals) ? window.__ac.seedTotals : null;
            let copyCount = seed && typeof seed.copy === 'number' ? seed.copy : 0;
            let pasteCount = seed && typeof seed.paste === 'number' ? seed.paste : 0;
            let inspectCount = seed && typeof seed.inspect === 'number' ? seed.inspect : 0;
            let printScreenCount = seed && typeof seed.print === 'number' ? seed.print : 0;
            let mouseBehaviorCount = seed && typeof seed.mouse === 'number' ? seed.mouse : 0;
            let lastMouseMoveTs = 0;
            let lastClipboardEventTs = 0;

            const scoreText = popup.querySelector('.ac-scoreText');
            const copyText = popup.querySelector('.ac-copyText');
            const inspectText = popup.querySelector('.ac-inspectText');
            const printScreenText = popup.querySelector('.ac-printScreenText');
            const mouseBehaviorText = popup.querySelector('.ac-mouseBehaviorText');

            function getIntFromElText(idOrSelector) {
                const el = idOrSelector.startsWith('#') || idOrSelector.startsWith('.')
                    ? document.querySelector(idOrSelector)
                    : document.getElementById(idOrSelector);
                if (!el) return 0;
                const n = parseInt((el.textContent || '0').trim(), 10);
                return isNaN(n) ? 0 : n;
            }

            function computeInteractionScore() {
                // Deterministic score derived from counters (no time-based inflation).
                const tabswitch = getIntFromElText('ac-tabSwitchCount');
                const focusloss = getIntFromElText('ac-focusLossCount');
                const screenOff = getIntFromElText('.ac-screen-turnoff-count');

                const score =
                    ((copyCount + pasteCount) * 2) +
                    (inspectCount * 1) +
                    (printScreenCount * 1) +
                    (mouseBehaviorCount * 0.25) +
                    (tabswitch * 2) +
                    (focusloss * 2) +
                    (screenOff * 3);

                return Math.round(score * 100) / 100; // keep 2 decimals if any
            }

            function syncScoreUI() {
                const score = computeInteractionScore();
                window.__ac.copy = copyCount;
                window.__ac.paste = pasteCount;
                window.__ac.inspect = inspectCount;
                window.__ac.print = printScreenCount;
                window.__ac.mouse = mouseBehaviorCount;
                window.__ac.score = score;
                if (scoreText) scoreText.textContent = `• Score: ${score}`;
            }

            // Keep score fresh even when focus module updates counters.
            setInterval(syncScoreUI, 1000);
            // Seed initial display
            try {
                if (copyText) copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                if (inspectText) inspectText.innerText = `• Inspect record: ${inspectCount}`;
                if (printScreenText) printScreenText.innerText = `• Print Screen record: ${printScreenCount}`;
                if (mouseBehaviorText) mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCount}`;
                syncScoreUI();
            } catch (_) { }

            document.addEventListener('copy', () => {
                lastClipboardEventTs = Date.now();
                copyCount++;
                copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                syncScoreUI();
                acSendEvent('copy_paste', { action: 'copy', delta: 1, count: copyCount });
                checkViolationThresholdAndCloseIfNeeded();
            });

            document.addEventListener('paste', () => {
                lastClipboardEventTs = Date.now();
                pasteCount++;
                copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                syncScoreUI();
                acSendEvent('copy_paste', { action: 'paste', delta: 1, count: pasteCount });
                checkViolationThresholdAndCloseIfNeeded();
            });

            /* Same-origin assessment iframe (e.g. /materials/forms/*.html) — StudentAnswerAssessment.js dispatches these.
               Cross-origin embeds (Google Forms) cannot be wired (browser security); events stay inside the iframe. */
            document.addEventListener('ac-assessment-embed', (ev) => {
                const kind = ev && ev.detail && ev.detail.kind;
                if (!kind) return;
                if (kind === 'copy') {
                    lastClipboardEventTs = Date.now();
                    copyCount++;
                    if (copyText) copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                    syncScoreUI();
                    acSendEvent('copy_paste', { action: 'copy', delta: 1, count: copyCount, source: 'embedded_form' });
                    checkViolationThresholdAndCloseIfNeeded();
                } else if (kind === 'paste') {
                    lastClipboardEventTs = Date.now();
                    pasteCount++;
                    if (copyText) copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                    syncScoreUI();
                    acSendEvent('copy_paste', { action: 'paste', delta: 1, count: pasteCount, source: 'embedded_form' });
                    checkViolationThresholdAndCloseIfNeeded();
                } else if (kind === 'contextmenu' || kind === 'inspect_key') {
                    inspectCount++;
                    if (inspectText) inspectText.innerText = `• Inspect record: ${inspectCount}`;
                    syncScoreUI();
                    acSendEvent('inspect', { delta: 1, count: 1, source: 'embedded_form' });
                    checkViolationThresholdAndCloseIfNeeded();
                } else if (kind === 'print_screen') {
                    printScreenCount++;
                    if (printScreenText) printScreenText.innerText = `• Print Screen record: ${printScreenCount}`;
                    syncScoreUI();
                    acSendEvent('print_screen', { count: printScreenCount, source: 'embedded_form' });
                }
            });

            // contextmenu used as heuristic for inspect / right-click
            document.addEventListener('contextmenu', (e) => {
                inspectCount++;
                inspectText.innerText = `• Inspect record: ${inspectCount}`;
                syncScoreUI();
                acSendEvent('inspect', { delta: 1, count: 1 });
                checkViolationThresholdAndCloseIfNeeded();
            });

            document.addEventListener('keydown', (e) => {
                // 'PrintScreen' key
                if (e.key === 'PrintScreen' || e.code === 'PrintScreen') {
                    printScreenCount++;
                    printScreenText.innerText = `• Print Screen record: ${printScreenCount}`;
                    syncScoreUI();
                    acSendEvent('print_screen', { count: printScreenCount });
                }
                const k = e.key.toLowerCase();
                if (k === 'f12' || (e.ctrlKey && e.shiftKey && k === 'i')) {
                    inspectCount++;
                    inspectText.innerText = `• Inspect record: ${inspectCount}`;
                    syncScoreUI();
                    acSendEvent('inspect', { delta: 1, count: 1 });
                    checkViolationThresholdAndCloseIfNeeded();
                }
            });

            // Heuristic for Ctrl/Cmd + C/V when clipboard events are blocked
            document.addEventListener('keyup', (e) => {
                const k = (e.key || '').toLowerCase();
                // Clipboard events already captured copy/paste; avoid duplicate logs.
                if (Date.now() - lastClipboardEventTs < 700) return;
                if ((e.ctrlKey || e.metaKey) && k === 'c') {
                    copyCount++;
                    copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                    syncScoreUI();
                    acSendEvent('copy_paste', { action: 'copy', delta: 1, count: copyCount });
                    checkViolationThresholdAndCloseIfNeeded();
                } else if ((e.ctrlKey || e.metaKey) && k === 'v') {
                    pasteCount++;
                    copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                    syncScoreUI();
                    acSendEvent('copy_paste', { action: 'paste', delta: 1, count: pasteCount });
                    checkViolationThresholdAndCloseIfNeeded();
                }
            });

            // Mouse behavior detection heuristic: track bounding boxes of elements that set cursor style
            // Start from server-seeded value so reopen preserves counts.
            let mouseBehaviorCounter = mouseBehaviorCount;
            const cursorHistory = new Map();

            const detectInterval = setInterval(() => {
                try {
                    const elements = document.querySelectorAll('[style*="cursor"]');
                    let detected = 0;
                    elements.forEach((el) => {
                        const rect = el.getBoundingClientRect();
                        const id = el.dataset.acCursorId || Math.random().toString(36).slice(2);
                        el.dataset.acCursorId = id;
                        const prev = cursorHistory.get(id) || [];
                        const current = { x: Math.round(rect.left), y: Math.round(rect.top), t: Date.now() };
                        prev.push(current);
                        if (prev.length > 10) prev.shift();
                        cursorHistory.set(id, prev);

                        if (prev.length >= 3) {
                            const dx1 = prev[1].x - prev[0].x;
                            const dy1 = prev[1].y - prev[0].y;
                            const dx2 = prev[2].x - prev[1].x;
                            const dy2 = prev[2].y - prev[1].y;
                            const sameDir = dx1 === dx2 && dy1 === dy2;
                            const linearMotion = (dx1 === 0 || dy1 === 0 || dx1 === dy1 || dx1 === -dy1);
                            if (sameDir && linearMotion) detected++;
                        }
                    });
                    if (detected > 0) {
                        mouseBehaviorCounter++;
                        mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCounter}`;
                        mouseBehaviorCount = mouseBehaviorCounter;
                        syncScoreUI();
                    }
                } catch (e) {
                    // don't break if DOM has issues
                }
            }, 500);

            document.addEventListener('wheel', () => {
                mouseBehaviorCount++;
                mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCount}`;
                window.__ac.mouse = mouseBehaviorCount;
                acSendEvent('mouse_activity', { delta: 1, count: mouseBehaviorCount });
            }, { passive: true });

            document.addEventListener('mousemove', () => {
                const now = Date.now();
                if (now - lastMouseMoveTs > 300) {
                    mouseBehaviorCount++;
                    mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCount}`;
                    syncScoreUI();
                    lastMouseMoveTs = now;
                    acSendEvent('mouse_activity', { delta: 1, count: mouseBehaviorCount });
                }
            }, { passive: true });

            let lastSummarySentAt = 0;
            function sendSummary() {
                try {
                    const now = Date.now();
                    if (now - lastSummarySentAt < 50000) return;
                    lastSummarySentAt = now;
                    const path = (window.location.pathname || '').split('/').filter(Boolean);
                    const idx = path.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
                    if (idx < 0 || path.length < idx + 3) return;
                    const classCode = path[idx + 1];
                    const contentId = path[idx + 2];
                    const duration = Math.floor(((Date.now() - (window.__ac.startTs || Date.now())))/1000);
                    // Ensure the score is computed from the latest counters before summarizing.
                    syncScoreUI();
                    const details = {
                        copyPasteCount: window.__ac.copy || 0,
                        inspectCount: window.__ac.inspect || 0,
                        printScreenCount: window.__ac.print || 0,
                        mouseBehaviorCount: window.__ac.mouse || 0,
                        interactionScore: window.__ac.score || 0,
                        durationSeconds: duration
                    };
                    const payload = {
                        resultId: window.__ac?.resultId || '',
                        eventType: 'summary',
                        details: JSON.stringify(details),
                        eventCount: 1,
                        eventDuration: duration,
                        severity: 'low',
                        flagged: false
                    };
                    fetch(`/StudentAnswerAssessment/${classCode}/${contentId}/log-event`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(payload),
                        credentials: 'same-origin'
                    }).catch(() => {});
                } catch (_) {}
            }

            window.addEventListener('beforeunload', sendSummary);
            setInterval(sendSummary, 60000);

            

            return { name: 'interact', popup, stop: () => clearInterval(detectInterval) };
        } catch (err) {
            console.error('AntiCheat.interact module failed to init', err);
            return null;
        }
    }

    /* ------------------ 3) Fullscreen / Locking subsystem (from fullscreen.js) ------------------ */
    function initFullscreenModule() {
        try {
            let fullscreenStartTime = null;
            let fullscreenTotalTime = 0;
            let leftFullscreenCount = 0;
            let leftFullscreenTime = 0;
            let leftStart = null;

            function docFullscreenActive() {
                return !!(document.fullscreenElement || document.webkitFullscreenElement);
            }
            function fullscreenApiAvailable() {
                const el = document.documentElement;
                return typeof el.requestFullscreen === 'function' || typeof el.webkitRequestFullscreen === 'function';
            }
            /**
             * Mobile browsers (iOS Safari, Android Chrome, etc.) do not support real “page fullscreen”
             * the way desktop Chrome/Edge do — the API is missing, blocked, or not useful for locking the exam.
             * Only desktop/laptop should show the “Enter fullscreen” gate.
             */
            function preferSkipFullscreenLock() {
                const ua = (navigator.userAgent || '').toLowerCase();
                if (/android|iphone|ipad|ipod|webos|blackberry|iemobile|opera mini/i.test(ua)) return true;
                if (typeof navigator.platform === 'string' && navigator.platform === 'MacIntel' && typeof navigator.maxTouchPoints === 'number' && navigator.maxTouchPoints > 1)
                    return true;
                try {
                    if (navigator.userAgentData && navigator.userAgentData.mobile === true) return true;
                } catch (_) { /* ignore */ }
                if (!fullscreenApiAvailable()) return true;
                return false;
            }
            const skipFsLock = preferSkipFullscreenLock();

            // Stats overlay
            const fullscreenStats = document.createElement('div');
            fullscreenStats.className = 'ac-fullscreen-stats';
            fullscreenStats.innerHTML = `
          <div>
            • Full screen for <span class="ac-fullscreen-duration">0m, 0s</span><br>
            • Left full screen <span class="ac-fullscreen-leave-count">0</span> times<br>
            • Left full screen for a total <span class="ac-fullscreen-leave-secondscount">0m, 0s</span>
          </div>
        `;
            utils.safeAppend(fullscreenStats);

            // Unlock overlay
            const lockScreen = document.createElement('div');
            lockScreen.className = 'ac-unlock-screen';
            lockScreen.id = 'ac-unlock-screen';
            lockScreen.innerHTML = `
          <div class="ac-unlock-screen__card" role="alertdialog" aria-modal="true" aria-labelledby="ac-unlock-title">
            <p id="ac-unlock-title" style="font-size:clamp(16px,4.2vw,20px); font-weight:600; color:#0f172a; margin:0 0 1.1rem; line-height:1.35;">Exam locked — use fullscreen to continue</p>
            <button type="button" id="ac-enter-fullscreen-btn" class="ac-button">Enter fullscreen</button>
          </div>
        `;
            utils.safeAppend(lockScreen);

            function updateStatsDisplay() {
                const now = Date.now();
                const activeTime = fullscreenStartTime ? Math.floor((now - fullscreenStartTime) / 1000) : 0;
                const totalTime = fullscreenTotalTime + activeTime;
                const totalLeftTime = leftFullscreenTime + (leftStart ? Math.floor((now - leftStart) / 1000) : 0);

                const durEl = fullscreenStats.querySelector('.ac-fullscreen-duration');
                const leaveCountEl = fullscreenStats.querySelector('.ac-fullscreen-leave-count');
                const leaveSecEl = fullscreenStats.querySelector('.ac-fullscreen-leave-secondscount');

                if (durEl) durEl.textContent = utils.formatTimeSec(totalTime);
                if (leaveCountEl) leaveCountEl.textContent = leftFullscreenCount;
                if (leaveSecEl) leaveSecEl.textContent = utils.formatTimeSec(totalLeftTime);
            }

            setInterval(updateStatsDisplay, 1000);

            function lockPage() {
                if (skipFsLock) return;
                const firstLock = !document.body.classList.contains('ac-locked');
                if (firstLock) {
                    document.body.classList.add('ac-locked');
                    if (fullscreenStartTime) {
                        fullscreenTotalTime += Math.floor((Date.now() - fullscreenStartTime) / 1000);
                        fullscreenStartTime = null;
                    }
                    leftStart = Date.now();
                    leftFullscreenCount++;
                }
                try { document.body.appendChild(lockScreen); } catch (_) { /* keep in DOM */ }
                lockScreen.style.display = 'flex';
                lockScreen.style.alignItems = 'center';
                lockScreen.style.justifyContent = 'center';
            }
            function unlockPage() {
                document.body.classList.remove('ac-locked');
                lockScreen.style.display = 'none';
                fullscreenStartTime = Date.now();
                if (leftStart) {
                    leftFullscreenTime += Math.floor((Date.now() - leftStart) / 1000);
                    leftStart = null;
                }
            }
            let lastFsAttempt = 0;
            function enterFullScreen() {
                const now = Date.now();
                if (now - lastFsAttempt < 500) return;
                lastFsAttempt = now;
                const el = document.documentElement;
                if (typeof el.requestFullscreen === 'function') {
                    el.requestFullscreen().catch(() => { /* ignore */ });
                } else if (typeof el.webkitRequestFullscreen === 'function') {
                    el.webkitRequestFullscreen();
                }
            }

            const btn = lockScreen.querySelector('#ac-enter-fullscreen-btn');
            if (btn) {
                const onActivate = (e) => {
                    e.stopPropagation();
                    if (e.cancelable) e.preventDefault();
                    enterFullScreen();
                };
                btn.addEventListener('pointerup', onActivate, { capture: true });
                btn.addEventListener('click', onActivate, { capture: true });
            }

            function syncLockFromFullscreen() {
                if (skipFsLock) return;
                if (docFullscreenActive()) unlockPage();
                else lockPage();
            }

            document.addEventListener('fullscreenchange', syncLockFromFullscreen);
            document.addEventListener('webkitfullscreenchange', syncLockFromFullscreen);

            document.addEventListener('visibilitychange', () => {
                if (skipFsLock) return;
                if (document.hidden) lockPage();
                else syncLockFromFullscreen();
            });

            // On load: wrap page content in page-content id if not present
            window.addEventListener('load', () => {
                if (!document.getElementById('page-content')) {
                    const wrapper = document.createElement('div');
                    wrapper.id = 'page-content';
                    // Move all nodes except our fullscreen stats & lock screen into wrapper
                    const preserve = new Set([fullscreenStats, lockScreen]);
                    while (document.body.firstChild) {
                        if (preserve.has(document.body.firstChild)) {
                            document.body.appendChild(document.body.firstChild); // keep in order
                            break;
                        }
                        wrapper.appendChild(document.body.firstChild);
                    }
                    // Insert wrapper at top of body
                    document.body.insertBefore(wrapper, document.body.firstChild);
                }
                if (!skipFsLock) lockPage();
            });

            return { name: 'fullscreen', fullscreenStats, lockScreen };
        } catch (err) {
            console.error('AntiCheat.fullscreen module failed to init', err);
            return null;
        }
    }

    /* ------------------ 4) Focus / Screen-share monitoring (from focus.js & parts of focus module) ------------------ */
    function preferMobileScreenShareUI() {
        const ua = (navigator.userAgent || '').toLowerCase();
        if (/android|iphone|ipad|ipod|webos|blackberry|iemobile|opera mini/i.test(ua)) return true;
        if (typeof navigator.platform === 'string' && navigator.platform === 'MacIntel' && typeof navigator.maxTouchPoints === 'number' && navigator.maxTouchPoints > 1)
            return true;
        try {
            if (navigator.userAgentData && navigator.userAgentData.mobile === true) return true;
        } catch (_) { /* ignore */ }
        // Chrome DevTools “device” mode often keeps a desktop User-Agent while resizing the viewport.
        // Phone-sized width + coarse pointer or no-hover matches emulated phones, not mouse laptops (fine pointer + hover).
        try {
            if (typeof window.matchMedia === 'function') {
                const narrow = window.matchMedia('(max-width: 480px)').matches;
                const touchLike = window.matchMedia('(pointer: coarse)').matches || window.matchMedia('(hover: none)').matches;
                if (narrow && touchLike) return true;
            }
        } catch (_) { /* ignore */ }
        return false;
    }

    function initFocusModule() {
        try {
            const useMobilePrompt = preferMobileScreenShareUI();
            const canGetDisplayMedia = !!(navigator.mediaDevices && typeof navigator.mediaDevices.getDisplayMedia === 'function');

            // Instead of document.write we create DOM nodes
            const overlay = document.createElement('div');
            overlay.className = 'ac-screenPromptOverlay' + (useMobilePrompt ? ' ac-screenPromptOverlay--mobile' : '');
            overlay.id = 'ac-screenPromptOverlay';
            overlay.innerHTML = `
          <div class="ac-screen-prompt-stack">
            <div id="ac-screen-prompt-desktop" class="ac-screen-prompt-panel"${useMobilePrompt ? ' hidden' : ''}>
              <button type="button" id="ac-startScreenShare" class="ac-btn-screen-primary">Share your entire screen</button>
              <p class="ac-screen-prompt-hint">When your browser asks, choose <strong>Entire screen</strong>, then come back to this tab.</p>
              <button type="button" id="ac-continueNoShareDesktop" class="ac-btn-screen-secondary"${canGetDisplayMedia ? ' hidden' : ''}>Continue without screen share</button>
            </div>
            <div id="ac-screen-prompt-mobile" class="ac-screen-prompt-panel"${useMobilePrompt ? '' : ' hidden'}>
              <p class="ac-screen-prompt-hint ac-screen-prompt-lead">Phones and tablets usually cannot share the whole screen from the browser. You can still take the exam with tab and focus monitoring only (no screen recording).</p>
              <button type="button" id="ac-continueMobileNoShare" class="ac-btn-screen-primary">Continue to the exam</button>
              <button type="button" id="ac-tryScreenShareMobile" class="ac-btn-screen-secondary"${canGetDisplayMedia ? '' : ' hidden'}>Try screen share anyway</button>
            </div>
            <p id="ac-screenShareError" class="ac-screen-prompt-error" role="alert" hidden></p>
          </div>
        `;
            utils.safeAppend(overlay);

            const errEl = overlay.querySelector('#ac-screenShareError');
            function clearScreenShareError() {
                if (!errEl) return;
                errEl.textContent = '';
                errEl.hidden = true;
            }
            function showScreenShareError(msg) {
                if (!errEl) return;
                errEl.textContent = msg;
                errEl.hidden = !msg;
            }

            const studentStatus = document.createElement('div');
            studentStatus.id = 'ac-studentStatus';
            studentStatus.className = 'ac-studentStatus ac-green';
            studentStatus.textContent = '🟢 Focused Student';
            studentStatus.style.opacity = '1';
            utils.safeAppend(studentStatus);

            // logs box
            const logsBox = document.createElement('div');
            logsBox.className = 'ac-logsBox';
            logsBox.innerHTML = `<table style="width:100%; border-collapse:collapse;"><thead><tr><th style="text-align:left; color:#ccc;">LOGS</th><th style="text-align:left; color:#ccc;">OFFENSE</th><th style="text-align:left; color:#ccc;">TIME</th></tr></thead><tbody id="ac-logsBody"></tbody></table>`;
            utils.safeAppend(logsBox);
            const logsBody = logsBox.querySelector('#ac-logsBody');

            const cheatMetrics = document.createElement('div');
            cheatMetrics.className = 'ac-cheatMetrics';
            cheatMetrics.innerHTML = `
          <div>Screen Share Off Count: <span class="ac-screen-turnoff-count">0</span></div>
          <div>Total Time Screen Share Was Off: <span class="ac-screen-turnoff-duration">0s</span></div>
        `;
            utils.safeAppend(cheatMetrics);

            const omniFocusBox = document.createElement('div');
            omniFocusBox.className = 'ac-omniFocusBox';
            omniFocusBox.innerHTML = `
          <strong style="display:block; margin-bottom:10px; color:#ffd700;">Omni Focus anti cheat</strong>
          <div>• Tab switching Count <span id="ac-tabSwitchCount">0</span> times</div>
          <div>• Open Programs Count <span id="ac-focusLossCount">0</span> times</div>
        `;
            utils.safeAppend(omniFocusBox);

            // State
            const seed = (window.__ac && window.__ac.seedTotals) ? window.__ac.seedTotals : null;
            const state = {
                focusLossEvents: [],
                tabSwitchEvents: [],
                seedFocusLossCount: seed && typeof seed.openPrograms === 'number' ? seed.openPrograms : 0,
                seedTabSwitchCount: seed && typeof seed.tabSwitch === 'number' ? seed.tabSwitch : 0,
                lastOffenseTime: null,
                lastLackFocusTime: null
            };

            let focusStart = null;
            let tabSwitchStart = null;
            let monitoringActive = false;
            let graceEnded = false;
            let stream = null;

            // screen off metrics
            let screenOffCount = 0;
            let screenOffStart = null;
            let totalScreenOffDuration = 0;
            const updateScreenOffMetrics = () => {
                const spanCount = cheatMetrics.querySelector('.ac-screen-turnoff-count');
                const spanDur = cheatMetrics.querySelector('.ac-screen-turnoff-duration');
                if (spanCount) spanCount.textContent = screenOffCount;
                const duration = screenOffStart ? totalScreenOffDuration + Math.floor((Date.now() - screenOffStart) / 1000) : totalScreenOffDuration;
                if (spanDur) spanDur.textContent = `${duration}s`;
            };
            setInterval(updateScreenOffMetrics, 1000);

            function onScreenShareStopped() {
                screenOffCount++;
                screenOffStart = Date.now();
                updateScreenOffMetrics();
                acSendEvent('screen_share', { on: false });
                checkViolationThresholdAndCloseIfNeeded();
            }
            function onScreenShareResumed(extra) {
                if (screenOffStart) {
                    const duration = Math.floor((Date.now() - screenOffStart) / 1000);
                    totalScreenOffDuration += duration;
                    screenOffStart = null;
                    updateScreenOffMetrics();
                }
                acSendEvent('screen_share', Object.assign({ on: true }, extra && typeof extra === 'object' ? extra : {}));
                checkViolationThresholdAndCloseIfNeeded();
            }

            const videoElement = document.createElement('video');
            videoElement.id = 'ac-screenVideo';
            videoElement.autoplay = true;
            videoElement.style.display = 'none';
            document.body.appendChild(videoElement);

            const colorClasses = ['ac-green', 'ac-blue', 'ac-yellow', 'ac-orange', 'ac-red'];

            function updateStatus() {
                const totalEvents = state.focusLossEvents.length + state.tabSwitchEvents.length;
                const totalTime = [...state.focusLossEvents, ...state.tabSwitchEvents].reduce((a, b) => a + b, 0);
                const maxSingle = Math.max(...state.focusLossEvents, ...state.tabSwitchEvents, 0);

                let label = 'Focused Student', color = 'ac-green', emoji = '🟢';
                if (maxSingle >= 39 || totalTime > 78 || totalEvents > 13)
                    [emoji, label, color] = ['🔴', 'Marked as Cheating', 'ac-red'];
                else if (maxSingle >= 26 || totalTime > 39 || totalEvents > 8)
                    [emoji, label, color] = ['🟠', 'Suspicious Activity Detected', 'ac-orange'];
                else if (maxSingle >= 13 || totalEvents > 4)
                    [emoji, label, color] = ['🟡', 'Minor Activity Detected', 'ac-yellow'];
                else if (totalEvents > 0)
                    [emoji, label, color] = ['🔵', 'Lacking Focus', 'ac-blue'];

                studentStatus.textContent = `${emoji} ${label}`;
                colorClasses.forEach(c => studentStatus.classList.remove(c));
                studentStatus.classList.add(color);
            }

            function captureScreenshot() {
                if (!videoElement || videoElement.videoWidth === 0) return;
                const canvas = document.createElement('canvas');
                canvas.width = videoElement.videoWidth;
                canvas.height = videoElement.videoHeight;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
                const img = new Image();
                img.src = canvas.toDataURL('image/png');
                img.style.display = 'none';
                document.body.appendChild(img);
            }

            const getElapsedSeconds = (start) => Math.round((Date.now() - start) / 1000);

            const updateOmniCounts = () => {
                const t = document.getElementById('ac-tabSwitchCount');
                const f = document.getElementById('ac-focusLossCount');
                if (t) t.textContent = state.seedTabSwitchCount + state.tabSwitchEvents.length;
                if (f) f.textContent = state.seedFocusLossCount + state.focusLossEvents.length;
            };
            updateOmniCounts();

            function appendLog(duration, type, time) {
                try {
                    const tr = document.createElement('tr');
                    tr.innerHTML = `<td style="padding:4px 6px; border-bottom:1px solid #555;">${duration}s</td><td style="padding:4px 6px; border-bottom:1px solid #555;">${type}</td><td style="padding:4px 6px; border-bottom:1px solid #555;">${time}</td>`;
                    logsBody.appendChild(tr);
                } catch (e) { /* ignore */ }
            }

            function handleFocusLossEnd() {
                if (!monitoringActive || !graceEnded) return;
                const duration = getElapsedSeconds(focusStart);
                const dur = duration > 0 ? duration : 1;
                state.focusLossEvents.push(dur);
                appendLog(dur, 'window focus', new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }).toLowerCase());
                if (dur >= 10) captureScreenshot();
                state.lastOffenseTime = Date.now();
                updateStatus();
                updateOmniCounts();
                acSendEvent('open_programs', { duration: dur, count: 1 });
                focusStart = null;
                checkViolationThresholdAndCloseIfNeeded();
            }

            function handleTabSwitchEnd() {
                if (!monitoringActive || !graceEnded) return;
                const duration = getElapsedSeconds(tabSwitchStart);
                const dur = duration > 0 ? duration : 1;
                state.tabSwitchEvents.push(dur);
                appendLog(dur, 'tabswitching', new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }).toLowerCase());
                if (dur >= 10) captureScreenshot();
                state.lastOffenseTime = Date.now();
                updateStatus();
                updateOmniCounts();
                acSendEvent('tab_switch', { duration: dur, count: 1 });
                tabSwitchStart = null;
                checkViolationThresholdAndCloseIfNeeded();
            }

            window.addEventListener('blur', () => {
                if (monitoringActive && !focusStart) focusStart = Date.now();
            });
            window.addEventListener('focus', () => {
                if (monitoringActive && focusStart) handleFocusLossEnd();
            });
            // Full-page leave (back, close, in-page nav): not the same as switching tabs. Without this,
            // pagehide/pageshow + visibility would log false "cheat" events when the student uses back then returns.
            window.addEventListener('pagehide', () => {
                if (!monitoringActive) return;
                tabSwitchStart = null;
                focusStart = null;
            });
            window.addEventListener('pageshow', (e) => {
                if (!monitoringActive) return;
                if (e.persisted) {
                    tabSwitchStart = null;
                    focusStart = null;
                }
            });

            document.addEventListener('visibilitychange', () => {
                if (!monitoringActive) return;
                if (document.hidden && !tabSwitchStart) {
                    tabSwitchStart = Date.now();
                    if (!focusStart) focusStart = Date.now();
                } else if (!document.hidden && tabSwitchStart) {
                    handleTabSwitchEnd();
                }
            });

            setInterval(() => {
                if (!graceEnded) return;
                if (state.lastOffenseTime && (Date.now() - state.lastOffenseTime > 30000)) {
                    if (state.lastLackFocusTime && (Date.now() - state.lastLackFocusTime > 30000)) {
                        state.focusLossEvents = [];
                        state.tabSwitchEvents = [];
                        updateStatus();
                        updateOmniCounts();
                    }
                }
            }, 1000);

            function startGraceAfterConsent() {
                setTimeout(() => {
                    graceEnded = true;
                    studentStatus.style.opacity = '1';
                }, 10000);
            }

            function beginNoCaptureMonitoring(modeDetail) {
                clearScreenShareError();
                if (stream) {
                    try {
                        stream.getTracks().forEach((t) => t.stop());
                    } catch (_) { /* ignore */ }
                    stream = null;
                }
                videoElement.srcObject = null;
                onScreenShareResumed(modeDetail || { mode: 'no_screen_capture' });
                overlay.style.display = 'none';
                monitoringActive = true;
                graceEnded = false;
                studentStatus.style.opacity = '0';
                startGraceAfterConsent();
            }

            async function requestScreenShare() {
                clearScreenShareError();
                if (!navigator.mediaDevices || typeof navigator.mediaDevices.getDisplayMedia !== 'function') {
                    showScreenShareError('Screen sharing is not available in this browser.');
                    return;
                }
                try {
                    const mediaStream = await navigator.mediaDevices.getDisplayMedia({ video: true });
                    stream = mediaStream;
                    videoElement.srcObject = stream;
                    onScreenShareResumed({ mode: 'display_media' });
                    overlay.style.display = 'none';
                    monitoringActive = true;
                    graceEnded = false;
                    studentStatus.style.opacity = '0';

                    const track = stream.getVideoTracks()[0];
                    const endedHandler = () => {
                        onScreenShareStopped();
                        overlay.style.display = 'flex';
                        monitoringActive = false;
                        graceEnded = false;
                        studentStatus.style.opacity = '0';
                        videoElement.srcObject = null;
                        stream = null;
                        if (track) track.removeEventListener('ended', endedHandler);
                    };
                    if (track) {
                        track.addEventListener('ended', endedHandler);
                        try { stream.addEventListener('inactive', endedHandler); } catch (_) { /* ignore */ }
                    }

                    startGraceAfterConsent();
                } catch (err) {
                    const denied = err && err.name === 'NotAllowedError';
                    showScreenShareError(denied ? 'Screen share was cancelled or blocked.' : 'Could not start screen share on this device.');
                }
            }

            const shareBtn = overlay.querySelector('#ac-startScreenShare');
            if (shareBtn) shareBtn.addEventListener('click', requestScreenShare);

            const continueMobile = overlay.querySelector('#ac-continueMobileNoShare');
            if (continueMobile) continueMobile.addEventListener('click', () => beginNoCaptureMonitoring({ mode: 'mobile_limited' }));

            const tryMobile = overlay.querySelector('#ac-tryScreenShareMobile');
            if (tryMobile) tryMobile.addEventListener('click', requestScreenShare);

            const continueDesktop = overlay.querySelector('#ac-continueNoShareDesktop');
            if (continueDesktop) continueDesktop.addEventListener('click', () => beginNoCaptureMonitoring({ mode: 'desktop_no_display_api' }));

            return { name: 'focus', overlay, studentStatus, requestScreenShare };
        } catch (err) {
            console.error('AntiCheat.focus module failed to init', err);
            return null;
        }
    }

    /* ------------------ Initialization & safe boot ------------------ */
    NS.modules = {};

    function tryInit(name, initFn) {
        try {
            const res = initFn();
            NS.modules[name] = res;
        } catch (e) {
            console.error(`AntiCheat: failed to initialize ${name}`, e);
            NS.modules[name] = null;
        }
    }

    function initAll() {
        tryInit('mouse', initMouseModule);
        tryInit('interact', initInteractModule);
        tryInit('fullscreen', initFullscreenModule);
        tryInit('focus', initFocusModule);
        // done
        console.info('AntiCheat: modules initialized', Object.keys(NS.modules));

        // If the student uses Back then re-opens the assessment, browsers may restore this page from bfcache
        // (or "back_forward" navigation) including previous anti-cheat counters/logs. Hard-reset the HUD so
        // it always starts fresh on re-open.
        try {
            const shouldResetOnShow = (e) => {
                if (e && e.persisted === true) return true; // bfcache
                try {
                    const nav = performance && typeof performance.getEntriesByType === 'function'
                        ? performance.getEntriesByType('navigation')[0]
                        : null;
                    if (nav && nav.type === 'back_forward') return true;
                } catch (_) { /* ignore */ }
                try {
                    // legacy fallback
                    // eslint-disable-next-line deprecation/deprecation
                    if (performance && performance.navigation && performance.navigation.type === 2) return true;
                } catch (_) { /* ignore */ }
                return false;
            };

            const resetHud = () => {
                try { document.body.classList.remove('ac-locked'); } catch (_) { }
                try { if (window.__ac) window.__ac = {}; } catch (_) { }
                try {
                    [
                        '#ac-screenPromptOverlay',
                        '#ac-studentStatus',
                        '#ac-unlock-screen',
                        '#ac-screenVideo'
                    ].forEach((sel) => {
                        const el = document.querySelector(sel);
                        if (el && el.parentNode) el.parentNode.removeChild(el);
                    });
                    [
                        '.ac-logsBox',
                        '.ac-cheatMetrics',
                        '.ac-omniFocusBox',
                        '.ac-fullscreen-stats',
                        '.ac-status-scoreboard',
                        '.ac-interact-popup',
                        '.ac-afk-radius',
                        '.ac-afk-confirmed',
                        '.ac-unlock-screen'
                    ].forEach((sel) => {
                        document.querySelectorAll(sel).forEach((el) => {
                            try { if (el && el.parentNode) el.parentNode.removeChild(el); } catch (_) { }
                        });
                    });
                } catch (_) { /* ignore */ }
            };

            window.addEventListener('pageshow', (e) => {
                const p = (window.location && window.location.pathname ? window.location.pathname : '').toLowerCase();
                if (p.indexOf('studentanswerassessment') === -1) return;
                if (!shouldResetOnShow(e)) return;
                resetHud();
                // Reload to guarantee all module-local counters are reset too.
                try { window.location.reload(); } catch (_) { }
            });
        } catch (_) { /* ignore */ }

        // Send a lightweight server-side ping so admins can verify the script is running.
        try {
            const modules = Object.keys(NS.modules).reduce((acc, k) => (acc[k] = !!NS.modules[k], acc), {});
            acSendEvent('ac_loaded', { modules });
        } catch (_) { }
    }

    // Auto-init once DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', async () => {
            await primeServerTotalsSeed();
            initAll();
        }, { once: true });
    } else {
        (async () => {
            await primeServerTotalsSeed();
            initAll();
        })();
    }

    // Expose a simple API for debugging
    NS.info = () => {
        return {
            loadedAt: new Date().toISOString(),
            modules: Object.keys(NS.modules).reduce((acc, k) => (acc[k] = !!NS.modules[k], acc), {})
        };
    };

})(window);
