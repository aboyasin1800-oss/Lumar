import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/referral_models.dart';
import 'package:tailoring_system/repositories/referral_repository.dart';
import 'package:tailoring_system/screens/referral/referral_rewards_screen.dart';

class _RewardsRepository extends ReferralRepository {
  @override
  Future<ReferralRewardsData> getReferralRewardsScreen({
    String? transactionType,
    DateTime? from,
    DateTime? to,
    int? customerId,
    String? search,
  }) async {
    return ReferralRewardsData(
      grantedCount: 2,
      reversalCount: 1,
      totalGrantedPoints: 100,
      beneficiaryCount: 1,
      topBeneficiaries: const [
        ReferralRewardBeneficiary(
          customerId: 50,
          customerCode: 'C10046',
          customerName: 'مسعد مسعد',
          totalPoints: 100,
          eventCount: 2,
        ),
      ],
      events: [
        ReferralRewardEvent(
          transactionId: 1,
          beneficiaryCustomerId: 50,
          beneficiaryCode: 'C10046',
          beneficiaryName: 'مسعد مسعد',
          referredCustomerId: 51,
          referredCustomerName: 'سعدون مسعد',
          transactionType: 'RewardGranted',
          fixedRewardAmount: 0,
          loyaltyPoints: 50,
          orderId: 10,
          notes: 'منح فعلي',
          createdAt: DateTime(2026, 9, 17),
        ),
        ReferralRewardEvent(
          transactionId: 2,
          beneficiaryCustomerId: 50,
          beneficiaryCode: 'C10046',
          beneficiaryName: 'مسعد مسعد',
          referredCustomerId: 51,
          referredCustomerName: 'سعدون مسعد',
          transactionType: 'RewardReversal',
          fixedRewardAmount: 0,
          loyaltyPoints: 50,
          orderId: 10,
          notes: 'عكس فعلي',
          createdAt: DateTime(2026, 9, 17),
        ),
      ],
    );
  }
}

void main() {
  testWidgets('renders real reward summary and both event types',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralRewardsScreen(repository: _RewardsRepository()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('2'), findsOneWidget);
    expect(find.text('1'), findsWidgets);
    expect(find.text('100'), findsOneWidget);
    expect(find.text('مسعد مسعد'), findsWidgets);

    await tester.scrollUntilVisible(
      find.text('منح مكافأة إحالة'),
      500,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('منح مكافأة إحالة'), findsOneWidget);
    expect(find.text('عكس مكافأة إحالة'), findsOneWidget);
  });
}
