using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

builder.AddServiceDefaults();

var app = builder.Build();

// app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/forecast", async (IHttpClientFactory httpClientFactory, IConfiguration config) =>
    {
        var http = httpClientFactory.CreateClient();
        var url = $"http://weatherapi/weatherforecast";

        var data = await http.GetFromJsonAsync<object>(url);
        return Results.Json(data);
    })
    .WithName("GetWeatherForecast");

app.Run();
