import 'dart:convert';

Map<String, dynamic> object(Object? value) =>
    Map<String, dynamic>.from(value as Map);

class Location {
  const Location(this.id, this.name, this.provinceName);
  final String id;
  final String name;
  final String provinceName;

  factory Location.fromJson(Object? value) {
    final json = object(value);
    return Location(
      json['id'] as String,
      json['name'] as String,
      json['provinceName'] as String,
    );
  }
}

class Station {
  const Station(this.id, this.name, this.riverName);
  final String id;
  final String name;
  final String riverName;

  factory Station.fromJson(Object? value) {
    final json = object(value);
    return Station(
      json['id'] as String,
      json['name'] as String,
      json['riverName'] as String,
    );
  }
}

class MapStation extends Station {
  const MapStation(
    super.id,
    super.name,
    super.riverName,
    this.longitude,
    this.latitude,
  );
  final double longitude;
  final double latitude;

  factory MapStation.fromJson(Object? value) {
    final json = object(value);
    return MapStation(
      json['id'] as String,
      json['name'] as String,
      json['riverName'] as String,
      (json['longitude'] as num).toDouble(),
      (json['latitude'] as num).toDouble(),
    );
  }
}

class Measurement {
  const Measurement(
    this.value,
    this.unit,
    this.observedAt,
    this.freshness,
    this.sourceId,
  );
  final double value;
  final String unit;
  final DateTime observedAt;
  final String freshness;
  final String sourceId;

  factory Measurement.fromJson(Object? value) {
    final json = object(value);
    return Measurement(
      (json['value'] as num).toDouble(),
      json['unit'] as String,
      DateTime.parse(json['observedAt'] as String),
      json['freshness'] as String,
      json['sourceId'] as String,
    );
  }
}

class Change {
  const Change(
    this.windowHours,
    this.delta,
    this.unit,
    this.trend,
    this.availability,
    this.reason,
  );
  final int windowHours;
  final double? delta;
  final String unit;
  final String trend;
  final String availability;
  final String? reason;

  factory Change.fromJson(Object? value) {
    final json = object(value);
    return Change(
      json['windowHours'] as int,
      (json['delta'] as num?)?.toDouble(),
      json['unit'] as String,
      json['trend'] as String,
      json['availability'] as String,
      json['unavailableReason'] as String?,
    );
  }
}

class StationSummary {
  const StationSummary(
    this.stationId,
    this.generatedAt,
    this.synthetic,
    this.height,
    this.discharge,
    this.changes,
    this.dataStatus,
    this.calculatedCondition,
    this.noticeCoverage,
    this.notices,
    this.dataVersion,
  );

  final String stationId;
  final DateTime generatedAt;
  final bool synthetic;
  final Measurement? height;
  final Measurement? discharge;
  final List<Change> changes;
  final String dataStatus;
  final String calculatedCondition;
  final String noticeCoverage;
  final List<Object?> notices;
  final String dataVersion;

  factory StationSummary.fromJson(Object? value) {
    final json = object(value);
    return StationSummary(
      json['stationId'] as String,
      DateTime.parse(json['generatedAt'] as String),
      json['synthetic'] as bool,
      json['height'] == null ? null : Measurement.fromJson(json['height']),
      json['discharge'] == null
          ? null
          : Measurement.fromJson(json['discharge']),
      (json['changes'] as List).map(Change.fromJson).toList(),
      json['dataStatus'] as String,
      json['calculatedCondition'] as String,
      object(json['noticeCoverage'])['status'] as String,
      List<Object?>.from(json['notices'] as List),
      json['dataVersion'] as String,
    );
  }

  static StationSummary fromEncoded(String encoded) =>
      StationSummary.fromJson(jsonDecode(encoded));
}

class SummaryResult {
  const SummaryResult(this.summary, this.offline);
  final StationSummary summary;
  final bool offline;
}
