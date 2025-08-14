using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Sql");

        cs ??= "Server=localhost,1433;Database=ECommerceDb;User Id=sa;Password=ECommercePass123!;TrustServerCertificate=True;";

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs, o => o.EnableRetryOnFailure())
            .Options;

        return new AppDbContext(opts);
    }
}
