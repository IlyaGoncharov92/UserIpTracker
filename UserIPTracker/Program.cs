using Microsoft.EntityFrameworkCore;
using UserIPTracker;
using UserIPTracker.Services;

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Configuration.GetConnectionString("DefaultConnection");

var services = builder.Services;

services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connString, o =>
    {
        o.EnableRetryOnFailure();
    })
);

services.AddScoped<ConnectionsRepository>();
services.AddSingleton<ConnectionsProcessor>();

services.AddControllers();

services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // db.Database.Migrate(); 
}

app.MapControllers();

app.Run();
