using DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Migrations;

public class RestaurantDbContextFactory : IDesignTimeDbContextFactory<RestaurantDbContext>
{
    public RestaurantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RestaurantDbContext>();
        optionsBuilder.UseSqlite("Data Source=restaurant.db", b => b.MigrationsAssembly("Migrations"));
        return new RestaurantDbContext(optionsBuilder.Options);
    }
}
