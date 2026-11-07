using System.ComponentModel.DataAnnotations;

namespace EventPlatform.Web.Models.Api;

// API'nin dışarıya verdiği veri biçimleri. Veritabanı sınıfları (entity) hiçbir zaman doğrudan döndürülmez:
// şifre, e-posta gibi alanlar bu yüzden yanlışlıkla dışarı sızamaz.

// Kayıt (record) tiplerinde doğrulama öznitelikleri parametreye yazılır; ASP.NET Core özelliğe yazılanları reddeder.
public sealed record TokenRequest(
    [Required, StringLength(50)] string Username,
    [Required, StringLength(100)] string Password);

public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

public sealed record EventSummaryDto(
    string PublicId,
    string Name,
    string Category,
    DateTime StartsAtUtc,
    int DurationHours,
    string Location,
    int ParticipantCount,
    string? PageUrl);

public sealed record EventCreatorDto(string Username, string DisplayName);

public sealed record EventDetailDto(
    string PublicId,
    string Name,
    string Description,
    string Category,
    DateTime StartsAtUtc,
    int DurationHours,
    string Location,
    double Latitude,
    double Longitude,
    int ParticipantCount,
    EventCreatorDto Creator,
    string? PageUrl);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record MyEventsDto(IReadOnlyList<EventSummaryDto> Created, IReadOnlyList<EventSummaryDto> Joined);
