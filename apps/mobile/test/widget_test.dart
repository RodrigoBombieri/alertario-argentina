import 'package:alertario_mobile/main.dart';
import 'package:alertario_mobile/src/api.dart';
import 'package:alertario_mobile/src/models.dart';
import 'package:alertario_mobile/src/store.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class MemoryStore extends LocalStore {
  final saved = <String, String>{};
  final favoriteStations = <String, Station>{};

  @override
  Future<List<Station>> favorites() async => favoriteStations.values.toList();
  @override
  Future<bool> containsFavorite(String id) async =>
      favoriteStations.containsKey(id);
  @override
  Future<void> saveFavorite(Station station) async =>
      favoriteStations[station.id] = station;
  @override
  Future<void> removeFavorite(String id) async => favoriteStations.remove(id);
  @override
  Future<void> saveSummary(String id, String json) async => saved[id] = json;
  @override
  Future<String?> readSummary(String id) async => saved[id];
  @override
  Future<void> deleteSummary(String id) async => saved.remove(id);
}

class MemoryApi extends AlertaRioApi {
  MemoryApi() : super(baseUrl: 'https://example.invalid');
  bool offline = false;
  bool revoked = false;

  @override
  Future<Map<String, dynamic>> summaryJson(String id) async {
    if (revoked) {
      final request = RequestOptions(path: id);
      throw DioException(
        requestOptions: request,
        response: Response(requestOptions: request, statusCode: 404),
      );
    }
    if (offline) throw DioException(requestOptions: RequestOptions(path: id));
    return {
      'stationId': id,
      'generatedAt': '2026-10-03T12:00:00Z',
      'synthetic': true,
      'height': null,
      'discharge': null,
      'changes': [],
      'dataStatus': 'unavailable',
      'calculatedCondition': 'unavailable',
      'noticeCoverage': {'status': 'unavailable'},
      'notices': [],
      'dataVersion': 'fixture',
    };
  }
}

void main() {
  test(
    'offline uses the saved response without claiming notice coverage',
    () async {
      final api = MemoryApi();
      final store = MemoryStore();
      final repository = SummaryRepository(api, store);
      expect((await repository.load('station-1')).offline, false);
      api.offline = true;
      final cached = await repository.load('station-1');
      expect(cached.offline, true);
      expect(cached.summary.noticeCoverage, 'unavailable');
      expect(cached.summary.height, isNull);
    },
  );

  test('a revoked station removes its cached summary', () async {
    final api = MemoryApi();
    final store = MemoryStore();
    final repository = SummaryRepository(api, store);
    await repository.load('station-1');
    api.revoked = true;
    await expectLater(
      repository.load('station-1'),
      throwsA(isA<DioException>()),
    );
    expect(await store.readSummary('station-1'), isNull);
  });

  testWidgets('favorites empty state is explicit', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [storeProvider.overrideWithValue(MemoryStore())],
        child: const AlertaRioApp(),
      ),
    );
    await tester.tap(find.text('Favoritos'));
    await tester.pumpAndSettle();
    expect(find.text('Todavía no guardaste estaciones.'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
  });
}
