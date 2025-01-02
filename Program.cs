using Basic_Stock_Viewer_Backend.Services;
using dotenv.net;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// 1. Define the "AllowAll" CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsPolicyBuilder =>
    {
        corsPolicyBuilder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// 2. Register controllers, etc.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Add any other services you need
builder.Services.AddSingleton<StockService>();

var app = builder.Build();

// 4. Use the "AllowAll" policy
app.UseCors("AllowAll");

// 5. Configure Swagger only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 6. The usual ASP.NET Core middleware pipeline
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.Run();