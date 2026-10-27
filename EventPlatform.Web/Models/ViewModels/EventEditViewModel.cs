using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EventPlatform.Web.Models.ViewModels;

// Düzenleme formu: oluşturma formu + hangi etkinlik olduğu.
// BindNever: bu alanlar formdan gelemez, sunucu belirler.
public class EventEditViewModel : EventCreateViewModel
{
    [BindNever]
    public int Eventid { get; set; }

    // Kayıtlı (UTC) tarih; tarayıcı bunu yerel saate çevirip forma yazar.
    [BindNever]
    public string? OriginalDateUtc { get; set; }
}