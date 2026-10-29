# EventPlatform: Proje notları

Bu dosya, projeyi yaparken verdiğim kararları ve nedenlerini içerir. Canlı demo: https://eventplatform-wvls.onrender.com

## 1. Proje nedir?

Kullanıcıların etkinlik oluşturup keşfettiği, katıldığı ve katılımcılarla gerçek zamanlı sohbet ettiği bir web uygulaması. Haritadan konum seçilir, ilgi alanlarına göre etkinlik filtrelenir, yöneticiler uygunsuz içeriği yayından kaldırabilir.

Amacım yalnızca çalışan bir uygulama değil, **savunabildiğim kararlarla yazılmış** bir proje çıkarmaktı: güvenlik, veri tutarlılığı, hata durumları ve yayına alma süreci dahil.

## 2. Teknoloji ve mimari

| Katman | Seçim | Neden |
|---|---|---|
| Web | ASP.NET Core MVC (.NET 10) | Sunucu tarafı render, basit ve güvenli varsayılanlar (anti-forgery, otomatik HTML kodlama) |
| Veri | Entity Framework Core, database-first | Şemayı SQL ile ben tasarladım, sınıflar veritabanından üretildi |
| Veritabanı | PostgreSQL (Neon) | Ücretsiz katmanı var, yerel ve canlı ortam aynı veritabanını kullanabiliyor |
| Gerçek zamanlı | SignalR | WebSocket'i yönetir, gerekirse otomatik olarak başka yönteme düşer |
| Harita | Google Maps JavaScript API | Yer arama (Places), adres çözme (Geocoding) |
| Arayüz | Razor, Bootstrap 5, vanilla JS, flatpickr | Ek framework gerektirmeden hızlı ve okunur |
| Yayın | Docker, Render | Her ortamda aynı şekilde çalışır, GitHub'a push'ta otomatik yayın |

Kod yapısı: Controller'lar ince tutuldu, formlar için ayrı **ViewModel** sınıfları kullanıldı, veritabanı sınıfları (entity) doğrudan ekrana ya da formlara bağlanmadı.

## 3. Veritabanı kararları

- **Tarihler UTC saklanır.** Sütunlar `timestamp without time zone`. Npgsql bu tipte `Kind = Unspecified` bekler, bu yüzden tarihleri yazarken `DateTime.SpecifyKind(..., Unspecified)` kullanıyorum. Tarayıcı UTC değeri kullanıcının yerel saatine çevirir (`<time>` etiketi + küçük bir JS dosyası). Formdan gelen yerel saat, tarayıcının saat dilimi farkıyla UTC'ye çevrilir.
- **Katılımcı sayısı.** `participants` tablosunda etkinliği oluşturan kişi yok. Gösterilen sayı = katılımcılar (oluşturan hariç) + 1, böylece hiç katılımcı yokken "0" değil "1" görünür.
- **Silme zinciri.** Etkinlik silinince mesajlar, katılımcı kayıtları ve okuma kayıtları veritabanındaki `ON DELETE CASCADE` ile silinir. Puan tablosundaki `eventid` ise `SET NULL`: etkinlik silinse de kullanıcının puan geçmişi kalır.
- **Sütun uzunlukları form sınırlarından büyük ya da eşit** tutuldu. Aksi halde doğrulamadan geçen bir veri veritabanında hata verirdi.
- **Etkinlik kısa kodu (`publicid`).** Bölüm 8'de anlatılıyor.

## 4. Güvenlik kararları

- **Şifreler BCrypt ile hashlenir.** Giriş hatasında "kullanıcı yok" ile "şifre yanlış" aynı mesajı verir, böylece kullanıcı adı denenerek hesap listesi çıkarılamaz.
- **Anti-forgery token** tüm durum değiştiren isteklerde (POST) zorunlu.
- **Open redirect koruması.** Giriş sonrası `returnUrl` yalnızca `Url.IsLocalUrl` ile yerel ise kullanılır.
- **Aşırı veri gönderme (over-posting) engellendi.** Formlar ViewModel ile bağlanır; sunucunun belirlediği alanlar (`Isapproved`, rol, oluşturan kullanıcı) `[BindNever]` ya da hiç modelde yok. Rol asla formdan alınmaz, cookie'deki claim'den okunur.
- **Sahiplik kontrolü sorgunun içinde.** Düzenleme ve silmede `Where(e => e.Publicid == kod && e.Createdbyuserid == ben)` kullanılır. Başkasının etkinliği için sonuç "bulunamadı" (404) olur; böylece hem yetkisiz erişim (IDOR) engellenir hem de etkinliğin var olduğu sızmaz.
- **XSS.** Razor çıktıyı otomatik kodlar. Sohbet mesajları `textContent` ile basılır, `innerHTML` kullanılmaz. Sohbet geçmişi sayfaya JSON olarak gömülür; `JsonSerializer` varsayılan olarak `<`, `>`, `&` karakterlerini kaçırır, bu yüzden `<script>` içinde güvenlidir.
- **Arama.** `ILIKE` deseninde `%`, `_`, `\` karakterleri kaçırılır; kullanıcı girdisi desen olarak yorumlanmaz. Arama metni 100 karakterle sınırlı.
- **Cookie.** `HttpOnly`, `Secure`, `SameSite=Lax`, 2 saat kayan süre.
- **Gizli bilgiler repoda yok.** Bağlantı dizesi, Maps anahtarı ve sertifika yerelde user-secrets, canlıda ortam değişkenlerinden gelir. `.gitignore` bunlar için de kural içerir. Bir sır başka biriyle paylaşılırsa tek çözüm onu yenilemektir; bu yüzden hepsi koda değil ortam değişkenine bağlı, değiştirmek kolay.
- **Maps anahtarı kısıtlı.** Yalnızca site adresleri (HTTP referrer) ve yalnızca gereken API'ler için geçerli. Daha önce başkasına ait bir anahtarı kullanmadım; kendi anahtarımı oluşturdum.
- **Sohbet yetkisi sunucuda her çağrıda denetlenir.** İstemcideki "katıldım" bilgisine güvenilmez (Bölüm 7).

## 5. Etkinlik yayını ve moderasyon kararı

**Soru:** Etkinlikler yayınlanmadan önce admin onayı gerekmeli mi?

**Karar:** Hayır. Etkinlikler oluşturulunca hemen yayınlanır; admin uygunsuz olanı sonradan **yayından kaldırabilir** ya da geri alabilir.

**Neden:**
- Onay beklemek yeni kullanıcı için ilk deneyimi bozar; küçük bir platformda tek admin bir darboğaz olur.
- Düzenleme de onay gerektirseydi, her küçük düzeltme yeniden onaya girerdi; sistem karmaşıklaşırdı.
- Risk, yayından kaldırma ve silme yetkisiyle yönetilebilir. Yayından kaldırma geri alınabilir, silme alınamaz; bu yüzden önce kaldırma tercih edilir.

**Uygulama:** `events.isapproved` sütunu artık "yayında mı" anlamına gelir (sütun adı değişmedi, anlamı değişti; bunu kod yorumlarında belirttim). Yayında olmayan etkinliği yalnızca oluşturan, katılımcılar ve admin görebilir; listelerde herkese kapalıdır. Yayında olmayan etkinlikte sohbet, katılma ve ayrılma kapalıdır.

## 6. Düzenleme ve silme

- Yalnızca oluşturan düzenleyebilir; **geçmiş etkinlik düzenlenemez**.
- Silme: oluşturan ya da admin. Silmeden önce tarayıcıda onay istenir.
- "Etkinliklerim" sayfası: oluşturduklarım (durumuyla: yayında / yayından kaldırıldı / sona erdi) ve katıldıklarım.
- Düzenleme formu kayıtlı UTC saati kullanıcının yerel saatine çevirerek açar; kaydederken tekrar UTC'ye çevrilir.

## 7. Gerçek zamanlı sohbet (SignalR)

- Her etkinliğin kendi grubu var (`event-{id}`). Mesaj yalnızca o gruba yayınlanır.
- **Yetki her çağrıda sunucuda kontrol edilir:** etkinlik yayında mı, çağıran oluşturan ya da katılımcı mı?
- **Hata fırlatmak yerine sonuç döndürme.** Hub metotları `"ok" | "no-access" | "invalid"` döndürür. İlk sürümde yetkisiz çağrıda `HubException` fırlatıyordum; bu hem geliştirme ortamında hata penceresi açıyordu hem de normal bir durumu (ör. etkinliğin yayından kaldırılması) hata gibi ele alıyordu.
- **Erişim kaybı.** Sohbet açıkken etkinlik yayından kaldırılır ya da kullanıcı ayrılırsa, sunucu `no-access` döner; istemci uyarıyı gösterip sayfayı yeniler. Kullanıcı gruptan da çıkarılır, canlı mesaj almaya devam etmez.
- **Okunmamış mesaj rozeti.** Her kullanıcı için etkinlik başına "son okuma zamanı" tutulur (`eventreadstatus`). Navbar'daki rozet, bu zamandan sonra başkalarının yazdığı mesajları sayar.
- Mesaj uzunluğu 1000 karakterle sınırlı; boş mesaj kabul edilmez.
- Sohbet geçmişi: son 50 mesaj sayfayla birlikte gelir, canlı mesajlar aynı JS koduyla eklenir.

## 8. Etkinlik adresleri

Adresler `/etkinlik/kadikoy-sahil-fotograf-yuruyusu-k7x2m9ab` biçiminde: etkinlik adından üretilen okunabilir bir yazı (slug) ve 8 karakterlik rastgele bir kısa kod.

- **Neden sıralı numara değil?** `/Events/Details/7` hem çirkin hem de etkinlik sayısını ve sırasını gösteriyor. Kısa kod kriptografik rastgele sayı üreticisiyle üretilir, tahmin edilemez.
- **Etkinliği bulan şey koddur.** Slug yalnızca okunabilirlik içindir. Etkinlik adı değişirse eski slug'lı adres güncel adrese yönlendirilir, bağlantılar bozulmaz.
- **Kod benzersizdir.** Veritabanında benzersiz kısıt var. Çok düşük ihtimalle çakışırsa uygulama yeni kod üretip tekrar dener.
- Düzenleme, silme, katılma ve ayrılma adresleri de aynı koda dayanır (`/etkinlik/{kod}/duzenle`, `/sil`, `/katil`, `/ayril`).
- **Türkçe karakterler** (ç, ğ, ı, ö, ş, ü) kendi eşleme tablosuyla çevrilir. Yerleşik normalizasyon kullanılmadı, çünkü bazı sunucu ortamlarında ICU kurulu olmayabilir.
- **Eski `/Events/Details/7` adreslerini bilerek yönlendirmedim.** Sıralı numarayı çalışır halde tutmak amacıma aykırıydı ve site yeni olduğu için korunması gereken eski bağlantı yoktu. Önce yönlendirmeyi yazmıştım, sonra kaldırdım.

## 9. Yöneticiler ve sohbet gizliliği

Yöneticiler, katılımcı olmasalar bile bir etkinliğin sohbet geçmişini **salt okunur** görebilir. Amaç moderasyon: uygunsuz içerik bildirildiğinde yönetici bakıp etkinliği yayından kaldırabilmeli.

- Yönetici mesaj yazamaz ve canlı akışa bağlanmaz. Sohbet yetkisi sunucuda her çağrıda ayrıca denetlendiği için görüntüleme ayrıcalığı yazma ayrıcalığına dönüşmez.
- Yönetici bakınca mesajlar "okundu" işaretlenmez, böylece katılımcıların bildirim rozeti etkilenmez.
- Bu bir gizlilik kararıdır. Kullanıcılar sohbet kutusunun altındaki notla bilgilendirilir: "Moderasyon amacıyla yöneticiler bu sohbeti görebilir."
- Gerçek bir ürün için ek önlemler gerekir: yönetici erişimini kaydeden bir denetim günlüğü, şikâyet gelmeden bakmayı kısıtlayan bir kural ve gizlilik politikasında açık bir madde.

## 10. Harita ve konum

- Konum iki yolla seçilir: **yer arama** (Places otomatik tamamlama) ya da haritaya tıklama. Tıklamada koordinat adrese çevrilir (Geocoding).
- Harita ve arayüz Türkçe: betik adresine `language=tr&region=TR` eklendi.
- Koordinatlar sunucuda yeniden doğrulanır (sayı mı, geçerli aralıkta mı); noktalı ondalık kültürden bağımsız (`InvariantCulture`) ayrıştırılır. Konum seçilmemişse form kabul edilmez.
- Kullanılan Google bileşeni ve olayların adı sürüme göre değişebiliyor (`gmp-select` / `gmp-placeselect`); kodda her ikisi de dinleniyor.

## 11. Yayına alma

- **Docker**: çok aşamalı derleme (SDK imajında derle, çalışma imajına yalnızca çıktıyı kopyala), uygulama 10000 portunu dinler, root olmayan kullanıcıyla çalışır.
- **Render** GitHub'daki `main` dalını izler; push'ta otomatik yeniden yayınlar. Ayarlar ortam değişkenleriyle verilir.
- **Ters vekil (proxy) başlıkları.** Render'ın önündeki yük dengeleyici HTTPS'i sonlandırır. `X-Forwarded-For/Proto` başlıkları işlenmezse uygulama kendini HTTP sanır, "Secure" cookie ve yönlendirmeler bozulur. `UseForwardedHeaders` bu yüzden ardışık işlemin en başında.
- **Veri koruma anahtarları veritabanında ve şifreli.** ASP.NET Core, cookie ve anti-forgery verisini şifrelemek için anahtarlar üretir. Varsayılan olarak bunlar sunucu diskinde durur; konteyner yeniden başlayınca silinir ve herkesin oturumu düşer. Anahtarları Neon'daki `dataprotectionkeys` tablosuna taşıdım ve kendi ürettiğim bir sertifikayla şifreledim; sertifika (base64) ve parolası ortam değişkenlerinde. Doğrulama: tablodaki XML'de `EncryptedData` bulunur.
- **`/healthz`** sağlık kontrolü adresi; yönlendirme ve kimlik doğrulama gerektirmez.
- **Soğuk başlangıç.** Render'ın ücretsiz katmanı 15 dakika kullanılmayınca uykuya geçer; ilk istek bir dakikaya kadar sürebilir. Neon da boştayken uyur. README'de bunu açıkça belirttim. Dış bir izleme servisi `/healthz`'i düzenli çağırarak uygulamayı uyanık tutabilir; Neon'u bilerek uyanık tutmuyorum.
- HTTPS yönlendirmesini uygulama yalnızca geliştirmede yapar; canlıda bunu Render'ın yük dengeleyicisi yönetir. HSTS yalnızca canlıda açık.

## 12. Karşılaşılan sorunlar ve ders çıkardıklarım

1. **Sırların yönetimi.** Bağlantı dizesi, API anahtarı ve sertifika yalnızca ortam değişkeni ya da user-secrets'te durur, repoya hiç girmez.
2. **Oturumların her yeniden başlatmada düşmesi.** Veri koruma anahtarlarının kalıcı ve şifreli saklanmasıyla çözüldü (Bölüm 11).
3. **Sohbette hata penceresi.** Yetkisiz hub çağrısında istisna fırlatmak yerine sonuç döndürmeye geçtim (Bölüm 7).
4. **Katılımcı sayısı 0 görünmesi.** Oluşturan `participants` tablosunda değil; sayıya +1 eklendi.
5. **Tarih/saat dilimi.** UTC sakla, tarayıcıda yerele çevir; formdan gelen yerel saati UTC'ye çevir.
6. **Sütun uzunluğu uyuşmazlığı.** Form sınırları veritabanı sütunlarından büyük olunca doğrulamadan geçen veri kayıtta hata verdi; sütunlar form sınırlarına göre ayarlandı.
7. **Arayüz dili karışıklığı.** Kategori adları veritabanında İngilizce, ekranda Türkçe olmalıydı: gösterim için ayrı bir eşleme (`CategoryStyle.Label`) kullanıyorum, veri değişmiyor.

## 13. Bilinen sınırlar (bilerek yapılmadı)

- **Otomatik test henüz yok.** Planlandı: slug üretici ve doğrulama mantığı için birim testler, GitHub Actions ile derleme.
- Şifre sıfırlama ve e-posta doğrulama yok (e-posta gönderen bir servis gerektirir).
- Profil fotoğrafı yok (dosya saklama kararı gerektirir).
- Kullanıcı sayısı arttığında sayfalama yok; etkinlik listesi tek sayfada.
- İstek hızı sınırlama (rate limiting) ve şikâyet/raporlama düğmesi yok.
- Sohbet mesajı düzenleme ve silme yok.
- Admin işlemleri için denetim günlüğü yok.
- Ücretsiz katmanlar nedeniyle soğuk başlangıç gecikmesi var.
