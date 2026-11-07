// Zil menüsü: SignalR ile "bildirimlerin değişti" haberi gelince menüyü sunucudan yeniler.
(function () {
    const root = document.getElementById('notif-root');
    if (!root || !window.signalR) return;

    const badge = document.getElementById('notif-badge');
    const panel = document.getElementById('notif-panel');

    function applyUnread() {
        const holder = panel.querySelector('[data-unread]');
        const n = holder ? parseInt(holder.dataset.unread || '0', 10) : 0;
        badge.textContent = n > 99 ? '99+' : String(n);
        badge.classList.toggle('d-none', n <= 0);
    }

    async function refresh() {
        try {
            const r = await fetch('/notifications/panel', {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'X-Requested-With': 'fetch' }
            });
            if (!r.ok) return;
            panel.innerHTML = await r.text();
            applyUnread();
        } catch (e) { /* ağ hatası: bir sonraki olayda yeniden denenir */ }
    }

    // "Tümünü okundu say" ve arkadaşlık "Kabul et / Reddet": sayfayı yenilemeden gönder, sonra menüyü tazele.
    root.addEventListener('submit', function (e) {
        const form = e.target;
        if (!form.matches('[data-notif-readall], [data-notif-action]')) return;
        e.preventDefault();
        fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'fetch' }
        }).then(refresh);
    });

    // Menü açılırken zamanlar ("5 dk önce") ve liste güncel olsun.
    root.addEventListener('show.bs.dropdown', refresh);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect()
        .build();

    connection.on('NotificationsChanged', refresh);
    connection.onreconnected(refresh);   // bağlantı koptuğu sürede kaçanları yakala
    connection.start().catch(function () { /* hub'a ulaşılamazsa menü yine sayfa yenilenince güncellenir */ });
})();
