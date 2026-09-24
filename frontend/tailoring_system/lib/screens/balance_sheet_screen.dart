import 'package:flutter/material.dart';
import 'financial/finance_views.dart' as finance;

class BalanceSheetScreen extends StatelessWidget {
	const BalanceSheetScreen({super.key});

	@override
	Widget build(BuildContext context) => const finance.FinancialStatementsScreen();
}