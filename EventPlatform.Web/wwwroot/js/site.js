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
