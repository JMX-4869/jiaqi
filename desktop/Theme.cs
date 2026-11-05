using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace JiaQi
{
    /// 一套配色。
    public class ThemeDef
    {
        public string id = "";
        public string name = "";
        public string desc = "";
        public int bg, cardBg, cardBg2, bar, text, sub, accent, warn, line;
        public bool light = false;
    }

    /// 主题：内置几套，切换后重画整个窗口。
    static class Theme
    {
        public static string Current = "deep";

        public static List<ThemeDef> All()
        {
            var list = new List<ThemeDef>();

            list.Add(Make("deep", "深海", "现在这套：深蓝黑 + 亮蓝",
                0xFF0E1116, 0xFF161B22, 0xFF1B222B, 0xFF11161D,
                0xFFE6EDF3, 0xFF8B949E, 0xFF4CC2FF, 0xFFFFB347, 0xFF232B36));

            list.Add(Make("warm", "暖褐", "旧纸感的深褐 + 橙",
                0xFF17120E, 0xFF211A15, 0xFF2A211A, 0xFF1B1410,
                0xFFF2E9DF, 0xFFA89884, 0xFFFF9E4A, 0xFFE8C46A, 0xFF3A2E25));

            list.Add(Make("forest", "森绿", "深林绿 + 青柠",
                0xFF0B1410, 0xFF131F19, 0xFF1A2A22, 0xFF0F1A15,
                0xFFE4F0E8, 0xFF88A392, 0xFF6FD98A, 0xFFE8B85A, 0xFF24382E));

            list.Add(Make("dusk", "暮紫", "灰紫的夜里 + 淡紫",
                0xFF121016, 0xFF1B1820, 0xFF241F2C, 0xFF16131B,
                0xFFEDE8F2, 0xFF9A93A8, 0xFFB48CFF, 0xFFFFB347, 0xFF2E2838));

            list.Add(Make("day", "白昼", "浅色：白底深字（有些硬编码的颜色可能没适配到）",
                0xFFF5F6F8, 0xFFFFFFFF, 0xFFECEFF3, 0xFFE8EBF0,
                0xFF1A2028, 0xFF5C6879, 0xFF1F6FEB, 0xFFB45309, 0xFFD8DEE7));
            var day = list[list.Count - 1];
            day.light = true;

            return list;
        }

        static ThemeDef Make(string id, string name, string desc,
            uint bg, uint cardBg, uint cardBg2, uint bar,
            uint text, uint sub, uint accent, uint warn, uint line)
        {
            return new ThemeDef
            {
                id = id, name = name, desc = desc,
                bg = (int)bg, cardBg = (int)cardBg, cardBg2 = (int)cardBg2, bar = (int)bar,
                text = (int)text, sub = (int)sub, accent = (int)accent, warn = (int)warn, line = (int)line,
            };
        }

        public static ThemeDef Get(string id)
        {
            foreach (var t in All()) if (t.id == id) return t;
            return All()[0];
        }

        /// 把主题的颜色灌进 Ui 的静态字段
        public static void Apply(string id)
        {
            var t = Get(id);
            Current = t.id;
            Ui.Bg = Color.FromArgb(t.bg);
            Ui.CardBg = Color.FromArgb(t.cardBg);
            Ui.CardBg2 = Color.FromArgb(t.cardBg2);
            Ui.Bar = Color.FromArgb(t.bar);
            Ui.Text = Color.FromArgb(t.text);
            Ui.Sub = Color.FromArgb(t.sub);
            Ui.Accent = Color.FromArgb(t.accent);
            Ui.Warn = Color.FromArgb(t.warn);
            Ui.Line = Color.FromArgb(t.line);
        }

        static string PathOf()
        {
            return System.IO.Path.Combine(Application.StartupPath, "theme.json");
        }

        public static string Load()
        {
            try
            {
                var p = PathOf();
                if (!File.Exists(p)) return "deep";
                var raw = File.ReadAllText(p, Encoding.UTF8).Trim();
                if (raw.Length == 0) return "deep";
                // 兼容 JSON 包装和纯文本
                if (raw.StartsWith("{"))
                {
                    var ser = new JavaScriptSerializer();
                    var d = ser.DeserializeObject(raw) as Dictionary<string, object>;
                    if (d != null && d.ContainsKey("id")) return Convert.ToString(d["id"]);
                }
                return raw;
            }
            catch { return "deep"; }
        }

        public static void Save(string id)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                var d = new Dictionary<string, object> { { "id", id } };
                File.WriteAllText(PathOf(), ser.Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
