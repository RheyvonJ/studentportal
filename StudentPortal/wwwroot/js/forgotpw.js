const step1 = document.getElementById('step-1');
const step2 = document.getElementById('step-2');
const step3 = document.getElementById('step-3');
const userEmail = document.getElementById('user-email');
let sendingCode = false;
let verifyingCode = false;
let resettingPassword = false;

document.getElementById('to-step-2').addEventListener('click', async () => {
    if (sendingCode) return;
    const email = document.getElementById('email').value.trim();
    if (!email) return alert("Please enter your email.");

    const sendButton = document.getElementById('to-step-2');
    sendingCode = true;
    sendButton.disabled = true;
    try {
        const response = await fetch('/Account/SendVerificationCode', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email })
        });

        if (response.ok) {
            userEmail.textContent = email;
            step1.classList.add('hidden');
            step2.classList.remove('hidden');
        } else {
            alert(await response.text());
        }
    } finally {
        sendingCode = false;
        sendButton.disabled = false;
    }
});

document.getElementById('to-step-3').addEventListener('click', async () => {
    if (verifyingCode) return;
    const code = document.getElementById('verification-code').value.trim();
    if (!code) return alert("Please enter the code.");

    const verifyButton = document.getElementById('to-step-3');
    verifyingCode = true;
    verifyButton.disabled = true;
    try {
        const response = await fetch('/Account/VerifyCode', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ verificationCode: code })
        });

        if (response.ok) {
            step2.classList.add('hidden');
            step3.classList.remove('hidden');
        } else {
            alert(await response.text());
        }
    } finally {
        verifyingCode = false;
        verifyButton.disabled = false;
    }
});

document.querySelector('form').addEventListener('submit', async (e) => {
    e.preventDefault();
    if (resettingPassword) return;
    const newPassword = document.getElementById('new-password').value.trim();
    const confirm = document.getElementById('confirm-password').value.trim();

    if (newPassword !== confirm) return alert("Passwords do not match.");

    const submitButton = document.querySelector('form button[type="submit"]');
    resettingPassword = true;
    if (submitButton) submitButton.disabled = true;
    try {
        const response = await fetch('/Account/ResetPassword', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ newPassword })
        });

        if (response.ok) {
            alert("Password successfully reset!");
            window.location.href = '/Account/Login';
        } else {
            alert(await response.text());
        }
    } finally {
        resettingPassword = false;
        if (submitButton) submitButton.disabled = false;
    }
});

function togglePassword(id) {
    const input = document.getElementById(id);
    input.type = input.type === 'password' ? 'text' : 'password';
}