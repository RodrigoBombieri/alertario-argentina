import 'package:sqflite/sqflite.dart';

import 'models.dart';

class LocalStore {
  LocalStore({this.databasePath});

  final String? databasePath;
  Future<Database>? _opening;

  Future<Database> get database => _opening ??= _open();

  Future<Database> _open() async {
    final path =
        databasePath ?? '${await getDatabasesPath()}/alertario_mobile.db';
    return openDatabase(
      path,
      version: 2,
      onCreate: (db, version) async {
        await db.execute('''
        CREATE TABLE favorites (
          station_id TEXT PRIMARY KEY, name TEXT NOT NULL,
          river_name TEXT NOT NULL, sort_order INTEGER NOT NULL)
      ''');
        await db.execute('''
        CREATE TABLE summary_cache (
          station_id TEXT PRIMARY KEY, schema_version INTEGER NOT NULL,
          payload TEXT NOT NULL)
      ''');
        await _createHistoryCache(db);
      },
      onUpgrade: (db, oldVersion, newVersion) async {
        if (oldVersion < 2) await _createHistoryCache(db);
      },
    );
  }

  static Future<void> _createHistoryCache(Database db) => db.execute('''
    CREATE TABLE history_cache (
      series_id TEXT PRIMARY KEY, station_id TEXT NOT NULL,
      schema_version INTEGER NOT NULL, payload TEXT NOT NULL)
  ''');

  Future<List<Station>> favorites() async {
    final rows = await (await database).query(
      'favorites',
      orderBy: 'sort_order DESC, station_id',
    );
    return rows
        .map(
          (row) => Station(
            row['station_id'] as String,
            row['name'] as String,
            row['river_name'] as String,
          ),
        )
        .toList();
  }

  Future<bool> containsFavorite(String stationId) async =>
      (await (await database).query(
        'favorites',
        columns: ['station_id'],
        where: 'station_id = ?',
        whereArgs: [stationId],
        limit: 1,
      )).isNotEmpty;

  Future<void> saveFavorite(Station station) async {
    final db = await database;
    await db.insert('favorites', {
      'station_id': station.id,
      'name': station.name,
      'river_name': station.riverName,
      'sort_order': DateTime.now().microsecondsSinceEpoch,
    }, conflictAlgorithm: ConflictAlgorithm.replace);
  }

  Future<void> removeFavorite(String stationId) async => (await database)
      .delete('favorites', where: 'station_id = ?', whereArgs: [stationId]);

  Future<void> saveSummary(String stationId, String json) async =>
      (await database).insert('summary_cache', {
        'station_id': stationId,
        'schema_version': 1,
        'payload': json,
      }, conflictAlgorithm: ConflictAlgorithm.replace);

  Future<String?> readSummary(String stationId) async {
    final rows = await (await database).query(
      'summary_cache',
      where: 'station_id = ? AND schema_version = 1',
      whereArgs: [stationId],
      limit: 1,
    );
    return rows.isEmpty ? null : rows.single['payload'] as String;
  }

  Future<void> deleteSummary(String stationId) async => (await database).delete(
    'summary_cache',
    where: 'station_id = ?',
    whereArgs: [stationId],
  );

  Future<void> saveHistory(
    String stationId,
    String seriesId,
    String json,
  ) async => (await database).insert('history_cache', {
    'series_id': seriesId,
    'station_id': stationId,
    'schema_version': 1,
    'payload': json,
  }, conflictAlgorithm: ConflictAlgorithm.replace);

  Future<String?> readHistory(String seriesId) async {
    final rows = await (await database).query(
      'history_cache',
      where: 'series_id = ? AND schema_version = 1',
      whereArgs: [seriesId],
      limit: 1,
    );
    return rows.isEmpty ? null : rows.single['payload'] as String;
  }

  Future<void> deleteHistory(String seriesId) async => (await database).delete(
    'history_cache',
    where: 'series_id = ?',
    whereArgs: [seriesId],
  );

  Future<void> deleteHistoryForStation(String stationId) async =>
      (await database).delete(
        'history_cache',
        where: 'station_id = ?',
        whereArgs: [stationId],
      );
}
