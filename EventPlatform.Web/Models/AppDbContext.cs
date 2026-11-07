using System;
using System.Collections.Generic;
using EventPlatform.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Eventparticipationrequest> Eventparticipationrequests { get; set; }

    public virtual DbSet<Eventreadstatus> Eventreadstatuses { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<Interest> Interests { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Point> Points { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Userinterest> Userinterests { get; set; }

    public virtual DbSet<Usersetting> Usersettings { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Eventid).HasName("events_pkey");

            entity.ToTable("events");

            entity.HasIndex(e => e.Publicid, "uq_events_publicid").IsUnique();

            entity.Property(e => e.Eventid).HasColumnName("eventid");
            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .HasColumnName("category");
            entity.Property(e => e.Createdbyuserid).HasColumnName("createdbyuserid");
            entity.Property(e => e.Date)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.Duration).HasColumnName("duration");
            entity.Property(e => e.Eventname)
                .HasMaxLength(100)
                .HasColumnName("eventname");
            entity.Property(e => e.Isapproved).HasColumnName("isapproved");
            entity.Property(e => e.Latitude).HasColumnName("latitude");
            entity.Property(e => e.Location)
                .HasMaxLength(100)
                .HasColumnName("location");
            entity.Property(e => e.Longitude).HasColumnName("longitude");
            entity.Property(e => e.Publicid)
                .HasMaxLength(8)
                .HasColumnName("publicid");
            entity.Property(e => e.Issample)
                .HasDefaultValue(false)
                .HasColumnName("issample");

            entity.HasOne(d => d.Createdbyuser).WithMany(p => p.Events)
                .HasForeignKey(d => d.Createdbyuserid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("events_createdbyuserid_fkey");
        });

        modelBuilder.Entity<Eventparticipationrequest>(entity =>
        {
            entity.HasKey(e => e.Requestid).HasName("eventparticipationrequests_pkey");

            entity.ToTable("eventparticipationrequests");

            entity.Property(e => e.Requestid).HasColumnName("requestid");
            entity.Property(e => e.Forevent).HasColumnName("forevent");
            entity.Property(e => e.Foruser).HasColumnName("foruser");
            entity.Property(e => e.Invitedby).HasColumnName("invitedby");
            entity.Property(e => e.Status)
                .HasMaxLength(100)
                .HasColumnName("status");
            entity.Property(e => e.Time)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("time");
            entity.Property(e => e.Type)
                .HasMaxLength(100)
                .HasColumnName("type");

            entity.HasOne(d => d.ForeventNavigation).WithMany(p => p.Eventparticipationrequests)
                .HasForeignKey(d => d.Forevent)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("eventparticipationrequests_forevent_fkey");

            entity.HasOne(d => d.ForuserNavigation).WithMany(p => p.EventparticipationrequestForuserNavigations)
                .HasForeignKey(d => d.Foruser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("eventparticipationrequests_foruser_fkey");

            entity.HasOne(d => d.InvitedbyNavigation).WithMany(p => p.EventparticipationrequestInvitedbyNavigations)
                .HasForeignKey(d => d.Invitedby)
                .HasConstraintName("eventparticipationrequests_invitedby_fkey");
        });

        modelBuilder.Entity<Eventreadstatus>(entity =>
        {
            entity.HasKey(e => new { e.Userid, e.Eventid }).HasName("eventreadstatus_pkey");

            entity.ToTable("eventreadstatus");

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Eventid).HasColumnName("eventid");
            entity.Property(e => e.Lastreadat)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("lastreadat");

            entity.HasOne(d => d.Event).WithMany(p => p.Eventreadstatuses)
                .HasForeignKey(d => d.Eventid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("eventreadstatus_eventid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Eventreadstatuses)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("eventreadstatus_userid_fkey");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => e.Friendshipid).HasName("friendships_pkey");

            entity.ToTable("friendships");

            entity.HasIndex(e => new { e.Addresseeid, e.Status }, "idx_friendships_addressee_status");

            entity.Property(e => e.Friendshipid).HasColumnName("friendshipid");
            entity.Property(e => e.Addresseeid).HasColumnName("addresseeid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Requesterid).HasColumnName("requesterid");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("pending")
                .HasColumnName("status");

            entity.HasOne(d => d.Addressee).WithMany()
                .HasForeignKey(d => d.Addresseeid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("friendships_addresseeid_fkey");

            entity.HasOne(d => d.Requester).WithMany()
                .HasForeignKey(d => d.Requesterid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("friendships_requesterid_fkey");
        });

        modelBuilder.Entity<Interest>(entity =>
        {
            entity.HasKey(e => e.Interestid).HasName("interests_pkey");

            entity.ToTable("interests");

            entity.HasIndex(e => e.Interestname, "interests_interestname_key").IsUnique();

            entity.Property(e => e.Interestid).HasColumnName("interestid");
            entity.Property(e => e.Interestname)
                .HasMaxLength(50)
                .HasColumnName("interestname");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Messageid).HasName("messages_pkey");

            entity.ToTable("messages");

            entity.HasIndex(e => new { e.Eventid, e.Senttime }, "idx_messages_eventid_senttime");

            entity.Property(e => e.Messageid).HasColumnName("messageid");
            entity.Property(e => e.Eventid).HasColumnName("eventid");
            entity.Property(e => e.Messagetext)
                .HasMaxLength(1000)
                .HasColumnName("messagetext");
            entity.Property(e => e.Senderid).HasColumnName("senderid");
            entity.Property(e => e.Senttime)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("senttime");

            entity.HasOne(d => d.Event).WithMany(p => p.Messages)
                .HasForeignKey(d => d.Eventid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messages_eventid_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.Senderid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("messages_senderid_fkey");
        });

        modelBuilder.Entity<Point>(entity =>
        {
            entity.HasKey(e => e.Pointid).HasName("points_pkey");

            entity.ToTable("points");

            entity.Property(e => e.Pointid).HasColumnName("pointid");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Earnedat)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("earnedat");
            entity.Property(e => e.Eventid).HasColumnName("eventid");
            entity.Property(e => e.Reason)
                .HasMaxLength(50)
                .HasColumnName("reason");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.Event).WithMany(p => p.Points)
                .HasForeignKey(d => d.Eventid)
                .HasConstraintName("points_eventid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Points)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("points_userid_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Birthdate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("birthdate");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.Firstname)
                .HasMaxLength(20)
                .HasColumnName("firstname");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasColumnName("gender");
            entity.Property(e => e.Lastname)
                .HasMaxLength(20)
                .HasColumnName("lastname");
            entity.Property(e => e.Latitude).HasColumnName("latitude");
            entity.Property(e => e.Location)
                .HasMaxLength(100)
                .HasColumnName("location");
            entity.Property(e => e.Longitude).HasColumnName("longitude");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Phonenumber)
                .HasMaxLength(15)
                .HasColumnName("phonenumber");
            entity.Property(e => e.Profilepicture)
                .HasMaxLength(255)
                .HasColumnName("profilepicture");
            entity.Property(e => e.Profilevisibility)
                .HasMaxLength(10)
                .HasDefaultValue("public")
                .HasColumnName("profilevisibility");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasColumnName("role");
            entity.Property(e => e.Username)
                .HasMaxLength(20)
                .HasColumnName("username");

            entity.HasMany(d => d.EventsNavigation).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "Participant",
                    r => r.HasOne<Event>().WithMany()
                        .HasForeignKey("Eventid")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("participants_eventid_fkey"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("Userid")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("participants_userid_fkey"),
                    j =>
                    {
                        j.HasKey("Userid", "Eventid").HasName("participants_pkey");
                        j.ToTable("participants");
                        j.IndexerProperty<int>("Userid").HasColumnName("userid");
                        j.IndexerProperty<int>("Eventid").HasColumnName("eventid");
                    });
        });

        modelBuilder.Entity<Userinterest>(entity =>
        {
            entity.HasKey(e => new { e.Userid, e.Interestid }).HasName("userinterests_pkey");

            entity.ToTable("userinterests");

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Interestid).HasColumnName("interestid");
            entity.Property(e => e.Time)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("time");

            entity.HasOne(d => d.Interest).WithMany(p => p.Userinterests)
                .HasForeignKey(d => d.Interestid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("userinterests_interestid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Userinterests)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("userinterests_userid_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Notificationid).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.Userid, e.Createdat }, "idx_notifications_user_created").IsDescending(false, true);

            entity.Property(e => e.Notificationid).HasColumnName("notificationid");
            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Type)
                .HasMaxLength(30)
                .HasColumnName("type");
            entity.Property(e => e.Actorid).HasColumnName("actorid");
            entity.Property(e => e.Eventid).HasColumnName("eventid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("(now() AT TIME ZONE 'utc')")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Readat)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("readat");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_userid_fkey");

            entity.HasOne(d => d.Actor).WithMany()
                .HasForeignKey(d => d.Actorid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_actorid_fkey");

            entity.HasOne(d => d.Event).WithMany()
                .HasForeignKey(d => d.Eventid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_eventid_fkey");
        });

        modelBuilder.Entity<Usersetting>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("usersettings_pkey");

            entity.ToTable("usersettings");

            entity.Property(e => e.Userid)
                .ValueGeneratedNever()
                .HasColumnName("userid");
            entity.Property(e => e.NotifyEventJoined)
                .HasDefaultValue(true)
                .HasColumnName("notify_event_joined");
            entity.Property(e => e.NotifyFriends)
                .HasDefaultValue(true)
                .HasColumnName("notify_friends");
            entity.Property(e => e.NotifyRecommended)
                .HasDefaultValue(false)
                .HasColumnName("notify_recommended");
            entity.Property(e => e.RadiusKm)
                .HasDefaultValue(50)
                .HasColumnName("radius_km");
            entity.Property(e => e.Theme)
                .HasMaxLength(10)
                .HasDefaultValue("system")
                .HasColumnName("theme");

            entity.HasOne(d => d.User).WithOne()
                .HasForeignKey<Usersetting>(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("usersettings_userid_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
