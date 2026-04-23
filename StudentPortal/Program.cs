using StudentPortal.Services;
using BCrypt.Net;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
 using StudentPortal.Models;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
};
var builder = WebApplication.CreateBuilder(options);
builder.Configuration["AllowedHosts"] = "*";

// Load local-only overrides (secrets) if present.
// These files are gitignored via appsettings.*.local.json.
builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);




// MongoDB conventions: ignore extra elements globally
var pack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
ConventionRegistry.Register("IgnoreExtraElements", pack, t => true);

// Explicit class map for User to ensure ignore extras
try
{
    BsonClassMap.RegisterClassMap<User>(cm =>
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true);
    });
}
catch
{
}

// ✅ Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<LibraryService>();
builder.Services.AddSingleton<JitsiJwtService>();

// ✅ Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// ✅ Add MVC
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();
var isRailway =
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT")) ||
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN"));

// ✅ Seed default admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedAdminAsync(services);
}

// ✅ Middleware configuration
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
if (!isRailway)
    app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // session middleware

app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();


// ✅ Local function to seed admin
async Task SeedAdminAsync(IServiceProvider services)
{
    try
    {
        var mongo = services.GetRequiredService<MongoDbService>();
        var existing = await mongo.GetUserByEmailAsync("admin@mysuqc.local");

        if (existing == null)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Admin@1234");
            await mongo.CreateUserAsync(
                email: "admin@mysuqc.local",
                hashedPassword: hash,
                otp: "",
                role: "Admin",
                markVerified: true
            );

            Console.WriteLine("✅ Default Admin created: admin@mysuqc.local / Admin@1234");
        }
        else
        {
            Console.WriteLine("ℹ️ Admin account already exists.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error seeding admin: {ex.Message}");
    }
}
