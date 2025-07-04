﻿using eTickets.Data;
using eTickets.Data.Cart;
using eTickets.Data.Services;
using eTickets.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace eTickets
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            // Configuration access
            var configuration = builder.Configuration;

            // DbContext configuration
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnectionString")));

            // Services configuration
            builder.Services.AddScoped<IActorsService, ActorsService>();
            builder.Services.AddScoped<IProducersService, ProducersService>();
            builder.Services.AddScoped<ICinemasService, CinemasService>();
            builder.Services.AddScoped<IMoviesService, MoviesService>();
            builder.Services.AddScoped<IOrdersService, OrdersService>();

            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.Services.AddScoped(sc => ShoppingCart.GetShoppingCart(sc));

            // Email sender configuration
            builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
            builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

            // Authentication and authorization
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            // google account auth
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
.AddCookie()
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    IConfigurationSection googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
    options.ClientId = googleAuthNSection["ClientId"];
    options.ClientSecret = googleAuthNSection["ClientSecret"];
});
         
            builder.Services.AddMemoryCache();
            builder.Services.AddSession();
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Seed database
            AppDbInitializer.Seed(app);
            await AppDbInitializer.SeedUsersAndRolesAsync(app);

            app.Run();
        }
    }
}


