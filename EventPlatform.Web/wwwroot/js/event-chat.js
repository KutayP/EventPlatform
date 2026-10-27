(function () {
    const card = document.getElementById("chatCard");
    if (!card) return;

    const eventId = parseInt(card.dataset.eventId, 10);
    const me = parseInt(card.dataset.userId, 10);
    const box = document.getElementById("chatMessages");
    const form = document.getElementById("chatForm");
    const input = document.getElementById("chatInput");
    const submitButton = form.querySelector("button[type=submit]");
    const statusBadge = document.getElementById("chatStatus");

    let accessLost = false;

    const formatTime = (iso) =>
        new Date(iso).toLocaleString("tr-TR", { dateStyle: "short", timeStyle: "short" });

    function setStatus(text, kind) {
        statusBadge.textContent = text;
        statusBadge.className = "badge bg-" + kind;
    }

    function scrollToEnd() {
        box.scrollTop = box.scrollHeight;
    }

    // Sohbet erişimi kapandı (etkinlik yayından kaldırıldı ya da silindi):
    // kullanıcıya haber ver, sonra sayfayı yenile. Sayfa güncel durumu (uyarıyı) gösterir.
    function handleAccessLost() {
        if (accessLost) return;
        accessLost = true;

        setStatus("erişim kapandı", "danger");
        input.disabled = true;
        submitButton.disabled = true;

        const note = document.createElement("div");
        note.className = "alert alert-warning small mt-2 mb-0 text-center";
        note.textContent = "Bu etkinlik artık sohbete açık değil (yayından kaldırılmış olabilir). Sayfa yenileniyor…";
        box.appendChild(note);
        scrollToEnd();

        connection.stop().catch(() => { });
        setTimeout(() => window.location.reload(), 2000);
    }

    function markRead() {
        if (accessLost || connection.state !== signalR.HubConnectionState.Connected) return;

        connection.invoke("MarkRead", eventId)
            .then((result) => {
                if (result === "no-access") handleAccessLost();
            })
            .catch((err) => console.warn(err));
    }

    // Metinler textContent ile yazılır: mesajın içindeki HTML çalıştırılamaz (XSS koruması).
    // Hem geçmiş hem canlı mesajlar bu fonksiyonla çizilir, görünüm her zaman aynıdır.
    function addMessage(m) {
        const mine = m.senderId === me;

        const wrap = document.createElement("div");
        wrap.className = "chat-msg mb-2" + (mine ? " text-end" : "");

        const head = document.createElement("div");
        head.className = "small text-muted";
        head.textContent = m.sender + " · " + formatTime(m.sentAt);

        const body = document.createElement("div");
        body.className = "d-inline-block px-2 py-1 rounded " + (mine ? "bg-primary text-white" : "bg-light border");
        body.style.whiteSpace = "pre-wrap";
        body.style.wordBreak = "break-word";
        body.style.textAlign = "left";
        body.textContent = m.text;

        wrap.appendChild(head);
        wrap.appendChild(body);
        box.appendChild(wrap);
    }

    // Sunucudan gelen geçmiş mesajlar.
    try {
        const history = JSON.parse(document.getElementById("chatHistory").textContent);
        history.forEach(addMessage);
    } catch (err) {
        console.error("Sohbet geçmişi okunamadı", err);
    }
    scrollToEnd();

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/eventchat")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", (m) => {
        const nearBottom = box.scrollHeight - box.scrollTop - box.clientHeight < 80;
        addMessage(m);
        if (nearBottom || m.senderId === me) scrollToEnd();

        // Sekme açıksa okundu say; arka plandaysa okunmamış kalsın.
        if (document.visibilityState === "visible") markRead();
    });

    // Sekmeye geri dönünce, arkada biriken mesajlar okundu olsun.
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") markRead();
    });

    connection.onreconnecting(() => setStatus("yeniden bağlanıyor…", "warning"));
    connection.onreconnected(async () => {
        try {
            // Grup üyeliği bağlantıyla birlikte kaybolur, yeniden katıl.
            const result = await connection.invoke("JoinEvent", eventId);
            if (result === "no-access") {
                handleAccessLost();
                return;
            }
            setStatus("bağlı", "success");
        } catch (err) {
            console.error(err);
            setStatus("bağlantı hatası", "danger");
        }
    });
    connection.onclose(() => {
        if (!accessLost) setStatus("bağlantı koptu", "danger");
    });

    async function start() {
        try {
            await connection.start();
            const result = await connection.invoke("JoinEvent", eventId);
            if (result === "no-access") {
                handleAccessLost();
                return;
            }
            setStatus("bağlı", "success");
        } catch (err) {
            console.error(err);
            setStatus("bağlanamadı", "danger");
        }
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (accessLost) return;

        const text = input.value.trim();
        if (!text) return;

        input.value = "";
        try {
            const result = await connection.invoke("SendMessage", eventId, text);

            if (result === "no-access") {
                handleAccessLost();
            } else if (result === "invalid") {
                input.value = text; // yazdığını kaybetme
                setStatus("mesaj geçersiz", "warning");
            }
        } catch (err) {
            console.error(err);
            input.value = text; // gönderilemediyse yazdığını kaybetme
            setStatus("gönderilemedi", "danger");
        }
    });

    start();
})();