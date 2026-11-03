using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EventPlatform.Web.Models.ViewModels;

// Ayarlar > Profil bilgileri: düzenlenebilir alanlar + yalnızca gösterilen kullanıcı adı ve e-posta.
public class SettingsProfileViewModel : ProfileInfoInput
{
    [BindNever, ValidateNever]
    public string Username { get; set; } = "";

    [BindNever, ValidateNever]
    public string Email { get; set; } = "";
}

// Ayarlar > İlgi alanları
public class InterestsSettingsViewModel
{
    [BindNever, ValidateNever]
    public List<InterestOption> Interests { get; set; } = new();
}

// Ayarlar > Gizlilik
public class PrivacySettingsViewModel
{
    public string Username { get; set; } = "";

    // "public" ya da "friends" (bkz. ProfileVisibilities)
    public string ProfileVisibility { get; set; } = ProfileVisibilities.Public;
}

// Ayarlar > Görünüm: "system", "light" ya da "dark"
public class ThemeSettingsViewModel
{
    public string Theme { get; set; } = "system";

    // Koyu tema paleti: "default", "slate", "soft", "graphite"
    public string Palette { get; set; } = "default";

    // Açık tema paleti: "default", "cream", "mint", "sky"
    public string LightPalette { get; set; } = "default";
}
