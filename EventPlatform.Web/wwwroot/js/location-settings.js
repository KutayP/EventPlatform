// Ayarlar > Konum: yer arama, harita ve çevre mesafesi dairesi.
// Google Maps betiği callback olarak window.initLocationMap'i çağırır (sayfadaki script etiketi).

let map, marker, circle, geocoder, Place, PlaceAutocompleteElement;

const latInput = () => document.getElementById("locLat");
const lngInput = () => document.getElementById("locLng");
const labelInput = () => document.getElementById("locLabel");
const radiusInput = () => document.getElementById("radiusKm");

// "İzmit, Kocaeli" gibi kaba bir etiket üretir (tam adres saklanmaz).
// Yer bileşenleri iki biçimde gelir: Place (longText) ve Geocoder (long_name).
function coarseLabel(components, fallback) {
    const list = components || [];
    const get = (type) => {
        const c = list.find((x) => (x.types || []).includes(type));
        return c ? (c.longText || c.long_name || null) : null;
    };
    const province = get("administrative_area_level_1");
    const district = get("administrative_area_level_2") || get("locality");

    let label;
    if (district && province && district !== province) label = district + ", " + province;
    else label = district || province || fallback || "Seçilen konum";

    return label.length > 100 ? label.slice(0, 100) : label;
}

function updateCircle() {
    const lat = parseFloat(latInput().value);
    const lng = parseFloat(lngInput().value);
    if (isNaN(lat) || isNaN(lng)) return;

    const center = { lat, lng };
    const radiusM = parseInt(radiusInput().value, 10) * 1000;

    if (!circle) {
        circle = new google.maps.Circle({
            map, center, radius: radiusM,
            strokeWeight: 1, strokeColor: "#4f46e5",
            fillColor: "#4f46e5", fillOpacity: 0.12,
            clickable: false
        });
    } else {
        circle.setCenter(center);
        circle.setRadius(radiusM);
    }
}

function setPoint(latLng, label) {
    latInput().value = latLng.lat().toFixed(6);
    lngInput().value = latLng.lng().toFixed(6);
    if (label) labelInput().value = label;

    if (!marker) {
        marker = new google.maps.Marker({ position: latLng, map, draggable: true });
        marker.addListener("dragend", () => resolveLabel(marker.getPosition()));
    } else {
        marker.setPosition(latLng);
    }
    updateCircle();
}

async function resolveLabel(latLng) {
    latInput().value = latLng.lat().toFixed(6);
    lngInput().value = latLng.lng().toFixed(6);
    updateCircle();

    try {
        const res = await geocoder.geocode({ location: latLng });
        const r = res.results && res.results[0];
        if (r) {
            labelInput().value = coarseLabel(r.address_components, r.formatted_address);
            return;
        }
    } catch (err) {
        console.error("Geocoding API hatası:", err);
    }
    labelInput().value = "Seçilen konum";
}

async function onMapClick(e) {
    if (e.placeId) e.stop(); // isimli yerlerde Google'ın kendi bilgi penceresi açılmasın
    setPoint(e.latLng, null);
    await resolveLabel(e.latLng);
}

async function selectPlace(place) {
    try {
        await place.fetchFields({ fields: ["displayName", "formattedAddress", "location", "viewport", "addressComponents"] });
    } catch (err) {
        console.warn("Yer bilgisi alınamadı:", err);
        return;
    }
    if (!place.location) return;

    const label = coarseLabel(place.addressComponents, place.displayName || place.formattedAddress);
    setPoint(place.location, label);

    if (place.viewport) map.fitBounds(place.viewport);
    else { map.setCenter(place.location); map.setZoom(12); }
}

function setupSearch() {
    const group = document.getElementById("searchGroup");
    const box = document.getElementById("placeSearch");
    if (!PlaceAutocompleteElement) {
        group.hidden = true; // Places yüklenemediyse arama gizlenir, harita tıklaması çalışır
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

async function initLocationMap() {
    try {
        ({ Place, PlaceAutocompleteElement } = await google.maps.importLibrary("places"));
    } catch (err) {
        console.warn("Places kütüphanesi yüklenemedi:", err);
    }

    geocoder = new google.maps.Geocoder();
    map = new google.maps.Map(document.getElementById("map"), {
        zoom: 8,
        center: { lat: 40.7654, lng: 29.9408 } // Kocaeli
    });
    map.addListener("click", onMapClick);
    setupSearch();

    radiusInput().addEventListener("change", updateCircle);

    // Kayıtlı konum varsa göster.
    const lat = parseFloat(latInput().value);
    const lng = parseFloat(lngInput().value);
    if (!isNaN(lat) && !isNaN(lng)) {
        const pos = new google.maps.LatLng(lat, lng);
        map.setCenter(pos);
        map.setZoom(10);
        setPoint(pos, null);
    }
}
window.initLocationMap = initLocationMap;
