using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using Ambc.Api.Data;
using Ambc.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ===== DB (PostgreSQL) =====
builder.Services.AddDbContext<AmbcDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ===== JWT Auth =====
builder.Services.AddScoped<JwtService>();

// ===== Groq (Fase 14) =====
builder.Services.AddHttpClient<GroqService>();

// ===== Notificaciones de partidos del día (idempotente, sin push) =====
builder.Services.AddHostedService<NotificacionesBackgroundService>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// ===== CORS (frontend Vite) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
        policy.WithOrigins(
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://10.1.33.181:5173",
            "http://10.1.33.181:5174"
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
}

app.UseCors("dev");
app.UseStaticFiles(); // sirve wwwroot (si existe)

// Sirve /uploads/** desde la carpeta uploads del proyecto
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads",
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== Migrar + seed en arranque (desarrollo) =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AmbcDbContext>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(db);
}

app.Run();