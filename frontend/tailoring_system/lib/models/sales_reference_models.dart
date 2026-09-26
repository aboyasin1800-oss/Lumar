class SalesReferenceVersion {
  const SalesReferenceVersion({
    required this.version,
    required this.schemaVersion,
  });

  final String version;
  final int schemaVersion;

  factory SalesReferenceVersion.fromJson(Map<String, dynamic> json) =>
      SalesReferenceVersion(
        version: json['version']?.toString() ?? '',
        schemaVersion: (json['schemaVersion'] as num?)?.toInt() ?? 0,
      );
}

class SalesReferenceProductType {
  const SalesReferenceProductType({
    required this.productTypeId,
    required this.code,
    required this.nameAr,
    required this.category,
    required this.scope,
    required this.isActive,
  });

  final int productTypeId;
  final String code;
  final String nameAr;
  final String? category;
  final String? scope;
  final bool isActive;

  factory SalesReferenceProductType.fromJson(Map<String, dynamic> json) =>
      SalesReferenceProductType(
        productTypeId: (json['productTypeId'] as num?)?.toInt() ?? 0,
        code: json['code']?.toString() ?? '',
        nameAr: json['nameAr']?.toString() ?? '',
        category: json['category']?.toString(),
        scope: json['scope']?.toString(),
        isActive: json['isActive'] == true,
      );

  Map<String, dynamic> toJson() => {
        'productTypeId': productTypeId,
        'code': code,
        'nameAr': nameAr,
        'category': category,
        'scope': scope,
        'isActive': isActive,
      };
}

class SalesReferenceMeasurementField {
  const SalesReferenceMeasurementField({
    required this.productTypeId,
    required this.measurementProfileId,
    required this.measurementFieldId,
    required this.code,
    required this.nameAr,
    required this.unit,
    required this.isRequired,
    required this.sequence,
  });

  final int productTypeId;
  final int measurementProfileId;
  final int measurementFieldId;
  final String code;
  final String nameAr;
  final String unit;
  final bool isRequired;
  final int sequence;

  factory SalesReferenceMeasurementField.fromJson(Map<String, dynamic> json) =>
      SalesReferenceMeasurementField(
        productTypeId: (json['productTypeId'] as num?)?.toInt() ?? 0,
        measurementProfileId: (json['measurementProfileId'] as num?)?.toInt() ?? 0,
        measurementFieldId: (json['measurementFieldId'] as num?)?.toInt() ?? 0,
        code: json['code']?.toString() ?? '',
        nameAr: json['nameAr']?.toString() ?? '',
        unit: json['unit']?.toString() ?? '',
        isRequired: json['isRequired'] == true,
        sequence: (json['sequence'] as num?)?.toInt() ?? 0,
      );

  Map<String, dynamic> toJson() => {
        'productTypeId': productTypeId,
        'measurementProfileId': measurementProfileId,
        'measurementFieldId': measurementFieldId,
        'code': code,
        'nameAr': nameAr,
        'unit': unit,
        'isRequired': isRequired,
        'sequence': sequence,
      };
}

class SalesReferenceLoyaltyPointSetting {
  const SalesReferenceLoyaltyPointSetting({
    required this.productTypeId,
    required this.points,
    required this.isActive,
    required this.isConfigured,
  });

  final int productTypeId;
  final double? points;
  final bool? isActive;
  final bool isConfigured;

  factory SalesReferenceLoyaltyPointSetting.fromJson(Map<String, dynamic> json) =>
      SalesReferenceLoyaltyPointSetting(
        productTypeId: (json['productTypeId'] as num?)?.toInt() ?? 0,
        points: (json['points'] as num?)?.toDouble(),
        isActive: json['isActive'] as bool?,
        isConfigured: json['isConfigured'] == true,
      );

  Map<String, dynamic> toJson() => {
        'productTypeId': productTypeId,
        'points': points,
        'isActive': isActive,
        'isConfigured': isConfigured,
      };
}

class SalesReferenceSnapshot {
  const SalesReferenceSnapshot({
    required this.version,
    required this.schemaVersion,
    required this.productTypes,
    required this.measurementFields,
    required this.loyaltyPointSettings,
  });

  final String version;
  final int schemaVersion;
  final List<SalesReferenceProductType> productTypes;
  final List<SalesReferenceMeasurementField> measurementFields;
  final List<SalesReferenceLoyaltyPointSetting> loyaltyPointSettings;

  Map<int, SalesReferenceProductType> get productTypesById => {
        for (final productType in productTypes) productType.productTypeId: productType,
      };

  Map<int, List<SalesReferenceMeasurementField>> get fieldsByProductType => {
        for (final productType in productTypes)
          productType.productTypeId: measurementFields
              .where((field) => field.productTypeId == productType.productTypeId)
              .toList(growable: false),
      };

  factory SalesReferenceSnapshot.fromJson(Map<String, dynamic> json) =>
      SalesReferenceSnapshot(
        version: json['version']?.toString() ?? '',
        schemaVersion: (json['schemaVersion'] as num?)?.toInt() ?? 0,
        productTypes: _readList(
          json['productTypes'],
          SalesReferenceProductType.fromJson,
        ),
        measurementFields: _readList(
          json['measurementFields'],
          SalesReferenceMeasurementField.fromJson,
        ),
        loyaltyPointSettings: _readList(
          json['loyaltyPiecePointSettings'],
          SalesReferenceLoyaltyPointSetting.fromJson,
        ),
      );

  Map<String, dynamic> toJson() => {
        'version': version,
        'schemaVersion': schemaVersion,
        'productTypes': productTypes.map((item) => item.toJson()).toList(),
        'measurementFields': measurementFields.map((item) => item.toJson()).toList(),
        'loyaltyPiecePointSettings': loyaltyPointSettings.map((item) => item.toJson()).toList(),
      };

  static List<T> _readList<T>(
    Object? value,
    T Function(Map<String, dynamic> json) fromJson,
  ) => value is List
      ? value
          .whereType<Map>()
          .map((item) => fromJson(Map<String, dynamic>.from(item)))
          .toList(growable: false)
      : const [];
}