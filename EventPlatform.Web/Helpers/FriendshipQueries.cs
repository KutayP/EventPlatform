using EventPlatform.Web.Models.Entities;
using EventPlatform.Web.Models.ViewModels;

namespace EventPlatform.Web.Helpers;

public static class FriendshipQueries
{
    // İki kullanıcı arasındaki kayıt (istek hangi yönde gönderilmiş olursa olsun).
    public static IQueryable<Friendship> Between(this IQueryable<Friendship> source, int userA, int userB) =>
        source.Where(f => (f.Requesterid == userA && f.Addresseeid == userB) ||
                          (f.Requesterid == userB && f.Addresseeid == userA));

    // Bir kullanıcının kabul edilmiş arkadaşlarının kullanıcı numaraları.
    public static IQueryable<int> FriendIdsOf(this IQueryable<Friendship> source, int userId) =>
        source
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.Requesterid == userId || f.Addresseeid == userId))
            .Select(f => f.Requesterid == userId ? f.Addresseeid : f.Requesterid);
}
