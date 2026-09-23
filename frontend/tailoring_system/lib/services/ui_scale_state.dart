import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class UiScaleState extends ChangeNotifier {
  static const _storageKey = 'lumar_ui_scale';
  static const levels = <double>[
    0.2,
    0.3,
    0.4,
    0.5,
    0.6,
    0.7,
    0.8,
    0.9,
    1,
    1.1,
    1.25,
    1.5
  ];

  final _storage = const FlutterSecureStorage();
  double _scale = 1;
  bool initialized = false;

  double get scale => _scale;
  int get levelIndex => levels.indexOf(_scale);

  Future<void> initialize() async {
    final storedValue = await _storage.read(key: _storageKey);
    final storedScale = double.tryParse(storedValue ?? '');
    _scale = levels.contains(storedScale) ? storedScale! : 1;
    initialized = true;
    notifyListeners();
  }

  Future<void> setScale(double value) async {
    if (!levels.contains(value) || _scale == value) return;
    _scale = value;
    await _storage.write(key: _storageKey, value: value.toString());
    notifyListeners();
  }

  Future<void> increase() async {
    final nextIndex = (levelIndex + 1).clamp(0, levels.length - 1);
    await setScale(levels[nextIndex]);
  }

  Future<void> decrease() async {
    final previousIndex = (levelIndex - 1).clamp(0, levels.length - 1);
    await setScale(levels[previousIndex]);
  }

  Future<void> reset() => setScale(1);
}
