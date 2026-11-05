package com.xiaojing.jiaqi;

import android.content.Context;
import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * 假期数据模型 + 解析 + 日期计算。
 * 数据来源：assets/holidays.json（内置），可被 files/holidays.json（在线更新）覆盖。
 */
public class Model {

    public static class Holiday {
        public String type = "";
        public String label = "";
        public String start = "";
        public String end = "";
        public String note = "";
        public boolean endDerived = false;
        public int days = 0;
    }

    public static class Source {
        public String title = "";
        public String url = "";
        public String published = "";
        public String grade = "media";
        public String publisher = "";
        public String docNo = "";
    }

    public static class Policy {
        public String scope = "";
        public String note = "";
        public List<Holiday> holidays = new ArrayList<Holiday>();
        public Source source = new Source();
    }

    public static class History {
        public int year = 0;
        public String primary = "";
        public String senior = "";
        public String springStart = "";
        public String note = "";
    }

    public static class City {
        public String code = "";
        public String name = "";
        public Map<String, Policy> policies = new LinkedHashMap<String, Policy>();
        public History history = null;
    }

    public static class University {
        public String id = "";
        public String name = "";
        public String province = "";
        public String city = "";
        public String note = "";
        public List<Holiday> holidays = new ArrayList<Holiday>();
        public Source source = new Source();
    }

    public static class National {
        public int year = 0;
        public List<Holiday> items = new ArrayList<Holiday>();
        public Source source = new Source();
    }

    public static class SeasonalItem {
        public String label = "";
        public String start = "";
        public String end = "";
        public String city = "";
        public String scope = "";
        public String note = "";
    }

    public static class Seasonal {
        public boolean policy = false;
        public String policyText = "";
        public List<SeasonalItem> items = new ArrayList<SeasonalItem>();
        public Source source = new Source();
    }

    public static class Region {
        public String code = "";
        public String name = "";
        public String abbr = "";
        public String kind = "";
        public List<City> cities = new ArrayList<City>();
        public String commonNote = "";
        public String pending = "";
        public String pendingHint = "";
        public Seasonal seasonal = null;
        public Source historySource = null;
        public Map<String, Policy> commonPolicies = new LinkedHashMap<String, Policy>();
    }

    public String updatedAt = "";
    public String academicYear = "";
    public String universityNote = "";
    public List<String> stageKeys = new ArrayList<String>();
    public List<String> stageLabels = new ArrayList<String>();
    public List<Region> regions = new ArrayList<Region>();
    public National national = new National();
    public List<University> universities = new ArrayList<University>();
    public int totalProvinces = 0;
    public List<String> missingProvinces = new ArrayList<String>();
    public List<Integer> weekendRest = new ArrayList<Integer>();
    public boolean fromUpdate = false;

    // ---------------------------------------------------------------- 载入

    private static String readAll(InputStream in) throws Exception {
        ByteArrayOutputStream bos = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
        in.close();
        return new String(bos.toByteArray(), "UTF-8");
    }

    public static Model load(Context ctx) {
        Model m = new Model();
        String text = null;
        File f = new File(ctx.getFilesDir(), "holidays.json");
        if (f.exists()) {
            try {
                FileInputStream in = new FileInputStream(f);
                text = readAll(in);
                m.fromUpdate = true;
            } catch (Exception e) {
                text = null;
            }
        }
        if (text == null) {
            try {
                text = readAll(ctx.getAssets().open("holidays.json"));
                m.fromUpdate = false;
            } catch (Exception e) {
                text = "{}";
            }
        }
        try {
            m.parse(new JSONObject(text));
        } catch (Exception e) {
            m.regions.clear();
        }
        return m;
    }

    private static String str(JSONObject o, String k) {
        return o.optString(k, "");
    }

    public void parse(JSONObject o) throws Exception {
        updatedAt = str(o, "updatedAt");
        academicYear = str(o, "academicYear");
        universityNote = str(o, "universityNote");

        JSONArray sm = o.optJSONArray("stageMeta");
        if (sm != null) {
            for (int i = 0; i < sm.length(); i++) {
                JSONObject s = sm.getJSONObject(i);
                stageKeys.add(str(s, "key"));
                stageLabels.add(str(s, "label"));
            }
        }

        JSONArray rs = o.optJSONArray("regions");
        if (rs == null) return;
        for (int i = 0; i < rs.length(); i++) {
            JSONObject ro = rs.getJSONObject(i);
            Region r = new Region();
            r.code = str(ro, "code");
            r.name = str(ro, "name");
            r.abbr = str(ro, "abbr");
            r.kind = str(ro, "kind");
            r.pending = str(ro, "pending");
            r.pendingHint = str(ro, "pendingHint");

            JSONObject sea = ro.optJSONObject("seasonal");
            if (sea != null) {
                Seasonal s = new Seasonal();
                s.policy = sea.optBoolean("policy", false);
                s.policyText = str(sea, "policyText");
                JSONArray sits = sea.optJSONArray("items");
                if (sits != null) {
                    for (int k = 0; k < sits.length(); k++) {
                        JSONObject io = sits.getJSONObject(k);
                        SeasonalItem it = new SeasonalItem();
                        it.label = str(io, "label");
                        it.start = str(io, "start");
                        it.end = str(io, "end");
                        it.city = str(io, "city");
                        it.scope = str(io, "scope");
                        it.note = str(io, "note");
                        s.items.add(it);
                    }
                }
                s.source = parseSource(sea.optJSONObject("source"));
                r.seasonal = s;
            }

            JSONObject common = ro.optJSONObject("common");
            if (common != null) {
                String sp = str(common, "springStart");
                String n = str(common, "note");
                if (sp.length() > 0) r.commonNote = "春季学期开学 " + pretty(sp);
                if (n.length() > 0) r.commonNote += (r.commonNote.length() > 0 ? "；" : "") + n;
            }

            JSONObject cp = ro.optJSONObject("commonPolicies");
            if (cp != null) {
                for (String sk : stageKeys) {
                    JSONObject po = cp.optJSONObject(sk);
                    if (po != null) r.commonPolicies.put(sk, parsePolicy(po));
                }
            }

            JSONObject hso = ro.optJSONObject("historySource");
            if (hso != null) r.historySource = parseSource(hso);

            JSONArray cs = ro.optJSONArray("cities");
            if (cs != null) {
                for (int j = 0; j < cs.length(); j++) {
                    JSONObject co = cs.getJSONObject(j);
                    City c = new City();
                    c.code = str(co, "code");
                    c.name = str(co, "name");
                    JSONObject ps = co.optJSONObject("policies");
                    if (ps != null) {
                        for (String sk : stageKeys) {
                            JSONObject po = ps.optJSONObject(sk);
                            if (po != null) c.policies.put(sk, parsePolicy(po));
                        }
                    }
                    JSONObject hd = co.optJSONObject("history");
                    if (hd != null) {
                        History h = new History();
                        h.year = hd.optInt("year", 0);
                        h.primary = str(hd, "primary");
                        h.senior = str(hd, "senior");
                        h.springStart = str(hd, "springStart");
                        h.note = str(hd, "note");
                        c.history = h;
                    }
                    r.cities.add(c);
                }
            }
            regions.add(r);
        }

        // 收录缺口
        JSONObject cov = o.optJSONObject("coverage");
        if (cov != null) {
            totalProvinces = cov.optInt("total", 0);
            JSONArray miss = cov.optJSONArray("missing");
            if (miss != null) {
                for (int i = 0; i < miss.length(); i++) {
                    JSONObject mo = miss.getJSONObject(i);
                    missingProvinces.add(mo.optString("name", ""));
                }
            }
        }

        // 周末规则（0=周日 … 6=周六）
        JSONObject wk = o.optJSONObject("weekendRule");
        if (wk != null) {
            JSONArray arr = wk.optJSONArray("rest");
            if (arr != null) {
                weekendRest.clear();
                for (int i = 0; i < arr.length(); i++) weekendRest.add(arr.optInt(i, 0));
            }
        }
        if (weekendRest.isEmpty()) {
            weekendRest.add(6);
            weekendRest.add(0);
        }

        // 全国法定节假日
        JSONObject nh = o.optJSONObject("nationalHolidays");
        if (nh != null) {
            national.year = nh.optInt("year", 0);
            JSONArray items = nh.optJSONArray("items");
            if (items != null) {
                for (int i = 0; i < items.length(); i++) {
                    national.items.add(parseHoliday(items.getJSONObject(i)));
                }
            }
            national.source = parseSource(nh.optJSONObject("source"));
        }

        // 高校校历（按学校）
        JSONArray us = o.optJSONArray("universities");
        if (us != null) {
            for (int i = 0; i < us.length(); i++) {
                JSONObject uo = us.getJSONObject(i);
                University u = new University();
                u.id = str(uo, "id");
                u.name = str(uo, "name");
                u.province = str(uo, "province");
                u.city = str(uo, "city");
                u.note = str(uo, "note");
                JSONArray hh = uo.optJSONArray("holidays");
                if (hh != null) {
                    for (int j = 0; j < hh.length(); j++) {
                        u.holidays.add(parseHoliday(hh.getJSONObject(j)));
                    }
                }
                u.source = parseSource(uo.optJSONObject("source"));
                universities.add(u);
            }
        }
    }

    private static Holiday parseHoliday(JSONObject ho) {
        Holiday h = new Holiday();
        h.type = str(ho, "type");
        h.label = str(ho, "label");
        h.start = str(ho, "start");
        h.end = str(ho, "end");
        h.note = str(ho, "note");
        h.endDerived = ho.optBoolean("endDerived", false);
        h.days = ho.optInt("days", 0);
        return h;
    }

    private static Source parseSource(JSONObject so) {
        Source s = new Source();
        if (so == null) return s;
        s.title = str(so, "title");
        s.url = str(so, "url");
        s.published = str(so, "published");
        s.grade = str(so, "grade");
        s.publisher = str(so, "publisher");
        s.docNo = str(so, "docNo");
        return s;
    }

    private static Policy parsePolicy(JSONObject po) throws Exception {
        Policy p = new Policy();
        p.scope = str(po, "scope");
        p.note = str(po, "note");
        JSONArray hs = po.optJSONArray("holidays");
        if (hs != null) {
            for (int i = 0; i < hs.length(); i++) {
                JSONObject ho = hs.getJSONObject(i);
                Holiday h = new Holiday();
                h.type = str(ho, "type");
                h.label = str(ho, "label");
                h.start = str(ho, "start");
                h.end = str(ho, "end");
                h.note = str(ho, "note");
                h.endDerived = ho.optBoolean("endDerived", false);
                p.holidays.add(h);
            }
        }
        JSONObject so = po.optJSONObject("source");
        if (so != null) {
            p.source.title = str(so, "title");
            p.source.url = str(so, "url");
            p.source.published = str(so, "published");
            p.source.grade = str(so, "grade");
            p.source.publisher = str(so, "publisher");
            p.source.docNo = str(so, "docNo");
        }
        return p;
    }

    // ---------------------------------------------------------------- 日期

    private static final SimpleDateFormat YMD = new SimpleDateFormat("yyyy-MM-dd", Locale.US);

    public static long parseMs(String ymd) {
        try {
            return YMD.parse(ymd).getTime();
        } catch (Exception e) {
            return 0;
        }
    }

    public static String todayYmd() {
        return YMD.format(new java.util.Date());
    }

    public static long todayMidnightMs() {
        Calendar c = Calendar.getInstance();
        c.set(Calendar.HOUR_OF_DAY, 0);
        c.set(Calendar.MINUTE, 0);
        c.set(Calendar.SECOND, 0);
        c.set(Calendar.MILLISECOND, 0);
        return c.getTimeInMillis();
    }

    /** 距开始还有几天（今天=0，明天=1，已开始为负）。 */
    public static long daysFromToday(String ymd) {
        long t = parseMs(ymd);
        if (t == 0) return Long.MIN_VALUE;
        return Math.round((t - todayMidnightMs()) / 86400000.0);
    }

    public static String pretty(String ymd) {
        if (ymd == null || ymd.length() < 10) return ymd == null ? "" : ymd;
        try {
            String[] p = ymd.split("-");
            int mo = Integer.parseInt(p[1]);
            int da = Integer.parseInt(p[2]);
            return mo + "月" + da + "日";
        } catch (Exception e) {
            return ymd;
        }
    }

    public static String fullPretty(String ymd) {
        if (ymd == null || ymd.length() < 10) return "";
        try {
            String[] p = ymd.split("-");
            return Integer.parseInt(p[0]) + "年" + Integer.parseInt(p[1]) + "月" + Integer.parseInt(p[2]) + "日";
        } catch (Exception e) {
            return ymd;
        }
    }

    /** 一条假期的状态描述。 */
    public static class Next {
        public Holiday h;
        public long days;      // >0 未开始；=0 明天；<0 已开始
        public boolean ongoing;
    }

    /** 从一组 policy 里挑「最近的假期」：优先正在放的，其次最近要来的。 */
    public static Next nextOf(Policy p) {
        return p == null ? null : nextOf(p.holidays);
    }

    public static Next nextOf(List<Holiday> holidays) {
        if (holidays == null) return null;
        long today = todayMidnightMs();
        Next best = null;
        for (Holiday h : holidays) {
            long s = parseMs(h.start);
            long e = parseMs(h.end);
            if (s == 0) continue;
            boolean ongoing = s <= today && (e == 0 || e >= today);
            if (ongoing) {
                Next n = new Next();
                n.h = h;
                n.days = Math.round((s - today) / 86400000.0);
                n.ongoing = true;
                if (best == null || !best.ongoing) best = n;
            } else if (s > today) {
                if (best == null || (!best.ongoing && s < parseMs(best.h.start))) {
                    Next n = new Next();
                    n.h = h;
                    n.days = Math.round((s - today) / 86400000.0);
                    n.ongoing = false;
                    best = n;
                }
            }
        }
        return best;
    }

    public static Policy firstPolicy(City c) {
        if (c == null) return null;
        for (String k : c.policies.keySet()) return c.policies.get(k);
        return null;
    }

    public Region findRegion(String code) {
        for (Region r : regions) if (r.code.equals(code)) return r;
        return null;
    }
}
