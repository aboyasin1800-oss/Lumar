import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AuthUser {
  const AuthUser({
    required this.userId,
    required this.username,
    required this.fullName,
    required this.isActive,
    this.role,
    this.lastLoginUtc,
    this.accountType,
    this.customerId,
    this.employeeId,
    this.supplierId,
  });
  factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(
        userId: (json['userId'] as num?)?.toInt() ?? 0,
        username: (json['username'] as String?) ?? '',
        fullName: (json['fullName'] as String?) ?? '',
        role: json['role'] as String?,
        isActive: (json['isActive'] as bool?) ?? false,
        lastLoginUtc: json['lastLoginUtc'] as String?,
        accountType: json['accountType'] as String?,
        customerId: (json['customerId'] as num?)?.toInt(),
        employeeId: (json['employeeId'] as num?)?.toInt(),
        supplierId: (json['supplierId'] as num?)?.toInt(),
      );
  final int userId;
  final String username;
  final String fullName;
  final String? role;
  final bool isActive;
  final String? lastLoginUtc;
  final String? accountType;
  final int? customerId;
  final int? employeeId;
  final int? supplierId;
}

class AuthState extends ChangeNotifier {
  static final AuthState instance = AuthState();

  AuthState({String? baseUrl})
      : _baseUrl = baseUrl ??
            const String.fromEnvironment('LUMAR_API_URL',
                defaultValue: 'http://127.0.0.1:5093');
  static const _tokenKey = 'lumar_auth_token';
  static const _usernameKey = 'lumar_remembered_username';
  final String _baseUrl;
  final _storage = const FlutterSecureStorage();
  AuthUser? user;
  String? _token;
  bool initialized = false;
  bool _disposed = false;
  bool get signedIn => user != null;
  String? get token => _token;
  String? get rememberedUsername => _rememberedUsername;
  String? _rememberedUsername;

  void _notifyIfActive() {
    if (!_disposed) {
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  Future<void> initialize() async {
    try {
      _rememberedUsername = await _storage.read(key: _usernameKey);
      _token = await _storage.read(key: _tokenKey);
      if (_token != null) {
        final currentUser = await _me(_token!);
        if (currentUser == null ||
            (currentUser.accountType == 'Employee' && currentUser.employeeId == null)) {
          await _storage.delete(key: _tokenKey);
          user = null;
        } else {
          user = currentUser;
        }
      }
      if (user == null) {
        await _storage.delete(key: _tokenKey);
      }
    } catch (_) {
      user = null;
      _token = null;
    } finally {
      if (!_disposed) {
        initialized = true;
        _notifyIfActive();
      }
    }
  }

  Future<String?> login(
      String username, String password, bool rememberMe) async {
    try {
      final response = await _request('POST', '/auth/login', body: {
        'username': username,
        'password': password,
        'rememberMe': rememberMe
      }).timeout(const Duration(seconds: 10));
      if (response.statusCode == 401) {
        return 'اسم المستخدم أو كلمة المرور غير صحيحة.';
      }
      if (response.statusCode >= 500) {
        return 'حدث خطأ في الخادم. أعد المحاولة لاحقًا.';
      }
      if (response.statusCode != 200) {
        return 'تعذر إكمال تسجيل الدخول. أعد المحاولة.';
      }
      final json = jsonDecode(response.body) as Map<String, dynamic>;
      final token = json['token'] as String;
      final currentUser =
          AuthUser.fromJson(json['user'] as Map<String, dynamic>);
      if (currentUser.accountType == 'Employee' && currentUser.employeeId == null) {
        return 'هذا الحساب غير مرتبط بموظف فعلي في النظام.';
      }
      if (rememberMe) {
        await _storage.write(key: _tokenKey, value: token);
        await _storage.write(key: _usernameKey, value: username.trim());
      } else {
        await _storage.delete(key: _tokenKey);
      }
      _token = token;
      user = currentUser;
      _notifyIfActive();
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

  Future<String?> registerCustomer({
    required String username,
    required String password,
    required String confirmPassword,
    required String fullName,
    required String customerName,
    required String phoneNumber,
    String? address,
    String? notes,
    int? referrerCustomerId,
    String? relationshipType,
    bool rememberMe = true,
  }) async {
    try {
      final response = await _request('POST', '/auth/mobile/customer/register', body: {
        'username': username.trim(),
        'password': password,
        'confirmPassword': confirmPassword,
        'fullName': fullName.trim(),
        'customerName': customerName.trim(),
        'phoneNumber': phoneNumber.trim(),
        'address': address?.trim(),
        'notes': notes?.trim(),
        'rememberMe': rememberMe,
        'referrerCustomerId': referrerCustomerId,
        'relationshipType': relationshipType?.trim(),
      }).timeout(const Duration(seconds: 12));

      if (response.statusCode == 200) {
        final json = jsonDecode(response.body) as Map<String, dynamic>;
        final token = json['token'] as String;
        final currentUser = AuthUser.fromJson(json['user'] as Map<String, dynamic>);
        if (rememberMe) {
          await _storage.write(key: _tokenKey, value: token);
          await _storage.write(key: _usernameKey, value: username.trim());
        }
        _token = token;
        user = currentUser;
        _notifyIfActive();
        return null;
      }

      if (response.statusCode == 409) {
        return 'اسم المستخدم أو العميل موجود بالفعل.';
      }
      if (response.statusCode == 400) {
        return 'بيانات إنشاء حساب العميل غير صالحة.';
      }
      return 'تعذر إنشاء حساب العميل.';
    } on TimeoutException {
      return 'انتهت مهلة إنشاء حساب العميل.';
    } on SocketException {
      return 'تعذر الاتصال بالخادم أثناء إنشاء حساب العميل.';
    } on HttpException {
      return 'تعذر الاتصال بالخادم أثناء إنشاء حساب العميل.';
    } catch (_) {
      return 'تعذر إنشاء حساب العميل.';
    }
  }

  Future<Map<String, dynamic>?> provisionEmployeeAccount({
    required String employeeCode,
    required String fullName,
    String? phoneNumber,
    String? preferredUsername,
    bool rememberMe = true,
  }) async {
    try {
      final response = await _request('POST', '/auth/mobile/employee/provision', body: {
        'employeeCode': employeeCode.trim(),
        if ((phoneNumber ?? '').trim().isNotEmpty) 'phoneNumber': phoneNumber!.trim(),
        'preferredUsername': preferredUsername?.trim(),
        'rememberMe': rememberMe,
      }).timeout(const Duration(seconds: 12));

      if (response.statusCode == 200) {
        final json = jsonDecode(response.body) as Map<String, dynamic>;
        return json;
      }

      final statusCode = response.statusCode;
      final message = switch (statusCode) {
        400 => 'بيانات الموظف غير صالحة.',
        404 => 'لم يتم العثور على الموظف الرسمي.',
        409 => 'هذا الموظف أو حسابه موجود بالفعل.',
        _ => 'تعذر إنشاء حساب الموظف الرسمي.',
      };

      return {'statusCode': statusCode, 'message': message};
    } on TimeoutException {
      return {'statusCode': 500, 'message': 'انتهت مهلة الاتصال بالخادم أثناء إنشاء حساب الموظف.'};
    } on SocketException {
      return {'statusCode': 500, 'message': 'تعذر الاتصال بالخادم أثناء إنشاء حساب الموظف.'};
    } on HttpException {
      return {'statusCode': 500, 'message': 'تعذر الاتصال بالخادم أثناء إنشاء حساب الموظف.'};
    } catch (_) {
      return {'statusCode': 500, 'message': 'تعذر إنشاء حساب الموظف الرسمي.'};
    }
  }

  Future<String?> completeEmployeePasswordSetup({
    required String setupToken,
    required String newPassword,
    required String confirmPassword,
  }) async {
    try {
      final response = await _request('POST', '/auth/mobile/employee/password-setup', body: {
        'setupToken': setupToken,
        'newPassword': newPassword,
        'confirmPassword': confirmPassword,
      }).timeout(const Duration(seconds: 12));

      if (response.statusCode == 200 || response.statusCode == 204) {
        return null;
      }
      if (response.statusCode == 400) {
        return 'رمز إعداد كلمة المرور غير صالح أو كلمة المرور غير متطابقة.';
      }
      return 'تعذر إعداد كلمة المرور لهذا الموظف.';
    } on TimeoutException {
      return 'انتهت مهلة إعداد كلمة المرور.';
    } on SocketException {
      return 'تعذر الاتصال بالخادم أثناء إعداد كلمة المرور.';
    } on HttpException {
      return 'تعذر الاتصال بالخادم أثناء إعداد كلمة المرور.';
    } catch (_) {
      return 'تعذر إعداد كلمة المرور.';
    }
  }

  Future<void> logout() async {
    if (_disposed) return;
    if (_token != null) {
      await _request('POST', '/auth/logout', token: _token);
    }
    await _storage.delete(key: _tokenKey);
    _token = null;
    user = null;
    _notifyIfActive();
  }

  Future<String?> changeUsername(String password, String username) async {
    if (_disposed) return null;
    final response = await _request('PUT', '/auth/username',
        token: _token,
        body: {'currentPassword': password, 'username': username});
    if (response.statusCode != 200) {
      return response.statusCode == 409
          ? 'اسم المستخدم مستخدم بالفعل.'
          : 'تعذر تغيير اسم المستخدم.';
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    _token = json['token'] as String;
    user = AuthUser.fromJson(json['user'] as Map<String, dynamic>);
    await _storage.write(key: _tokenKey, value: _token);
    await _storage.write(key: _usernameKey, value: user!.username);
    _notifyIfActive();
    return null;
  }

  Future<String?> changePassword(String currentPassword, String newPassword,
      String confirmPassword) async {
    if (_disposed) return 'تعذر تغيير كلمة المرور.';
    final response =
        await _request('PUT', '/auth/password', token: _token, body: {
      'currentPassword': currentPassword,
      'newPassword': newPassword,
      'confirmPassword': confirmPassword
    });
    if (response.statusCode == 204) {
      await logout();
      return null;
    }
    return 'تعذر تغيير كلمة المرور.';
  }

  Future<AuthUser?> _me(String token) async {
    final response = await _request('GET', '/auth/me', token: token);
    return response.statusCode == 200
        ? AuthUser.fromJson(jsonDecode(response.body) as Map<String, dynamic>)
        : null;
  }

  Future<ApiResponse> _request(String method, String path,
      {String? token, Map<String, dynamic>? body}) async {
    final client = HttpClient()..connectionTimeout = const Duration(seconds: 5);
    try {
      final request = await client.openUrl(method, Uri.parse('$_baseUrl$path'));
      if (token != null) {
        request.headers.set(
          HttpHeaders.authorizationHeader,
          'Bearer $token',
        );
      }
      if (body != null) {
        request.headers.contentType = ContentType.json;
        request.write(jsonEncode(body));
      }
      final response = await request.close();
      final text = await utf8.decoder.bind(response).join();
      return ApiResponse(response.statusCode, text);
    } finally {
      client.close();
    }
  }
}

class ApiResponse {
  const ApiResponse(this.statusCode, this.body);
  final int statusCode;
  final String body;
}




