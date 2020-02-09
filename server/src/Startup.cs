using System;
using System.Collections.Generic;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using IsThisAMood.Middlewares;
using IsThisAMood.Models;
using IsThisAMood.Models.Database;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsThisAMood
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<IdentityDbContext>(builder => { builder.UseInMemoryDatabase("IsThisAMood"); } );
            services.AddIdentity<IdentityUser, IdentityRole>(options =>
                    {
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireDigit = false;
                        options.Password.RequireUppercase = false;
                    })
                .AddEntityFrameworkStores<IdentityDbContext>();
            
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/auth";
                options.Cookie.Name = "Cookie";
            });

            services.AddIdentityServer()
                .AddAspNetIdentity<IdentityUser>()
                .AddDeveloperSigningCredential()
                .AddInMemoryIdentityResources(GetIdentityResources())
                .AddInMemoryClients(GetClients());

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Cookie";
                    options.DefaultChallengeScheme = "oidc";
                    
                })
                .AddCookie("Cookie")
                .AddOpenIdConnect("oidc", options =>
                {
                    options.ClientId = Environment.GetEnvironmentVariable("ALEXA_CLIENT_ID");
                    options.ClientSecret = Environment.GetEnvironmentVariable("ALEXA_SECRET_ID");
                    options.SaveTokens = true;
                    options.Authority = "http://localhost:6000/";
                    options.ResponseType = "code";
                    options.RequireHttpsMetadata = false;
                });


            services.Configure<DatabaseSettings>(
                Configuration.GetSection(nameof(DatabaseSettings)));

            services.AddSingleton<IDatabaseSettings>(sp =>
                sp.GetRequiredService<IOptions<DatabaseSettings>>().Value);

            services.AddSingleton<IParticipantsService, ParticipantsService>();
            services.AddSingleton<CreateEntryStore>();
            services.AddHttpClient();

            services.AddMvc();

            services.AddControllers().AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseRouting();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseIdentityServer();

            if (!env.IsDevelopment())
                app.UseAlexaRequestValidation();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }


        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId()
            };
        }

        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                new Client
                {
                    ClientId = Environment.GetEnvironmentVariable("ALEXA_CLIENT_ID"),
                    RequireConsent = false,
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowedScopes = {"openid"},
                    RedirectUris =
                    {
                        Environment.GetEnvironmentVariable("ALEXA_REDIRECT_URI_ONE"),
                        Environment.GetEnvironmentVariable("ALEXA_REDIRECT_URI_TWO"),
                        Environment.GetEnvironmentVariable("ALEXA_REDIRECT_URI_THREE"),
                    },
                    ClientSecrets =
                    {
                        new Secret(Environment.GetEnvironmentVariable("ALEXA_CLIENT_SECRET").Sha256())
                    },
                }
            };
        }
    }
}