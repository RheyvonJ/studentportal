/**
 * Polls /studentdb/StudentDb/Notifications while the student portal is open.
 * Shows long toasts for new items (e.g. new task) without refresh or redeploy.
 */
(function () {
    'use strict';

    var POLL_MS = 12000;
    var STORAGE_KEY = 'stuLiveNotifSeenIds';
    var MAX_STORED_IDS = 400;

    function hubRoot() {
        return document.getElementById('stuLiveNotifHub');
    }

    function loadSeenIds() {
        try {
            var raw = sessionStorage.getItem(STORAGE_KEY);
            var arr = raw ? JSON.parse(raw) : [];
            return new Set(Array.isArray(arr) ? arr : []);
        } catch (_) {
            return new Set();
        }
    }

    function saveSeenIds(set) {
        try {
            var arr = Array.from(set);
            if (arr.length > MAX_STORED_IDS) arr = arr.slice(-MAX_STORED_IDS);
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify(arr));
        } catch (_) {}
    }

    function escapeHtml(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function iconClass(type) {
        var t = (type || '').toLowerCase();
        if (t === 'class-approved') return 'fa-check';
        if (t === 'class-rejected') return 'fa-xmark';
        if (t === 'class-auto-joined') return 'fa-user-plus';
        if (t === 'assessment' || t === 'content-assessment-new') return 'fa-clipboard-question';
        if (t === 'task' || t === 'content-task-new') return 'fa-list-check';
        if (t === 'class-unenrolled') return 'fa-user-minus';
        if (t === 'announcement') return 'fa-bullhorn';
        if (t === 'material' || t === 'content-material-new') return 'fa-book';
        if (t === 'meeting') return 'fa-video';
        if (t === 'libra-approved') return 'fa-book';
        if (t === 'libra-rejected' || t === 'libra-cancelled') return 'fa-ban';
        if (t === 'libra-penalty') return 'fa-scale-balanced';
        if (t === 'libra-due-soon') return 'fa-clock';
        return 'fa-bell';
    }

    function timeAgo(date) {
        var seconds = Math.floor((Date.now() - date.getTime()) / 1000);
        var intervals = [
            { s: 31536000, l: 'y' },
            { s: 2592000, l: 'mo' },
            { s: 604800, l: 'w' },
            { s: 86400, l: 'd' },
            { s: 3600, l: 'h' },
            { s: 60, l: 'm' }
        ];
        for (var i = 0; i < intervals.length; i++) {
            var v = Math.floor(seconds / intervals[i].s);
            if (v >= 1) return v + intervals[i].l;
        }
        return 'now';
    }

    function showAlert(messages) {
        if (!messages || !messages.length) return;
        var fn = typeof window.showPersistentToast === 'function' ? window.showPersistentToast : window.showToast;
        if (typeof fn !== 'function') return;
        if (messages.length === 1) {
            fn(messages[0]);
            return;
        }
        fn(messages.length + ' new updates — open notifications to read them.');
    }

    function setup() {
        var root = hubRoot();
        if (!root) return;

        var bell = document.getElementById('stuLiveNotifBell');
        var badge = document.getElementById('stuLiveNotifBadge');
        var backdrop = document.getElementById('stuLiveNotifBackdrop');
        var panel = document.getElementById('stuLiveNotifPanel');
        var listEl = document.getElementById('stuLiveNotifList');
        var closeBtn = document.getElementById('stuLiveNotifClose');
        var markAllBtn = document.getElementById('stuLiveNotifMarkAll');
        var clearBtn = document.getElementById('stuLiveNotifClearAll');

        var seenIds = loadSeenIds();
        var baselineDone = false;
        var lastCreatedMs = 0;
        var timer = null;
        var stopped = false;

        function openPanel() {
            backdrop.classList.add('is-open');
            panel.classList.add('is-open');
            backdrop.setAttribute('aria-hidden', 'false');
            panel.setAttribute('aria-hidden', 'false');
        }

        function closePanel() {
            backdrop.classList.remove('is-open');
            panel.classList.remove('is-open');
            backdrop.setAttribute('aria-hidden', 'true');
            panel.setAttribute('aria-hidden', 'true');
        }

        function updateBadge(n) {
            if (!badge) return;
            if (n > 0) {
                badge.textContent = n > 99 ? '99+' : String(n);
                badge.style.display = 'block';
            } else {
                badge.style.display = 'none';
            }
        }

        function renderList() {
            var d = window.__stuLiveNotifData || {};
            var items = d.items || [];
            if (!listEl) return;
            if (!items.length) {
                listEl.innerHTML = '<li class="stu-live-empty">No notifications yet.</li>';
                return;
            }
            listEl.innerHTML = items
                .map(function (n) {
                    var type = n.type || '';
                    var text = n.text || '';
                    var id = n.id || '';
                    var read = n.read === true;
                    var created = n.createdAt ? new Date(n.createdAt) : null;
                    var rel = created ? timeAgo(created) : '';
                    return (
                        '<li class="stu-live-item ' +
                        escapeHtml(type) +
                        (read ? ' is-read' : '') +
                        '" data-id="' +
                        escapeHtml(id) +
                        '">' +
                        '<span class="stu-live-ico"><i class="fa-solid ' +
                        iconClass(type) +
                        '"></i></span>' +
                        '<span class="stu-live-txt">' +
                        escapeHtml(text) +
                        '</span>' +
                        '<span class="stu-live-when">' +
                        escapeHtml(rel) +
                        '</span>' +
                        '<div class="stu-live-actions">' +
                        '<button type="button" class="stu-live-mark" title="Mark read" aria-label="Mark read"><i class="fa-solid fa-envelope-open"></i></button>' +
                        '<button type="button" class="stu-live-del" title="Delete" aria-label="Delete"><i class="fa-solid fa-trash"></i></button>' +
                        '</div></li>'
                    );
                })
                .join('');
        }

        function processNewItems(items) {
            var newTexts = [];
            var maxTs = lastCreatedMs;
            for (var i = 0; i < items.length; i++) {
                var n = items[i];
                var id = n.id || '';
                var created = n.createdAt ? new Date(n.createdAt).getTime() : 0;
                if (created > maxTs) maxTs = created;
                var type = (n.type || '').toLowerCase();
                var read = n.read === true;
                var del = n.deleted === true;
                var text = n.text || '';
                if (!id || read || del || !text) continue;
                if (type === 'class-auto-joined') continue;
                if (created <= lastCreatedMs) continue;
                if (seenIds.has(id)) continue;
                seenIds.add(id);
                newTexts.push(text);
            }
            saveSeenIds(seenIds);
            if (maxTs > lastCreatedMs) lastCreatedMs = maxTs;
            if (newTexts.length) showAlert(newTexts);
        }

        async function pull() {
            if (stopped || document.visibilityState === 'hidden') return;
            try {
                var res = await fetch('/studentdb/StudentDb/Notifications?ts=' + Date.now(), {
                    credentials: 'same-origin',
                    cache: 'no-store',
                    headers: { 'Cache-Control': 'no-cache' }
                });
                if (res.status === 401) {
                    stopped = true;
                    ['stuLiveNotifHub', 'stuLiveNotifBackdrop', 'stuLiveNotifPanel'].forEach(function (hid) {
                        var el = document.getElementById(hid);
                        if (el) el.hidden = true;
                    });
                    return;
                }
                if (!res.ok) return;
                var data = await res.json();
                if (!data || data.success !== true) return;

                window.__stuLiveNotifData = data;
                var items = Array.isArray(data.items) ? data.items : [];
                updateBadge(typeof data.unread === 'number' ? data.unread : 0);

                if (!baselineDone) {
                    for (var j = 0; j < items.length; j++) {
                        var c = items[j].createdAt ? new Date(items[j].createdAt).getTime() : 0;
                        if (c > lastCreatedMs) lastCreatedMs = c;
                    }
                    baselineDone = true;
                    renderList();
                    return;
                }

                processNewItems(items);
                renderList();
            } catch (_) {}
        }

        bell?.addEventListener('click', async function (e) {
            e.stopPropagation();
            if (typeof window.ensureToastInBodyForPortal === 'function') {
                window.ensureToastInBodyForPortal();
            }
            if (typeof window.showToast === 'function') {
                window.showToast('Loading notifications...', 2800);
            }
            if (typeof window.setProfessorDbShellLoading === 'function') {
                window.setProfessorDbShellLoading(true);
            }
            try {
                await pull();
                renderList();
                openPanel();
            } finally {
                if (typeof window.setProfessorDbShellLoading === 'function') {
                    window.setProfessorDbShellLoading(false);
                }
            }
        });
        closeBtn?.addEventListener('click', function (e) {
            e.stopPropagation();
            closePanel();
        });
        backdrop?.addEventListener('click', closePanel);

        markAllBtn?.addEventListener('click', async function (e) {
            e.stopPropagation();
            var d = window.__stuLiveNotifData || {};
            var items = d.items || [];
            var ids = items
                .filter(function (x) {
                    return !x.read;
                })
                .map(function (x) {
                    return x.id;
                })
                .filter(Boolean);
            if (!ids.length) return;
            await Promise.all(
                ids.map(function (id) {
                    return fetch('/studentdb/StudentDb/Notifications/MarkRead', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        credentials: 'same-origin',
                        body: JSON.stringify({ id: id })
                    });
                })
            );
            await pull();
        });

        clearBtn?.addEventListener('click', async function (e) {
            e.stopPropagation();
            var d = window.__stuLiveNotifData || {};
            var items = d.items || [];
            var ids = items
                .map(function (x) {
                    return x.id;
                })
                .filter(Boolean);
            if (!ids.length) return;
            await Promise.all(
                ids.map(function (id) {
                    return fetch('/studentdb/StudentDb/Notifications/Delete', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        credentials: 'same-origin',
                        body: JSON.stringify({ id: id })
                    });
                })
            );
            await pull();
        });

        listEl?.addEventListener('click', async function (e) {
            var del = e.target.closest('.stu-live-del');
            var mark = e.target.closest('.stu-live-mark');
            var li = e.target.closest('.stu-live-item');
            if (!li || li.classList.contains('stu-live-empty')) return;
            var id = li.getAttribute('data-id');
            if (!id) return;
            if (del) {
                await fetch('/studentdb/StudentDb/Notifications/Delete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'same-origin',
                    body: JSON.stringify({ id: id })
                });
                await pull();
            } else if (mark) {
                await fetch('/studentdb/StudentDb/Notifications/MarkRead', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'same-origin',
                    body: JSON.stringify({ id: id })
                });
                await pull();
            }
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && panel.classList.contains('is-open')) closePanel();
        });

        function schedule() {
            if (stopped) return;
            clearInterval(timer);
            timer = setInterval(pull, POLL_MS);
        }

        pull();
        schedule();
        document.addEventListener('visibilitychange', function () {
            if (document.visibilityState === 'visible') pull();
        });
        window.addEventListener('focus', function () {
            pull();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setup);
    } else {
        setup();
    }
})();
