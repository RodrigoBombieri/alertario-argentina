import 'dart:io';

import 'package:alertario_mobile/src/models.dart';
import 'package:alertario_mobile/src/store.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  test('SQLite keeps favorites and cached summaries after reopening', () async {
    final directory = await Directory.systemTemp.createTemp(
      'alertario-store-test-',
    );
    final path = '${directory.path}/mobile.db';
    try {
      final first = LocalStore(databasePath: path);
      await first.saveFavorite(const Station('station-1', 'Estación', 'Río'));
      await first.saveSummary('station-1', '{"stationId":"station-1"}');
      await first.saveHistory(
        'station-1',
        'series-1',
        '{"seriesId":"series-1"}',
      );
      await (await first.database).close();

      final second = LocalStore(databasePath: path);
      expect((await second.favorites()).single.id, 'station-1');
      expect(
        await second.readSummary('station-1'),
        '{"stationId":"station-1"}',
      );
      expect(await second.readHistory('series-1'), '{"seriesId":"series-1"}');
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

  test('version 1 cache upgrades without losing favorites', () async {
    final directory = await Directory.systemTemp.createTemp(
      'alertario-upgrade-test-',
    );
    final path = '${directory.path}/mobile.db';
    try {
      final old = await openDatabase(
        path,
        version: 1,
        onCreate: (db, version) async {
          await db.execute('''CREATE TABLE favorites (
          station_id TEXT PRIMARY KEY, name TEXT NOT NULL,
          river_name TEXT NOT NULL, sort_order INTEGER NOT NULL)''');
          await db.execute('''CREATE TABLE summary_cache (
          station_id TEXT PRIMARY KEY, schema_version INTEGER NOT NULL,
          payload TEXT NOT NULL)''');
        },
      );
      await old.insert('favorites', {
        'station_id': 'station-1',
        'name': 'Estación',
        'river_name': 'Río',
        'sort_order': 1,
      });
      await old.close();

      final upgraded = LocalStore(databasePath: path);
      expect((await upgraded.favorites()).single.id, 'station-1');
      await upgraded.saveHistory('station-1', 'series-1', '{}');
      expect(await upgraded.readHistory('series-1'), '{}');
      await (await upgraded.database).close();
    } finally {
      final tempRoot = Directory.systemTemp.absolute.path.toLowerCase();
      expect(directory.absolute.path.toLowerCase().startsWith(tempRoot), true);
      await directory.delete(recursive: true);
    }
  });
}
