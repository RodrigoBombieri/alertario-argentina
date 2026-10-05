package com.example.alertario_mobile

import android.app.*
import android.app.job.*
import android.content.*
import android.os.Build
import android.os.SystemClock
import org.json.JSONArray
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.time.Instant
import java.util.concurrent.Executors
import java.util.concurrent.Future

/** All registration, episode and history state stays on this device. No push tokens exist. */
object LocalNotices {
    private const val JOB_ID = 8140
    private const val CHANNEL = "river-observations"
    private val lock = Any()
    private fun prefs(context: Context) = context.getSharedPreferences("river-notices-v1", Context.MODE_PRIVATE)
    private fun read(context: Context) = JSONObject(prefs(context).getString("state", "{}")!!)
    private fun save(context: Context, state: JSONObject) {
        check(prefs(context).edit().putString("state", state.toString()).commit()) { "No se pudo guardar." }
    }
    fun allowed(context: Context): Boolean {
        val manager = context.getSystemService(NotificationManager::class.java)
        if (Build.VERSION.SDK_INT >= 24 && !manager.areNotificationsEnabled()) return false
        return Build.VERSION.SDK_INT < 26 || manager.getNotificationChannel(CHANNEL)?.importance != NotificationManager.IMPORTANCE_NONE
    }
    fun state(context: Context): String = synchronized(lock) {
        read(context).put("permission", allowed(context)).toString()
    }
    fun set(context: Context, id: String, name: String, base: String, enabled: Boolean) = synchronized(lock) {
        require(id.matches(Regex("[A-Za-z0-9_-]{1,100}")) && name.length in 1..160)
        val url = URL(base)
        require(url.protocol == "https" ||
            (context.applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE != 0 && url.protocol == "http"))
        require(url.userInfo == null && url.query == null && url.ref == null && url.host.isNotEmpty())
        val state = read(context)
        val rules = state.optJSONObject("rules") ?: JSONObject()
        if (enabled) {
            check(allowed(context)) { "Permití las notificaciones en los ajustes de Android." }
            require(rules.has(id) || rules.length() < 20) { "El máximo es 20 estaciones." }
            if (!rules.has(id) || rules.getJSONObject(id).optString("base") != base)
                rules.put(id, JSONObject().put("name", name).put("base", base).put("token", java.util.UUID.randomUUID().toString()))
        } else {
            rules.remove(id)
            context.getSystemService(NotificationManager::class.java).cancel(id, JOB_ID)
        }
        state.put("rules", rules)
        save(context, state)
        val scheduler = context.getSystemService(JobScheduler::class.java)
        if (rules.length() == 0) scheduler.cancel(JOB_ID)
        else if (scheduler.getPendingJob(JOB_ID) == null) {
            val job = JobInfo.Builder(JOB_ID, ComponentName(context, NoticeJobService::class.java))
                .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY)
                .setPeriodic(15 * 60 * 1000L).setPersisted(true).build()
            if (scheduler.schedule(job) != JobScheduler.RESULT_SUCCESS) {
                rules.remove(id)
                save(context, state)
                error("Android no pudo programar las consultas.")
            }
        }
    }
    fun clearHistory(context: Context) = synchronized(lock) {
        save(context, read(context).put("history", JSONArray()))
    }
    fun resume(context: Context) = synchronized(lock) {
        if (!allowed(context)) return@synchronized
        val rules = read(context).optJSONObject("rules") ?: return@synchronized
        if (rules.length() == 0) return@synchronized
        val id = rules.keys().next()
        val rule = rules.getJSONObject(id)
        try { set(context, id, rule.getString("name"), rule.getString("base"), true) }
        catch (_: Exception) { mark(context, id, "No se pudo programar; desactivá y activá el seguimiento") }
    }
    fun poll(context: Context, stopped: () -> Boolean) {
        val rules = synchronized(lock) { read(context).optJSONObject("rules") ?: JSONObject() }
        for (id in rules.keys()) {
            if (stopped() || !allowed(context)) return
            val rule = rules.getJSONObject(id)
            try {
                val started = SystemClock.elapsedRealtime()
                val first = fetch(rule.getString("base"), id)
                if (first == null) { revoke(context, id, rule); continue }
                if (!usable(first)) { mark(context, id, "Sin dato actual verificable"); continue }
                // Fetch again immediately before posting. Never send a cached/version-replaced signal.
                val verified = fetch(rule.getString("base"), id)
                if (verified == null) { revoke(context, id, rule); continue }
                if (stopped() || SystemClock.elapsedRealtime() - started > 20000 || !usable(verified) ||
                    first.getString("dataVersion") != verified.getString("dataVersion") ||
                    first.getString("calculatedCondition") != verified.getString("calculatedCondition")) continue
                apply(context, id, rule, verified, stopped)
            } catch (_: Exception) {
                mark(context, id, "Consulta fallida; se reintentará")
            }
        }
    }
    private fun time(value: String): Long = Instant.parse(value).toEpochMilli()
    private fun usable(json: JSONObject): Boolean {
        val height = json.optJSONObject("height") ?: return false
        return NoticePolicy.usable(json.getBoolean("synthetic"), json.getString("dataStatus"),
            height.getString("quality"), height.getString("freshness"), time(json.getString("generatedAt")),
            time(height.getString("observedAt")), json.getString("dataVersion")) &&
            (NoticePolicy.noteworthy(json.getString("calculatedCondition")) || json.getString("calculatedCondition") == "noNotableChange")
    }
    private fun fetch(base: String, id: String): JSONObject? {
        val connection = URL(URL(base), "/v1/stations/$id/summary").openConnection() as HttpURLConnection
        try {
            connection.connectTimeout = 8000
            connection.readTimeout = 8000
            connection.instanceFollowRedirects = false
            connection.useCaches = false
            connection.setRequestProperty("Cache-Control", "no-cache")
            if (connection.responseCode == 403 || connection.responseCode == 404) return null
            check(connection.responseCode == 200)
            val bytes = connection.inputStream.use { input ->
                val output = java.io.ByteArrayOutputStream()
                val buffer = ByteArray(4096)
                while (output.size() <= 65536) {
                    val count = input.read(buffer)
                    if (count < 0) break
                    output.write(buffer, 0, count)
                }
                output.toByteArray()
            }
            check(bytes.size <= 65536)
            return JSONObject(String(bytes, Charsets.UTF_8)).also { check(it.getString("stationId") == id) }
        } finally { connection.disconnect() }
    }
    private fun apply(context: Context, id: String, original: JSONObject, json: JSONObject, stopped: () -> Boolean) = synchronized(lock) {
        val state = read(context)
        val rule = state.optJSONObject("rules")?.optJSONObject(id) ?: return@synchronized
        if (stopped() || !allowed(context) || rule.optString("token") != original.optString("token")) return@synchronized
        val observed = time(json.getJSONObject("height").getString("observedAt"))
        val generated = time(json.getString("generatedAt"))
        if (generated < rule.optLong("lastChecked") || observed < rule.optLong("lastObserved")) return@synchronized
        val condition = json.getString("calculatedCondition")
        val send = NoticePolicy.shouldNotify(rule.has("condition"), rule.optString("condition"),
            rule.optLong("lastObserved"), rule.optLong("lastChecked"), condition, observed)
        rule.put("condition", condition).put("lastObserved", observed).put("lastChecked", generated)
            .put("status", "Consultado: ${json.getString("generatedAt")}")
        val label = when (condition) {
            "aboveEvacuationThreshold" -> "Altura sobre la referencia de evacuación; no es una orden de evacuar."
            "aboveAlertThreshold" -> "Altura sobre la referencia de alerta."
            else -> "Cambio que requiere seguimiento."
        }
        // Persist before posting: a crash can lose one notification, but never replay it after restart.
        if (send) {
            val old = state.optJSONArray("history") ?: JSONArray()
            val history = JSONArray().put(JSONObject().put("stationId", id).put("name", rule.getString("name"))
                .put("text", label).put("observedAt", json.getJSONObject("height").getString("observedAt"))
                .put("dataVersion", json.getString("dataVersion")))
            for (index in 0 until minOf(old.length(), 99)) history.put(old.get(index))
            state.put("history", history)
        }
        save(context, state)
        val manager = context.getSystemService(NotificationManager::class.java)
        if (!NoticePolicy.noteworthy(condition)) manager.cancel(id, JOB_ID)
        if (send && !stopped()) {
            if (Build.VERSION.SDK_INT >= 26) manager.createNotificationChannel(NotificationChannel(
                CHANNEL, "Seguimiento de ríos", NotificationManager.IMPORTANCE_DEFAULT))
            val intent = context.packageManager.getLaunchIntentForPackage(context.packageName)!!
            val pending = PendingIntent.getActivity(context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
            val builder = Notification.Builder(context, CHANNEL)
            manager.notify(id, JOB_ID, builder.setSmallIcon(R.drawable.ic_river_notice)
                .setContentTitle(rule.getString("name")).setContentText(label)
                .setStyle(Notification.BigTextStyle().bigText("$label Medición: ${json.getJSONObject("height").getString("observedAt")}. Abrí la app para verificar el estado actual."))
                .setContentIntent(pending).setAutoCancel(true).setOnlyAlertOnce(true)
                .setTimeoutAfter(3600000 - (generated - observed)).build())
        }
    }
    private fun revoke(context: Context, id: String, original: JSONObject) = synchronized(lock) {
        val state = read(context)
        val rules = state.optJSONObject("rules") ?: return@synchronized
        if (rules.optJSONObject(id)?.optString("token") != original.optString("token")) return@synchronized
        rules.remove(id)
        save(context, state)
        context.getSystemService(NotificationManager::class.java).cancel(id, JOB_ID)
        if (rules.length() == 0) context.getSystemService(JobScheduler::class.java).cancel(JOB_ID)
    }
    private fun mark(context: Context, id: String, status: String) = synchronized(lock) {
        val state = read(context)
        state.optJSONObject("rules")?.optJSONObject(id)?.put("status", status)
        save(context, state)
        context.getSystemService(NotificationManager::class.java).cancel(id, JOB_ID)
    }
}

class NoticeJobService : JobService() {
    private val executor = Executors.newSingleThreadExecutor()
    private var task: Future<*>? = null
    @Volatile private var generation = 0
    override fun onStartJob(params: JobParameters): Boolean {
        val run = ++generation
        task = executor.submit {
            try { LocalNotices.poll(applicationContext) { generation != run || Thread.currentThread().isInterrupted } }
            finally { if (generation == run) jobFinished(params, false) }
        }
        return true
    }
    override fun onStopJob(params: JobParameters): Boolean {
        generation++
        task?.cancel(true)
        return true
    }
    override fun onDestroy() { generation++; executor.shutdownNow(); super.onDestroy() }
}
