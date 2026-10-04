import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'models.dart';

List<List<HistoryPoint>> contiguousHistorySegments(SeriesHistory history) {
  final segments = <List<HistoryPoint>>[];
  for (final point in history.points) {
    if (segments.isEmpty) {
      segments.add([point]);
      continue;
    }
    final current = segments.last;
    final gap = point.observedAt.difference(current.last.observedAt).inSeconds;
    if (history.cadenceSeconds == null ||
        gap <= 0 ||
        gap > history.cadenceSeconds! * 1.5) {
      segments.add([point]);
    } else {
      current.add(point);
    }
  }
  return segments;
}

class HistoryChart extends StatelessWidget {
  const HistoryChart({
    super.key,
    required this.history,
    this.showReadings = true,
  });
  final SeriesHistory history;
  final bool showReadings;

  @override
  Widget build(BuildContext context) {
    if (history.points.isEmpty) {
      return const Text('Sin lecturas aceptadas en las últimas 24 horas.');
    }
    final segments = contiguousHistorySegments(history);
    final first = history.points.first;
    final last = history.points.last;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Semantics(
          label:
              'Gráfico de ${history.points.length} lecturas en ${history.unit}, '
              'de ${first.observedAt.toUtc().toIso8601String()} a '
              '${last.observedAt.toUtc().toIso8601String()}, '
              '${segments.length} tramos separados por huecos.',
          child: SizedBox(
            height: 180,
            width: double.infinity,
            child: CustomPaint(
              painter: _HistoryPainter(
                history,
                Theme.of(context).colorScheme.primary,
              ),
            ),
          ),
        ),
        if (history.truncated)
          const Text(
            'Se muestran los 2.000 puntos más recientes; hay más lecturas en el período.',
          ),
        Text(
          'Desde ${first.observedAt.toUtc().toIso8601String()} UTC '
          'hasta ${last.observedAt.toUtc().toIso8601String()} UTC',
        ),
        if (showReadings)
          ExpansionTile(
            title: const Text('Lecturas en texto'),
            children: [
              for (final point in history.points.reversed.take(200))
                ListTile(
                  title: Text(
                    '${point.value.toStringAsFixed(2)} ${history.unit}',
                  ),
                  subtitle: Text(
                    '${point.observedAt.toUtc().toIso8601String()} UTC',
                  ),
                ),
              if (history.points.length > 200)
                const ListTile(
                  title: Text(
                    'La lista muestra las 200 lecturas más recientes.',
                  ),
                ),
            ],
          ),
      ],
    );
  }
}

class _HistoryPainter extends CustomPainter {
  const _HistoryPainter(this.history, this.color);
  final SeriesHistory history;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    const inset = 12.0;
    final values = history.points.map((point) => point.value);
    var minValue = values.reduce(math.min);
    var maxValue = values.reduce(math.max);
    if (minValue == maxValue) {
      minValue -= 0.5;
      maxValue += 0.5;
    }
    final firstTime = history.points.first.observedAt.microsecondsSinceEpoch;
    final lastTime = history.points.last.observedAt.microsecondsSinceEpoch;
    Offset position(HistoryPoint point) {
      final x =
          firstTime == lastTime
              ? size.width / 2
              : inset +
                  (point.observedAt.microsecondsSinceEpoch - firstTime) /
                      (lastTime - firstTime) *
                      (size.width - 2 * inset);
      final y =
          size.height -
          inset -
          (point.value - minValue) /
              (maxValue - minValue) *
              (size.height - 2 * inset);
      return Offset(x, y);
    }

    final paint =
        Paint()
          ..color = color
          ..strokeWidth = 2
          ..style = PaintingStyle.stroke;
    for (final segment in contiguousHistorySegments(history)) {
      if (segment.length == 1) {
        canvas.drawCircle(position(segment.single), 3, Paint()..color = color);
        continue;
      }
      final path =
          Path()
            ..moveTo(position(segment.first).dx, position(segment.first).dy);
      for (final point in segment.skip(1)) {
        final offset = position(point);
        path.lineTo(offset.dx, offset.dy);
      }
      canvas.drawPath(path, paint);
    }
  }

  @override
  bool shouldRepaint(covariant _HistoryPainter oldDelegate) =>
      oldDelegate.history != history || oldDelegate.color != color;
}
