import 'package:flutter/foundation.dart';

import '../models/loyalty_models.dart';
import '../repositories/loyalty_repository.dart';

enum LoyaltyLoadState { idle, loading, ready, empty, error }

class LoyaltyProvider extends ChangeNotifier {
  LoyaltyProvider({LoyaltyRepository? repository})
      : _repository = repository ?? LoyaltyRepository();

  final LoyaltyRepository _repository;
  LoyaltyLoadState state = LoyaltyLoadState.idle;
  Object? error;

  LoyaltyAccount? account;
  LoyaltyBalance? balance;
  List<LoyaltyTransaction> transactions = const [];
  List<LoyaltyProgramSettings> programSettings = const [];
  List<LoyaltyPiecePointSetting> piecePointSettings = const [];
  List<VipLevel> vipLevels = const [];
  List<LoyaltyRule> rules = const [];

  Future<void> loadOverview(int customerId) async {
    state = LoyaltyLoadState.loading;
    error = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _repository.getAccount(customerId),
        _repository.getBalance(customerId),
        _repository.getTransactions(customerId),
        _repository.getProgramSettings(),
        _repository.getPiecePointSettings(),
        _repository.getVipLevels(),
        _repository.getRules(),
      ]);

      account = results[0] as LoyaltyAccount;
      balance = results[1] as LoyaltyBalance;
      transactions = results[2] as List<LoyaltyTransaction>;
      programSettings = results[3] as List<LoyaltyProgramSettings>;
      piecePointSettings = results[4] as List<LoyaltyPiecePointSetting>;
      vipLevels = results[5] as List<VipLevel>;
      rules = results[6] as List<LoyaltyRule>;

      state = account == null && transactions.isEmpty
          ? LoyaltyLoadState.empty
          : LoyaltyLoadState.ready;
    } catch (caught) {
      error = caught;
      state = LoyaltyLoadState.error;
    }

    notifyListeners();
  }
}
