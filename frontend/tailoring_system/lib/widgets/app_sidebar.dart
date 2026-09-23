import 'package:flutter/material.dart';

import '../core/app_routes.dart';

class AppSidebar extends StatelessWidget {
	const AppSidebar({required this.selectedIndex, required this.onDestinationSelected, super.key});
	final int selectedIndex;
	final ValueChanged<int> onDestinationSelected;

	static const Set<String> _hiddenRoutes = {
		AppRoutes.referralDashboard,
		AppRoutes.referralTree,
		AppRoutes.referralHistory,
		AppRoutes.referralCodes,
		AppRoutes.referralRewards,
		AppRoutes.referralAnalytics,
		AppRoutes.loyaltyDashboard,
		AppRoutes.loyaltyTransactions,
		AppRoutes.loyaltyRedemption,
		AppRoutes.loyaltyRedemptionsHistory,
		AppRoutes.loyaltyRewards,
		AppRoutes.loyaltyRules,
		AppRoutes.vipLevels,
	};

	@override
	Widget build(BuildContext context) {
		final visibleDestinations = AppRoutes.destinations
			.where((destination) => !_hiddenRoutes.contains(destination.route))
			.toList();
		final activeIndex = visibleDestinations.indexWhere(
			(destination) => destination.route == AppRoutes.destinations[selectedIndex].route,
		);

		return SingleChildScrollView(
			child: IntrinsicHeight(
				child: NavigationRail(
					selectedIndex: activeIndex >= 0 ? activeIndex : 0,
					onDestinationSelected: (index) {
						final selectedRoute = visibleDestinations[index].route;
						final targetIndex = AppRoutes.destinations.indexWhere(
							(destination) => destination.route == selectedRoute,
						);
						if (targetIndex >= 0) onDestinationSelected(targetIndex);
					},
					labelType: NavigationRailLabelType.all,
					leading: const Padding(
						padding: EdgeInsets.symmetric(vertical: 16),
						child: Text('لومار', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
					),
					destinations: visibleDestinations.map((destination) => NavigationRailDestination(
						icon: Icon(destination.icon),
						selectedIcon: Icon(destination.icon),
						label: Text(destination.title),
					)).toList(),
				),
			),
		);
	}
}