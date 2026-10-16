(function () {
    const card = document.getElementById("chatCard");
    if (!card) return;

    const eventId = parseInt(card.dataset.eventId, 10);
    const me = parseInt(card.dataset.userId, 10);
    const box = document.getElementById("chatMessages");
    const form = document.getElementById("chatForm");
    const input = document.getElementById("chatInput");
    const statusBadge = document.getElementById("chatStatus");

    const formatTime = (iso) =>
        new Date(iso).toLocaleString("tr-TR", { dateStyle: "short", timeStyle: "short" });

    function setStatus(text, kind) {
        statusBadge.textContent = text;
        statusBadge.className = "badge bg-" + kind;
    }

    function scrollToEnd() {
        box.scrollTop = box.scrollHeight;
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

        if (document.visibilityState === "visible") {
            connection.invoke("MarkRead", eventId).catch((err) => console.warn(err));
        }
    });

    connection.onreconnecting(() => setStatus("yeniden bağlanıyor…", "warning"));
    connection.onreconnected(async () => {
        try {
            await connection.invoke("JoinEvent", eventId); // grup üyeliği bağlantıyla birlikte kaybolur
            setStatus("bağlı", "success");
        } catch (err) {
            console.error(err);
            setStatus("bağlantı hatası", "danger");
        }
    });
    connection.onclose(() => setStatus("bağlantı koptu", "danger"));

    async function start() {
        try {
            await connection.start();
            await connection.invoke("JoinEvent", eventId);
            setStatus("bağlı", "success");
        } catch (err) {
            console.error(err);
            setStatus("bağlanamadı", "danger");
        }
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;

        input.value = "";
        try {
            await connection.invoke("SendMessage", eventId, text);
        } catch (err) {
            console.error(err);
            input.value = text; // gönderilemediyse yazdığını kaybetme
            setStatus("gönderilemedi", "danger");
        }
    });

    start();
})();