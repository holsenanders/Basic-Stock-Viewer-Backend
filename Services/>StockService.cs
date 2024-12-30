using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

public class StockService
{
    private readonly List<Stock> _stocks;

    public StockService()
    {
        using var reader = new StreamReader("data/stock_info.csv");
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        _stocks = csv.GetRecords<Stock>().ToList();
    }

    public IEnumerable<Stock> Search(string query, int limit = 5)
    {
        return _stocks
            .Where(stock =>
                stock.Ticker.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                stock.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(stock => stock.Name)
            .Take(limit);
    }
}