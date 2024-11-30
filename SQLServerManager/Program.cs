using DatabaseSynchronizer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using SQLServerManager.Services;
using SQLServerManager.Services.Implementations;
using SQLServerManager.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<ITableService, SqlServerTableService>();

// Thêm Radzen services
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

// Thêm các service khác của bạn
builder.Services.AddScoped<SqlServerService>();
builder.Services.AddScoped<IDatabaseService, SqlServerDatabaseService>();
builder.Services.AddLogging();
builder.Services.AddHttpClient();


builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<ModelGeneratorService>();
builder.Services.AddScoped<DbContextGenerationService>();
// Đọc configuration
builder.Configuration.AddJsonFile("appsettings.json");

// Đăng ký configuration
builder.Services.AddSingleton(builder.Configuration);
// Đăng ký configuration và dịch vụ
builder.Services.AddSingleton(builder.Configuration);
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<ModelGeneratorService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
