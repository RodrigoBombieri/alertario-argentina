import 'package:alertario_mobile/main.dart';
import 'package:alertario_mobile/src/api.dart';
import 'package:alertario_mobile/src/models.dart';
import 'package:alertario_mobile/src/pages.dart';
import 'package:alertario_mobile/src/station_map.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

class MapSmokeApi extends AlertaRioApi {
  MapSmokeApi() : super(baseUrl: 'https://example.invalid');

  @override
  Future<List<MapStation>> stationsInBounds(String bbox) async => [
    const MapStation(
      'station-map-test',
      'Estación de prueba',
      'Río de prueba',
      -58.5,
      -31.5,
    ),
  ];
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('MapLibre loads a local style and keeps list selection', (
    tester,
  ) async {
    expect(mapStyleConfigured, isTrue);
    Station? selected;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [apiProvider.overrideWithValue(MapSmokeApi())],
        child: MaterialApp(
          home: Scaffold(
            body: MapListPage(
              onOpenStation: (station) async {
                selected = station;
              },
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Buscar estaciones en el área'));
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.byType(StationMap), findsOneWidget);
    expect(find.text('Estación de prueba'), findsOneWidget);

    for (var attempt = 0; attempt < 20; attempt++) {
      if (find.text('1 estación en el mapa').evaluate().isNotEmpty) break;
      await tester.pump(const Duration(seconds: 1));
    }
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('1 estación en el mapa'), findsOneWidget);
    expect(find.textContaining('Mapa no disponible'), findsNothing);

    await tester.ensureVisible(find.text('Estación de prueba'));
    await tester.tap(find.text('Estación de prueba'));
    await tester.pump();
    expect(selected?.id, 'station-map-test');
  });
}
