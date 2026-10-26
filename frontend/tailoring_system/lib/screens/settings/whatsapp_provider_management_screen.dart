import 'dart:async';

import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../core/app_navigation.dart';
import '../../core/authenticated_api_client.dart';
import '../../core/ui_palette.dart';
import '../../services/auth_state.dart';
import 'whatsapp_connection_settings_screen.dart';

class WhatsAppProviderManagementScreen extends StatefulWidget {
  const WhatsAppProviderManagementScreen({super.key, this.api});

  final AuthenticatedApiClient? api;

  @override
  State<WhatsAppProviderManagementScreen> createState() =>
      _WhatsAppProviderManagementScreenState();
}

class _WhatsAppProviderManagementScreenState
    extends State<WhatsAppProviderManagementScreen>
    with WidgetsBindingObserver {
  late final AuthenticatedApiClient _api =
      widget.api ?? AuthenticatedApiClient(auth: AuthState.instance);
  Timer? _pollTimer;
  Timer? _expiryTimer;
  Future<void>? _refreshFuture;
  bool _visible = true;
  bool _busy = false;
  bool _refreshing = false;
  bool _pairingRequested = false;
  bool? _healthy;
  bool? _metaReady;
  bool _linked = false;
  String? _provider;
  String _session = 'Unavailable';
  String? _number;
  String? _qr;
  String? _error;
  String? _providerError;
  String? _localError;
  String? _metaError;

  static const _sessionNames = <String, String>{
    'NotLinked': 'غير مرتبط',
    'Linking': 'جارٍ الربط',
    'Connected': 'متصل',
    'Disconnected': 'غير متصل',
    'Reconnecting': 'جارٍ إعادة الاتصال',
    'LoggedOut': 'تم إلغاء الربط',
    'Error': 'تعذر الاتصال',
    'Unavailable': 'الحالة غير متاحة',
  };
  static const _safeErrors = <String, String>{
    'SESSION_STORAGE_ERROR': 'تعذر الوصول إلى مخزن الجلسة.',
    'CURRENT_NUMBER_UNCONFIRMED': 'لم يتم تأكيد رقم الحساب.',
    'SESSION_LOGGED_OUT': 'تم إلغاء ربط الحساب.',
    'CONNECTION_LOST': 'انقطع الاتصال بواتساب.',
    'SESSION_CONNECTION_FAILED': 'تعذر إكمال اتصال واتساب.',
    'SEND_UNCONFIRMED': 'نتيجة الإرسال غير مؤكدة؛ لا تعاد الرسالة تلقائيًا.',
    'GATEWAY_ERROR': 'تعذر تنفيذ العملية في خدمة واتساب المحلي.',
  };

  bool get _localReady =>
      _healthy == true && _session == 'Connected' && _linked && _number != null;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    unawaited(_refresh());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _pollTimer?.cancel();
    _expiryTimer?.cancel();
    _qr = null;
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _visible = state == AppLifecycleState.resumed;
    if (!_visible) {
      _pollTimer?.cancel();
      _pairingRequested = false;
      if (mounted) setState(_clearQr);
    } else {
      unawaited(_refresh());
    }
  }

  void _clearQr() {
    _expiryTimer?.cancel();
    _qr = null;
  }

  void _schedule() {
    _pollTimer?.cancel();
    if (!mounted || !_visible || _busy) return;
    _pollTimer = Timer(
      Duration(
          seconds: _pairingRequested || _session == 'Reconnecting' ? 2 : 10),
      () => unawaited(_refresh()),
    );
  }

  Future<void> _refresh() {
    if (_refreshFuture != null) return _refreshFuture!;
    if (!mounted || !_visible) return Future.value();
    final future = _load();
    _refreshFuture = future;
    return future.whenComplete(() {
      _refreshFuture = null;
      _schedule();
    });
  }

  Future<void> _fetch(String path, void Function(Map<String, dynamic>) apply,
      void Function() failed) async {
    try {
      final value =
          await _api.getObject(path).timeout(const Duration(seconds: 15));
      if (mounted && _visible) setState(() => apply(value));
    } catch (_) {
      if (mounted && _visible) setState(failed);
    }
  }

  Future<void> _load() async {
    setState(() => _refreshing = true);
    await Future.wait([
      _fetch('/whatsapp-provider', (value) {
        final provider = value['activeProvider'];
        _provider = ['Meta', 'Local', 'None'].contains(provider)
            ? provider as String
            : null;
        _providerError =
            _provider == null ? 'تعذر تأكيد مزود الإرسال النشط.' : null;
      }, () {
        _provider = null;
        _providerError =
            'تعذر قراءة مزود الإرسال. تحقق من الاتصال وصلاحيات الإدارة.';
      }),
      _fetch('/settings/whatsapp-connection', (value) {
        _metaReady = value['isConfigured'] == true && value['enabled'] == true;
        _metaError = null;
      }, () {
        _metaReady = null;
        _metaError = 'تعذر قراءة جاهزية اتصال ميتا.';
      }),
      _fetch('/local-whatsapp/health', (value) {
        _healthy = value['healthy'] == true && value['contractVersion'] == 1;
      }, () => _healthy = false),
      _fetch('/local-whatsapp/session/status', (value) {
        final state = value['state']?.toString();
        _session = _sessionNames.containsKey(state) ? state! : 'Unavailable';
        _linked = value['linked'] == true;
        final number = value['currentNumber']?.toString();
        _number = _session == 'Connected' &&
                number != null &&
                RegExp(r'^[1-9]\d{7,14}$').hasMatch(number)
            ? number
            : null;
        final code = value['lastError']?.toString();
        _localError = code == null
            ? null
            : _safeErrors[code] ?? 'تعذر تأكيد حالة خدمة واتساب.';
        if (_session != 'Linking') {
          _clearQr();
          if (_session == 'Connected') _pairingRequested = false;
        }
      }, () {
        _session = 'Unavailable';
        _number = null;
        _localError = 'تعذر الوصول إلى خدمة واتساب المحلي.';
        _clearQr();
      }),
    ]);
    if (!mounted) return;
    if (_visible && _pairingRequested && _session == 'Linking') {
      await _fetch('/local-whatsapp/session/link-material', (value) {
        final expiry =
            DateTime.tryParse(value['expiresAt']?.toString() ?? '')?.toUtc();
        final material = value['value'];
        final now = DateTime.now().toUtc();
        _clearQr();
        if (value['kind'] == 'qr' &&
            material is String &&
            material.isNotEmpty &&
            material.length <= 4096 &&
            expiry != null &&
            expiry.isAfter(now) &&
            expiry.difference(now) <= const Duration(minutes: 1)) {
          _qr = material;
          _expiryTimer = Timer(expiry.difference(now), () {
            if (mounted) setState(_clearQr);
          });
        }
      }, _clearQr);
    }
    if (mounted) setState(() => _refreshing = false);
  }

  Future<bool> _confirm(String title, String message) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => Directionality(
          textDirection: TextDirection.rtl,
          child: AlertDialog(
            title: Text(title),
            content: Text(message),
            actions: [
              TextButton(
                  onPressed: () => Navigator.pop(context, false),
                  child: const Text('إلغاء')),
              FilledButton(
                  onPressed: () => Navigator.pop(context, true),
                  child: const Text('تأكيد')),
            ],
          ),
        ),
      ) ??
      false;

  Future<void> _run(Future<void> Function() operation) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
      _clearQr();
    });
    _pollTimer?.cancel();
    try {
      await _refreshFuture;
      if (!mounted || !_visible) return;
      await operation();
    } on ApiClientException catch (error) {
      if (mounted) {
        setState(() => _error = error.statusCode == 409
            ? 'تعذر تنفيذ العملية: تحقق من جاهزية الجلسة أو انتظر انتهاء العملية الجارية. لم يُعتمد تغيير.'
            : error.statusCode == 401 || error.statusCode == 403
                ? 'يلزم تسجيل الدخول بحساب مسؤول نظام نشط.'
                : 'تعذر إكمال الطلب. تحقق من الحالة قبل إعادة المحاولة.');
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error =
            'لم تتأكد نتيجة العملية. تحقق من الحالة قبل إعادة المحاولة.');
      }
    } finally {
      if (mounted) {
        await _refresh();
        if (mounted) {
          setState(() => _busy = false);
        }
        _schedule();
      }
    }
  }

  Future<void> _activate(String provider, bool enabled) async {
    if (_busy || _provider == null) return;
    final target = enabled ? provider : 'None';
    final name = provider == 'Local' ? 'واتساب المحلي' : 'واتساب ميتا';
    if (!await _confirm(
        enabled ? 'تفعيل $name للإرسال' : 'إيقاف إرسال واتساب',
        enabled
            ? 'ستوجّه رسائل واتساب الجديدة إلى $name وحده. لن تتغير قنوات الرسائل الموجودة.'
            : 'سيتوقف إنشاء رسائل واتساب الجديدة وتستمر إشعارات التطبيق. لن يُلغى ربط الحساب.')) {
      return;
    }
    if (!mounted) return;
    await _run(() async {
      final result = await _api.putObject('/whatsapp-provider',
          {'provider': target}).timeout(const Duration(seconds: 70));
      if (mounted) {
        setState(() => _provider = result['activeProvider']?.toString());
      }
    });
  }

  Future<void> _sessionAction(String operation) async {
    if (_busy) return;
    final confirmation = switch (operation) {
      'logout' => (
          'إلغاء ربط الحساب',
          'ستنتهي الجلسة ويلزم رمز ربط جديد. لن يتغير مزود الإرسال النشط تلقائيًا.'
        ),
      'replace' => (
          'استبدال رقم واتساب',
          'سيُلغى ربط الحساب الحالي ويظهر رمز لربط حساب جديد. لن يتغير مزود الإرسال النشط تلقائيًا.'
        ),
      'disconnect' => (
          'فصل الاتصال',
          'سيتوقف الاتصال مع الاحتفاظ بالجلسة لإعادة الاتصال. لن يتغير مزود الإرسال النشط.'
        ),
      _ => null,
    };
    if (confirmation != null &&
        !await _confirm(confirmation.$1, confirmation.$2)) {
      return;
    }
    if (!mounted) return;
    await _run(() async {
      await _api.postObject('/local-whatsapp/session/$operation', {}).timeout(
          const Duration(seconds: 40));
      _pairingRequested = operation == 'link' || operation == 'replace';
    });
  }

  Widget _status(String title, String value, {Key? key}) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 7),
        child: Wrap(spacing: 12, runSpacing: 4, children: [
          Text('$title:', style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(value, key: key),
        ]),
      );

  Widget _warning(String? message) => message == null
      ? const SizedBox.shrink()
      : Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Text(message,
              style: TextStyle(color: Theme.of(context).colorScheme.error)));

  Widget _activation(String provider, {required bool canActivate}) =>
      SwitchListTile.adaptive(
        key: ValueKey('activate-$provider'),
        contentPadding: EdgeInsets.zero,
        title: const Text('مفعل للإرسال'),
        subtitle: Text(_provider == null
            ? 'لم تُؤكد حالة التفعيل'
            : _provider == provider
                ? 'مفعل للإرسال'
                : 'غير مفعل للإرسال'),
        value: _provider == provider,
        activeTrackColor: UiPalette.primaryBorder,
        onChanged: _busy ||
                _provider == null ||
                (!canActivate && _provider != provider)
            ? null
            : (value) => unawaited(_activate(provider, value)),
      );

  Widget _command(
          String label, IconData icon, String operation, bool enabled) =>
      OutlinedButton.icon(
        key: ValueKey('session-$operation'),
        onPressed: !_busy && enabled
            ? () => unawaited(_sessionAction(operation))
            : null,
        icon: Icon(icon, size: 18),
        label: Text(label),
      );

  @override
  Widget build(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    final background =
        dark ? UiPalette.screenBackground : UiPalette.lightBackground;
    final foreground = UiPalette.adaptiveTextColor(background);
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: background,
        appBar: AppBar(
          title: const Text('إدارة واتساب'),
          backgroundColor: background,
          foregroundColor: foreground,
          actions: [
            IconButton(
                tooltip: 'تحديث الحالة',
                onPressed:
                    _busy || _refreshing ? null : () => unawaited(_refresh()),
                icon: const Icon(Icons.refresh))
          ],
        ),
        body: DefaultTextStyle.merge(
          style: TextStyle(color: foreground),
          child: ListView(padding: const EdgeInsets.all(24), children: [
            SizedBox(
                height: 4,
                child: _busy || _refreshing
                    ? const LinearProgressIndicator(color: UiPalette.primary)
                    : null),
            _warning(_error),
            _warning(_providerError),
            if (_provider == 'None')
              _status('إرسال واتساب', 'غير مفعل؛ إشعارات التطبيق مستقلة'),
            if (_provider == 'Local' && !_localReady)
              _warning(
                  'واتساب المحلي مفعل للإرسال لكنه غير جاهز حاليًا. لن يتحول الإرسال تلقائيًا إلى ميتا.'),
            const SizedBox(height: 16),
            const Text('واتساب الرسمي عبر ميتا',
                style: TextStyle(fontSize: 21, fontWeight: FontWeight.w700)),
            _status(
                'جاهزية الاتصال',
                _metaReady == null
                    ? 'غير مؤكدة'
                    : _metaReady!
                        ? 'الإعدادات جاهزة'
                        : 'الإعدادات غير جاهزة'),
            _warning(_metaError),
            _activation('Meta', canActivate: true),
            Align(
                alignment: AlignmentDirectional.centerStart,
                child: TextButton.icon(
                  onPressed: _busy
                      ? null
                      : () async {
                          _visible = false;
                          _pollTimer?.cancel();
                          setState(() {
                            _pairingRequested = false;
                            _clearQr();
                          });
                          await AppNavigation.push(context,
                              (_) => const WhatsAppConnectionSettingsScreen());
                          _visible = true;
                          if (mounted) await _refresh();
                        },
                  icon: const Icon(Icons.settings_outlined),
                  label: const Text('إعدادات اتصال ميتا'),
                )),
            const Divider(height: 40),
            const Text('واتساب المحلي',
                style: TextStyle(fontSize: 21, fontWeight: FontWeight.w700)),
            _status(
                'حالة خدمة القناة',
                _healthy == null
                    ? 'جارٍ التحقق'
                    : _healthy!
                        ? 'تعمل'
                        : 'غير متاحة'),
            _status('حالة الجلسة', _sessionNames[_session]!),
            _status('الرقم الحالي', _number ?? 'غير مؤكد',
                key: const ValueKey('current-number')),
            _warning(_localError),
            _activation('Local', canActivate: _localReady),
            Wrap(spacing: 10, runSpacing: 10, children: [
              _command(
                  'ربط حساب',
                  Icons.link,
                  'link',
                  _healthy == true &&
                      !_linked &&
                      ['NotLinked', 'LoggedOut', 'Error'].contains(_session)),
              _command(
                  'فصل الاتصال',
                  Icons.pause_circle_outline,
                  'disconnect',
                  _healthy == true &&
                      ['Connected', 'Linking', 'Reconnecting', 'Error']
                          .contains(_session)),
              _command('إعادة الاتصال', Icons.sync, 'reconnect',
                  _healthy == true && _linked && _session != 'Connected'),
              _command('إلغاء الربط', Icons.link_off, 'logout', _localReady),
              _command(
                  'استبدال الرقم', Icons.swap_horiz, 'replace', _localReady),
              OutlinedButton.icon(
                  onPressed:
                      _busy || _refreshing ? null : () => unawaited(_refresh()),
                  icon: const Icon(Icons.wifi_find),
                  label: const Text('اختبار الاتصال')),
              if (_session == 'Linking' && !_pairingRequested)
                OutlinedButton.icon(
                    onPressed: _busy
                        ? null
                        : () {
                            setState(() => _pairingRequested = true);
                            unawaited(_refresh());
                          },
                    icon: const Icon(Icons.qr_code),
                    label: const Text('عرض رمز الربط')),
            ]),
            if (_pairingRequested && _session == 'Linking') ...[
              const SizedBox(height: 20),
              Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: SizedBox(
                    width: 280,
                    height: 280,
                    child: _qr == null
                        ? const Center(child: Text('بانتظار رمز ربط صالح'))
                        : QrImageView(
                            data: _qr!,
                            size: 280,
                            padding: const EdgeInsets.all(20),
                            backgroundColor: Colors.white,
                            semanticsLabel: 'رمز ربط واتساب المؤقت',
                            errorStateBuilder: (context, error) => const Center(
                                child: Text('تعذر عرض رمز الربط'))),
                  )),
            ],
          ]),
        ),
      ),
    );
  }
}
