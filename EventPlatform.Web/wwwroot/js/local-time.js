// <time class="js-local" datetime="2026-10-21T14:16:00Z"> öğelerini kullanıcının yerel saatine çevirir.
document.querySelectorAll("time.js-local").forEach((el) => {
    const d = new Date(el.getAttribute("datetime"));
    if (!isNaN(d)) {
        el.textContent = d.toLocaleString("tr-TR", { dateStyle: "short", timeStyle: "short" });
    }
});