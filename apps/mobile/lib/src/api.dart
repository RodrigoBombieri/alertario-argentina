import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import 'models.dart';
import 'store.dart';

class AlertaRioApi {
  AlertaRioApi({Dio? client, String? baseUrl})
    : _baseUrl = baseUrl ?? const String.fromEnvironment('API_BASE_URL'),
      _client =
          client ??
          Dio(
            BaseOptions(
              connectTimeout: const Duration(seconds: 8),
              receiveTimeout: const Duration(seconds: 8),
            ),
          );

  final Dio _client;
  final String _baseUrl;

  String _url(String path) {
    final base = Uri.tryParse(_baseUrl);
    if (base == null ||
        !base.hasScheme ||
        base.host.isEmpty ||
        !{'http', 'https'}.contains(base.scheme) ||
        (kReleaseMode && base.scheme != 'https')) {
      throw StateError('Configurá API_BASE_URL para usar la API propia.');
    }
    return base.resolve(path).toString();
  }

  Future<String> dataMode() async {
    final response = await _client.get<Object?>(_url('/v1/status'));
    return object(response.data)['mode'] as String;
  }

  Future<List<Location>> searchLocations(String query) async {
    final response = await _client.get<Object?>(
      _url('/v1/locations'),
      queryParameters: {'query': query, 'limit': 20},
    );
    return (object(response.data)['items'] as List)
        .map(Location.fromJson)
        .toList();
  }

  Future<List<Station>> stationsForLocation(String id) async {
    final response = await _client.get<Object?>(
      _url('/v1/locations/${Uri.encodeComponent(id)}/stations'),
    );
    return (object(response.data)['items'] as List)
        .map(Station.fromJson)
        .toList();
  }

  Future<Map<String, dynamic>> summaryJson(String id) async {
    final response = await _client.get<Object?>(
      _url('/v1/stations/${Uri.encodeComponent(id)}/summary'),
    );
    return object(response.data);
  }

  Future<Map<String, dynamic>> historyJson(String seriesId) async {
    final response = await _client.get<Object?>(
      _url('/v1/series/${Uri.encodeComponent(seriesId)}/recent'),
    );
    return object(response.data);
  }

  Future<SeriesHistoryPage> historyPage(
    String seriesId,
    DateTime from,
    DateTime to,
    String? cursor,
  ) async {
    final response = await _client.get<Object?>(
      _url('/v1/series/${Uri.encodeComponent(seriesId)}/history'),
      queryParameters: {
        'from': from.toUtc().toIso8601String(),
        'to': to.toUtc().toIso8601String(),
        'limit': 100,
        if (cursor != null) 'cursor': cursor,
      },
    );
    return SeriesHistoryPage.fromJson(response.data);
  }

  Future<List<MapStation>> stationsInBounds(String bbox) async {
    final response = await _client.get<Object?>(
      _url('/v1/stations/map'),
      queryParameters: {'bbox': bbox, 'limit': 200},
    );
    return (object(response.data)['items'] as List)
        .map(MapStation.fromJson)
        .toList();
  }
}

class SummaryRepository {
  const SummaryRepository(this.api, this.store);
  final AlertaRioApi api;
  final LocalStore store;

  Future<SummaryResult> load(String stationId) async {
    try {
      final json = await api.summaryJson(stationId);
      final summary = StationSummary.fromJson(json);
      await store.saveSummary(stationId, jsonEncode(json));
      return SummaryResult(summary, false);
    } on DioException catch (error) {
      final status = error.response?.statusCode;
      if (status == 403 || status == 404) {
        await store.deleteSummary(stationId);
        await store.deleteHistoryForStation(stationId);
        rethrow;
      }
      if (status != null && status != 503) rethrow;
      final cached = await store.readSummary(stationId);
      if (cached == null) rethrow;
      try {
        return SummaryResult(StationSummary.fromEncoded(cached), true);
      } on Object {
        await store.deleteSummary(stationId);
        rethrow;
      }
    }
  }

  Future<HistoryResult> loadHistory(String stationId, String seriesId) async {
    try {
      final json = await api.historyJson(seriesId);
      final history = SeriesHistory.fromJson(json);
      if (history.seriesId != seriesId) {
        throw const FormatException('History series ID mismatch.');
      }
      await store.saveHistory(stationId, seriesId, jsonEncode(json));
      return HistoryResult(history, false);
    } on DioException catch (error) {
      final status = error.response?.statusCode;
      if (status == 403 || status == 404) {
        await store.deleteHistory(seriesId);
        rethrow;
      }
      if (status != null && status != 503) rethrow;
      final cached = await store.readHistory(seriesId);
      if (cached == null) rethrow;
      try {
        final history = SeriesHistory.fromEncoded(cached);
        if (history.seriesId != seriesId) {
          throw const FormatException('History series ID mismatch.');
        }
        return HistoryResult(history, true);
      } on Object {
        await store.deleteHistory(seriesId);
        rethrow;
      }
    }
  }
}
