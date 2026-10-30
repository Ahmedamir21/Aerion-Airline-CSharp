// Light/dark switch. The saved choice is applied early by a small script in the layout head.
(function () {
    var button = document.getElementById('theme-toggle');
    if (!button) return;
    var icon = button.querySelector('i');

    function isDark() {
        return document.documentElement.getAttribute('data-theme') === 'dark';
    }

    function syncIcon() {
        icon.className = isDark() ? 'fas fa-sun' : 'fas fa-moon';
    }

    button.addEventListener('click', function () {
        var next = isDark() ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('theme', next); } catch (e) {}
        syncIcon();
    });
    syncIcon();
})();
