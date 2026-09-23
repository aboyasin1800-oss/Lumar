import 'dart:convert';

const Set<String> _technicalMeasurementKeys = {
  'request1',
  'request2',
  'specialRequest',
  'notes1',
  'notes2',
  'fabricCode',
  'fabricType',
  'fabricColor',
  'catalogNumber',
  'barcode',
  'fullCost',
  'source',
  'consumption',
  'consumptionUnit',
  'quantity',
  'unitPrice',
  'lineTotal',
  'productType',
  'productCode',
  'availableInches',
  'quantityInch',
  'quantityinch',
  'totalCost',
  'fabric',
  'fabrictype',
  'fabriccolor',
  'fabriccode',
  'catalognumber',
  'specialrequest',
  'request1value',
  'request2value',
  'notes1value',
  'notes2value',
  '_catalogNumber',
  '_consumption',
  '_consumptionUnit',
  'measurementSnapshot',
};

Map<String, dynamic> parseMeasurementSnapshot(Object? source) {
  if (source == null) return const {};

  try {
    final unwrapped = _unwrapMeasurementValue(source);
    if (unwrapped is Map<String, dynamic>) {
      return _normalizeMeasurementMap(unwrapped);
    }
    if (unwrapped is Map) {
      return _normalizeMeasurementMap(Map<String, dynamic>.from(unwrapped));
    }
    if (unwrapped is String) {
      final trimmed = unwrapped.trim();
      if (trimmed.isEmpty) return const {};
      final decoded = jsonDecode(trimmed);
      if (decoded is Map<String, dynamic>) {
        return _normalizeMeasurementMap(decoded);
      }
      if (decoded is Map) {
        return _normalizeMeasurementMap(Map<String, dynamic>.from(decoded));
      }
      if (decoded is String) {
        return parseMeasurementSnapshot(decoded);
      }
    }
  } catch (_) {
    return const {};
  }

  return const {};
}

Object? _unwrapMeasurementValue(Object? source) {
  if (source == null) return null;

  if (source is Map<String, dynamic>) {
    return source;
  }
  if (source is Map) {
    return Map<String, dynamic>.from(source);
  }
  if (source is String) {
    final trimmed = source.trim();
    if (trimmed.isEmpty) return source;
    if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) {
      return source;
    }

    try {
      final decoded = jsonDecode(trimmed);
      if (decoded is Map || decoded is String) {
        return _unwrapMeasurementValue(decoded);
      }
      return decoded;
    } catch (_) {
      return source;
    }
  }

  return source;
}

Map<String, dynamic> _normalizeMeasurementMap(Map<String, dynamic> source) {
  final normalized = <String, dynamic>{};

  for (final entry in source.entries) {
    final key = entry.key.toString().trim();
    if (key.isEmpty) continue;

    if ((key == 'measurementSnapshot' || key == 'MeasurementSnapshot')) {
      final nested = parseMeasurementSnapshot(entry.value);
      if (nested.isNotEmpty) {
        normalized.addAll(nested);
      }
      continue;
    }

    if (entry.value is Map || entry.value is String) {
      final nested = parseMeasurementSnapshot(entry.value);
      if (nested.isNotEmpty &&
          (key == 'measurementMap' || key == 'MeasurementMap')) {
        normalized.addAll(nested);
        continue;
      }
    }

    normalized[key] = entry.value;
  }

  return normalized;
}

Map<String, dynamic> extractRealPieceMeasurements(Map<String, dynamic> source) {
  final flattened = _normalizeMeasurementMap(source);
  final filtered = <String, dynamic>{};

  for (final entry in flattened.entries) {
    final key = entry.key.toString().trim();
    if (key.isEmpty || key.startsWith('_')) {
      continue;
    }

    final normalizedKey = key.toLowerCase();
    if (_technicalMeasurementKeys.contains(key) ||
        _technicalMeasurementKeys.contains(normalizedKey)) {
      continue;
    }

    final value = entry.value;
    if (value == null || value.toString().trim().isEmpty) {
      continue;
    }

    filtered[key] = value;
  }

  return filtered;
}
