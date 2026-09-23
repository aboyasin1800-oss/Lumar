import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/referral_models.dart';
import 'package:tailoring_system/repositories/referral_repository.dart';
import 'package:tailoring_system/screens/referral/referral_analytics_screen.dart';

class _AnalyticsRepository extends ReferralRepository {
  @override
  Future<ReferralAnalyticsData> getReferralAnalytics({
    String? search,
    DateTime? from,
    DateTime? to,
  }) async {
    return ReferralAnalyticsData(
      totalRegistrations: 27,
      totalReferredCustomers: 27,
      rewardsGranted: 27,
      totalReferralPoints: 860,
      averageReferralsPerCustomer: 2.7,
      topReferrers: const [
        ReferralAnalyticsReferrer(
          customerId: 1,
          customerCode: 'C001',
          customerName: 'مسعد مسعد',
          referralCount: 15,
        ),
      ],
      topCodes: const [
        ReferralAnalyticsCode(
          referralCodeId: 1,
          code: 'MASAD-001',
          customerId: 1,
          customerName: 'مسعد مسعد',
          usageCount: 15,
        ),
      ],
      topRewardCustomers: const [
        ReferralAnalyticsRewardCustomer(
          customerId: 1,
          customerCode: 'C001',
          customerName: 'مسعد مسعد',
          totalPoints: 350,
          rewardCount: 10,
        ),
      ],
      quality: const ReferralAnalyticsQuality(
        averageUsagePerCode: 2.7,
        referredCustomersRate: 100,
        rewardsToRegistrationsRate: 100,
        reversalsToRewardsRate: 14.81,
        rewardReversals: 4,
      ),
      activity: const ReferralAnalyticsActivity(
        todayRegistrations: 2,
        thisWeekRegistrations: 8,
        thisMonthRegistrations: 27,
        dailyRegistrations: [],
      ),
      tree: const ReferralAnalyticsTree(
        rootCount: 2,
        maxDepth: 4,
        largestNetworkSize: 18,
        largestNetworkCustomerId: 1,
        largestNetworkCustomerName: 'مسعد مسعد',
      ),
    );
  }
}

void main() {
  testWidgets('renders analytics returned by the API', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralAnalyticsScreen(repository: _AnalyticsRepository()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('27'), findsWidgets);
    expect(find.text('860'), findsOneWidget);
    expect(find.text('مسعد مسعد'), findsWidgets);

    await tester.scrollUntilVisible(
      find.text('جودة برنامج الإحالات'),
      500,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('نسبة العكس إلى المكافآت'), findsOneWidget);
    expect(find.text('14.81%'), findsOneWidget);
  });
}
