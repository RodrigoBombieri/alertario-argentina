import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'src/api.dart';
import 'src/pages.dart';
import 'src/store.dart';
import 'src/visuals.dart';

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
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      colorScheme: ColorScheme.fromSeed(
        seedColor: riverTeal,
        primary: riverTeal,
        surface: Colors.white,
        onSurface: riverInk,
      ),
      scaffoldBackgroundColor: const Color(0xFFF3F7F8),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFFF3F7F8),
        foregroundColor: riverInk,
        elevation: 0,
        scrolledUnderElevation: 0,
        titleTextStyle: TextStyle(
          color: riverInk,
          fontSize: 20,
          fontWeight: FontWeight.w700,
        ),
      ),
      textTheme: const TextTheme(
        bodyMedium: TextStyle(color: riverMuted, height: 1.5),
        titleMedium: TextStyle(
          color: riverInk,
          fontSize: 17,
          fontWeight: FontWeight.w700,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.all(18),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: Color(0xFFCEDDE1)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: Color(0xFFCEDDE1)),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(48, 52),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
          textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
        ),
      ),
      navigationBarTheme: const NavigationBarThemeData(
        backgroundColor: Colors.white,
        indicatorColor: Color(0xFFD8EEEE),
        elevation: 0,
      ),
      dividerTheme: const DividerThemeData(
        color: Color(0xFFE0E9EC),
        thickness: 1,
      ),
      useMaterial3: true,
    ),
    home: const HomePage(),
  );
}
