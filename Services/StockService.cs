using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Basic_Stock_Viewer_Backend.Services;

public class StockService
{
    private readonly List<Stock> _stocks;

    public StockService()
    {
        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "data/stock_info.csv");
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            _stocks = csv.GetRecords<Stock>().ToList();
            Console.WriteLine($"Loaded {_stocks.Count} stocks.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stocks: {ex.Message}");
            _stocks = new List<Stock>();
        }

    }

    public IEnumerable<Stock> Search(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        if (limit <= 0)
        {
            limit = 5;
        }

        var results = _stocks
            .Where(stock =>
                (stock.Ticker?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (stock.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(stock => stock.Name ?? string.Empty)
            .Take(limit)
            .ToList();

        Console.WriteLine($"Search Query: '{query}', Results Found: {results.Count}");
        return results;
    }

}