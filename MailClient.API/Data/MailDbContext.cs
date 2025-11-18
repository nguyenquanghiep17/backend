using Microsoft.EntityFrameworkCore;
using MailClient.API.Models;

namespace MailClient.API.Data;

public class MailDbContext : DbContext
{
    public MailDbContext(DbContextOptions<MailDbContext> options) : base(options)
    {
    }

    public DbSet<ManagementConfigMail> ManagementConfigMails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ManagementConfigMail>(entity =>
        {
            entity.ToTable("management_config_mail");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(255);
            entity.Property(e => e.Password).HasColumnName("Password").HasMaxLength(255);
            entity.Property(e => e.ImapServer).HasColumnName("ImapServer").HasMaxLength(255);
            entity.Property(e => e.ImapPort).HasColumnName("ImapPort");
            entity.Property(e => e.SmtpServer).HasColumnName("SmtpServer").HasMaxLength(255);
            entity.Property(e => e.SmtpPort).HasColumnName("SmtpPort");
            entity.Property(e => e.UseSsl).HasColumnName("UseSsl");
        });
    }
}

