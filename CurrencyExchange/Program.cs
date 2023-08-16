using CurrencyExchange.DbContexts;
using CurrencyExchange.Interfaces;
using CurrencyExchange.Models;
using CurrencyExchange.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    options.Configuration = "ryanvella.redis.cache.windows.net:6380,password=AXELJx2JVLhwcxyhKYjwF3C07ERiaRTvMAzCaCU2UdE=,ssl=True,abortConnect=False"; 
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
