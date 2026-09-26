import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:tailoring_system/core/app_navigation.dart';

void main() {
  testWidgets('pops the nested page before using a root navigation fallback',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        navigatorKey: AppNavigation.navigatorKey,
        navigatorObservers: [AppNavigation.observer],
        home: Builder(
          builder: (context) => FilledButton(
            onPressed: () => AppNavigation.push<void>(
              context,
              (_) => const Scaffold(body: Center(child: Text('تفاصيل'))),
            ),
            child: const Text('فتح التفاصيل'),
          ),
        ),
      ),
    );

    await tester.tap(find.text('فتح التفاصيل'));
    await tester.pumpAndSettle();

    expect(find.text('تفاصيل'), findsOneWidget);
    expect(AppNavigation.popIfPossible(), isTrue);

    await tester.pumpAndSettle();

    expect(find.text('تفاصيل'), findsNothing);
    expect(AppNavigation.popIfPossible(), isFalse);
  });
}