$json = @"
{
  "goodsReceiptItemId": 329,
  "itemCode": "FA0033",
  "fabricTypeCode": "تي ار صيني",
  "rollCode": "R-329-01",
  "colorValue": "أبيض",
  "unitId": 1,
  "opposingLedgerAccountCode": "2100",
  "sourceOperationId": "7a4c8d2d-b5bf-4a8d-a2d4-82f6983e5d59"
}
"@

curl.exe -s -w "`nHTTP_STATUS:%{http_code}`n" -X POST "http://127.0.0.1:5093/inventory/foundation/fabric-receipts" -H "Content-Type: application/json" --data $json
