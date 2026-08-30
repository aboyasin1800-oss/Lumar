import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class SidebarState extends ChangeNotifier {
  static const _storageKey = 'lumar_sidebar_visible';
  final _storage = const FlutterSecureStorage();
  bool _isVisible = false;
  bool initialized = false;

  bool get isVisible => _isVisible;

  Future<void> initialize() async {
    _isVisible = (await _storage.read(key: _storageKey)) == 'true';
    initialized = true;
    notifyListeners();
  }

  Future<void> setVisible(bool value) async {
    if (_isVisible == value) return;
    _isVisible = value;
    await _storage.write(key: _storageKey, value: value.toString());
    notifyListeners();
  }

  Future<void> toggle() => setVisible(!_isVisible);
}
