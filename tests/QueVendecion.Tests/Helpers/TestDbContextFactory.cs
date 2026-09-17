using Microsoft.EntityFrameworkCore;
using QueVendecion.Infrastructure.Data;

namespace QueVendecion.Tests.Helpers;

public static class TestDbContextFactory
{
    public static QueVendecionDbContext Create()
    {
        var options = new DbContextOptionsBuilder<QueVendecionDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new QueVendecionDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
