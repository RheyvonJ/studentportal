(function () {
    'use strict';

    function showImpl(message, duration) {
        try {
            var ms = typeof duration === 'number' ? duration : 2500;
            var toast = document.getElementById('toast') || document.querySelector('.toast');
            if (!toast) {
                toast = document.createElement('div');
                toast.id = 'toast';
                toast.className = 'toast';
                toast.setAttribute('aria-live', 'polite');
                toast.setAttribute('role', 'status');
                document.body.appendChild(toast);
            }
            toast.textContent = message;
            toast.classList.add('show');
            clearTimeout(window.__toastTimer);
            window.__toastTimer = setTimeout(function () {
                toast.classList.remove('show');
            }, ms);
        } catch (_) {}
    }

    if (typeof window.showToast !== 'function') {
        window.showToast = showImpl;
    }

    /** Longer on-screen time for background notifications (new tasks, etc.). */
    window.showPersistentToast = function (message) {
        var fn = typeof window.showToast === 'function' ? window.showToast : showImpl;
        fn(message, 12000);
    };
})();
