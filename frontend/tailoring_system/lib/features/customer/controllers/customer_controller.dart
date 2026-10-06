import '../../../services/auth_state.dart';

class CustomerController {
  const CustomerController();

  CustomerProfileViewModel profileFor(AuthUser? user) {
    final currentUser = user;
    return CustomerProfileViewModel(
      fullName: currentUser?.fullName ?? 'غير متوفر',
      customerCode: currentUser?.customerId != null
          ? 'CUST-${currentUser!.customerId}'
          : 'غير متوفر',
      status: currentUser?.isActive == true ? 'نشط' : 'غير نشط',
      lastLogin: currentUser?.lastLoginUtc ?? 'غير متوفر',
      mobile: 'غير متوفر',
      email: 'غير متوفر',
      city: 'غير متوفر',
      address: 'غير متوفر',
      loyaltyTier: currentUser?.accountType ?? 'حساب عادي',
      points: 'غير متوفر',
      preferredLanguage: 'العربية',
      joinedAt: 'غير متوفر',
      username: currentUser?.username ?? 'غير متوفر',
      role: currentUser?.role ?? 'غير متوفر',
    );
  }
}

class CustomerProfileViewModel {
  const CustomerProfileViewModel({
    required this.fullName,
    required this.customerCode,
    required this.status,
    required this.lastLogin,
    required this.mobile,
    required this.email,
    required this.city,
    required this.address,
    required this.loyaltyTier,
    required this.points,
    required this.preferredLanguage,
    required this.joinedAt,
    required this.username,
    required this.role,
  });

  final String fullName;
  final String customerCode;
  final String status;
  final String lastLogin;
  final String mobile;
  final String email;
  final String city;
  final String address;
  final String loyaltyTier;
  final String points;
  final String preferredLanguage;
  final String joinedAt;
  final String username;
  final String role;
}
