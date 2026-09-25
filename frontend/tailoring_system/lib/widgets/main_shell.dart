import 'package:flutter/material.dart';

import '../core/ui_palette.dart';
import '../screens/module_screens.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/workspace_controller.dart';
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
      backgroundColor: UiPalette.screenBackground,
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

class _WorkspaceHeader extends StatelessWidget {
  const _WorkspaceHeader({required this.auth, required this.workspace});

  final AuthState auth;
  final WorkspaceController workspace;

  @override
  Widget build(BuildContext context) {
    final active = workspace.activeRouteId == null
        ? null
        : workspace.definitionFor(workspace.activeRouteId!);
    return SizedBox(
      height: 48,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 6),
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
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
            const CircleAvatar(
              radius: 16,
              child: Icon(Icons.person_outline, size: 18),
            ),
            const SizedBox(width: 8),
            Text(auth.user?.username ?? ''),
            const SizedBox(width: 8),
            IconButton(
              tooltip: 'تسجيل الخروج',
              onPressed: () async {
                await auth.logout();
              },
              icon: const Icon(Icons.logout),
            ),
          ],
        ),
      ),
    );
  }
}
