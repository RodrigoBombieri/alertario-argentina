import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'src/api.dart';
import 'src/pages.dart';
import 'src/store.dart';

void main() => runApp(const ProviderScope(child: AlertaRioApp()));

final apiProvider = Provider<AlertaRioApi>((ref) => AlertaRioApi());
final storeProvider = Provider<LocalStore>((ref) => LocalStore());
final repositoryProvider = Provider<SummaryRepository>(
  (ref) => SummaryRepository(ref.watch(apiProvider), ref.watch(storeProvider)),
);

class AlertaRioApp extends StatelessWidget {
  const AlertaRioApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'AlertaRío Argentina',
    theme: ThemeData(
      colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF087F8C)),
      useMaterial3: true,
    ),
    home: const HomePage(),
  );
}
