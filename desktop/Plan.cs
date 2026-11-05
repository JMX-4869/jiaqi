using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace JiaQi
{
    /// 计划里的一条内容：可以是纯文字，也可以挂图片/视频/音乐。
    public class PlanItem
    {
        public string id = "";
        public string date = "";        // yyyy-MM-dd，可以留空（不绑定日期）
        public string time = "";        // HH:mm，留空表示当天全天
        public string title = "";
        public string note = "";
        public List<Media> media = new List<Media>();
        public List<PageElement> elements = new List<PageElement>();
    }

    /// 页面上的一个元素（像 PPT 里的一个文本框 / 图片 / 视频 / 音乐）
    public class PageElement
    {
        public string id = "";
        public string type = "";      // text / image / video / audio
        public int x, y, w, h;
        public string text = "";      // type=text 时用
        public string path = "";      // 媒体路径
        public string fontSize = "18";
        public string color = "";
    }

    public class Media
    {
        public string type = "";        // image / video / audio
        public string path = "";        // 绝对路径（本机）
        public string caption = "";
    }

    /// 一份假期计划。绑定一个假期区间，里面是一堆条目。
    public class Plan
    {
        public string id = "";
        public string title = "";
        public string start = "";       // 假期开始
        public string end = "";         // 假期结束
        public string note = "";
        public bool remind = true;      // 要不要提醒
        public string remindTime = "09:00";
        public bool sound = true;
        public bool vibrate = true;
        public string soundPath = "";     // 自定义铃声（空 = 系统提示音）
        public double soundStart = 0;     // 从第几秒开始
        public double soundEnd = 0;       // 到第几秒结束（0 = 整段）
        public List<PlanItem> items = new List<PlanItem>();
        public string created = "";
    }

    public static class PlanStore
    {
        public static List<Plan> Load(string path)
        {
            var list = new List<Plan>();
            try
            {
                if (!File.Exists(path)) return list;
                var ser = new JavaScriptSerializer();
                var raw = ser.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as object[];
                if (raw == null) return list;
                foreach (var o in raw)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    var p = new Plan
                    {
                        id = S(d, "id"),
                        title = S(d, "title"),
                        start = S(d, "start"),
                        end = S(d, "end"),
                        note = S(d, "note"),
                        remind = B(d, "remind", true),
                        remindTime = S(d, "remindTime"),
                        sound = B(d, "sound", true),
                        vibrate = B(d, "vibrate", true),
                        soundPath = S(d, "soundPath"),
                        soundStart = D(d, "soundStart"),
                        soundEnd = D(d, "soundEnd"),
                        created = S(d, "created"),
                    };
                    if (p.remindTime.Length == 0) p.remindTime = "09:00";
                    var its = d.ContainsKey("items") ? d["items"] as object[] : null;
                    if (its != null)
                    {
                        foreach (var io in its)
                        {
                            var id = io as Dictionary<string, object>;
                            if (id == null) continue;
                            var it = new PlanItem
                            {
                                id = S(id, "id"),
                                date = S(id, "date"),
                                time = S(id, "time"),
                                title = S(id, "title"),
                                note = S(id, "note"),
                            };
                            var ms = id.ContainsKey("media") ? id["media"] as object[] : null;
                            if (ms != null)
                            {
                                foreach (var mo in ms)
                                {
                                    var md = mo as Dictionary<string, object>;
                                    if (md == null) continue;
                                    it.media.Add(new Media
                                    {
                                        type = S(md, "type"),
                                        path = S(md, "path"),
                                        caption = S(md, "caption"),
                                    });
                                }
                            }
                            var els = id.ContainsKey("elements") ? id["elements"] as object[] : null;
                            if (els != null)
                            {
                                foreach (var eo in els)
                                {
                                    var ed = eo as Dictionary<string, object>;
                                    if (ed == null) continue;
                                    it.elements.Add(new PageElement
                                    {
                                        id = S(ed, "id"), type = S(ed, "type"),
                                        x = I(ed, "x"), y = I(ed, "y"),
                                        w = I(ed, "w"), h = I(ed, "h"),
                                        text = S(ed, "text"), path = S(ed, "path"),
                                        fontSize = S(ed, "fontSize"), color = S(ed, "color"),
                                    });
                                }
                            }
                            p.items.Add(it);
                        }
                    }
                    list.Add(p);
                }
            }
            catch { }
            return list;
        }

        public static void Save(string path, List<Plan> list)
        {
            try
            {
                var arr = new List<object>();
                foreach (var p in list)
                {
                    var items = new List<object>();
                    foreach (var it in p.items)
                    {
                        var els = new List<object>();
                        foreach (var e in it.elements)
                            els.Add(new Dictionary<string, object>
                            {
                                {"id", e.id}, {"type", e.type}, {"x", e.x}, {"y", e.y},
                                {"w", e.w}, {"h", e.h}, {"text", e.text}, {"path", e.path},
                                {"fontSize", e.fontSize}, {"color", e.color}
                            });
                        var ms = new List<object>();
                        foreach (var m in it.media)
                            ms.Add(new Dictionary<string, object>
                            {
                                {"type", m.type}, {"path", m.path}, {"caption", m.caption}
                            });
                        items.Add(new Dictionary<string, object>
                        {
                            {"id", it.id}, {"date", it.date}, {"time", it.time},
                            {"title", it.title}, {"note", it.note}, {"media", ms},
                            {"elements", els}
                        });
                    }
                    arr.Add(new Dictionary<string, object>
                    {
                        {"id", p.id}, {"title", p.title}, {"start", p.start}, {"end", p.end},
                        {"note", p.note}, {"remind", p.remind}, {"remindTime", p.remindTime},
                        {"sound", p.sound}, {"vibrate", p.vibrate},
                        {"soundPath", p.soundPath}, {"soundStart", p.soundStart}, {"soundEnd", p.soundEnd},
                        {"created", p.created}, {"items", items}
                    });
                }
                var ser = new JavaScriptSerializer();
                File.WriteAllText(path, ser.Serialize(arr), new UTF8Encoding(false));
            }
            catch { }
        }

        static string S(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
        }

        static int I(Dictionary<string, object> d, string k)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToInt32(v); } catch { return 0; } }
            return 0;
        }

        static double D(Dictionary<string, object> d, string k)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToDouble(v); } catch { return 0; } }
            return 0;
        }

        static bool B(Dictionary<string, object> d, string k, bool def)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToBoolean(v); } catch { return def; }
            }
            return def;
        }
    }
}
