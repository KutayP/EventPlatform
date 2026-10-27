// Etkinlik oluşturma / düzenleme formunun harita, konum arama ve tarih kodu.
// Google Maps betiği callback olarak window.initMap'i çağırır (sayfadaki script etiketi).

let map, marker, geocoder, Place, PlaceAutocompleteElement;

function placeMarker(latLng) {
    if (!marker) {
        marker = new google.maps.Marker({ position: latLng, map: map, draggable: true });
        marker.addListener("dragend", () => fillLocation(marker.getPosition(), null));
    } else {
        marker.setPosition(latLng);
    }
}

function setCoordinates(latLng) {
    document.getElementById("Latitude").value = latLng.lat().toFixed(6);
    document.getElementById("Longitude").value = latLng.lng().toFixed(6);
}

async function fillLocation(latLng, placeId) {
    setCoordinates(latLng);
    const lat = document.getElementById("Latitude").value;
    const lng = document.getElementById("Longitude").value;
    const field = document.getElementById("Location");

    // 1) İsimli bir yere (restoran, okul, park...) tıklandıysa adını ve adresini al.
    if (placeId && Place) {
        try {
            const place = new Place({ id: placeId });
            await place.fetchFields({ fields: ["displayName", "formattedAddress"] });
            if (place.displayName) {
                field.value = place.formattedAddress
                    ? place.displayName + " – " + place.formattedAddress
                    : place.displayName;
                return;
            }
        } catch (err) {
            console.warn("Places API hatası:", err);
        }
    }

    // 2) İsimsiz bir yerse sadece adres.
    try {
        const res = await geocoder.geocode({ location: latLng });
        if (res.results && res.results[0]) {
            field.value = res.results[0].formatted_address;
            return;
        }
    } catch (err) {
        console.error("Geocoding API hatası:", err);
    }

    // 3) Google'dan bir şey alınamadıysa koordinatı yaz (hata konsolda görünür).
    field.value = lat + ", " + lng;
}

async function onMapClick(e) {
    if (e.placeId) e.stop(); // isimli yerlerde Google'ın kendi bilgi penceresi açılmasın
    placeMarker(e.latLng);
    await fillLocation(e.latLng, e.placeId);
}

// Arama kutusundan seçilen yeri haritaya ve forma uygula.
async function selectPlace(place) {
    try {
        await place.fetchFields({
            fields: ["displayName", "formattedAddress", "location", "viewport"]
        });
    } catch (err) {
        console.warn("Yer bilgisi alınamadı:", err);
        return;
    }
    if (!place.location) return;

    placeMarker(place.location);
    setCoordinates(place.location);

    if (place.viewport) {
        map.fitBounds(place.viewport);
        // Küçük yerlerde (bina, iskele) aşırı yakınlaşmasın.
        google.maps.event.addListenerOnce(map, "idle", () => {
            if (map.getZoom() > 17) map.setZoom(17);
        });
    } else {
        map.setCenter(place.location);
        map.setZoom(16);
    }

    const field = document.getElementById("Location");
    field.value = place.displayName && place.formattedAddress
        ? place.displayName + " – " + place.formattedAddress
        : (place.formattedAddress || place.displayName || "");
}

function setupSearch() {
    const group = document.getElementById("searchGroup");
    const box = document.getElementById("placeSearch");
    if (!PlaceAutocompleteElement) {
        // Places kütüphanesi yüklenemediyse arama gizlenir, harita tıklaması çalışmaya devam eder.
        group.hidden = true;
        return;
    }

    const autocomplete = new PlaceAutocompleteElement({ includedRegionCodes: ["tr"] });
    box.appendChild(autocomplete);

    // Yeni sürümlerde "gmp-select", eskilerde "gmp-placeselect" olayı gelir.
    autocomplete.addEventListener("gmp-select", async (ev) => {
        await selectPlace(ev.placePrediction.toPlace());
    });
    autocomplete.addEventListener("gmp-placeselect", async (ev) => {
        await selectPlace(ev.place);
    });
}

async function initMap() {
    try {
        ({ Place, PlaceAutocompleteElement } = await google.maps.importLibrary("places"));
    } catch (err) {
        console.warn("Places kütüphanesi yüklenemedi:", err);
    }

    geocoder = new google.maps.Geocoder();
    map = new google.maps.Map(document.getElementById("map"), {
        zoom: 12,
        center: { lat: 40.7654, lng: 29.9408 } // Kocaeli
    });
    map.addListener("click", onMapClick);
    setupSearch();

    // Düzenleme ya da doğrulama hatasıyla geri gelen formda seçili konumu göster.
    const lat = parseFloat(document.getElementById("Latitude").value);
    const lng = parseFloat(document.getElementById("Longitude").value);
    if (!isNaN(lat) && !isNaN(lng)) {
        const pos = new google.maps.LatLng(lat, lng);
        map.setCenter(pos);
        map.setZoom(15);
        placeMarker(pos);
    }
}

// Düzenleme: kayıtlı tarih UTC'dir, kullanıcının yerel saatine çevirip forma yaz.
function prefillLocalDate() {
    const el = document.getElementById("Date");
    const utc = el && el.dataset.utc;
    if (!utc) return;

    const d = new Date(utc);
    if (isNaN(d.getTime())) return;

    if (el._flatpickr) {
        el._flatpickr.setDate(d, false);
    } else {
        const pad = (n) => String(n).padStart(2, "0");
        el.value = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` +
            `T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }
}
prefillLocalDate();
document.addEventListener("DOMContentLoaded", prefillLocalDate);

// Seçilen tarih için tarayıcının UTC farkını gönder (yaz/kış saati dahil).
document.getElementById("createForm").addEventListener("submit", () => {
    const v = document.getElementById("Date").value;
    const d = v ? new Date(v) : new Date();
    document.getElementById("TimezoneOffsetMinutes").value = d.getTimezoneOffset();
});