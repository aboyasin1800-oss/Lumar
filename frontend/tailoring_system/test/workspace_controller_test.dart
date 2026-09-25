import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:tailoring_system/services/auth_state.dart';
import 'package:tailoring_system/services/theme_state.dart';
import 'package:tailoring_system/services/ui_scale_state.dart';
import 'package:tailoring_system/services/workspace_controller.dart';
import 'package:tailoring_system/services/workspace_registry.dart';
import 'package:tailoring_system/screens/module_screens.dart';

class _MemoryPreferences implements WorkspacePreferences {
  final values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async => values[key] = value;
}

WorkspaceRouteDefinition _definition(String routeId, String title) =>
    WorkspaceRouteDefinition(
      routeId: routeId,
      title: title,
      icon: Icons.circle_outlined,
      factory: (_) => const SizedBox(key: ValueKey('screen')),
    );

WorkspaceController _controller(_MemoryPreferences preferences) =>
    WorkspaceController(
      registry: [
        _definition(WorkspaceRouteIds.dashboard, 'لوحة التحكم'),
        _definition('/customers', 'العملاء'),
        _definition('/reports', 'التقارير'),
      ],
      auth: AuthState(),
      themeState: ThemeState(),
      uiScale: UiScaleState(),
      preferences: preferences,
    );

void main() {
  test('central registry uses unique stable RouteIds and keeps Dashboard out of taskbar', () {
    final routeIds = workspaceRegistry.map((definition) => definition.routeId).toList();

    expect(routeIds.toSet(), hasLength(routeIds.length));
    expect(routeIds, contains(WorkspaceRouteIds.dashboard));
    expect(workspaceRegistry.any((definition) => definition.routeId == WorkspaceRouteIds.dashboard), isTrue);
  });

  test('registry keeps stable RouteIds and excludes Dashboard from taskbar', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();

    controller.open('/customers');

    expect(controller.taskbarTasks.map((task) => task.definition.routeId), ['/customers']);
    expect(controller.taskbarTasks.any((task) => task.definition.routeId == WorkspaceRouteIds.dashboard), isFalse);
  });

  test('opening an existing route activates one preserved task', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();

    controller.open('/customers');
    final task = controller.taskFor('/customers')!;
    final widget = controller.buildTask(task);
    controller.open('/customers');

    expect(controller.tasks, hasLength(1));
    expect(controller.activeRouteId, '/customers');
    expect(identical(controller.buildTask(task), widget), isTrue);
    expect(task.state, WorkspaceTaskState.openActive);
  });

  test('minimize and restore preserve the same task', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();
    controller.open('/customers');
    final task = controller.taskFor('/customers')!;
    final widget = controller.buildTask(task);

    controller.toggle('/customers');
    expect(controller.dashboardVisible, isTrue);
    expect(task.state, WorkspaceTaskState.minimized);

    controller.toggle('/customers');
    expect(controller.activeRouteId, '/customers');
    expect(identical(controller.buildTask(task), widget), isTrue);
  });

  test('activating another task keeps the previous task inactive', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();
    controller.open('/customers');
    controller.open('/reports');

    expect(controller.activeRouteId, '/reports');
    expect(controller.taskFor('/customers')!.state, WorkspaceTaskState.openInactive);
    expect(controller.taskFor('/reports')!.state, WorkspaceTaskState.openActive);
  });

  test('close disposes the task widget and dashboard becomes the fallback', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();
    controller.open('/customers');
    final task = controller.taskFor('/customers')!;
    controller.buildTask(task);

    controller.close('/customers');

    expect(controller.taskFor('/customers'), isNull);
    expect(task.widget, isNull);
    expect(controller.dashboardVisible, isTrue);
  });

  test('pinning persists closed shortcuts without creating screen state', () async {
    final preferences = _MemoryPreferences();
    final first = _controller(preferences);
    await first.initialize();
    await first.pin('/customers');
    expect(first.taskFor('/customers')!.state, WorkspaceTaskState.pinnedClosed);
    expect(first.taskFor('/customers')!.widget, isNull);

    final second = _controller(preferences);
    await second.initialize();

    expect(second.pinnedRouteIds, ['/customers']);
    expect(second.taskFor('/customers')!.state, WorkspaceTaskState.pinnedClosed);
    expect(second.taskFor('/customers')!.widget, isNull);
  });

  test('unpin removes a closed shortcut and keeps an open task until close', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();
    await controller.pin('/customers');
    controller.open('/customers');

    await controller.unpin('/customers');
    expect(controller.taskFor('/customers'), isNotNull);

    controller.close('/customers');
    expect(controller.taskFor('/customers'), isNull);
  });

  test('dirty tasks enter ClosingPending and require explicit confirmation', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();
    controller.open('/customers');
    controller.markDirty('/customers', true);

    expect(controller.beginClose('/customers'), isTrue);
    expect(controller.taskFor('/customers')!.state, WorkspaceTaskState.closingPending);
    controller.cancelClose('/customers');
    expect(controller.taskFor('/customers')!.state, WorkspaceTaskState.openActive);

    controller.close('/customers');
    expect(controller.taskFor('/customers'), isNotNull);
    controller.closeAfterConfirmation('/customers');
    expect(controller.taskFor('/customers'), isNull);
  });

  test('taskbar size and pinned order restore from local preferences', () async {
    final preferences = _MemoryPreferences();
    final first = _controller(preferences);
    await first.initialize();
    await first.pin('/customers');
    await first.pin('/reports');
    await first.setTaskbarSize(WorkspaceTaskbarSize.large);
    await first.reorderPinned(['/reports', '/customers']);

    final second = _controller(preferences);
    await second.initialize();

    expect(second.taskbarSize, WorkspaceTaskbarSize.large);
    expect(second.pinnedRouteIds, ['/reports', '/customers']);
  });

  test('opening Dashboard clears the active task and keeps the task available', () async {
    final controller = _controller(_MemoryPreferences());
    await controller.initialize();
    controller.open('/customers');
    controller.open(WorkspaceRouteIds.dashboard);

    expect(controller.dashboardVisible, isTrue);
    expect(controller.taskFor('/customers')!.state, WorkspaceTaskState.minimized);
  });
}
