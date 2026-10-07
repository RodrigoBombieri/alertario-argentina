import 'dart:convert';

import 'package:alertario_mobile/main.dart';
import 'package:alertario_mobile/src/api.dart';
import 'package:alertario_mobile/src/history_chart.dart';
import 'package:alertario_mobile/src/models.dart';
import 'package:alertario_mobile/src/notices.dart';
import 'package:alertario_mobile/src/pages.dart';
import 'package:alertario_mobile/src/store.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class MemoryStore extends LocalStore {
  final saved = <String, String>{};
  final savedHistory = <String, String>{};
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
  @override
  Future<void> saveHistory(
    String stationId,
    String seriesId,
    String json,
  ) async => savedHistory[seriesId] = json;
  @override
  Future<String?> readHistory(String seriesId) async => savedHistory[seriesId];
  @override
  Future<void> deleteHistory(String seriesId) async =>
      savedHistory.remove(seriesId);
  @override
  Future<void> deleteHistoryForStation(String stationId) async =>
      savedHistory.clear();
}

class MemoryApi extends AlertaRioApi {
  MemoryApi() : super(baseUrl: 'https://example.invalid');
  bool offline = false;
  bool revoked = false;
  String mode = 'synthetic';
  List<MapStation> mapStations = [];
  final requestedHistoryDays = <int>[];

  @override
  Future<String> dataMode() async => mode;

  @override
  Future<List<Location>> searchLocations(String query) async =>
      mode == 'collecting' && query == 'ejemplo'
          ? [
            const Location(
              'location-demo',
              'Localidad de ejemplo',
              'Provincia de ejemplo',
            ),
          ]
          : [];

  @override
  Future<List<MapStation>> stationsInBounds(String bbox) async => mapStations;

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

  @override
  Future<Map<String, dynamic>> historyJson(String id) async {
    if (offline) throw DioException(requestOptions: RequestOptions(path: id));
    return {
      'seriesId': id,
      'unit': 'm',
      'cadenceSeconds': 3600,
      'generatedAt': '2026-10-03T12:00:00Z',
      'truncated': false,
      'points': [
        {'observedAt': '2026-10-03T06:00:00Z', 'value': 7.2},
        {'observedAt': '2026-10-03T07:00:00Z', 'value': 7.3},
        {'observedAt': '2026-10-03T12:00:00Z', 'value': 7.5},
      ],
    };
  }

  @override
  Future<SeriesHistoryPage> historyPage(
    String seriesId,
    DateTime from,
    DateTime to,
    String? cursor,
  ) async {
    requestedHistoryDays.add(to.difference(from).inDays);
    final observedAt = to.subtract(Duration(hours: cursor == null ? 1 : 2));
    final point = HistoryPoint(
      observedAt,
      cursor == null ? 7.5 : 7.3,
      sourceUpdatedAt: observedAt,
      ingestedAt: observedAt.add(const Duration(minutes: 2)),
      revision: 1,
    );
    return SeriesHistoryPage(
      seriesId,
      'm',
      3600,
      DateTime.now().toUtc(),
      true,
      from,
      to,
      cursor == null ? point.observedAt.toIso8601String() : null,
      [point],
    );
  }
}

void main() {
  testWidgets('awaiting real data never claims to show a synthetic sample', (
    tester,
  ) async {
    final api = MemoryApi()..mode = 'awaitingData';
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          apiProvider.overrideWithValue(api),
          storeProvider.overrideWithValue(MemoryStore()),
        ],
        child: const AlertaRioApp(),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      find.textContaining('Todavía no hay mediciones reales'),
      findsOneWidget,
    );
    expect(find.textContaining('14 días simulados'), findsNothing);
    final semantics = tester.ensureSemantics();
    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
    semantics.dispose();
  });

  testWidgets('notification permission denial keeps station disabled', (
    tester,
  ) async {
    final calls = <String>[];
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(DeviceNotices.channel, (
          MethodCall call,
        ) async {
          calls.add(call.method);
          if (call.method == 'state') {
            return jsonEncode({
              'permission': false,
              'rules': {},
              'history': [],
            });
          }
          if (call.method == 'permission') return false;
          return null;
        });
    addTearDown(
      () => TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(DeviceNotices.channel, null),
    );
    await tester.pumpWidget(
      const MaterialApp(
        home: NoticesPage(station: Station('id', 'Prueba', 'Río')),
      ),
    );
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byType(SwitchListTile));
    await tester.tap(find.byType(SwitchListTile));
    await tester.pumpAndSettle();
    expect(calls, contains('permission'));
    expect(calls, isNot(contains('set')));
    expect(
      tester.widget<SwitchListTile>(find.byType(SwitchListTile)).value,
      false,
    );
  });

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

  test('history cache preserves gaps while offline', () async {
    final api = MemoryApi();
    final store = MemoryStore();
    final repository = SummaryRepository(api, store);
    expect(
      (await repository.loadHistory('station-1', 'series-1')).offline,
      false,
    );
    api.offline = true;
    final cached = await repository.loadHistory('station-1', 'series-1');
    expect(cached.offline, true);
    final segments = contiguousHistorySegments(cached.history);
    expect(segments.length, 2);
    expect(segments.first.length, 2);
    expect(segments.last.length, 1);
  });

  test('history rejects unordered or non-finite cached readings', () {
    final history = {
      'seriesId': 'series-1',
      'unit': 'm',
      'cadenceSeconds': 3600,
      'generatedAt': '2026-10-03T12:00:00Z',
      'truncated': false,
      'points': [
        {'observedAt': '2026-10-03T12:00:00Z', 'value': 7.5},
        {'observedAt': '2026-10-03T11:00:00Z', 'value': 7.4},
      ],
    };
    expect(() => SeriesHistory.fromJson(history), throwsFormatException);
    (history['points'] as List)[1] = {
      'observedAt': '2026-10-03T13:00:00Z',
      'value': double.nan,
    };
    expect(() => SeriesHistory.fromJson(history), throwsFormatException);
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

  testWidgets('cold start explains the absence of official data', (
    tester,
  ) async {
    final api = MemoryApi()..mode = 'collecting';
    await tester.pumpWidget(
      ProviderScope(
        overrides: [apiProvider.overrideWithValue(api)],
        child: const AlertaRioApp(),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('14 días simulados'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Localidad de ejemplo'),
      180,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('Localidad de ejemplo'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.byType(TextField).first,
      -180,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.enterText(find.byType(TextField).first, 'ejemplo');
    await tester.ensureVisible(find.text('Buscar localidad'));
    await tester.tap(find.text('Buscar localidad'));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.text('Localidad de ejemplo'),
      180,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('Localidad de ejemplo'), findsOneWidget);
    expect(find.textContaining('sin avisos vigentes'), findsNothing);
  });

  testWidgets('home stays usable with doubled system text size', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(400, 800);
    tester.view.devicePixelRatio = 1;
    tester.binding.platformDispatcher.textScaleFactorTestValue = 2;
    addTearDown(() {
      tester.view.resetPhysicalSize();
      tester.view.resetDevicePixelRatio();
      tester.binding.platformDispatcher.clearTextScaleFactorTestValue();
    });
    await tester.pumpWidget(
      ProviderScope(
        overrides: [apiProvider.overrideWithValue(MemoryApi())],
        child: const AlertaRioApp(),
      ),
    );
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.byType(TextField),
      250,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.byType(TextField), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Buscar localidad'),
      150,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('Buscar localidad'), findsOneWidget);
    await tester.tap(find.text('Favoritos'));
    await tester.pumpAndSettle();
    expect(find.text('Todavía no guardaste estaciones.'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('map without an approved style keeps the station list', (
    tester,
  ) async {
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          home: Scaffold(body: MapListPage(onOpenStation: (_) async {})),
        ),
      ),
    );
    expect(
      find.textContaining('proveedor de tiles autorizado'),
      findsOneWidget,
    );
    expect(find.text('Buscar estaciones en el área'), findsOneWidget);
  });

  testWidgets('map filters only curated province associations', (tester) async {
    final api =
        MemoryApi()
          ..mapStations = const [
            MapStation(
              'a',
              'Estación A',
              'Río A',
              -58,
              -31,
              provinceNames: ['Entre Ríos'],
            ),
            MapStation(
              'b',
              'Estación B',
              'Río B',
              -59,
              -32,
              provinceNames: ['Santa Fe'],
            ),
            MapStation(
              'c',
              'Estación C',
              'Río C',
              -58.5,
              -31.5,
              provinceNames: ['Entre Ríos', 'Santa Fe'],
            ),
          ];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [apiProvider.overrideWithValue(api)],
        child: MaterialApp(
          home: Scaffold(body: MapListPage(onOpenStation: (_) async {})),
        ),
      ),
    );
    await tester.tap(find.text('Buscar estaciones en el área'));
    await tester.pumpAndSettle();
    expect(find.text('Estación A'), findsOneWidget);
    await tester.tap(find.text('Todas las provincias asociadas'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Santa Fe').last);
    await tester.pumpAndSettle();
    expect(find.text('Estación A'), findsNothing);
    expect(find.text('Estación B'), findsOneWidget);
    expect(find.text('Estación C'), findsOneWidget);
  });

  testWidgets(
    'extended history loads an older page without repeating a point',
    (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [apiProvider.overrideWithValue(MemoryApi())],
          child: const MaterialApp(
            home: ExtendedHistoryPage(seriesId: 'series-demo'),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('7.50 m'),
        250,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('7.50 m'), findsOneWidget);
      await tester.ensureVisible(find.text('7.50 m'));
      await tester.tap(find.text('7.50 m'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Fuente publicó:'), findsOneWidget);
      expect(find.textContaining('AlertaRío incorporó:'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Cargar lecturas anteriores'),
        300,
      );
      await tester.tap(find.text('Cargar lecturas anteriores'));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('7.50 m'),
        -250,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('7.50 m'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('7.30 m'),
        250,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('7.30 m'), findsOneWidget);
      expect(find.text('Cargar lecturas anteriores'), findsNothing);
    },
  );

  testWidgets('extended history changes range and rebuilds the chart', (
    tester,
  ) async {
    final api = MemoryApi();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [apiProvider.overrideWithValue(api)],
        child: const MaterialApp(
          home: ExtendedHistoryPage(seriesId: 'series-demo'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(api.requestedHistoryDays, [7]);
    expect(find.textContaining('Gráfico de 1 lectura'), findsOneWidget);
    await tester.tap(find.text('30 días'));
    await tester.pumpAndSettle();
    expect(api.requestedHistoryDays, [7, 30]);
    await tester.scrollUntilVisible(
      find.text('7.50 m'),
      250,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('7.50 m'), findsOneWidget);
    expect(find.text('7.30 m'), findsNothing);
    await tester.scrollUntilVisible(
      find.textContaining('gráfico es parcial'),
      -250,
    );
    expect(find.textContaining('gráfico es parcial'), findsOneWidget);
  });
}
