import 'package:flutter/material.dart';

import '../../services/auth_state.dart';
import 'customer_shell.dart';
import 'employee_shell.dart';
import 'role_gate.dart';
import 'supplier_shell.dart';

class AppRouter {
  const AppRouter._();

  static const roleGate = '/role-gate';
  static const customerShell = '/customer-shell';
  static const employeeShell = '/employee-shell';
  static const supplierShell = '/supplier-shell';

  static const List<String> foundationRoutes = [
    roleGate,
    customerShell,
    employeeShell,
    supplierShell,
  ];

  static Route<dynamic> generateRoute(RouteSettings settings) {
    switch (settings.name) {
      case roleGate:
        return MaterialPageRoute<void>(
          settings: settings,
          builder: (_) => const RoleGate(),
        );
      case customerShell:
        return MaterialPageRoute<void>(
          settings: settings,
          builder: (_) => CustomerShell(auth: AuthState.instance),
        );
      case employeeShell:
        return MaterialPageRoute<void>(
          settings: settings,
          builder: (_) => const EmployeeShell(),
        );
      case supplierShell:
        return MaterialPageRoute<void>(
          settings: settings,
          builder: (_) => const SupplierShell(),
        );
      default:
        return MaterialPageRoute<void>(
          settings: settings,
          builder: (_) => const RoleGate(),
        );
    }
  }
}
