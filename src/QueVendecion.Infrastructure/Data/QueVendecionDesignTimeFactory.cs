using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QueVendecion.Infrastructure.Data;

/// <summary>Production migrations always target PostgreSQL, without starting the API or its seed.</summary>
public class QueVendecionDesignTimeFactory : IDesignTimeDbContextFactory<QueVendecionDbContext>
{
    public QueVendecionDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=quevendecion_design;Username=design_only";
        return new QueVendecionDbContext(new DbContextOptionsBuilder<QueVendecionDbContext>()
            .UseNpgsql(connection).Options);
    }
}
