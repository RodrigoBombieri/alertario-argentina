import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../main.dart';
import 'history_chart.dart';
import 'models.dart';
import 'notices.dart';
import 'station_map.dart';
import 'visuals.dart';

String utc(DateTime value) => readingTime(value);

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
  bool searched = false;
  String? dataMode;
  int tab = 0;

  @override
  void initState() {
    super.initState();
    _loadFavorites();
    _loadDataMode();
  }

  Future<void> _loadDataMode() async {
    try {
      final api = ref.read(apiProvider);
      final mode = await api.dataMode();
      if (mounted) setState(() => dataMode = mode);
      if (mode == 'collecting' || mode == 'synthetic') {
        final sample = await api.searchLocations('ejemplo');
        if (mounted && !searched && query.text.isEmpty) {
          setState(() {
            query.text = 'ejemplo';
            locations = sample;
          });
        }
      }
    } catch (_) {
      // Connectivity errors are reported by the requested data view.
    }
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
      searched = true;
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
    appBar: AppBar(
      leading: const Padding(padding: EdgeInsets.all(8), child: RiverMark()),
      title: const Text('AlertaRío'),
      actions: [
        IconButton(
          tooltip: 'Avisos de seguimiento',
          icon: const Icon(Icons.notifications_outlined),
          onPressed:
              () => Navigator.of(context).push(
                MaterialPageRoute<void>(builder: (_) => const NoticesPage()),
              ),
        ),
      ],
    ),
    body: SafeArea(
      child: switch (tab) {
        0 => _searchPage(),
        1 => _favoritesPage(),
        _ => MapListPage(
          onOpenStation: _openStation,
          showSample: dataMode == 'collecting' || dataMode == 'synthetic',
        ),
      },
    ),
    bottomNavigationBar: NavigationBar(
      selectedIndex: tab,
      onDestinationSelected: (value) => setState(() => tab = value),
      destinations: const [
        NavigationDestination(
          icon: Icon(Icons.search_rounded),
          selectedIcon: Icon(Icons.travel_explore_rounded),
          label: 'Buscar',
        ),
        NavigationDestination(
          icon: Icon(Icons.star_border_rounded),
          selectedIcon: Icon(Icons.star_rounded),
          label: 'Favoritos',
        ),
        NavigationDestination(
          icon: Icon(Icons.explore_outlined),
          selectedIcon: Icon(Icons.explore_rounded),
          label: 'Explorar',
        ),
      ],
    ),
  );

  Widget _searchPage() => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      const RiverHero(
        eyebrow: 'OBSERVAR · ENTENDER · SEGUIR',
        title: 'Tu río,\nen perspectiva.',
        subtitle: 'Consultá sus niveles y explorá cómo cambian en el tiempo.',
      ),
      if (dataMode == 'collecting' || dataMode == 'synthetic')
        const SampleNotice(home: true),
      if (dataMode == 'awaitingData')
        const Padding(
          padding: EdgeInsets.symmetric(vertical: 16),
          child: Text(
            'Todavía no hay mediciones reales habilitadas. Las estaciones aparecerán cuando se verifique su información.',
          ),
        ),
      const SizedBox(height: 12),
      Text(
        'Encontrá tu localidad',
        style: Theme.of(context).textTheme.titleMedium,
      ),
      const SizedBox(height: 12),
      TextField(
        controller: query,
        textInputAction: TextInputAction.search,
        decoration: const InputDecoration(
          labelText: 'Localidad',
          prefixIcon: Icon(Icons.search_rounded),
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
        Padding(
          padding: const EdgeInsets.all(16),
          child: Text(
            !searched
                ? 'Sin resultados cargados.'
                : dataMode == 'collecting'
                ? 'Todavía no hay localidades habilitadas.'
                : 'No se encontraron localidades con ese nombre.',
          ),
        ),
      const SizedBox(height: 18),
      for (final location in locations)
        Card(
          elevation: 0,
          color: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(18),
          ),
          child: ListTile(
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 10,
            ),
            leading: const CircleAvatar(
              backgroundColor: Color(0xFFE0F1F1),
              child: Icon(Icons.place_outlined, color: riverTeal),
            ),
            title: Text(location.name),
            subtitle: Text(location.provinceName),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _selectLocation(location),
          ),
        ),
      if (!loading && stations.isEmpty && locations.isNotEmpty)
        const Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'Elegí una localidad para ver sus estaciones. La más cercana no siempre representa tu río.',
          ),
        ),
      for (final station in stations) _stationTile(station),
    ],
  );

  Widget _favoritesPage() => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      const RiverHero(
        eyebrow: 'TU SEGUIMIENTO',
        title: 'Tus ríos, a mano.',
        subtitle:
            'Estaciones guardadas en este dispositivo, sin cuenta ni GPS.',
      ),
      const SizedBox(height: 20),
      if (dataMode == 'collecting' || dataMode == 'synthetic')
        const SampleNotice(),
      if (error != null) Text(error!),
      if (favorites.isEmpty)
        const Padding(
          padding: EdgeInsets.all(16),
          child: Text('Todavía no guardaste estaciones.'),
        ),
      for (final station in favorites) _stationTile(station),
    ],
  );

  Widget _stationTile(Station station) => Card(
    elevation: 0,
    color: Colors.white,
    child: ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      leading: const CircleAvatar(
        backgroundColor: Color(0xFFE0F1F1),
        child: Icon(Icons.waves_rounded, color: riverTeal),
      ),
      title: Text(station.name),
      subtitle: Text(
        station.riverName.isEmpty ? 'Río no informado' : station.riverName,
      ),
      trailing: const Icon(Icons.chevron_right),
      onTap: () => _openStation(station),
    ),
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
  HistoryResult? history;
  String? historyError;
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
      history = null;
      historyError = null;
    });
    try {
      final loaded = await ref.read(repositoryProvider).load(widget.station.id);
      if (mounted) setState(() => result = loaded);
      final seriesId = loaded.summary.height?.seriesId;
      if (seriesId != null) {
        try {
          final loadedHistory = await ref
              .read(repositoryProvider)
              .loadHistory(widget.station.id, seriesId);
          if (mounted) setState(() => history = loadedHistory);
        } catch (cause) {
          if (mounted) {
            setState(
              () => historyError = 'No se pudo cargar el gráfico reciente.',
            );
          }
        }
      }
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
            tooltip: 'Avisos de seguimiento',
            icon: const Icon(Icons.notifications_outlined),
            onPressed:
                () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => NoticesPage(station: widget.station),
                  ),
                ),
          ),
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
              if (summary.synthetic) const SampleNotice(),
              const SizedBox(height: 12),
              _measurement('Altura', summary.height),
              _measurement('Caudal', summary.discharge),
              const SizedBox(height: 12),
              RiverPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Altura reciente · 24 horas',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 16),
                    if (history?.offline == true)
                      const Text(
                        'Gráfico guardado: puede estar desactualizado.',
                      ),
                    if (historyError != null) Text(historyError!),
                    if (history != null)
                      HistoryChart(history: history!.history),
                  ],
                ),
              ),
              if (summary.height != null)
                TextButton(
                  onPressed:
                      () => Navigator.of(context).push(
                        MaterialPageRoute<void>(
                          builder:
                              (_) => ExtendedHistoryPage(
                                seriesId: summary.height!.seriesId,
                                sourceId: summary.height!.sourceId,
                              ),
                        ),
                      ),
                  child: const Text('Ver historial'),
                ),
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
                        ? statusLabel(change.reason ?? change.availability)
                        : 'Cambio observado: ${change.delta! >= 0 ? '+' : ''}${change.delta!.toStringAsFixed(2)} ${change.unit} · ${statusLabel(change.trend)}',
                  ),
                  leading: const Icon(Icons.timeline_rounded, color: riverTeal),
                ),
              const SizedBox(height: 12),
              Text('Estado del dato: ${statusLabel(summary.dataStatus)}'),
              Text(
                'Comparación calculada: ${statusLabel(summary.calculatedCondition)}',
              ),
              Text(
                'Avisos oficiales: ${summary.noticeCoverage == 'complete' && summary.notices.isEmpty ? 'sin avisos vigentes verificados' : 'cobertura no confirmada'}',
              ),
              const SizedBox(height: 8),
              ExpansionTile(
                title: const Text('Detalles de los datos'),
                children: [
                  ListTile(
                    title: Text(
                      'Respuesta generada: ${utc(summary.generatedAt)}',
                    ),
                  ),
                  ListTile(
                    title: Text('Versión de datos: ${summary.dataVersion}'),
                  ),
                  ListTile(
                    title: Text(
                      'Estado original: ${summary.dataStatus} · ${summary.calculatedCondition}',
                    ),
                  ),
                ],
              ),
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

  Widget _measurement(String label, Measurement? measurement) {
    if (label == 'Altura' && measurement != null) {
      return Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: RiverHero(
          eyebrow: 'ALTURA DEL RÍO',
          title: '${measurement.value.toStringAsFixed(2)} ${measurement.unit}',
          subtitle:
              'Medido: ${utc(measurement.observedAt)}\n'
              '${sourceLabel(measurement.sourceId)} · ${statusLabel(measurement.freshness)}',
        ),
      );
    }
    return RiverPanel(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.water_outlined, color: riverTeal),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 4),
                Text(
                  measurement == null
                      ? 'No disponible'
                      : '${measurement.value.toStringAsFixed(2)} ${measurement.unit}',
                  style: const TextStyle(fontSize: 18, color: riverInk),
                ),
                if (measurement != null) ...[
                  Text('Medido: ${utc(measurement.observedAt)}'),
                  Text(
                    'Fuente: ${sourceLabel(measurement.sourceId)} · ${statusLabel(measurement.freshness)}',
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class ExtendedHistoryPage extends ConsumerStatefulWidget {
  const ExtendedHistoryPage({super.key, required this.seriesId, this.sourceId});

  final String seriesId;
  final String? sourceId;

  @override
  ConsumerState<ExtendedHistoryPage> createState() =>
      _ExtendedHistoryPageState();
}

class _ExtendedHistoryPageState extends ConsumerState<ExtendedHistoryPage> {
  late DateTime to;
  late DateTime from;
  int rangeDays = 7;
  int requestGeneration = 0;
  final List<HistoryPoint> points = [];
  String? cursor;
  String? error;
  String? unit;
  int? cadenceSeconds;
  DateTime? generatedAt;
  bool synthetic = false;
  bool loading = false;
  bool loaded = false;

  @override
  void initState() {
    super.initState();
    to = DateTime.now().toUtc();
    from = to.subtract(Duration(days: rangeDays));
    _load();
  }

  void _selectRange(int days) {
    if (days == rangeDays) return;
    requestGeneration++;
    setState(() {
      rangeDays = days;
      to = DateTime.now().toUtc();
      from = to.subtract(Duration(days: days));
      points.clear();
      cursor = null;
      error = null;
      unit = null;
      cadenceSeconds = null;
      generatedAt = null;
      synthetic = false;
      loaded = false;
      loading = false;
    });
    _load();
  }

  Future<void> _load() async {
    if (loading || loaded && cursor == null) return;
    final generation = requestGeneration;
    final requestedFrom = from;
    final requestedTo = to;
    final requestedCursor = cursor;
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final page = await ref
          .read(apiProvider)
          .historyPage(
            widget.seriesId,
            requestedFrom,
            requestedTo,
            requestedCursor,
          );
      if (!mounted || generation != requestGeneration) return;
      if (page.seriesId != widget.seriesId ||
          page.from.toUtc() != requestedFrom ||
          page.to.toUtc() != requestedTo ||
          unit != null && page.unit != unit ||
          loaded && page.cadenceSeconds != cadenceSeconds ||
          loaded && page.synthetic != synthetic ||
          points.isNotEmpty &&
              page.points.isNotEmpty &&
              !page.points.first.observedAt.isBefore(points.last.observedAt)) {
        throw const FormatException('Inconsistent history page.');
      }
      setState(() {
        points.addAll(page.points);
        cursor = page.nextCursor;
        unit = page.unit;
        cadenceSeconds = page.cadenceSeconds;
        generatedAt = page.generatedAt;
        synthetic = page.synthetic;
        loaded = true;
      });
    } catch (_) {
      if (mounted && generation == requestGeneration) {
        setState(
          () =>
              error =
                  'No se pudo cargar el historial. Revisá la conexión e intentá de nuevo.',
        );
      }
    } finally {
      if (mounted && generation == requestGeneration) {
        setState(() => loading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final chartPoints = points.take(2000).toList().reversed.toList();
    final chart = SeriesHistory(
      widget.seriesId,
      unit ?? '',
      cadenceSeconds,
      generatedAt ?? to,
      points.length > 2000,
      chartPoints,
    );
    return Scaffold(
      appBar: AppBar(title: const Text('Historial')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          SegmentedButton<int>(
            segments: const [
              ButtonSegment(value: 1, label: Text('24 h')),
              ButtonSegment(value: 7, label: Text('7 días')),
              ButtonSegment(value: 30, label: Text('30 días')),
            ],
            selected: {rangeDays},
            onSelectionChanged: (selection) => _selectRange(selection.single),
          ),
          const SizedBox(height: 12),
          Text(
            'Evolución del nivel',
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w700,
              color: riverInk,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            '${utc(from)} → ${utc(to)}',
            style: const TextStyle(fontSize: 12),
          ),
          if (widget.sourceId != null)
            Text(
              'Fuente de la serie: ${sourceLabel(widget.sourceId!)}',
              style: const TextStyle(fontSize: 12),
            ),
          if (synthetic) const SampleNotice(),
          if (loading) const LinearProgressIndicator(),
          if (error != null) Text(error!),
          if (loaded && points.isEmpty)
            const Text('No hay lecturas en este rango.'),
          if (points.isNotEmpty) ...[
            Text(
              'Gráfico de ${chartPoints.length} '
              '${chartPoints.length == 1 ? 'lectura cargada' : 'lecturas cargadas'}',
            ),
            if (cursor != null)
              const Text(
                'El gráfico es parcial. Cargá las páginas anteriores para ampliar el rango.',
              ),
            const SizedBox(height: 12),
            RiverPanel(
              child: HistoryChart(history: chart, showReadings: false),
            ),
            const SizedBox(height: 8),
            Text(
              'Registro de lecturas',
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ],
          for (final point in points)
            ExpansionTile(
              leading: const Icon(Icons.water_drop_outlined, color: riverTeal),
              title: Text(
                '${point.value.toStringAsFixed(2)} ${unit ?? ''}',
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: riverInk,
                ),
              ),
              subtitle: Text('Medido: ${utc(point.observedAt)}'),
              children: [
                ListTile(
                  title: Text(
                    point.sourceUpdatedAt == null
                        ? 'Hora de publicación de la fuente: no informada'
                        : 'Fuente publicó: ${utc(point.sourceUpdatedAt!)}',
                  ),
                ),
                ListTile(
                  title: Text(
                    point.ingestedAt == null
                        ? 'Hora de incorporación: no informada'
                        : 'AlertaRío incorporó: ${utc(point.ingestedAt!)}',
                  ),
                ),
                if (point.revision != null)
                  ListTile(title: Text('Revisión: ${point.revision}')),
              ],
            ),
          if (!loading && (!loaded || cursor != null))
            TextButton(
              onPressed: _load,
              child: Text(loaded ? 'Cargar lecturas anteriores' : 'Reintentar'),
            ),
        ],
      ),
    );
  }
}

class MapListPage extends ConsumerStatefulWidget {
  const MapListPage({
    super.key,
    required this.onOpenStation,
    this.showSample = false,
  });
  final bool showSample;
  final Future<void> Function(Station) onOpenStation;

  @override
  ConsumerState<MapListPage> createState() => _MapListPageState();
}

class _MapListPageState extends ConsumerState<MapListPage> {
  final bbox = TextEditingController(text: '-59,-32,-58,-31');
  List<MapStation> stations = [];
  String riverFilter = '';
  String provinceFilter = '';
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
      if (mounted) {
        setState(() {
          stations = result;
          if (!result.any((station) => station.riverName == riverFilter)) {
            riverFilter = '';
          }
          if (!result.any(
            (station) => station.provinceNames.contains(provinceFilter),
          )) {
            provinceFilter = '';
          }
        });
      }
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
  Widget build(BuildContext context) {
    final rivers =
        stations
            .map((station) => station.riverName)
            .where((river) => river.isNotEmpty)
            .toSet()
            .toList()
          ..sort();
    final provinces =
        stations.expand((station) => station.provinceNames).toSet().toList()
          ..sort();
    final visibleStations =
        stations
            .where(
              (station) =>
                  (riverFilter.isEmpty || station.riverName == riverFilter) &&
                  (provinceFilter.isEmpty ||
                      station.provinceNames.contains(provinceFilter)),
            )
            .toList();
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (widget.showSample) const SampleNotice(),
        Text(
          mapStyleConfigured
              ? 'Explorá las estaciones en el mapa o elegilas en la lista.'
              : 'Exploración en lista. El mapa requiere un proveedor de tiles autorizado.',
        ),
        if (mapStyleConfigured) ...[
          const SizedBox(height: 12),
          SizedBox(
            height: 320,
            child: StationMap(
              stations: visibleStations,
              onOpenStation: widget.onOpenStation,
            ),
          ),
        ],
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
        if (rivers.length > 1)
          InputDecorator(
            decoration: const InputDecoration(labelText: 'Filtrar por río'),
            child: DropdownButton<String>(
              value: riverFilter,
              isExpanded: true,
              items: [
                const DropdownMenuItem(
                  value: '',
                  child: Text('Todos los ríos'),
                ),
                for (final river in rivers)
                  DropdownMenuItem(
                    value: river,
                    child: Text(river.isEmpty ? 'Río no informado' : river),
                  ),
              ],
              onChanged: (value) => setState(() => riverFilter = value ?? ''),
            ),
          ),
        if (provinces.length > 1)
          InputDecorator(
            decoration: const InputDecoration(
              labelText: 'Filtrar por provincia asociada',
            ),
            child: DropdownButton<String>(
              value: provinceFilter,
              isExpanded: true,
              items: [
                const DropdownMenuItem(
                  value: '',
                  child: Text('Todas las provincias asociadas'),
                ),
                for (final province in provinces)
                  DropdownMenuItem(value: province, child: Text(province)),
              ],
              onChanged:
                  (value) => setState(() => provinceFilter = value ?? ''),
            ),
          ),
        if (!loading && stations.isEmpty && error == null)
          const Text('No hay estaciones cargadas en esta área.'),
        for (final station in visibleStations)
          ListTile(
            title: Text(station.name),
            subtitle: Text(
              '${station.riverName.isEmpty ? 'Río no informado' : station.riverName} · '
              '${station.provinceNames.isEmpty ? 'Sin provincia asociada' : station.provinceNames.join(', ')} · '
              '${station.latitude}, ${station.longitude}',
            ),
            onTap: () => widget.onOpenStation(station),
          ),
      ],
    );
  }
}
