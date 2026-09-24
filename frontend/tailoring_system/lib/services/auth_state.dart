import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AuthUser {
	const AuthUser({required this.userId, required this.username, required this.fullName, required this.isActive, this.role, this.lastLoginUtc});
	factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(userId: json['userId'] as int, username: json['username'] as String, fullName: json['fullName'] as String, role: json['role'] as String?, isActive: json['isActive'] as bool, lastLoginUtc: json['lastLoginUtc'] as String?);
	final int userId;
	final String username;
	final String fullName;
	final String? role;
	final bool isActive;
	final String? lastLoginUtc;
}

class AuthState extends ChangeNotifier {
	AuthState({String? baseUrl}) : _baseUrl = baseUrl ?? const String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');
	static const _tokenKey = 'lumar_auth_token';
	static const _usernameKey = 'lumar_remembered_username';
	final String _baseUrl;
	final _storage = const FlutterSecureStorage();
	AuthUser? user;
	String? _token;
	bool initialized = false;
	bool get signedIn => user != null;
	String? get token => _token;
	String? get rememberedUsername => _rememberedUsername;
	String? _rememberedUsername;

	Future<void> initialize() async {
		try {
			_rememberedUsername = await _storage.read(key: _usernameKey);
			_token = await _storage.read(key: _tokenKey);
			if (_token != null) user = await _me(_token!);
			if (user == null) await _storage.delete(key: _tokenKey);
		} catch (_) {
			user = null;
			_token = null;
		} finally {
			initialized = true;
			notifyListeners();
		}
	}

	Future<String?> login(String username, String password, bool rememberMe) async {
		try {
			final response = await _request('POST', '/auth/login', body: {'username': username, 'password': password, 'rememberMe': rememberMe}).timeout(const Duration(seconds: 10));
			if (response.statusCode == 401) return 'اسم المستخدم أو كلمة المرور غير صحيحة.';
			if (response.statusCode >= 500) return 'حدث خطأ في الخادم. أعد المحاولة لاحقًا.';
			if (response.statusCode != 200) return 'تعذر إكمال تسجيل الدخول. أعد المحاولة.';
			final json = jsonDecode(response.body) as Map<String, dynamic>;
			final token = json['token'] as String;
			final currentUser = AuthUser.fromJson(json['user'] as Map<String, dynamic>);
			if (rememberMe) { await _storage.write(key: _tokenKey, value: token); await _storage.write(key: _usernameKey, value: username.trim()); } else { await _storage.delete(key: _tokenKey); }
			_token = token;
			user = currentUser;
			notifyListeners();
			return null;
		} on TimeoutException {
			return 'انتهت مهلة تسجيل الدخول. تأكد من تشغيل الخادم ثم أعد المحاولة.';
		} on SocketException {
			return 'تعذر الاتصال بالخادم. تأكد من تشغيل الخادم ثم أعد المحاولة.';
		} on HttpException {
			return 'تعذر الاتصال بالخادم. تأكد من تشغيل الخادم ثم أعد المحاولة.';
		} catch (_) {
			return 'تعذر إكمال تسجيل الدخول. أعد المحاولة.';
		}
	}

	Future<void> logout() async { if (_token != null) await _request('POST', '/auth/logout', token: _token); await _storage.delete(key: _tokenKey); _token = null; user = null; notifyListeners(); }
	Future<String?> changeUsername(String password, String username) async { final response=await _request('PUT','/auth/username',token:_token,body:{'currentPassword':password,'username':username}); if(response.statusCode!=200)return response.statusCode==409?'اسم المستخدم مستخدم بالفعل.':'تعذر تغيير اسم المستخدم.'; final json=jsonDecode(response.body) as Map<String,dynamic>;_token=json['token'] as String;user=AuthUser.fromJson(json['user'] as Map<String,dynamic>);await _storage.write(key:_tokenKey,value:_token);await _storage.write(key:_usernameKey,value:user!.username);notifyListeners();return null; }
	Future<String?> changePassword(String currentPassword,String newPassword,String confirmPassword) async { final response=await _request('PUT','/auth/password',token:_token,body:{'currentPassword':currentPassword,'newPassword':newPassword,'confirmPassword':confirmPassword});if(response.statusCode==204){await logout();return null;}return 'تعذر تغيير كلمة المرور.'; }
	Future<AuthUser?> _me(String token) async { final response=await _request('GET','/auth/me',token:token);return response.statusCode==200?AuthUser.fromJson(jsonDecode(response.body) as Map<String,dynamic>):null; }
	Future<ApiResponse> _request(String method,String path,{String? token,Map<String,dynamic>? body}) async { final client=HttpClient()..connectionTimeout=const Duration(seconds: 5);try{final request=await client.openUrl(method,Uri.parse('$_baseUrl$path'));if(token!=null)request.headers.set(HttpHeaders.authorizationHeader,'Bearer $token');if(body!=null){request.headers.contentType=ContentType.json;request.write(jsonEncode(body));}final response=await request.close();final text=await utf8.decoder.bind(response).join();return ApiResponse(response.statusCode,text);}finally{client.close();} }
}
class ApiResponse { const ApiResponse(this.statusCode,this.body); final int statusCode; final String body; }