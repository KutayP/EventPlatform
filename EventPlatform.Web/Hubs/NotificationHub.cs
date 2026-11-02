using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EventPlatform.Web.Hubs;

// Sunucudan istemciye tek yönlü haber: "bildirimlerin değişti". İstemci bu hub'a metot çağırmaz.
// Bağlantı kimliği cookie'deki NameIdentifier claim'inden gelir; Clients.User(id) yalnızca o kişinin
// bağlantılarına ulaşır.
[Authorize]
public class NotificationHub : Hub
{
}
