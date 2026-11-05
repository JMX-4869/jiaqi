package com.xiaojing.jiaqi;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * 假期计划：一份计划 -> 多页 -> 每页多个元素（文字/图片/视频/音乐）。
 * 存在 SharedPreferences 里（JSON）。
 */
public class PlanModel {

    public static class PageElement {
        public String type = "text";     // text / image / video / audio
        public String text = "";
        public String src = "";          // 图片/视频/音频文件路径
        public String name = "";
        public int size = 16;
        public int x, y, w, h;

        public JSONObject toJson() {
            JSONObject o = new JSONObject();
            try {
                o.put("type", type);
                o.put("text", text);
                o.put("src", src);
                o.put("name", name);
                o.put("size", size);
                o.put("x", x);
                o.put("y", y);
                o.put("w", w);
                o.put("h", h);
            } catch (Exception e) { }
            return o;
        }

        public static PageElement fromJson(JSONObject o) {
            PageElement e = new PageElement();
            e.type = o.optString("type", "text");
            e.text = o.optString("text", "");
            e.src = o.optString("src", "");
            e.name = o.optString("name", "");
            e.size = o.optInt("size", 16);
            e.x = o.optInt("x", 0);
            e.y = o.optInt("y", 0);
            e.w = o.optInt("w", 0);
            e.h = o.optInt("h", 0);
            return e;
        }
    }

    public static class Page {
        public String date = "";
        public List<PageElement> elements = new ArrayList<PageElement>();

        public JSONObject toJson() {
            JSONObject o = new JSONObject();
            try {
                o.put("date", date);
                JSONArray arr = new JSONArray();
                for (PageElement e : elements) arr.put(e.toJson());
                o.put("elements", arr);
            } catch (Exception e) { }
            return o;
        }

        public static Page fromJson(JSONObject o) {
            Page p = new Page();
            p.date = o.optString("date", "");
            JSONArray arr = o.optJSONArray("elements");
            if (arr != null) {
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject eo = arr.optJSONObject(i);
                    if (eo != null) p.elements.add(PageElement.fromJson(eo));
                }
            }
            return p;
        }
    }

    public static class Plan {
        public String id = "";
        public String title = "";
        public String start = "";
        public String end = "";
        public boolean remind = true;
        public String remindTime = "09:00";
        public boolean sound = true;
        public boolean vibrate = true;
        public String soundPath = "";
        public double soundStart, soundEnd;
        public List<Page> pages = new ArrayList<Page>();

        public JSONObject toJson() {
            JSONObject o = new JSONObject();
            try {
                o.put("id", id);
                o.put("title", title);
                o.put("start", start);
                o.put("end", end);
                o.put("remind", remind);
                o.put("remindTime", remindTime);
                o.put("sound", sound);
                o.put("vibrate", vibrate);
                o.put("soundPath", soundPath);
                o.put("soundStart", soundStart);
                o.put("soundEnd", soundEnd);
                JSONArray arr = new JSONArray();
                for (Page p : pages) arr.put(p.toJson());
                o.put("pages", arr);
            } catch (Exception e) { }
            return o;
        }

        public static Plan fromJson(JSONObject o) {
            Plan p = new Plan();
            p.id = o.optString("id", "");
            p.title = o.optString("title", "");
            p.start = o.optString("start", "");
            p.end = o.optString("end", "");
            p.remind = o.optBoolean("remind", true);
            p.remindTime = o.optString("remindTime", "09:00");
            p.sound = o.optBoolean("sound", true);
            p.vibrate = o.optBoolean("vibrate", true);
            p.soundPath = o.optString("soundPath", "");
            p.soundStart = o.optDouble("soundStart", 0);
            p.soundEnd = o.optDouble("soundEnd", 0);
            JSONArray arr = o.optJSONArray("pages");
            if (arr != null) {
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject po = arr.optJSONObject(i);
                    if (po != null) p.pages.add(Page.fromJson(po));
                }
            }
            return p;
        }
    }

    static final String PREF = "jiaqi_plans";
    static final String KEY = "plans";

    public static List<Plan> load(Context ctx) {
        List<Plan> list = new ArrayList<Plan>();
        try {
            SharedPreferences sp = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE);
            String raw = sp.getString(KEY, "");
            if (raw == null || raw.length() == 0) return list;
            JSONArray arr = new JSONArray(raw);
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o != null) list.add(Plan.fromJson(o));
            }
        } catch (Exception e) { }
        return list;
    }

    public static boolean save(Context ctx, List<Plan> list) {
        try {
            JSONArray arr = new JSONArray();
            for (Plan p : list) arr.put(p.toJson());
            SharedPreferences sp = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE);
            return sp.edit().putString(KEY, arr.toString()).commit();
        } catch (Exception e) {
            return false;
        }
    }
}
