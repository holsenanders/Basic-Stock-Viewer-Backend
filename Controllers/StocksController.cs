using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RestSharp;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly StockService _stockService;

    public StocksController(StockService stockService)
    {
        _stockService = stockService;
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query parameter is required.");

        var results = _stockService.Search(query);

        // Print the search results to the console
        Console.WriteLine("Search Results:");
        foreach (var result in results)
        {
            Console.WriteLine(JsonSerializer.Serialize(result));
        }

        return Ok(results);
    }

    [HttpGet("get_data")]
    public async Task<IActionResult> GetData([FromQuery] string stock, [FromQuery] string start, [FromQuery] string end)
    {
        if (string.IsNullOrWhiteSpace(stock) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return BadRequest("Stock, start date, and end date are required.");

        try
        {
            var apiKey = Environment.GetEnvironmentVariable("ALPHA_VANTAGE_KEY");
            if (string.IsNullOrEmpty(apiKey))
                return StatusCode(500, "Alpha Vantage API key is not configured.");
            
            var startDate = DateTime.Parse(start);
            var endDate = DateTime.Parse(end);
            
            var dateRange = (endDate - startDate).TotalDays;
            string function;

            if (dateRange <= 31)
            {
                function = "TIME_SERIES_DAILY"; 
            }
            else if (dateRange > 31 && dateRange < 365)
            {
                function = "TIME_SERIES_WEEKLY"; 
            }
            else
            {
                function = "TIME_SERIES_MONTHLY"; 
            }

            var client = new RestClient("https://www.alphavantage.com");
            var request = new RestRequest("/query");
            request.AddQueryParameter("function", function);
            request.AddQueryParameter("symbol", stock);
            request.AddQueryParameter("apikey", apiKey);

            var response = await client.ExecuteAsync(request);
            
            Console.WriteLine("Alpha Vantage Response:");
            Console.WriteLine(response.Content);

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                return StatusCode((int)response.StatusCode, "Failed to fetch data from Alpha Vantage.");

            var jsonResponse = JsonDocument.Parse(response.Content);

            string timeSeriesKey = function switch
            {
                "TIME_SERIES_DAILY" => "Time Series (Daily)",
                "TIME_SERIES_WEEKLY" => "Weekly Time Series",
                "TIME_SERIES_MONTHLY" => "Monthly Time Series",
                _ => throw new InvalidOperationException("Invalid function type")
            };

            if (!jsonResponse.RootElement.TryGetProperty(timeSeriesKey, out var timeSeries))
            {
                Console.WriteLine("Full JSON Response:");
                Console.WriteLine(response.Content);
                return StatusCode(500, "Invalid response from Alpha Vantage.");
            }

            // Filter the data based on the start and end dates
            var filteredData = timeSeries.EnumerateObject()
                .Where(e => DateTime.Parse(e.Name) >= startDate && DateTime.Parse(e.Name) <= endDate)
                .ToDictionary(e => e.Name, e => e.Value);

            return Ok(filteredData);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}
