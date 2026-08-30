using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ScannerRepository(ReadOnlySqlConnectionFactory connections) : IScannerRepository
{
    public Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken ct) => QueryAsync("SELECT ScannerId, ScannerCode, ScannerName, Description, IsActive, CreatedAt, UpdatedAt FROM dbo.Scanners ORDER BY ScannerName, ScannerId", reader => new ScannerDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("Description"), reader.GetBoolean(4), reader.GetDateTime(5), reader.NullableDateTime("UpdatedAt")), ct);
    public Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken ct) => QueryAsync("SELECT ID, TrackingCode, ScanTime FROM dbo.Live_Scan ORDER BY ScanTime DESC, ID DESC", reader => new LiveScanDto(reader.GetInt32(0), reader.NullableInt32("TrackingCode"), reader.NullableDateTime("ScanTime")), ct);
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}