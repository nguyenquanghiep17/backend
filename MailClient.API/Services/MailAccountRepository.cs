using MailClient.API.Data;
using MailClient.API.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql;

namespace MailClient.API.Services;

public class MailAccountRepository : IMailAccountRepository
{
    private readonly DbContextOptions<MailDbContext> _options1;
    private readonly DbContextOptions<MailDbContext> _options2;

    public MailAccountRepository()
    {
        // Create separate options for each database
        // Using MySQL 8.0.21 server version (adjust if needed)
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 21));

        _options1 = new DbContextOptionsBuilder<MailDbContext>()
            .UseMySql(
                "Server=103.127.207.153;Port=3306;Database=mail_management_1;User=root;Password=12345678;",
                serverVersion)
            .Options;

        _options2 = new DbContextOptionsBuilder<MailDbContext>()
            .UseMySql(
                "Server=103.127.207.153;Port=3306;Database=mail_management_2;User=root;Password=12345678;",
                serverVersion)
            .Options;
    }

    public async Task<List<ManagementConfigMail>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext1 = new MailDbContext(_options1);
        await using var dbContext2 = new MailDbContext(_options2);

        var accounts1 = await dbContext1.ManagementConfigMails
            .Where(a => a.Email != null && !string.IsNullOrWhiteSpace(a.Email))
            .ToListAsync(cancellationToken);

        var accounts2 = await dbContext2.ManagementConfigMails
            .Where(a => a.Email != null && !string.IsNullOrWhiteSpace(a.Email))
            .ToListAsync(cancellationToken);

        return accounts1.Concat(accounts2).ToList();
    }
}

