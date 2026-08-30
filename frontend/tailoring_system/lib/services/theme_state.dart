import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

enum AppThemePreference { light, dark, system }

class ThemeState extends ChangeNotifier {
  static const _storageKey = 'lumar_theme_preference';
  final _storage = const FlutterSecureStorage();
  AppThemePreference _preference = AppThemePreference.system;
  bool initialized = false;

  AppThemePreference get preference => _preference;
  ThemeMode get themeMode => switch (_preference) {
    AppThemePreference.light => ThemeMode.light,
    AppThemePreference.dark => ThemeMode.dark,
    AppThemePreference.system => ThemeMode.system,
  };

  Future<void> initialize() async {
    final savedValue = await _storage.read(key: _storageKey);
    _preference = switch (savedValue) {
      'light' => AppThemePreference.light,
      'dark' => AppThemePreference.dark,
      _ => AppThemePreference.system,
    };
    initialized = true;
    notifyListeners();
  }

  Future<void> setPreference(AppThemePreference value) async {
    if (_preference == value) return;
    _preference = value;
    await _storage.write(key: _storageKey, value: value.name);
    notifyListeners();
  }
}
