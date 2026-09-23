import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:tailoring_system/repositories/loyalty_repository.dart';
import 'package:tailoring_system/screens/loyalty/vip_customers_list_screen.dart';

void main() {
  testWidgets('filters VIP rows and opens the existing evaluation screen',
      (tester) async {
    final client = MockClient((request) async {
      switch (request.url.path) {
        case '/api/vip-levels/customers':
          return http.Response(
            jsonEncode([
              _customerJson(1, 'مسعد مسعد', 'GOLD', 'ذهبي', 3, 100, 4),
              _customerJson(2, 'سعدون مسعد', 'SILVER', 'فضي', 2, 90, 2),
              _customerJson(3, 'عميل برونزي', 'BRONZE', 'برونزي', 1, 0, 0),
            ]),
            200,
          );
        case '/api/vip-levels/criteria':
          return http.Response('[]', 200);
        case '/api/vip-levels/customers/1/evaluation':
          return http.Response(
            jsonEncode({
              'customerId': 1,
              'customerCode': 'C-1',
              'customerName': 'مسعد مسعد',
              'previousVipLevelId': 2,
              'previousVipLevelCode': 'SILVER',
              'previousVipLevelDisplayName': 'فضي',
              'evaluatedVipLevelId': 3,
              'evaluatedVipLevelCode': 'GOLD',
              'evaluatedVipLevelDisplayName': 'ذهبي',
              'directReferralCount': 1,
              'level1ReferralCount': 1,
              'level2ReferralCount': 2,
              'level3ReferralCount': 1,
              'level4ReferralCount': 0,
              'networkSize': 4,
              'networkMaxDepth': 3,
              'ownOrderCount': 5,
              'networkOrderCount': 5,
              'score': 100,
              'reason': 'اختبار قائمة VIP',
              'evaluatedAtUtc': '2026-09-20T00:00:00Z',
              'changed': true,
            }),
            200,
          );
        default:
          return http.Response('{}', 404);
      }
    });

    await tester.pumpWidget(
      MaterialApp(
        home: VipCustomersListScreen(
          repository: LoyaltyRepository(client: client),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('مسعد مسعد'), findsOneWidget);
    expect(find.text('سعدون مسعد'), findsOneWidget);
    expect(find.text('عميل برونزي'), findsOneWidget);

    await tester.tap(find.text('ذهبي').last);
    await tester.pumpAndSettle();

    expect(find.text('مسعد مسعد'), findsOneWidget);
    expect(find.text('سعدون مسعد'), findsNothing);
    expect(find.text('عميل برونزي'), findsNothing);

    await tester.tap(find.text('مسعد مسعد'));
    await tester.pumpAndSettle();

    expect(find.text('تقييم مستويات كبار العملاء'), findsOneWidget);
  });
}

Map<String, dynamic> _customerJson(
  int customerId,
  String customerName,
  String levelCode,
  String levelName,
  int priority,
  num score,
  int networkSize,
) => {
      'customerId': customerId,
      'customerCode': 'C-$customerId',
      'customerName': customerName,
      'vipLevelId': priority,
      'vipLevelCode': levelCode,
      'vipLevelDisplayName': levelName,
      'vipLevelPriority': priority,
      'score': score,
      'directReferralCount': networkSize > 0 ? 1 : 0,
      'ownOrderCount': networkSize > 0 ? 5 : 0,
      'networkOrderCount': networkSize,
      'networkSize': networkSize,
      'networkMaxDepth': networkSize > 0 ? 2 : 0,
      'evaluatedAtUtc': '2026-09-20T00:00:00Z',
    };