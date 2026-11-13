import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/settings/test_data_reset_screen.dart';

void main() {
  testWidgets('structured reset failure metadata is shown after confirmation', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: TestDataResetScreen(
          resetExecutor: () async {
            return {
              'success': false,
              'message': 'تعذر إكمال تنظيف بيانات الاختبار.',
              'failureStage': 'Verify',
              'failureCode': 'VERIFY_FAILED',
              'correlationId': 'RESET-TEST-123',
              'details': 'تفاصيل تحقق آمنة',
              'timestampUtc': '2026-11-08T00:00:00Z',
            };
          },
        ),
      ),
    );

    await tester.tap(find.byKey(const ValueKey('reset_primary_button')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('confirm_reset_button')));
    await tester.pumpAndSettle();

    expect(find.textContaining('Verify'), findsOneWidget);
    expect(find.textContaining('VERIFY_FAILED'), findsOneWidget);
    expect(find.textContaining('RESET-TEST-123'), findsOneWidget);
    expect(find.textContaining('تفاصيل تحقق آمنة'), findsOneWidget);
  });

  testWidgets('readiness-only action calls the safe GET endpoint without executing reset', (tester) async {
    var callCount = 0;

    await tester.pumpWidget(
      MaterialApp(
        home: TestDataResetScreen(
          readinessExecutor: () async {
            callCount += 1;
            return {
              'success': true,
              'message': 'جاهزية التنظيف موثقة.',
              'configuredTimeoutSeconds': 180,
              'liveMode': true,
              'testOverride': false,
            };
          },
        ),
      ),
    );

    await tester.tap(find.byKey(const ValueKey('readiness_check_button')));
    await tester.pumpAndSettle();

    expect(callCount, 1);
    expect(find.textContaining('جاهزية التنظيف موثقة.'), findsWidgets);
    expect(find.textContaining('ConfiguredTimeoutSeconds=180'), findsOneWidget);
    expect(find.textContaining('LiveMode=true'), findsOneWidget);
    expect(find.textContaining('TestOverride=false'), findsOneWidget);
  });

  testWidgets('long error content is scrollable without overflow', (tester) async {
    final longDetail = 'تفاصيل تحقق آمنة ' * 30;

    await tester.pumpWidget(
      MaterialApp(
        home: TestDataResetScreen(
          resetExecutor: () async {
            return {
              'success': false,
              'message': 'تعذر إكمال تنظيف بيانات الاختبار.',
              'failureStage': 'Verify',
              'failureCode': 'VERIFY_FAILED',
              'correlationId': 'RESET-TEST-123',
              'details': longDetail,
              'timestampUtc': '2026-11-08T00:00:00Z',
            };
          },
        ),
      ),
    );

    await tester.tap(find.byKey(const ValueKey('reset_primary_button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('confirm_reset_button')));
    await tester.pumpAndSettle();

    expect(find.byType(SingleChildScrollView), findsWidgets);
    expect(tester.takeException(), isNull);
  });

  testWidgets('repeated confirmation tap only triggers one reset execution', (tester) async {
    var count = 0;

    await tester.pumpWidget(
      MaterialApp(
        home: TestDataResetScreen(
          resetExecutor: () async {
            count += 1;
            await Future<void>.delayed(const Duration(milliseconds: 200));
            return {
              'success': false,
              'message': 'تعذر إكمال تنظيف بيانات الاختبار.',
              'failureStage': 'Verify',
              'failureCode': 'VERIFY_FAILED',
              'correlationId': 'RESET-TEST-123',
              'details': 'تفاصيل تحقق آمنة',
              'timestampUtc': '2026-11-08T00:00:00Z',
            };
          },
        ),
      ),
    );

    await tester.tap(find.byKey(const ValueKey('reset_primary_button')));
    await tester.pumpAndSettle();

    final confirm = find.byKey(const ValueKey('confirm_reset_button'));
    await tester.tap(confirm);
    await tester.pump();
    await tester.tap(confirm);
    await tester.pumpAndSettle();

    expect(count, 1);
  });

  testWidgets('success path shows the success message without requiring a single exact widget count', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: TestDataResetScreen(
          resetExecutor: () async {
            return {
              'success': true,
              'verified': true,
              'message': 'تم تنظيف بيانات الاختبار بنجاح.',
            };
          },
        ),
      ),
    );

    await tester.tap(find.byKey(const ValueKey('reset_primary_button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('confirm_reset_button')));
    await tester.pumpAndSettle();

    expect(find.textContaining('تم تنظيف بيانات الاختبار بنجاح.'), findsWidgets);
  });
}
