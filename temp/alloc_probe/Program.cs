using System.Collections.Concurrent;
using System.Data.Common;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

var connectionString = "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST_R3_ROLLS;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

static async Task<List<string>> RunParallelAllocationsAsync(string connectionString, Func<SqlConnection, CancellationToken, Task<string>> factory, int count)
{
    var results = new ConcurrentBag<string>();
    var tasks = Enumerable.Range(0, count).Select(async _ =>
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var result = await factory(connection, CancellationToken.None);
        results.Add(result);
    });

    await Task.WhenAll(tasks);
    return results.OrderBy(x => x).ToList();
}

var catalogResults = await RunParallelAllocationsAsync(connectionString, async (connection, token) => await SystemCodeGenerator.GetNextAllocatedCatalogNumberAsync(connection, null, token), 20);
var fabricResults = await RunParallelAllocationsAsync(connectionString, async (connection, token) => await SystemCodeGenerator.GetNextAllocatedFabricCodeAsync(connection, null, token), 20);

await using (var connection = new SqlConnection(connectionString))
{
    await connection.OpenAsync();
    var nextCatalog = await SystemCodeGenerator.GetNextAllocatedCatalogNumberAsync(connection, null, CancellationToken.None);
    var nextFabric = await SystemCodeGenerator.GetNextAllocatedFabricCodeAsync(connection, null, CancellationToken.None);
    Console.WriteLine($"CATALOG_NEXT_AFTER_PROOF={nextCatalog}");
    Console.WriteLine($"FABRIC_NEXT_AFTER_PROOF={nextFabric}");
}

Console.WriteLine($"CATALOG_REQUESTS=20");
Console.WriteLine($"CATALOG_RESULTS={catalogResults.Count}");
Console.WriteLine($"CATALOG_DISTINCT_RESULTS={catalogResults.Distinct().Count()}");
Console.WriteLine($"CATALOG_DUPLICATES={catalogResults.Count - catalogResults.Distinct().Count()}");
Console.WriteLine($"FABRIC_REQUESTS=20");
Console.WriteLine($"FABRIC_RESULTS={fabricResults.Count}");
Console.WriteLine($"FABRIC_DISTINCT_RESULTS={fabricResults.Distinct().Count()}");
Console.WriteLine($"FABRIC_DUPLICATES={fabricResults.Count - fabricResults.Distinct().Count()}");
Console.WriteLine($"CATALOG_VALUES={string.Join(",", catalogResults)}");
Console.WriteLine($"FABRIC_VALUES={string.Join(",", fabricResults)}");
