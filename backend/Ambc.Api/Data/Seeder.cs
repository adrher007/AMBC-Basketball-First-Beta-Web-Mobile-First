using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;

namespace Ambc.Api.Data;

public static class Seeder
{
    public static async Task SeedAsync(AmbcDbContext db)
    {
        if (!db.Users.Any())
        {
            // ADMIN semilla
            db.Users.Add(new User
            {
                Nombre = "Administrador AMBC",
                Username = "admin",
                PasswordHash = PasswordHasher.Hash("admin123"),
                Rol = Rol.ADMIN_AMBC,
                Activo = true,
            });
            await db.SaveChangesAsync();
        }
    }
}
