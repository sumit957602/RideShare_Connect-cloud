using Microsoft.EntityFrameworkCore;
using RideShareConnect.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using RideShare_Connect.Services;
using Microsoft.AspNetCore.Identity;
using RideShare_Connect.Models.UserManagement;
using RideShare_Connect.Models.VehicleManagement;
using RideShare_Connect.Models.AdminManagement;
using RideShare_Connect.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllersWithViews();

// HttpClient removed as we are moving to monolithic architecture

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied"; 
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
    });

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});



builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    )
);

builder.Services.AddHostedService<VerificationReminderService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasher<Driver>, PasswordHasher<Driver>>();
builder.Services.AddScoped<IPasswordHasher<Admin>, PasswordHasher<Admin>>();

builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentGateway"));
builder.Services.AddScoped<IPaymentGateway, PaymentGateway>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IRatingService, RatingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
