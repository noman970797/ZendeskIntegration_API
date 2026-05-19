using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;
using ZendeskIntegration.Infrastructure.Data.Repositories;
using ZendeskIntegration.Infrastructure.Services;

namespace ZendeskIntegration.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZendeskInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ZendeskOptions>(configuration.GetSection(ZendeskOptions.SectionName));

        services.AddDbContext<ZendeskDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
                    sql.CommandTimeout(30);
                    sql.MigrationsAssembly("ZendeskIntegration.Infrastructure");
                }));

        // Repositories
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IJwtTokenLogRepository, JwtTokenLogRepository>();
        services.AddScoped<IZendeskApiLogRepository, ZendeskApiLogRepository>();
        services.AddScoped<IAttachmentLogRepository, AttachmentLogRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

        // Services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IWebhookService, WebhookService>();

        services.AddHttpClient<IZendeskTicketService, ZendeskTicketService>();
        services.AddHttpClient<IAttachmentService, AttachmentService>();

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZendeskDbContext>();
        await db.Database.MigrateAsync();
    }
}
