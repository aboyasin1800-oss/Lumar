import 'package:flutter/material.dart';

class PiecePointSettingsScreen extends StatelessWidget {
  const PiecePointSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('إعدادات نقاط القطع')),
      body: const Center(
        child: Text('إعدادات نقاط القطع تُدار من إدارة الولاء في الخلفية.'),
      ),
    );
  }
}
