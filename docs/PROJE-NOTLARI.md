# EventPlatform Proje Notları

Bu belge projedeki kararları ve nedenlerini içerir. Mülakatta "neden böyle yaptın?" sorularına cevap hazırlığı olarak da kullanılır.

## 1. Mimari

- **ASP.NET Core MVC (.NET 10):** Sunucu tarafında Razor view'lar. Sayfa yönlendirme, form doğrulama ve yetkilendirme tek yerde, JavaScript yalnızca gereken yerlerde (harita, chat, tarih seçici).
- **Katmanlar:** Controller → EF Core `AppDbContext` → PostgreSQL. Formlar için ayrı **ViewModel**'ler kullanıldı; entity'ler doğrudan formlara bağlanmaz.
- **Veritabanı-önce (database-first):** Şema Neon üzerinde SQL ile kuruldu, entity'ler ondan üretildi. Sebep: şemayı (kısıtlar, indeksler, cascade) doğrudan kontrol etmek.
- **Gerçek zamanlı chat:** SignalR. Her etkinlik için bir grup (`event-{id}`). Sunucu, her hub çağrısında kullanıcının o etkinliğin sohbetine erişim hakkını yeniden doğrular; grup adını bilmek yetmez.
- **Okunmamış mesaj rozeti:** Her kullanıcı ve etkinlik için tek satırlık "okuma işareti" tablosu (`eventreadstatus`, son okunma zamanı). Rozet, sorgu anında bu işaretten sonra gelen başkalarının mesajları sayılarak hesaplanır; mesaj başına okundu kaydı tutulmaz, bu yüzden ucuzdur.

## 2. Veritabanı kararları

- **Sütun uzunlukları, form sınırlarından küçük olmamalı.** İki kez yaşadığımız hata: formda 255 karakter serbestken sütun `varchar(100)` olduğu için `22001 (value too long)` hatası alındı. Kural: önce form sınırı belirlenir, sütun en az o kadar olur. Sonradan tüm kullanıcı alanları gözden geçirildi.
- **Zaman:** Sütunlar `timestamp without time zone`. Her zaman UTC saklanır; Npgsql için `DateTimeKind.Unspecified` yapılır. Gösterim tarayıcıda yerel saate çevrilir (JS), form girişi de UTC'ye çevrilerek gönderilir.
- **Büyük/küçük harfe duyarsız benzersiz kullanıcı adı:** `lower(username)` üzerinde benzersiz indeks. `Ali` ve `ali` aynı kullanıcı adı sayılır.
- **Cascade silme:** Kullanıcı ya da etkinlik silinince bağlı kayıtlar (mesaj, katılım, okuma işareti) yetim kalmasın diye yabancı anahtarlar cascade tanımlı.
- **İlgi alanları:** `userinterests` ayrı bir gerçek entity (ek `Time` sütunu olduğu için); kategoriler `interests` tablosundan okunur, koda gömülü değildir.
- **Arama:** `ILike` ve kaçışlanmış desen. Kullanıcının yazdığı `%` ve `_` karakterleri joker olarak yorumlanmaz.
- **Sorgu performansı:** Salt okunur sorgularda `AsNoTracking`, listelerde yalnızca gereken alanları çeken `Select` projeksiyonu, toplu güncellemelerde `ExecuteUpdateAsync`.

## 3. Güvenlik önlemleri ve nedenleri

| Önlem | Neden |
|---|---|
| BCrypt ile şifre hash'i | Veritabanı sızarsa şifreler düz okunamaz; hash bilerek yavaştır. |
| Kullanıcı yok / şifre yanlış için aynı hata mesajı | Kullanıcı adı numaralandırmasını (hangi hesap var?) engeller. |
| Anti-forgery token | Başka bir siteden kullanıcı adına form gönderilmesini (CSRF) engeller. |
| `Url.IsLocalUrl` ile returnUrl kontrolü | Giriş sonrası dış siteye yönlendirmeyi (open redirect) engeller. |
| ViewModel + sunucu doğrulaması | Over-posting engellenir; `Role` alanı hiçbir formdan okunmaz. |
| Razor otomatik encode, chat'te `textContent` | XSS: `<b>test</b>` yazan biri etiket değil düz metin görür (test edildi). |
| Kimlik her zaman cookie claim'lerinden | İstemciden gelen kullanıcı id'sine güvenilmez. |
| Cookie: HttpOnly, Secure, SameSite=Lax, kayan süre | Script erişimini, düz HTTP'de sızmayı ve çapraz site isteklerini azaltır. |
| Ad/kullanıcı adı karakter beyaz listesi | Sadece yaygın karakterlere izin; hem sunucuda hem istemcide aynı regex. |
| Sırlar kodda değil | Yerelde user-secrets, canlıda ortam değişkeni. Repo geçmişi, sır sızıntısı için taranarak yayınlandı. |
| Maps anahtarı kısıtlı | Yalnızca ilgili siteler (referrer) ve yalnızca kullanılan API'ler; bütçe uyarısı açık. |
| Data Protection anahtarları DB'de, sertifikayla şifreli | Konteyner diski geçici; yeniden başlatmada oturumlar düşmesin. DB'ye erişen biri cookie üretemesin diye anahtar XML'i sertifikayla şifrelenir, sertifika yalnızca ortam değişkeninde. |
| Hata sayfaları | Canlıda ayrıntılı hata gösterilmez; 404/403/500 için siteye uygun sayfalar. |

## 4. Dağıtım

- **Docker çok aşamalı build:** SDK imajıyla derlenir, çalışma zamanında yalnızca ASP.NET çalışma imajı kullanılır; root olmayan kullanıcıyla çalışır.
- **Render:** Web Service (Docker). Sağlık kontrolü `/healthz` (veritabanına dokunmaz, böylece Neon boş yere uyandırılmaz).
- **Ters vekil arkasında çalışma:** Render TLS'i kenarda sonlandırır. `ForwardedHeaders` ile uygulama doğru şemayı (https) ve istemci adresini görür; aksi halde `Secure` cookie ve yönlendirmeler bozulur.
- **Yapılandırma:** `ConnectionStrings__DefaultConnection`, `GoogleMaps__ApiKey`, `DataProtection__CertificateBase64`, `DataProtection__CertificatePassword` (çift alt çizgi, .NET'te `:` yerine).
- **Ücretsiz plan:** 15 dakika boşta kalınca uyur; bir dış izleme servisi `/healthz`'e istek atarak uyanık tutar. Neon bilerek uyanık tutulmaz (ücretsiz işlem saati sınırı), uyanması yaklaşık 1 sn sürer.

## 5. Yaşanan sorunlar ve çıkarılan dersler

- **Sonsuz yükleme (view özyinelemesi):** Bir ViewComponent'in view dosyasına yanlışlıkla tüm `_Layout` yapıştırılmıştı. Layout → bileşen → layout diye sonsuz döngü oluştu. Bulma yöntemi: bileşen çağrısını yorum satırı yapıp siteyi ikiye bölerek aramak (bisect). Ders: şüphelendiğin yeri erken izole et, dosya içeriğini komutla doğrula.
- **Chat baloncuğu bozulması:** Sunucuda üretilen geçmiş HTML'i ile JS'in ürettiği HTML farklıydı. Geçmiş de aynı JS fonksiyonuyla (JSON'dan) çizilerek tek kaynağa indirildi.
- **Sütun uzunluğu uyuşmazlığı:** Bkz. veritabanı kararları.
- **Kimlik ve CSRF gibi konularda "formdan gelene güvenme" ilkesi:** Rol, kullanıcı id'si ve yetki her zaman sunucuda belirlenir.

## 6. Bilinen sınırlar ve sonraki adımlar

- Profil fotoğrafı yükleme yok: Render diski geçici, kalıcı bir depolama (nesne depolama) gerekir.
- Şifre sıfırlama e-postası, etkinlik davetleri/katılım istekleri ve puan sistemi planlandı ama henüz uygulanmadı.
- Şema script'i veritabanında elle kuruldu; yeniden üretilebilir bir `schema.sql` eklenebilir.

## 7. Mülakat için kısa cevaplar

- **Neden SignalR?** Çift yönlü gerçek zamanlı iletişim; WebSocket'i, mümkün değilse yedek yöntemleri otomatik yönetir; ASP.NET Core kimlik doğrulamasıyla aynı cookie'yi kullanır.
- **Okunmamış mesajı nasıl saydın?** Mesaj başına değil, kullanıcı-etkinlik başına "son okunma zamanı" tuttum; sayı sorgu anında hesaplanıyor. Depolama küçük, güncelleme tek satır.
- **Oturumlar yeniden başlatmada neden düşmüyor?** Şifreleme anahtarlarını DB'de, sertifikayla şifreli tutuyorum; yeni konteyner aynı anahtarları okur.
- **Bir güvenlik açığını nasıl önledin?** (Örnek: XSS) Çıktı encode'u, chat'te `textContent`, ve test: `<b>test</b>` düz metin görünüyor.
- **Ölçeklenme?** Tek örnek için tasarlandı. Birden fazla örnekte SignalR için bir backplane (Redis ya da Azure SignalR) gerekir; bunu bilinen sınır olarak belirtiyorum.