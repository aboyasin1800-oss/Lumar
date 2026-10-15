import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/supplier_purchasing_operations_screen.dart';
import 'package:tailoring_system/services/auth_state.dart';

class _LineLifecycleProbe extends StatefulWidget {
  const _LineLifecycleProbe();

  @override
  State<_LineLifecycleProbe> createState() => _LineLifecycleProbeState();
}

class _LineLifecycleProbeState extends State<_LineLifecycleProbe> {
  final lines = <TextEditingController>[
    TextEditingController(),
    TextEditingController(),
  ];

  @override
  void dispose() {
    for (final line in lines) {
      line.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: Center(
          child: ElevatedButton(
            onPressed: () => showDialog<void>(
              context: context,
              builder: (dialogContext) => StatefulBuilder(
                builder: (context, setDialogState) => AlertDialog(
                  title: const Text('اختبار دورة الحياة'),
                  content: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      for (var index = 0; index < lines.length; index++)
                        TextField(controller: lines[index]),
                    ],
                  ),
                  actions: [
                    TextButton(
                      onPressed: () => setDialogState(() {
                        lines.removeAt(0);
                      }),
                      child: const Text('حذف السطر'),
                    ),
                  ],
                ),
              ),
            ),
            child: const Text('فتح الحوار'),
          ),
        ),
      ),
    );
  }
}

class _InvoiceSaveProbe extends StatefulWidget {
  const _InvoiceSaveProbe();

  @override
  State<_InvoiceSaveProbe> createState() => _InvoiceSaveProbeState();
}

class _InvoiceSaveProbeState extends State<_InvoiceSaveProbe> {
  final controller = TextEditingController();
  bool _busy = false;
  int saveCount = 0;

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: Center(
          child: ElevatedButton(
            onPressed: () => showDialog<void>(
              context: context,
              builder: (dialogContext) => StatefulBuilder(
                builder: (context, setDialogState) => AlertDialog(
                  title: const Text('فاتورة مورد جديدة'),
                  content: SizedBox(
                    width: 420,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        TextField(
                          controller: controller,
                          decoration: const InputDecoration(
                            labelText: 'رقم الفاتورة',
                          ),
                        ),
                      ],
                    ),
                  ),
                  actions: [
                    FilledButton(
                      onPressed: _busy
                          ? null
                          : () {
                              setDialogState(() => _busy = true);
                              setState(() => saveCount += 1);
                              Future<void>.microtask(() {
                                if (mounted) {
                                  setState(() => _busy = false);
                                }
                                if (Navigator.of(dialogContext).mounted) {
                                  Navigator.of(dialogContext).pop();
                                }
                              });
                            },
                      child: const Text('حفظ الفاتورة'),
                    ),
                  ],
                ),
              ),
            ),
            child: const Text('فتح الحوار'),
          ),
        ),
      ),
    );
  }
}

void main() {
  testWidgets('supplier purchasing operations screen renders and handles load failure without crashing', (tester) async {
    final auth = AuthState(baseUrl: 'http://127.0.0.1:1');

    await tester.pumpWidget(
      MaterialApp(
        home: SupplierPurchasingOperationsScreen(auth: auth),
      ),
    );

    await tester.pumpAndSettle(const Duration(milliseconds: 400));

    expect(find.text('الموردون والمشتريات'), findsOneWidget);
  });

  testWidgets('removing a dialog row should not dispose active text controllers while the dialog is still mounted', (tester) async {
    await tester.pumpWidget(MaterialApp(home: const _LineLifecycleProbe()));
    await tester.tap(find.text('فتح الحوار'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('حذف السطر'));
    await tester.pump();

    expect(tester.takeException(), isNull);
  });

  testWidgets('single-line invoice dialog saves once without framework assertion', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: _InvoiceSaveProbe()));
    await tester.tap(find.text('فتح الحوار'));
    await tester.pumpAndSettle();

    expect(find.text('حفظ الفاتورة'), findsOneWidget);
    expect(find.byType(TextField), findsOneWidget);

    await tester.tap(find.text('حفظ الفاتورة'));
    await tester.pump();
    await tester.pump();

    final state = tester.state(find.byType(_InvoiceSaveProbe)) as _InvoiceSaveProbeState;
    expect(state.saveCount, 1);
    expect(tester.takeException(), isNull);
  });

  test('fabric invoice itemCount remains nullable when blank and rejects invalid numeric text', () {
    expect(SupplierPurchasingOperationsScreen.parseOptionalDecimal(''), isNull);
    expect(SupplierPurchasingOperationsScreen.parseOptionalDecimal('12.5'), 12.5);
    expect(
      () => SupplierPurchasingOperationsScreen.parseOptionalDecimal('abc'),
      throwsFormatException,
    );
  });
}
