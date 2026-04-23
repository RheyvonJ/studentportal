// StudentTodo — filter toggle (layout provides nav / notifications)

const toggleOptions = document.querySelectorAll('.toggle-button .option');

function applyFilter(view) {
    document.querySelectorAll('.task-card').forEach((task) => {
        const status = task.dataset.status || 'todo';
        task.style.display = status === view ? 'flex' : 'none';
    });
}

function setQueryParam(key, value) {
    const url = new URL(window.location.href);
    if (value == null || value === '') url.searchParams.delete(key);
    else url.searchParams.set(key, value);
    // Keep the same path, just update query string
    window.location.href = url.toString();
}

toggleOptions.forEach((option) => {
    option.addEventListener('click', () => {
        toggleOptions.forEach((opt) => {
            opt.classList.remove('active');
            opt.setAttribute('aria-selected', 'false');
        });
        option.classList.add('active');
        option.setAttribute('aria-selected', 'true');
        applyFilter(option.dataset.view);
    });

    option.addEventListener('keydown', (e) => {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        e.preventDefault();
        option.click();
    });
});

document.addEventListener('DOMContentLoaded', () => {
    applyFilter('todo');

    const subjectFilter = document.getElementById('subjectFilter');
    if (subjectFilter) {
        subjectFilter.addEventListener('change', () => {
            setQueryParam('subject', subjectFilter.value || '');
        });
    }
});
