"""Summarize bounded INA observation probes without publishing measurement values."""

import argparse
import json
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from statistics import median
from urllib.parse import urlparse


def instant(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def summarize(paths):
    by_series = defaultdict(lambda: {"checks": [], "observations": defaultdict(list), "failures": []})
    for path in paths:
        doc = json.loads(path.read_text(encoding="utf-8-sig"))
        if doc.get("schema") != "alertario-f1-probe-v1" or doc.get("mode") != "Observations":
            raise ValueError(f"Unexpected probe document: {path}")
        for result in doc["results"]:
            parsed = urlparse(result["url"])
            series_id = parsed.path.split("/series/")[1].split("/")[0]
            group = by_series[series_id]
            group["checks"].append(instant(result["checkedAt"]))
            if result.get("status") != 200:
                group["failures"].append({"checkedAt": result["checkedAt"], "status": result.get("status")})
                continue
            for observation in result.get("observations", []):
                group["observations"][observation["id"]].append(observation)

    report = {}
    for series_id, group in sorted(by_series.items(), key=lambda pair: int(pair[0])):
        observations = [versions[-1] for versions in group["observations"].values()]
        times = sorted({instant(item["observedStart"]) for item in observations})
        gaps = [(b - a).total_seconds() / 3600 for a, b in zip(times, times[1:])]
        lags = [
            (instant(item["sourceUpdated"]) - instant(item["observedStart"])).total_seconds() / 3600
            for item in observations if item.get("sourceUpdated") and item.get("observedStart")
        ]
        changes = sum(len({v["fingerprint"] for v in versions}) > 1 for versions in group["observations"].values())
        checks = sorted(group["checks"])
        report[series_id] = {
            "firstCheckUtc": checks[0].astimezone(timezone.utc).isoformat(),
            "lastCheckUtc": checks[-1].astimezone(timezone.utc).isoformat(),
            "elapsedDays": round((checks[-1] - checks[0]).total_seconds() / 86400, 2),
            "checkCount": len(checks),
            "failedChecks": group["failures"],
            "distinctObservations": len(observations),
            "nullValues": sum(not item.get("valuePresent", False) for item in observations),
            "firstObservedUtc": times[0].isoformat() if times else None,
            "lastObservedUtc": times[-1].isoformat() if times else None,
            "observedGapHours": {"min": min(gaps), "median": median(gaps), "max": max(gaps)} if gaps else None,
            "sourceUpdateLagHoursMedian": round(median(lags), 2) if lags else None,
            "changedPayloadIds": changes,
            "note": "Observed gaps from the sampled window are not an approved publication cadence.",
        }
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", type=Path, help="Observation probe JSON files in chronological order")
    args = parser.parse_args()
    print(json.dumps(summarize(args.files), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
