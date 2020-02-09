using System;
using System.Security.Claims;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace IsThisAMood
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200") ){
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7
            })
            .CreateLogger();

            try
            {
                Log.Information("Starting web host");
                var host = CreateHostBuilder(args).Build();

                using (var scope = host.Services.CreateScope())
                {
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                    var participantsService = scope.ServiceProvider.GetRequiredService<IParticipantsService>();

                    var participants = participantsService.GetParticipants();

                    foreach (var participant in participants)
                    {
                        userManager.CreateAsync(new IdentityUser(participant.Username), participant.Password).GetAwaiter();
                    }

                }

                host.Run();
                
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(config =>
                    {
                        config.ListenLocalhost(6000);
                    }).UseStartup<Startup>();
                })
                .UseSerilog();
    }
}