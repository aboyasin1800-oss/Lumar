import 'package:flutter/material.dart';

import '../financial/finance_views.dart';

class FinancialDashboardScreen extends StatelessWidget {
	const FinancialDashboardScreen({super.key});

	@override
	  Widget build(BuildContext context) => Scaffold(
				appBar: AppBar(title: Text('الإدارة المالية')),
		  body: const FinancialScreen(),
			);
}