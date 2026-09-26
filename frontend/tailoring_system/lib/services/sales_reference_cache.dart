import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/sales_reference_models.dart';
import '../repositories/sales_reference_repository.dart';

abstract interface class SalesReferenceStorage {
  Future<String?> read();
  Future<void> write(String value);
}

class SecureSalesReferenceStorage implements SalesReferenceStorage {
  const SecureSalesReferenceStorage({FlutterSecureStorage storage = const FlutterSecureStorage()})
      : _storage = storage;

  static const _snapshotKey = 'lumar_sales_reference_snapshot_v1';
  final FlutterSecureStorage _storage;

  @override
  Future<String?> read() => _storage.read(key: _snapshotKey);

  @override
  Future<void> write(String value) => _storage.write(key: _snapshotKey, value: value);
}

class SalesReferenceCache {
  SalesReferenceCache({
    SalesReferenceRepository? repository,
    SalesReferenceStorage? storage,
  })  : _repository = repository ?? SalesReferenceRepository(),
        _storage = storage ?? const SecureSalesReferenceStorage();

  static const schemaVersion = 1;
  static SalesReferenceSnapshot? _memorySnapshot;
  static Future<SalesReferenceSnapshot>? _loadFuture;
  static Future<SalesReferenceSnapshot?>? _refreshFuture;

  final SalesReferenceRepository _repository;
  final SalesReferenceStorage _storage;

  Future<SalesReferenceSnapshot> load() {
    final memory = _memorySnapshot;
    if (memory != null && memory.schemaVersion == schemaVersion) {
      return Future.value(memory);
    }
    return _loadFuture ??= _loadInitial();
  }

  Future<SalesReferenceSnapshot?> refreshIfChanged() {
    return _refreshFuture ??= _refreshIfChanged().whenComplete(() {
      _refreshFuture = null;
    });
  }

  Future<SalesReferenceSnapshot> forceRefresh() async {
    final snapshot = await _repository.getSnapshot();
    return _store(snapshot);
  }

  Future<SalesReferenceSnapshot> _loadInitial() async {
    final persistent = await _readPersistent();
    if (persistent != null) {
      _memorySnapshot = persistent;
      return persistent;
    }
    return forceRefresh();
  }

  Future<SalesReferenceSnapshot?> _refreshIfChanged() async {
    final current = _memorySnapshot ?? await _readPersistent();
    final version = await _repository.getVersion();
    if (current != null &&
        current.schemaVersion == schemaVersion &&
        current.version == version.version) {
      return null;
    }
    return forceRefresh();
  }

  Future<SalesReferenceSnapshot?> _readPersistent() async {
    final raw = await _storage.read();
    if (raw == null || raw.trim().isEmpty) return null;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map<String, dynamic>) return null;
      final snapshot = SalesReferenceSnapshot.fromJson(decoded);
      return snapshot.schemaVersion == schemaVersion && snapshot.version.isNotEmpty
          ? snapshot
          : null;
    } catch (_) {
      return null;
    }
  }

  Future<SalesReferenceSnapshot> _store(SalesReferenceSnapshot snapshot) async {
    if (snapshot.schemaVersion != schemaVersion || snapshot.version.isEmpty) {
      throw const FormatException('إصدار بيانات المبيعات المرجعية غير مدعوم.');
    }
    _memorySnapshot = snapshot;
    await _storage.write(jsonEncode(snapshot.toJson()));
    return snapshot;
  }
}