import 'package:flutter/foundation.dart';

import '../models/referral_models.dart';
import '../repositories/referral_repository.dart';

enum ReferralLoadState { idle, loading, ready, empty, error }

class ReferralProvider extends ChangeNotifier {
  ReferralProvider({ReferralRepository? repository})
      : _repository = repository ?? ReferralRepository();

  final ReferralRepository _repository;
  ReferralLoadState state = ReferralLoadState.idle;
  Object? error;

  ReferralAccount? account;
  List<ReferralCode> codes = const [];
  List<ReferralTransaction> transactions = const [];
  ReferralTree? tree;
  List<ReferralReward> rewards = const [];

  Future<void> loadOverview(int customerId) async {
    state = ReferralLoadState.loading;
    error = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _repository.getAccount(customerId),
        _repository.getCodes(customerId),
        _repository.getTransactions(customerId),
        _repository.getTree(customerId),
        _repository.getReferralRewards(),
      ]);

      account = results[0] as ReferralAccount;
      codes = results[1] as List<ReferralCode>;
      transactions = results[2] as List<ReferralTransaction>;
      tree = results[3] as ReferralTree;
      rewards = results[4] as List<ReferralReward>;

      state = account == null && codes.isEmpty && transactions.isEmpty
          ? ReferralLoadState.empty
          : ReferralLoadState.ready;
    } catch (caught) {
      error = caught;
      state = ReferralLoadState.error;
    }

    notifyListeners();
  }
}
