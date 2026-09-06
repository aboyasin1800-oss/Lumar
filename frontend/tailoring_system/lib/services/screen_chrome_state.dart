import 'package:flutter/foundation.dart';

class ScreenChromeState extends ChangeNotifier {
  ScreenChromeState._();

  static final instance = ScreenChromeState._();

  bool hideTopChrome = false;

  void setHideTopChrome(bool value) {
    if (hideTopChrome == value) return;
    hideTopChrome = value;
    notifyListeners();
  }
}
