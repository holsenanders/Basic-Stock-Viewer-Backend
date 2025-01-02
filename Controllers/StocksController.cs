using System.Text.Json;
using Basic_Stock_Viewer_Backend.Models;
using Basic_Stock_Viewer_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using RestSharp;

namespace Basic_Stock_Viewer_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly StockService _stockService;
    private readonly IConfiguration _configuration;
    private readonly RestClient _client;
    private readonly string _apiKey;
    private readonly ILogger<StocksController> _logger;

    public StocksController(
        StockService stockService,
        IConfiguration configuration,
        ILogger<StocksController> logger)
    {
        _stockService = stockService;
        _configuration = configuration;
        _logger = logger;
        _apiKey = configuration["AlphaVantage:ApiKey"] 
            ?? throw new InvalidOperationException("Alpha Vantage API key not configured");
        _client = new RestClient("https://www.alphavantage.co/");
    }

    [HttpGet("get_data")]
    public async Task<IActionResult> GetData([FromQuery] string symbol, [FromQuery] string start, [FromQuery] string end)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
        {
            return BadRequest("Symbol, start date, and end date are required.");
        }

        try
        {
            if (!DateTime.TryParse(start, out var startDate) || !DateTime.TryParse(end, out var endDate))
            {
                return BadRequest("Invalid date format. Please use yyyy-MM-dd format.");
            }

            if (startDate > endDate)
            {
                return BadRequest("Start date must be before end date.");
            }
            
            var (function, outputsize) = GetTimeSeriesParameters(startDate, endDate);
            
            var request = new RestRequest("query")
                .AddQueryParameter("function", function)
                .AddQueryParameter("symbol", symbol)
                .AddQueryParameter("apikey", _apiKey);

            if (outputsize != null)
            {
                request.AddQueryParameter("outputsize", outputsize);
            }

            _logger.LogInformation(
                "Requesting Alpha Vantage data: Function={Function}, Symbol={Symbol}, DateRange={Start}-{End}", 
                function, symbol, start, end);

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                _logger.LogError(
                    "Alpha Vantage API request failed: {StatusCode} {Content}", 
                    response.StatusCode, response.Content);
                return StatusCode((int)response.StatusCode, "Failed to fetch data from Alpha Vantage.");
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                return StatusCode(500, "Empty response received from Alpha Vantage.");
            }

            return await ProcessAlphaVantageResponse(response.Content, startDate, endDate, function);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Alpha Vantage request");
            return StatusCode(500, "An internal error occurred while processing your request.");
        }
    }

    private (string function, string? outputsize) GetTimeSeriesParameters(DateTime startDate, DateTime endDate)
    {
        var dateRange = (endDate - startDate).TotalDays;

        return dateRange switch
        {
            <= 100 => ("TIME_SERIES_DAILY", "compact"),
            <= 365 => ("TIME_SERIES_DAILY", "full"),
            <= 1825 => ("TIME_SERIES_WEEKLY", null),
            _ => ("TIME_SERIES_MONTHLY", null)
        };
    }

    private async Task<IActionResult> ProcessAlphaVantageResponse(
        string content, 
        DateTime startDate, 
        DateTime endDate,
        string function)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(content);
            var root = jsonDocument.RootElement;

            // Check for API errors
            if (root.TryGetProperty("Error Message", out var errorMessage))
            {
                _logger.LogWarning("Alpha Vantage API error: {Error}", errorMessage.GetString());
                return BadRequest($"API Error: {errorMessage.GetString()}");
            }

            if (root.TryGetProperty("Note", out var note))
            {
                _logger.LogWarning("Alpha Vantage API rate limit exceeded: {Note}", note.GetString());
                return StatusCode(429, "API rate limit exceeded. Please try again later.");
            }
            
            var timeSeriesKey = function switch
            {
                "TIME_SERIES_DAILY" => "Time Series (Daily)",
                "TIME_SERIES_WEEKLY" => "Weekly Time Series",
                "TIME_SERIES_MONTHLY" => "Monthly Time Series",
                _ => throw new ArgumentException($"Invalid function type: {function}")
            };

            if (!root.TryGetProperty(timeSeriesKey, out var timeSeries))
            {
                _logger.LogError("Invalid Alpha Vantage response structure: {Content}", content);
                return StatusCode(500, "Received invalid data structure from Alpha Vantage.");
            }
            
            var stockData = new Dictionary<string, StockDataPoint>();

            foreach (var entry in timeSeries.EnumerateObject())
            {
                if (DateTime.TryParse(entry.Name, out var date) && 
                    date >= startDate && 
                    date <= endDate)
                {
                    stockData[entry.Name] = new StockDataPoint
                    {
                        Open = ParseDecimal(entry.Value, "1. open"),
                        High = ParseDecimal(entry.Value, "2. high"),
                        Low = ParseDecimal(entry.Value, "3. low"),
                        Close = ParseDecimal(entry.Value, "4. close"),
                        Volume = ParseDecimal(entry.Value, "5. volume")
                    };
                }
            }

            return Ok(new
            {
                Success = true,
                Data = stockData.OrderByDescending(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Value)
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Alpha Vantage response");
            return StatusCode(500, "Error processing the response from Alpha Vantage.");
        }
    }

    private static decimal ParseDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && 
               decimal.TryParse(property.GetString(), out var value) 
            ? value 
            : 0m;
    }
    
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query parameter is required.");

        var results = _stockService.Search(query);
        
        Console.WriteLine("Search Results:");
        foreach (var result in results)
        {
            Console.WriteLine(JsonSerializer.Serialize(result));
        }

        return Ok(results);
    }
}
