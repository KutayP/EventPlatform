# EventPlatform: Proje notları

Bu dosya, projeyi yaparken verdiğim kararları ve nedenlerini içerir. Canlı demo: https://eventplatform-wvls.onrender.com

## 1. Proje nedir?

Kullanıcıların etkinlik oluşturup keşfettiği, katıldığı ve katılımcılarla gerçek zamanlı sohbet ettiği sosyal bir web uygulaması. Haritadan konum seçilir, ilgi alanlarına göre etkinlik filtrelenir. Kullanıcılar arkadaş ekler, profillerini yalnızca arkadaşlarına açabilir, anlık bildirim alır ve isterlerse ilgi alanlarına uyan, yakınlarındaki yeni etkinliklerden haberdar olur. Yöneticiler uygunsuz içeriği yayından kaldırabilir.

Amacım yalnızca çalışan bir uygulama değil, **savunabildiğim kararlarla yazılmış** bir proje çıkarmaktı: güvenlik, veri tutarlılığı, hata durumları ve yayına alma süreci dahil.

## 2. Teknoloji ve mimari

| Katman | Seçim | Neden |
|---|---|---|
| Web | ASP.NET Core MVC (.NET 10) | Sunucu tarafı render, basit ve güvenli varsayılanlar (anti-forgery, otomatik HTML kodlama) |
| Veri | Entity Framework Core, database-first | Şemayı SQL ile ben tasarladım, sınıflar veritabanından üretildi |
| Veritabanı | PostgreSQL (Neon) | Ücretsiz katmanı var, yerel ve canlı ortam aynı veritabanını kullanabiliyor |
| API | REST (JSON), JWT, OpenAPI + Scalar | Sitenin verisine başka istemcilerin (mobil, oyun, betik) de erişebilmesi |
| Gerçek zamanlı | SignalR | WebSocket'i yönetir, gerekirse otomatik olarak başka yönteme düşer |
| Harita | Google Maps JavaScript API | Yer arama (Places), adres çözme (Geocoding) |
| Arayüz | Razor, Bootstrap 5.3, vanilla JS, flatpickr | Ek framework gerektirmeden hızlı ve okunur; açık/koyu tema Bootstrap'in renk modlarıyla |
| Arama | PostgreSQL `pg_trgm` | Yazım hatalarına dayanıklı kullanıcı araması, ek servis gerektirmez |
| Test / CI | xUnit, GitHub Actions | Her push'ta derleme, birim testler ve Docker imajı derlemesi |
| Yayın | Docker, Render | Her ortamda aynı şekilde çalışır, GitHub'a push'ta otomatik yayın |

Kod yapısı: Controller'lar ince tutuldu, formlar için ayrı **ViewModel** sınıfları kullanıldı, veritabanı sınıfları (entity) doğrudan ekrana ya da formlara bağlanmadı. Birden çok yerden kullanılan iş mantığı (bildirimler) ayrı bir servis sınıfında (`NotificationService`); navbar'daki zil gibi her sayfada görünen parçalar ViewComponent.

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
- **Herkese açık demo hesabı.** README'de bir demo girişi var. Bu hesapla şifre ve kişisel bilgiler değiştirilemez; örnek veriden gelen etkinlikler (`events.issample`) düzenlenemez ve silinemez, ziyaretçinin kendi oluşturduğu etkinlikler ise serbestçe düzenlenip silinebilir; kontrol sunucuda yapılır, arayüzde de her sayfanın üstünde "demo oturumu" şeridi görünür. Amaç, bir ziyaretçinin şifreyi değiştirip hesabı başkalarına kapatmasını ya da örnek veriyi bozmasını engellemek. Etkinlik oluşturma ve sohbet açık; kötüye kullanımda admin yayından kaldırır, gerekirse örnek veri yeniden yüklenir.
- **Örnek veri ve gece sıfırlaması.** Canlı sitedeki kullanıcılar, etkinlikler, sohbetler ve bildirimler kurgusaldır; `db/seed.sql` ile yüklenir (şehirlere ve kategorilere dağıtılmış, geçmiş ve gelecek tarihli). Zamanlanmış bir GitHub Actions iş akışı bu betiği her gece çalıştırır: ziyaretçilerin değişiklikleri silinir ve tarihler çalıştırıldığı güne göre üretildiği için etkinlikler hep "yaklaşan" kalır. Şifreler dosyada yok; GitHub secret'larından psql değişkeni olarak verilir ve veritabanının kendi BCrypt uygulamasıyla (`pgcrypto`) hashlenir. Değişken eksikse betik hiçbir şeyi silmeden durur.
- **Silinen kullanıcının oturumu.** Betik kimlik numaralarını sıfırlamaz, böylece eski bir çerez yeni bir kullanıcıyla eşleşemez. Ayrıca her istekte (bir dakikalık önbellekle) çerezdeki kullanıcının numarası ve adıyla hâlâ var olduğu kontrol edilir; yoksa oturum kapatılır.
- **Kimlik her zaman cookie'den gelir.** Arkadaşlık isteği, bildirim okuma ya da ayar değiştirme gibi işlemlerde kullanıcı numarası formdan alınmaz; sorgular "sahibi ben olan kayıt" şartını içerir.

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
- **Okunmamış mesajlar.** Her kullanıcı için etkinlik başına "son okuma zamanı" tutulur (`eventreadstatus`). Bu zamandan sonra başkalarının yazdığı mesajı olan etkinlikler bildirim zilinde "X etkinliğinde 3 yeni mesaj" olarak görünür (Bölüm 14). Yeni mesaj gelince diğer katılımcıların zili anında güncellenir.
- Mesaj uzunluğu 1000 karakterle sınırlı; boş mesaj kabul edilmez.
- Sohbet geçmişi: son 50 mesaj sayfayla birlikte gelir, canlı mesajlar aynı JS koduyla eklenir.

## 8. Etkinlik adresleri

Adresler `/events/kadikoy-sahil-fotograf-yuruyusu-k7x2m9ab` biçiminde: etkinlik adından üretilen okunabilir bir yazı (slug) ve 8 karakterlik rastgele bir kısa kod.

- **Neden sıralı numara değil?** `/Events/Details/7` hem çirkin hem de etkinlik sayısını ve sırasını gösteriyor. Kısa kod kriptografik rastgele sayı üreticisiyle üretilir, tahmin edilemez.
- **Etkinliği bulan şey koddur.** Slug yalnızca okunabilirlik içindir. Etkinlik adı değişirse eski slug'lı adres güncel adrese yönlendirilir, bağlantılar bozulmaz.
- **Kod benzersizdir.** Veritabanında benzersiz kısıt var. Çok düşük ihtimalle çakışırsa uygulama yeni kod üretip tekrar dener.
- Düzenleme, silme, katılma ve ayrılma adresleri de aynı koda dayanır (`/events/{kod}/edit`, `/delete`, `/join`, `/leave`).
- **Türkçe karakterler** (ç, ğ, ı, ö, ş, ü) kendi eşleme tablosuyla çevrilir. Yerleşik normalizasyon kullanılmadı, çünkü bazı sunucu ortamlarında ICU kurulu olmayabilir.
- **Tüm sayfa adresleri İngilizce ve küçük harfli** (`/events`, `/events/new`, `/friends`, `/notifications`, `/settings/profile`). Başta bazı adresler Türkçeydi (`/etkinlik`, `/arkadaslar`); karışık görünüyordu, tek dile çektim. Site yeni olduğu için eski adresler yönlendirilmedi.
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

## 12. Profiller ve gizlilik

- Her kullanıcının herkese açık bir profil sayfası var: `/u/kullaniciadi`. Sayfada kullanıcı adı, **ad ve soyadın baş harfi** ("Deniz A."), ilgi alanları, oluşturduğu ve katıldığı etkinlikler görünür.
- **E-posta, doğum tarihi ve cinsiyet hiçbir zaman başkasına gösterilmez.** Bu bilgiler yalnızca kişinin kendi ayarlar sayfasında durur.
- **Görünürlük ayarı:** "Herkese açık" ya da "Yalnızca arkadaşlarım". Kapalı profili yalnızca arkadaşlar, profilin sahibi ve yöneticiler görür; diğerleri yalnızca adı ve arkadaşlık düğmesini görür. İzin verilen değerler veritabanında `CHECK` kısıtıyla da sınırlı.
- "Profilim" kendi profil sayfama gider; düzenleme ayrı bir yerde (Ayarlar, Bölüm 15). Böylece kullanıcı profilinin başkalarına nasıl göründüğünü doğrudan görür.

## 13. Arkadaşlık ve kullanıcı arama

- `friendships` tablosunda iki kişi arasında **tek bir kayıt** olabilir (yönden bağımsız benzersiz indeks). Durum: `pending` ya da `accepted`.
- İki taraf birbirine aynı anda istek gönderirse ikinci istek doğrudan kabul sayılır.
- Reddetme ve iptal kaydı siler; tekrar istek gönderilebilir. Bir kullanıcının bekleyen en fazla 50 isteği olabilir (istek yağdırmayı sınırlar).
- Aynı anda iki istek gelirse benzersiz kısıt ikincisini engeller; uygulama hatayı (`23505`) yakalayıp anlaşılır bir mesaj gösterir.
- **Arama:** kullanıcı adı, ad ve soyadın baş harfi üzerinde yapılır (başkalarına gösterdiğimiz bilgi kadar). "kaan y", "@Kaan_Y." ve "kaan_y" aynı sonucu verir: metin hem C#'ta hem SQL'de aynı kuralla sadeleştirilir (Türkçe harfler ASCII'ye, noktalama boşluğa). Birebir eşleşmeler önce, yazım hatalı yakın sonuçlar `pg_trgm` benzerliğiyle arkadan gelir. Arama metni SQL'e yalnızca parametre olarak gider.
- **Arkadaşlarının etkinlikleri:** ana sayfada "arkadaşlarının oluşturduğu" ve "arkadaşlarının katıldığı" yaklaşan etkinlikler ayrı bölümlerde. Kendi oluşturduğum ve zaten katıldığım etkinlikler burada tekrar gösterilmez; onlar "Yaklaşan etkinliklerim" bölümünde (önümüzdeki 7 gün, en fazla 3) hatırlatma olarak durur.

## 14. Bildirimler

- **Türler:** etkinliğime biri katıldı, arkadaşlık isteği geldi, isteğim kabul edildi, ilgi alanıma uygun yeni etkinlik. Okunmamış sohbet mesajları da aynı zilde görünür ama ayrı kayıt olarak saklanmaz, her seferinde `eventreadstatus` üzerinden hesaplanır.
- **Metin saklanmaz.** `notifications` tablosu yalnızca tür, alıcı, işlemi yapan ve etkinliği tutar; ekrandaki cümle gösterim sırasında üretilir. Böylece etkinliğin adı değişirse bildirim de güncel adı gösterir, etkinlik silinirse bildirimi de silinir (`ON DELETE CASCADE`).
- **Geçersiz kalan bildirim silinir.** İstek iptal edilince, reddedilince ya da kabul edilince "istek geldi" bildirimi; katılımcı etkinlikten ayrılınca "katıldı" bildirimi kaldırılır. Zildeki "Kabul et / Reddet" düğmeleri bu sayede yalnızca hâlâ bekleyen isteklerde görünür.
- **Anlık iletim (SignalR):** ayrı bir hub (`/hubs/notifications`) yalnızca "bildirimlerin değişti" haberi gönderir; `Clients.User(id)` cookie'deki kimliğe göre yalnızca o kişinin bağlantılarına ulaşır. Tarayıcı bu haberi alınca zil menüsünü sunucudan yeniden çeker. Menünün HTML'i sunucuda Razor ile üretildiği için JavaScript'te metin birleştirip XSS riski oluşturmuyorum.
- **Zil yalnızca okunmamışları gösterir**; "Tümünü gör" sayfası tüm geçmişi Yeni / Son 7 gün / Son 30 gün / Daha önce gruplarıyla sayfa sayfa listeler. Gruplama saat dilimi gerektirmesin diye takvim gününe değil yaşa göre.
- **Bildirim asıl işlemi bozmaz.** Bildirim oluşturma ya da gönderme hata verirse günlüğe yazılır, katılma ya da arkadaşlık işlemi yine tamamlanır.
- Her türü kullanıcı ayarlardan kapatabilir (Bölüm 15).

## 15. Ayarlar ve tema

- Ayarlar tek bir yerde, sol menülü kategorilerle: profil bilgileri, ilgi alanları, gizlilik, bildirimler, konum, görünüm, güvenlik (`/settings/...`).
- Bildirim tercihleri ve mesafe `usersettings` tablosunda. Satır ilk ayar değişikliğinde oluşturulur; yoksa varsayılanlar geçerli. Aynı anda iki istek satır eklemeye çalışırsa benzersiz kısıt hatası yakalanıp güncellemeyle tekrar denenir.
- **Tema:** açık, koyu ya da cihaz ayarını izle. Bootstrap 5.3'ün `data-bs-theme` özelliğiyle uygulanır. Tema bir hesap bilgisi değil cihaz tercihi olduğu için çerezde tutulur (telefonda koyu, bilgisayarda açık olabilir).
- Sayfa çizilmeden önce `<head>` içindeki küçük bir betik temayı uygular; açılışta beyaz parlama olmaz. "Cihaz ayarı" seçiliyken sistem teması değişince site de değişir.
- **Paletler:** açık ve koyu temanın her biri için dört renk paleti. Tüm renkler CSS değişkenlerine bağlandı; palet yalnızca değişkenleri değiştirir, düğmeler, bağlantılar ve vurgular buna göre değişir. Görünümlerdeki sabit `bg-white` gibi sınıflar koyu temada CSS ile düzeltildi.
- Şifre alanlarındaki göster/gizle düğmesi tüm şifre alanlarına tek bir betikle eklenir; görünümlerde değişiklik gerekmedi.

## 16. Konum ve etkinlik önerileri

- **İsteğe bağlı ve varsayılan kapalı.** Kullanıcı konumunu ve mesafeyi (10–250 km) Ayarlar > Konum'dan seçer, öneri bildirimini Ayarlar > Bildirimler'den açar.
- **Konum gizliliği.** Koordinatlar iki ondalığa yuvarlanarak (yaklaşık 1 km) saklanır; etiket "İzmit, Kocaeli" gibi kabadır, tam adres saklanmaz. Konum profilde ya da başka bir yerde kimseye gösterilmez, yalnızca eşleştirmede kullanılır ve tek tuşla silinebilir.
- **Eşleştirme etkinlik oluşturulunca, tek SQL ile:** kategorisi kullanıcının ilgi alanlarından biri olan, kullanıcının seçtiği mesafe içinde kalan (haversine formülü) ve öneriyi açmış kullanıcılar için `INSERT ... SELECT` ile bildirim eklenir, en yakından başlayarak.
- **Spam sınırları:** bir etkinlik en fazla 200 kişiye önerilir; bir kişiye günde en fazla 3 öneri gider; aynı etkinlik aynı kişiye iki kez önerilmez (`(userid, eventid)` üzerinde kısmi benzersiz indeks + `ON CONFLICT DO NOTHING`). Etkinliği oluşturana öneri gitmez.
- **Yakındaki etkinlikler önizlemesi:** konum sayfası, seçili konumun çevresindeki en yakın 3 yaklaşan etkinliği gösterir. Önce kaba bir enlem/boylam kutusuyla veritabanında daraltılır, sonra gerçek mesafe hesaplanır. Konum seçilmemişken belirgin biçimde "örnek" olarak işaretlenmiş bir önizleme gösterilir; amaç kullanıcıya özelliğin ne işe yaradığını göstermek.

## 17. REST API

Site sunucu tarafında render edilen bir MVC uygulaması; aynı veriye tarayıcı dışından (mobil uygulama, betik, başka bir servis) erişilebilmesi için `/api/v1` altında ayrı bir JSON API ekledim. Uç noktalar README'de listeli; canlı belge `/api/docs/v1` adresinde.

- **Ayrı kimlik doğrulama şeması.** Site cookie ile çalışır, API ise JWT (`Authorization: Bearer ...`) ile. JwtBearer şeması yalnızca API controller'larında kullanılır; API cookie'yi kabul etmediği için tarayıcının otomatik gönderdiği çerezle istek atılamaz, yani API'de CSRF riski yok ve anti-forgery token gerekmez.
- **Token.** HMAC-SHA256 ile imzalanır, 1 saat geçerlidir; issuer, audience, imza ve süre doğrulanır. İmza anahtarı koddan değil yapılandırmadan gelir (yerelde user-secrets, canlıda `Jwt__SigningKey`). Yerel ve canlı ortam ayrı anahtar kullanır: birinde üretilen token diğerinde geçmez. Anahtar tanımlı değilse uygulama yine açılır ama geçici rastgele bir anahtar kullanır ve günlüğe uyarı yazar.
- **Giriş uç noktası korumalı.** IP başına dakikada 10 istek sınırı var (aşılırsa 429). Kullanıcı yoksa da sahte bir BCrypt karşılaştırması yapılır; böylece yanıt süresinden kullanıcı adının var olup olmadığı anlaşılamaz.
- **Hatalar ProblemDetails biçiminde.** Geçersiz parametre 400, token yok 401, etkinlik yok 404, zaten katılmış / katılmamış / kendi etkinliği 409. Bilinmeyen kategori gönderilirse 400 yanıtı geçerli değerleri listeler (kategoriler veritabanında İngilizce; `Oyun` değil `Gaming`). Sitenin hata sayfası yönlendirmesi `/api` için kapalı, API her zaman JSON döner.
- **Sayfalama.** Liste uç noktası `page` ve `pageSize` (1–50) alır, `totalCount` döner; sınırsız liste istenemez.
- **Belge.** OpenAPI belgesi .NET'in kendi paketiyle üretilir, yalnızca `/api` uç noktalarını içerir; kilit simgesi yalnızca token gerektiren uç noktalarda görünür. Arayüz için önce Swagger UI kullandım, yanıtlar okunaklı ayrılmadığı için Scalar'a geçtim.
- **İş mantığı tekrar edilmedi.** API'den katılınca da etkinlik sahibine aynı bildirim gider; görünürlük ve yayın kuralları sitedekiyle aynı sorgularla uygulanır.

## 18. Çift gönderim (idempotency)

"Oluştur" butonuna art arda basınca aynı etkinlik iki kez oluştu. Üç katmanda çözdüm:

- **Tarayıcı:** form gönderilince submit butonları kilitlenir (geri tuşuyla dönülünce tekrar açılır).
- **Sunucu:** kaydetmeden önce aynı kullanıcının aynı ad, tarih ve konumla etkinliği var mı diye bakılır; varsa yenisi oluşturulmaz, var olana yönlendirilir.
- **Veritabanı:** `(oluşturan, ad, tarih, konum)` üzerinde benzersiz indeks. İki istek aynı anda kontrolü geçse bile ikincisi indekse takılır; uygulama bu hatayı yakalayıp yine var olan etkinliğe yönlendirir. Asıl garanti buradadır, ilk iki katman kullanıcı deneyimi içindir.

## 19. Testler ve sürekli entegrasyon

- `EventPlatform.Tests` (xUnit) veritabanı gerektirmeyen saf mantığı test eder: adres slug'ı ve kısa kod ayrıştırma, kısa kod üretimi ve doğrulama, arama metni sadeleştirme, mesafe hesabı, "Deniz A." gösterimi, bildirim metinleri ve "5 dk önce" biçimi, profil görünürlük değerleri, JWT üretimi ve doğrulaması (yanlış anahtar, süresi dolmuş token).
- GitHub Actions her push'ta çözümü derler, testleri çalıştırır ve Docker imajını derler (yayınlamaz). README'deki rozet son durumu gösterir.
- Veritabanına bağlı akışlar (arkadaşlık, bildirim, öneri) şu an elle test ediliyor; bunlar için gerçek bir PostgreSQL ile entegrasyon testi sonraki adım (Bölüm 21).

## 20. Karşılaşılan sorunlar ve ders çıkardıklarım

1. **Sırların yönetimi.** Bağlantı dizesi, API anahtarı ve sertifika yalnızca ortam değişkeni ya da user-secrets'te durur, repoya hiç girmez.
2. **Oturumların her yeniden başlatmada düşmesi.** Veri koruma anahtarlarının kalıcı ve şifreli saklanmasıyla çözüldü (Bölüm 11).
3. **Sohbette hata penceresi.** Yetkisiz hub çağrısında istisna fırlatmak yerine sonuç döndürmeye geçtim (Bölüm 7).
4. **Katılımcı sayısı 0 görünmesi.** Oluşturan `participants` tablosunda değil; sayıya +1 eklendi.
5. **Tarih/saat dilimi.** UTC sakla, tarayıcıda yerele çevir; formdan gelen yerel saati UTC'ye çevir.
6. **Sütun uzunluğu uyuşmazlığı.** Form sınırları veritabanı sütunlarından büyük olunca doğrulamadan geçen veri kayıtta hata verdi; sütunlar form sınırlarına göre ayarlandı.
7. **Arayüz dili karışıklığı.** Kategori adları veritabanında İngilizce, ekranda Türkçe olmalıydı: gösterim için ayrı bir eşleme (`CategoryStyle.Label`) kullanıyorum, veri değişmiyor.
8. **Kültüre bağlı sayı okuma.** Konum ayarında tarayıcıdan gelen `40.765400` değeri, Türkçe kültürde çalışan sunucuda noktayı binlik ayracı sayarak `40765400` olarak okundu ve form reddedildi. Koordinatları artık kültürden bağımsız (`InvariantCulture`) ayrıştırıyorum.
9. **`ILIKE` kaçış karakteri.** Npgsql, `EF.Functions.ILike` çağrısında kaçış karakteri verilmezse ters eğik çizgiyle kaçışı kapatıyor; `%` ve `_` karakterleri yine joker sayılıyordu. Kaçış karakterini açıkça (`"\\"`) veriyorum.
10. **Yanlışlıkla commit'lenen dosyalar.** Repoya giren zip dosyalarını yalnızca silmek yetmedi, geçmişten de temizledim ve `.gitignore`'a kural ekledim.
11. **Butona art arda basınca çift kayıt.** İstemci kilidi tek başına yetmez; asıl çözüm veritabanındaki benzersiz indeks (Bölüm 18).

## 21. Bilinen sınırlar (bilerek yapılmadı)

- Veritabanına bağlı akışlar için otomatik entegrasyon testi yok (birim testler var).
- Şifre sıfırlama ve e-posta doğrulama yok (e-posta gönderen bir servis gerektirir).
- Profil fotoğrafı yok (dosya saklama kararı gerektirir).
- Sitedeki etkinlik listesinde sayfalama yok (API'de var); kullanıcı sayısı artınca gerekir.
- İstek hızı sınırı yalnızca API giriş uç noktasında var; sitenin giriş formunda ve diğer uç noktalarda yok. Şikâyet/raporlama düğmesi yok.
- API'de yenileme token'ı (refresh token) ve token iptali yok; token 1 saat sonra kendiliğinden geçersiz olur, sonra tekrar giriş gerekir. API şu an yalnızca okuma ve katılma/ayrılma sunuyor; etkinlik oluşturma sitede.
- Sohbet mesajı düzenleme ve silme yok.
- Admin işlemleri için denetim günlüğü yok.
- Eski bildirimler otomatik silinmiyor; büyük ölçekte bir saklama süresi gerekir.
- Öneri yalnızca etkinlik oluşturulunca hesaplanır; etkinliğin konumu ya da kategorisi sonradan değişirse yeniden önerilmez.
- Kullanıcı araması tüm kullanıcıları tarar; büyük ölçekte trigram (GIN) indeksi gerekir.
- Tema tercihi cihaz başına (çerez); hesapla senkronize değil.
- Gece sıfırlaması tüm verileri siler; ziyaretçilerin demo sitesinde açtığı hesaplar da gider (demo için bilinçli bir tercih).
- GitHub, 60 gün hiç değişiklik olmayan depolarda zamanlanmış iş akışlarını durdurur; o durumda Actions sekmesinden yeniden etkinleştirilmeli.
- Ücretsiz katmanlar nedeniyle soğuk başlangıç gecikmesi var.
