using Microsoft.EntityFrameworkCore;
using ZendeskIntegration.Core.Models;

namespace ZendeskIntegration.Infrastructure.Data;

public class ZendeskDbContext : DbContext
{
    public ZendeskDbContext(DbContextOptions<ZendeskDbContext> options) : base(options) { }

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<JwtTokenLog> JwtTokenLogs => Set<JwtTokenLog>();
    public DbSet<ZendeskApiLog> ZendeskApiLogs => Set<ZendeskApiLog>();
    public DbSet<AttachmentLog> AttachmentLogs => Set<AttachmentLog>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SupportTicket>(e =>
        {
            e.ToTable("SupportTickets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            e.Property(x => x.Description).IsRequired().HasColumnType("nvarchar(max)");
            e.Property(x => x.Tags).HasMaxLength(2000);
            e.Property(x => x.RequesterName).HasMaxLength(200);
            e.Property(x => x.RequesterEmail).HasMaxLength(320);
            e.Property(x => x.Priority).HasMaxLength(20).HasDefaultValue("normal");
            e.Property(x => x.Type).HasMaxLength(20).HasDefaultValue("problem");
            e.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("pending");
            e.Property(x => x.ZendeskTicketUrl).HasMaxLength(500);
            e.Property(x => x.ZendeskRawResponse).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.ZendeskTicketId);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.SyncedToZendesk);
        });

        modelBuilder.Entity<JwtTokenLog>(e =>
        {
            e.ToTable("JwtTokenLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.ExternalUserId).IsRequired().HasMaxLength(200);
            e.Property(x => x.UserName).IsRequired().HasMaxLength(200);
            e.Property(x => x.UserEmail).IsRequired().HasMaxLength(320);
            e.Property(x => x.Algorithm).HasMaxLength(10).HasDefaultValue("HS256");
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.ExternalUserId);
            e.HasIndex(x => x.TokenHash);
        });

        modelBuilder.Entity<ZendeskApiLog>(e =>
        {
            e.ToTable("ZendeskApiLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.Operation).IsRequired().HasMaxLength(100);
            e.Property(x => x.HttpMethod).IsRequired().HasMaxLength(10);
            e.Property(x => x.Endpoint).IsRequired().HasMaxLength(500);
            e.Property(x => x.RequestBody).HasColumnType("nvarchar(max)");
            e.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.Operation);
            e.HasIndex(x => x.Success);
        });

        modelBuilder.Entity<AttachmentLog>(e =>
        {
            e.ToTable("AttachmentLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.FileName).IsRequired().HasMaxLength(500);
            e.Property(x => x.ContentType).IsRequired().HasMaxLength(200);
            e.Property(x => x.UploadToken).IsRequired().HasMaxLength(500);
            e.Property(x => x.UploadedBy).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.UploadToken);
            e.HasIndex(x => x.RelatedTicketId);
        });

        modelBuilder.Entity<WebhookEvent>(e =>
        {
            e.ToTable("WebhookEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            e.Property(x => x.TicketStatus).HasMaxLength(50);
            e.Property(x => x.TicketPriority).HasMaxLength(20);
            e.Property(x => x.AssigneeEmail).HasMaxLength(320);
            e.Property(x => x.LatestCommentAuthor).HasMaxLength(200);
            e.Property(x => x.RawPayload).IsRequired().HasColumnType("nvarchar(max)");
            e.Property(x => x.ProcessingError).HasMaxLength(2000);
            e.Property(x => x.SourceIpAddress).HasMaxLength(45);
            e.Property(x => x.ReceivedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.ZendeskTicketId);
            e.HasIndex(x => x.EventType);
            e.HasIndex(x => x.ProcessedSuccessfully);
            e.HasIndex(x => x.ReceivedAt);
        });
    }
}
