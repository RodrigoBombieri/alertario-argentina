package com.example.alertario_mobile

import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings

class MainActivity : FlutterActivity() {
    private var permissionResult: MethodChannel.Result? = null
    private val noticeExecutor = java.util.concurrent.Executors.newSingleThreadExecutor()
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "ar.alertario/notices")
            .setMethodCallHandler { call, result ->
                try {
                    when (call.method) {
                        "state" -> result.success(LocalNotices.state(this))
                        "set" -> {
                            LocalNotices.set(this, call.argument<String>("id")!!, call.argument<String>("name")!!,
                                call.argument<String>("base")!!, call.argument<Boolean>("enabled")!!)
                            result.success(null)
                        }
                        "clearHistory" -> { LocalNotices.clearHistory(this); result.success(null) }
                        "checkNow" -> noticeExecutor.submit {
                            try {
                                LocalNotices.poll(applicationContext) { Thread.currentThread().isInterrupted }
                                runOnUiThread { result.success(null) }
                            } catch (_: Exception) {
                                runOnUiThread { result.error("notices", "No se pudo completar la consulta.", null) }
                            }
                        }
                        "permission" -> {
                            if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                                check(permissionResult == null) { "Ya hay una solicitud en curso." }
                                permissionResult = result
                                requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 8140)
                            } else result.success(LocalNotices.allowed(this))
                        }
                        "settings" -> {
                            startActivity(Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, packageName))
                            result.success(null)
                        }
                        "official" -> {
                            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://www.smn.gob.ar/alertas")))
                            result.success(null)
                        }
                        else -> result.notImplemented()
                    }
                } catch (error: Exception) { result.error("notices", error.message, null) }
            }
    }
    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 8140) {
            permissionResult?.success(grantResults.isNotEmpty() && grantResults[0] == PackageManager.PERMISSION_GRANTED)
            permissionResult = null
        }
    }
    override fun onDestroy() { noticeExecutor.shutdownNow(); super.onDestroy() }
    override fun onResume() { super.onResume(); LocalNotices.resume(this) }
}
