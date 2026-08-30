import 'package:flutter/material.dart';

import '../core/app_routes.dart';
import '../screens/module_screens.dart';
import 'app_sidebar.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/sidebar_state.dart';

class MainShell extends StatefulWidget {
	const MainShell({required this.auth, required this.themeState, required this.uiScale, required this.sidebarState, this.initialIndex = 0, this.content, this.title, super.key});
	final AuthState auth;
	final ThemeState themeState;
	final UiScaleState uiScale;
	final SidebarState sidebarState;
	final int initialIndex;
	final Widget? content;
	final String? title;

	@override
	State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
	late int selectedIndex;

	@override
	void initState() {
		super.initState();
		selectedIndex = widget.initialIndex;
		if (selectedIndex != 0) widget.sidebarState.setVisible(true);
	}

	void _navigate(int index) {
		widget.sidebarState.setVisible(true);
		if (index == selectedIndex) return;
		Navigator.of(context).pushReplacementNamed(AppRoutes.destinations[index].route);
	}

	@override
	Widget build(BuildContext context) {
		final destination = AppRoutes.destinations[selectedIndex];
		final title = widget.title ?? destination.title;
		return Scaffold(
			body: Row(children: [
				if (widget.sidebarState.isVisible) ...[AppSidebar(selectedIndex: selectedIndex, onDestinationSelected: _navigate), const VerticalDivider(width: 1)],
				Expanded(child: Column(children: [
					Container(
						height: 72,
						padding: const EdgeInsets.symmetric(horizontal: 28),
						alignment: Alignment.centerLeft,
						child: Row(children: [
							IconButton(tooltip: widget.sidebarState.isVisible ? 'إخفاء القائمة الجانبية' : 'إظهار القائمة الجانبية', onPressed: widget.sidebarState.toggle, icon: Icon(widget.sidebarState.isVisible ? Icons.menu_open : Icons.menu)),
							const SizedBox(width: 8),
							Expanded(child: Text(title, style: Theme.of(context).textTheme.headlineSmall)),
							const CircleAvatar(child: Icon(Icons.person_outline)),
							const SizedBox(width: 10),
							Text(widget.auth.user!.username),
							IconButton(tooltip: 'تسجيل الخروج', onPressed: () async {await widget.auth.logout();if(mounted)Navigator.of(this.context).pushNamedAndRemoveUntil(AppRoutes.login, (_) => false);}, icon: const Icon(Icons.logout)),
						]),
					),
					const Divider(height: 1),
					Expanded(child: Padding(padding: const EdgeInsets.all(28), child: widget.content ?? screenForIndex(selectedIndex, widget.auth, widget.themeState, widget.uiScale))),
				])),
			]),
		);
	}
}