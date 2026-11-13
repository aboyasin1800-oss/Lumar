import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:tailoring_system/core/authenticated_api_client.dart';
import 'package:tailoring_system/services/auth_state.dart';

class TestDataResetScreen extends StatefulWidget {
  const TestDataResetScreen({
    super.key,
    this.client,
    this.baseUrl,
    this.resetExecutor,
    this.readinessExecutor,
    this.apiClient,
  });

  final http.Client? client;
  final String? baseUrl;
  final Future<Map<String, dynamic>> Function()? resetExecutor;
  final Future<Map<String, dynamic>> Function()? readinessExecutor;
  final AuthenticatedApiClient? apiClient;

  @override
  State<TestDataResetScreen> createState() => _TestDataResetScreenState();
}

class _TestDataResetScreenState extends State<TestDataResetScreen> {
  String get _baseUrl => widget.baseUrl ?? const String.fromEnvironment(
        'LUMAR_API_URL',
        defaultValue: 'http://127.0.0.1:5093',
      );

  bool _busy = false;
  String? _statusMessage;

  AuthenticatedApiClient get _apiClient =>
      widget.apiClient ?? AuthenticatedApiClient(auth: AuthState.instance, baseUrl: _baseUrl);

  Future<void> _checkReadiness() async {
    if (_busy) {
      return;
    }

    setState(() {
      _busy = true;
      _statusMessage = 'جارٍ فحص جاهزية التنظيف...';
    });

    try {
      final Map<String, dynamic> map = widget.readinessExecutor != null
          ? await widget.readinessExecutor!()
          : await _apiClient.getObject('/settings/test-data-reset/readiness');

      final success = map['success'] == true;
      final message = map['message']?.toString() ??
          (success ? 'تم فحص جاهزية التنظيف بنجاح.' : 'تعذر فحص جاهزية التنظيف.');
      final configTimeout = map['configuredTimeoutSeconds'];
      final liveMode = map['liveMode'];
      final testOverride = map['testOverride'];
      final summary = [
        message,
        if (configTimeout != null) 'ConfiguredTimeoutSeconds=$configTimeout',
        if (liveMode != null) 'LiveMode=$liveMode',
        if (testOverride != null) 'TestOverride=$testOverride',
        if (map['databaseName'] != null) 'DatabaseName=${map['databaseName']}',
        if (map['scriptPath'] != null) 'ScriptPath=${map['scriptPath']}',
        if (map['manifestPath'] != null) 'ManifestPath=${map['manifestPath']}',
        if (map['dependencyGraphPath'] != null) 'DependencyGraphPath=${map['dependencyGraphPath']}',
        if (map['failureStage'] != null && map['failureStage'].toString().isNotEmpty) 'المرحلة: ${map['failureStage']}',
        if (map['failureCode'] != null && map['failureCode'].toString().isNotEmpty) 'FailureCode: ${map['failureCode']}',
        if (map['correlationId'] != null && map['correlationId'].toString().isNotEmpty) 'CorrelationId: ${map['correlationId']}',
        if (map['details'] != null && map['details'].toString().isNotEmpty) 'Details: ${map['details']}',
      ].join('\n');

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(success ? message : 'تعذر فحص جاهزية التنظيف.')),
      );
      setState(() => _statusMessage = summary);
    } on StructuredApiException catch (error) {
      if (!mounted) return;
      final safeMessage = [
        error.message,
        if (error.failureStage.isNotEmpty) 'المرحلة: ${error.failureStage}',
        if (error.failureCode.isNotEmpty) 'FailureCode: ${error.failureCode}',
        if (error.correlationId.isNotEmpty) 'CorrelationId: ${error.correlationId}',
        if (error.details.isNotEmpty) 'Details: ${error.details}',
      ].join('\n');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error.message)),
      );
      setState(() => _statusMessage = safeMessage);
    } catch (_) {
      if (!mounted) return;
      const failureMessage = 'تعذر فحص جاهزية التنظيف. لم يتم تنفيذ أي تعديل.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(failureMessage)),
      );
      setState(() => _statusMessage = failureMessage);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _confirmAndExecute() async {
    if (_busy) {
      return;
    }

    final shouldContinue = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => AlertDialog(
        title: const Text('تأكيد تنظيف بيانات الاختبار'),
        content: const Text(
          'سيتم حذف بيانات الاختبار التشغيلية وإعادة قاعدة التطوير إلى حالتها النظيفة المعتمدة.\n\nلن يتم حذف إعدادات النظام أو التعريفات أو القوالب أو بيانات الأساس المحمية.\n\nهل تريد المتابعة؟',
          textDirection: TextDirection.rtl,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton.icon(
            key: const ValueKey('confirm_reset_button'),
            onPressed: () => Navigator.of(context).pop(true),
            icon: const Icon(Icons.cleaning_services_rounded),
            label: const Text('تنظيف بيانات الاختبار'),
          ),
        ],
      ),
    );

    if (shouldContinue != true) {
      return;
    }

    await _executeReset();
  }

  void _logResetEvent(String eventName, {String? url, int? statusCode, String? errorType}) {
    final parts = <String>[
      eventName,
      if (url != null && url.isNotEmpty) 'RESET_POST_URL=$url',
      if (statusCode != null) 'RESET_POST_STATUS=$statusCode',
      if (errorType != null && errorType.isNotEmpty) 'RESET_POST_ERROR_TYPE=$errorType',
    ];
    debugPrint(parts.join(' | '));
  }

  Future<void> _executeReset() async {
    if (_busy) {
      return;
    }

    final url = '$_baseUrl/settings/test-data-reset';
    _logResetEvent('RESET_BUTTON_PRESSED');

    setState(() {
      _busy = true;
      _statusMessage = 'جارٍ تنظيف بيانات الاختبار...';
    });

    try {
      _logResetEvent('RESET_POST_STARTED', url: url);
      final Map<String, dynamic> map = widget.resetExecutor != null
          ? await widget.resetExecutor!()
          : await _apiClient.postObject('/settings/test-data-reset', {});
      _logResetEvent('RESET_POST_COMPLETED', url: url, statusCode: 200);

      final success = map['success'] == true;

      if (success) {
        final successMessage = map['message']?.toString() ?? 'تم تنظيف بيانات الاختبار بنجاح.';
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(successMessage)),
        );
        setState(() => _statusMessage = 'تم تنظيف بيانات الاختبار بنجاح.');
        return;
      }

      final failureMessage = map['message']?.toString() ??
          'تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.';
      final failureStage = map['failureStage']?.toString();
      final failureCode = map['failureCode']?.toString();
      final correlationId = map['correlationId']?.toString();
      final details = map['details']?.toString();
      final combinedMessage = [
        failureMessage,
        if (failureStage != null && failureStage.isNotEmpty) 'المرحلة: $failureStage',
        if (failureCode != null && failureCode.isNotEmpty) 'FailureCode: $failureCode',
        if (correlationId != null && correlationId.isNotEmpty) 'CorrelationId: $correlationId',
        if (details != null && details.isNotEmpty) 'Details: $details',
      ].join('\n');
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(failureMessage)),
      );
      setState(() => _statusMessage = combinedMessage);
    } on StructuredApiException catch (error) {
      _logResetEvent('RESET_POST_ERROR_TYPE', url: url, errorType: error.runtimeType.toString());
      final safeMessage = [
        error.message,
        if (error.failureStage.isNotEmpty) 'المرحلة: ${error.failureStage}',
        if (error.failureCode.isNotEmpty) 'FailureCode: ${error.failureCode}',
        if (error.correlationId.isNotEmpty) 'CorrelationId: ${error.correlationId}',
        if (error.details.isNotEmpty) 'Details: ${error.details}',
      ].join('\n');
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error.message)),
      );
      setState(() => _statusMessage = safeMessage);
    } catch (error) {
      _logResetEvent('RESET_POST_ERROR_TYPE', url: url, errorType: error.runtimeType.toString());
      if (!mounted) return;
      const failureMessage = 'تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(failureMessage)),
      );
      setState(() => _statusMessage = failureMessage);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('تنظيف بيانات الاختبار'),
        ),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 620),
                child: Card(
                  color: colorScheme.surface,
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.cleaning_services_rounded,
                          size: 56,
                          color: colorScheme.error,
                        ),
                        const SizedBox(height: 18),
                        Text(
                          'تنظيف بيانات الاختبار',
                          textAlign: TextAlign.center,
                          style: theme.textTheme.headlineSmall?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          'إعادة بيانات الاختبار التشغيلية إلى الحالة النظيفة المعتمدة مع الحفاظ على إعدادات وتعريفات النظام.',
                          textAlign: TextAlign.center,
                          style: theme.textTheme.bodyLarge?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                          ),
                        ),
                        const SizedBox(height: 24),
                        OutlinedButton.icon(
                          key: const ValueKey('readiness_check_button'),
                          onPressed: _busy ? null : _checkReadiness,
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 16),
                          ),
                          icon: const Icon(Icons.health_and_safety_rounded),
                          label: Text(
                            _busy ? 'جارٍ فحص جاهزية التنظيف...' : 'فحص جاهزية التنظيف',
                          ),
                        ),
                        const SizedBox(height: 12),
                        FilledButton.icon(
                          key: const ValueKey('reset_primary_button'),
                          onPressed: _busy ? null : _confirmAndExecute,
                          style: FilledButton.styleFrom(
                            backgroundColor: colorScheme.error,
                            foregroundColor: colorScheme.onError,
                            padding: const EdgeInsets.symmetric(vertical: 16),
                          ),
                          icon: const Icon(Icons.delete_forever_rounded),
                          label: Text(
                            _busy ? 'جارٍ تنظيف بيانات الاختبار...' : 'تنظيف بيانات الاختبار',
                          ),
                        ),
                        if (_statusMessage != null) ...[
                          const SizedBox(height: 20),
                          ConstrainedBox(
                            constraints: const BoxConstraints(maxHeight: 220),
                            child: Container(
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                color: colorScheme.surfaceContainerHighest,
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: SingleChildScrollView(
                                child: Text(
                                  _statusMessage!,
                                  textAlign: TextAlign.center,
                                  style: theme.textTheme.bodyMedium,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
