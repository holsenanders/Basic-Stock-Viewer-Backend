using Basic_Stock_Viewer_Backend.Services;
using dotenv.net;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsPolicyBuilder =>
    {
        corsPolicyBuilder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
    options.AddPolicy("AllowSpecificOrigins", corsPolicyBuilder =>
    {
        corsPolicyBuilder.WithOrigins("https://basicstockviewerfrontend-fdfzfve0fdhseneu.northeurope-01.azurewebsites.net/")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<StockService>();

var app = builder.Build();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseCors("AllowSpecificOrigins");
    app.UseExceptionHandler("/error");
    app.UseStatusCodePages();
}

app.UseAuthorization();

app.MapControllers();

app.Run();