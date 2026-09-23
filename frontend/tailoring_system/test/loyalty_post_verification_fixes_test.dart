import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/loyalty_models.dart';
import 'package:tailoring_system/repositories/loyalty_repository.dart';
import 'package:tailoring_system/screens/loyalty/loyalty_transactions_screen.dart';

class _RecordingLoyaltyRepository extends LoyaltyRepository {
  int? lastCustomerId;

  @override
  Future<LoyaltyTransactionsScreenData> getTransactionsScreen({
    String? search,
    String? transactionType,
    DateTime? from,
    DateTime? to,
    int? customerId,
  }) async {
    lastCustomerId = customerId;
    return const LoyaltyTransactionsScreenData(
      summary: LoyaltyTransactionsSummary(
        transactionCount: 0,
        earnedPointsTotal: 0,
        redeemedPointsTotal: 0,
        reversedPointsTotal: 0,
        adjustCount: 0,
        activeCustomerCount: 0,
        types: [],
      ),
      transactions: [],
    );
  }
}

void main() {
  test('redemption preview reads the backend validation message', () {
    final preview = LoyaltyRedemptionPreview.fromJson({
      'customerId': 78,
      'orderId': 308,
      'currentPoints': 100,
      'remainingOrderAmount': 50,
      'pointsRedeemed': 100,
      'pointMonetaryValue': 1,
      'creditAmount': 50,
      'isValid': false,
      'validationMessage': 'PointsRedeemed exceeds current points balance.',
    });

    expect(preview.validationMessage,
        'PointsRedeemed exceeds current points balance.');
  });

  testWidgets('transactions screen uses the requested customer scope',
      (tester) async {
    final repository = _RecordingLoyaltyRepository();
    tester.view.physicalSize = const Size(1400, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
        home: LoyaltyTransactionsScreen(
          customerId: 78,
          repository: repository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(repository.lastCustomerId, 78);
    expect(find.text('النطاق الحالي: العميل رقم 78'), findsOneWidget);
  });

  testWidgets('transactions screen uses all customers when no scope is given',
      (tester) async {
    final repository = _RecordingLoyaltyRepository();
    tester.view.physicalSize = const Size(1400, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
        home: LoyaltyTransactionsScreen(repository: repository),
      ),
    );
    await tester.pumpAndSettle();

    expect(repository.lastCustomerId, isNull);
    expect(find.text('النطاق الحالي: جميع العملاء'), findsOneWidget);
  });
}
