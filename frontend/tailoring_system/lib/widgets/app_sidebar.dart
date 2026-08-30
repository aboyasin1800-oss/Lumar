import 'package:flutter/material.dart';

import '../core/app_routes.dart';

class AppSidebar extends StatelessWidget {
	const AppSidebar({required this.selectedIndex, required this.onDestinationSelected, super.key});
	final int selectedIndex;
	final ValueChanged<int> onDestinationSelected;

	@override
	Widget build(BuildContext context) => SingleChildScrollView(
		child: IntrinsicHeight(
			child: NavigationRail(
				selectedIndex: selectedIndex,
				onDestinationSelected: onDestinationSelected,
				labelType: NavigationRailLabelType.all,
				leading: const Padding(
					padding: EdgeInsets.symmetric(vertical: 16),
					child: Text('لومار', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
				),
				destinations: AppRoutes.destinations.map((destination) => NavigationRailDestination(
					icon: Icon(destination.icon),
					selectedIcon: Icon(destination.icon),
					label: Text(destination.title),
				)).toList(),
			),
		),
	);
}