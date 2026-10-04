import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:maplibre_gl/maplibre_gl.dart';

import 'models.dart';

const mapStyleUrl = String.fromEnvironment('MAP_STYLE_URL');

bool get mapStyleConfigured {
  final uri = Uri.tryParse(mapStyleUrl);
  return uri != null &&
      uri.host.isNotEmpty &&
      (uri.scheme == 'https' || (!kReleaseMode && uri.scheme == 'http'));
}

class StationMap extends StatefulWidget {
  const StationMap({
    super.key,
    required this.stations,
    required this.onOpenStation,
  });

  final List<MapStation> stations;
  final Future<void> Function(Station) onOpenStation;

  @override
  State<StationMap> createState() => _StationMapState();
}

class _StationMapState extends State<StationMap> {
  MapLibreMapController? controller;
  bool styleReady = false;
  bool markersReady = false;
  bool timedOut = false;
  int revision = 0;

  @override
  void initState() {
    super.initState();
    Future<void>.delayed(const Duration(seconds: 12), () {
      if (mounted && !styleReady) {
        debugPrint('MapLibre: style did not load within 12 seconds.');
        setState(() => timedOut = true);
      }
    });
  }

  @override
  void didUpdateWidget(covariant StationMap oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (styleReady && oldWidget.stations != widget.stations) {
      _showStations();
    }
  }

  void _created(MapLibreMapController value) {
    controller = value;
    value.onCircleTapped.add(_openCircle);
  }

  void _openCircle(Circle circle) {
    final id = circle.data?['stationId'];
    for (final station in widget.stations) {
      if (station.id == id) {
        widget.onOpenStation(station);
        return;
      }
    }
  }

  Future<void> _showStations() async {
    final map = controller;
    if (map == null || !styleReady) return;
    final currentRevision = ++revision;
    if (mounted) setState(() => markersReady = false);
    try {
      await map.clearCircles();
      for (final station in widget.stations) {
        if (currentRevision != revision || !mounted) return;
        await map.addCircle(
          CircleOptions(
            geometry: LatLng(station.latitude, station.longitude),
            circleRadius: 8,
            circleColor: '#1769AA',
            circleStrokeColor: '#FFFFFF',
            circleStrokeWidth: 2,
          ),
          {'stationId': station.id},
        );
      }
      if (widget.stations.isEmpty || currentRevision != revision || !mounted) {
        return;
      }
      if (widget.stations.length == 1) {
        final station = widget.stations.single;
        await map.animateCamera(
          CameraUpdate.newLatLngZoom(
            LatLng(station.latitude, station.longitude),
            11,
          ),
        );
      } else {
        final south = widget.stations.map((s) => s.latitude).reduce(math.min);
        final north = widget.stations.map((s) => s.latitude).reduce(math.max);
        final west = widget.stations.map((s) => s.longitude).reduce(math.min);
        final east = widget.stations.map((s) => s.longitude).reduce(math.max);
        await map.animateCamera(
          CameraUpdate.newLatLngBounds(
            LatLngBounds(
              southwest: LatLng(
                south == north ? south - 0.01 : south,
                west == east ? west - 0.01 : west,
              ),
              northeast: LatLng(
                south == north ? north + 0.01 : north,
                west == east ? east + 0.01 : east,
              ),
            ),
            left: 36,
            top: 36,
            right: 36,
            bottom: 36,
          ),
        );
      }
      if (currentRevision == revision && mounted) {
        setState(() => markersReady = true);
      }
    } on Object catch (cause) {
      debugPrint('MapLibre: could not show station points: $cause');
      if (mounted) setState(() => timedOut = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (timedOut) {
      return const Center(
        child: Text('Mapa no disponible. Usá la lista de estaciones.'),
      );
    }
    return Stack(
      children: [
        MapLibreMap(
          styleString: mapStyleUrl,
          initialCameraPosition: const CameraPosition(
            target: LatLng(-31.5, -58.5),
            zoom: 7,
          ),
          myLocationEnabled: false,
          onMapCreated: _created,
          onStyleLoadedCallback: () {
            if (!mounted) return;
            setState(() => styleReady = true);
            _showStations();
          },
        ),
        if (!styleReady) const Center(child: CircularProgressIndicator()),
        if (markersReady)
          Positioned(
            top: 8,
            left: 8,
            child: Chip(
              label: Text(
                '${widget.stations.length} '
                '${widget.stations.length == 1 ? 'estación' : 'estaciones'} en el mapa',
              ),
            ),
          ),
      ],
    );
  }
}
