using System.Data;
using Microsoft.Data.SqlClient;

var cs = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
    ?? throw new InvalidOperationException("Lumar__ConnectionString is missing.");

var resource = $"MessageClaim:UniqueProbe:{Guid.NewGuid():N}";
Console.WriteLine($"RESOURCE={resource}");

var firstTask = Task.Run(async () =>
{
    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

    using var cmd = new SqlCommand(
        "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000; SELECT @result;",
        conn,
        tx);
    cmd.Parameters.AddWithValue("@resource", resource);
    var raw = await cmd.ExecuteScalarAsync();
    var result = raw is null ? -999 : Convert.ToInt32(raw);
    Console.WriteLine($"FIRST_LOCK_RESULT={result} @ {DateTime.UtcNow:O}");

    await Task.Delay(5000);
    await tx.CommitAsync();
    Console.WriteLine($"FIRST_COMMIT @ {DateTime.UtcNow:O}");
});

await Task.Delay(1000);

var secondTask = Task.Run(async () =>
{
    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    using var cmd = new SqlCommand(
        "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000; SELECT @result;",
        conn,
        tx);
    cmd.Parameters.AddWithValue("@resource", resource);
    var raw = await cmd.ExecuteScalarAsync();
    var result = raw is null ? -999 : Convert.ToInt32(raw);
    sw.Stop();
    Console.WriteLine($"SECOND_LOCK_RESULT={result} WAIT_MS={sw.ElapsedMilliseconds} @ {DateTime.UtcNow:O}");

    await tx.CommitAsync();
    Console.WriteLine($"SECOND_COMMIT @ {DateTime.UtcNow:O}");
});

await Task.WhenAll(firstTask, secondTask);
