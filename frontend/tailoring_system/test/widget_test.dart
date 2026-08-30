import 'package:flutter_test/flutter_test.dart';
import 'package:flutter/material.dart';

import 'package:tailoring_system/screens/login_screen.dart';
import 'package:tailoring_system/services/auth_state.dart';

void main() {
  testWidgets('login screen renders', (WidgetTester tester) async {
    await tester.pumpWidget(MaterialApp(home: LoginScreen(auth: AuthState())));
    expect(find.text('لومار لإدارة الأعمال'), findsOneWidget);
    expect(find.text('تسجيل الدخول'), findsOneWidget);
  });
}
