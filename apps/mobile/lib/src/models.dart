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
    this.latitude, {
    this.provinceNames = const [],
  });
  final double longitude;
  final double latitude;
  final List<String> provinceNames;

  factory MapStation.fromJson(Object? value) {
    final json = object(value);
    return MapStation(
      json['id'] as String,
      json['name'] as String,
      json['riverName'] as String,
      (json['longitude'] as num).toDouble(),
      (json['latitude'] as num).toDouble(),
      provinceNames: (json['provinceNames'] as List).cast<String>(),
    );
  }
}

class Measurement {
  const Measurement(
    this.seriesId,
    this.value,
    this.unit,
    this.observedAt,
    this.freshness,
    this.sourceId,
  );
  final String seriesId;
  final double value;
  final String unit;
  final DateTime observedAt;
  final String freshness;
  final String sourceId;

  factory Measurement.fromJson(Object? value) {
    final json = object(value);
    return Measurement(
      json['seriesId'] as String,
      (json['value'] as num).toDouble(),
      json['unit'] as String,
      DateTime.parse(json['observedAt'] as String),
      json['freshness'] as String,
      json['sourceId'] as String,
    );
  }
}

class HistoryPoint {
  const HistoryPoint(this.observedAt, this.value);
  final DateTime observedAt;
  final double value;

  factory HistoryPoint.fromJson(Object? value) {
    final json = object(value);
    final number = (json['value'] as num).toDouble();
    if (!number.isFinite) throw const FormatException('Non-finite reading.');
    return HistoryPoint(DateTime.parse(json['observedAt'] as String), number);
  }
}

class SeriesHistory {
  const SeriesHistory(
    this.seriesId,
    this.unit,
    this.cadenceSeconds,
    this.generatedAt,
    this.truncated,
    this.points,
  );
  final String seriesId;
  final String unit;
  final int? cadenceSeconds;
  final DateTime generatedAt;
  final bool truncated;
  final List<HistoryPoint> points;

  factory SeriesHistory.fromJson(Object? value) {
    final json = object(value);
    final cadence = json['cadenceSeconds'] as int?;
    final points = (json['points'] as List).map(HistoryPoint.fromJson).toList();
    if (cadence != null && cadence <= 0) {
      throw const FormatException('Invalid cadence.');
    }
    if (points.length > 2000) {
      throw const FormatException('Too many history points.');
    }
    for (var index = 1; index < points.length; index++) {
      if (!points[index].observedAt.isAfter(points[index - 1].observedAt)) {
        throw const FormatException('History points must be chronological.');
      }
    }
    return SeriesHistory(
      json['seriesId'] as String,
      json['unit'] as String,
      cadence,
      DateTime.parse(json['generatedAt'] as String),
      json['truncated'] as bool,
      points,
    );
  }

  static SeriesHistory fromEncoded(String encoded) =>
      SeriesHistory.fromJson(jsonDecode(encoded));
}

class HistoryResult {
  const HistoryResult(this.history, this.offline);
  final SeriesHistory history;
  final bool offline;
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
