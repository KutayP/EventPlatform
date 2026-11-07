// Tüm şifre alanlarına sağda "göster / gizle" (göz) düğmesi ekler. Görünümlerde değişiklik gerekmez.
(function () {
    function enhance(input) {
        if (input.dataset.pwToggle === '1' || input.closest('.input-group')) return;
        input.dataset.pwToggle = '1';

        var wrap = document.createElement('div');
        wrap.className = 'position-relative';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);
        input.style.paddingRight = '2.75rem';

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-link text-secondary position-absolute top-0 end-0 h-100 px-3 text-decoration-none';
        btn.style.zIndex = '5';
        btn.innerHTML = '<i class="bi bi-eye"></i>';
        btn.title = 'Şifreyi göster';
        btn.setAttribute('aria-label', 'Şifreyi göster');
        wrap.appendChild(btn);

        btn.addEventListener('click', function () {
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.innerHTML = show ? '<i class="bi bi-eye-slash"></i>' : '<i class="bi bi-eye"></i>';
            btn.title = show ? 'Şifreyi gizle' : 'Şifreyi göster';
            btn.setAttribute('aria-label', btn.title);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('input[type="password"]').forEach(enhance);
    });
})();

// Çift gönderimi engelle: bir POST formu gönderilince düğmeleri kilitle.
// Doğrulama hatası olan ya da JavaScript ile gönderilen (preventDefault yapılan) formlara dokunmaz.
(function () {
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (e.defaultPrevented || !(form instanceof HTMLFormElement)) return;
        if ((form.getAttribute('method') || 'get').toLowerCase() !== 'post') return;

        // Gönderim başladıktan sonra kilitle (tıklanan düğmenin değeri forma girsin diye bir sonraki adımda).
        setTimeout(function () {
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (b) {
                b.disabled = true;
            });
        }, 0);
    });

    // Geri tuşuyla önbellekten dönülen sayfada düğmeler kilitli kalmasın.
    window.addEventListener('pageshow', function (e) {
        if (!e.persisted) return;
        document.querySelectorAll('button[type="submit"]:disabled, input[type="submit"]:disabled').forEach(function (b) {
            b.disabled = false;
        });
    });
})();
