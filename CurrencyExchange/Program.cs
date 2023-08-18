using CurrencyExchange.DbContexts;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Set up configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json")
            .Build();

        // Add services to the container.
        builder.Services.Configure<ExchangeRateApiConfiguration>(configuration.GetSection("ExchangeRatesApi"));
        builder.Services.AddScoped<ITradeService, TradeService>();
        builder.Services.AddHttpClient<IExchangeRateProviderService, ExchangeRateProviderService>((provider, client) =>
        {
            var configuration = provider.GetRequiredService<IOptions<ExchangeRateApiConfiguration>>().Value;
            client.BaseAddress = new Uri(configuration.BaseUrl + configuration.AccessKey);
        });

        builder.Services.AddDbContext<CurrencyExchangeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("CurrencyExchangeDbConnection")));

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("RedisConfiguration");
            options.InstanceName = "CurrencyExchangeCache";
        });

        builder.WebHost.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });


        builder.Services.AddControllers();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}