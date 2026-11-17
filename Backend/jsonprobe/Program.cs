using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class ReceiveFabricInventoryDto
{
    public int GoodsReceiptItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string FabricTypeCode { get; set; } = string.Empty;
    public string OpposingLedgerAccountCode { get; set; } = string.Empty;
    public Guid SourceOperationId { get; set; }
    public List<ReceiveFabricInventoryRollDto>? Rolls { get; set; }
}

public sealed class ReceiveFabricInventoryRollDto
{
    public string FabricCode { get; set; } = string.Empty;
    public string? ColorValue { get; set; }
    public decimal Quantity { get; set; }
}

var json = "{ \"GoodsReceiptItemId\": 101, \"ItemCode\": \"ITEM-01\", \"FabricTypeCode\": \"TYPE-01\", \"OpposingLedgerAccountCode\": \"2100\", \"SourceOperationId\": \"a6d1a928-3f09-4fd7-8d36-12b3d8b7d5d9\", \"rolls\": [{ \"fabricCode\": \"FA-001\", \"rollCode\": \"LOCAL-ROLL-1\", \"quantity\": 6.5 }] }";
var dto = JsonSerializer.Deserialize<ReceiveFabricInventoryDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
Console.WriteLine($"HasRollCodeProperty={typeof(ReceiveFabricInventoryDto).GetProperty(\"RollCode\") is null}");
Console.WriteLine($"RollsCount={dto?.Rolls?.Count ?? -1}");
Console.WriteLine($"FabricCode={dto?.Rolls?[0].FabricCode ?? \"<null>\"}");
Console.WriteLine($"IgnoredRollCodeField={json.Contains(\"rollCode\", StringComparison.OrdinalIgnoreCase)}");
Console.WriteLine($"BatchPayloadValueIsUsed={dto?.Rolls?[0].FabricCode == \"FA-001\"}");
