package com.xiaojing.jiaqi;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/**
 * 假期表 —— 全国学校假期查询
 * 关注页看「我盯的那几所学校/地区」，全部页按省→市→学段翻，数据页看来源和更新。
 */
public class MainActivity extends Activity {

    // ---------------------------------------------------------------- 配色
    static final int C_BG = 0xFF0E1116;
    static final int C_CARD = 0xFF161B22;
    static final int C_CARD2 = 0xFF1B222B;
    static final int C_ACCENT = 0xFF4CC2FF;
    static final int C_TEXT = 0xFFE6EDF3;
    static final int C_SUB = 0xFF8B949E;
    static final int C_LINE = 0xFF232B36;
    static final int C_WARN = 0xFFFFB347;

    Model model;
    SharedPreferences prefs;

    LinearLayout body;
    TextView[] tabViews = new TextView[4];
    ScrollView scroll;

    // 导航状态
    int navLevel = 0;            // 0=tracks 1=all 2=region 3=city 4=data
    Model.Region curRegion;
    Model.City curCity;

    final Handler ui = new Handler(Looper.getMainLooper());

    // ---------------------------------------------------------------- 生命周期

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().setStatusBarColor(C_BG);
        getWindow().setNavigationBarColor(C_BG);
        prefs = getSharedPreferences("jiaqi", MODE_PRIVATE);
        model = Model.load(this);
        buildShell();
        showTracks();
        autoUpdate();   // 启动就在后台查一次，用户不用点任何按钮
    }

    // ---------------------------------------------------------------- 自动更新

    static final String UPDATE_URL =
            "https://raw.githubusercontent.com/JMX-4869/jiaqi-data/master/holidays.json";

    static String today() {
        return new java.text.SimpleDateFormat("yyyyMMdd", java.util.Locale.US)
                .format(new java.util.Date());
    }

    void autoUpdate() {
        // 同一天只查一次，别每次开都发请求
        if (today().equals(prefs.getString("lastCheckDay", ""))) return;

        new Thread(new Runnable() {
            public void run() {
                String body = null;
                try {
                    // 带日期参数：每天第一次请求绕过 CDN 缓存，保证拿到当天最新的
                    String url = UPDATE_URL + "?t=" + System.currentTimeMillis();
                    HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
                    conn.setConnectTimeout(12000);
                    conn.setReadTimeout(20000);
                    conn.setRequestProperty("User-Agent", "jiaqi-android");
                    if (conn.getResponseCode() / 100 != 2) return;
                    InputStream in = conn.getInputStream();
                    ByteArrayOutputStream bos = new ByteArrayOutputStream();
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
                    in.close();
                    body = new String(bos.toByteArray(), "UTF-8");
                } catch (Exception e) {
                    return;   // 没网 / 超时：什么都不做，继续用本地数据
                }

                final String text = body;
                String remoteAt = grabUpdatedAt(text);
                final boolean newer = remoteAt.length() > 0
                        && remoteAt.compareTo(model.updatedAt) > 0
                        && text.contains("\"regions\"");
                if (newer) {
                    try {
                        FileOutputStream out = new FileOutputStream(
                                new File(getFilesDir(), "holidays.json"));
                        out.write(text.getBytes("UTF-8"));
                        out.close();
                    } catch (Exception e) {
                        return;
                    }
                }
                ui.post(new Runnable() {
                    public void run() {
                        prefs.edit().putString("lastCheckDay", today()).apply();
                        if (newer) {
                            model = Model.load(MainActivity.this);
                            showCurrentPage();
                            Toast.makeText(MainActivity.this,
                                    "数据已自动更新到 " + shortDate(model.updatedAt),
                                    Toast.LENGTH_SHORT).show();
                        }
                    }
                });
            }
        }).start();
    }

    static String grabUpdatedAt(String json) {
        if (json == null) return "";
        int i = json.indexOf("\"updatedAt\"");
        if (i < 0) return "";
        int q1 = json.indexOf('"', i + 11);
        if (q1 < 0) return "";
        int q2 = json.indexOf('"', q1 + 1);
        if (q2 < 0) return "";
        return json.substring(q1 + 1, q2);
    }

    void showCurrentPage() {
        if (navLevel == 0) showTracks();
        else if (navLevel == 4) showData();
        else if (navLevel == 9) showPlans();
        else if (navLevel == 5) showNational();
        else if (navLevel == 6) showUniversities();
        else showAll();
    }

    @Override
    public void onBackPressed() {
        if (navLevel == 8) { showAll(); return; }
        if (navLevel == 7) { showUniversities(); return; }
        if (navLevel == 6 || navLevel == 5) { showAll(); return; }
        if (navLevel == 3) {
            if (curRegion != null) showRegion(curRegion);
            else showAll();
            return;
        }
        if (navLevel == 2) { showAll(); return; }
        if (navLevel == 1 || navLevel == 4) { showTracks(); return; }
        super.onBackPressed();
    }

    // ---------------------------------------------------------------- 骨架

    void buildShell() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(C_BG);

        // 标题
        LinearLayout head = new LinearLayout(this);
        head.setOrientation(LinearLayout.VERTICAL);
        head.setPadding(dp(18), dp(22), dp(18), dp(10));

        TextView title = tv("假期表", 26, C_TEXT, true);
        head.addView(title);

        String sub = model.academicYear.length() > 0
                ? model.academicYear + " 学年 · 数据更新 " + Model.pretty(shortDate(model.updatedAt))
                : "数据未加载";
        TextView subTv = tv(sub, 12, C_SUB, false);
        subTv.setPadding(0, dp(4), 0, 0);
        head.addView(subTv);
        root.addView(head);

        scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f);
        scroll.setLayoutParams(slp);

        body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setPadding(dp(14), dp(4), dp(14), dp(28));
        scroll.addView(body, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        root.addView(scroll);

        // 底部 tab
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setBackgroundColor(0xFF11161D);
        bar.setPadding(0, dp(6), 0, dp(6));
        String[] names = {"关注", "全部", "计划", "数据"};
        for (int i = 0; i < 4; i++) {
            final int idx = i;
            TextView t = tv(names[i], 13, C_SUB, true);
            t.setGravity(Gravity.CENTER);
            t.setPadding(0, dp(9), 0, dp(9));
            t.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    if (idx == 0) showTracks();
                    else if (idx == 1) showAll();
                    else if (idx == 2) showPlans();
                    else showData();
                }
            });
            bar.addView(t, new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            tabViews[i] = t;
        }
        root.addView(bar);

        // 通知权限（Android 13+）和闹钟
        try {
            if (android.os.Build.VERSION.SDK_INT >= 33) {
                requestPermissions(new String[]{"android.permission.POST_NOTIFICATIONS"}, 9001);
            }
            ReminderReceiver.rescheduleAll(this);
        } catch (Exception e) { }

        setContentView(root);
    }

    void markTab(int i) {
        for (int k = 0; k < 4; k++) {
            tabViews[k].setTextColor(k == i ? C_ACCENT : C_SUB);
        }
    }

    void clearBody() {
        body.removeAllViews();
        scroll.scrollTo(0, 0);
    }

    // ---------------------------------------------------------------- 关注页

    void showTracks() {
        navLevel = 0;
        markTab(0);
        clearBody();

        List<String> keys = trackKeys();
        if (keys.isEmpty()) {
            body.addView(emptyHint("还没有关注的学校或地区",
                    "去「全部」里选一个城市，点学段右侧的 + 就能加到这里。\n"
                            + "学校假期大多跟着所属地区的教育局校历走，所以关注地区就等于关注这一片的学校。",
                    "去挑一个 →", new View.OnClickListener() {
                        public void onClick(View v) { showAll(); }
                    }));
            return;
        }

        List<Model.Region> order = new ArrayList<Model.Region>(model.regions);
        Collections.sort(order, (a, b) -> a.code.compareTo(b.code));

        for (Model.University u : model.universities) {
            if (keys.contains("uni|" + u.id)) body.addView(uniTrackCard(u));
        }

        for (Model.Region r : order) {
            for (Model.City c : r.cities) {
                for (String sk : model.stageKeys) {
                    if (!c.policies.containsKey(sk)) continue;
                    String key = r.code + "|" + c.code + "|" + sk;
                    if (!keys.contains(key)) continue;
                    body.addView(trackCard(r, c, sk, key));
                }
            }
        }

        TextView tip = tv("长按卡片可以取消关注", 11, C_SUB, false);
        tip.setPadding(dp(4), dp(18), 0, 0);
        body.addView(tip);
    }

    View trackCard(final Model.Region r, final Model.City c, String stageKey, final String key) {
        Model.Policy p = c.policies.get(stageKey);
        Model.Next next = Model.nextOf(p);

        LinearLayout card = card();
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        LinearLayout left = new LinearLayout(this);
        left.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams llp = new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        left.setLayoutParams(llp);

        String stageLabel = stageLabel(stageKey);
        String nameText = simplifyCity(c.name) + " · " + stageLabel;
        TextView nm = tv(nameText, 16, C_TEXT, true);
        left.addView(nm);

        if (next != null) {
            String when;
            if (next.ongoing) {
                when = next.h.label + "进行中";
            } else {
                when = next.h.label + " " + Model.pretty(next.h.start) + " 开始";
            }
            TextView w = tv(when, 12, C_SUB, false);
            w.setPadding(0, dp(5), 0, 0);
            left.addView(w);

            if (next.h.end != null && next.h.end.length() > 0) {
                String range = Model.pretty(next.h.start) + " — " + Model.pretty(next.h.end)
                        + (next.h.endDerived ? "（推算）" : "");
                TextView rg = tv(range, 12, C_SUB, false);
                rg.setPadding(0, dp(2), 0, 0);
                left.addView(rg);
            }
        } else {
            left.addView(tv("暂无假期数据", 12, C_SUB, false));
        }

        row.addView(left);

        if (next != null) {
            LinearLayout right = new LinearLayout(this);
            right.setOrientation(LinearLayout.VERTICAL);
            right.setGravity(Gravity.CENTER);
            String big;
            String small;
            if (next.ongoing) {
                big = "放"; small = "假中";
            } else {
                big = String.valueOf(next.days);
                small = "天后";
            }
            TextView bv = tv(big, 30, C_ACCENT, true);
            bv.setGravity(Gravity.CENTER);
            right.addView(bv);
            TextView sv = tv(small, 11, C_SUB, false);
            sv.setGravity(Gravity.CENTER);
            right.addView(sv);
            right.setPadding(dp(12), 0, dp(2), 0);
            row.addView(right);
        }

        card.addView(row);

        card.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                curRegion = r;
                curCity = c;
                showCity(r, c);
            }
        });
        card.setOnLongClickListener(new View.OnLongClickListener() {
            public boolean onLongClick(View v) {
                new AlertDialog.Builder(MainActivity.this)
                        .setMessage("不再关注这个地区？")
                        .setPositiveButton("取消关注", (d, w) -> {
                            Set<String> s = new LinkedHashSet<String>(trackKeys());
                            s.remove(key);
                            prefs.edit().putStringSet("tracks", s).apply();
                            showTracks();
                        })
                        .setNegativeButton("留着", null)
                        .show();
                return true;
            }
        });
        return card;
    }

    List<String> trackKeys() {
        Set<String> s = prefs.getStringSet("tracks", null);
        if (s == null) return new ArrayList<String>();
        List<String> l = new ArrayList<String>(s);
        return l;
    }

    // ---------------------------------------------------------------- 搜索

    String searchKw = "";

    static class Hit {
        String kind = "";
        Model.Region r;
        Model.City c;
        Model.University u;
    }

    List<Hit> searchHits(String k) {
        List<Hit> hits = new ArrayList<Hit>();
        if (k.length() == 0) return hits;
        for (Model.Region r : model.regions) {
            if (r.name.contains(k)) {
                Hit h = new Hit();
                h.kind = "region";
                h.r = r;
                hits.add(h);
            }
            for (Model.City c : r.cities) {
                if (c.name.contains(k)) {
                    Hit h = new Hit();
                    h.kind = "city";
                    h.r = r;
                    h.c = c;
                    hits.add(h);
                }
            }
        }
        for (Model.University u : model.universities) {
            if ((u.name + u.province + u.city).contains(k)) {
                Hit h = new Hit();
                h.kind = "uni";
                h.u = u;
                hits.add(h);
            }
        }
        return hits;
    }

    android.view.View searchBox() {
        LinearLayout c = card();
        final EditText tb = new EditText(this);
        tb.setText(searchKw);
        tb.setSingleLine(true);
        tb.setTextSize(13);
        tb.setTextColor(C_TEXT);
        tb.setHint("搜：石家庄 / 山东 / 清华");
        tb.setHintTextColor(0xFF5A6675);
        tb.setImeOptions(android.view.inputmethod.EditorInfo.IME_ACTION_SEARCH);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(C_CARD2);
        bg.setCornerRadius(dp(8));
        tb.setBackground(bg);
        tb.setPadding(dp(10), dp(8), dp(10), dp(8));
        tb.setOnEditorActionListener(new TextView.OnEditorActionListener() {
            public boolean onEditorAction(TextView v, int actionId, android.view.KeyEvent event) {
                boolean enter = event != null && event.getKeyCode() == android.view.KeyEvent.KEYCODE_ENTER;
                if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_SEARCH
                        || actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE || enter) {
                    searchKw = tb.getText().toString().trim();
                    showAll();
                    return true;
                }
                return false;
            }
        });
        c.addView(tb);
        TextView tip = tv("输完按搜索键：城市 / 省份 / 高校名", 10, C_SUB, false);
        tip.setPadding(0, dp(6), 0, 0);
        c.addView(tip);
        return c;
    }

    // ---------------------------------------------------------------- 全部页

    void showAll() {
        navLevel = 1;
        markTab(1);
        curRegion = null;
        curCity = null;
        clearBody();

        if (model.regions.isEmpty()) {
            body.addView(emptyHint("数据没有加载成功",
                    "内置数据文件可能损坏。可以去「数据」页恢复内置数据。",
                    null, null));
            return;
        }

        TextView hint = tv("按地区查。点开城市后可以加入关注。", 12, C_SUB, false);
        hint.setPadding(dp(4), dp(2), dp(4), dp(12));
        body.addView(hint);

        body.addView(searchBox());

        if (searchKw.length() > 0) {
            List<Hit> hits = searchHits(searchKw);
            if (hits.isEmpty()) {
                LinearLayout eb = card();
                eb.addView(tv("没搜到「" + searchKw + "」", 15, C_TEXT, true));
                eb.addView(tv("试试城市名（石家庄）、省份名（山东）、或者高校名（清华）。", 12, C_SUB, false));
                body.addView(eb);
            } else {
                int shown = 0;
                for (final Hit h : hits) {
                    if (shown >= 40) break;
                    shown++;
                    LinearLayout c = card();
                    LinearLayout row = new LinearLayout(this);
                    row.setOrientation(LinearLayout.HORIZONTAL);
                    row.setGravity(Gravity.CENTER_VERTICAL);
                    LinearLayout left = new LinearLayout(this);
                    left.setOrientation(LinearLayout.VERTICAL);
                    left.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

                    String name;
                    String meta;
                    if ("region".equals(h.kind)) {
                        name = h.r.name;
                        meta = "省份 · " + h.r.cities.size() + " 个地级行政区";
                    } else if ("city".equals(h.kind)) {
                        name = h.c.name;
                        StringBuilder st = new StringBuilder();
                        for (String sk : model.stageKeys) {
                            if (!h.c.policies.containsKey(sk)) continue;
                            if (st.length() > 0) st.append(" · ");
                            st.append(stageLabel(sk));
                        }
                        if (st.length() == 0) {
                            if (h.c.history != null) st.append("2026 年 ").append(Model.pretty(h.c.history.primary)).append(" 放假");
                            else if (!h.r.commonPolicies.isEmpty()) st.append("省级口径");
                            else st.append("未收录");
                        }
                        meta = h.r.name + " · " + st;
                    } else {
                        name = h.u.name;
                        Model.Next nx = Model.nextOf(h.u.holidays);
                        String st = nx != null ? nx.h.label + " " + Model.pretty(nx.h.start) + " 开始" : "暂无数据";
                        meta = "高校 · " + h.u.province + h.u.city + " · " + st;
                    }
                    left.addView(tv(name, 16, C_TEXT, true));
                    left.addView(tv(meta, 12, C_SUB, false));
                    row.addView(left);
                    row.addView(tv("›", 22, C_SUB, false));
                    c.addView(row);
                    c.setOnClickListener(new View.OnClickListener() {
                        public void onClick(View v) {
                            navLevel = 1;
                            if ("region".equals(h.kind)) {
                                curRegion = h.r;
                                if (h.r.cities.size() == 1) showCity(h.r, h.r.cities.get(0));
                                else showRegion(h.r);
                            } else if ("city".equals(h.kind)) {
                                curRegion = h.r;
                                curCity = h.c;
                                showCity(h.r, h.c);
                            } else {
                                showUniversity(h.u);
                            }
                        }
                    });
                    body.addView(c);
                }
                if (hits.size() > 40) {
                    TextView more = tv("还有 " + (hits.size() - 40) + " 条，输入更具体的词。", 11, C_SUB, false);
                    more.setPadding(dp(4), dp(10), 0, 0);
                    body.addView(more);
                }
            }
            return;
        }

        // 全国法定节假日
        LinearLayout natCard = card();
        natCard.setOrientation(LinearLayout.HORIZONTAL);
        natCard.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout natLeft = new LinearLayout(this);
        natLeft.setOrientation(LinearLayout.VERTICAL);
        natLeft.setLayoutParams(new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        natLeft.addView(tv("全国法定节假日", 17, C_TEXT, true));
        natLeft.addView(tv(model.national.year + " 年 · " + model.national.items.size()
                + " 个节日 · 国务院办公厅", 12, C_SUB, false));
        natCard.addView(natLeft);
        natCard.addView(tv("›", 22, C_SUB, false));
        natCard.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) { showNational(); }
        });
        body.addView(natCard);

        // 高校校历
        LinearLayout uniCard = card();
        uniCard.setOrientation(LinearLayout.HORIZONTAL);
        uniCard.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout uniLeft = new LinearLayout(this);
        uniLeft.setOrientation(LinearLayout.VERTICAL);
        uniLeft.setLayoutParams(new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        uniLeft.addView(tv("高校校历", 17, C_TEXT, true));
        uniLeft.addView(tv(model.universities.size() + " 所 · 按学校收录，可单独关注", 12, C_SUB, false));
        uniCard.addView(uniLeft);
        uniCard.addView(tv("›", 22, C_SUB, false));
        uniCard.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) { showUniversities(); }
        });
        body.addView(uniCard);

        for (Model.Region r : model.regions) {
            final Model.Region fr = r;
            LinearLayout card = card();
            card.setOrientation(LinearLayout.HORIZONTAL);
            card.setGravity(Gravity.CENTER_VERTICAL);

            LinearLayout left = new LinearLayout(this);
            left.setOrientation(LinearLayout.VERTICAL);
            left.setLayoutParams(new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

            left.addView(tv(r.name, 17, C_TEXT, true));

            int n = r.cities.size();
            String info;
            int infoColor = C_SUB;
            if (n == 0) {
                info = "待补 · 官方尚未公布";
                infoColor = C_WARN;
            } else if (n == 1) {
                info = "全市统一";
            } else {
                info = n + " 个城市";
            }
            left.addView(tv(info, 12, infoColor, false));

            card.addView(left);

            TextView arrow = tv("›", 22, C_SUB, false);
            arrow.setPadding(dp(8), 0, 0, 0);
            card.addView(arrow);

            card.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    if (fr.cities.size() == 0) {
                        curRegion = fr;
                        curCity = null;
                        showPending(fr);
                    } else if (fr.cities.size() == 1) {
                        curRegion = fr;
                        curCity = fr.cities.get(0);
                        showCity(fr, fr.cities.get(0));
                    } else {
                        showRegion(fr);
                    }
                }
            });
            body.addView(card);
        }

        StringBuilder miss = new StringBuilder();
        for (String n : model.missingProvinces) {
            if (miss.length() > 0) miss.append(" · ");
            miss.append(n);
        }
        if (miss.length() > 0) {
            LinearLayout mc = card();
            TextView t1 = tv("未收录 " + model.missingProvinces.size() + " / "
                    + model.totalProvinces + " 个省级地区", 15, C_WARN, true);
            mc.addView(t1);
            TextView t2 = tv(miss.toString(), 12, C_SUB, false);
            t2.setPadding(0, dp(8), 0, 0);
            t2.setLineSpacing(dp(4), 1f);
            mc.addView(t2);
            TextView t3 = tv("不是这些地方没假期，是还没拿到能核对的官方校历。宁可不写，也不拿二手日期充数。",
                    11, C_SUB, false);
            t3.setPadding(0, dp(8), 0, 0);
            t3.setLineSpacing(dp(3), 1f);
            mc.addView(t3);
            body.addView(mc);
        }
    }

    // ---------------------------------------------------------------- 法定节假日

    void showNational() {
        navLevel = 5;
        clearBody();
        curRegion = null;
        curCity = null;
        body.addView(crumb("全部", new View.OnClickListener() {
            public void onClick(View v) { showAll(); }
        }, "全国法定节假日"));

        LinearLayout top = card();
        top.addView(tv(model.national.year + " 年 · 全国统一", 18, C_TEXT, true));
        TextView t1 = tv("对全国机关、学校、企业都适用。学校通常在此基础上再按校历调整。",
                12, C_SUB, false);
        t1.setPadding(0, dp(5), 0, 0);
        t1.setLineSpacing(dp(3), 1f);
        top.addView(t1);
        body.addView(top);

        long today = Model.todayMidnightMs();
        for (Model.Holiday h : model.national.items) {
            LinearLayout c = card();
            LinearLayout line = new LinearLayout(this);
            line.setOrientation(LinearLayout.HORIZONTAL);
            line.setGravity(Gravity.CENTER_VERTICAL);
            line.addView(tv(h.label, 16, C_TEXT, true));

            long s = Model.parseMs(h.start);
            long e = Model.parseMs(h.end);
            boolean ongoing = s > 0 && e > 0 && s <= today && e >= today;
            if (ongoing) {
                TextView cd = tv("放假中", 11, C_ACCENT, true);
                cd.setPadding(dp(8), 0, 0, 0);
                line.addView(cd);
            } else if (s > today) {
                long dd = Math.round((s - today) / 86400000.0);
                TextView cd = tv("还有 " + dd + " 天", 11, C_ACCENT, true);
                cd.setPadding(dp(8), 0, 0, 0);
                line.addView(cd);
            }
            c.addView(line);

            String range = h.start.equals(h.end)
                    ? Model.fullPretty(h.start)
                    : Model.fullPretty(h.start) + " — " + Model.fullPretty(h.end);
            String meta = range;
            if (h.days > 0) meta += " · 共 " + h.days + " 天";
            TextView rg = tv(meta, 12, C_SUB, false);
            rg.setPadding(0, dp(4), 0, 0);
            c.addView(rg);

            if (h.note != null && h.note.length() > 0) {
                TextView nt = tv(h.note, 11, C_WARN, false);
                nt.setPadding(0, dp(3), 0, 0);
                c.addView(nt);
            }
            body.addView(c);
        }

        View nb = sourceBox(model.national.source, null);
        if (nb != null) body.addView(nb);
    }

    // ---------------------------------------------------------------- 高校

    void showUniversities() {
        navLevel = 6;
        clearBody();
        body.addView(crumb("全部", new View.OnClickListener() {
            public void onClick(View v) { showAll(); }
        }, "高校校历"));

        final EditText q = new EditText(this);
        q.setHint("搜学校名 / 城市");
        q.setHintTextColor(0xFF5A6675);
        q.setTextColor(C_TEXT);
        q.setTextSize(13);
        q.setSingleLine(true);
        GradientDrawable qbg = new GradientDrawable();
        qbg.setColor(C_CARD2);
        qbg.setCornerRadius(dp(10));
        q.setBackground(qbg);
        q.setPadding(dp(12), dp(9), dp(12), dp(9));
        LinearLayout.LayoutParams qlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        qlp.bottomMargin = dp(12);
        q.setLayoutParams(qlp);
        body.addView(q);

        final LinearLayout list = new LinearLayout(this);
        list.setOrientation(LinearLayout.VERTICAL);
        body.addView(list);

        final Runnable render = new Runnable() {
            public void run() {
                list.removeAllViews();
                String k = q.getText().toString().trim();
                int shown = 0;
                for (final Model.University u : model.universities) {
                    String hay = u.name + u.province + u.city;
                    if (k.length() > 0 && !hay.contains(k)) continue;
                    shown++;
                    Model.Next nx = Model.nextOf(u.holidays);
                    String meta = u.province + (u.province.equals(u.city) ? "" : u.city);
                    if (nx != null) {
                        meta += " · " + nx.h.label + " " + Model.pretty(nx.h.start) + " 开始";
                        if (nx.h.end != null && nx.h.end.length() > 0) {
                            meta += " · " + Model.pretty(nx.h.end) + " 结束";
                        }
                    } else {
                        meta += " · 暂无数据";
                    }
                    LinearLayout c = card();
                    c.setOrientation(LinearLayout.HORIZONTAL);
                    c.setGravity(Gravity.CENTER_VERTICAL);
                    LinearLayout left = new LinearLayout(MainActivity.this);
                    left.setOrientation(LinearLayout.VERTICAL);
                    left.setLayoutParams(new LinearLayout.LayoutParams(0,
                            ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
                    left.addView(tv(u.name, 16, C_TEXT, true));
                    left.addView(tv(meta, 12, C_SUB, false));
                    c.addView(left);
                    c.addView(tv("›", 22, C_SUB, false));
                    c.setOnClickListener(new View.OnClickListener() {
                        public void onClick(View v) { showUniversity(u); }
                    });
                    list.addView(c);
                }
                if (shown == 0) {
                    list.addView(emptyHint("没找到",
                            "目前只收录了 " + model.universities.size()
                                    + " 所高校的校历，没收录的可以去学校教务处官网查。数据会持续补。",
                            null, null));
                }
            }
        };
        q.addTextChangedListener(new android.text.TextWatcher() {
            public void beforeTextChanged(CharSequence s, int a, int b, int c) {}
            public void onTextChanged(CharSequence s, int a, int b, int c) {}
            public void afterTextChanged(android.text.Editable s) { render.run(); }
        });
        render.run();
    }

    void showUniversity(final Model.University u) {
        navLevel = 7;
        clearBody();
        body.addView(crumb("高校校历", new View.OnClickListener() {
            public void onClick(View v) { showUniversities(); }
        }, u.name));

        LinearLayout c = card();
        LinearLayout head = new LinearLayout(this);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout left = new LinearLayout(this);
        left.setOrientation(LinearLayout.VERTICAL);
        left.setLayoutParams(new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        left.addView(tv(u.name, 18, C_TEXT, true));
        left.addView(tv(u.province + u.city + " · 大学", 12, C_SUB, false));
        head.addView(left);

        final String key = "uni|" + u.id;
        final boolean on = trackKeys().contains(key);
        TextView plus = tv(on ? "已关注" : "＋", on ? 12 : 20, on ? C_SUB : C_ACCENT, true);
        plus.setPadding(dp(10), dp(6), dp(10), dp(6));
        plus.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                Set<String> s = new LinkedHashSet<String>(trackKeys());
                if (s.contains(key)) s.remove(key); else s.add(key);
                prefs.edit().putStringSet("tracks", s).apply();
                Toast.makeText(MainActivity.this, s.contains(key) ? "已加入关注" : "已取消关注",
                        Toast.LENGTH_SHORT).show();
                showUniversity(u);
            }
        });
        head.addView(plus);
        c.addView(head);

        Model.Next nx = Model.nextOf(u.holidays);
        for (Model.Holiday h : u.holidays) {
            LinearLayout hr = new LinearLayout(this);
            hr.setOrientation(LinearLayout.VERTICAL);
            hr.setPadding(0, dp(12), 0, dp(2));

            LinearLayout line = new LinearLayout(this);
            line.setOrientation(LinearLayout.HORIZONTAL);
            line.setGravity(Gravity.CENTER_VERTICAL);
            line.addView(tv(h.label, 15, C_TEXT, true));
            if (nx != null && nx.h == h) {
                TextView cd = tv(nx.ongoing ? "进行中" : "还有 " + nx.days + " 天", 11, C_ACCENT, true);
                cd.setPadding(dp(8), 0, 0, 0);
                line.addView(cd);
            }
            hr.addView(line);

            String range = Model.fullPretty(h.start);
            if (h.end != null && h.end.length() > 0) {
                range += " — " + Model.fullPretty(h.end)
                        + (h.endDerived ? "（结束日期为推算）" : "");
            } else {
                range += " 起";
            }
            TextView rg = tv(range, 12, C_SUB, false);
            rg.setPadding(0, dp(4), 0, 0);
            hr.addView(rg);

            if (h.note != null && h.note.length() > 0) {
                TextView nt = tv(h.note, 11, C_WARN, false);
                nt.setPadding(0, dp(3), 0, 0);
                hr.addView(nt);
            }
            c.addView(hr);
        }

        View sb = sourceBox(u.source, null);
        if (sb != null) c.addView(sb);
        body.addView(c);

        body.addView(noteBox("关于高校校历", model.universityNote));
    }

    /** 来源块：分隔线 + 可信等级 + 标题 + 日期 + 打开原文 */
    View sourceBox(final Model.Source s, String extra) {
        if (s == null || s.title.length() == 0) return null;
        LinearLayout wrap = new LinearLayout(this);
        wrap.setOrientation(LinearLayout.VERTICAL);

        View sep = new View(this);
        sep.setBackgroundColor(C_LINE);
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dp(1));
        slp.topMargin = dp(12);
        sep.setLayoutParams(slp);
        wrap.addView(sep);

        TextView t0 = tv("来源 · " + gradeText(s.grade) + "  " + s.publisher, 11, C_SUB, false);
        t0.setPadding(0, dp(10), 0, 0);
        wrap.addView(t0);

        TextView t1 = tv(s.title, 11, C_SUB, false);
        t1.setPadding(0, dp(3), 0, 0);
        wrap.addView(t1);

        if (s.published.length() > 0) {
            String meta = "发布 " + s.published;
            if (s.docNo.length() > 0) meta += " · " + s.docNo;
            wrap.addView(tv(meta, 11, C_SUB, false));
        }

        if (s.url.length() > 0) {
            TextView open = tv("打开原文 ↗", 12, C_ACCENT, true);
            open.setPadding(0, dp(8), 0, 0);
            open.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    try {
                        startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(s.url)));
                    } catch (Exception e) {
                        Toast.makeText(MainActivity.this, "没有可用的浏览器", Toast.LENGTH_SHORT).show();
                    }
                }
            });
            wrap.addView(open);
        }
        return wrap;
    }

    View uniTrackCard(final Model.University u) {
        final String key = "uni|" + u.id;
        Model.Next next = Model.nextOf(u.holidays);

        LinearLayout card = card();
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        LinearLayout left = new LinearLayout(this);
        left.setOrientation(LinearLayout.VERTICAL);
        left.setLayoutParams(new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        left.addView(tv(u.name, 16, C_TEXT, true));

        TextView metaTv = tv(u.province + u.city + " · 大学", 12, C_SUB, false);
        metaTv.setPadding(0, dp(3), 0, 0);
        left.addView(metaTv);

        if (next != null) {
            String when = next.ongoing ? next.h.label + "进行中"
                    : next.h.label + " " + Model.pretty(next.h.start) + " 开始";
            TextView w = tv(when, 12, C_SUB, false);
            w.setPadding(0, dp(5), 0, 0);
            left.addView(w);
            if (next.h.end != null && next.h.end.length() > 0) {
                TextView rg = tv(Model.pretty(next.h.start) + " — " + Model.pretty(next.h.end)
                        + (next.h.endDerived ? "（推算）" : ""), 12, C_SUB, false);
                rg.setPadding(0, dp(2), 0, 0);
                left.addView(rg);
            }
        } else {
            left.addView(tv("暂无假期数据", 12, C_SUB, false));
        }
        row.addView(left);

        if (next != null) {
            LinearLayout right = new LinearLayout(this);
            right.setOrientation(LinearLayout.VERTICAL);
            right.setGravity(Gravity.CENTER);
            TextView bv = tv(next.ongoing ? "放" : String.valueOf(next.days), 30, C_ACCENT, true);
            bv.setGravity(Gravity.CENTER);
            right.addView(bv);
            TextView sv = tv(next.ongoing ? "假中" : "天后", 11, C_SUB, false);
            sv.setGravity(Gravity.CENTER);
            right.addView(sv);
            right.setPadding(dp(12), 0, dp(2), 0);
            row.addView(right);
        }

        card.addView(row);
        card.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) { showUniversity(u); }
        });
        card.setOnLongClickListener(new View.OnLongClickListener() {
            public boolean onLongClick(View v) {
                new AlertDialog.Builder(MainActivity.this)
                        .setMessage("不再关注这所学校？")
                        .setPositiveButton("取消关注", (d, w) -> {
                            Set<String> s = new LinkedHashSet<String>(trackKeys());
                            s.remove(key);
                            prefs.edit().putStringSet("tracks", s).apply();
                            showTracks();
                        })
                        .setNegativeButton("留着", null)
                        .show();
                return true;
            }
        });
        return card;
    }

    // ---------------------------------------------------------------- 休息日

    static class RestRange {
        String label = "";
        String kind = "school";
        long s, e;
    }

    static class RestDay {
        long t;
        String date = "";
        String reason = "";
        String kind = "school";
        boolean rest;
        int day;
        int dow;
    }

    static final String[] WD_CN = {"日", "一", "二", "三", "四", "五", "六"};

    List<RestRange> restRanges(Model.Region r, Model.City c, String stage) {
        List<RestRange> list = new ArrayList<RestRange>();
        if (c != null && stage != null && c.policies.containsKey(stage)) {
            for (Model.Holiday h : c.policies.get(stage).holidays) {
                long s = Model.parseMs(h.start);
                if (s == 0) continue;
                long e = Model.parseMs(h.end);
                RestRange rr = new RestRange();
                rr.label = h.label;
                rr.kind = "term";
                rr.s = s;
                rr.e = e == 0 ? s : e;
                list.add(rr);
            }
        }
        if (r != null && r.seasonal != null && c != null) {
            String shortName = c.name.replace("市", "");
            for (Model.SeasonalItem it : r.seasonal.items) {
                if (it.city.length() > 0 && shortName.length() > 0
                        && !it.city.startsWith(shortName)) continue;
                long s = Model.parseMs(it.start);
                if (s == 0) continue;
                long e = Model.parseMs(it.end);
                RestRange rr = new RestRange();
                rr.label = it.label;
                rr.kind = "seasonal";
                rr.s = s;
                rr.e = e == 0 ? s : e;
                list.add(rr);
            }
        }
        for (Model.Holiday it : model.national.items) {
            long s = Model.parseMs(it.start);
            if (s == 0) continue;
            long e = Model.parseMs(it.end);
            RestRange rr = new RestRange();
            rr.label = it.label;
            rr.kind = "national";
            rr.s = s;
            rr.e = e == 0 ? s : e;
            list.add(rr);
        }
        return list;
    }

    List<RestDay> restTimeline(Model.Region r, Model.City c, String stage, int n) {
        List<RestRange> ranges = restRanges(r, c, stage);
        List<RestDay> out = new ArrayList<RestDay>();
        long base = Model.todayMidnightMs();
        java.text.SimpleDateFormat fmt = new java.text.SimpleDateFormat("yyyy-MM-dd", java.util.Locale.US);
        Calendar cal = Calendar.getInstance();
        for (int i = 0; i < n; i++) {
            long t = base + i * 86400000L;
            RestDay d = new RestDay();
            d.t = t;
            d.date = fmt.format(new java.util.Date(t));
            cal.setTimeInMillis(t);
            d.day = cal.get(Calendar.DAY_OF_MONTH);
            d.dow = cal.get(Calendar.DAY_OF_WEEK) - 1;   // 0=周日
            for (RestRange rr : ranges) {
                if (t >= rr.s && t <= rr.e) {
                    d.rest = true;
                    d.reason = rr.label;
                    d.kind = rr.kind;
                    break;
                }
            }
            if (!d.rest && model.weekendRest.contains(Integer.valueOf(d.dow))) {
                d.rest = true;
                d.reason = "周末";
                d.kind = "weekend";
            }
            out.add(d);
        }
        return out;
    }

    /** 接下来 14 天的条带卡片 */
    android.view.View stripCard(final Model.Region r, final Model.City c, String stage) {
        final int N = 14;
        List<RestDay> tl = restTimeline(r, c, stage, N);
        int restCount = 0;
        for (RestDay d : tl) if (d.rest) restCount++;

        LinearLayout card = card();

        LinearLayout head = new LinearLayout(this);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);
        TextView h1 = tv("接下来 14 天", 15, C_TEXT, true);
        h1.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        head.addView(h1);
        head.addView(tv("休 " + restCount + " 天", 13, C_ACCENT, true));
        card.addView(head);

        HorizontalScrollView hsv = new HorizontalScrollView(this);
        hsv.setHorizontalScrollBarEnabled(false);
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setPadding(0, dp(10), 0, 0);

        long todayMs = Model.todayMidnightMs();
        for (RestDay d : tl) {
            LinearLayout cell = new LinearLayout(this);
            cell.setOrientation(LinearLayout.VERTICAL);
            cell.setGravity(Gravity.CENTER);
            int bg;
            int fg;
            if ("seasonal".equals(d.kind)) { bg = 0xFF16303F; fg = 0xFF5FB6D9; }
            else if ("term".equals(d.kind) || "national".equals(d.kind)) { bg = 0xFF3A2F12; fg = C_WARN; }
            else if ("weekend".equals(d.kind)) { bg = 0xFF1E2733; fg = 0xFF93A0AE; }
            else { bg = 0xFF10151B; fg = 0xFF3E4A58; }
            GradientDrawable gd = new GradientDrawable();
            gd.setColor(bg);
            gd.setCornerRadius(dp(6));
            cell.setBackground(gd);

            LinearLayout.LayoutParams clp = new LinearLayout.LayoutParams(dp(30), dp(48));
            clp.rightMargin = dp(3);
            cell.setLayoutParams(clp);

            TextView num = tv(String.valueOf(d.day), 13, fg, true);
            num.setGravity(Gravity.CENTER);
            cell.addView(num);
            TextView wd = tv(WD_CN[d.dow], 10, fg, false);
            wd.setGravity(Gravity.CENTER);
            cell.addView(wd);

            if (d.t == todayMs) {
                View mark = new View(this);
                mark.setBackgroundColor(C_ACCENT);
                LinearLayout.LayoutParams mlp = new LinearLayout.LayoutParams(dp(30), dp(2));
                mlp.topMargin = dp(2);
                mark.setLayoutParams(mlp);
                cell.addView(mark);
            }
            row.addView(cell);
        }
        hsv.addView(row);
        card.addView(hsv);

        TextView legend = tv("橙=放假　蓝=春秋假　灰=周末　深=上课", 10, C_SUB, false);
        legend.setPadding(0, dp(8), 0, 0);
        card.addView(legend);

        TextView note = tv("按「" + stageLabel(stage) + "」算，周末按周六日双休；"
                + "高中/寄宿制可能周六上课，以学校通知为准。", 10, C_SUB, false);
        note.setPadding(0, dp(4), 0, 0);
        note.setLineSpacing(dp(3), 1f);
        card.addView(note);

        return card;
    }

    // ---------------------------------------------------------------- 待补省份

    void showPending(final Model.Region r) {
        navLevel = 8;
        clearBody();
        body.addView(crumb("全部", new View.OnClickListener() {
            public void onClick(View v) { showAll(); }
        }, r.name));

        LinearLayout c = card();
        c.addView(tv(r.name + " · 数据待补", 17, C_WARN, true));
        String msg = r.pending.length() > 0 ? r.pending : "这个省暂时还没有可核对的数据。";
        TextView t = tv(msg, 13, C_SUB, false);
        t.setPadding(0, dp(8), 0, 0);
        t.setLineSpacing(dp(4), 1f);
        c.addView(t);
        if (r.pendingHint.length() > 0) {
            TextView h = tv(r.pendingHint, 12, C_SUB, false);
            h.setPadding(0, dp(10), 0, 0);
            h.setLineSpacing(dp(4), 1f);
            c.addView(h);
        }
        body.addView(c);

        body.addView(noteBox("为什么空着",
                "这套数据只收能从公开渠道核对到的日期。查不到就写「待补」，"
                        + "不拿别处的估算填进来——填错了比空着更糟。\n"
                        + "已经贴了出处的条目，点开任何一条都能看到原文链接和核对日期。"));

        if (r.seasonal != null) {
            LinearLayout sc = card();
            sc.addView(tv("春秋假", 16, C_TEXT, true));
            if (r.seasonal.policyText.length() > 0) {
                TextView pt = tv(r.seasonal.policyText, 12, C_SUB, false);
                pt.setPadding(0, dp(5), 0, 0);
                pt.setLineSpacing(dp(3), 1f);
                sc.addView(pt);
            }
            if (r.seasonal.items.isEmpty()) {
                TextView wt = tv("具体日期由各市公布，目前还没查到。", 11, C_WARN, false);
                wt.setPadding(0, dp(8), 0, 0);
                sc.addView(wt);
            }
            View sb = sourceBox(r.seasonal.source, null);
            if (sb != null) sc.addView(sb);
            body.addView(sc);
        }
    }

    void showRegion(Model.Region r) {
        navLevel = 2;
        curRegion = r;
        curCity = null;
        clearBody();

        body.addView(crumb("全部", new View.OnClickListener() {
            public void onClick(View v) { showAll(); }
        }, r.name));

        // 这个省还没查到正式文件
        if (r.pending.length() > 0) {
            LinearLayout pb = card();
            pb.addView(tv("这个省还没查到正式文件", 13, C_WARN, true));
            TextView pt = tv(r.pending, 12, C_SUB, false);
            pt.setPadding(0, dp(6), 0, 0);
            pt.setLineSpacing(dp(3), 1f);
            pb.addView(pt);
            if (r.pendingHint.length() > 0) {
                TextView ph = tv(r.pendingHint, 11, C_SUB, false);
                ph.setPadding(0, dp(6), 0, 0);
                ph.setLineSpacing(dp(3), 1f);
                pb.addView(ph);
            }
            body.addView(pb);
        }

        int withData = 0;
        for (Model.City cc : r.cities) if (!cc.policies.isEmpty()) withData++;
        if (r.cities.size() > 1) {
            TextView hint = tv(r.name + "共 " + r.cities.size() + " 个地级行政区，其中 "
                    + withData + " 个已收录数据"
                    + (r.commonPolicies.isEmpty() ? "，其余显示「未收录」" : "；其余按省级口径")
                    + "。点开还能看「接下来 14 天」和春秋假。", 11, C_SUB, false);
            hint.setPadding(dp(4), dp(2), dp(4), dp(10));
            hint.setLineSpacing(dp(3), 1f);
            body.addView(hint);
        }

        for (final Model.City c : r.cities) {
            LinearLayout card = card();
            card.setOrientation(LinearLayout.HORIZONTAL);
            card.setGravity(Gravity.CENTER_VERTICAL);

            LinearLayout left = new LinearLayout(this);
            left.setOrientation(LinearLayout.VERTICAL);
            left.setLayoutParams(new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            left.addView(tv(c.name, 16, C_TEXT, true));

            StringBuilder sb = new StringBuilder();
            for (String sk : model.stageKeys) {
                if (c.policies.containsKey(sk)) {
                    if (sb.length() > 0) sb.append(" · ");
                    sb.append(stageLabel(sk));
                }
            }
            String meta;
            if (sb.length() > 0) meta = sb.toString();
            else if (c.history != null)
                meta = "未收录今年 · " + c.history.year + " 年 " + Model.pretty(c.history.primary) + " 放假";
            else meta = "未收录";
            left.addView(tv(meta, 12, C_SUB, false));
            card.addView(left);

            TextView arrow = tv("›", 22, C_SUB, false);
            arrow.setPadding(dp(8), 0, 0, 0);
            card.addView(arrow);

            card.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    curCity = c;
                    showCity(curRegion, c);
                }
            });
            body.addView(card);
        }

        if (r.commonNote != null && r.commonNote.length() > 0) {
            body.addView(noteBox("全省说明", r.commonNote));
        }
    }

    void showCity(Model.Region r, Model.City c) {
        navLevel = 3;
        curRegion = r;
        curCity = c;
        clearBody();

        body.addView(crumb(r.name, new View.OnClickListener() {
            public void onClick(View v) {
                if (curRegion != null && curRegion.cities.size() > 1) showRegion(curRegion);
                else showAll();
            }
        }, c.name));

        // 接下来 14 天：先挑一个能代表这座城市的学段
        String repStage = null;
        for (String sk0 : model.stageKeys) {
            if (c.policies.containsKey(sk0)) { repStage = sk0; break; }
        }
        if (repStage == null && !model.stageKeys.isEmpty()) repStage = model.stageKeys.get(0);
        if (repStage != null) body.addView(stripCard(r, c, repStage));

        boolean useProv = c.policies.isEmpty() && !r.commonPolicies.isEmpty();

        if (!useProv && c.policies.isEmpty()) {
            if (c.history != null) {
                LinearLayout hc = card();
                hc.addView(tv(c.history.year + " 年（去年）的安排", 15, C_TEXT, true));
                TextView hint = tv("2026-2027 学年还没公布，这是上一学年的，供参考。", 12, C_SUB, false);
                hint.setPadding(0, dp(5), 0, 0);
                hint.setLineSpacing(dp(3), 1f);
                hc.addView(hint);

                hc.addView(tv("义务教育（小学/初中）", 13, C_TEXT, true));
                hc.addView(tv(Model.fullPretty(c.history.primary) + " 放假", 12, C_SUB, false));
                hc.addView(tv("普通高中", 13, C_TEXT, true));
                hc.addView(tv(Model.fullPretty(c.history.senior) + " 放假", 12, C_SUB, false));
                if (c.history.springStart.length() > 0) {
                    hc.addView(tv("春季开学", 13, C_TEXT, true));
                    hc.addView(tv(Model.fullPretty(c.history.springStart), 12, C_SUB, false));
                }
                if (c.history.note.length() > 0) {
                    hc.addView(tv(c.history.note, 11, C_WARN, false));
                }
                if (r.historySource != null) {
                    View sb = sourceBox(r.historySource, null);
                    if (sb != null) hc.addView(sb);
                }
                body.addView(hc);
            } else {
                LinearLayout nb = card();
                nb.addView(tv("该市校历未收录", 14, C_WARN, true));
                TextView nt = tv(r.name + c.name + " 的 2026-2027 学年校历还没查到。"
                        + "上面那条「接下来 14 天」只按周末和全国节假日算的，不算寒暑假。", 12, C_SUB, false);
                nt.setPadding(0, dp(6), 0, 0);
                nt.setLineSpacing(dp(3), 1f);
                nb.addView(nt);
                body.addView(nb);
            }
        }

        for (final String sk : model.stageKeys) {
            Model.Policy p = useProv ? r.commonPolicies.get(sk) : c.policies.get(sk);
            if (p == null) continue;

            LinearLayout card = card();
            LinearLayout headRow = new LinearLayout(this);
            headRow.setOrientation(LinearLayout.HORIZONTAL);
            headRow.setGravity(Gravity.CENTER_VERTICAL);

            LinearLayout left = new LinearLayout(this);
            left.setOrientation(LinearLayout.VERTICAL);
            left.setLayoutParams(new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            left.addView(tv(stageLabel(sk), 18, C_TEXT, true));
            if (useProv) {
                TextView wt = tv("省级口径 · 该市未单独发布，按全省安排", 11, C_WARN, false);
                wt.setPadding(0, dp(3), 0, 0);
                left.addView(wt);
            } else if (p.scope.length() > 0) {
                TextView sc = tv(p.scope, 11, C_SUB, false);
                sc.setPadding(0, dp(3), 0, 0);
                left.addView(sc);
            }
            headRow.addView(left);

            final String tkey = r.code + "|" + c.code + "|" + sk;
            final boolean tracked = trackKeys().contains(tkey);
            TextView plus = tv(tracked ? "已关注" : "＋", tracked ? 12 : 20,
                    tracked ? C_SUB : C_ACCENT, true);
            plus.setPadding(dp(10), dp(6), dp(10), dp(6));
            plus.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    Set<String> s = new LinkedHashSet<String>(trackKeys());
                    if (s.contains(tkey)) {
                        s.remove(tkey);
                        Toast.makeText(MainActivity.this, "已取消关注", Toast.LENGTH_SHORT).show();
                    } else {
                        s.add(tkey);
                        Toast.makeText(MainActivity.this, "已加入关注", Toast.LENGTH_SHORT).show();
                    }
                    prefs.edit().putStringSet("tracks", s).apply();
                    showCity(curRegion, curCity);
                }
            });
            headRow.addView(plus);
            card.addView(headRow);

            for (Model.Holiday h : p.holidays) {
                LinearLayout hr = new LinearLayout(this);
                hr.setOrientation(LinearLayout.VERTICAL);
                hr.setPadding(0, dp(12), 0, dp(2));

                LinearLayout line1 = new LinearLayout(this);
                line1.setOrientation(LinearLayout.HORIZONTAL);
                line1.setGravity(Gravity.CENTER_VERTICAL);

                TextView label = tv(h.label, 15, C_TEXT, true);
                line1.addView(label);

                Model.Next nx = Model.nextOf(p);
                if (nx != null && nx.h == h && !nx.ongoing) {
                    TextView cd = tv("还有 " + nx.days + " 天", 11, C_ACCENT, true);
                    cd.setPadding(dp(8), 0, 0, 0);
                    line1.addView(cd);
                } else if (nx != null && nx.h == h && nx.ongoing) {
                    TextView cd = tv("进行中", 11, C_ACCENT, true);
                    cd.setPadding(dp(8), 0, 0, 0);
                    line1.addView(cd);
                }
                hr.addView(line1);

                String range = Model.fullPretty(h.start);
                if (h.end != null && h.end.length() > 0) {
                    range += " — " + Model.fullPretty(h.end)
                            + (h.endDerived ? "（结束日期为推算）" : "");
                } else {
                    range += " 起";
                }
                TextView rg = tv(range, 12, C_SUB, false);
                rg.setPadding(0, dp(3), 0, 0);
                hr.addView(rg);

                if (h.note != null && h.note.length() > 0) {
                    TextView nt = tv(h.note, 11, C_WARN, false);
                    nt.setPadding(0, dp(3), 0, 0);
                    hr.addView(nt);
                }
                card.addView(hr);
            }

            // 来源
            if (p.source != null && p.source.title.length() > 0) {
                View sep = new View(this);
                sep.setBackgroundColor(C_LINE);
                LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, dp(1));
                slp.topMargin = dp(12);
                sep.setLayoutParams(slp);
                card.addView(sep);

                String gradeTag = gradeText(p.source.grade);
                TextView src = tv("来源 · " + gradeTag + "  " + p.source.publisher, 11, C_SUB, false);
                src.setPadding(0, dp(10), 0, 0);
                card.addView(src);

                TextView st = tv(p.source.title, 11, C_SUB, false);
                st.setPadding(0, dp(3), 0, 0);
                card.addView(st);

                if (p.source.published.length() > 0) {
                    String meta = "发布 " + p.source.published;
                    if (p.source.docNo.length() > 0) meta += " · " + p.source.docNo;
                    card.addView(tv(meta, 11, C_SUB, false));
                }

                if (p.source.url.length() > 0) {
                    final String url = p.source.url;
                    TextView open = tv("打开原文 ↗", 12, C_ACCENT, true);
                    open.setPadding(0, dp(8), 0, 0);
                    open.setOnClickListener(new View.OnClickListener() {
                        public void onClick(View v) {
                            try {
                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url)));
                            } catch (Exception e) {
                                Toast.makeText(MainActivity.this, "没有可用的浏览器", Toast.LENGTH_SHORT).show();
                            }
                        }
                    });
                    card.addView(open);
                }
            }
            body.addView(card);
        }

        // 大学
        // 春秋假
        if (r.seasonal != null && !r.seasonal.items.isEmpty()) {
            String shortName = c.name.replace("市", "");
            List<Model.SeasonalItem> mine = new ArrayList<Model.SeasonalItem>();
            for (Model.SeasonalItem it : r.seasonal.items) {
                if (it.city.length() > 0 && shortName.length() > 0
                        && !it.city.startsWith(shortName)) continue;
                mine.add(it);
            }
            if (!mine.isEmpty()) {
                LinearLayout sc = card();
                sc.addView(tv("春秋假", 16, C_TEXT, true));
                if (r.seasonal.policyText.length() > 0) {
                    TextView pt = tv(r.seasonal.policyText, 12, C_SUB, false);
                    pt.setPadding(0, dp(5), 0, 0);
                    pt.setLineSpacing(dp(3), 1f);
                    sc.addView(pt);
                }
                for (Model.SeasonalItem it : mine) {
                    LinearLayout hr = new LinearLayout(this);
                    hr.setOrientation(LinearLayout.VERTICAL);
                    hr.setPadding(0, dp(10), 0, 0);
                    hr.addView(tv(it.label, 14, C_TEXT, true));
                    String range = it.start.equals(it.end)
                            ? Model.fullPretty(it.start)
                            : Model.fullPretty(it.start) + " — " + Model.fullPretty(it.end);
                    if (it.scope.length() > 0) range += " · " + it.scope;
                    TextView rt = tv(range, 12, C_SUB, false);
                    rt.setPadding(0, dp(3), 0, 0);
                    hr.addView(rt);
                    if (it.city.length() > 0 && !it.city.equals(c.name)) {
                        hr.addView(tv(it.city, 11, C_SUB, false));
                    }
                    if (it.note.length() > 0) {
                        TextView nt = tv(it.note, 11, C_WARN, false);
                        nt.setPadding(0, dp(3), 0, 0);
                        hr.addView(nt);
                    }
                    sc.addView(hr);
                }
                View sb = sourceBox(r.seasonal.source, null);
                if (sb != null) sc.addView(sb);
                body.addView(sc);
            }
        }

        if (!c.policies.containsKey("university")) {
            LinearLayout card = card();
            card.addView(tv("大学", 18, C_TEXT, true));
            TextView sc = tv("高校校历由各校自行制定", 11, C_SUB, false);
            sc.setPadding(0, dp(3), 0, 0);
            card.addView(sc);
            TextView note = tv(model.universityNote, 12, C_SUB, false);
            note.setPadding(0, dp(10), 0, 0);
            note.setLineSpacing(dp(3), 1f);
            card.addView(note);
            body.addView(card);
        }
    }

    // ---------------------------------------------------------------- 数据页


    // ---------------------------------------------------------------- 计划页

    List<PlanModel.Plan> plans = null;

    List<PlanModel.Plan> plans() {
        if (plans == null) plans = PlanModel.load(this);
        return plans;
    }

    void savePlans() {
        PlanModel.save(this, plans());
        ReminderReceiver.rescheduleAll(this);
    }

    View.OnClickListener planOpen(final int idx) {
        return new View.OnClickListener() {
            public void onClick(View v) {
                showPlan(idx);
            }
        };
    }

    void showPlans() {
        navLevel = 9;
        markTab(2);
        clearBody();
        TextView secT = tv("假期计划", 17, C_TEXT, true);
        secT.setPadding(0, 0, 0, dp(6));
        body.addView(secT);

        TextView hint = tv("给某个假期排计划：每天做什么，能插图。存在手机里，换设备不同步。",
                12, C_SUB, false);
        hint.setPadding(0, 0, 0, dp(10));
        body.addView(hint);

        List<PlanModel.Plan> list = plans();
        if (list.isEmpty()) {
            LinearLayout empty = card();
            empty.addView(tv("还没有计划", 15, C_TEXT, true));
            TextView t2 = tv("点下面的「＋ 新建计划」，或者用「一键生成」。", 12, C_SUB, false);
            t2.setPadding(0, dp(6), 0, 0);
            empty.addView(t2);
            body.addView(empty);
        }

        for (int i = 0; i < list.size(); i++) {
            final int idx = i;
            PlanModel.Plan p = list.get(i);
            LinearLayout c = card();
            c.setOrientation(LinearLayout.HORIZONTAL);
            c.setGravity(Gravity.CENTER_VERTICAL);

            LinearLayout left = new LinearLayout(this);
            left.setOrientation(LinearLayout.VERTICAL);
            left.setLayoutParams(new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            left.addView(tv(p.title, 16, C_TEXT, true));
            String meta = (p.start.length() > 0 ? Model.pretty(p.start) : "?")
                    + " — " + (p.end.length() > 0 ? Model.pretty(p.end) : "?")
                    + " · " + p.pages.size() + " 页";
            if (p.remind) meta += " · 每天 " + p.remindTime + " 提醒";
            left.addView(tv(meta, 12, C_SUB, false));
            c.addView(left);

            TextView arrow = tv("›", 22, C_SUB, false);
            arrow.setPadding(dp(8), 0, 0, 0);
            c.addView(arrow);
            c.setOnClickListener(planOpen(idx));
            body.addView(c);
        }

        TextView add = tv("＋ 新建计划", 15, C_ACCENT, true);
        add.setGravity(Gravity.CENTER);
        add.setPadding(0, dp(14), 0, dp(14));
        add.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) { newPlanDialog(); }
        });
        body.addView(add);
    }

    void newPlanDialog() {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(20), dp(8), dp(20), 0);

        final EditText titleIn = new EditText(this);
        titleIn.setHint("计划名字，比如「寒假计划」");
        titleIn.setTextColor(C_TEXT);
        titleIn.setHintTextColor(C_SUB);
        box.addView(titleIn);

        new android.app.AlertDialog.Builder(this)
                .setTitle("新建计划")
                .setMessage("下一步选开始日期")
                .setView(box)
                .setPositiveButton("选日期", new android.content.DialogInterface.OnClickListener() {
                    public void onClick(android.content.DialogInterface d, int w) {
                        String t = titleIn.getText().toString().trim();
                        if (t.length() == 0) t = "假期计划";
                        pendingTitle = t;
                        pickStartDate();
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    String pendingTitle = "";
    String pendingStart = "", pendingEnd = "";

    void pickStartDate() {
        java.util.Calendar c = java.util.Calendar.getInstance();
        new android.app.DatePickerDialog(this, new android.app.DatePickerDialog.OnDateSetListener() {
            public void onDateSet(android.widget.DatePicker v, int y, int m, int d) {
                pendingStart = String.format("%04d-%02d-%02d", y, m + 1, d);
                pickEndDate();
            }
        }, c.get(java.util.Calendar.YEAR), c.get(java.util.Calendar.MONTH),
                c.get(java.util.Calendar.DAY_OF_MONTH)).show();
    }

    void pickEndDate() {
        java.util.Calendar c = java.util.Calendar.getInstance();
        try {
            java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd");
            c.setTime(f.parse(pendingStart));
            c.add(java.util.Calendar.DAY_OF_MONTH, 20);
        } catch (Exception e) { }
        new android.app.DatePickerDialog(this, new android.app.DatePickerDialog.OnDateSetListener() {
            public void onDateSet(android.widget.DatePicker v, int y, int m, int d) {
                String end = String.format("%04d-%02d-%02d", y, m + 1, d);
                if (end.compareTo(pendingStart) < 0) end = pendingStart;
                createPlan(pendingTitle, pendingStart, end);
            }
        }, c.get(java.util.Calendar.YEAR), c.get(java.util.Calendar.MONTH),
                c.get(java.util.Calendar.DAY_OF_MONTH)).show();
    }

    void createPlan(String title, String start, String end) {
        PlanModel.Plan p = new PlanModel.Plan();
        p.id = "p" + System.currentTimeMillis();
        p.title = title;
        p.start = start;
        p.end = end;
        plans().add(p);
        savePlans();
        showPlan(plans().size() - 1);
    }

    void showPlan(final int planIdx) {
        navLevel = 9;
        markTab(2);
        clearBody();
        final PlanModel.Plan p = plans().get(planIdx);

        LinearLayout cr = new LinearLayout(this);
        cr.setOrientation(LinearLayout.HORIZONTAL);
        cr.setPadding(0, 0, 0, dp(10));
        TextView back = tv("‹ 计划", 13, C_ACCENT, true);
        back.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) { showPlans(); }
        });
        cr.addView(back);
        body.addView(cr);

        LinearLayout head = card();
        head.addView(tv(p.title, 18, C_TEXT, true));
        TextView range = tv((p.start.length() > 0 ? Model.fullPretty(p.start) : "?")
                + " — " + (p.end.length() > 0 ? Model.fullPretty(p.end) : "?"), 12, C_SUB, false);
        range.setPadding(0, dp(4), 0, 0);
        head.addView(range);

        LinearLayout btns = new LinearLayout(this);
        btns.setOrientation(LinearLayout.HORIZONTAL);
        btns.setPadding(0, dp(10), 0, 0);

        TextView addPage = tv("＋ 加一页", 13, C_ACCENT, true);
        addPage.setPadding(0, dp(6), dp(16), dp(6));
        addPage.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                PlanModel.Page pg = new PlanModel.Page();
                PlanModel.PageElement el = new PlanModel.PageElement();
                el.type = "text";
                el.text = "新的一页";
                el.size = 22;
                pg.elements.add(el);
                p.pages.add(pg);
                savePlans();
                showPlan(planIdx);
            }
        });
        btns.addView(addPage);

        TextView gen = tv("✨ 一键生成", 13, C_ACCENT, true);
        gen.setPadding(0, dp(6), dp(16), dp(6));
        gen.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) { genFormDialog(planIdx); }
        });
        btns.addView(gen);

        TextView del = tv("删除", 13, C_WARN, true);
        del.setPadding(0, dp(6), 0, dp(6));
        del.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                new android.app.AlertDialog.Builder(MainActivity.this)
                        .setTitle("删掉「" + p.title + "」？")
                        .setPositiveButton("删", new android.content.DialogInterface.OnClickListener() {
                            public void onClick(android.content.DialogInterface d, int w) {
                                plans().remove(planIdx);
                                savePlans();
                                showPlans();
                            }
                        })
                        .setNegativeButton("算了", null)
                        .show();
            }
        });
        btns.addView(del);
        head.addView(btns);
        body.addView(head);

        if (p.pages.isEmpty()) {
            LinearLayout empty = card();
            empty.addView(tv("还没有页", 15, C_TEXT, true));
            TextView t2 = tv("点「加一页」，或者「一键生成」按假期长度排。", 12, C_SUB, false);
            t2.setPadding(0, dp(6), 0, 0);
            empty.addView(t2);
            body.addView(empty);
        }

        for (int i = 0; i < p.pages.size(); i++) {
            final int pgIdx = i;
            PlanModel.Page pg = p.pages.get(i);
            LinearLayout c = card();
            c.addView(tv("第 " + (i + 1) + " 页" + (pg.date.length() > 0 ? " · " + Model.pretty(pg.date) : ""),
                    12, C_ACCENT, true));

            for (PlanModel.PageElement el : pg.elements) {
                if ("text".equals(el.type)) {
                    TextView t = tv(el.text, el.size > 0 ? el.size : 15, C_TEXT, false);
                    t.setPadding(0, dp(6), 0, 0);
                    t.setLineSpacing(dp(3), 1f);
                    c.addView(t);
                } else if ("image".equals(el.type)) {
                    android.widget.ImageView iv = new android.widget.ImageView(this);
                    LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                            ViewGroup.LayoutParams.MATCH_PARENT, dp(180));
                    lp.topMargin = dp(8);
                    iv.setLayoutParams(lp);
                    iv.setScaleType(android.widget.ImageView.ScaleType.CENTER_CROP);
                    if (el.src != null && el.src.length() > 0) {
                        android.graphics.Bitmap bm = android.graphics.BitmapFactory.decodeFile(el.src);
                        if (bm != null) iv.setImageBitmap(bm);
                    }
                    c.addView(iv);
                } else {
                    final String mp = el.src;
                    TextView t = tv(("video".equals(el.type) ? "▶ " : "♪ ")
                            + (el.name.length() > 0 ? el.name : "文件")
                            + "（点这里播放）", 12, C_ACCENT, false);
                    t.setPadding(0, dp(6), 0, 0);
                    t.setOnClickListener(new View.OnClickListener() {
                        public void onClick(View v) { openMedia(mp); }
                    });
                    c.addView(t);
                }
            }

            LinearLayout ops = new LinearLayout(this);
            ops.setOrientation(LinearLayout.HORIZONTAL);
            ops.setPadding(0, dp(8), 0, 0);

            TextView edit = tv("编辑", 12, C_ACCENT, true);
            edit.setPadding(0, dp(4), dp(16), dp(4));
            edit.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) { editPage(planIdx, pgIdx); }
            });
            ops.addView(edit);

            TextView rm = tv("删", 12, C_WARN, true);
            rm.setPadding(0, dp(4), 0, dp(4));
            rm.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    p.pages.remove(pgIdx);
                    savePlans();
                    showPlan(planIdx);
                }
            });
            ops.addView(rm);
            c.addView(ops);
            body.addView(c);
        }
    }

    void editPage(final int planIdx, final int pgIdx) {
        final PlanModel.Plan p = plans().get(planIdx);
        final PlanModel.Page pg = p.pages.get(pgIdx);

        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(20), dp(8), dp(20), 0);

        final EditText dateIn = new EditText(this);
        dateIn.setHint("这一页的日期，可空。2027-01-19");
        dateIn.setText(pg.date);
        dateIn.setTextColor(C_TEXT);
        dateIn.setHintTextColor(C_SUB);
        box.addView(dateIn);

        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setPadding(0, dp(8), 0, 0);

        TextView addText = tv("＋ 文字", 13, C_ACCENT, true);
        addText.setPadding(0, dp(6), dp(18), dp(6));
        addText.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                pg.date = dateIn.getText().toString().trim();
                PlanModel.PageElement el = new PlanModel.PageElement();
                el.type = "text";
                el.text = "点这里改字";
                el.size = 16;
                pg.elements.add(el);
                savePlans();
                editPage(planIdx, pgIdx);
            }
        });
        row.addView(addText);

        TextView addImg = tv("＋ 图片", 13, C_ACCENT, true);
        addImg.setPadding(0, dp(6), 0, dp(6));
        addImg.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                pg.date = dateIn.getText().toString().trim();
                savePlans();
                pickImageFor(planIdx, pgIdx);
            }
        });
        row.addView(addImg);
        box.addView(row);

        // 每个元素一行，文字可改
        for (int i = 0; i < pg.elements.size(); i++) {
            final int eIdx = i;
            final PlanModel.PageElement el = pg.elements.get(i);
            LinearLayout row2 = new LinearLayout(this);
            row2.setOrientation(LinearLayout.HORIZONTAL);
            row2.setGravity(Gravity.CENTER_VERTICAL);
            row2.setPadding(0, dp(6), 0, dp(6));

            if ("text".equals(el.type)) {
                final EditText te = new EditText(this);
                te.setText(el.text);
                te.setTextColor(C_TEXT);
                te.setLayoutParams(new LinearLayout.LayoutParams(0,
                        ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
                te.addTextChangedListener(new android.text.TextWatcher() {
                    public void beforeTextChanged(CharSequence s, int a, int b, int c) { }
                    public void onTextChanged(CharSequence s, int a, int b, int c) { }
                    public void afterTextChanged(android.text.Editable s) {
                        el.text = s.toString();
                    }
                });
                row2.addView(te);
            } else {
                TextView nm = tv(("image".equals(el.type) ? "图" : ("video".equals(el.type) ? "视频" : "音乐"))
                        + " · " + (el.name.length() > 0 ? el.name : "文件"),
                        12, C_SUB, false);
                nm.setLayoutParams(new LinearLayout.LayoutParams(0,
                        ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
                row2.addView(nm);
            }

            TextView x = tv("删", 12, C_WARN, true);
            x.setPadding(dp(12), 0, 0, 0);
            x.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    pg.elements.remove(eIdx);
                    savePlans();
                    editPage(planIdx, pgIdx);
                }
            });
            row2.addView(x);
            box.addView(row2);
        }

        new android.app.AlertDialog.Builder(this)
                .setTitle("第 " + (pgIdx + 1) + " 页")
                .setView(new android.widget.ScrollView(this) {{ addView(box); }})
                .setPositiveButton("存", new android.content.DialogInterface.OnClickListener() {
                    public void onClick(android.content.DialogInterface d, int w) {
                        pg.date = dateIn.getText().toString().trim();
                        savePlans();
                        showPlan(planIdx);
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    void pickImageFor(final int planIdx, final int pgIdx) {
        android.content.Intent it = new android.content.Intent(android.content.Intent.ACTION_GET_CONTENT);
        it.setType("image/*");
        try {
            startActivityForResult(android.content.Intent.createChooser(it, "选一张图"), 2001);
            pendingPlan = planIdx;
            pendingPage = pgIdx;
        } catch (Exception e) {
            android.widget.Toast.makeText(this, "没有文件管理器", android.widget.Toast.LENGTH_SHORT).show();
        }
    }

    int pendingPlan = -1, pendingPage = -1;

    @Override
    protected void onActivityResult(int req, int res, android.content.Intent data) {
        super.onActivityResult(req, res, data);
        if (req == 2001 && res == RESULT_OK && data != null && data.getData() != null) {
            try {
                String path = copyToFiles(data.getData());
                if (path != null && pendingPlan >= 0 && pendingPage >= 0) {
                    PlanModel.Plan p = plans().get(pendingPlan);
                    PlanModel.Page pg = p.pages.get(pendingPage);
                    PlanModel.PageElement el = new PlanModel.PageElement();
                    el.type = "image";
                    el.src = path;
                    el.name = path.substring(path.lastIndexOf('/') + 1);
                    pg.elements.add(el);
                    savePlans();
                    editPage(pendingPlan, pendingPage);
                }
            } catch (Exception e) {
                android.widget.Toast.makeText(this, "图读不进来：" + e.getMessage(),
                        android.widget.Toast.LENGTH_LONG).show();
            }
        }
    }

    void openMedia(String path) {
        if (path == null || path.length() == 0) return;
        try {
            java.io.File f = new java.io.File(path);
            if (!f.exists()) {
                android.widget.Toast.makeText(this, "文件不在了", android.widget.Toast.LENGTH_SHORT).show();
                return;
            }
            String type = java.net.URLConnection.guessContentTypeFromName(path);
            if (type == null) type = path.toLowerCase().endsWith(".mp3") ? "audio/*" : "video/*";
            android.content.Intent it = new android.content.Intent(android.content.Intent.ACTION_VIEW);
            it.setDataAndType(android.net.Uri.fromFile(f), type);
            it.addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivity(it);
        } catch (Exception e) {
            android.widget.Toast.makeText(this, "没有能打开它的播放器", android.widget.Toast.LENGTH_SHORT).show();
        }
    }

    String copyToFiles(android.net.Uri uri) {
        try {
            java.io.InputStream in = getContentResolver().openInputStream(uri);
            if (in == null) return null;
            java.io.File dir = new java.io.File(getFilesDir(), "plans");
            if (!dir.exists()) dir.mkdirs();
            java.io.File f = new java.io.File(dir, "img_" + System.currentTimeMillis() + ".jpg");
            java.io.FileOutputStream out = new java.io.FileOutputStream(f);
            byte[] buf = new byte[64 * 1024];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
            in.close();
            out.close();
            return f.getAbsolutePath();
        } catch (Exception e) {
            return null;
        }
    }

    // ---------------------------------------------------------------- 生成表单

    String gStyle = "混合";
    EditText gDest, gPeople, gBudget, gHobby, gStay, gTransport, gFood, gMustGo, gBack;
    EditText gSubject, gHours, gGoal, gWeak, gWhere;
    EditText gSleep, gWantDo, gDontDo, gMustFinish;
    EditText gRest, gExtra, gCustom;

    void genFormDialog(final int planIdx) {
        final PlanModel.Plan p = plans().get(planIdx);
        gStyle = "混合";

        android.widget.ScrollView sv = new android.widget.ScrollView(this);
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(18), dp(8), dp(18), dp(8));

        // 类型
        final TextView[] chips = new TextView[5];
        String[] styles = {"旅行", "学习", "休息", "混合", "自定义"};
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        for (int i = 0; i < styles.length; i++) {
            final int idx = i;
            final String st = styles[i];
            TextView t = tv(st, 12, C_SUB, true);
            t.setGravity(Gravity.CENTER);
            t.setPadding(dp(10), dp(6), dp(10), dp(6));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0,
                    ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            t.setLayoutParams(lp);
            t.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    gStyle = st;
                    for (int k = 0; k < chips.length; k++)
                        chips[k].setTextColor(chips[k].getText().toString().equals(gStyle) ? C_ACCENT : C_SUB);
                    showGroup(gStyle);
                }
            });
            chips[i] = t;
            row.addView(t);
        }
        box.addView(row);

        // 旅行
        gDest = addFieldTo(box, "去哪", "成都、西安、大理", "trip");
        gPeople = addFieldTo(box, "几个人", "2 人 / 一家三口", "trip");
        gBudget = addFieldTo(box, "预算", "3000 元以内", "trip");
        gHobby = addFieldTo(box, "兴趣爱好", "吃、拍照、博物馆", "trip");
        gStay = addFieldTo(box, "住宿要求", "干净就行 / 民宿", "trip");
        gTransport = addFieldTo(box, "出行方式", "高铁 / 自驾 / 飞机", "trip");
        gFood = addFieldTo(box, "吃喝讲究", "不吃辣 / 想吃火锅", "trip");
        gMustGo = addFieldTo(box, "非去不可", "熊猫基地", "trip");
        gBack = addFieldTo(box, "返程安排", "最后一天下午到家", "trip");

        // 学习
        gSubject = addFieldTo(box, "主要学什么", "数学和英语", "study");
        gHours = addFieldTo(box, "每天学多久", "上午 3 小时", "study");
        gGoal = addFieldTo(box, "目标", "补差 / 预习 / 赶作业", "study");
        gWeak = addFieldTo(box, "薄弱在哪", "几何、完形填空", "study");
        gWhere = addFieldTo(box, "在哪学", "家里 / 图书馆", "study");

        // 休息
        gSleep = addFieldTo(box, "作息", "几点睡几点起", "rest");
        gWantDo = addFieldTo(box, "想做的事", "看完那本书", "rest");
        gDontDo = addFieldTo(box, "不想做的事", "不想出门", "rest");
        gMustFinish = addFieldTo(box, "必须完成的", "有个作业要交", "rest");

        // 自定义
        gCustom = addFieldTo(box, "自己写（一行一页）", "第一天：去都江堰", "custom");

        // 通用
        gRest = addFieldTo(box, "每天几点休息", "晚上 11 点前睡", "common");
        gExtra = addFieldTo(box, "还想补充什么", "随便写", "common");

        sv.addView(box);
        showGroup(gStyle);
        new android.app.AlertDialog.Builder(this)
                .setTitle("生成计划")
                .setView(sv)
                .setPositiveButton("生成", new android.content.DialogInterface.OnClickListener() {
                    public void onClick(android.content.DialogInterface d, int w) {
                        genFromForm(planIdx, p);
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    java.util.Map<String, java.util.List<View>> fieldGroups =
            new java.util.HashMap<String, java.util.List<View>>();

    EditText addField(LinearLayout box, String label, String hint) {
        return addFieldTo(box, label, hint, "trip");
    }

    EditText addFieldTo(LinearLayout box, String label, String hint, String group) {
        LinearLayout wrap = new LinearLayout(this);
        wrap.setOrientation(LinearLayout.VERTICAL);

        TextView l = tv(label, 11, C_SUB, false);
        l.setPadding(0, dp(8), 0, dp(2));
        wrap.addView(l);

        EditText e = new EditText(this);
        e.setHint(hint);
        e.setTextColor(C_TEXT);
        e.setHintTextColor(0xFF5A6472);
        e.setTextSize(13);
        e.setPadding(dp(8), dp(4), dp(8), dp(4));
        wrap.addView(e);

        box.addView(wrap);
        java.util.List<View> g = fieldGroups.get(group);
        if (g == null) { g = new java.util.ArrayList<View>(); fieldGroups.put(group, g); }
        g.add(wrap);
        return e;
    }

    void showGroup(String style) {
        java.util.Set<String> on = new java.util.HashSet<String>();
        if ("旅行".equals(style)) { on.add("trip"); }
        else if ("学习".equals(style)) { on.add("study"); }
        else if ("休息".equals(style)) { on.add("rest"); }
        else if ("自定义".equals(style)) { on.add("custom"); }
        else { on.add("trip"); on.add("study"); on.add("rest"); on.add("custom"); }
        on.add("common");
        for (String k : fieldGroups.keySet()) {
            boolean vis = on.contains(k);
            for (View v : fieldGroups.get(k)) v.setVisibility(vis ? View.VISIBLE : View.GONE);
        }
    }

    String val(EditText e) {
        return e == null ? "" : e.getText().toString().trim();
    }

    void genFromForm(int planIdx, PlanModel.Plan p) {
        String style = gStyle;
        if ("自定义".equals(style)) {
            String cc = val(gCustom);
            if (cc.length() > 0) {
                genCustomPages(planIdx, p, cc);
                return;
            }
            android.widget.Toast.makeText(this, "自定义那栏是空的，写了才能生成",
                    android.widget.Toast.LENGTH_SHORT).show();
            return;
        }
        genPages(planIdx, style);
    }

    void genCustomPages(int planIdx, PlanModel.Plan p, String text) {
        long st = 0, en = 0;
        try {
            java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd");
            st = f.parse(p.start).getTime();
            en = f.parse(p.end).getTime();
        } catch (Exception e) {
            android.widget.Toast.makeText(this, "假期区间不对", android.widget.Toast.LENGTH_SHORT).show();
            return;
        }
        int days = (int) ((en - st) / 86400000L) + 1;
        if (days < 1) days = 1;
        String[] parts = text.split("\n");
        java.util.List<String> lines = new java.util.ArrayList<String>();
        for (String one : parts) {
            String t = one.trim();
            if (t.length() > 0) lines.add(t);
        }
        if (lines.isEmpty()) return;

        p.pages.clear();
        java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd");
        int n = lines.size();
        for (int i = 0; i < n; i++) {
            int off = n == 1 ? 0 : (int) Math.round((double) i * (days - 1) / (n - 1));
            if (off >= days) off = days - 1;
            PlanModel.Page pg = new PlanModel.Page();
            pg.date = f.format(new java.util.Date(st + off * 86400000L));

            String line = lines.get(i);
            String title = line, note = "";
            int colon = line.indexOf('：');
            if (colon <= 0) colon = line.indexOf(':');
            if (colon > 0 && colon < line.length() - 1 && colon <= 10) {
                title = line.substring(0, colon).trim();
                note = line.substring(colon + 1).trim();
            }
            PlanModel.PageElement e1 = new PlanModel.PageElement();
            e1.type = "text";
            e1.text = title;
            e1.size = 24;
            pg.elements.add(e1);
            if (note.length() > 0) {
                PlanModel.PageElement e2 = new PlanModel.PageElement();
                e2.type = "text";
                e2.text = note;
                e2.size = 15;
                pg.elements.add(e2);
            }
            p.pages.add(pg);
        }
        savePlans();
        showPlan(planIdx);
        android.widget.Toast.makeText(this, "生成了 " + n + " 页", android.widget.Toast.LENGTH_SHORT).show();
    }

    void genPages(int planIdx, String style) {
        PlanModel.Plan p = plans().get(planIdx);
        long st = 0, en = 0;
        try {
            java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd");
            st = f.parse(p.start).getTime();
            en = f.parse(p.end).getTime();
        } catch (Exception e) {
            android.widget.Toast.makeText(this, "假期区间不对", android.widget.Toast.LENGTH_SHORT).show();
            return;
        }
        int days = (int) ((en - st) / 86400000L) + 1;
        if (days <= 0) {
            android.widget.Toast.makeText(this, "结束比开始还早", android.widget.Toast.LENGTH_SHORT).show();
            return;
        }
        int n = Math.min(8, Math.max(3, days / 4 + 2));
        int step = Math.max(1, days / n);
        boolean study = style.contains("学习") || style.contains("混合");
        boolean trip = style.contains("旅行") || style.contains("混合");

        p.pages.clear();
        String[] tripText = {"收拾行李，查路线", "出发，路上拍点照片", "到目的地，先逛老街"};
        String[] studyAM = {"早起背单词", "做一套卷子", "整理错题本"};

        java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd");
        for (int i = 0; i < n; i++) {
            long t = st + Math.min(days - 1, i * step) * 86400000L;
            PlanModel.Page pg = new PlanModel.Page();
            pg.date = f.format(new java.util.Date(t));
            String title, note;
            if (i == 0) { title = "假期开始 · 先歇一天"; note = "别急着排满。房间收拾一下，睡个够。"; }
            else if (i == n - 1) { title = "收心 · 准备开学"; note = "作业过一遍有没有漏的，作息往回调两天。"; }
            else if (trip && i <= 2) { title = "出门走走"; note = tripText[(i - 1) % tripText.length]; }
            else if (study) { title = "上午学，下午玩"; note = studyAM[(i - 1) % studyAM.length] + "；下午自由活动。"; }
            else { title = "慢慢过"; note = "睡到自然醒，出门散步。"; }

            PlanModel.PageElement e1 = new PlanModel.PageElement();
            e1.type = "text";
            e1.text = title;
            e1.size = 24;
            pg.elements.add(e1);
            PlanModel.PageElement e2 = new PlanModel.PageElement();
            e2.type = "text";
            e2.text = note;
            e2.size = 15;
            pg.elements.add(e2);
            p.pages.add(pg);
        }
        savePlans();
        showPlan(planIdx);
        android.widget.Toast.makeText(this, "生成了 " + n + " 页", android.widget.Toast.LENGTH_SHORT).show();
    }

    void showData() {
        navLevel = 4;
        markTab(2);
        clearBody();

        LinearLayout c1 = card();
        c1.addView(tv("数据状态", 16, C_TEXT, true));
        String srcName = model.fromUpdate ? "在线更新导入" : "安装包内置";
        addKv(c1, "学年", model.academicYear);
        addKv(c1, "更新时间", model.updatedAt);
        addKv(c1, "当前来源", srcName);
        int cities = 0, policies = 0, hols = 0;
        for (Model.Region r : model.regions) {
            cities += r.cities.size();
            for (Model.City c : r.cities) {
                policies += c.policies.size();
                for (Model.Policy p : c.policies.values()) hols += p.holidays.size();
            }
        }
        addKv(c1, "覆盖", model.regions.size() + " 个省级地区 / " + cities + " 个城市 / " + hols + " 条假期记录");
        addKv(c1, "另收录", model.national.items.size() + " 个法定节假日 · "
                + model.universities.size() + " 所高校校历");
        body.addView(c1);

        // 更新
        LinearLayout c2 = card();
        c2.addView(tv("更新数据", 16, C_TEXT, true));
        TextView desc = tv("填一个 JSON 地址，能拉到新数据就替换本地的。地址为空时不会联网。",
                12, C_SUB, false);
        desc.setPadding(0, dp(6), 0, dp(10));
        desc.setLineSpacing(dp(3), 1f);
        c2.addView(desc);

        final EditText input = new EditText(this);
        input.setHint("https://…/holidays.json");
        input.setHintTextColor(0xFF5A6675);
        input.setTextColor(C_TEXT);
        input.setTextSize(12);
        input.setSingleLine(true);
        input.setText(prefs.getString("updateUrl", ""));
        GradientDrawable ebg = new GradientDrawable();
        ebg.setColor(C_CARD2);
        ebg.setCornerRadius(dp(8));
        input.setBackground(ebg);
        input.setPadding(dp(10), dp(8), dp(10), dp(8));
        c2.addView(input, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        LinearLayout btnRow = new LinearLayout(this);
        btnRow.setOrientation(LinearLayout.HORIZONTAL);
        btnRow.setPadding(0, dp(10), 0, 0);

        TextView btn = pill("检查更新", C_ACCENT, 0xFF0B1A22);
        btn.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                String u = input.getText().toString().trim();
                prefs.edit().putString("updateUrl", u).apply();
                if (u.length() == 0) {
                    Toast.makeText(MainActivity.this, "先填地址", Toast.LENGTH_SHORT).show();
                    return;
                }
                doUpdate(u);
            }
        });
        btnRow.addView(btn);

        TextView reset = pill("恢复内置数据", 0xFF2A323D, C_TEXT);
        reset.setPadding(dp(14), dp(9), dp(14), dp(9));
        LinearLayout.LayoutParams rlp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rlp.leftMargin = dp(8);
        reset.setLayoutParams(rlp);
        reset.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                new File(getFilesDir(), "holidays.json").delete();
                model = Model.load(MainActivity.this);
                Toast.makeText(MainActivity.this, "已回到内置数据", Toast.LENGTH_SHORT).show();
                showData();
            }
        });
        btnRow.addView(reset);
        c2.addView(btnRow);
        body.addView(c2);

        // 来源清单
        LinearLayout c3 = card();
        c3.addView(tv("数据来源", 16, C_TEXT, true));
        TextView d3 = tv("每条假期在详情页都标了出处。官方 = 教育厅/政府原文；媒体 = 转载官方校历；汇总 = 第三方整理，可能有误。",
                12, C_SUB, false);
        d3.setPadding(0, dp(6), 0, dp(4));
        d3.setLineSpacing(dp(3), 1f);
        c3.addView(d3);

        List<String> seen = new ArrayList<String>();
        for (Model.Region r : model.regions) {
            for (Model.City c : r.cities) {
                for (Model.Policy p : c.policies.values()) {
                    if (p.source == null || p.source.url.length() == 0) continue;
                    if (seen.contains(p.source.url)) continue;
                    seen.add(p.source.url);
                    final String url = p.source.url;
                    LinearLayout item = new LinearLayout(this);
                    item.setOrientation(LinearLayout.VERTICAL);
                    item.setPadding(0, dp(12), 0, dp(2));
                    item.addView(tv("[" + gradeText(p.source.grade) + "] " + p.source.publisher, 12, C_TEXT, true));
                    TextView t = tv(p.source.title, 11, C_SUB, false);
                    t.setPadding(0, dp(3), 0, 0);
                    item.addView(t);
                    TextView o = tv("打开 ↗", 12, C_ACCENT, true);
                    o.setPadding(0, dp(5), 0, 0);
                    o.setOnClickListener(new View.OnClickListener() {
                        public void onClick(View v) {
                            try {
                                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url)));
                            } catch (Exception e) {
                                Toast.makeText(MainActivity.this, "打不开", Toast.LENGTH_SHORT).show();
                            }
                        }
                    });
                    item.addView(o);
                    c3.addView(item);
                }
            }
        }
        body.addView(c3);

        // 免责
        body.addView(noteBox("它不保证什么",
                "这份数据是从公开的官方校历整理来的，不是实时接口。\n"
                        + "· 学校实际放假可能跟所属地区有几天出入，以学校通知为准。\n"
                        + "· 官方临时调整（极端天气、重大活动）不会自动同步到这里。\n"
                        + "· 没收录的地区显示为空，不代表那里没有假期。"));
    }

    void doUpdate(final String url) {
        Toast.makeText(this, "正在拉取…", Toast.LENGTH_SHORT).show();
        new Thread(() -> {
            String err = null;
            try {
                HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
                conn.setConnectTimeout(10000);
                conn.setReadTimeout(20000);
                conn.setRequestProperty("User-Agent", "jiaqi-app");
                int code = conn.getResponseCode();
                if (code / 100 != 2) throw new Exception("HTTP " + code);
                InputStream in = conn.getInputStream();
                ByteArrayOutputStream bos = new ByteArrayOutputStream();
                byte[] buf = new byte[8192];
                int n;
                while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
                in.close();
                String text = new String(bos.toByteArray(), "UTF-8");

                JSONObject o = new JSONObject(text);
                if (o.optInt("schemaVersion", 0) < 1) throw new Exception("不是假期数据文件");
                if (o.optJSONArray("regions") == null) throw new Exception("缺少 regions");

                File f = new File(getFilesDir(), "holidays.json");
                FileOutputStream out = new FileOutputStream(f);
                out.write(text.getBytes("UTF-8"));
                out.close();
            } catch (Exception e) {
                err = e.getClass().getSimpleName() + ": " + e.getMessage();
            }
            final String fe = err;
            ui.post(() -> {
                if (fe == null) {
                    model = Model.load(MainActivity.this);
                    Toast.makeText(MainActivity.this, "更新好了", Toast.LENGTH_SHORT).show();
                    showData();
                } else {
                    new AlertDialog.Builder(MainActivity.this)
                            .setTitle("更新失败")
                            .setMessage(fe + "\n\n改用的还是本地数据。")
                            .setPositiveButton("知道了", null)
                            .show();
                }
            });
        }).start();
    }

    // ---------------------------------------------------------------- 组件

    interface Click {
        void run();
    }

    LinearLayout card() {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(C_CARD);
        bg.setCornerRadius(dp(14));
        c.setBackground(bg);
        c.setPadding(dp(16), dp(14), dp(16), dp(14));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = dp(10);
        c.setLayoutParams(lp);
        c.setClickable(true);
        return c;
    }

    View emptyHint(String title, String desc, String action, View.OnClickListener l) {
        LinearLayout c = card();
        c.setGravity(Gravity.CENTER);
        c.setPadding(dp(20), dp(30), dp(20), dp(30));
        TextView t = tv(title, 15, C_TEXT, true);
        t.setGravity(Gravity.CENTER);
        c.addView(t);
        TextView d = tv(desc, 12, C_SUB, false);
        d.setGravity(Gravity.CENTER);
        d.setPadding(0, dp(10), 0, 0);
        d.setLineSpacing(dp(4), 1f);
        c.addView(d);
        if (action != null) {
            TextView a = tv(action, 13, C_ACCENT, true);
            a.setGravity(Gravity.CENTER);
            a.setPadding(0, dp(16), 0, 0);
            a.setOnClickListener(l);
            c.addView(a);
        }
        return c;
    }

    View noteBox(String title, String text) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(0xFF12171E);
        bg.setCornerRadius(dp(12));
        bg.setStroke(dp(1), C_LINE);
        c.setBackground(bg);
        c.setPadding(dp(14), dp(12), dp(14), dp(12));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dp(6);
        lp.bottomMargin = dp(6);
        c.setLayoutParams(lp);
        c.addView(tv(title, 11, C_WARN, true));
        TextView t = tv(text, 11, C_SUB, false);
        t.setPadding(0, dp(6), 0, 0);
        t.setLineSpacing(dp(4), 1f);
        c.addView(t);
        return c;
    }

    View crumb(String parent, View.OnClickListener back, String current) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(2), dp(2), dp(2), dp(14));

        TextView p = tv(parent + " ›", 13, C_ACCENT, false);
        p.setOnClickListener(back);
        p.setPadding(0, dp(4), dp(6), dp(4));
        row.addView(p);

        row.addView(tv(current, 13, C_SUB, false));
        return row;
    }

    TextView pill(String text, int bgColor, int fgColor) {
        TextView t = tv(text, 13, fgColor, true);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(bgColor);
        bg.setCornerRadius(dp(20));
        t.setBackground(bg);
        t.setPadding(dp(18), dp(9), dp(18), dp(9));
        t.setGravity(Gravity.CENTER);
        return t;
    }

    void addKv(LinearLayout parent, String k, String v) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setPadding(0, dp(7), 0, 0);

        TextView kt = tv(k, 12, C_SUB, false);
        kt.setLayoutParams(new LinearLayout.LayoutParams(dp(76),
                ViewGroup.LayoutParams.WRAP_CONTENT));
        row.addView(kt);

        TextView vt = tv(v == null || v.length() == 0 ? "—" : v, 12, C_TEXT, false);
        vt.setLayoutParams(new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        row.addView(vt);
        parent.addView(row);
    }

    TextView tv(String text, float sp, int color, boolean bold) {
        TextView t = new TextView(this);
        t.setText(text == null ? "" : text);
        t.setTextSize(sp);
        t.setTextColor(color);
        if (bold) t.setTypeface(Typeface.DEFAULT_BOLD);
        return t;
    }

    int dp(float v) {
        return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v,
                getResources().getDisplayMetrics());
    }

    String shortDate(String iso) {
        if (iso == null || iso.length() < 10) return "";
        return iso.substring(0, 10);
    }

    String stageLabel(String key) {
        int i = model.stageKeys.indexOf(key);
        return i >= 0 ? model.stageLabels.get(i) : key;
    }

    String simplifyCity(String n) {
        if (n == null) return "";
        if (n.endsWith("市")) return n.substring(0, n.length() - 1);
        return n;
    }

    String gradeText(String g) {
        if ("official".equals(g)) return "官方";
        if ("aggregate".equals(g)) return "汇总";
        return "媒体";
    }
}
