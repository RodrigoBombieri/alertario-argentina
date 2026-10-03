import 'dart:io';

import 'package:alertario_mobile/src/models.dart';
import 'package:alertario_mobile/src/store.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  test('SQLite keeps favorites and cached summaries after reopening', () async {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
    final directory = await Directory.systemTemp.createTemp(
      'alertario-store-test-',
    );
    final path = '${directory.path}/mobile.db';
    try {
      final first = LocalStore(databasePath: path);
      await first.saveFavorite(const Station('station-1', 'Estación', 'Río'));
      await first.saveSummary('station-1', '{"stationId":"station-1"}');
      await (await first.database).close();

      final second = LocalStore(databasePath: path);
      expect((await second.favorites()).single.id, 'station-1');
      expect(
        await second.readSummary('station-1'),
        '{"stationId":"station-1"}',
      );
      await second.removeFavorite('station-1');
      await second.removeFavorite('station-1');
      expect(await second.favorites(), isEmpty);
      await (await second.database).close();
    } finally {
      final tempRoot = Directory.systemTemp.absolute.path.toLowerCase();
      expect(directory.absolute.path.toLowerCase().startsWith(tempRoot), true);
      await directory.delete(recursive: true);
    }
  });
}
