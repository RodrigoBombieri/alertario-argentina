import 'package:flutter/material.dart';

const riverInk = Color(0xFF143743);
const riverTeal = Color(0xFF087E86);
const riverMuted = Color(0xFF526B74);

String readingTime(DateTime value) {
  final date = value.toUtc();
  String two(int n) => n.toString().padLeft(2, '0');
  return '${two(date.day)}/${two(date.month)}/${date.year} · '
      '${two(date.hour)}:${two(date.minute)} UTC';
}

String sourceLabel(String value) =>
    value == 'source-demo' ? 'Muestra de AlertaRío' : value;

String statusLabel(String value) =>
    const {
      'fresh': 'Vigente',
      'stale': 'Desactualizado',
      'unknown': 'Sin información suficiente',
      'notApplicable': 'Dato de muestra',
      'unavailable': 'No disponible',
      'current': 'Actual',
      'noNotableChange': 'Sin cambios destacados',
      'followUp': 'Requiere seguimiento',
      'aboveAlertThreshold': 'Sobre referencia de alerta',
      'aboveEvacuationThreshold': 'Sobre referencia de evacuación',
      'rising': 'En suba',
      'falling': 'En baja',
      'stable': 'Estable',
      'insufficientObservations': 'Faltan lecturas comparables',
      'insufficientData': 'Faltan lecturas comparables',
      'historicalOnly': 'Comparación histórica',
      'freshnessUnknown': 'Vigencia no confirmada',
      'epsilonUnknown': 'Parámetro no aprobado',
      'incompatible': 'Lecturas no comparables',
    }[value] ??
    'Sin clasificación disponible';

class SampleNotice extends StatelessWidget {
  const SampleNotice({super.key, this.home = false});
  final bool home;

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.symmetric(vertical: 12),
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: const Color(0xFFFFF4DF),
      borderRadius: BorderRadius.circular(16),
      border: Border.all(color: const Color(0xFFEBDAB6)),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.science_outlined, color: Color(0xFF785318), size: 22),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            home
                ? 'DATOS DE MUESTRA · 14 días simulados. La recopilación real aún no está habilitada; no hay avisos oficiales verificados.'
                : 'DATOS SINTÉTICOS · solo para pruebas',
            style: const TextStyle(
              color: Color(0xFF674711),
              fontSize: 12,
              height: 1.5,
            ),
          ),
        ),
      ],
    ),
  );
}

class RiverPanel extends StatelessWidget {
  const RiverPanel({super.key, required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    margin: const EdgeInsets.only(bottom: 12),
    padding: const EdgeInsets.all(18),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(22),
      border: Border.all(color: const Color(0xFFE0E9EC)),
    ),
    child: child,
  );
}

class RiverHero extends StatelessWidget {
  const RiverHero({
    super.key,
    required this.eyebrow,
    required this.title,
    required this.subtitle,
  });
  final String eyebrow;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    clipBehavior: Clip.antiAlias,
    decoration: BoxDecoration(
      gradient: const LinearGradient(
        colors: [Color(0xFF123B49), Color(0xFF076D79)],
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
      ),
      borderRadius: BorderRadius.circular(26),
    ),
    child: CustomPaint(
      painter: const _RiverLines(),
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              eyebrow,
              style: const TextStyle(
                color: Color(0xFFB7E9E7),
                fontSize: 11,
                fontWeight: FontWeight.w700,
                letterSpacing: 1.8,
              ),
            ),
            const SizedBox(height: 12),
            Text(
              title,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 34,
                fontWeight: FontWeight.w700,
                height: 1.15,
                letterSpacing: -1,
              ),
            ),
            const SizedBox(height: 12),
            Text(
              subtitle,
              style: const TextStyle(
                color: Color(0xFFD7EBEE),
                fontSize: 13,
                height: 1.5,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

class _RiverLines extends CustomPainter {
  const _RiverLines();
  @override
  void paint(Canvas canvas, Size size) {
    final paint =
        Paint()
          ..color = Colors.white.withValues(alpha: .06)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.3;
    for (var i = 0; i < 6; i++) {
      final y = size.height * .35 + i * 16;
      canvas.drawPath(
        Path()
          ..moveTo(size.width * .55, y)
          ..cubicTo(
            size.width * .85,
            y - 50,
            size.width * .68,
            y + 100,
            size.width + 40,
            y + 20,
          ),
        paint,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _RiverLines oldDelegate) => false;
}

class RiverMark extends StatelessWidget {
  const RiverMark({super.key});
  @override
  Widget build(BuildContext context) => ExcludeSemantics(
    child: Container(
      width: 40,
      height: 40,
      decoration: BoxDecoration(
        color: riverInk,
        borderRadius: BorderRadius.circular(13),
      ),
      child: CustomPaint(painter: _RiverMarkPainter()),
    ),
  );
}

class _RiverMarkPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    canvas.save();
    canvas.scale(size.width / 108, size.height / 108);
    final paint =
        Paint()
          ..color = const Color(0xFF75E0D2)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 8
          ..strokeCap = StrokeCap.round;
    for (final y in [42.0, 65.0]) {
      canvas.drawPath(
        Path()
          ..moveTo(23, y)
          ..cubicTo(43, y - 23, 65, y + 23, 85, y),
        paint,
      );
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _RiverMarkPainter oldDelegate) => false;
}
