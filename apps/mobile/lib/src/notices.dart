import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'models.dart';

class DeviceNotices {
  static const channel = MethodChannel('ar.alertario/notices');
  static bool get supported =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;
  static Future<Map<String, dynamic>> state() async =>
      object(jsonDecode(await channel.invokeMethod<String>('state') ?? '{}'));
}

class NoticesPage extends StatefulWidget {
  const NoticesPage({super.key, this.station});
  final Station? station;
  @override
  State<NoticesPage> createState() => _NoticesPageState();
}

class _NoticesPageState extends State<NoticesPage> with WidgetsBindingObserver {
  Map<String, dynamic> state = {};
  String? error;
  bool busy = false;
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState value) {
    if (value == AppLifecycleState.resumed) _load();
  }

  Future<void> _load() async {
    if (!DeviceNotices.supported) return;
    try {
      final loaded = await DeviceNotices.state();
      if (mounted) setState(() => state = loaded);
    } catch (_) {
      if (mounted) setState(() => error = 'No se pudo leer la configuración.');
    }
  }

  Future<void> _action(Future<void> Function() run) async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await run();
      await _load();
    } on PlatformException catch (cause) {
      if (mounted) {
        setState(() => error = cause.message ?? 'No se pudo guardar.');
      }
    } catch (_) {
      if (mounted) setState(() => error = 'No se pudo completar la operación.');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> _set(String id, String name, bool enabled, {String? base}) =>
      _action(() async {
        if (enabled &&
            await DeviceNotices.channel.invokeMethod<bool>('permission') !=
                true) {
          throw PlatformException(
            code: 'permission',
            message: 'Permití las notificaciones en los ajustes de Android.',
          );
        }
        await DeviceNotices.channel.invokeMethod<void>('set', {
          'id': id,
          'name': name,
          'enabled': enabled,
          'base': base ?? const String.fromEnvironment('API_BASE_URL'),
        });
      });

  @override
  Widget build(BuildContext context) {
    final rules = object(state['rules'] ?? {});
    final history = (state['history'] as List? ?? []).map(object);
    final station = widget.station;
    return Scaffold(
      appBar: AppBar(title: const Text('Avisos de seguimiento')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text(
            'El teléfono consulta la API aproximadamente cada 15 minutos. Android puede demorar o suspender las consultas por batería, falta de red o cierre forzado. No es un servicio de emergencia.',
          ),
          const SizedBox(height: 12),
          const Text(
            'Avisa ante un nuevo cambio calculado que requiere seguimiento o al superar una referencia oficial de altura. No equivale a un aviso oficial ni a una orden de evacuación. La primera consulta establece la referencia; no avisa por episodios anteriores, muestras ni datos viejos.',
          ),
          if (!DeviceNotices.supported)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Text(
                'Los avisos locales están disponibles en Android 8 o posterior.',
              ),
            ),
          if (DeviceNotices.supported) ...[
            Text(
              'Permiso de Android: ${state['permission'] == true ? 'habilitado' : 'no habilitado'}',
            ),
            TextButton(
              onPressed:
                  busy
                      ? null
                      : () => _action(
                        () => DeviceNotices.channel.invokeMethod<void>(
                          'settings',
                        ),
                      ),
              child: const Text('Abrir ajustes de notificaciones'),
            ),
            if (station != null)
              SwitchListTile(
                title: Text('Seguir ${station.name}'),
                value: rules.containsKey(station.id),
                onChanged:
                    busy
                        ? null
                        : (value) => _set(station.id, station.name, value),
              ),
            for (final entry in rules.entries)
              ListTile(
                title: Text(object(entry.value)['name'] as String),
                subtitle: Text(
                  object(entry.value)['status'] as String? ??
                      'Esperando la primera consulta de Android.',
                ),
                trailing: IconButton(
                  tooltip: 'Dejar de seguir',
                  icon: const Icon(Icons.notifications_off_outlined),
                  onPressed:
                      busy
                          ? null
                          : () => _set(
                            entry.key,
                            object(entry.value)['name'] as String,
                            false,
                            base: object(entry.value)['base'] as String,
                          ),
                ),
              ),
            TextButton(
              onPressed:
                  busy || rules.isEmpty
                      ? null
                      : () => _action(
                        () => DeviceNotices.channel.invokeMethod<void>(
                          'checkNow',
                        ),
                      ),
              child: const Text('Consultar ahora'),
            ),
            if (rules.isEmpty)
              const Text(
                'Sin estaciones seguidas. Activá los avisos desde la ficha de una estación.',
              ),
            const Divider(),
            Text(
              'Historial local',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const Text(
              'Últimos 100 avisos preparados en este teléfono. Android puede bloquear su presentación. Borrar el historial no reactiva episodios.',
            ),
            for (final item in history)
              ListTile(
                title: Text(item['name'] as String),
                subtitle: Text(
                  '${item['text']}\nMedición: ${item['observedAt']}',
                ),
              ),
            if (history.isEmpty) const Text('Todavía no hay avisos.'),
            TextButton(
              onPressed:
                  busy
                      ? null
                      : () => _action(
                        () => DeviceNotices.channel.invokeMethod<void>(
                          'clearHistory',
                        ),
                      ),
              child: const Text('Borrar historial'),
            ),
            const Divider(),
            const Text(
              'La cobertura de avisos oficiales del SMN aún no está integrada. Consultá su sitio para verificar avisos vigentes.',
            ),
            TextButton(
              onPressed:
                  busy
                      ? null
                      : () => _action(
                        () => DeviceNotices.channel.invokeMethod<void>(
                          'official',
                        ),
                      ),
              child: const Text('Consultar avisos del SMN'),
            ),
          ],
          if (busy) const LinearProgressIndicator(),
          if (error != null)
            Text(
              error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
        ],
      ),
    );
  }
}
