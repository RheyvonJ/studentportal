// Make script resilient when used on pages without login form
document.addEventListener('DOMContentLoaded', () => {
    const pwd = document.getElementById('loginPassword');
    const toggle = document.getElementById('passwordToggle');
    if (pwd && toggle) {
        toggle.addEventListener('click', () => {
            const show = pwd.getAttribute('type') === 'password';
            pwd.setAttribute('type', show ? 'text' : 'password');
            toggle.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            toggle.setAttribute('aria-pressed', show ? 'true' : 'false');
            const icon = toggle.querySelector('i');
            if (icon) {
                icon.classList.remove('fa-eye', 'fa-eye-slash');
                icon.classList.add(show ? 'fa-eye-slash' : 'fa-eye');
            }
        });
    }

    // Simple slideshow (only run if slides exist)
    const slides = document.querySelectorAll('.slide');
    let index = 0;
    if (slides && slides.length > 0) {
        // Ensure first slide is active
        slides[0].classList.add('active');
        setInterval(() => {
            if (slides.length === 0) return;
            slides[index]?.classList.remove('active');
            index = (index + 1) % slides.length;
            slides[index]?.classList.add('active');
        }, 4000);
    }

    // Login form handler - disabled AJAX to allow standard form submission
    /*
    const form = document.getElementById('loginForm');
    if (!form) return;

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        // ... rest of the code ...
    });
    */
});
