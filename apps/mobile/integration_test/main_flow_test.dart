import 'package:alertario_mobile/main.dart';
import 'package:alertario_mobile/src/store.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('search, station summary, recent chart and favorite', (
    tester,
  ) async {
    final store = LocalStore();
    await store.removeFavorite('station-demo');
    await tester.pumpWidget(
      ProviderScope(
        overrides: [storeProvider.overrideWithValue(store)],
        child: const AlertaRioApp(),
      ),
    );

    await tester.enterText(find.byType(TextField).first, 'ejemplo');
    await tester.tap(find.text('Buscar localidad'));
    await tester.pumpAndSettle();
    expect(find.text('Localidad de ejemplo'), findsOneWidget);

    await tester.tap(find.text('Localidad de ejemplo'));
    await tester.pumpAndSettle();
    expect(find.text('Estación de ejemplo'), findsOneWidget);

    await tester.tap(find.text('Estación de ejemplo'));
    await tester.pumpAndSettle();
    expect(find.text('DATOS SINTÉTICOS · solo para pruebas'), findsOneWidget);
    expect(find.text('Altura reciente · 24 horas'), findsOneWidget);

    await tester.tap(find.byTooltip('Guardar en favoritos'));
    await tester.pumpAndSettle();
    await tester.pageBack();
    await tester.pumpAndSettle();
    await tester.tap(find.text('Favoritos'));
    await tester.pumpAndSettle();
    expect(find.text('Estación de ejemplo'), findsOneWidget);
  });
}
