import 'dart:convert';
import 'dart:io';

import 'package:alertario_mobile/src/notices.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  testWidgets('Android local episodes, replay, stale data and revocation', (
    tester,
  ) async {
    final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    var tick = DateTime.utc(2026, 10, 4, 12);
    var condition = 'noNotableChange';
    var synthetic = false;
    var status = 'current';
    var version = 1;
    var revoked = false;
    server.listen((request) async {
      request.response.headers.contentType = ContentType.json;
      request.response.statusCode = revoked ? 404 : 200;
      request.response.write(
        jsonEncode({
          'stationId': 'fixture-notice',
          'synthetic': synthetic,
          'generatedAt': tick.toIso8601String(),
          'dataStatus': status,
          'calculatedCondition': condition,
          'dataVersion': '$version:1',
          'height': {
            'quality': 'accepted',
            'freshness': 'fresh',
            'observedAt':
                tick.subtract(const Duration(seconds: 10)).toIso8601String(),
          },
        }),
      );
      await request.response.close();
    });
    const channel = DeviceNotices.channel;
    final base = 'http://127.0.0.1:${server.port}';
    Future<void> set(bool enabled) => channel.invokeMethod<void>('set', {
      'id': 'fixture-notice',
      'name': 'PRUEBA SINTÉTICA de notificaciones',
      'base': base,
      'enabled': enabled,
    });
    try {
      expect(
        (await DeviceNotices.state())['permission'],
        true,
        reason:
            'Grant POST_NOTIFICATIONS to the debug test APK before running.',
      );
      await set(false);
      await channel.invokeMethod<void>('clearHistory');
      await set(true);
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['history'] ?? [], isEmpty);
      tick = tick.add(const Duration(minutes: 5));
      version++;
      condition = 'aboveAlertThreshold';
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['history'], hasLength(1));
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['history'], hasLength(1));
      tick = tick.add(const Duration(minutes: 5));
      version++;
      status = 'stale';
      condition = 'aboveEvacuationThreshold';
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['history'], hasLength(1));
      status = 'current';
      synthetic = true;
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['history'], hasLength(1));
      synthetic = false;
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['history'], hasLength(2));
      revoked = true;
      await channel.invokeMethod<void>('checkNow');
      expect((await DeviceNotices.state())['rules'], isEmpty);
    } finally {
      await set(false);
      await channel.invokeMethod<void>('clearHistory');
      await server.close(force: true);
    }
  });
}
