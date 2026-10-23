using Microsoft.EntityFrameworkCore;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Tests.Helpers;

public static class TestDbContextFactory
{
    public static QueVendecionDbContext Create()
    {
        var options = new DbContextOptionsBuilder<QueVendecionDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new QueVendecionDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
