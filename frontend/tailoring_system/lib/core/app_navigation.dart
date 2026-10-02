import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';

typedef AppPageBuilder = Widget Function(BuildContext context);

class AppNavigation {
	AppNavigation._();

	static final navigatorKey = GlobalKey<NavigatorState>();
	static final observer = _AppNavigationObserver();

	static BuildContext? _safeContext(BuildContext? context) {
		if (context != null) {
			try {
				if (Navigator.maybeOf(context) != null) return context;
			} catch (_) {
				// Ignore invalid stale contexts and fall back to navigatorKey.
			}
		}
		return navigatorKey.currentContext;
	}

	static Future<T?> push<T>(BuildContext context, AppPageBuilder builder, {RouteSettings? settings}) {
		final safeContext = _safeContext(context);
		if (safeContext == null) {
			return Future<T?>.value();
		}
		final route = _AppPageRoute<T>(pageBuilder: builder, settings: settings);
		return Navigator.of(safeContext).push<T>(route);
	}

	static Future<T?> pushNamed<T>(BuildContext context, String routeName, {Object? arguments}) {
		final safeContext = _safeContext(context);
		if (safeContext == null) {
			return Future<T?>.value();
		}
		return Navigator.of(safeContext).pushNamed<T>(routeName, arguments: arguments);
	}

	static Future<void> back() async {
		final navigator = navigatorKey.currentState;
		if (navigator != null && navigator.canPop()) await navigator.maybePop();
	}

	static bool popIfPossible() {
		final navigator = navigatorKey.currentState;
		if (navigator == null || !navigator.canPop()) return false;
		back();
		return true;
	}

	static void forward() => observer.forward(navigatorKey.currentState);

	static void handlePointerDown(PointerDownEvent event) {
		if (event.kind != PointerDeviceKind.mouse) return;
		if (event.buttons & kBackMouseButton != 0) {
			popIfPossible();
		} else if (event.buttons & kForwardMouseButton != 0) {
			forward();
		}
	}
}

class AppNavigationRegion extends StatelessWidget {
	const AppNavigationRegion({
		required this.child,
		this.onBackMouseButton,
		this.onCloseWorkspaceTask,
		this.showWorkspaceCloseButton = false,
		super.key,
	});

	final Widget child;
	final VoidCallback? onBackMouseButton;
	final Future<void> Function(BuildContext context)? onCloseWorkspaceTask;
	final bool showWorkspaceCloseButton;

	@override
	Widget build(BuildContext context) => Stack(
		children: [
			Listener(
				behavior: HitTestBehavior.translucent,
				onPointerDown: (event) {
					if (event.kind == PointerDeviceKind.mouse &&
						event.buttons & kBackMouseButton != 0) {
						if (!AppNavigation.popIfPossible()) {
							onBackMouseButton?.call();
						}
						return;
					}
					AppNavigation.handlePointerDown(event);
				},
				child: child,
			),
			if (showWorkspaceCloseButton && onCloseWorkspaceTask != null)
				Positioned(
					right:1,
					top: 1,
					child: Material(
						color: Theme.of(context).colorScheme.errorContainer,
						borderRadius: BorderRadius.circular(6),
						child: Semantics(
							label: 'إغلاق الشاشة',
							child: IconButton(
								padding: EdgeInsets.zero,
								constraints: const BoxConstraints(
									minWidth: 37,
									maxWidth: 37,
									minHeight: 26,
									maxHeight: 26,
								),
								onPressed: () => onCloseWorkspaceTask!(context),
								icon: Icon(
									Icons.close_rounded,
									size: 18,
									color: Theme.of(context).colorScheme.error,
								),
							),
						),
					),
				),
		],
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