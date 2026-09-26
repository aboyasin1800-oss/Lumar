import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:tailoring_system/core/app_error.dart';

void main() {
  test('uses the standardized safe API error response', () {
    final error = AppError.fromResponse(http.Response(
      '{"success":false,"errorCode":"DLV-001","userFriendlyMessage":"تعذر إكمال تسليم الطلب."}',
      500,
    ));

    expect(error.errorCode, 'DLV-001');
    expect(error.userFriendlyMessage, 'تعذر إكمال تسليم الطلب.');
  });

  test('rejects legacy technical response bodies', () {
    final error = AppError.fromResponse(http.Response(
      'System.InvalidOperationException: OrderRepository.DeliverAsync',
      500,
    ));

    expect(error.errorCode, 'GEN-001');
    expect(error.userFriendlyMessage,
        'تعذر إتمام العملية. يرجى إعادة المحاولة.');
  });
}