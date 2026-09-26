import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/core/app_message.dart';
import 'package:tailoring_system/core/theme/app_theme.dart';

void main() {
  testWidgets('success and error messages use white text in dark mode',
      (tester) async {
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.dark(),
      home: Builder(
        builder: (context) => Scaffold(
          body: Column(
            children: [
              TextButton(
                onPressed: () => AppMessage.show(
                  context,
                  'رسالة نجاح',
                  type: AppMessageType.success,
                ),
                child: const Text('إظهار نجاح'),
              ),
              TextButton(
                onPressed: () => AppMessage.show(
                  context,
                  'رسالة خطأ',
                  type: AppMessageType.error,
                ),
                child: const Text('إظهار خطأ'),
              ),
            ],
          ),
        ),
      ),
    ));

    await tester.tap(find.text('إظهار نجاح'));
    await tester.pump();
    expect(tester.widget<SnackBar>(find.byType(SnackBar)).backgroundColor,
        const Color(0xFF167C4B));
    expect(tester.widget<Text>(find.text('رسالة نجاح')).style?.color,
        Colors.white);

    await tester.tap(find.text('إظهار خطأ'));
    await tester.pump();
    expect(tester.widget<SnackBar>(find.byType(SnackBar)).backgroundColor,
      const Color(0xFFC62828));
    expect(tester.widget<Text>(find.text('رسالة خطأ')).style?.color,
      Colors.white);
  });
}