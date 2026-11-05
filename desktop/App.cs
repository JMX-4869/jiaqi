using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace JiaQi
{
    static class Ui
    {
        public static Color Bg = Color.FromArgb(14, 17, 22);
        public static Color CardBg = Color.FromArgb(22, 27, 34);
        public static Color CardBg2 = Color.FromArgb(27, 34, 43);
        public static Color Bar = Color.FromArgb(17, 22, 29);
        public static Color Text = Color.FromArgb(230, 237, 243);
        public static Color Sub = Color.FromArgb(139, 148, 158);
        public static Color Accent = Color.FromArgb(76, 194, 255);
        public static Color Warn = Color.FromArgb(255, 179, 71);
        public static Color Line = Color.FromArgb(35, 43, 54);

        public static Font F(float pt, bool bold)
        {
            return new Font("Microsoft YaHei UI", pt, bold ? FontStyle.Bold : FontStyle.Regular,
                GraphicsUnit.Point);
        }

        public static Label L(string text, float pt, Color color, bool bold)
        {
            var l = new Label();
            l.Text = text ?? "";
            l.Font = F(pt, bold);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = true;
            l.UseMnemonic = false;
            return l;
        }

        public static GraphicsPath RoundRect(int w, int h, int r)
        {
            var p = new GraphicsPath();
            if (r * 2 > w) r = w / 2;
            if (r * 2 > h) r = h / 2;
            int d = r * 2;
            p.AddArc(0, 0, d, d, 180, 90);
            p.AddArc(w - d - 1, 0, d, d, 270, 90);
            p.AddArc(w - d - 1, h - d - 1, d, d, 0, 90);
            p.AddArc(0, h - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Round(Control c, int r)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            // 全限定：本项目自己也有个叫 Region 的数据类
            using (var p = RoundRect(c.Width, c.Height, r))
                c.Region = new System.Drawing.Region(p);
        }
    }

    /// 往一张卡片里竖着堆内容，自动算高度。
    class CardBuilder
    {
        public Panel Card;
        int y;
        int pad;
        public int Width;

        public CardBuilder(int width, int padding)
        {
            Width = width;
            pad = padding;
            Card = new Panel();
            Card.Width = width;
            Card.BackColor = Ui.CardBg;
            Card.Margin = new Padding(0);
            y = padding;
            Card.Cursor = Cursors.Hand;
        }

        public Label Add(Label l, int indent, int rightReserve)
        {
            int maxW = Width - indent * 2 - rightReserve;
            if (maxW < 40) maxW = 40;
            l.Left = indent;
            l.Top = y;
            l.AutoSize = false;
            var pref = l.GetPreferredSize(new Size(maxW, 0));
            l.Size = pref;
            Card.Controls.Add(l);
            y += pref.Height + 3;
            return l;
        }

        /// 一行里左边一个标题、紧跟一个角标（如「寒假  还有 74 天」）。
        public void AddInline(Label title, Label tag, int indent)
        {
            title.Left = indent;
            title.Top = y;
            title.AutoSize = false;
            var tp = title.GetPreferredSize(new Size(Width - indent * 2, 0));
            title.Size = tp;
            Card.Controls.Add(title);

            if (tag != null)
            {
                tag.Left = indent + tp.Width + 9;
                tag.Top = y + 3;
                tag.AutoSize = false;
                var gp = tag.GetPreferredSize(new Size(Width - tag.Left - indent, 0));
                tag.Size = gp;
                Card.Controls.Add(tag);
            }
            y += tp.Height + 3;
        }

        /// 左边标题，右边贴边放一个东西（如箭头、大数字）。
        public void AddWithRight(Label title, Label right, int indent, int rightPad)
        {
            title.Left = indent;
            title.Top = y;
            title.AutoSize = false;
            var tp = title.GetPreferredSize(new Size(Width - indent * 2 - 80, 0));
            title.Size = tp;
            Card.Controls.Add(title);

            if (right != null)
            {
                right.AutoSize = false;
                var rp = right.GetPreferredSize(new Size(200, 0));
                right.Size = rp;
                right.Left = Width - rp.Width - rightPad;
                right.Top = y;
                Card.Controls.Add(right);
            }
            y += tp.Height + 3;
        }

        public Label AddRow(Label left, Label right, int indent)
        {
            left.Left = indent;
            left.Top = y;
            Card.Controls.Add(left);
            int width = Width - indent * 2;
            right.Left = width - right.PreferredSize.Width + indent;
            right.Top = y + 2;
            Card.Controls.Add(right);
            int h = Math.Max(left.PreferredSize.Height, right.PreferredSize.Height);
            y += h + 3;
            return left;
        }

        public int Y { get { return y; } set { y = value; } }

        public void Space(int px) { y += px; }

        public Panel Finish()
        {
            // 卡片最小高度统一，免得一行字的卡片矮一截、看着不齐
            int h = y + pad;
            if (h < 72) h = 72;
            Card.Height = h;
            Ui.Round(Card, 12);
            return Card;
        }

        /// 卡片底部加一条分隔线
        public void Divider()
        {
            var line = new Panel();
            line.BackColor = Ui.Line;
            line.Left = 0;
            line.Top = y + 6;
            line.Width = Width;
            line.Height = 1;
            Card.Controls.Add(line);
            y += 13;
        }
    }

    /// 一个最小的文本输入框（WinForms 没有内置的 InputBox）
    class InputForm : Form
    {
        public string Value = "";
        TextBox tb;

        public InputForm(string title, string label, string initial)
        {
            Text = title;
            ClientSize = new Size(440, 152);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Ui.CardBg;
            ForeColor = Ui.Text;

            var l = Ui.L(label, 9f, Ui.Sub, false);
            l.Left = 16; l.Top = 14;
            Controls.Add(l);

            tb = new TextBox();
            tb.Left = 16; tb.Top = 40; tb.Width = 408;
            tb.Text = initial ?? "";
            tb.Font = Ui.F(9.5f, false);
            tb.BackColor = Ui.CardBg2;
            tb.ForeColor = Ui.Text;
            tb.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(tb);

            var ok = new Button();
            ok.Text = "确定";
            ok.Left = 252; ok.Top = 92; ok.Width = 80; ok.Height = 32;
            ok.DialogResult = DialogResult.OK;
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Ui.Accent;
            ok.ForeColor = Color.FromArgb(11, 26, 34);
            Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "取消";
            cancel.Left = 344; cancel.Top = 92; cancel.Width = 80; cancel.Height = 32;
            cancel.DialogResult = DialogResult.Cancel;
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Color.FromArgb(42, 50, 61);
            cancel.ForeColor = Ui.Text;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            FormClosing += (s2, e) =>
            {
                if (DialogResult == DialogResult.OK) Value = tb.Text.Trim();
            };
        }
    }

    public class App : Form
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        class WheelFilter : IMessageFilter
        {
            public App app;
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x020A || app == null || app.content == null) return false;
                if (!app.content.IsHandleCreated) return false;
                Point p = app.content.PointToClient(Cursor.Position);
                if (!app.content.ClientRectangle.Contains(p)) return false;
                int delta = (short)((long)m.WParam >> 16);
                app.ScrollBy(-delta / 120 * 72);
                return true;
            }
        }

        Model M;
        Panel content;
        Panel inner;
        Label[] tabs = new Label[4];
        string[] tabNames = { "关注", "全部", "计划", "数据" };
        int page = 0;
        string dataPath;
        bool fromExternal;

        Stack<Action> nav = new Stack<Action>();

        HashSet<string> tracks = new HashSet<string>();

        List<CrawlSource> sources = new List<CrawlSource>();

        List<Plan> plans = new List<Plan>();
        PlanReminder reminder;
        string planPath = "";
        string srcPath = "";

        public App()
        {
            Text = "假期表";
            ClientSize = new Size(430, 800);
            MinimumSize = new Size(390, 600);
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            int minH = 640;
            if (ClientSize.Height < minH) ClientSize = new Size(ClientSize.Width, minH);

            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            // 先上主题，后面所有控件才用得到正确颜色
            Theme.Apply(Theme.Load());

            dataPath = Path.Combine(dir, "holidays.json");
            srcPath = Path.Combine(dir, "sources.json");
            sources = SourceStore.Load(srcPath);
            planPath = Path.Combine(dir, "plans.json");
            plans = PlanStore.Load(planPath);
            reminder = new PlanReminder(plans);
            reminder.OnRemind += (p2) => { if (page == 2) ShowPlans(); };

            if (sources.Count == 0)
            {
                // 第一次打开：把内置的省级教育主管部门清单灌进去
                foreach (var seed in LoadSeed())
                {
                    sources.Add(new CrawlSource
                    {
                        input = seed.Value,
                        origin = Crawler.NormalizeOrigin(seed.Value),
                        name = seed.Key,
                        status = "",
                        message = "AI 发现 · 待抓取",
                    });
                }
                SourceStore.Save(srcPath, sources);
            }

            LoadTracks();
            LoadData();
            BuildShell();
            ShowTracks();

            Application.AddMessageFilter(new WheelFilter { app = this });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref dark, sizeof(int)); // 暗色标题栏
            }
            catch { }
            LoadStamp();
            AutoUpdate();   // 启动就在后台查一次，用户不用点任何按钮
        }

        void ScrollBy(int dy)
        {
            if (inner == null || content == null) return;
            int minTop = Math.Min(0, content.ClientSize.Height - inner.Height);
            int t = inner.Top + dy;
            if (t > 0) t = 0;
            if (t < minTop) t = minTop;
            inner.Top = t;
        }

        // ------------------------------------------------------------ 数据

        void LoadData()
        {
            string json = null;
            fromExternal = false;
            try
            {
                if (File.Exists(dataPath))
                {
                    json = File.ReadAllText(dataPath, Encoding.UTF8);
                    fromExternal = true;
                }
            }
            catch { }

            if (json == null)
            {
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using (var s = asm.GetManifestResourceStream("holidays.json"))
                    {
                        if (s != null)
                        {
                            using (var r = new StreamReader(s, Encoding.UTF8))
                                json = r.ReadToEnd();
                        }
                    }
                }
                catch { }
            }

            if (json == null)
            {
                MessageBox.Show("数据文件缺失：\n" + dataPath, "假期表", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                M = new Model();
                return;
            }

            try { M = Model.Load(json); }
            catch (Exception e)
            {
                MessageBox.Show("数据解析失败：" + e.Message, "假期表");
                M = new Model();
            }
        }

        string TracksFile
        {
            get { return Path.Combine(Application.StartupPath, "tracks.txt"); }
        }

        void LoadTracks()
        {
            tracks.Clear();
            try
            {
                if (!File.Exists(TracksFile)) return;
                foreach (var line in File.ReadAllLines(TracksFile, Encoding.UTF8))
                {
                    string s = line.Trim();
                    if (s.Length > 0) tracks.Add(s);
                }
            }
            catch { }
        }

        void SaveTracks()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var s in tracks) sb.AppendLine(s);
                File.WriteAllText(TracksFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        // ------------------------------------------------------------ 骨架

        void BuildShell()
        {
            var head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 86;
            head.BackColor = Ui.Bg;

            var title = Ui.L("假期表", 15f, Ui.Text, true);
            title.Left = 18; title.Top = 18;
            head.Controls.Add(title);

            string sub = M.academicYear.Length > 0
                ? M.academicYear + " 学年 · 数据更新 " + (M.updatedAt.Length >= 10 ? M.updatedAt.Substring(0, 10) : "")
                : "数据未加载";
            var st = Ui.L(sub, 8.5f, Ui.Sub, false);
            st.Left = 18; st.Top = 50;
            head.Controls.Add(st);

            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Ui.Bg;
            content.AutoScroll = false;
            content.Padding = new Padding(0);

            inner = new Panel();
            inner.BackColor = Ui.Bg;
            inner.Left = 0;
            inner.Top = 0;
            content.Controls.Add(inner);

            var bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 58;
            bar.BackColor = Ui.Bar;

            for (int i = 0; i < 4; i++)
            {
                var t = Ui.L(tabNames[i], 9.5f, Ui.Sub, true);
                t.TextAlign = ContentAlignment.MiddleCenter;
                t.AutoSize = false;
                t.Width = ClientSize.Width / 4;
                t.Height = 58;
                t.Left = i * (ClientSize.Width / 4);
                t.Top = 0;
                t.Cursor = Cursors.Hand;
                int idx = i;
                t.Click += (s, e) =>
                {
                    if (idx == 0) ShowTracks();
                    else if (idx == 1) ShowAll();
                    else if (idx == 2) ShowPlans();
                    else ShowData();
                };
                t.MouseEnter += (s, e) => { if (page != idx) t.ForeColor = Color.FromArgb(200, 208, 216); };
                t.MouseLeave += (s, e) => { if (page != idx) t.ForeColor = Ui.Sub; };
                bar.Controls.Add(t);
                tabs[i] = t;
            }

            Controls.Add(content);
            Controls.Add(bar);
            Controls.Add(head);

            Resize += (s, e) => { Relayout(); };
        }

        void Relayout()
        {
            int w = ClientSize.Width / 4;
            for (int i = 0; i < 4; i++)
            {
                tabs[i].Left = i * w;
                tabs[i].Width = w;
            }
            if (page == 0) ShowTracks();
            else if (page == 1) ShowAll();
            else if (page == 2) ShowPlans();
            else ShowData();
        }

        void MarkTab(int i)
        {
            page = i;
            for (int k = 0; k < 4; k++)
                tabs[k].ForeColor = k == i ? Ui.Accent : Ui.Sub;
        }

        int CardW
        {
            get
            {
                int w = content.ClientSize.Width - 32;
                return w < 200 ? 320 : w;
            }
        }

        Panel Column()
        {
            var col = new Panel();
            col.Width = CardW;
            col.Left = 14;
            col.Top = 4;
            col.BackColor = Ui.Bg;
            col.AutoSize = false;
            return col;
        }

        /// 把一组卡片竖着放进滚动区
        void Render(List<Panel> cards)
        {
            if (inner == null) return;
            inner.SuspendLayout();
            inner.Controls.Clear();
            inner.Width = content.ClientSize.Width;
            // 间距按 4 的倍数走：左右 16、卡与卡 12、顶上留 8
            int y = 8;
            foreach (var c in cards)
            {
                c.Left = 16;
                c.Top = y;
                inner.Controls.Add(c);
                y += c.Height + 12;
            }
            inner.Height = Math.Max(y, content.ClientSize.Height);
            inner.Top = 0;
            inner.ResumeLayout();
        }

        // ------------------------------------------------------------ 待补省份

        void ShowPending(Region r)
        {
            MarkTab(1);
            var cards = new List<Panel>();
            cards.Add(Crumb("全部", r.name, (s, e) => ShowAll()));

            var b = new CardBuilder(CardW, 16);
            b.Add(Ui.L(r.name + " · 数据待补", 12f, Ui.Warn, true), 0, 0);
            b.Add(Ui.L(r.pending.Length > 0 ? r.pending : "这个省暂时还没有可核对的数据。",
                9f, Ui.Sub, false), 0, 0);
            if (r.pendingHint.Length > 0)
            {
                b.Space(8);
                b.Add(Ui.L(r.pendingHint, 8.5f, Ui.Sub, false), 0, 0);
            }
            cards.Add(b.Finish());

            var note = new CardBuilder(CardW, 14);
            note.Add(Ui.L("为什么空着", 8f, Ui.Warn, true), 0, 0);
            note.Add(Ui.L("这套数据只收能从公开渠道核对到的日期。查不到就写「待补」，"
                + "不拿别处的估算填进来——填错了比空着更糟。\n"
                + "已经贴了出处的条目，点开任何一条都能看到原文链接和核对日期。",
                8.5f, Ui.Sub, false), 0, 0);
            cards.Add(note.Finish());

            // 待补省份可能还有政策层面的东西（比如河北已经全省推行春秋假）
            if (r.seasonal != null)
            {
                var b2 = new CardBuilder(CardW, 14);
                b2.Add(Ui.L("春秋假", 12f, Ui.Text, true), 0, 0);
                if (r.seasonal.policyText.Length > 0)
                    b2.Add(Ui.L(r.seasonal.policyText, 8.5f, Ui.Sub, false), 0, 0);
                if (r.seasonal.items.Count == 0)
                    b2.Add(Ui.L("具体日期由各市公布，目前还没查到。", 8.5f, Ui.Warn, false), 0, 0);
                AddSource(b2, r.seasonal.source);
                cards.Add(b2.Finish());
            }

            Render(cards);
        }

        // ------------------------------------------------------------ 自动更新

        const string UPDATE_URL =
            "https://raw.githubusercontent.com/JMX-4869/jiaqi-data/master/holidays.json";

        string lastCheckDay = "";
        bool updating;

        string StampFile
        {
            get { return Path.Combine(Application.StartupPath, ".lastcheck"); }
        }

        void LoadStamp()
        {
            try
            {
                if (File.Exists(StampFile))
                    lastCheckDay = File.ReadAllText(StampFile).Trim();
            }
            catch { }
        }

        void SaveStamp()
        {
            try { File.WriteAllText(StampFile, lastCheckDay); }
            catch { }
        }

        /// 启动后自动跑一次：远端更新的就换上，失败就静默用本地的。
        /// 同一天只查一次，别每次开都发请求。
        void AutoUpdate()
        {
            if (updating) return;
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (lastCheckDay == today) return;
            updating = true;

            var th = new Thread(() =>
            {
                string body = null;
                try
                {
                    // 带日期参数：每天第一次请求绕过 CDN 缓存，保证拿到当天最新的
                    string url = UPDATE_URL + "?t=" + DateTime.Now.Ticks;
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "jiaqi-desktop";
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 15000;
                    req.AutomaticDecompression = DecompressionMethods.GZip;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        body = sr.ReadToEnd();
                }
                catch
                {
                    updating = false;
                    return;   // 没网 / 被墙 / 超时：什么都不做
                }

                string remoteAt = GrabUpdatedAt(body);
                bool newer = remoteAt.Length > 0
                             && string.CompareOrdinal(remoteAt, M.updatedAt) > 0
                             && body.IndexOf("\"regions\"", StringComparison.Ordinal) >= 0;

                if (newer)
                {
                    try { File.WriteAllText(dataPath, body, new UTF8Encoding(false)); }
                    catch { }
                }

                Invoke((Action)(() =>
                {
                    lastCheckDay = DateTime.Now.ToString("yyyyMMdd");
                    SaveStamp();
                    updating = false;
                    if (newer)
                    {
                        LoadData();
                        ShowCurrentPage(true);
                    }
                }));
            });
            th.IsBackground = true;
            th.Start();
        }

        static string GrabUpdatedAt(string json)
        {
            if (json == null) return "";
            int i = json.IndexOf("\"updatedAt\"", StringComparison.Ordinal);
            if (i < 0) return "";
            int q1 = json.IndexOf('"', i + 11);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        void ShowCurrentPage(bool afterUpdate)
        {
            if (page == 0) ShowTracks();
            else if (page == 1) ShowAll();
            else if (page == 2) ShowPlans();
            else ShowData();
            if (afterUpdate)
            {
                var head = this.Text;
                this.Text = "假期表 · 数据已更新到 " + (M.updatedAt.Length >= 10 ? M.updatedAt.Substring(0, 10) : "");
                var t = new System.Windows.Forms.Timer();
                t.Interval = 4000;
                t.Tick += (s, e) => { this.Text = head; t.Stop(); t.Dispose(); };
                t.Start();
            }
        }

        // ------------------------------------------------------------ 休息日

        class RestRange
        {
            public string label = "";
            public string kind = "school";
            public DateTime s;
            public DateTime e;
        }

        class RestDay
        {
            public DateTime date;
            public bool rest;
            public string reason = "";
            public string kind = "school";
        }

        List<RestRange> RestRanges(Region r, City c, string stage)
        {
            var list = new List<RestRange>();
            Policy pol;
            if (c != null && stage != null && c.policies.TryGetValue(stage, out pol))
            {
                foreach (var h in pol.holidays)
                {
                    var s = Model.Parse(h.start);
                    if (s == null) continue;
                    var e = Model.Parse(h.end);
                    list.Add(new RestRange
                    {
                        label = h.label,
                        kind = "term",
                        s = s.Value,
                        e = e.HasValue ? e.Value : s.Value
                    });
                }
            }
            if (r != null && r.seasonal != null && c != null)
            {
                string shortName = c.name.Replace("市", "");
                foreach (var it in r.seasonal.items)
                {
                    if (it.city.Length > 0 && shortName.Length > 0 && !it.city.StartsWith(shortName))
                        continue;
                    var s = Model.Parse(it.start);
                    if (s == null) continue;
                    var e = Model.Parse(it.end);
                    list.Add(new RestRange
                    {
                        label = it.label,
                        kind = "seasonal",
                        s = s.Value,
                        e = e.HasValue ? e.Value : s.Value
                    });
                }
            }
            foreach (var it in M.national.items)
            {
                var s = Model.Parse(it.start);
                if (s == null) continue;
                var e = Model.Parse(it.end);
                list.Add(new RestRange
                {
                    label = it.label,
                    kind = "national",
                    s = s.Value,
                    e = e.HasValue ? e.Value : s.Value
                });
            }
            return list;
        }

        List<RestDay> RestTimeline(Region r, City c, string stage, int n)
        {
            var ranges = RestRanges(r, c, stage);
            var outList = new List<RestDay>();
            DateTime baseDay = DateTime.Today;
            for (int i = 0; i < n; i++)
            {
                var d = baseDay.AddDays(i);
                var hit = ranges.Find(x => d >= x.s && d <= x.e);
                var day = new RestDay { date = d };
                if (hit != null)
                {
                    day.rest = true;
                    day.reason = hit.label;
                    day.kind = hit.kind;
                }
                else if (M.weekendRest.Contains((int)d.DayOfWeek))
                {
                    day.rest = true;
                    day.reason = "周末";
                    day.kind = "weekend";
                }
                outList.Add(day);
            }
            return outList;
        }

        static readonly string[] WdName = { "日", "一", "二", "三", "四", "五", "六" };

        Panel BuildStrip(Region r, City c, string stage, int n)
        {
            var tl = RestTimeline(r, c, stage, n);
            int restCount = 0;
            foreach (var d in tl) if (d.rest) restCount++;

            int boxW = (CardW - 32) / n - 2;
            if (boxW < 18) boxW = 18;

            var row = new Panel();
            row.Height = 52;
            row.BackColor = Color.Transparent;
            row.Width = (boxW + 2) * n;

            for (int i = 0; i < n; i++)
            {
                var d = tl[i];
                Color bg, fg;
                switch (d.kind)
                {
                    case "seasonal": bg = Color.FromArgb(22, 48, 63); fg = Color.FromArgb(95, 182, 217); break;
                    case "term":
                    case "national": bg = Color.FromArgb(58, 47, 18); fg = Color.FromArgb(255, 179, 71); break;
                    case "weekend": bg = Color.FromArgb(30, 39, 51); fg = Color.FromArgb(147, 160, 174); break;
                    default: bg = Color.FromArgb(16, 21, 27); fg = Color.FromArgb(62, 74, 88); break;
                }
                var cell = new Panel();
                cell.Width = boxW;
                cell.Height = 46;
                cell.Left = i * (boxW + 2);
                cell.Top = 0;
                cell.BackColor = bg;
                cell.Cursor = Cursors.Default;

                var num = Ui.L(d.date.Day.ToString(), 8.5f, fg, true);
                num.AutoSize = false;
                num.Width = boxW;
                num.Height = 20;
                num.TextAlign = ContentAlignment.MiddleCenter;
                num.Left = 0;
                num.Top = 4;
                num.BackColor = Color.Transparent;
                cell.Controls.Add(num);

                var wd = Ui.L(WdName[(int)d.date.DayOfWeek], 7f, fg, false);
                wd.AutoSize = false;
                wd.Width = boxW;
                wd.Height = 16;
                wd.TextAlign = ContentAlignment.MiddleCenter;
                wd.Left = 0;
                wd.Top = 24;
                wd.BackColor = Color.Transparent;
                cell.Controls.Add(wd);

                if (d.date == DateTime.Today)
                {
                    var mark = new Panel();
                    mark.BackColor = Ui.Accent;
                    mark.Height = 2;
                    mark.Width = boxW;
                    mark.Left = 0;
                    mark.Top = 44;
                    cell.Controls.Add(mark);
                }

                if (d.rest && d.reason.Length > 0)
                {
                    var tip = new ToolTip();
                    tip.SetToolTip(cell, d.date.ToString("MM月dd日") + " " + d.reason);
                    tip.SetToolTip(num, d.date.ToString("MM月dd日") + " " + d.reason);
                }
                row.Controls.Add(cell);
            }
            return row;
        }

        void AddStripCard(List<Panel> cards, Region r, City c, string stage)
        {
            if (c == null || stage == null) return;
            var b = new CardBuilder(CardW, 14);
            b.AddInline(Ui.L("接下来 14 天", 12f, Ui.Text, true),
                        Ui.L("休 " + CountRest(r, c, stage, 14) + " 天", 9f, Ui.Accent, true), 0);
            b.Space(6);

            var strip = BuildStrip(r, c, stage, 14);
            strip.Left = 0;
            strip.Top = b.Y;
            b.Card.Controls.Add(strip);
            b.Y = strip.Top + strip.Height + 6;

            var legend = Ui.L("橙=放假  蓝=春秋假  灰=周末  深=上课", 8f, Ui.Sub, false);
            b.Add(legend, 0, 0);
            var note = Ui.L("按「" + M.StageLabel(stage) + "」算，周末按周六日双休；"
                + "高中/寄宿制可能周六上课，以学校通知为准。", 8f, Ui.Sub, false);
            b.Add(note, 0, 0);
            cards.Add(b.Finish());
        }

        int CountRest(Region r, City c, string stage, int n)
        {
            int k = 0;
            foreach (var d in RestTimeline(r, c, stage, n)) if (d.rest) k++;
            return k;
        }

        // ------------------------------------------------------------ 组件

        void Go(Action back)
        {
            nav.Push(back);
        }

        void Back()
        {
            if (nav.Count > 0)
            {
                var a = nav.Pop();
                a();
            }
        }

        Panel Crumb(string parent, string current, EventHandler back)
        {
            var b = new CardBuilder(CardW, 10);
            var p = Ui.L(parent + " ›", 9f, Ui.Accent, false);
            p.Cursor = Cursors.Hand;
            p.Margin = new Padding(0);
            var c = Ui.L(current, 9f, Ui.Sub, false);
            c.Margin = new Padding(0);
            b.AddInline(p, c, 0);
            if (back != null) p.Click += back;
            return b.Finish();
        }

        void AddSource(CardBuilder b, Source s)
        {
            if (s == null || s.title.Length == 0) return;
            b.Divider();
            b.Add(Ui.L("来源 · " + Model.GradeText(s.grade) + "  " + s.publisher, 8f, Ui.Sub, false), 0, 0);
            b.Add(Ui.L(s.title, 8f, Ui.Sub, false), 0, 0);
            string meta = "";
            if (s.published.Length > 0) meta = "发布 " + s.published;
            if (s.docNo.Length > 0) meta += (meta.Length > 0 ? " · " : "") + s.docNo;
            if (s.fetchedAt.Length >= 10) meta += (meta.Length > 0 ? " · " : "") + "核对 " + s.fetchedAt.Substring(0, 10);
            if (meta.Length > 0) b.Add(Ui.L(meta, 8f, Ui.Sub, false), 0, 0);

            if (s.url.Length > 0)
            {
                var link = Ui.L("打开原文 ↗", 9f, Ui.Accent, true);
                link.Cursor = Cursors.Hand;
                string url = s.url;
                link.Click += (o, e) =>
                {
                    try { System.Diagnostics.Process.Start(url); }
                    catch (Exception ex) { MessageBox.Show("打不开：" + ex.Message); }
                };
                b.Add(link, 0, 0);
            }
        }

        // ------------------------------------------------------------ 关注页

        void ShowTracks()
        {
            nav.Clear();
            MarkTab(0);
            var cards = new List<Panel>();

            var uniHits = new List<Uni>();
            foreach (var u in M.universities) if (tracks.Contains("uni|" + u.id)) uniHits.Add(u);

            var list = new List<string>();
            foreach (var r in M.regions)
                foreach (var c in r.cities)
                    foreach (var sk in M.stageKeys)
                        if (c.policies.ContainsKey(sk) && tracks.Contains(r.code + "|" + c.code + "|" + sk))
                            list.Add(r.code + "|" + c.code + "|" + sk);

            if (uniHits.Count == 0 && list.Count == 0)
            {
                var b = new CardBuilder(CardW, 26);
                var h = Ui.L("还没有关注的学校或地区", 11f, Ui.Text, true);
                h.AutoSize = false;
                var hp = h.GetPreferredSize(new Size(CardW - 52, 0));
                h.Size = hp;
                h.Left = 0;
                h.Top = 0;
                b.Card.Controls.Add(h);
                b.Y = hp.Height + 12;
                b.Add(Ui.L("去「全部」里选一个城市，点学段右侧的 ＋ 就能加到这里。\n"
                    + "中小学假期跟着所属地区教育局的校历走，所以关注地区 = 关注这一片学校。\n"
                    + "大学是按学校收录的，可以在「高校校历」里单独关注。", 8.5f, Ui.Sub, false), 0, 0);
                var go = Ui.L("去挑一个 →", 9.5f, Ui.Accent, true);
                go.Cursor = Cursors.Hand;
                go.Click += (s, e) => { ShowAll(); };
                b.Add(go, 0, 0);
                cards.Add(b.Finish());
                Render(cards);
                return;
            }

            foreach (var u in uniHits) cards.Add(UniTrackCard(u));

            foreach (var key in list)
            {
                var parts = key.Split('|');
                var r = M.FindRegion(parts[0]);
                if (r == null) continue;
                City c = null;
                foreach (var x in r.cities) if (x.code == parts[1]) c = x;
                if (c == null || !c.policies.ContainsKey(parts[2])) continue;
                cards.Add(RegionTrackCard(r, c, parts[2], key));
            }

            var tip = new CardBuilder(CardW, 8);
            tip.Add(Ui.L("点卡片看详情 · 右键取消关注", 8f, Ui.Sub, false), 0, 0);
            cards.Add(tip.Finish());

            Render(cards);
        }

        Panel RegionTrackCard(Region r, City c, string sk, string key)
        {
            var p = c.policies[sk];
            var nx = Model.NextOf(p);
            var b = new CardBuilder(CardW, 14);

            var name = Ui.L(c.name.Replace("市", "") + " · " + M.StageLabel(sk), 11f, Ui.Text, true);
            b.Add(name, 0, 90);

            if (nx != null)
            {
                string when = nx.ongoing
                    ? nx.h.label + "进行中"
                    : nx.h.label + " " + Model.Pretty(nx.h.start) + " 开始";
                b.Add(Ui.L(when, 8.5f, Ui.Sub, false), 0, 90);
                if (nx.h.end.Length > 0)
                    b.Add(Ui.L(Model.Pretty(nx.h.start) + " — " + Model.Pretty(nx.h.end)
                        + (nx.h.endDerived ? "（推算）" : ""), 8.5f, Ui.Sub, false), 0, 90);

                string big = nx.ongoing ? "放" : nx.days.ToString();
                var num = Ui.L(big, 17f, Ui.Accent, true);
                num.Left = CardW - 72;
                num.Top = 12;
                b.Card.Controls.Add(num);

                var unit = Ui.L(nx.ongoing ? "假中" : "天后", 8f, Ui.Sub, false);
                unit.Left = CardW - 72;
                unit.Top = 46;
                b.Card.Controls.Add(unit);
            }
            else
            {
                b.Add(Ui.L("暂无假期数据", 8.5f, Ui.Sub, false), 0, 0);
            }

            var card = b.Finish();
            card.Click += (s, e) =>
            {
                Go(ShowTracks);
                ShowCity(r, c);
            };
            card.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var ok = MessageBox.Show("不再关注这个地区？", "假期表",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ok == DialogResult.Yes)
                    {
                        tracks.Remove(key);
                        SaveTracks();
                        ShowTracks();
                    }
                }
            };
            return card;
        }

        Panel UniTrackCard(Uni u)
        {
            var nx = Model.NextOf(u.holidays);
            var b = new CardBuilder(CardW, 14);
            b.Add(Ui.L(u.name, 11f, Ui.Text, true), 0, 90);
            b.Add(Ui.L(u.province + u.city + " · 大学", 8.5f, Ui.Sub, false), 0, 90);

            if (nx != null)
            {
                string when = nx.ongoing
                    ? nx.h.label + "进行中"
                    : nx.h.label + " " + Model.Pretty(nx.h.start) + " 开始";
                b.Add(Ui.L(when, 8.5f, Ui.Sub, false), 0, 90);
                if (nx.h.end.Length > 0)
                    b.Add(Ui.L(Model.Pretty(nx.h.start) + " — " + Model.Pretty(nx.h.end)
                        + (nx.h.endDerived ? "（推算）" : ""), 8.5f, Ui.Sub, false), 0, 90);

                var num = Ui.L(nx.ongoing ? "放" : nx.days.ToString(), 17f, Ui.Accent, true);
                num.Left = CardW - 72;
                num.Top = 12;
                b.Card.Controls.Add(num);
                var unit = Ui.L(nx.ongoing ? "假中" : "天后", 8f, Ui.Sub, false);
                unit.Left = CardW - 72;
                unit.Top = 46;
                b.Card.Controls.Add(unit);
            }
            else
            {
                b.Add(Ui.L("暂无假期数据", 8.5f, Ui.Sub, false), 0, 0);
            }

            var card = b.Finish();
            card.Click += (s, e) => { Go(ShowTracks); ShowUni(u); };
            card.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var ok = MessageBox.Show("不再关注这所学校？", "假期表",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ok == DialogResult.Yes)
                    {
                        tracks.Remove("uni|" + u.id);
                        SaveTracks();
                        ShowTracks();
                    }
                }
            };
            return card;
        }

        // ------------------------------------------------------------ 搜索

        string searchKw = "";

        class Hit
        {
            public string kind = "";
            public Region r;
            public City c;
            public Uni u;
        }

        List<Hit> SearchHits(string k)
        {
            var hits = new List<Hit>();
            if (k.Length == 0) return hits;
            foreach (var r in M.regions)
            {
                if (r.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(new Hit { kind = "region", r = r });
                foreach (var c in r.cities)
                    if (c.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                        hits.Add(new Hit { kind = "city", r = r, c = c });
            }
            foreach (var u in M.universities)
                if ((u.name + u.province + u.city).IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(new Hit { kind = "uni", u = u });
            return hits;
        }

        Panel SearchBox()
        {
            var b = new CardBuilder(CardW, 12);
            var tb = new TextBox();
            tb.Text = searchKw;
            tb.Width = CardW - 28;
            tb.Font = Ui.F(9.5f, false);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = Ui.CardBg2;
            tb.ForeColor = Ui.Text;
            tb.Left = 0;
            tb.Top = 0;
            tb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    searchKw = tb.Text.Trim();
                    ShowAll();
                }
            };
            b.Card.Controls.Add(tb);
            b.Y = tb.Height + 7;
            b.Add(Ui.L("输完按回车搜：城市 / 省份 / 高校名", 8f, Ui.Sub, false), 0, 0);
            var card = b.Finish();
            card.Cursor = Cursors.IBeam;
            return card;
        }

        // ------------------------------------------------------------ 全部页

        void ShowAll()
        {
            nav.Clear();
            MarkTab(1);
            var cards = new List<Panel>();

            cards.Add(SearchBox());

            // 有搜索词就只显示结果
            if (searchKw.Length > 0)
            {
                var hits = SearchHits(searchKw);
                if (hits.Count == 0)
                {
                    var eb = new CardBuilder(CardW, 18);
                    eb.Add(Ui.L("没搜到「" + searchKw + "」", 11f, Ui.Text, true), 0, 0);
                    eb.Add(Ui.L("试试城市名（石家庄）、省份名（山东）、或者高校名（清华）。", 8.5f, Ui.Sub, false), 0, 0);
                    cards.Add(eb.Finish());
                }
                else
                {
                    int shown = 0;
                    foreach (var h in hits)
                    {
                        if (shown >= 40) break;
                        shown++;
                        var b = new CardBuilder(CardW, 13);
                        string name, meta;
                        if (h.kind == "region")
                        {
                            name = h.r.name;
                            meta = "省份 · " + h.r.cities.Count + " 个地级行政区";
                        }
                        else if (h.kind == "city")
                        {
                            name = h.c.name;
                            string st = "";
                            foreach (var sk in M.stageKeys)
                                if (h.c.policies.ContainsKey(sk)) st += (st.Length > 0 ? " · " : "") + M.StageLabel(sk);
                            if (st.Length == 0)
                            {
                                if (h.c.history != null)
                                    st = "2026 年 " + Model.Pretty(h.c.history.primary) + " 放假";
                                else if (h.r.commonPolicies.Count > 0) st = "省级口径";
                                else st = "未收录";
                            }
                            meta = h.r.name + " · " + st;
                        }
                        else
                        {
                            name = h.u.name;
                            var nx = Model.NextOf(h.u.holidays);
                            string st = nx != null
                                ? nx.h.label + " " + Model.Pretty(nx.h.start) + " 开始"
                                : "暂无数据";
                            meta = "高校 · " + h.u.province + h.u.city + " · " + st;
                        }
                        b.Add(Ui.L(name, 11f, Ui.Text, true), 0, 28);
                        b.Add(Ui.L(meta, 8.5f, Ui.Sub, false), 0, 28);
                        var arrow = Ui.L("›", 14f, Ui.Sub, false);
                        arrow.Left = CardW - 32;
                        arrow.Top = 10;
                        b.Card.Controls.Add(arrow);
                        var card = b.Finish();
                        var hit = h;
                        card.Click += (s, e) =>
                        {
                            nav.Clear();
                            Go(ShowAll);
                            if (hit.kind == "region") ShowRegion(hit.r);
                            else if (hit.kind == "city") ShowCity(hit.r, hit.c);
                            else ShowUni(hit.u);
                        };
                        cards.Add(card);
                    }
                    if (hits.Count > 40)
                    {
                        var tb = new CardBuilder(CardW, 8);
                        tb.Add(Ui.L("还有 " + (hits.Count - 40) + " 条，输入更具体的词。", 8f, Ui.Sub, false), 0, 0);
                        cards.Add(tb.Finish());
                    }
                }
                Render(cards);
                return;
            }

            var hint = new CardBuilder(CardW, 6);
            hint.Add(Ui.L("按地区查。点开城市可以把某个学段加入关注。", 8.5f, Ui.Sub, false), 0, 0);
            cards.Add(hint.Finish());

            // 全国法定节假日
            var b1 = new CardBuilder(CardW, 14);
            b1.Add(Ui.L("全国法定节假日", 11f, Ui.Text, true), 0, 0);
            b1.Add(Ui.L(M.national.year + " 年 · " + M.national.items.Count
                + " 个节日 · 国务院办公厅", 8.5f, Ui.Sub, false), 0, 0);
            var c1 = b1.Finish();
            c1.Click += (s, e) => { Go(ShowAll); ShowNational(); };
            cards.Add(c1);

            // 高校
            var b2 = new CardBuilder(CardW, 14);
            b2.Add(Ui.L("高校校历", 11f, Ui.Text, true), 0, 0);
            b2.Add(Ui.L(M.universities.Count + " 所 · 按学校收录，可单独关注", 8.5f, Ui.Sub, false), 0, 0);
            var c2 = b2.Finish();
            c2.Click += (s, e) => { Go(ShowAll); ShowUniversities(); };
            cards.Add(c2);

            foreach (var r in M.regions)
            {
                var b = new CardBuilder(CardW, 14);
                b.Add(Ui.L(r.name, 10.5f, Ui.Text, true), 0, 30);
                string meta;
                if (r.cities.Count == 0) meta = "待补 · 官方尚未公布";
                else if (r.cities.Count == 1) meta = "全市统一";
                else meta = r.cities.Count + " 个城市";
                b.Add(Ui.L(meta, 8.5f, r.cities.Count == 0 ? Ui.Warn : Ui.Sub, false), 0, 30);
                var arrow = Ui.L("›", 14f, Ui.Sub, false);
                arrow.Left = CardW - 34;
                arrow.Top = 12;
                b.Card.Controls.Add(arrow);
                var card = b.Finish();
                var reg = r;
                card.Click += (s, e) =>
                {
                    Go(ShowAll);
                    if (reg.cities.Count == 0) ShowPending(reg);
                    else if (reg.cities.Count == 1) ShowCity(reg, reg.cities[0]);
                    else ShowRegion(reg);
                };
                cards.Add(card);
            }

            // 未收录缺口
            if (M.missing.Count > 0)
            {
                var b = new CardBuilder(CardW, 14);
                b.Add(Ui.L(string.Format("未收录 {0} / {1} 个省级地区", M.missing.Count, M.totalProvinces),
                    10.5f, Ui.Warn, true), 0, 0);
                var sb = new StringBuilder();
                foreach (var mp in M.missing) sb.Append(mp.name + "  ");
                b.Add(Ui.L(sb.ToString().Trim(), 8.5f, Ui.Sub, false), 0, 0);
                b.Add(Ui.L("不是这些地方没假期，是还没拿到能核对的官方校历。宁可不写，也不拿二手日期充数。",
                    8.5f, Ui.Sub, false), 0, 0);
                cards.Add(b.Finish());
            }

            Render(cards);
        }

        bool HasSeasonal(Region r, City c)
        {
            if (r == null || r.seasonal == null || c == null) return false;
            string shortName = c.name.Replace("市", "");
            foreach (var it in r.seasonal.items)
            {
                if (it.city.Length == 0) return true;
                if (shortName.Length > 0 && it.city.StartsWith(shortName)) return true;
            }
            return false;
        }

        void ShowRegion(Region r)
        {
            MarkTab(1);
            var cards = new List<Panel>();
            cards.Add(Crumb("全部", r.name, (s, e) => ShowAll()));

            if (r.pending.Length > 0)
            {
                var pb = new CardBuilder(CardW, 14);
                pb.Add(Ui.L("这个省还没查到正式文件", 8f, Ui.Warn, true), 0, 0);
                pb.Add(Ui.L(r.pending, 8.5f, Ui.Sub, false), 0, 0);
                if (r.pendingHint.Length > 0)
                    pb.Add(Ui.L(r.pendingHint, 8f, Ui.Sub, false), 0, 0);
                cards.Add(pb.Finish());
            }

            int withData = 0;
            foreach (var c in r.cities) if (c.policies.Count > 0) withData++;
            if (r.cities.Count > 1)
            {
                string hint = r.name + "共 " + r.cities.Count + " 个地级行政区，其中 "
                    + withData + " 个已收录数据"
                    + (r.commonPolicies.Count > 0 ? "；其余按省级口径" : "，其余显示「未收录」")
                    + "。点开还能看「接下来 14 天」和春秋假。";
                var hb = new CardBuilder(CardW, 8);
                hb.Add(Ui.L(hint, 8.5f, Ui.Sub, false), 0, 0);
                cards.Add(hb.Finish());
            }

            foreach (var c in r.cities)
            {
                var b = new CardBuilder(CardW, 14);
                b.Add(Ui.L(c.name, 10.5f, Ui.Text, true), 0, 30);
                var sb = new StringBuilder();
                foreach (var sk in M.stageKeys)
                {
                    if (!c.policies.ContainsKey(sk)) continue;
                    if (sb.Length > 0) sb.Append(" · ");
                    sb.Append(M.StageLabel(sk));
                }
                if (sb.Length == 0 && HasSeasonal(r, c)) sb.Append("春秋假");
                string meta;
                if (sb.Length > 0) meta = sb.ToString();
                else if (c.history != null)
                    meta = "未收录今年 · " + c.history.year + " 年 " + Model.Pretty(c.history.primary) + " 放假";
                else meta = "未收录";
                b.Add(Ui.L(meta, 8.5f, Ui.Sub, false), 0, 30);
                var arrow = Ui.L("›", 14f, Ui.Sub, false);
                arrow.Left = CardW - 34;
                arrow.Top = 12;
                b.Card.Controls.Add(arrow);
                var card = b.Finish();
                var city = c;
                card.Click += (s, e) => { Go(() => ShowRegion(r)); ShowCity(r, city); };
                cards.Add(card);
            }

            if (r.commonNote.Length > 0)
            {
                var b = new CardBuilder(CardW, 14);
                b.Add(Ui.L("全省说明", 8f, Ui.Warn, true), 0, 0);
                b.Add(Ui.L(r.commonNote, 8.5f, Ui.Sub, false), 0, 0);
                cards.Add(b.Finish());
            }

            Render(cards);
        }

        void ShowCity(Region r, City c)
        {
            MarkTab(1);
            var cards = new List<Panel>();
            cards.Add(Crumb(r.name, c.name,
                (s, e) => { if (r.cities.Count > 1) ShowRegion(r); else ShowAll(); }));

            // 接下来 14 天：先挑一个能代表这座城市的学段
            string rep = null;
            foreach (var sk in M.stageKeys)
                if (c.policies.ContainsKey(sk)) { rep = sk; break; }
            if (rep == null && M.stageKeys.Count > 0) rep = M.stageKeys[0];
            AddStripCard(cards, r, c, rep);

            bool useProv = c.policies.Count == 0 && r.commonPolicies.Count > 0;

            if (!useProv && c.policies.Count == 0)
            {
                if (c.history != null)
                {
                    var hb = new CardBuilder(CardW, 14);
                    hb.Add(Ui.L(c.history.year + " 年（去年）的安排", 12f, Ui.Text, true), 0, 0);
                    hb.Add(Ui.L("2026-2027 学年还没公布，这是上一学年的，供参考。", 8.5f, Ui.Sub, false), 0, 0);
                    hb.Space(6);
                    hb.AddInline(Ui.L("义务教育（小学/初中）", 10f, Ui.Text, true), null, 0);
                    hb.Add(Ui.L(Model.FullPretty(c.history.primary) + " 放假", 8.5f, Ui.Sub, false), 0, 0);
                    hb.Space(6);
                    hb.AddInline(Ui.L("普通高中", 10f, Ui.Text, true), null, 0);
                    hb.Add(Ui.L(Model.FullPretty(c.history.senior) + " 放假", 8.5f, Ui.Sub, false), 0, 0);
                    if (c.history.springStart.Length > 0)
                    {
                        hb.Space(6);
                        hb.AddInline(Ui.L("春季开学", 10f, Ui.Text, true), null, 0);
                        hb.Add(Ui.L(Model.FullPretty(c.history.springStart), 8.5f, Ui.Sub, false), 0, 0);
                    }
                    if (c.history.note.Length > 0)
                        hb.Add(Ui.L(c.history.note, 8f, Ui.Warn, false), 0, 0);
                    AddSource(hb, r.historySource);
                    cards.Add(hb.Finish());
                }
                else
                {
                    var nb = new CardBuilder(CardW, 14);
                    nb.Add(Ui.L("该市校历未收录", 8f, Ui.Warn, true), 0, 0);
                    nb.Add(Ui.L(r.name + c.name + " 的 2026-2027 学年校历还没查到。"
                        + "上面那条「接下来 14 天」只按周末和全国节假日算的，不算寒暑假。", 8.5f, Ui.Sub, false), 0, 0);
                    cards.Add(nb.Finish());
                }
            }

            foreach (var sk in M.stageKeys)
            {
                Policy p = null;
                if (useProv) r.commonPolicies.TryGetValue(sk, out p);
                else c.policies.TryGetValue(sk, out p);
                if (p == null) continue;
                var b = new CardBuilder(CardW, 14);

                string key = r.code + "|" + c.code + "|" + sk;
                bool on = tracks.Contains(key);
                var plus = Ui.L(on ? "已关注" : "＋", on ? 9f : 13f, on ? Ui.Sub : Ui.Accent, true);
                plus.Cursor = Cursors.Hand;
                plus.Click += (s, e) =>
                {
                    if (tracks.Contains(key)) tracks.Remove(key);
                    else tracks.Add(key);
                    SaveTracks();
                    ShowCity(r, c);
                };
                b.AddInline(Ui.L(M.StageLabel(sk), 12f, Ui.Text, true), plus, 0);

                if (useProv)
                    b.Add(Ui.L("省级口径 · 该市未单独发布，按全省安排", 8f, Ui.Warn, false), 0, 0);
                else if (p.scope.Length > 0)
                    b.Add(Ui.L(p.scope, 8f, Ui.Sub, false), 0, 0);

                var nx = Model.NextOf(p);
                foreach (var h in p.holidays)
                {
                    b.Space(9);
                    Label cd = null;
                    if (nx != null && nx.h == h)
                        cd = Ui.L(nx.ongoing ? "进行中" : "还有 " + nx.days + " 天", 8f, Ui.Accent, true);
                    b.AddInline(Ui.L(h.label, 10f, Ui.Text, true), cd, 0);

                    string range = Model.FullPretty(h.start);
                    if (h.end.Length > 0)
                        range += " — " + Model.FullPretty(h.end) + (h.endDerived ? "（结束日期为推算）" : "");
                    else
                        range += " 起";
                    b.Add(Ui.L(range, 8.5f, Ui.Sub, false), 0, 0);
                    if (h.note.Length > 0) b.Add(Ui.L(h.note, 8f, Ui.Warn, false), 0, 0);
                }

                AddSource(b, p.source);
                cards.Add(b.Finish());
            }

            // 春秋假
            if (r.seasonal != null && r.seasonal.items.Count > 0)
            {
                string shortName = c.name.Replace("市", "");
                var mine = new List<SeasonalItem>();
                foreach (var it in r.seasonal.items)
                {
                    if (it.city.Length > 0 && shortName.Length > 0 && !it.city.StartsWith(shortName))
                        continue;
                    mine.Add(it);
                }
                if (mine.Count > 0)
                {
                    var b = new CardBuilder(CardW, 14);
                    b.Add(Ui.L("春秋假", 12f, Ui.Text, true), 0, 0);
                    if (r.seasonal.policyText.Length > 0)
                        b.Add(Ui.L(r.seasonal.policyText, 8.5f, Ui.Sub, false), 0, 0);
                    foreach (var it in mine)
                    {
                        b.Space(9);
                        b.AddInline(Ui.L(it.label, 10f, Ui.Text, true),
                            it.city.Length > 0 && it.city != c.name
                                ? Ui.L(it.city, 8f, Ui.Sub, false) : null, 0);
                        string range = it.start == it.end
                            ? Model.FullPretty(it.start)
                            : Model.FullPretty(it.start) + " — " + Model.FullPretty(it.end);
                        if (it.scope.Length > 0) range += " · " + it.scope;
                        b.Add(Ui.L(range, 8.5f, Ui.Sub, false), 0, 0);
                        if (it.note.Length > 0) b.Add(Ui.L(it.note, 8f, Ui.Warn, false), 0, 0);
                    }
                    AddSource(b, r.seasonal.source);
                    cards.Add(b.Finish());
                }
            }

            if (!c.policies.ContainsKey("university"))
            {
                var b = new CardBuilder(CardW, 14);
                b.Add(Ui.L("大学", 12f, Ui.Text, true), 0, 0);
                b.Add(Ui.L("高校校历由各校自行制定", 8f, Ui.Sub, false), 0, 0);
                b.Add(Ui.L(M.universityNote, 8.5f, Ui.Sub, false), 0, 0);
                var go = Ui.L("去看高校校历 →", 9f, Ui.Accent, true);
                go.Cursor = Cursors.Hand;
                go.Click += (s, e) => { Go(ShowAll); ShowUniversities(); };
                b.Add(go, 0, 0);
                cards.Add(b.Finish());
            }

            Render(cards);
        }

        // ------------------------------------------------------------ 法定节假日

        void ShowNational()
        {
            MarkTab(1);
            var cards = new List<Panel>();
            cards.Add(Crumb("全部", "全国法定节假日", (s, e) => ShowAll()));

            var top = new CardBuilder(CardW, 14);
            top.Add(Ui.L(M.national.year + " 年 · 全国统一", 12f, Ui.Text, true), 0, 0);
            top.Add(Ui.L("对全国机关、学校、企业都适用。学校通常在此基础上再按校历调整。",
                8.5f, Ui.Sub, false), 0, 0);
            cards.Add(top.Finish());

            foreach (var h in M.national.items)
            {
                var b = new CardBuilder(CardW, 14);

                var s = Model.Parse(h.start);
                var e = Model.Parse(h.end);
                DateTime today = DateTime.Today;
                bool ongoing = s != null && e != null && s.Value <= today && e.Value >= today;
                Label cd = null;
                if (ongoing) cd = Ui.L("放假中", 8f, Ui.Accent, true);
                else if (s != null && s.Value > today)
                    cd = Ui.L("还有 " + (long)(s.Value - today).TotalDays + " 天", 8f, Ui.Accent, true);
                b.AddInline(Ui.L(h.label, 11f, Ui.Text, true), cd, 0);

                string range = h.start == h.end
                    ? Model.FullPretty(h.start)
                    : Model.FullPretty(h.start) + " — " + Model.FullPretty(h.end);
                if (h.days > 0) range += " · 共 " + h.days + " 天";
                b.Add(Ui.L(range, 8.5f, Ui.Sub, false), 0, 0);
                if (h.note.Length > 0) b.Add(Ui.L(h.note, 8f, Ui.Warn, false), 0, 0);
                cards.Add(b.Finish());
            }

            var src = new CardBuilder(CardW, 14);
            AddSource(src, M.national.source);
            cards.Add(src.Finish());

            Render(cards);
        }

        // ------------------------------------------------------------ 高校

        void ShowUniversities()
        {
            MarkTab(1);
            var cards = new List<Panel>();
            cards.Add(Crumb("全部", "高校校历", (s, e) => ShowAll()));

            var search = new CardBuilder(CardW, 12);
            var box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Ui.CardBg2;
            box.ForeColor = Ui.Text;
            box.Font = Ui.F(9f, false);
            box.Width = CardW - 24;
            box.Left = 0;
            box.Top = 0;
            box.Text = "";
            search.Card.Controls.Add(box);
            search.Y = box.Height + 6;
            var tip = Ui.L("输入学校名或城市过滤", 8f, Ui.Sub, false);
            search.Card.Controls.Add(tip);
            tip.Left = 0; tip.Top = box.Height + 8;
            search.Y = box.Height + 8 + tip.Height;
            var searchCard = search.Finish();
            cards.Add(searchCard);

            var listHolder = new Panel();
            listHolder.Width = CardW;
            listHolder.BackColor = Ui.Bg;
            listHolder.Height = 10;

            Action<string> fill = null;
            fill = (kw) =>
            {
                listHolder.SuspendLayout();
                listHolder.Controls.Clear();
                int y = 4;
                int shown = 0;
                foreach (var u in M.universities)
                {
                    string hay = u.name + u.province + u.city;
                    if (kw.Length > 0 && hay.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    shown++;
                    var nx = Model.NextOf(u.holidays);
                    var b = new CardBuilder(CardW, 14);
                    b.Add(Ui.L(u.name, 10.5f, Ui.Text, true), 0, 30);
                    string meta = u.province + (u.province == u.city ? "" : u.city);
                    if (nx != null)
                    {
                        meta += " · " + nx.h.label + " " + Model.Pretty(nx.h.start) + " 开始";
                        if (nx.h.end.Length > 0) meta += " · " + Model.Pretty(nx.h.end) + " 结束";
                    }
                    else meta += " · 暂无数据";
                    b.Add(Ui.L(meta, 8.5f, Ui.Sub, false), 0, 30);
                    var arrow = Ui.L("›", 14f, Ui.Sub, false);
                    arrow.Left = CardW - 34;
                    arrow.Top = 12;
                    b.Card.Controls.Add(arrow);
                    var card = b.Finish();
                    card.Left = 0;
                    card.Top = y;
                    var uu = u;
                    card.Click += (s, e) => { Go(ShowUniversities); ShowUni(uu); };
                    listHolder.Controls.Add(card);
                    y += card.Height + 10;
                }
                if (shown == 0)
                {
                    var b = new CardBuilder(CardW, 16);
                    b.Add(Ui.L("没找到", 10.5f, Ui.Text, true), 0, 0);
                    b.Add(Ui.L("目前只收录了 " + M.universities.Count
                        + " 所高校的校历，没收录的可以去学校教务处官网查。", 8.5f, Ui.Sub, false), 0, 0);
                    var card = b.Finish();
                    card.Left = 0; card.Top = 4;
                    listHolder.Controls.Add(card);
                    y = card.Height + 14;
                }
                listHolder.Height = y;
                listHolder.ResumeLayout();
            };

            box.TextChanged += (s, e) => fill(box.Text.Trim());
            fill("");
            cards.Add(listHolder);

            Render(cards);
            box.Focus();
        }

        void ShowUni(Uni u)
        {
            MarkTab(1);
            var cards = new List<Panel>();
            cards.Add(Crumb("高校校历", u.name, (s, e) => ShowUniversities()));

            var b = new CardBuilder(CardW, 14);
            string key = "uni|" + u.id;
            bool on = tracks.Contains(key);

            var plus = Ui.L(on ? "已关注" : "＋", on ? 9f : 13f, on ? Ui.Sub : Ui.Accent, true);
            plus.Cursor = Cursors.Hand;
            plus.Click += (s, e) =>
            {
                if (tracks.Contains(key)) tracks.Remove(key); else tracks.Add(key);
                SaveTracks();
                ShowUni(u);
            };
            b.AddInline(Ui.L(u.name, 12f, Ui.Text, true), plus, 0);
            b.Add(Ui.L(u.province + u.city + " · 大学", 8.5f, Ui.Sub, false), 0, 0);

            var nx = Model.NextOf(u.holidays);
            foreach (var h in u.holidays)
            {
                b.Space(9);
                Label cd = null;
                if (nx != null && nx.h == h)
                    cd = Ui.L(nx.ongoing ? "进行中" : "还有 " + nx.days + " 天", 8f, Ui.Accent, true);
                b.AddInline(Ui.L(h.label, 10f, Ui.Text, true), cd, 0);

                string range = Model.FullPretty(h.start);
                if (h.end.Length > 0)
                    range += " — " + Model.FullPretty(h.end) + (h.endDerived ? "（结束日期为推算）" : "");
                else range += " 起";
                b.Add(Ui.L(range, 8.5f, Ui.Sub, false), 0, 0);
                if (h.note.Length > 0) b.Add(Ui.L(h.note, 8f, Ui.Warn, false), 0, 0);
            }

            AddSource(b, u.source);
            cards.Add(b.Finish());

            var note = new CardBuilder(CardW, 14);
            note.Add(Ui.L("关于高校校历", 8f, Ui.Warn, true), 0, 0);
            note.Add(Ui.L(M.universityNote, 8.5f, Ui.Sub, false), 0, 0);
            cards.Add(note.Finish());

            Render(cards);
        }

        // ------------------------------------------------------------ 数据页

        // ------------------------------------------------------------ 假期计划

        void ShowPlans()
        {
            nav.Clear();
            MarkTab(2);
            var cards = new List<Panel>();

            var head = new CardBuilder(CardW, 8);
            head.Add(Ui.L("假期计划", 13f, Ui.Text, true), 0, 0);
            head.Add(Ui.L("给某个假期排一份计划：每天做什么、带什么图、放什么歌。可以翻页像 PPT 一样看。",
                8.5f, Ui.Sub, false), 0, 0);
            cards.Add(head.Finish());

            if (plans.Count == 0)
            {
                var eb = new CardBuilder(CardW, 20);
                eb.Add(Ui.L("还没有计划", 11f, Ui.Text, true), 0, 0);
                eb.Add(Ui.L("点下面的「＋ 新建计划」，挑一个假期，然后往里加条目。", 8.5f, Ui.Sub, false), 0, 0);
                cards.Add(eb.Finish());
            }

            foreach (var pl in plans)
            {
                var b = new CardBuilder(CardW, 14);
                b.Add(Ui.L(pl.title, 11.5f, Ui.Text, true), 0, 60);
                string range = (pl.start.Length > 0 ? Model.Pretty(pl.start) : "?")
                    + " — " + (pl.end.Length > 0 ? Model.Pretty(pl.end) : "?");
                string meta = range + " · " + pl.items.Count + " 个条目";
                if (pl.remind) meta += " · 每天 " + pl.remindTime + " 提醒";
                b.Add(Ui.L(meta, 8.5f, Ui.Sub, false), 0, 60);
                var arrow = Ui.L("›", 14f, Ui.Sub, false);
                arrow.Left = CardW - 32;
                arrow.Top = 10;
                b.Card.Controls.Add(arrow);
                var card = b.Finish();
                var pp = pl;
                BindClickAll(card, (s2, e2) => { Go(ShowPlans); ShowPlan(pp); });
                cards.Add(card);
            }

            var addBtn = MkBtn("＋ 新建计划", 0, 0, 140);
            addBtn.Click += (s2, e2) => NewPlan();
            var holder = new CardBuilder(CardW, 12);
            addBtn.Left = 0;
            addBtn.Top = holder.Y;
            holder.Card.Controls.Add(addBtn);
            holder.Y = addBtn.Top + addBtn.Height;
            cards.Add(holder.Finish());

            Render(cards);
        }

        void NewPlan()
        {
            using (var f = new PlanDialog("新建计划", "", "", ""))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var pl = new Plan
                {
                    id = "p" + DateTime.Now.Ticks,
                    title = f.PlanTitle,
                    start = f.StartStr,
                    end = f.EndStr,
                    created = DateTime.Now.ToString("yyyy-MM-dd"),
                };
                plans.Add(pl);
                PlanStore.Save(planPath, plans);
                ShowPlan(pl);
            }
        }

        void AIGeneratePlan(Plan pl)
        {
            DateTime st, en;
            if (!DateTime.TryParse(pl.start, out st)) { MessageBox.Show("计划开始日期不对。"); return; }
            if (!DateTime.TryParse(pl.end, out en)) { MessageBox.Show("计划结束日期不对。"); return; }
            int days = (int)(en - st).TotalDays + 1;
            if (days <= 0) { MessageBox.Show("结束日期比开始还早。"); return; }

            PlanGenForm form;
            using (form = new PlanGenForm(pl))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }

            if (pl.items.Count > 0)
            {
                if (MessageBox.Show("计划里已经有 " + pl.items.Count + " 页，生成会再加进去。继续？",
                    "生成计划", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            }

            string s1 = pl.start, s2 = pl.end;
            var pages = PlanGen.Generate(s1, s2, form);
            if (pages.Count == 0) { MessageBox.Show("没生成出东西。"); return; }
            foreach (var pg in pages) pl.items.Add(pg);
            PlanStore.Save(planPath, plans);
            ShowPlan(pl);

            string where = form.Dest.Length > 0 ? ("去 " + form.Dest + "，") : "";
            MessageBox.Show("生成了 " + pages.Count + " 页\n"
                + "假期 " + days + " 天 · 风格 " + form.Style + "\n"
                + where + (form.People.Length > 0 ? form.People + " · " : "")
                + "点「编辑页面」看效果，不满意可以删了重来。", "生成计划");
        }

        void ShowPlan(Plan pl)
        {
            MarkTab(2);
            var cards = new List<Panel>();
            cards.Add(Crumb("计划", pl.title, (s, e) => ShowPlans()));

            var b = new CardBuilder(CardW, 14);
            b.Add(Ui.L(pl.title, 12f, Ui.Text, true), 0, 0);
            string range = (pl.start.Length > 0 ? Model.FullPretty(pl.start) : "?")
                + " — " + (pl.end.Length > 0 ? Model.FullPretty(pl.end) : "?");
            b.Add(Ui.L(range, 8.5f, Ui.Sub, false), 0, 0);
            if (pl.note.Length > 0) b.Add(Ui.L(pl.note, 8.5f, Ui.Sub, false), 0, 0);
            b.Space(8);

            cards.Add(b.Finish());

            // 一键生成
            var gb = new CardBuilder(CardW, 14);
            gb.Add(Ui.L("✨  一键生成计划", 11f, Color.FromArgb(150, 220, 130), true), 0, 40);
            gb.Add(Ui.L("先问你：去哪、几个人、预算、喜欢什么、住哪、怎么去——再按这些排页", 8.5f, Ui.Sub, false), 0, 40);
            var ga = Ui.L("›", 14f, Ui.Sub, false);
            ga.Left = CardW - 32; ga.Top = 12;
            gb.Card.Controls.Add(ga);
            var gcard = gb.Finish();
            BindClickAll(gcard, (s2, e2) => AIGeneratePlan(pl));
            cards.Add(gcard);

            // 两行大按钮（整张卡片可点，别做小按钮）
            var eb1 = new CardBuilder(CardW, 14);
            eb1.Add(Ui.L("✎  编辑页面", 11f, Ui.Accent, true), 0, 40);
            eb1.Add(Ui.L("像 PPT 那样排版：加文字、图片、视频、音乐，拖到想要的位置", 8.5f, Ui.Sub, false), 0, 40);
            var a1 = Ui.L("›", 14f, Ui.Sub, false);
            a1.Left = CardW - 32; a1.Top = 12;
            eb1.Card.Controls.Add(a1);
            var card1 = eb1.Finish();
            BindClickAll(card1, (s2, e2) => ShowSlides(pl));
            cards.Add(card1);

            var eb2 = new CardBuilder(CardW, 14);
            eb2.Add(Ui.L("改计划信息", 10f, Ui.Text, true), 0, 40);
            eb2.Add(Ui.L("名字、假期区间、每天提醒时间", 8.5f, Ui.Sub, false), 0, 40);
            var a2 = Ui.L("›", 14f, Ui.Sub, false);
            a2.Left = CardW - 32; a2.Top = 12;
            eb2.Card.Controls.Add(a2);
            var card2 = eb2.Finish();
            BindClickAll(card2, (s2, e2) => EditPlan(pl));
            cards.Add(card2);

            var eb3 = new CardBuilder(CardW, 14);
            eb3.Add(Ui.L("提醒铃声", 10f, Ui.Text, true), 0, 40);
            string ringDesc;
            if (pl.soundPath.Length > 0)
            {
                ringDesc = Path.GetFileName(pl.soundPath)
                    + (pl.soundEnd > 0 ? " · 只播 " + pl.soundStart.ToString("0.##")
                        + "—" + pl.soundEnd.ToString("0.##") + " 秒" : "");
            }
            else ringDesc = "系统提示音";
            eb3.Add(Ui.L(ringDesc + " · 点这里换成本地音频、可以截一段", 8.5f, Ui.Sub, false), 0, 40);
            var a3 = Ui.L("›", 14f, Ui.Sub, false);
            a3.Left = CardW - 32; a3.Top = 12;
            eb3.Card.Controls.Add(a3);
            var card3 = eb3.Finish();
            BindClickAll(card3, (s2, e2) => OpenRingSetting(pl));
            cards.Add(card3);

            if (pl.items.Count == 0)
            {
                var eb = new CardBuilder(CardW, 18);
                eb.Add(Ui.L("计划里还没有内容", 10f, Ui.Text, true), 0, 0);
                eb.Add(Ui.L("加一条：哪天、做什么、附上图片视频音乐。", 8.5f, Ui.Sub, false), 0, 0);
                cards.Add(eb.Finish());
            }
            foreach (var it in pl.items)
            {
                var ib = new CardBuilder(CardW, 13);
                string when = "";
                if (it.date.Length > 0) when = Model.Pretty(it.date) + (it.time.Length > 0 ? " " + it.time : "");
                if (when.Length > 0) ib.Add(Ui.L(when, 8f, Ui.Accent, true), 0, 0);
                ib.Add(Ui.L(it.title, 10.5f, Ui.Text, true), 0, 0);
                if (it.note.Length > 0) ib.Add(Ui.L(it.note, 8.5f, Ui.Sub, false), 0, 0);
                if (it.media.Count > 0)
                {
                    string mt = "";
                    foreach (var mm in it.media)
                    {
                        string kind = mm.type == "image" ? "图" : (mm.type == "video" ? "视频" : "音乐");
                        if (mt.Length > 0) mt += " · ";
                        mt += kind + "(" + Path.GetFileName(mm.path) + ")";
                    }
                    ib.Add(Ui.L(mt, 8f, Color.FromArgb(120, 132, 148), false), 0, 0);
                }
                var ic = ib.Finish();
                var itt = it;
                ic.Click += (s2, e2) => EditItem(pl, itt);
                cards.Add(ic);
            }

            var addItem = MkBtn("＋ 加一条", 0, 0, 120);
            addItem.BackColor = Color.FromArgb(42, 50, 61);
            addItem.ForeColor = Ui.Text;
            addItem.Click += (s2, e2) => EditItem(pl, null);
            var hb = new CardBuilder(CardW, 12);
            addItem.Left = 0;
            addItem.Top = hb.Y;
            hb.Card.Controls.Add(addItem);
            hb.Y = addItem.Top + addItem.Height;
            cards.Add(hb.Finish());

            Render(cards);
        }


        void EditPlan(Plan pl)
        {
            using (var f = new PlanDialog("改计划", pl.title, pl.start, pl.end))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                pl.title = f.PlanTitle;
                pl.start = f.StartStr;
                pl.end = f.EndStr;

                string rt = Ask("每天提醒时间", "HH:mm（留空就不提醒）", pl.remindTime);
                if (rt != null)
                {
                    pl.remindTime = rt.Trim();
                    pl.remind = pl.remindTime.Length > 0;
                }
                PlanStore.Save(planPath, plans);
                ShowPlan(pl);
            }
        }

        void EditItem(Plan pl, PlanItem it)
        {
            bool isNew = it == null;
            if (isNew) it = new PlanItem { id = "i" + DateTime.Now.Ticks };

            string title = Ask(isNew ? "新条目" : "编辑条目", "标题（比如「去爬山」）", it.title);
            if (title == null) return;
            it.title = title;

            string date = Ask("日期", "yyyy-MM-dd，留空表示不绑定日期", it.date);
            if (date == null) return;
            it.date = date.Trim();

            string note = Ask("备注", "想写什么写什么", it.note);
            if (note != null) it.note = note;

            // 加媒体
            while (true)
            {
                var r = MessageBox.Show(
                    "这个条目现在有 " + it.media.Count + " 个媒体。\n\n要再加一个吗？选了文件会记下路径（不复制）。",
                    "媒体", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) break;

                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "选图片 / 视频 / 音乐";
                    dlg.Filter = "全部支持|*.jpg;*.jpeg;*.jpe;*.jfif;*.pjpeg;*.pjp;*.png;*.apng;*.gif;*.bmp;*.dib;*.webp;*.tif;*.tiff;*.ico;*.svg;*.svgz;*.heic;*.heif;*.avif;*.raw;*.cr2;*.nef;*.dng;*.psd;*.xcf;*.mp4;*.m4v;*.mov;*.qt;*.avi;*.mkv;*.wmv;*.flv;*.f4v;*.webm;*.mpg;*.mpeg;*.mpe;*.ts;*.m2ts;*.mts;*.3gp;*.3g2;*.rm;*.rmvb;*.asf;*.vob;*.ogv;*.divx;*.dv;*.mxf;*.mp3;*.wav;*.wave;*.flac;*.m4a;*.aac;*.ogg;*.oga;*.opus;*.wma;*.ape;*.aiff;*.aif;*.amr;*.mid;*.midi;*.ac3;*.dts;*.mka;*.ra;*.au;*.caf;*.wv;*.tta;*.spx|图片（全格式）|*.jpg;*.jpeg;*.jpe;*.jfif;*.pjpeg;*.pjp;*.png;*.apng;*.gif;*.bmp;*.dib;*.webp;*.tif;*.tiff;*.ico;*.svg;*.svgz;*.heic;*.heif;*.avif;*.raw;*.cr2;*.nef;*.dng;*.psd;*.xcf|视频（全格式）|*.mp4;*.m4v;*.mov;*.qt;*.avi;*.mkv;*.wmv;*.flv;*.f4v;*.webm;*.mpg;*.mpeg;*.mpe;*.ts;*.m2ts;*.mts;*.3gp;*.3g2;*.rm;*.rmvb;*.asf;*.vob;*.ogv;*.divx;*.dv;*.mxf|音频（全格式）|*.mp3;*.wav;*.wave;*.flac;*.m4a;*.aac;*.ogg;*.oga;*.opus;*.wma;*.ape;*.aiff;*.aif;*.amr;*.mid;*.midi;*.ac3;*.dts;*.mka;*.ra;*.au;*.caf;*.wv;*.tta;*.spx|所有文件|*.*";
                    if (dlg.ShowDialog() != DialogResult.OK) break;
                    string ext = Path.GetExtension(dlg.FileName).ToLower();
                    string type = ".mp4.mov.avi.mkv".Contains(ext) ? "video"
                        : (".mp3.wav.flac.m4a".Contains(ext) ? "audio" : "image");
                    it.media.Add(new Media { type = type, path = dlg.FileName });
                }
            }

            if (isNew) pl.items.Add(it);
            pl.items.Sort((a, b) =>
            {
                if (a.date.Length == 0) return 1;
                if (b.date.Length == 0) return -1;
                return string.CompareOrdinal(a.date, b.date);
            });
            PlanStore.Save(planPath, plans);
            ShowPlan(pl);
        }

        void OpenRingSetting(Plan pl)
        {
            using (var f = new RingSetting(pl, () => PlanStore.Save(planPath, plans)))
                f.ShowDialog(this);
            ShowPlan(pl);
        }

        void ShowSlides(Plan pl)
        {
            try
            {
                if (pl.items.Count == 0)
                {
                    if (MessageBox.Show("计划里还没有页，先建一页？", "编辑", MessageBoxButtons.YesNo) != DialogResult.Yes)
                        return;
                    pl.items.Add(new PlanItem { id = "i" + DateTime.Now.Ticks, title = "第 1 页" });
                    PlanStore.Save(planPath, plans);
                }
                using (var f = new PageEditor(pl, 0, () => PlanStore.Save(planPath, plans)))
                    f.ShowDialog(this);
                ShowPlan(pl);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开编辑出错: " + ex.GetType().Name + " / " + ex.Message, "编辑");
            }
        }

        /// 卡片里点在哪都算点这张卡（Label 会把点击吃掉）
        static void BindClickAll(Control root, EventHandler h)
        {
            root.Click += h;
            foreach (Control c in root.Controls) BindClickAll(c, h);
        }

        Button MkBtn(string text, int left, int top, int width)
        {
            var b = new Button();
            b.Text = text;
            b.AutoSize = false;
            b.Width = width;
            b.Height = 34;
            b.Left = left;
            b.Top = top;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Ui.Accent;
            b.ForeColor = Color.FromArgb(11, 26, 34);
            return b;
        }

        void ShowData()
        {
            nav.Clear();
            MarkTab(2);
            var cards = new List<Panel>();

            int cities = 0, hols = 0;
            foreach (var r in M.regions)
                foreach (var c in r.cities)
                    foreach (var p in c.policies.Values)
                    {
                        cities++;
                        hols += p.holidays.Count;
                    }

            var b1 = new CardBuilder(CardW, 14);
            b1.Add(Ui.L("数据状态", 12f, Ui.Text, true), 0, 0);
            b1.Space(4);
            Kv(b1, "学年", M.academicYear);
            Kv(b1, "更新时间", M.updatedAt.Length >= 10 ? M.updatedAt.Substring(0, 10) : M.updatedAt);
            Kv(b1, "当前来源", fromExternal ? "外部 holidays.json" : "程序内置");
            Kv(b1, "覆盖", M.regions.Count + " 个省 / " + cities + " 版政策 / " + hols + " 条假期");
            Kv(b1, "另收录", M.national.items.Count + " 个法定节假日 · " + M.universities.Count + " 所高校");
            Kv(b1, "缺口", M.missing.Count + " / " + M.totalProvinces + " 个省级地区未收录");
            cards.Add(b1.Finish());

            // 更新
            var b2 = new CardBuilder(CardW, 14);
            b2.Add(Ui.L("更新数据", 12f, Ui.Text, true), 0, 0);
            b2.Add(Ui.L("填一个 JSON 地址，拉到新数据就覆盖同目录的 holidays.json。留空不联网。",
                8.5f, Ui.Sub, false), 0, 0);

            var box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Ui.CardBg2;
            box.ForeColor = Ui.Text;
            box.Font = Ui.F(9f, false);
            box.Width = CardW - 24;
            box.Left = 0;
            box.Top = b2.Y + 6;
            b2.Card.Controls.Add(box);
            b2.Y = box.Top + box.Height + 8;

            var btns = new FlowLayoutPanel();
            btns.FlowDirection = FlowDirection.LeftToRight;
            btns.WrapContents = true;
            btns.AutoSize = true;
            btns.BackColor = Color.Transparent;
            btns.Left = 0;
            btns.Top = b2.Y;

            var go = Ui.L("检查更新", 9f, Color.FromArgb(11, 26, 34), true);
            go.AutoSize = false;
            go.Width = 92; go.Height = 32;
            go.TextAlign = ContentAlignment.MiddleCenter;
            go.BackColor = Ui.Accent;
            go.Cursor = Cursors.Hand;
            go.Margin = new Padding(0, 0, 8, 8);
            go.Click += (s, e) => DoUpdate(box.Text.Trim());
            btns.Controls.Add(go);

            var reset = Ui.L("用内置数据", 9f, Ui.Text, true);
            reset.AutoSize = false;
            reset.Width = 102; reset.Height = 32;
            reset.TextAlign = ContentAlignment.MiddleCenter;
            reset.BackColor = Color.FromArgb(42, 50, 61);
            reset.Cursor = Cursors.Hand;
            reset.Margin = new Padding(0, 0, 8, 8);
            reset.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(dataPath)) File.Delete(dataPath);
                }
                catch (Exception ex) { MessageBox.Show("删不掉：" + ex.Message); return; }
                LoadData();
                ShowData();
            };
            btns.Controls.Add(reset);

            b2.Card.Controls.Add(btns);
            b2.Y = btns.Top + btns.Height;
            cards.Add(b2.Finish());

            cards.Add(BuildThemeCard());
            foreach (var th in Theme.All()) cards.Add(BuildOneTheme(th.id));

            cards.Add(BuildSourcesCard());

            // 来源
            var b3 = new CardBuilder(CardW, 14);
            b3.Add(Ui.L("数据来源", 12f, Ui.Text, true), 0, 0);
            b3.Add(Ui.L("每条假期都标了出处。官方 = 教育厅/政府原文；媒体 = 转载官方校历；汇总 = 第三方整理，可能有误。",
                8.5f, Ui.Sub, false), 0, 0);

            var seen = new HashSet<string>();
            foreach (var r in M.regions)
                foreach (var c in r.cities)
                    foreach (var p in c.policies.Values)
                    {
                        var s = p.source;
                        if (s == null || s.url.Length == 0 || seen.Contains(s.url)) continue;
                        seen.Add(s.url);
                        b3.Space(6);
                        AddSource(b3, s);
                    }
            cards.Add(b3.Finish());

            var note = new CardBuilder(CardW, 14);
            note.Add(Ui.L("它不保证什么", 8f, Ui.Warn, true), 0, 0);
            note.Add(Ui.L("这份数据是从公开的官方校历整理来的，不是实时接口。\n"
                + "· 学校实际放假可能跟所属地区差几天，以学校通知为准。\n"
                + "· 官方临时调整（极端天气、重大活动）不会自动同步。\n"
                + "· 没收录的地区显示为空，不代表那里没有假期。", 8.5f, Ui.Sub, false), 0, 0);
            cards.Add(note.Finish());

            Render(cards);
        }

        /// 读内置的种子源（31 个省级教育主管部门），格式 [{name,url,ok}]
        List<KeyValuePair<string, string>> LoadSeed()
        {
            var outList = new List<KeyValuePair<string, string>>();
            try
            {
                string json = null;
                string sideFile = Path.Combine(Application.StartupPath, "sources_seed.json");
                if (File.Exists(sideFile)) json = File.ReadAllText(sideFile, Encoding.UTF8);
                if (json == null)
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    using (var st = asm.GetManifestResourceStream("sources_seed.json"))
                    {
                        if (st != null)
                            using (var sr = new StreamReader(st, Encoding.UTF8))
                                json = sr.ReadToEnd();
                    }
                }
                if (json != null)
                {
                    {
                        var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                        var arr = ser.DeserializeObject(json) as object[];
                        if (arr == null) return outList;
                        foreach (var o in arr)
                        {
                            var d = o as Dictionary<string, object>;
                            if (d == null) continue;
                            object name, url;
                            d.TryGetValue("name", out name);
                            d.TryGetValue("url", out url);
                            if (name == null || url == null) continue;
                            outList.Add(new KeyValuePair<string, string>(
                                Convert.ToString(name), Convert.ToString(url)));
                        }
                    }
                }
            }
            catch { }
            return outList;
        }

        Panel BuildThemeCard()
        {
            // 用一张普通卡片当标题 + 说明。真正的按钮在 ShowData 里一张张加。
            var b = new CardBuilder(CardW, 14);
            b.Add(Ui.L("外观", 12f, Ui.Text, true), 0, 0);
            b.Add(Ui.L("换一套配色，点一下立刻生效，下次打开还是这套。", 8.5f, Ui.Sub, false), 0, 0);
            return b.Finish();
        }

        Panel BuildOneTheme(string id)
        {
            var t = Theme.Get(id);
            var b = new CardBuilder(CardW, 14);
            string mark = (t.id == Theme.Current) ? "  · 正在用" : "";
            b.Add(Ui.L(t.name + mark, 11f, t.id == Theme.Current ? Ui.Accent : Ui.Text, true), 0, 60);
            b.Add(Ui.L(t.desc, 8.5f, Ui.Sub, false), 0, 60);
            // 右侧放三个色块，直接看到这套是什么样
            int bx = CardW - 78;
            int[] cols = { t.bg, t.cardBg, t.accent };
            for (int i = 0; i < 3; i++)
            {
                var sw = new Panel();
                sw.Left = bx + i * 22;
                sw.Top = 14;
                sw.Width = 18;
                sw.Height = 18;
                sw.BackColor = Color.FromArgb(cols[i]);
                b.Card.Controls.Add(sw);
            }
            var card = b.Finish();
            string tid = t.id;
            BindClickAll(card, (s2, e2) =>
            {
                Theme.Apply(tid);
                Theme.Save(tid);
                ApplyThemeLive();
            });
            return card;
        }

        /// 换完主题要重画整个窗口
        void ApplyThemeLive()
        {
            try
            {
                BackColor = Ui.Bg;
                Controls.Clear();
                BuildShell();
                Relayout();
                MarkTab(page);
                if (page == 0) ShowTracks();
                else if (page == 1) ShowAll();
                else if (page == 2) ShowPlans();
                else ShowData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("换主题时出错：" + ex.Message);
            }
        }

        Panel BuildSourcesCard()
        {
            var b = new CardBuilder(CardW, 14);
            b.Add(Ui.L("抓取网站", 12f, Ui.Text, true), 0, 0);
            b.Add(Ui.L("贴具体的文章页最有效——会直接抓那一页。只贴主站的话，会去站内翻「政务公开 / 通知公告」栏目找相关页面，不一定找得到。",
                8.5f, Ui.Sub, false), 0, 0);

            if (sources.Count == 0)
                b.Add(Ui.L("还没有添加。", 8.5f, Ui.Sub, false), 0, 0);

            foreach (var src in sources)
            {
                int rowY = b.Y + 6;

                var name = new Button();
                name.Text = (src.name.Length > 0 ? src.name : src.origin);
                name.AutoSize = false;
                name.Width = CardW - 120;
                name.Height = 28;
                name.Left = 0;
                name.Top = rowY;
                name.FlatStyle = FlatStyle.Flat;
                name.FlatAppearance.BorderSize = 0;
                name.BackColor = Color.FromArgb(27, 34, 43);
                name.ForeColor = Ui.Accent;
                name.TextAlign = ContentAlignment.MiddleLeft;
                name.Font = Ui.F(9f, true);
                var sr = src;
                name.Click += (s2, e2) =>
                {
                    string v = Ask("编辑网站", "网址（子页面会收成主站）", sr.input);
                    if (v == null) return;
                    string origin = Crawler.NormalizeOrigin(v);
                    if (origin.Length == 0) { MessageBox.Show("这个地址认不出来。"); return; }
                    sr.input = v;
                    sr.origin = origin;
                    sr.status = "";
                    sr.message = "改过了，还没抓";
                    SourceStore.Save(srcPath, sources);
                    ShowData();
                };
                b.Card.Controls.Add(name);

                var fetch = new Button();
                fetch.Text = "抓";
                fetch.AutoSize = false;
                fetch.Width = 42; fetch.Height = 28;
                fetch.Left = CardW - 110;
                fetch.Top = rowY;
                fetch.FlatStyle = FlatStyle.Flat;
                fetch.BackColor = Ui.Accent;
                fetch.ForeColor = Color.FromArgb(11, 26, 34);
                fetch.Click += (s2, e2) => RunCrawl(sr);
                b.Card.Controls.Add(fetch);

                var del = new Button();
                del.Text = "删";
                del.AutoSize = false;
                del.Width = 42; del.Height = 28;
                del.Left = CardW - 62;
                del.Top = rowY;
                del.FlatStyle = FlatStyle.Flat;
                del.BackColor = Color.FromArgb(42, 50, 61);
                del.ForeColor = Ui.Text;
                del.Click += (s2, e2) =>
                {
                    if (MessageBox.Show("删掉「" + (sr.name.Length > 0 ? sr.name : sr.origin) + "」？", "抓取网站",
                        MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    sources.Remove(sr);
                    SourceStore.Save(srcPath, sources);
                    ShowData();
                };
                b.Card.Controls.Add(del);

                b.Y = rowY + 30;

                string status = src.status == "ok" ? ("上次：" + src.message)
                    : (src.status == "fail" ? ("失败：" + src.message) : (src.message.Length > 0 ? src.message : "待抓取"));
                b.Add(Ui.L(status, 8f, src.status == "fail" ? Ui.Warn : Ui.Sub, false), 0, 0);
                b.Add(Ui.L(src.origin, 8f, Color.FromArgb(90, 102, 117), false), 0, 0);
            }

            b.Space(10);
            var add = new Button();
            add.Text = "＋ 添加网站";
            add.AutoSize = false;
            add.Width = 130; add.Height = 34;
            add.Left = 0;
            add.Top = b.Y;
            add.FlatStyle = FlatStyle.Flat;
            add.BackColor = Color.FromArgb(42, 50, 61);
            add.ForeColor = Ui.Text;
            add.Click += (s2, e2) =>
            {
                string v = Ask("添加抓取网站", "网址（可以贴具体的文章页，会自动收成主站）", "");
                if (v == null) return;
                string origin = Crawler.NormalizeOrigin(v);
                if (origin.Length == 0) { MessageBox.Show("这个地址认不出来。"); return; }
                foreach (var x in sources)
                    if (x.origin == origin) { MessageBox.Show("这个站已经加过了。"); return; }
                sources.Add(new CrawlSource { input = v, origin = origin, status = "", message = "待抓取" });
                SourceStore.Save(srcPath, sources);
                ShowData();
            };
            b.Card.Controls.Add(add);
            b.Y = add.Top + add.Height;
            return b.Finish();
        }

        static string Ask(string title, string label, string initial)
        {
            using (var f = new InputForm(title, label, initial))
            {
                if (f.ShowDialog() == DialogResult.OK) return f.Value;
                return null;
            }
        }

        readonly Dictionary<string, DateTime> lastCrawl = new Dictionary<string, DateTime>();

        void RunCrawl(CrawlSource src)
        {
            DateTime last;
            if (lastCrawl.TryGetValue(src.origin, out last)
                && (DateTime.Now - last).TotalSeconds < 60)
            {
                MessageBox.Show("同一个网站 60 秒内只抓一次，别把人家服务器打疼了。");
                return;
            }
            lastCrawl[src.origin] = DateTime.Now;
            src.message = "抓取中…";
            ShowData();
            var th = new Thread(() =>
            {
                string msg;
                List<CrawlHit> hits;
                try
                {
                    hits = Crawler.Crawl(src, out msg);
                }
                catch (Exception e)
                {
                    hits = new List<CrawlHit>();
                    msg = e.Message;
                }
                src.lastRun = DateTime.Now.ToString("MM-dd HH:mm");
                src.found = hits.Count;
                src.status = hits.Count > 0 ? "ok" : "fail";
                src.message = msg;
                SourceStore.Save(srcPath, sources);

                Invoke((Action)(() =>
                {
                    ShowData();
                    if (hits.Count == 0) return;
                    var sb = new System.Text.StringBuilder();
                    int n = 0;
                    foreach (var h in hits)
                    {
                        if (n >= 12) break;
                        n++;
                        sb.AppendLine(h.label + "  " + h.date + "   ← " + h.pageTitle);
                    }
                    if (hits.Count > 12) sb.AppendLine("…… 还有 " + (hits.Count - 12) + " 条");
                    MessageBox.Show(sb.ToString(), src.name.Length > 0 ? src.name : src.origin);
                }));
            });
            th.IsBackground = true;
            th.Start();
        }

        void Kv(CardBuilder b, string k, string v)
        {
            b.Space(4);
            int y0 = b.Y;

            var kl = Ui.L(k, 8.5f, Ui.Sub, false);
            kl.Left = 0;
            kl.Top = y0;
            kl.AutoSize = false;
            kl.Size = new Size(74, 18);
            b.Card.Controls.Add(kl);

            var vl = Ui.L(v, 8.5f, Ui.Text, false);
            vl.Left = 74;
            vl.Top = y0;
            vl.AutoSize = false;
            int maxW = CardW - 74 - 30;
            var pref = vl.GetPreferredSize(new Size(maxW, 0));
            vl.Size = pref;
            b.Card.Controls.Add(vl);

            b.Y = y0 + Math.Max(18, pref.Height) + 2;
        }

        void DoUpdate(string url)
        {
            if (url.Length == 0)
            {
                MessageBox.Show("先填地址。", "假期表");
                return;
            }
            Cursor = Cursors.WaitCursor;
            var th = new Thread(() =>
            {
                string err = null;
                string body = null;
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "jiaqi-desktop";
                    req.Timeout = 20000;
                    req.ReadWriteTimeout = 20000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        body = sr.ReadToEnd();

                    if (body.IndexOf("\"regions\"", StringComparison.Ordinal) < 0)
                        throw new Exception("不是假期数据文件");

                    File.WriteAllText(dataPath, body, new UTF8Encoding(false));
                }
                catch (Exception e)
                {
                    err = e.Message;
                }

                Invoke((Action)(() =>
                {
                    Cursor = Cursors.Default;
                    if (err == null)
                    {
                        LoadData();
                        MessageBox.Show("更新好了。", "假期表");
                        ShowData();
                    }
                    else
                    {
                        MessageBox.Show("更新失败：" + err + "\n\n用的还是原来的数据。", "假期表");
                    }
                }));
            });
            th.IsBackground = true;
            th.Start();
        }

        // ------------------------------------------------------------ 键盘

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Back(); return true; }
            if (keyData == Keys.BrowserBack) { Back(); return true; }
            if (keyData == Keys.PageDown) { ScrollBy(-420); return true; }
            if (keyData == Keys.PageUp) { ScrollBy(420); return true; }
            if (keyData == Keys.Home) { if (inner != null) inner.Top = 0; return true; }
            if (keyData == Keys.End) { ScrollBy(-200000); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new App());
        }
    }
}
