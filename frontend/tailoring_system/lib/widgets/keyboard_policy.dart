import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';

import '../services/ui_scale_state.dart';

class KeyboardPolicy extends StatelessWidget {
  const KeyboardPolicy({required this.child, required this.uiScale, super.key});

  final Widget child;
  final UiScaleState uiScale;

  @override
  Widget build(BuildContext context) => Focus(
    autofocus: true,
    onKeyEvent: (_, event) {
      if (event is! KeyDownEvent || !HardwareKeyboard.instance.isControlPressed) return KeyEventResult.ignored;
      if (event.logicalKey == LogicalKeyboardKey.numpadAdd || event.logicalKey == LogicalKeyboardKey.equal) { uiScale.increase(); return KeyEventResult.handled; }
      if (event.logicalKey == LogicalKeyboardKey.numpadSubtract || event.logicalKey == LogicalKeyboardKey.minus) { uiScale.decrease(); return KeyEventResult.handled; }
      if (event.logicalKey == LogicalKeyboardKey.digit0 || event.logicalKey == LogicalKeyboardKey.numpad0) { uiScale.reset(); return KeyEventResult.handled; }
      return KeyEventResult.ignored;
    },
    child: Listener(
      onPointerSignal: (signal) { if (signal is PointerScrollEvent && HardwareKeyboard.instance.isControlPressed) { if (signal.scrollDelta.dy < 0) { uiScale.increase(); } else if (signal.scrollDelta.dy > 0) { uiScale.decrease(); } } },
      child: CallbackShortcuts(bindings: {
        const SingleActivator(LogicalKeyboardKey.escape): () { final navigator = Navigator.of(context); if (navigator.canPop()) navigator.maybePop(); },
      }, child: child),
    ),
  );
}
