import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/referral_models.dart';
import 'package:tailoring_system/repositories/referral_repository.dart';
import 'package:tailoring_system/screens/referral/referral_codes_screen.dart';

class _ReferralCodesRepository extends ReferralRepository {
  @override
  Future<List<ReferralCustomerIdentity>> getAllCustomerIdentities() async =>
      const [
        ReferralCustomerIdentity(
          customerId: 50,
          customerCode: 'C10046',
          customerName: 'مسعد مسعد',
        ),
      ];

  @override
  Future<List<ReferralCode>> getCodes(int customerId) async => [
        ReferralCode(
          referralCodeId: 7,
          customerId: 50,
          code: 'REF-50',
          isActive: true,
          createdAt: DateTime(2026, 8, 1),
          lastUsedAt: DateTime(2026, 8, 17),
        ),
      ];

  @override
  Future<List<ReferralTransaction>> getTransactions(int customerId) async => [
        ReferralTransaction(
          referralTransactionId: 1,
          referrerCustomerId: 50,
          referredCustomerId: 51,
          referralCodeId: 7,
          orderId: null,
          referralRewardId: null,
          transactionType: 'Registration',
          fixedRewardAmount: 0,
          loyaltyPoints: 0,
          notes: null,
          createdAt: DateTime(2026, 8, 17),
        ),
        ReferralTransaction(
          referralTransactionId: 2,
          referrerCustomerId: 50,
          referredCustomerId: 51,
          referralCodeId: 7,
          orderId: 900,
          referralRewardId: 3,
          transactionType: 'RewardGranted',
          fixedRewardAmount: 25,
          loyaltyPoints: 50,
          notes: null,
          createdAt: DateTime(2026, 8, 18),
        ),
      ];
}

void main() {
  testWidgets('displays referral codes and database-backed usage',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralCodesScreen(repository: _ReferralCodesRepository()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('أكواد الإحالة'), findsOneWidget);
    expect(find.text('REF-50'), findsOneWidget);
    expect(find.textContaining('مسعد مسعد'), findsOneWidget);
    expect(find.text('1 عميل'), findsOneWidget);
    expect(find.text('إجمالي الأكواد'), findsOneWidget);

    await tester.tap(find.text('REF-50'));
    await tester.pumpAndSettle();
    expect(find.text('مرات الاستخدام'), findsOneWidget);
    expect(find.text('إجمالي نقاط المكافآت'), findsOneWidget);
    expect(find.text('25.0'), findsOneWidget);
    expect(find.text('50.0'), findsOneWidget);
  });
}
