import 'package:flutter/material.dart';

import '../core/app_routes.dart';
import '../services/auth_state.dart';

class LoginScreen extends StatefulWidget {
	const LoginScreen({required this.auth, super.key});
	final AuthState auth;
	@override State<LoginScreen> createState()=>_LoginScreenState();
}
class _LoginScreenState extends State<LoginScreen> {
	final username=TextEditingController(); final password=TextEditingController(); final passwordFocus=FocusNode(); bool rememberMe=false; bool loading=false; String? error;
	@override void initState(){super.initState();username.text=widget.auth.rememberedUsername??'';}
	@override void dispose(){username.dispose();password.dispose();passwordFocus.dispose();super.dispose();}
	Future<void> _login() async { if(loading)return;setState(()=>loading=true);final message=await widget.auth.login(username.text,password.text,rememberMe);if(!mounted)return;setState((){loading=false;error=message;});if(message==null)Navigator.of(context).pushReplacementNamed(AppRoutes.dashboard); }

	@override
	Widget build(BuildContext context) => Scaffold(
		body: Center(child: SizedBox(
			width: 360,
			child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
				Text('لومار لإدارة الأعمال', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineMedium),
				const SizedBox(height: 24),
				TextField(controller:username,textInputAction:TextInputAction.next,onSubmitted:(_)=>passwordFocus.requestFocus(),decoration:const InputDecoration(labelText: 'اسم المستخدم', border: OutlineInputBorder())),
				const SizedBox(height: 12),
				TextField(controller:password,focusNode:passwordFocus,obscureText: true,textInputAction:TextInputAction.done,onSubmitted:(_)=>_login(), decoration:const InputDecoration(labelText: 'كلمة المرور', border: OutlineInputBorder())),
				CheckboxListTile(contentPadding:EdgeInsets.zero,value:rememberMe,onChanged:(value)=>setState(()=>rememberMe=value??false),title:const Text('تذكرني')),
				if(error!=null) Text(error!,style:TextStyle(color:Theme.of(context).colorScheme.error)),
				const SizedBox(height: 20),
				FilledButton(onPressed:loading?null:_login, child: Text(loading?'جارٍ تسجيل الدخول...':'تسجيل الدخول')),
			]),
		)),
	);
}