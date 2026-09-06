import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';

typedef AppPageBuilder = Widget Function(BuildContext context);

class AppNavigation {
	AppNavigation._();

	static final navigatorKey = GlobalKey<NavigatorState>();
	static final observer = _AppNavigationObserver();

	static Future<T?> push<T>(BuildContext context, AppPageBuilder builder, {RouteSettings? settings}) {
		final route = _AppPageRoute<T>(pageBuilder: builder, settings: settings);
		return Navigator.of(context).push<T>(route);
	}

	static Future<T?> pushNamed<T>(BuildContext context, String routeName, {Object? arguments}) =>
			Navigator.of(context).pushNamed<T>(routeName, arguments: arguments);

	static Future<void> back() async {
		final navigator = navigatorKey.currentState;
		if (navigator != null && navigator.canPop()) await navigator.maybePop();
	}

	static void forward() => observer.forward(navigatorKey.currentState);

	static void handlePointerDown(PointerDownEvent event) {
		if (event.kind != PointerDeviceKind.mouse) return;
		if (event.buttons & kBackMouseButton != 0) {
			back();
		} else if (event.buttons & kForwardMouseButton != 0) {
			forward();
		}
	}
}

class AppNavigationRegion extends StatelessWidget {
	const AppNavigationRegion({required this.child, super.key});

	final Widget child;

	@override
	Widget build(BuildContext context) => Listener(
		behavior: HitTestBehavior.translucent,
		onPointerDown: AppNavigation.handlePointerDown,
		child: child,
	);
}

class _AppPageRoute<T> extends MaterialPageRoute<T> {
	_AppPageRoute({required this.pageBuilder, super.settings}) : super(builder: pageBuilder);

	final AppPageBuilder pageBuilder;
}

class _ForwardEntry {
	const _ForwardEntry.page(this.builder, this.settings) : routeName = null, arguments = null;
	const _ForwardEntry.named(this.routeName, this.arguments) : builder = null, settings = null;

	final AppPageBuilder? builder;
	final RouteSettings? settings;
	final String? routeName;
	final Object? arguments;
}

class _AppNavigationObserver extends NavigatorObserver {
	final List<_ForwardEntry> _forwardHistory = [];
	bool _restoringForward = false;

	@override
	void didPush(Route<dynamic> route, Route<dynamic>? previousRoute) {
		super.didPush(route, previousRoute);
		if (_restoringForward || route is! PageRoute<dynamic>) return;
		_forwardHistory.clear();
	}

	@override
	void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) {
		super.didPop(route, previousRoute);
		if (route is _AppPageRoute<dynamic>) {
			_forwardHistory.add(_ForwardEntry.page(route.pageBuilder, route.settings));
		} else if (route is PageRoute<dynamic> && route.settings.name != null) {
			_forwardHistory.add(_ForwardEntry.named(route.settings.name, route.settings.arguments));
		}
	}

	@override
	void didReplace({Route<dynamic>? newRoute, Route<dynamic>? oldRoute}) {
		super.didReplace(newRoute: newRoute, oldRoute: oldRoute);
		_forwardHistory.clear();
	}

	void forward(NavigatorState? navigator) {
		if (navigator == null || _forwardHistory.isEmpty) return;
		final entry = _forwardHistory.removeLast();
		_restoringForward = true;
		if (entry.builder != null) {
			navigator.push<dynamic>(_AppPageRoute<dynamic>(pageBuilder: entry.builder!, settings: entry.settings));
		} else {
			navigator.pushNamed(entry.routeName!, arguments: entry.arguments);
		}
		_restoringForward = false;
	}
}