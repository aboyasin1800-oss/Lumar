import 'package:flutter/material.dart';

class AppPlaceholder extends StatelessWidget {
	const AppPlaceholder({required this.title, super.key});
	final String title;

	@override
	Widget build(BuildContext context) => Center(
		child: Text('وحدة $title جاهزة للمرحلة التالية من التنفيذ.', style: Theme.of(context).textTheme.titleMedium),
	);
}