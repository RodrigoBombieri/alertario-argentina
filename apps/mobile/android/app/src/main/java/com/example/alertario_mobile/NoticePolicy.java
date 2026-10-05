package com.example.alertario_mobile;

/** Conservative device-side episode policy. Times come from the server, never the phone clock. */
public final class NoticePolicy {
    private NoticePolicy() {}

    public static boolean usable(boolean synthetic, String status, String quality,
            String freshness, long generated, long observed, String version) {
        return !synthetic && "current".equals(status) && "accepted".equals(quality)
            && "fresh".equals(freshness) && generated > 0 && observed > 0
            && observed <= generated && generated - observed < 3600000L
            && version != null && !version.isEmpty() && !"unavailable".equals(version);
    }

    public static boolean noteworthy(String condition) {
        return "followUp".equals(condition) || "aboveAlertThreshold".equals(condition)
            || "aboveEvacuationThreshold".equals(condition);
    }

    public static boolean shouldNotify(boolean initialized, String previousCondition,
            long lastObserved, long lastChecked, String condition, long observed) {
        // First observation establishes a baseline; corrections/backfill cannot open episodes.
        return initialized && noteworthy(condition) && !condition.equals(previousCondition)
            && observed > lastObserved && observed > lastChecked;
    }
}
