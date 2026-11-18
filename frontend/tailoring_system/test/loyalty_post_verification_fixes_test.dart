import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
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
  test('program settings fetches the active loyalty-management endpoint', () async {
    final client = MockClient((request) async {
      expect(request.method, 'GET');
      expect(request.url.toString(),
          'http://127.0.0.1:5093/api/loyalty-management/program-settings');

      return http.Response(
        jsonEncode([
          {
            'loyaltyProgramSettingId': 1,
            'isEnabled': true,
            'pointsPerPiece': 10,
            'pointMonetaryValue': 1,
            'effectiveFromUtc': '2024-01-01T00:00:00Z',
            'createdAt': '2024-01-01T00:00:00Z',
            'updatedAt': '2024-01-01T00:00:00Z',
            'allowRedemption': true,
            'minimumRedemptionPoints': 0,
            'maximumRedemptionPoints': 1000,
            'loyaltyFreezeEnabled': false,
            'gracePeriodDays': 180,
            'warningPeriodDays': 30,
            'manualReactivationEnabled': true,
            'purchaseReactivationEnabled': true,
          },
        ]),
        200,
      );
    });

    final settings = await LoyaltyRepository(client: client).getProgramSettings();

    expect(settings, hasLength(1));
    expect(settings.first.isEnabled, isTrue);
    expect(settings.first.pointsPerPiece, 10);
  });

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
