using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PieceWageRepository(ReadOnlySqlConnectionFactory connections) : IPieceWageRepository
{
    public async Task<IReadOnlyList<PieceWageDto>> GetByPieceIdAsync(int pieceId, CancellationToken ct)
    {
        const string sql = "SELECT PieceWageRecordID, OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, PayrollPeriodId, PayrollRecordId, Status, Notes, CreatedAt FROM dbo.PieceWageRecords WHERE PieceID = @pieceId ORDER BY CreatedAt DESC, PieceWageRecordID DESC";
        await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@pieceId", pieceId); await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<PieceWageDto>(); while (await reader.ReadAsync(ct)) items.Add(new PieceWageDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.NullableInt32("EmployeeId"), reader.NullableString("EmployeeCode"), reader.GetString(7), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.NullableInt32("PayrollPeriodId"), reader.NullableInt32("PayrollRecordId"), reader.GetString(14), reader.NullableString("Notes"), reader.GetDateTime(16)));
        return items;
    }
}