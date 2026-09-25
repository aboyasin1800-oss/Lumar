import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'auth_state.dart';
import 'theme_state.dart';
import 'ui_scale_state.dart';

enum WorkspaceTaskState {
  pinnedClosed,
  openActive,
  openInactive,
  minimized,
  closingPending,
  closed,
}

enum WorkspaceStatePolicy { keepAlive, lazyRestorable }

enum WorkspacePerformanceClass { light, heavy }

enum WorkspaceTaskbarSize { small, medium, large }

extension WorkspaceTaskbarSizeValues on WorkspaceTaskbarSize {
  String get storageValue => name;

  double get height => switch (this) {
        WorkspaceTaskbarSize.small => 34,
        WorkspaceTaskbarSize.medium => 44,
        WorkspaceTaskbarSize.large => 58,
      };

  String get arabicLabel => switch (this) {
        WorkspaceTaskbarSize.small => 'صغير',
        WorkspaceTaskbarSize.medium => 'متوسط',
        WorkspaceTaskbarSize.large => 'كبير',
      };

  static WorkspaceTaskbarSize fromStorage(String? value) => switch (value) {
        'small' => WorkspaceTaskbarSize.small,
        'large' => WorkspaceTaskbarSize.large,
        _ => WorkspaceTaskbarSize.medium,
      };
}

typedef WorkspaceScreenFactory = Widget Function(WorkspaceBuildContext context);

class WorkspaceRouteDefinition {
  const WorkspaceRouteDefinition({
    required this.routeId,
    required this.title,
    required this.icon,
    required this.factory,
    this.statePolicy = WorkspaceStatePolicy.keepAlive,
    this.performanceClass = WorkspacePerformanceClass.light,
    this.canOpenMultipleInstances = false,
    this.restorationEnabled = false,
  });

  final String routeId;
  final String title;
  final IconData icon;
  final WorkspaceScreenFactory factory;
  final WorkspaceStatePolicy statePolicy;
  final WorkspacePerformanceClass performanceClass;
  final bool canOpenMultipleInstances;
  final bool restorationEnabled;

  String get route => routeId;
}

class WorkspaceBuildContext {
  const WorkspaceBuildContext({
    required this.controller,
    required this.auth,
    required this.themeState,
    required this.uiScale,
  });

  final WorkspaceController controller;
  final AuthState auth;
  final ThemeState themeState;
  final UiScaleState uiScale;
}

abstract interface class WorkspacePreferences {
  Future<String?> read(String key);
  Future<void> write(String key, String value);
}

class SecureWorkspacePreferences implements WorkspacePreferences {
  const SecureWorkspacePreferences({FlutterSecureStorage storage = const FlutterSecureStorage()})
      : _storage = storage;

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read(String key) => _storage.read(key: key);

  @override
  Future<void> write(String key, String value) => _storage.write(key: key, value: value);
}

class WorkspaceTask {
  WorkspaceTask({required this.definition, required this.isPinned})
      : state = isPinned ? WorkspaceTaskState.pinnedClosed : WorkspaceTaskState.closed;

  final WorkspaceRouteDefinition definition;
  Widget? widget;
  WorkspaceTaskState state;
  bool isPinned;
  bool isDirty = false;
  WorkspaceTaskState? stateBeforeClosing;

  bool get isOpen => state != WorkspaceTaskState.pinnedClosed && state != WorkspaceTaskState.closed;
}

class WorkspaceController extends ChangeNotifier {
  WorkspaceController({
    required this.registry,
    required this.auth,
    required this.themeState,
    required this.uiScale,
    WorkspacePreferences? preferences,
  }) : _preferences = preferences ?? const SecureWorkspacePreferences();

  static const pinnedRouteIdsKey = 'lumar_workspace_pinned_route_ids';
  static const taskbarSizeKey = 'lumar_workspace_taskbar_size';

  final List<WorkspaceRouteDefinition> registry;
  final AuthState auth;
  final ThemeState themeState;
  final UiScaleState uiScale;
  final WorkspacePreferences _preferences;
  final List<WorkspaceTask> _tasks = [];

  late final Map<String, WorkspaceRouteDefinition> _definitions = {
    for (final definition in registry) definition.routeId: definition,
  };

  String? _activeRouteId;
  WorkspaceTaskbarSize _taskbarSize = WorkspaceTaskbarSize.medium;
  bool initialized = false;

  String? get activeRouteId => _activeRouteId;
  WorkspaceTaskbarSize get taskbarSize => _taskbarSize;
  bool get dashboardVisible => _activeRouteId == null;
  List<WorkspaceTask> get tasks => List.unmodifiable(_tasks);
  List<WorkspaceTask> get taskbarTasks => List.unmodifiable(
        _tasks.where((task) => task.state != WorkspaceTaskState.closed),
      );
  List<WorkspaceTask> get openTasks => List.unmodifiable(
        _tasks.where((task) => task.isOpen),
      );
  List<String> get pinnedRouteIds => List.unmodifiable(
        _tasks.where((task) => task.isPinned).map((task) => task.definition.routeId),
      );

  WorkspaceRouteDefinition? definitionFor(String routeId) => _definitions[routeId];

  WorkspaceTask? taskFor(String routeId) {
    for (final task in _tasks) {
      if (task.definition.routeId == routeId) return task;
    }
    return null;
  }

  Future<void> initialize() async {
    final savedPins = await _preferences.read(pinnedRouteIdsKey);
    final pinnedIds = _decodeRouteIds(savedPins);
    for (final routeId in pinnedIds) {
      if (_definitions.containsKey(routeId)) {
        _tasks.add(WorkspaceTask(definition: _definitions[routeId]!, isPinned: true));
      }
    }
    _taskbarSize = WorkspaceTaskbarSizeValues.fromStorage(
      await _preferences.read(taskbarSizeKey),
    );
    initialized = true;
    notifyListeners();
  }

  Widget buildTask(WorkspaceTask task) {
    return task.widget ??= task.definition.factory(
      WorkspaceBuildContext(
        controller: this,
        auth: auth,
        themeState: themeState,
        uiScale: uiScale,
      ),
    );
  }

  void open(String routeId) {
    if (routeId == '/dashboard') {
      _minimizeActiveWithoutNotification();
      _activeRouteId = null;
      notifyListeners();
      return;
    }

    final definition = _definitions[routeId];
    if (definition == null) return;
    final task = taskFor(routeId) ?? _createTask(definition);
    if (task == null) return;
    for (final other in _tasks) {
      if (other != task && other.state == WorkspaceTaskState.openActive) {
        other.state = WorkspaceTaskState.openInactive;
      }
    }
    task.state = WorkspaceTaskState.openActive;
    task.stateBeforeClosing = null;
    _activeRouteId = routeId;
    notifyListeners();
  }

  void toggle(String routeId) {
    if (_activeRouteId == routeId) {
      minimize(routeId);
    } else {
      open(routeId);
    }
  }

  void minimize(String routeId) {
    final task = taskFor(routeId);
    if (task == null || _activeRouteId != routeId) return;
    task.state = WorkspaceTaskState.minimized;
    _activeRouteId = null;
    notifyListeners();
  }

  bool beginClose(String routeId) {
    final task = taskFor(routeId);
    if (task == null || !task.isDirty) return false;
    if (task.state == WorkspaceTaskState.closingPending) return true;
    task.stateBeforeClosing = task.state;
    task.state = WorkspaceTaskState.closingPending;
    notifyListeners();
    return true;
  }

  void cancelClose(String routeId) {
    final task = taskFor(routeId);
    if (task == null || task.state != WorkspaceTaskState.closingPending) return;
    task.state = task.stateBeforeClosing ?? WorkspaceTaskState.openInactive;
    task.stateBeforeClosing = null;
    notifyListeners();
  }

  void close(String routeId) {
    final task = taskFor(routeId);
    if (task == null) return;
    if (task.isDirty) return;
    _removeTask(task);
    notifyListeners();
  }

  void closeAfterConfirmation(String routeId) {
    final task = taskFor(routeId);
    if (task == null) return;
    task.isDirty = false;
    _removeTask(task);
    notifyListeners();
  }

  void markDirty(String routeId, bool value) {
    final task = taskFor(routeId);
    if (task == null || task.isDirty == value) return;
    task.isDirty = value;
    notifyListeners();
  }

  Future<void> pin(String routeId) async {
    final task = taskFor(routeId) ?? _createTask(_definitions[routeId]);
    if (task == null || task.isPinned) return;
    task.isPinned = true;
    if (task.state == WorkspaceTaskState.closed) {
      task.state = WorkspaceTaskState.pinnedClosed;
    }
    await _persistPins();
    notifyListeners();
  }

  Future<void> unpin(String routeId) async {
    final task = taskFor(routeId);
    if (task == null || !task.isPinned) return;
    task.isPinned = false;
    if (!task.isOpen) {
      _tasks.remove(task);
    }
    await _persistPins();
    notifyListeners();
  }

  Future<void> setTaskbarSize(WorkspaceTaskbarSize size) async {
    if (_taskbarSize == size) return;
    _taskbarSize = size;
    await _preferences.write(taskbarSizeKey, size.storageValue);
    notifyListeners();
  }

  Future<void> reorderPinned(List<String> routeIds) async {
    final ordered = <WorkspaceTask>[];
    for (final routeId in routeIds) {
      final task = taskFor(routeId);
      if (task != null && task.isPinned) ordered.add(task);
    }
    for (final task in _tasks) {
      if (task.isPinned && !ordered.contains(task)) ordered.add(task);
    }
    final nonPinned = _tasks.where((task) => !task.isPinned).toList();
    _tasks
      ..clear()
      ..addAll(ordered)
      ..addAll(nonPinned);
    await _persistPins();
    notifyListeners();
  }

  @override
  void dispose() {
    _tasks.clear();
    super.dispose();
  }

  WorkspaceTask? _createTask(WorkspaceRouteDefinition? definition) {
    if (definition == null) return null;
    final task = WorkspaceTask(definition: definition, isPinned: false);
    _tasks.add(task);
    return task;
  }

  void _removeTask(WorkspaceTask task) {
    if (_activeRouteId == task.definition.routeId) _activeRouteId = null;
    task.widget = null;
    task.state = task.isPinned ? WorkspaceTaskState.pinnedClosed : WorkspaceTaskState.closed;
    task.stateBeforeClosing = null;
    if (!task.isPinned) _tasks.remove(task);
  }

  void _minimizeActiveWithoutNotification() {
    final active = _activeRouteId;
    if (active == null) return;
    final task = taskFor(active);
    if (task != null) task.state = WorkspaceTaskState.minimized;
  }

  Future<void> _persistPins() => _preferences.write(
        pinnedRouteIdsKey,
        jsonEncode(pinnedRouteIds),
      );

  List<String> _decodeRouteIds(String? raw) {
    if (raw == null || raw.trim().isEmpty) return const [];
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! List) return const [];
      return decoded.whereType<String>().toSet().toList();
    } catch (_) {
      return const [];
    }
  }
}
