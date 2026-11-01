using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed partial class OrderRepository
{
    public async Task<IReadOnlyDictionary<int,decimal>?> GetCapturedBasePointsAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT piece.OrderItemID,SUM(value.OriginalBasePoints),COUNT(*),COUNT(value.OriginalBasePoints)
            FROM dbo.Pieces piece JOIN dbo.OrderItems item ON item.OrderItemID=piece.OrderItemID
            LEFT JOIN dbo.TailoringPieceSaleValues value ON value.PieceID=piece.PieceID
            WHERE item.OrderID=@orderId GROUP BY piece.OrderItemID
            """, connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<int,decimal>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetInt32(2) != reader.GetInt32(3)) return null;
            result.Add(reader.GetInt32(0),reader.GetDecimal(1));
        }
        return result.Count == 0 ? null : result;
    }

    public async Task<bool> HasCompletedEditAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT TOP(1) 1 FROM dbo.TailoringOrderEditOperations WHERE OrderId=@id AND ResultJson IS NOT NULL",connection);
        command.Parameters.AddWithValue("@id",orderId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task CaptureCreationBenefitsAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,cancellationToken);
        await using var customerCommand = new SqlCommand("SELECT CustomerID FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@id",connection,transaction);
        customerCommand.Parameters.AddWithValue("@id",orderId);
        var customerId = Convert.ToInt32(await customerCommand.ExecuteScalarAsync(cancellationToken));
        await using var pieceCommand = new SqlCommand("""
            SELECT piece.PieceID,value.OriginalBasePoints,value.BenefitsCaptured
            FROM dbo.Pieces piece JOIN dbo.OrderItems item ON item.OrderItemID=piece.OrderItemID
            JOIN dbo.TailoringPieceSaleValues value WITH(UPDLOCK,HOLDLOCK) ON value.PieceID=piece.PieceID
            WHERE item.OrderID=@id ORDER BY piece.PieceID
            """,connection,transaction);
        pieceCommand.Parameters.AddWithValue("@id",orderId);
        await using var pieceReader = await pieceCommand.ExecuteReaderAsync(cancellationToken);
        var pieces = new List<(int PieceId,decimal BasePoints,bool Captured)>();
        while (await pieceReader.ReadAsync(cancellationToken))
            pieces.Add((pieceReader.GetInt32(0),pieceReader.GetDecimal(1),pieceReader.GetBoolean(2)));
        await pieceReader.DisposeAsync();
        if (pieces.Count == 0 || pieces.All(piece => piece.Captured))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (pieces.Any(piece => piece.Captured)) throw new InvalidOperationException("The original benefit snapshot is incomplete.");
        var totalBase = pieces.Sum(piece => piece.BasePoints);
        var groups = new List<(int CustomerId,string Kind,int Level,decimal Points,long? LoyaltyId,long? ReferralId)>();
        await using (var grants = new SqlCommand("""
            SELECT CustomerId,SUM(Points),MAX(LoyaltyTransactionId)
            FROM dbo.LoyaltyTransactions WITH(HOLDLOCK)
            WHERE OrderId=@id AND Source=N'PiecePurchase' AND TransactionType=N'Earn' GROUP BY CustomerId
            """,connection,transaction))
        {
            grants.Parameters.AddWithValue("@id",orderId);
            await using var reader = await grants.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetInt32(0)!=customerId || reader.GetDecimal(1)!=totalBase)
                    throw new InvalidOperationException("The original buyer grant does not match the captured official piece bases.");
                groups.Add((customerId,"Buyer",0,reader.GetDecimal(1),reader.GetInt64(2),null));
                if (await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Ambiguous original buyer grant.");
            }
            else groups.Add((customerId,"Buyer",0,0m,null,null));
        }
        await using (var grants = new SqlCommand("""
              SELECT reward.ReferrerCustomerId,SUM(reward.LoyaltyPoints),MAX(reward.ReferralTransactionId),MAX(reward.Notes),
                (SELECT MAX(loyalty.LoyaltyTransactionId) FROM dbo.LoyaltyTransactions loyalty
                  WHERE loyalty.OrderId=@id AND loyalty.CustomerId=reward.ReferrerCustomerId AND loyalty.Source=N'ReferralReward'),
                 (SELECT SUM(loyalty.Points) FROM dbo.LoyaltyTransactions loyalty
                  WHERE loyalty.OrderId=@id AND loyalty.CustomerId=reward.ReferrerCustomerId AND loyalty.Source=N'ReferralReward')
            FROM dbo.ReferralTransactions reward WITH(HOLDLOCK)
            WHERE reward.OrderId=@id AND reward.TransactionType=N'RewardGranted'
            GROUP BY reward.ReferrerCustomerId
            """,connection,transaction))
        {
            grants.Parameters.AddWithValue("@id",orderId);
            await using var reader = await grants.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var points = reader.GetDecimal(1);
                var levelField = reader.IsDBNull(3) ? null : reader.GetString(3).Split('|')
                    .SingleOrDefault(field => field.StartsWith("Level:",StringComparison.Ordinal));
                if (levelField is null || !int.TryParse(levelField.AsSpan(6),out var level) || level is < 1 or > 4 ||
                    reader.IsDBNull(4) || reader.IsDBNull(5) || reader.GetDecimal(5)!=points ||
                    decimal.Round(ReferralRewardEngine.CalculateLevels(totalBase,level)[0],4,MidpointRounding.AwayFromZero)!=points)
                    throw new InvalidOperationException("The original referral grant cannot be assigned without ambiguity.");
                groups.Add((reader.GetInt32(0),"Referral",level,points,reader.GetInt64(4),reader.GetInt64(2)));
            }
        }
        foreach (var group in groups)
        {
            var raw = pieces.Select(piece => group.Kind=="Buyer" ? piece.BasePoints
                : ReferralRewardEngine.CalculateLevels(piece.BasePoints,group.Level)[0]).ToArray();
            if (group.Points==0) Array.Fill(raw,0m);
            var shares = AllocateBenefitShares(raw,group.Points);
            for (var index=0;index<pieces.Count;index++)
            {
                await using var insert = new SqlCommand("""
                    INSERT dbo.TailoringPieceBenefitBalances(PieceId,CustomerId,BenefitKind,ReferralLevel,
                      OriginalPoints,CurrentPoints,OriginalLoyaltyTransactionId,OriginalReferralTransactionId)
                    VALUES(@piece,@customer,@kind,@level,@points,@points,@loyalty,@referral)
                    """,connection,transaction);
                insert.Parameters.AddWithValue("@piece",pieces[index].PieceId);
                insert.Parameters.AddWithValue("@customer",group.CustomerId);
                insert.Parameters.AddWithValue("@kind",group.Kind);
                insert.Parameters.AddWithValue("@level",group.Level);
                insert.Parameters.AddWithValue("@points",shares[index]);
                insert.Parameters.AddWithValue("@loyalty",(object?)group.LoyaltyId ?? DBNull.Value);
                insert.Parameters.AddWithValue("@referral",(object?)group.ReferralId ?? DBNull.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await using var mark = new SqlCommand("""
            UPDATE value SET BenefitsCaptured=1 FROM dbo.TailoringPieceSaleValues value
            JOIN dbo.Pieces piece ON piece.PieceID=value.PieceID JOIN dbo.OrderItems item ON item.OrderItemID=piece.OrderItemID
            WHERE item.OrderID=@id
            """,connection,transaction);
        mark.Parameters.AddWithValue("@id",orderId);
        await mark.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static decimal[] AllocateBenefitShares(IReadOnlyList<decimal> bases,decimal total)
    {
        var shares=bases.Select(value=>decimal.Floor(value*10000m)/10000m).ToArray();
        var remaining=decimal.ToInt32((total-shares.Sum())*10000m);
        if (remaining<0 || remaining>bases.Count) throw new InvalidOperationException("The original benefit allocation is inconsistent.");
        foreach(var index in Enumerable.Range(0,bases.Count).OrderByDescending(index=>bases[index]-shares[index]).ThenBy(index=>index).Take(remaining))
            shares[index]+=0.0001m;
        return shares;
    }
}