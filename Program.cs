using Dapper;
using Npgsql;
using realCabinly.Models; // User modelini ekleyin

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        // Cookie settings
        options.Cookie.Name = "MyCookieAuth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
    });


var app = builder.Build();

// Seed the admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var configuration = services.GetRequiredService<IConfiguration>();
    await using var connection = new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
    
    var adminUser = await connection.QuerySingleOrDefaultAsync<User>("SELECT * FROM users WHERE email = @Email", new { Email = "admin@admin.com" });

    if (adminUser == null)
    {
        await connection.ExecuteAsync(
            "INSERT INTO users (name, email, password_hash, role) VALUES (@Name, @Email, @PasswordHash, @Role)",
            new { Name = "Admin", Email = "admin@admin.com", PasswordHash = "admin123", Role = "admin" });
        
        Console.WriteLine("Admin user created successfully with a plain-text password.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{action=Index}/{id?}",
    defaults: new { controller = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();