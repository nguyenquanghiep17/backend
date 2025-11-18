using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailClient.API.Models;

[Table("management_config_mail")]
public class ManagementConfigMail
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("Email")]
    [MaxLength(255)]
    public string? Email { get; set; }

    [Column("Password")]
    [MaxLength(255)]
    public string? Password { get; set; }

    [Column("ImapServer")]
    [MaxLength(255)]
    public string? ImapServer { get; set; }

    [Column("ImapPort")]
    public int? ImapPort { get; set; }

    [Column("SmtpServer")]
    [MaxLength(255)]
    public string? SmtpServer { get; set; }

    [Column("SmtpPort")]
    public int? SmtpPort { get; set; }

    [Column("UseSsl")]
    public bool? UseSsl { get; set; }
}

