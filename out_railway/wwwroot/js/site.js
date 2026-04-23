// Shared navigation helpers (used by instructor/admin and student pages).
(function () {
    'use strict';

    function readDataDashboardUrl() {
        const ids = [
            'adminTaskPage',
            'adminClassPage',
            'adminMaterialPage',
            'adminAssessmentPage',
            'adminManageClassPage',
            'adminManageGradesPage'
        ];
        for (let i = 0; i < ids.length; i++) {
            const el = document.getElementById(ids[i]);
            const u = el && el.getAttribute('data-dashboard-url');
            if (u && u.trim()) return u.trim();
        }
        const b = document.body && document.body.getAttribute('data-dashboard-url');
        if (b && b.trim()) return b.trim();
        return '';
    }

    /**
     * Home URL for professors/teachers/admins when leaving a class-scoped page.
     * Prefer data-dashboard-url on the page root (set in Razor).
     */
    window.resolveInstructorDashboardUrl = function () {
        const explicit = readDataDashboardUrl();
        if (explicit) return explicit;
        const role = ((document.body && document.body.getAttribute('data-user-role')) || '').toLowerCase();
        if (role === 'admin' || role === 'administrator') return '/admindb/AdminDb';
        return '/professordb/ProfessorDb';
    };

    /**
     * Student dashboard when no class context.
     */
    window.resolveStudentDashboardUrl = function () {
        const el = document.getElementById('studentTaskPage')
            || document.getElementById('studentMaterialPage')
            || document.getElementById('studentAssessmentPage')
            || document.body;
        const u = el && el.getAttribute('data-student-dashboard-url');
        if (u && u.trim()) return u.trim();
        return '/StudentDb/StudentDb';
    };

    /**
     * Full-screen loading used on instructor/admin pages (pairs with StudentDb.css `.slsp-shell.is-loading`).
     */
    window.setProfessorDbShellLoading = function (on) {
        var shell = document.querySelector('.slsp-shell');
        if (!shell) return;
        shell.classList.toggle('is-loading', !!on);
    };

    // If the user navigates with the browser back/forward cache, DOMContentLoaded may not fire.
    // Ensure the overlay is cleared on bfcache restore to prevent "stuck loading screen".
    window.addEventListener('pageshow', function () {
        try {
            var shell = document.querySelector('.slsp-shell');
            if (shell) {
                // Clear any leftover overlay (including when restored from bfcache).
                // Use a micro-delay so the navigation still "feels" like it loaded.
                setTimeout(function () { shell.classList.remove('is-loading'); }, 50);
            }
        } catch (_) { /* ignore */ }
    });

    // Show loading overlay when leaving the page (including browser back/forward).
    // This gives a consistent UX, but will always be cleared by pageshow above.
    window.addEventListener('pagehide', function () {
        try {
            var shell = document.querySelector('.slsp-shell');
            if (shell) shell.classList.add('is-loading');
        } catch (_) { /* ignore */ }
    });

    /**
     * #toast is often rendered inside .slsp-shell; the loading ::after pseudo can paint above it.
     * Moving the toast to document.body makes fixed + z-index reliable above the overlay.
     */
    window.ensureToastInBodyForPortal = function () {
        var el = document.getElementById('toast');
        if (el && el.parentNode !== document.body) {
            document.body.appendChild(el);
        }
    };

    /**
     * Breadcrumb / recent-upload / in-app navigation copy for admin class-scoped pages.
     * @param {string} href - Absolute or relative URL
     */
    window.resolveAdminNavToastMessage = function (href) {
        if (!href) return 'Opening...';
        try {
            var abs = new URL(href, window.location.href);
            var path = abs.pathname.toLowerCase();
            var dashPath = '';
            if (typeof window.resolveInstructorDashboardUrl === 'function') {
                try {
                    var du = window.resolveInstructorDashboardUrl();
                    if (du) dashPath = new URL(du, window.location.href).pathname.toLowerCase();
                } catch (_) { /* ignore */ }
            }
            if (dashPath && path === dashPath) return 'Redirecting to Dashboard';

            if (typeof window.resolveStudentDashboardUrl === 'function') {
                try {
                    var sdu = window.resolveStudentDashboardUrl();
                    if (sdu) {
                        var studentDashPath = new URL(sdu, window.location.href).pathname.toLowerCase();
                        if (path === studentDashPath) return 'Redirecting to Dashboard';
                    }
                } catch (_) { /* ignore */ }
            }

            if (path.indexOf('/adminclass') !== -1) return 'Redirecting to Class';

            if (path.indexOf('/adminsubject') !== -1) return 'Opening subjects';
            if (path.indexOf('/adminassessmentlist') !== -1) return 'Opening assessments';

            if (path.indexOf('/adminmaterial') !== -1) return 'Opening Material';
            if (path.indexOf('/admintask') !== -1) return 'Opening Task';
            if (path.indexOf('/adminassessment') !== -1) return 'Opening Assessment';
            if (path.indexOf('/adminmanageclass') !== -1) return 'Opening Class Management';
        } catch (_) { /* ignore */ }
        return 'Opening...';
    };

    window.resolvePortalSidebarToastMessage = function (href) {
        if (!href) return 'Opening...';
        try {
            var abs = new URL(href, window.location.href);
            var path = abs.pathname.toLowerCase();
            var msg = typeof window.resolveStudentNavToastMessage === 'function'
                ? window.resolveStudentNavToastMessage(path)
                : 'Opening...';
            if (msg && msg !== 'Opening...') return msg;
            msg = window.resolveAdminNavToastMessage(path);
            if (msg && msg !== 'Opening...') return msg;

            if (path.indexOf('/library/search') !== -1 || path === '/library') return 'Opening Library';
            if (path.indexOf('/studenttodo') !== -1) return 'Opening To-Do';
        } catch (_) { /* ignore */ }
        return 'Opening...';
    };

    window.resolveStudentNavToastMessage = function (href) {
        if (!href) return 'Opening...';
        try {
            var abs = new URL(href, window.location.href);
            var path = abs.pathname.toLowerCase();
            var studentDash = '';
            if (typeof window.resolveStudentDashboardUrl === 'function') {
                try {
                    var sd = window.resolveStudentDashboardUrl();
                    if (sd) studentDash = new URL(sd, window.location.href).pathname.toLowerCase();
                } catch (_) { /* ignore */ }
            }
            if (studentDash && path === studentDash) return 'Redirecting to Dashboard';
            if (path.indexOf('/studentclass') !== -1) return 'Redirecting to Class';
            if (path.indexOf('/studentmaterial') !== -1) return 'Opening Material';
            if (path.indexOf('/studenttask') !== -1) return 'Opening Task';
            if (path.indexOf('/studentassessment') !== -1) return 'Opening Assessment';
            if (path.indexOf('/library/search') !== -1 || path === '/library') return 'Opening Library';
            if (path.indexOf('/studenttodo') !== -1) return 'Opening To-Do';
        } catch (_) { /* ignore */ }
        return 'Opening...';
    };

    function showInstructorToastMessage(message) {
        if (!message) return;
        if (typeof window.ensureToastInBodyForPortal === 'function') {
            window.ensureToastInBodyForPortal();
        }
        if (typeof window.showToast === 'function') {
            window.showToast(message);
            return;
        }
        var el = document.getElementById('toast');
        if (!el) return;
        el.textContent = message;
        el.classList.add('show');
        setTimeout(function () { el.classList.remove('show'); }, 2800);
    }

    /**
     * Toast (if message) then grey overlay + spinner, then navigate. Toast stacks above overlay via z-index in CSS.
     * @param {string|null} message - Pass null if the caller already showed a toast.
     */
    window.navigateWithProfessorLoading = function (url, message, delay) {
        if (!url) return;
        if (delay == null) delay = 600;
        if (typeof window.ensureToastInBodyForPortal === 'function') {
            window.ensureToastInBodyForPortal();
        }
        if (message) showInstructorToastMessage(message);
        window.setProfessorDbShellLoading(true);
        setTimeout(function () {
            window.location.href = url;
        }, delay);
    };

    function sameDocumentLocation(url) {
        try {
            var u = new URL(url, window.location.href);
            return u.origin === window.location.origin
                && u.pathname === window.location.pathname
                && u.search === window.location.search
                && u.hash === window.location.hash;
        } catch (_) {
            return false;
        }
    }

    /**
     * Breadcrumb + recent-upload links on class-scoped admin pages (Material, Task, Assessment, Manage class).
     * AdminClass recent-upload + content cards use AdminClass.js (delete mode + stopPropagation on recent links).
     */
    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest(
            'main#adminClassPage .class-breadcrumb a[href], ' +
            'main#adminMaterialPage .class-breadcrumb a[href], ' +
            'main#adminTaskPage .class-breadcrumb a[href], ' +
            'main#adminAssessmentPage .class-breadcrumb a[href], ' +
            'main#adminManageClassPage .class-breadcrumb a[href], ' +
            'main#adminMaterialPage .recent-uploads a.recent-upload-link[href], ' +
            'main#adminTaskPage .recent-uploads a.recent-upload-link[href], ' +
            'main#adminAssessmentPage .recent-uploads a.recent-upload-link[href]'
        );
        if (!a) return;
        var href = a.getAttribute('href');
        if (!href || href === '#' || href.indexOf('javascript:') === 0) return;
        if (a.target === '_blank' || a.hasAttribute('download')) return;
        try {
            var abs = new URL(href, window.location.href);
            if (abs.origin !== window.location.origin) return;
            if (sameDocumentLocation(abs.href)) return;
        } catch (_) {
            return;
        }
        e.preventDefault();
        var path = (function () {
            try {
                var u = new URL(href, window.location.href);
                return u.pathname + u.search + u.hash;
            } catch (_) {
                return href;
            }
        })();
        var msg = typeof window.resolveAdminNavToastMessage === 'function'
            ? window.resolveAdminNavToastMessage(path)
            : 'Opening...';
        window.navigateWithProfessorLoading(path, msg, 600);
    });

    /**
     * Sidebar primary nav (Dashboard / Library / To-Do): toast + overlay + delayed navigation.
     */
    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest(
            '#slspNav a.slsp-nav-dashboard[href], #slspNav a.slsp-nav-library[href], #slspNav a.slsp-nav-todo[href]'
        );
        if (!a) return;
        var href = a.getAttribute('href');
        if (!href || href === '#' || href.indexOf('javascript:') === 0) return;
        if (a.target === '_blank' || a.hasAttribute('download')) return;
        try {
            var abs = new URL(href, window.location.href);
            if (abs.origin !== window.location.origin) return;
            if (sameDocumentLocation(abs.href)) {
                e.preventDefault();
                if (typeof window.ensureToastInBodyForPortal === 'function') {
                    window.ensureToastInBodyForPortal();
                }
                if (typeof window.showToast === 'function') {
                    window.showToast("You're already here.");
                }
                return;
            }
        } catch (_) {
            return;
        }
        e.preventDefault();
        var path = (function () {
            try {
                var u = new URL(href, window.location.href);
                return u.pathname + u.search + u.hash;
            } catch (_) {
                return href;
            }
        })();
        var msg = typeof window.resolveAdminNavToastMessage === 'function'
            ? (typeof window.resolvePortalSidebarToastMessage === 'function'
                ? window.resolvePortalSidebarToastMessage(path)
                : window.resolveAdminNavToastMessage(path))
            : 'Opening...';
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading(path, msg, 600);
        }
    });

    /**
     * Student class-scoped links (breadcrumbs + recent links): toast + overlay + delayed navigation.
     */
    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest(
            'main#studentClassPage .class-breadcrumb a[href], ' +
            'main#studentMaterialPage .class-breadcrumb a[href], ' +
            'main#studentTaskPage .class-breadcrumb a[href], ' +
            'main#studentAssessmentPage .class-breadcrumb a[href], ' +
            'main#studentClassPage .recent-uploads a.recent-upload-link[href], ' +
            'main#studentMaterialPage .recent-uploads a.recent-upload-link[href], ' +
            'main#studentTaskPage .recent-uploads a.recent-title[href]'
        );
        if (!a) return;
        var href = a.getAttribute('href');
        if (!href || href === '#' || href.indexOf('javascript:') === 0) return;
        if (a.target === '_blank' || a.hasAttribute('download')) return;
        try {
            var abs = new URL(href, window.location.href);
            if (abs.origin !== window.location.origin) return;
            if (sameDocumentLocation(abs.href)) return;
        } catch (_) {
            return;
        }
        e.preventDefault();
        var path = (function () {
            try {
                var u = new URL(href, window.location.href);
                return u.pathname + u.search + u.hash;
            } catch (_) {
                return href;
            }
        })();
        var msg = typeof window.resolveStudentNavToastMessage === 'function'
            ? window.resolveStudentNavToastMessage(path)
            : 'Opening...';
        if (typeof window.navigateWithProfessorLoading === 'function') {
            window.navigateWithProfessorLoading(path, msg, 600);
        }
    });
})();
