package com.xiaojing.jiaqi;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.media.Ringtone;
import android.media.RingtoneManager;
import android.net.Uri;
import android.os.Build;
import android.os.Vibrator;
import android.os.VibrationEffect;

import java.util.Calendar;
import java.util.List;

/**
 * 计划提醒：每天到点用 AlarmManager 拉起一个广播，
 * 广播里发通知栏通知 + 响铃 + 振动。
 */
public class ReminderReceiver extends BroadcastReceiver {

    static final String CH = "jiaqi_plan";

    @Override
    public void onReceive(Context ctx, Intent it) {
        String title = it.getStringExtra("title");
        String body = it.getStringExtra("body");
        boolean sound = it.getBooleanExtra("sound", true);
        boolean vibrate = it.getBooleanExtra("vibrate", true);
        String soundPath = it.getStringExtra("soundPath");
        if (title == null) title = "假期计划";
        if (body == null) body = "该看看今天的计划了";

        NotificationManager nm = (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationChannel ch = new NotificationChannel(CH, "假期计划提醒",
                    NotificationManager.IMPORTANCE_HIGH);
            ch.enableVibration(vibrate);
            ch.enableLights(true);
            nm.createNotificationChannel(ch);
        }

        Intent open = new Intent(ctx, MainActivity.class);
        open.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= 23) flags |= PendingIntent.FLAG_IMMUTABLE;
        PendingIntent pi = PendingIntent.getActivity(ctx, title.hashCode(), open, flags);

        Notification.Builder b;
        if (Build.VERSION.SDK_INT >= 26) {
            b = new Notification.Builder(ctx, CH);
        } else {
            b = new Notification.Builder(ctx);
        }
        b.setContentTitle(title)
                .setContentText(body)
                .setStyle(new Notification.BigTextStyle().bigText(body))
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setAutoCancel(true)
                .setContentIntent(pi);
        if (!sound) b.setSound(null);
        nm.notify(title.hashCode(), b.build());

        // 铃声
        if (sound) {
            try {
                if (soundPath != null && soundPath.length() > 0) {
                    Ringtone r = RingtoneManager.getRingtone(ctx, Uri.parse(soundPath));
                    if (r != null) r.play();
                } else {
                    Uri def = RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION);
                    Ringtone r = RingtoneManager.getRingtone(ctx, def);
                    if (r != null) r.play();
                }
            } catch (Exception e) { }
        }

        // 振动
        if (vibrate) {
            try {
                Vibrator v = (Vibrator) ctx.getSystemService(Context.VIBRATOR_SERVICE);
                if (v != null) {
                    if (Build.VERSION.SDK_INT >= 26) {
                        v.vibrate(VibrationEffect.createOneShot(600, VibrationEffect.DEFAULT_AMPLITUDE));
                    } else {
                        v.vibrate(600);
                    }
                }
            } catch (Exception e) { }
        }
    }

    /** 给一份计划排明天的闹钟（每天重复）。 */
    public static void schedule(Context ctx, PlanModel.Plan p) {
        try {
            AlarmManager am = (AlarmManager) ctx.getSystemService(Context.ALARM_SERVICE);
            if (am == null) return;

            int hm = 9, mm = 0;
            try {
                String[] a = p.remindTime.split(":");
                hm = Integer.parseInt(a[0]);
                mm = Integer.parseInt(a[1]);
            } catch (Exception e) { }

            Calendar c = Calendar.getInstance();
            c.set(Calendar.HOUR_OF_DAY, hm);
            c.set(Calendar.MINUTE, mm);
            c.set(Calendar.SECOND, 0);
            if (c.getTimeInMillis() <= System.currentTimeMillis())
                c.add(Calendar.DAY_OF_MONTH, 1);

            Intent it = new Intent(ctx, ReminderReceiver.class);
            it.putExtra("title", p.title);
            it.putExtra("body", firstLine(p));
            it.putExtra("sound", p.sound);
            it.putExtra("vibrate", p.vibrate);
            it.putExtra("soundPath", p.soundPath);

            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= 23) flags |= PendingIntent.FLAG_IMMUTABLE;
            PendingIntent pi = PendingIntent.getBroadcast(ctx, p.id.hashCode(), it, flags);

            if (Build.VERSION.SDK_INT >= 23) {
                am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, c.getTimeInMillis(), pi);
            } else {
                am.set(AlarmManager.RTC_WAKEUP, c.getTimeInMillis(), pi);
            }
        } catch (Exception e) { }
    }

    public static void cancel(Context ctx, PlanModel.Plan p) {
        try {
            AlarmManager am = (AlarmManager) ctx.getSystemService(Context.ALARM_SERVICE);
            Intent it = new Intent(ctx, ReminderReceiver.class);
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= 23) flags |= PendingIntent.FLAG_IMMUTABLE;
            PendingIntent pi = PendingIntent.getBroadcast(ctx, p.id.hashCode(), it, flags);
            if (am != null) am.cancel(pi);
        } catch (Exception e) { }
    }

    static String firstLine(PlanModel.Plan p) {
        try {
            String today = new java.text.SimpleDateFormat("yyyy-MM-dd").format(new java.util.Date());
            for (PlanModel.Page pg : p.pages) {
                if (today.equals(pg.date)) {
                    for (PlanModel.PageElement el : pg.elements) {
                        if ("text".equals(el.type) && el.text.length() > 0) return el.text;
                    }
                }
            }
        } catch (Exception e) { }
        return "该看看今天的计划了";
    }

    /** 重新排所有计划的闹钟。 */
    public static void rescheduleAll(Context ctx) {
        try {
            List<PlanModel.Plan> list = PlanModel.load(ctx);
            for (PlanModel.Plan p : list) {
                if (p.remind) schedule(ctx, p);
                else cancel(ctx, p);
            }
        } catch (Exception e) { }
    }
}
