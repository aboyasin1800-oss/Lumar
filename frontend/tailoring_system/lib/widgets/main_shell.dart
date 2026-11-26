import 'package:flutter/material.dart';

import '../core/ui_palette.dart';
import '../screens/module_screens.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/workspace_controller.dart';
import '../services/workspace_registry.dart';
import '../services/sensitive_area_lock.dart';
import 'workspace_taskbar.dart';

class MainShell extends StatelessWidget {
  const MainShell({
    required this.auth,
    required this.themeState,
    required this.uiScale,
    required this.workspace,
    super.key,
  });

  final AuthState auth;
  final ThemeState themeState;
  final UiScaleState uiScale;
  final WorkspaceController workspace;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color.fromARGB(255, 25, 34, 49),
      body: Column(
        children: [
          Expanded(
            child: _WorkspaceViewport(
              auth: auth,
              workspace: workspace,
            ),
          ),
          WorkspaceTaskbar(controller: workspace),
        ],
      ),
    );
  }
}

class _WorkspaceViewport extends StatefulWidget {
  const _WorkspaceViewport({required this.auth, required this.workspace});

  final AuthState auth;
  final WorkspaceController workspace;

  @override
  State<_WorkspaceViewport> createState() => _WorkspaceViewportState();
}

class _WorkspaceViewportState extends State<_WorkspaceViewport> {
  late final Widget _dashboard;

  @override
  void initState() {
    super.initState();
    _dashboard = DashboardScreen(workspace: widget.workspace);
    widget.workspace.protectedAreasKey.loadStatus();
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.workspace,
      builder: (context, _) {
        final openTasks = widget.workspace.openTasks;
        final children = <Widget>[
          _dashboard,
          ...openTasks.map(
            (task) => KeyedSubtree(
              key: ValueKey(task.definition.routeId),
              child: widget.workspace.buildTask(task),
            ),
          ),
        ];
        final activeIndex = widget.workspace.activeRouteId == null
            ? 0
            : openTasks.indexWhere(
                  (task) =>
                      task.definition.routeId == widget.workspace.activeRouteId,
                ) +
                1;

        return Column(
          children: [
            _WorkspaceHeader(
              auth: widget.auth,
              workspace: widget.workspace,
            ),
            const Divider(height: 1),
            Expanded(
              child: IndexedStack(
                index: activeIndex < 0 ? 0 : activeIndex,
                children: children,
              ),
            ),
          ],
        );
      },
    );
  }
}

class _WorkspaceHeader extends StatefulWidget {
  const _WorkspaceHeader({required this.auth, required this.workspace});

  final AuthState auth;
  final WorkspaceController workspace;

  @override
  State<_WorkspaceHeader> createState() => _WorkspaceHeaderState();
}

class _WorkspaceHeaderState extends State<_WorkspaceHeader>
    with WidgetsBindingObserver {
  int _pendingOrdersCount = 0;
  bool _pendingOrdersLoading = true;
  String? _lastLoadedRoute;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    widget.workspace.addListener(_onWorkspaceChanged);
    _refreshPendingOrders();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    widget.workspace.removeListener(_onWorkspaceChanged);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _refreshPendingOrders();
    }
  }

  void _onWorkspaceChanged() {
    final route = widget.workspace.activeRouteId;
    if (route != _lastLoadedRoute) {
      _lastLoadedRoute = route;
      _refreshPendingOrders();
    }
  }

  bool get _canOpenOrders {
    final role = widget.auth.user?.role?.trim().toLowerCase() ?? '';
    return role.isNotEmpty && role != 'customer' && role != 'supplier';
  }

  Future<void> _refreshPendingOrders() async {
    if (!mounted) return;
    setState(() => _pendingOrdersLoading = true);
    try {
      final requests = await widget.auth
          .listAccountRecoveryRequests()
          .timeout(const Duration(seconds: 8));
      final count = requests.where((item) {
        final status = (item['status'] ?? '').toString().trim();
        return status == 'Pending' ||
            status == 'MatchRequired' ||
            status == 'Approved';
      }).length;
      if (!mounted) return;
      setState(() {
        _pendingOrdersCount = count;
        _pendingOrdersLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _pendingOrdersLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLight = Theme.of(context).brightness == Brightness.light;
    final workspace = widget.workspace;
    final auth = widget.auth;
    final active = workspace.activeRouteId == null
        ? null
        : workspace.definitionFor(workspace.activeRouteId!);
    return SizedBox(
      height: 48,
      child: ColoredBox(
        color: isLight
            ? const Color(0xFFFFFFFF)
            : const Color.fromARGB(255, 25, 34, 49),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 64, 6),
          child: Row(
            children: [
              Icon(
                active?.icon ?? Icons.dashboard_outlined,
                color: UiPalette.primaryBlue,
                size: 21,
              ),
              const SizedBox(width: 9),
              Expanded(
                child: Text(
                  active?.title ?? 'لوحة التحكم',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: isLight
                            ? const Color(0xFF172033)
                            : UiPalette.textMain,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              const CircleAvatar(
                radius: 16,
                child: Icon(Icons.person_outline, size: 18),
              ),
              const SizedBox(width: 8),
              Text(auth.user?.username ?? ''),
              const SizedBox(width: 8),
              if (_canOpenOrders)
                TextButton.icon(
                  onPressed: () {
                    widget.workspace.open('/settings/account-recovery');
                  },
                  icon: Stack(
                    clipBehavior: Clip.none,
                    children: [
                      const Icon(Icons.receipt_long_outlined, size: 20),
                      Positioned(
                        right: -8,
                        top: -8,
                        child: Container(
                          constraints: const BoxConstraints(minWidth: 16),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 3, vertical: 1),
                          decoration: BoxDecoration(
                            color: Theme.of(context).colorScheme.error,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          alignment: Alignment.center,
                          child: _pendingOrdersLoading
                              ? const SizedBox(
                                  width: 8,
                                  height: 8,
                                  child: CircularProgressIndicator(
                                      strokeWidth: 1.5))
                              : Text('$_pendingOrdersCount',
                                  style: const TextStyle(
                                      color: Colors.white,
                                      fontSize: 9,
                                      fontWeight: FontWeight.w700)),
                        ),
                      ),
                    ],
                  ),
                  label: const Text('طلبات الاستعادة'),
                ),
              const SizedBox(width: 8),
              ListenableBuilder(
                listenable: widget.workspace.protectedAreasKey,
                builder: (context, _) {
                  final key = workspace.protectedAreasKey;
                  final unlocked = key.unlocked;
                  return Tooltip(
                    message: unlocked
                        ? 'المناطق المحمية مفتوحة'
                        : 'المناطق المحمية مقفلة',
                    child: TextButton.icon(
                      onPressed: () async {
                        if (unlocked) {
                          key.close();
                          if (workspace.activeRouteId != null &&
                              SensitiveAreaLock.isLockedRoute(
                                  workspace.activeRouteId!)) {
                            workspace.open(WorkspaceRouteIds.dashboard);
                          }
                        } else if (!key.passwordConfigured) {
                          await key.requestInitialSetup(context);
                        } else {
                          await key.requestUnlock(context);
                        }
                      },
                      icon: Icon(
                          unlocked
                              ? Icons.lock_open_outlined
                              : Icons.lock_outline,
                          color: unlocked ? Colors.green : null),
                      label: Text(unlocked
                          ? (key.manualOnly
                              ? 'مفتوحة حتى الإغلاق اليدوي'
                              : 'المناطق مفتوحة')
                          : 'المناطق الحساسة مقفلة'),
                    ),
                  );
                },
              ),
              const SizedBox(width: 8),
              IconButton(
                tooltip: 'تسجيل الخروج',
                onPressed: () async {
                  workspace.clearLockedAccess();
                  await auth.logout();
                },
                icon: const Icon(Icons.logout),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
