import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/referral_models.dart';
import 'package:tailoring_system/repositories/referral_repository.dart';
import 'package:tailoring_system/screens/referral/referral_dashboard_screen.dart';

class _DashboardRepository extends ReferralRepository {
  @override
  Future<ReferralDashboard> getDashboard() async => ReferralDashboard(
        totalRegistrations: 15,
        participatingCustomers: 9,
        rewardsGranted: 4,
        rewardReversals: 1,
        totalReferralPoints: 200,
        topReferrers: const [],
        topCodes: const [],
        recentEvents: const [],
        topReceivers: const [],
        treeSummary: const ReferralDashboardTreeSummary(
          rootCount: 2,
          maxDepth: 4,
          maxDirectReferrals: 3,
        ),
      );
}

void main() {
  testWidgets('dashboard renders API metrics and tree summary', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralDashboardScreen(repository: _DashboardRepository()),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('15'), findsOneWidget);
    expect(find.text('9'), findsOneWidget);
    expect(find.text('4'), findsOneWidget);
    expect(find.text('1'), findsOneWidget);
    expect(find.text('200'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('عدد الجذور'),
      500,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('عدد الجذور'), findsOneWidget);
    expect(find.text('أكبر عمق'), findsOneWidget);
    expect(find.text('أكبر عدد تابعين مباشرين'), findsOneWidget);
  });
}
