// StudentTodo — filter toggle (layout provides nav / notifications)

const toggleOptions = document.querySelectorAll('.toggle-button .option');

function applyFilter(view) {
    document.querySelectorAll('.task-card').forEach((task) => {
        const status = task.dataset.status || 'todo';
        task.style.display = status === view ? 'flex' : 'none';
    });
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
});
