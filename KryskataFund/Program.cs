using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KryskataFund.Data;
using KryskataFund.Services;
using KryskataFund.Services.Interfaces;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Add database context
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string? connectionString;
if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgresql://"))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connectionString = databaseUrl ?? builder.Configuration.GetConnectionString("DefaultConnection");
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add email service
builder.Services.AddSingleton<IEmailService, EmailService>();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

// Configure Stripe
StripeConfiguration.ApiKey = app.Configuration["Stripe:SecretKey"];

// Ensure database is created and seed initial data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    // Create Messages table if it doesn't exist (EnsureCreated skips if DB already has tables)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Messages"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""SenderId"" INTEGER NOT NULL,
            ""ReceiverId"" INTEGER NOT NULL,
            ""SenderName"" TEXT NOT NULL DEFAULT '',
            ""ReceiverName"" TEXT NOT NULL DEFAULT '',
            ""Content"" TEXT NOT NULL DEFAULT '',
            ""SentAt"" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
            ""IsRead"" BOOLEAN NOT NULL DEFAULT FALSE
        )");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""FundComments"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""FundId"" INTEGER NOT NULL,
            ""UserId"" INTEGER NOT NULL,
            ""UserName"" TEXT NOT NULL DEFAULT '',
            ""Content"" TEXT NOT NULL DEFAULT '',
            ""CreatedAt"" TIMESTAMP WITHOUT TIME ZONE NOT NULL
        )");

    db.Database.ExecuteSqlRaw(@"
        ALTER TABLE ""Messages"" ADD COLUMN IF NOT EXISTS ""SharedFundId"" INTEGER NULL");

    DbSeeder.Seed(db);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Error/InternalError");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Allow iframe embedding for the embed widget
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Funds/Embed"))
    {
        context.Response.Headers.Remove("X-Frame-Options");
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
