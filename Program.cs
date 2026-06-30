using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Services;


var builder = WebApplication.CreateBuilder(args);

// 1. إعدادات الـ MVC والـ DB
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// 2. إعدادات الـ Session والـ Identity
builder.Services.AddSession(c => c.IdleTimeout = TimeSpan.FromMinutes(10));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ " +
        "أبتثجحخدذرزسشصضطظعغفقكلمنهويءآأؤإئ";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. تسجيل الخدمات (Services)
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ICommitteesAssignmentsService, _CommitteesAssignmentsService>();




// 4. بناء التطبيق
var app = builder.Build();

// 5. إعدادات الـ Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    string adminEmail = "admin@example.com";
    string adminPassword = "Admin@123";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        var newAdmin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(newAdmin, adminPassword);
        if (result.Succeeded) await userManager.AddToRoleAsync(newAdmin, "Admin");
    }
    
}

// 7. تشغيل التطبيق
app.Run();