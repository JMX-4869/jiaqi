using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace JiaQi
{
    public class Holiday
    {
        public string type = "";
        public string label = "";
        public string start = "";
        public string end = "";
        public string note = "";
        public bool endDerived;
        public int days;
    }

    public class Source
    {
        public string title = "";
        public string url = "";
        public string published = "";
        public string grade = "media";
        public string publisher = "";
        public string docNo = "";
        public string fetchedAt = "";
    }

    public class Policy
    {
        public string scope = "";
        public string note = "";
        public List<Holiday> holidays = new List<Holiday>();
        public Source source = new Source();
    }

    public class History
    {
        public int year;
        public string primary = "";
        public string senior = "";
        public string springStart = "";
        public string note = "";
    }

    public class City
    {
        public string code = "";
        public string name = "";
        public Dictionary<string, Policy> policies = new Dictionary<string, Policy>();
        public History history;
    }

    public class SeasonalItem
    {
        public string label = "";
        public string start = "";
        public string end = "";
        public string city = "";
        public string scope = "";
        public string note = "";
    }

    public class Seasonal
    {
        public bool policy;
        public string policyText = "";
        public int year;
        public List<SeasonalItem> items = new List<SeasonalItem>();
        public Source source = new Source();
    }

    public class Region
    {
        public string code = "";
        public string name = "";
        public string abbr = "";
        public string kind = "";
        public List<City> cities = new List<City>();
        public string commonNote = "";
        public string pending = "";
        public string pendingHint = "";
        public Seasonal seasonal;
        public Source historySource;
        public Dictionary<string, Policy> commonPolicies = new Dictionary<string, Policy>();
    }

    public class Uni
    {
        public string id = "";
        public string name = "";
        public string province = "";
        public string city = "";
        public List<Holiday> holidays = new List<Holiday>();
        public Source source = new Source();
    }

    public class National
    {
        public int year;
        public List<Holiday> items = new List<Holiday>();
        public Source source = new Source();
    }

    public class MissingProv
    {
        public string code = "";
        public string name = "";
    }

    /// 数据模型：解析 holidays.json，提供「下一个假期」计算。
    public class Model
    {
        public string updatedAt = "";
        public string academicYear = "";
        public string universityNote = "";
        public List<string> stageKeys = new List<string>();
        public List<string> stageLabels = new List<string>();
        public List<Region> regions = new List<Region>();
        public List<Uni> universities = new List<Uni>();
        public National national = new National();
        public List<MissingProv> missing = new List<MissingProv>();
        public int totalProvinces;
        public List<int> weekendRest = new List<int> { 0, 6 };

        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ 载入

        public static Model Load(string json)
        {
            Model m = new Model();
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = 32 * 1024 * 1024;
            var root = ser.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) return m;

            m.updatedAt = S(root, "updatedAt");
            m.academicYear = S(root, "academicYear");
            m.universityNote = S(root, "universityNote");

            var sm = A(root, "stageMeta");
            if (sm != null)
            {
                foreach (var o in sm)
                {
                    var d = D(o);
                    if (d == null) continue;
                    m.stageKeys.Add(S(d, "key"));
                    m.stageLabels.Add(S(d, "label"));
                }
            }

            var cov = D(G(root, "coverage"));
            if (cov != null)
            {
                m.totalProvinces = I(cov, "total");
                var miss = A(cov, "missing");
                if (miss != null)
                {
                    foreach (var o in miss)
                    {
                        var d = D(o);
                        if (d == null) continue;
                        m.missing.Add(new MissingProv { code = S(d, "code"), name = S(d, "name") });
                    }
                }
            }

            var rs = A(root, "regions");
            if (rs != null)
            {
                foreach (var o in rs)
                {
                    var ro = D(o);
                    if (ro == null) continue;
                    var r = new Region();
                    r.code = S(ro, "code");
                    r.name = S(ro, "name");
                    r.abbr = S(ro, "abbr");
                    r.kind = S(ro, "kind");
                    r.pending = S(ro, "pending");
                    r.pendingHint = S(ro, "pendingHint");

                    var sea = D(G(ro, "seasonal"));
                    if (sea != null)
                    {
                        var s = new Seasonal();
                        s.policy = B(sea, "policy");
                        s.policyText = S(sea, "policyText");
                        s.year = I(sea, "year");
                        var sits = A(sea, "items");
                        if (sits != null)
                        {
                            foreach (var o2 in sits)
                            {
                                var it = D(o2);
                                if (it == null) continue;
                                s.items.Add(new SeasonalItem
                                {
                                    label = S(it, "label"),
                                    start = S(it, "start"),
                                    end = S(it, "end"),
                                    city = S(it, "city"),
                                    scope = S(it, "scope"),
                                    note = S(it, "note"),
                                });
                            }
                        }
                        s.source = ParseSource(D(G(sea, "source")));
                        r.seasonal = s;
                    }

                    var common = D(G(ro, "common"));
                    if (common != null)
                    {
                        string sp = S(common, "springStart");
                        string nn = S(common, "note");
                        if (sp.Length > 0) r.commonNote = "春季学期开学 " + Pretty(sp);
                        if (nn.Length > 0)
                            r.commonNote += (r.commonNote.Length > 0 ? "；" : "") + nn;
                    }

                    var cp = D(G(ro, "commonPolicies"));
                    if (cp != null)
                    {
                        foreach (string sk in m.stageKeys)
                        {
                            var po = D(G(cp, sk));
                            if (po != null) r.commonPolicies[sk] = ParsePolicy(po);
                        }
                    }

                    var hso = D(G(ro, "historySource"));
                    if (hso != null) r.historySource = ParseSource(hso);

                    var cs = A(ro, "cities");
                    if (cs != null)
                    {
                        foreach (var co in cs)
                        {
                            var cd = D(co);
                            if (cd == null) continue;
                            var c = new City();
                            c.code = S(cd, "code");
                            c.name = S(cd, "name");
                            var ps = D(G(cd, "policies"));
                            if (ps != null)
                            {
                                foreach (string sk in m.stageKeys)
                                {
                                    var po = D(G(ps, sk));
                                    if (po != null) c.policies[sk] = ParsePolicy(po);
                                }
                            }
                            var hd = D(G(cd, "history"));
                            if (hd != null)
                            {
                                c.history = new History
                                {
                                    year = I(hd, "year"),
                                    primary = S(hd, "primary"),
                                    senior = S(hd, "senior"),
                                    springStart = S(hd, "springStart"),
                                    note = S(hd, "note"),
                                };
                            }
                            r.cities.Add(c);
                        }
                    }                    m.regions.Add(r);
                }
            }

            var wk = D(G(root, "weekendRule"));
            if (wk != null)
            {
                var arr = A(wk, "rest");
                if (arr != null)
                {
                    m.weekendRest.Clear();
                    foreach (var o in arr)
                    {
                        try { m.weekendRest.Add(Convert.ToInt32(o)); }
                        catch { }
                    }
                }
            }

            var nh = D(G(root, "nationalHolidays"));
            if (nh != null)
            {
                m.national.year = I(nh, "year");
                var its = A(nh, "items");
                if (its != null)
                    foreach (var o in its)
                    {
                        var d = D(o);
                        if (d != null) m.national.items.Add(ParseHoliday(d));
                    }
                m.national.source = ParseSource(D(G(nh, "source")));
            }

            var us = A(root, "universities");
            if (us != null)
            {
                foreach (var o in us)
                {
                    var ud = D(o);
                    if (ud == null) continue;
                    var u = new Uni();
                    u.id = S(ud, "id");
                    u.name = S(ud, "name");
                    u.province = S(ud, "province");
                    u.city = S(ud, "city");
                    var hh = A(ud, "holidays");
                    if (hh != null)
                        foreach (var ho in hh)
                        {
                            var hd = D(ho);
                            if (hd != null) u.holidays.Add(ParseHoliday(hd));
                        }
                    u.source = ParseSource(D(G(ud, "source")));
                    m.universities.Add(u);
                }
            }
            return m;
        }

        static Policy ParsePolicy(Dictionary<string, object> po)
        {
            var p = new Policy();
            p.scope = S(po, "scope");
            p.note = S(po, "note");
            var hs = A(po, "holidays");
            if (hs != null)
                foreach (var o in hs)
                {
                    var d = D(o);
                    if (d != null) p.holidays.Add(ParseHoliday(d));
                }
            p.source = ParseSource(D(G(po, "source")));
            return p;
        }

        static Holiday ParseHoliday(Dictionary<string, object> d)
        {
            var h = new Holiday();
            h.type = S(d, "type");
            h.label = S(d, "label");
            h.start = S(d, "start");
            h.end = S(d, "end");
            h.note = S(d, "note");
            h.endDerived = B(d, "endDerived");
            h.days = I(d, "days");
            return h;
        }

        static Source ParseSource(Dictionary<string, object> d)
        {
            var s = new Source();
            if (d == null) return s;
            s.title = S(d, "title");
            s.url = S(d, "url");
            s.published = S(d, "published");
            s.grade = S(d, "grade");
            s.publisher = S(d, "publisher");
            s.docNo = S(d, "docNo");
            s.fetchedAt = S(d, "fetchedAt");
            return s;
        }

        // ------------------------------------------------------------ 工具

        static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }

        static object[] A(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v)) return v as object[];
            return null;
        }

        static object G(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v)) return v;
            return null;
        }

        static string S(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null) return Convert.ToString(v);
            return "";
        }

        static int I(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToInt32(v); }
                catch { return 0; }
            }
            return 0;
        }

        static bool B(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToBoolean(v); }
                catch { return false; }
            }
            return false;
        }

        // ------------------------------------------------------------ 日期

        public static DateTime? Parse(string ymd)
        {
            if (string.IsNullOrEmpty(ymd)) return null;
            DateTime dt;
            if (DateTime.TryParseExact(ymd, "yyyy-MM-dd", Inv, DateTimeStyles.None, out dt))
                return dt;
            return null;
        }

        public static string Pretty(string ymd)
        {
            var dt = Parse(ymd);
            if (dt == null) return ymd;
            return string.Format("{0}月{1}日", dt.Value.Month, dt.Value.Day);
        }

        public static string FullPretty(string ymd)
        {
            var dt = Parse(ymd);
            if (dt == null) return ymd;
            return string.Format("{0}年{1}月{2}日", dt.Value.Year, dt.Value.Month, dt.Value.Day);
        }

        public class Next
        {
            public Holiday h;
            public long days;
            public bool ongoing;
        }

        /// 最近的假期：正在放的优先，其次最近要来的。
        public static Next NextOf(List<Holiday> holidays)
        {
            if (holidays == null) return null;
            DateTime today = DateTime.Today;
            Next best = null;
            foreach (var h in holidays)
            {
                var s = Parse(h.start);
                if (s == null) continue;
                var e = Parse(h.end);
                bool ongoing = s.Value <= today && (e == null || e.Value >= today);
                if (ongoing)
                {
                    if (best == null || !best.ongoing)
                        best = new Next { h = h, days = (long)(s.Value - today).TotalDays, ongoing = true };
                }
                else if (s.Value > today)
                {
                    if (best == null || (!best.ongoing && s.Value < Parse(best.h.start).Value))
                        best = new Next { h = h, days = (long)(s.Value - today).TotalDays, ongoing = false };
                }
            }
            return best;
        }

        public static Next NextOf(Policy p)
        {
            return p == null ? null : NextOf(p.holidays);
        }

        public Region FindRegion(string code)
        {
            foreach (var r in regions) if (r.code == code) return r;
            return null;
        }

        public Uni FindUni(string id)
        {
            foreach (var u in universities) if (u.id == id) return u;
            return null;
        }

        public string StageLabel(string key)
        {
            int i = stageKeys.IndexOf(key);
            return i >= 0 ? stageLabels[i] : key;
        }

        public static string GradeText(string g)
        {
            if (g == "official") return "官方";
            if (g == "aggregate") return "汇总";
            return "媒体";
        }
    }
}
