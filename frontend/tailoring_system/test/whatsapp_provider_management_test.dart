import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:tailoring_system/core/authenticated_api_client.dart';
import 'package:tailoring_system/screens/settings/whatsapp_provider_management_screen.dart';
import 'package:tailoring_system/services/auth_state.dart';

class ProviderApi extends AuthenticatedApiClient {
  ProviderApi() : super(auth: AuthState());
  String provider = 'Meta';
  String state = 'NotLinked';
  bool healthy = true;
  bool expired = false;
  bool rejectActivation = false;
  String? lastError;
  final List<String> writes = [];

  @override
  Future<Map<String, dynamic>> getObject(String path) async => switch (path) {
        '/whatsapp-provider' => {'activeProvider': provider},
        '/settings/whatsapp-connection' => {
            'isConfigured': true,
            'enabled': true
          },
        '/local-whatsapp/health' => {'healthy': healthy, 'contractVersion': 1},
        '/local-whatsapp/session/status' => {
            'state': state,
            'linked': ['Connected', 'Disconnected'].contains(state),
            'currentNumber': state == 'Connected' ? '967777123456' : null,
            'lastError': lastError
          },
        '/local-whatsapp/session/link-material' => {
            'kind': 'qr',
            'value': 'synthetic-test-material',
            'expiresAt': DateTime.now()
                .toUtc()
                .add(Duration(seconds: expired ? -1 : 20))
                .toIso8601String()
          },
        _ => throw StateError('Unexpected route'),
      };

  @override
  Future<Map<String, dynamic>> postObject(
      String path, Map<String, dynamic> body) async {
    writes.add(path);
    state = path.endsWith('/link') || path.endsWith('/replace')
        ? 'Linking'
        : path.endsWith('/reconnect')
            ? 'Connected'
            : path.endsWith('/logout')
                ? 'LoggedOut'
                : 'Disconnected';
    return {};
  }

  @override
  Future<Map<String, dynamic>> putObject(
      String path, Map<String, dynamic> body) async {
    writes.add(path);
    if (rejectActivation) {
      throw const ApiClientException(
          '/whatsapp-provider', 409, 'unsafe raw details');
    }
    provider = body['provider'] as String;
    return {'activeProvider': provider};
  }
}

void main() {
  Future<void> open(WidgetTester tester, ProviderApi api) async {
    tester.view.physicalSize = const Size(1100, 1100);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
        MaterialApp(home: WhatsAppProviderManagementScreen(api: api)));
    await tester.pumpAndSettle();
  }

  testWidgets(
      'manual activation is exclusive; rejection does not change provider',
      (tester) async {
    final api = ProviderApi()..state = 'Connected';
    await open(tester, api);
    await tester.tap(find.byKey(const ValueKey('activate-Local')));
    await tester.pumpAndSettle();
    expect(api.writes, isEmpty);
    await tester.tap(find.text('إلغاء'));
    await tester.pumpAndSettle();
    expect(api.provider, 'Meta');
    api.rejectActivation = true;
    await tester.tap(find.byKey(const ValueKey('activate-Local')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('تأكيد'));
    await tester.pumpAndSettle();
    expect(api.provider, 'Meta');
    expect(find.textContaining('unsafe raw'), findsNothing);
    api.rejectActivation = false;
    await tester.tap(find.byKey(const ValueKey('activate-Local')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('تأكيد'));
    await tester.pumpAndSettle();
    expect(api.provider, 'Local');
    expect(
        tester
            .widget<SwitchListTile>(find.byKey(const ValueKey('activate-Meta')))
            .value,
        false);
    await tester.tap(find.byKey(const ValueKey('activate-Local')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('تأكيد'));
    await tester.pumpAndSettle();
    expect(api.provider, 'None');
    expect(api.writes.every((path) => path == '/whatsapp-provider'), true);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets(
      'disconnect reconnect logout and replacement preserve activation and require confirmation',
      (tester) async {
    final api = ProviderApi()
      ..state = 'Connected'
      ..provider = 'Local';
    await open(tester, api);
    await tester.tap(find.byKey(const ValueKey('session-disconnect')));
    await tester.pumpAndSettle();
    expect(api.writes, isEmpty);
    await tester.tap(find.text('تأكيد'));
    await tester.pumpAndSettle();
    expect(api.state, 'Disconnected');
    expect(api.provider, 'Local');
    expect(find.textContaining('لن يتحول الإرسال تلقائيًا'), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('session-reconnect')));
    await tester.pumpAndSettle();
    expect(api.state, 'Connected');
    await tester.tap(find.byKey(const ValueKey('session-logout')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('إلغاء'));
    await tester.pumpAndSettle();
    expect(api.state, 'Connected');
    await tester.tap(find.byKey(const ValueKey('session-replace')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('تأكيد'));
    await tester.pumpAndSettle();
    expect(api.state, 'Linking');
    expect(find.byType(QrImageView), findsOneWidget);
    expect(api.provider, 'Local');
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets(
      'expired QR and backgrounded window do not retain pairing material',
      (tester) async {
    final api = ProviderApi();
    await open(tester, api);
    await tester.tap(find.byKey(const ValueKey('session-link')));
    await tester.pumpAndSettle();
    expect(find.byType(QrImageView), findsOneWidget);
    api.expired = true;
    await tester.tap(find.byTooltip('تحديث الحالة'));
    await tester.pumpAndSettle();
    expect(find.byType(QrImageView), findsNothing);
    api.expired = false;
    await tester.tap(find.byTooltip('تحديث الحالة'));
    await tester.pumpAndSettle();
    expect(find.byType(QrImageView), findsOneWidget);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    await tester.pump();
    expect(find.byType(QrImageView), findsNothing);
    await tester.pumpWidget(const SizedBox.shrink());
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
  });

  testWidgets(
      'unready Local cannot be activated and unknown errors are sanitized',
      (tester) async {
    final api = ProviderApi()
      ..provider = 'Local'
      ..state = 'Disconnected'
      ..healthy = false
      ..lastError = 'secret-token';
    await open(tester, api);
    expect(find.text('secret-token'), findsNothing);
    expect(find.textContaining('لن يتحول الإرسال تلقائيًا'), findsOneWidget);
    api.provider = 'Meta';
    await tester.tap(find.byTooltip('تحديث الحالة'));
    await tester.pumpAndSettle();
    expect(
        tester
            .widget<SwitchListTile>(
                find.byKey(const ValueKey('activate-Local')))
            .onChanged,
        isNull);
    expect(api.writes, isEmpty);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  for (final brightness in Brightness.values) {
    testWidgets('connection and activation remain separate in $brightness',
        (tester) async {
      tester.view.physicalSize = const Size(1100, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = ProviderApi()..state = 'Connected';
      await tester.pumpWidget(MaterialApp(
          theme: ThemeData(brightness: brightness),
          home: WhatsAppProviderManagementScreen(api: api)));
      await tester.pumpAndSettle();
      expect(find.text('967777123456'), findsOneWidget);
      expect(
          tester
              .widget<SwitchListTile>(
                  find.byKey(const ValueKey('activate-Local')))
              .value,
          false);
      expect(
          tester
              .widget<SwitchListTile>(
                  find.byKey(const ValueKey('activate-Meta')))
              .value,
          true);
      expect(api.writes, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    });
  }

  testWidgets(
      'link shows temporary QR then clears it on connection without activation',
      (tester) async {
    tester.view.physicalSize = const Size(1100, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final api = ProviderApi();
    await tester.pumpWidget(
        MaterialApp(home: WhatsAppProviderManagementScreen(api: api)));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('session-link')));
    await tester.pumpAndSettle();
    expect(find.byType(QrImageView), findsOneWidget);
    api.state = 'Connected';
    await tester.pump(const Duration(seconds: 3));
    await tester.pumpAndSettle();
    expect(find.byType(QrImageView), findsNothing);
    expect(find.text('967777123456'), findsOneWidget);
    expect(api.provider, 'Meta');
    expect(api.writes, ['/local-whatsapp/session/link']);
    await tester.pumpWidget(const SizedBox.shrink());
  });
}
