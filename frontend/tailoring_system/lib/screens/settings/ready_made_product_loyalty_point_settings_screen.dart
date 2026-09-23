import 'package:flutter/material.dart';

import 'product_loyalty_point_settings_screen.dart';

class ReadyMadeProductLoyaltyPointSettingsScreen extends StatelessWidget {
  const ReadyMadeProductLoyaltyPointSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const ProductLoyaltyPointSettingsScreen(
        mode: ProductLoyaltyPointSettingsMode.readyMade,
      );
}