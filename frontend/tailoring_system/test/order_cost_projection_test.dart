import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/order_models.dart';

void main() {
  test('projects stored fabric cost and official pricing snapshot full cost', () {
    final fabric = OrderFabric.fromJson({
      'orderItemFabricId': 601,
      'orderItemId': 1274,
      'inventoryItemId': 608,
      'fabricCode': 'FA0110',
      'fabricType': 'Fabric',
      'fabricColor': 'Ivory',
      'quantity': 52.0,
      'unit': 'Inch',
      'unitCost': 27.78,
      'totalCost': 1444.44,
      'consumedQuantity': 52.0,
      'yardPrice': 1000.0,
      'createdDate': '2026-11-02T00:00:00Z',
    });
    final item = OrderItem.fromJson({
      'orderItemId': 1274,
      'orderId': 1580,
      'pieceType': 'Shirt',
      'quantity': 1,
      'fabricCode': 'FA0110',
      'fabricType': 'Fabric',
      'fabricColor': 'Ivory',
      'request1': null,
      'request2': null,
      'specialRequest': null,
      'notes1': null,
      'notes2': null,
      'measurementSnapshot': '{"Length":52}',
      'trackingCode': 'TRK-000064',
      'pieceStatus': 'Cancelled',
      'createdDate': '2026-11-02T00:00:00Z',
      'fullCost': 3014.44444040,
    });

    expect(fabric.consumedQuantity, 52.0);
    expect(fabric.consumedCost, closeTo(1444.44, 0.001));
    expect(item.snapshot.containsKey('_fullCost'), isFalse);
    expect(item.fullCost, closeTo(3014.44444040, 0.00000001));
  });
}