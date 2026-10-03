import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../main.dart';
import 'models.dart';

String utc(DateTime value) => '${value.toUtc().toIso8601String()} UTC';

class HomePage extends ConsumerStatefulWidget {
  const HomePage({super.key});

  @override
  ConsumerState<HomePage> createState() => _HomePageState();
}

class _HomePageState extends ConsumerState<HomePage> {
  final query = TextEditingController();
  List<Location> locations = [];
  List<Station> stations = [];
  List<Station> favorites = [];
  String? error;
  bool loading = false;
  int tab = 0;

  @override
  void initState() {
    super.initState();
    _loadFavorites();
  }

  @override
  void dispose() {
    query.dispose();
    super.dispose();
  }

  Future<void> _loadFavorites() async {
    try {
      final list = await ref.read(storeProvider).favorites();
      if (mounted) setState(() => favorites = list);
    } catch (cause) {
      if (mounted) {
        setState(() => error = 'No se pudieron abrir los favoritos.');
      }
    }
  }

  Future<void> _search() async {
    if (query.text.trim().length < 2) {
      setState(() => error = 'Escribí al menos dos caracteres.');
      return;
    }
    setState(() {
      loading = true;
      error = null;
      stations = [];
    });
    try {
      final result = await ref
          .read(apiProvider)
          .searchLocations(query.text.trim());
      if (mounted) setState(() => locations = result);
    } catch (cause) {
      if (mounted) {
        setState(
          () => error = 'No se pudo buscar. Revisá la conexión y la API.',
        );
      }
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _selectLocation(Location location) async {
    setState(() {
      loading = true;
      error = null;
      stations = [];
    });
    try {
      final result = await ref
          .read(apiProvider)
          .stationsForLocation(location.id);
      if (mounted) setState(() => stations = result);
    } catch (cause) {
      if (mounted) {
        setState(() => error = 'No se pudieron cargar las estaciones.');
      }
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _openStation(Station station) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => StationPage(station: station)),
    );
    if (mounted) await _loadFavorites();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('AlertaRío Argentina')),
    body: SafeArea(
      child: switch (tab) {
        0 => _searchPage(),
        1 => _favoritesPage(),
        _ => MapListPage(onOpenStation: _openStation),
      },
    ),
    bottomNavigationBar: NavigationBar(
      selectedIndex: tab,
      onDestinationSelected: (value) => setState(() => tab = value),
      destinations: const [
        NavigationDestination(icon: Icon(Icons.search), label: 'Buscar'),
        NavigationDestination(icon: Icon(Icons.star), label: 'Favoritos'),
        NavigationDestination(icon: Icon(Icons.list_alt), label: 'Explorar'),
      ],
    ),
  );

  Widget _searchPage() => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      const Text(
        'Buscá una localidad y elegí una estación. La estación más cercana no siempre representa tu río.',
      ),
      const SizedBox(height: 12),
      TextField(
        controller: query,
        textInputAction: TextInputAction.search,
        decoration: const InputDecoration(
          labelText: 'Localidad',
          border: OutlineInputBorder(),
        ),
        onSubmitted: (_) => _search(),
      ),
      const SizedBox(height: 8),
      FilledButton(
        onPressed: loading ? null : _search,
        child: const Text('Buscar localidad'),
      ),
      if (loading) const LinearProgressIndicator(),
      if (error != null)
        Text(
          error!,
          style: TextStyle(color: Theme.of(context).colorScheme.error),
        ),
      if (!loading && locations.isEmpty && error == null)
        const Padding(
          padding: EdgeInsets.all(16),
          child: Text('Sin resultados cargados.'),
        ),
      for (final location in locations)
        ListTile(
          title: Text(location.name),
          subtitle: Text(location.provinceName),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => _selectLocation(location),
        ),
      if (!loading && stations.isEmpty && locations.isNotEmpty)
        const Padding(
          padding: EdgeInsets.all(16),
          child: Text('Elegí una localidad para ver sus estaciones.'),
        ),
      for (final station in stations) _stationTile(station),
    ],
  );

  Widget _favoritesPage() => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      const Text(
        'Estaciones guardadas en este dispositivo, sin cuenta ni GPS.',
      ),
      if (error != null) Text(error!),
      if (favorites.isEmpty)
        const Padding(
          padding: EdgeInsets.all(16),
          child: Text('Todavía no guardaste estaciones.'),
        ),
      for (final station in favorites) _stationTile(station),
    ],
  );

  Widget _stationTile(Station station) => ListTile(
    title: Text(station.name),
    subtitle: Text(
      station.riverName.isEmpty ? 'Río no informado' : station.riverName,
    ),
    trailing: const Icon(Icons.chevron_right),
    onTap: () => _openStation(station),
  );
}

class StationPage extends ConsumerStatefulWidget {
  const StationPage({super.key, required this.station});
  final Station station;

  @override
  ConsumerState<StationPage> createState() => _StationPageState();
}

class _StationPageState extends ConsumerState<StationPage> {
  SummaryResult? result;
  String? error;
  bool loading = true;
  bool favorite = false;

  @override
  void initState() {
    super.initState();
    _refresh();
    _loadFavorite();
  }

  Future<void> _loadFavorite() async {
    final saved = await ref
        .read(storeProvider)
        .containsFavorite(widget.station.id);
    if (mounted) setState(() => favorite = saved);
  }

  Future<void> _toggleFavorite() async {
    final store = ref.read(storeProvider);
    if (favorite) {
      await store.removeFavorite(widget.station.id);
    } else {
      await store.saveFavorite(widget.station);
    }
    if (mounted) setState(() => favorite = !favorite);
  }

  Future<void> _refresh() async {
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final loaded = await ref.read(repositoryProvider).load(widget.station.id);
      if (mounted) setState(() => result = loaded);
    } catch (cause) {
      if (mounted) {
        setState(
          () =>
              error = 'No hay una ficha disponible ni una copia local válida.',
        );
      }
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final summary = result?.summary;
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.station.name),
        actions: [
          IconButton(
            onPressed: _toggleFavorite,
            tooltip: favorite ? 'Quitar de favoritos' : 'Guardar en favoritos',
            icon: Icon(favorite ? Icons.star : Icons.star_border),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(
              widget.station.riverName.isEmpty
                  ? 'Río no informado'
                  : widget.station.riverName,
            ),
            if (loading) const LinearProgressIndicator(),
            if (error != null) Text(error!),
            if (result?.offline == true)
              const Card(
                child: Padding(
                  padding: EdgeInsets.all(12),
                  child: Text(
                    'Sin conexión: se muestra la última copia guardada. No hay avisos nuevos verificados.',
                  ),
                ),
              ),
            if (summary != null) ...[
              if (summary.synthetic)
                const Text('DATOS SINTÉTICOS · solo para pruebas'),
              const SizedBox(height: 12),
              _measurement('Altura', summary.height),
              _measurement('Caudal', summary.discharge),
              const SizedBox(height: 12),
              Text(
                'Variaciones por ventana',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              for (final change in summary.changes)
                ListTile(
                  title: Text('${change.windowHours} h'),
                  subtitle: Text(
                    change.delta == null
                        ? 'No disponible: ${change.reason ?? change.availability}'
                        : 'Cambio observado: ${change.delta! >= 0 ? '+' : ''}${change.delta!.toStringAsFixed(2)} ${change.unit}',
                  ),
                  trailing: Text(change.trend),
                ),
              const SizedBox(height: 12),
              Text('Estado del dato: ${summary.dataStatus}'),
              Text('Comparación calculada: ${summary.calculatedCondition}'),
              Text(
                'Avisos oficiales: ${summary.noticeCoverage == 'complete' && summary.notices.isEmpty ? 'sin avisos vigentes verificados' : 'cobertura no confirmada (${summary.noticeCoverage})'}',
              ),
              const SizedBox(height: 8),
              Text('Respuesta generada: ${utc(summary.generatedAt)}'),
              Text('Versión de datos: ${summary.dataVersion}'),
              if (result?.offline == true)
                const Text(
                  'La hora del dispositivo puede ser incorrecta; consultá la hora absoluta de medición.',
                ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _measurement(String label, Measurement? measurement) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.titleMedium),
          Text(
            measurement == null
                ? 'No disponible'
                : '${measurement.value.toStringAsFixed(2)} ${measurement.unit}',
            style: Theme.of(context).textTheme.headlineMedium,
          ),
          if (measurement != null) ...[
            Text('Medido: ${utc(measurement.observedAt)}'),
            Text(
              'Fuente: ${measurement.sourceId} · calidad: ${measurement.freshness}',
            ),
          ],
        ],
      ),
    ),
  );
}

class MapListPage extends ConsumerStatefulWidget {
  const MapListPage({super.key, required this.onOpenStation});
  final Future<void> Function(Station) onOpenStation;

  @override
  ConsumerState<MapListPage> createState() => _MapListPageState();
}

class _MapListPageState extends ConsumerState<MapListPage> {
  final bbox = TextEditingController(text: '-59,-32,-58,-31');
  List<MapStation> stations = [];
  String? error;
  bool loading = false;

  @override
  void dispose() {
    bbox.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final result = await ref
          .read(apiProvider)
          .stationsInBounds(bbox.text.trim());
      if (mounted) setState(() => stations = result);
    } catch (cause) {
      if (mounted) {
        setState(
          () =>
              error = 'No se pudo cargar esta área. Revisá bbox o la conexión.',
        );
      }
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      const Text(
        'Exploración en lista. El mapa con tiles espera un proveedor autorizado.',
      ),
      const SizedBox(height: 12),
      TextField(
        controller: bbox,
        decoration: const InputDecoration(
          labelText: 'Área: oeste,sur,este,norte',
          border: OutlineInputBorder(),
        ),
      ),
      const SizedBox(height: 8),
      FilledButton(
        onPressed: loading ? null : _load,
        child: const Text('Buscar estaciones en el área'),
      ),
      if (loading) const LinearProgressIndicator(),
      if (error != null) Text(error!),
      if (!loading && stations.isEmpty && error == null)
        const Text('No hay estaciones cargadas en esta área.'),
      for (final station in stations)
        ListTile(
          title: Text(station.name),
          subtitle: Text(
            '${station.riverName.isEmpty ? 'Río no informado' : station.riverName} · ${station.latitude}, ${station.longitude}',
          ),
          onTap: () => widget.onOpenStation(station),
        ),
    ],
  );
}
