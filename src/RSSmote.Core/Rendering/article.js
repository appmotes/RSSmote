// Article page behaviour: tidy the feed's markup, hand links and shortcuts to the app, scroll by pages.
(function () {
    function post(message) {
        if (typeof invokeCSharpAction !== 'function') { return false; }
        invokeCSharpAction(JSON.stringify(message));
        return true;
    }

    document.querySelectorAll('.content img').forEach(function (img) {
        img.removeAttribute('width');
        img.removeAttribute('height');
        img.loading = 'lazy';
        var small = function () {
            if (img.naturalWidth > 0 && img.naturalWidth < 48) { img.style.display = 'inline'; img.style.margin = 0; }
        };
        if (img.complete) { small(); } else { img.addEventListener('load', small); }
    });
    document.querySelectorAll('.content iframe').forEach(function (frame) {
        if (!frame.parentNode.classList.contains('video')) {
            var wrapper = document.createElement('div');
            wrapper.className = 'video';
            frame.parentNode.insertBefore(wrapper, frame);
            wrapper.appendChild(frame);
        }
        frame.setAttribute('allowfullscreen', '');
    });
    document.querySelectorAll('.content p').forEach(function (p) {
        if (!p.textContent.trim() && !p.querySelector('img,iframe,video,picture')) { p.remove(); }
    });
    var hero = document.querySelector('figure.hero img');
    var first = document.querySelector('.content img');
    if (hero && first && hero.src === first.src) { hero.parentNode.remove(); }

    // Links open in the system browser; the page itself never navigates.
    document.addEventListener('click', function (e) {
        var a = e.target.closest ? e.target.closest('a[href]') : null;
        if (a && post({ type: 'link', href: a.href })) { e.preventDefault(); }
    });

    // The web view keeps keyboard focus once clicked, so single-key shortcuts are forwarded to the app (D15).
    var plain = 'jkmsbvranp. ';
    var modified = '123f+=-0n,';
    document.addEventListener('keydown', function (e) {
        var key = e.key.length === 1 ? e.key.toLowerCase() : e.key;
        var command = e.metaKey || e.ctrlKey;
        var forward = command && !e.altKey
            ? modified.indexOf(key) >= 0
            : !e.altKey && (plain.indexOf(key) >= 0 || key === 'Escape');
        if (forward && post({ type: 'key', key: key, shift: e.shiftKey, command: command })) { e.preventDefault(); }
    });

    window.rssmote = {
        scrollPage: function (up) {
            var max = document.documentElement.scrollHeight - window.innerHeight;
            if (!up && window.scrollY >= max - 2) { return false; }
            window.scrollBy({ top: (up ? -1 : 1) * window.innerHeight * 0.85, behavior: 'smooth' });
            return true;
        }
    };
})();
