import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/referral_models.dart';
import 'package:tailoring_system/repositories/referral_repository.dart';
import 'package:tailoring_system/screens/referral/referral_history_screen.dart';

class _ReferralHistoryRepository extends ReferralRepository {
  @override
  Future<List<ReferralRoot>> getRoots() async => const [
        ReferralRoot(
          customerId: 50,
          customerCode: 'C10046',
          customerName: 'مسعد مسعد',
          directReferralsCount: 1,
          totalDescendantsCount: 1,
          maxDepth: 1,
        ),
      ];

  @override
  Future<ReferralTree> getTree(int customerId) async => const ReferralTree(
        rootCustomerId: 50,
        rootCustomerCode: 'C10046',
        rootCustomerName: 'مسعد مسعد',
        directReferralsCount: 1,
        totalDescendantsCount: 1,
        maxDepth: 1,
        children: [
          ReferralTreeNode(
            customerId: 51,
            customerCode: 'C10047',
            customerName: 'سعدون مسعد',
            parentCustomerId: 50,
            level: 1,
            children: [],
            directChildrenCount: 0,
            totalDescendantsCount: 0,
            maxDepth: 0,
            referralCode: 'R-10051',
            isActive: true,
          ),
        ],
      );

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
          notes: 'تسجيل فعلي',
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
          notes: 'مكافأة فعلية',
          createdAt: DateTime(2026, 8, 18),
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
          lastUsedAt: null,
        ),
      ];
}

void main() {
  testWidgets('displays real referral transaction events and details', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralHistoryScreen(repository: _ReferralHistoryRepository()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('سجل أحداث الإحالات'), findsOneWidget);
    expect(find.text('تسجيل إحالة'), findsOneWidget);
    expect(find.text('منح مكافأة إحالة'), findsOneWidget);
    expect(find.text('إجمالي نقاط الإحالة'), findsOneWidget);
    expect(find.text('50.0 نقطة'), findsOneWidget);

    await tester.tap(find.text('منح مكافأة إحالة'));
    await tester.pumpAndSettle();
    expect(find.text('رقم الطلب'), findsOneWidget);
    expect(find.text('900'), findsOneWidget);
    expect(find.text('50.0'), findsOneWidget);
  });
}
