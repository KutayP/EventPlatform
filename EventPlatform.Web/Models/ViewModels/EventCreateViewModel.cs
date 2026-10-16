using System.ComponentModel.DataAnnotations;

namespace EventPlatform.Web.Models.ViewModels;

public class EventCreateViewModel
{
    [Required(ErrorMessage = "Etkinlik adı gerekli.")]
    [StringLength(100)]
    public string Eventname { get; set; } = "";

    [Required(ErrorMessage = "Açıklama gerekli.")]
    [StringLength(2000)]
    public string Description { get; set; } = "";

    // Kullanıcının yerel saati (datetime-local). Kaydederken UTC'ye çevrilir.
    [Required(ErrorMessage = "Tarih ve saat gerekli.")]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Kategori seçin.")]
    public string Category { get; set; } = "";

    [Range(1, 12, ErrorMessage = "Süre 1 ile 12 saat arasında olmalı.")]
    public int Duration { get; set; } = 1;

    [Required(ErrorMessage = "Haritadan bir konum seçin.")]
    [StringLength(255)]
    public string? Location { get; set; }

    // String tutuyoruz: "39.920770" gibi değerler tr-TR kültüründe yanlış okunabilir
    // (nokta binlik ayracı sayılır). Controller'da InvariantCulture ile ayrıştırıyoruz.
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }

    // Tarayıcının UTC'ye uzaklığı (dakika). İstanbul için -180.
    [Range(-840, 840)]
    public int TimezoneOffsetMinutes { get; set; }
}