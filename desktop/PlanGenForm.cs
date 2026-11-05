using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace JiaQi
{
    /// 生成计划前先问清楚。字段按类型切换：
    /// 旅行 / 学习 / 休息 / 混合 各问各的，缺的那点我自己补。
    class PlanGenForm : Form
    {
        public string Style = "混合";
        public bool Ok = false;

        // 旅行
        public string Dest = "", People = "", Budget = "", Hobby = "";
        public string Stay = "", Transport = "", Food = "", MustGo = "", BackPlan = "";
        // 学习
        public string Subject = "", DailyHours = "", Goal = "", Weak = "", Where = "";
        // 休息
        public string Sleep = "", WantDo = "", DontDo = "", MustFinish = "";
        // 通用
        public string RestTime = "", Extra = "";
        // 自定义
        public string CustomText = "";

        Panel bodyPanel;
        Panel pTrip, pStudy, pRest, pCustom;
        FlowLayoutPanel chips;
        Dictionary<string, TextBox> boxes = new Dictionary<string, TextBox>();
        Dictionary<string, string> hints = new Dictionary<string, string>();

        public PlanGenForm(Plan plan)
        {
            Text = "生成计划 · " + plan.title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(580, 850);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(16, 20, 26);
            ForeColor = Color.FromArgb(230, 237, 243);
            AutoScroll = false;

            var head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 96;
            head.BackColor = Color.FromArgb(16, 20, 26);
            Controls.Add(head);

            var l0 = Ui.L("先填一下，生成的内容按这些排。空的项就跳过。", 8.5f,
                Color.FromArgb(139, 148, 158), false);
            l0.Left = 16; l0.Top = 10; l0.AutoSize = true;
            head.Controls.Add(l0);

            var l1 = Ui.L("这次想干什么是重点", 9f, Color.FromArgb(200, 210, 220), false);
            l1.Left = 16; l1.Top = 34; l1.AutoSize = true;
            head.Controls.Add(l1);

            chips = new FlowLayoutPanel();
            chips.FlowDirection = FlowDirection.LeftToRight;
            chips.AutoSize = true;
            chips.Left = 16;
            chips.Top = 56;
            chips.BackColor = Color.Transparent;
            string[] styles = { "旅行", "学习", "休息", "混合", "自定义" };
            foreach (var st in styles)
            {
                var b = new Button();
                b.Text = st;
                b.AutoSize = false;
                b.Width = 76;
                b.Height = 30;
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = Color.FromArgb(32, 40, 51);
                b.ForeColor = Color.FromArgb(220, 228, 238);
                b.Margin = new Padding(0, 0, 8, 0);
                string captured = st;
                b.Click += (s, e) => { Style = captured; RefreshChips(); };
                chips.Controls.Add(b);
            }
            head.Controls.Add(chips);

            // 内容区（可滚动）
            bodyPanel = new Panel();
            bodyPanel.Dock = DockStyle.Fill;
            bodyPanel.AutoScroll = true;
            bodyPanel.BackColor = Color.FromArgb(16, 20, 26);
            Controls.Add(bodyPanel);

            // 三个类型的字段组
            pTrip = new Panel();
            pTrip.Width = 540;
            pTrip.BackColor = Color.Transparent;
            pTrip.Left = 0; pTrip.Top = 0;
            bodyPanel.Controls.Add(pTrip);
            AddField(pTrip, "dest", "去哪（旅行必填）", "比如：成都、西安、大理");
            AddField(pTrip, "people", "几个人", "比如：2 人 / 一家三口，带不带老人小孩");
            AddField(pTrip, "budget", "预算", "比如：3000 元以内，含不含路费");
            AddField(pTrip, "hobby", "兴趣爱好", "比如：吃、拍照、博物馆、爬山、逛市场");
            AddField(pTrip, "stay", "住宿要求", "比如：干净就行 / 想住有院子的民宿 / 要能停车");
            AddField(pTrip, "transport", "出行方式", "比如：高铁 / 自驾 / 飞机，回来几号");
            AddField(pTrip, "food", "吃喝讲究", "比如：不吃辣 / 一定想吃一顿火锅");
            AddField(pTrip, "mustgo", "非去不可的地方", "比如：熊猫基地、某个博物馆");
            AddField(pTrip, "backplan", "返程安排", "比如：最后一天下午到家，别太赶");

            pStudy = new Panel();
            pStudy.Width = 540;
            pStudy.BackColor = Color.Transparent;
            pStudy.Left = 0; pStudy.Top = 0;
            pStudy.Visible = false;
            bodyPanel.Controls.Add(pStudy);
            AddField(pStudy, "subject", "主要学什么", "比如：数学和英语 / 物理 / 四六级");
            AddField(pStudy, "hours", "每天大概学多久", "比如：上午 3 小时，下午不学");
            AddField(pStudy, "goal", "目标", "比如：补差 / 预习下学期 / 赶完作业");
            AddField(pStudy, "weak", "薄弱在哪", "比如：几何、完形填空、作文");
            AddField(pStudy, "where", "在哪学", "比如：家里 / 图书馆 / 自习室");
            AddField(pStudy, "hobby", "学习之外想做什么", "比如：打球、看书、打游戏");

            pRest = new Panel();
            pRest.Width = 540;
            pRest.BackColor = Color.Transparent;
            pRest.Left = 0; pRest.Top = 0;
            pRest.Visible = false;
            bodyPanel.Controls.Add(pRest);
            AddField(pRest, "sleep", "作息", "比如：几点睡几点起，想不想调过来");
            AddField(pRest, "wantdo", "想做的事", "比如：看完那本书、学做饭、见朋友");
            AddField(pRest, "dontdo", "不想做的事", "比如：不想出门、不想碰作业");
            AddField(pRest, "mustfinish", "必须完成的事", "比如：有个作业要交、要走亲戚");
            AddField(pRest, "hobby", "平时喜欢什么", "比如：看剧、画画、听歌");

            pCustom = new Panel();
            pCustom.Width = 540;
            pCustom.BackColor = Color.Transparent;
            pCustom.Left = 0; pCustom.Top = 0;
            pCustom.Visible = false;
            bodyPanel.Controls.Add(pCustom);

            var cl = Ui.L("自己写（想怎么安排直接写，一行就是一段，生成时按行分页）", 8.5f,
                Color.FromArgb(170, 184, 200), false);
            cl.Left = 0; cl.Top = 0; cl.Width = 520; cl.Height = 18;
            pCustom.Controls.Add(cl);

            var customBox = new TextBox();
            customBox.Left = 0;
            customBox.Top = 24;
            customBox.Width = 520;
            customBox.Height = 260;
            customBox.Multiline = true;
            customBox.ScrollBars = ScrollBars.Vertical;
            customBox.BackColor = Color.FromArgb(28, 34, 43);
            customBox.ForeColor = Color.FromArgb(100, 112, 128);
            customBox.BorderStyle = BorderStyle.FixedSingle;
            customBox.Font = Ui.F(9.5f, false);
            customBox.Text = string.Join(Environment.NewLine, new string[] {
                "比如：",
                "第一天：坐高铁去成都，下午逛宽窄巷子",
                "第二天：熊猫基地，晚上吃火锅",
                "第三天：都江堰，晚上回市区",
                "第四天：回家，路上补作业"
            });
            customBox.GotFocus += (s2, e2) =>
            {
                if (customBox.Text.StartsWith("比如：")) { customBox.Text = ""; customBox.ForeColor = Color.FromArgb(230, 237, 243); }
            };
            customBox.LostFocus += (s2, e2) =>
            {
                if (customBox.Text.Trim().Length == 0)
                {
                    customBox.Text = string.Join(Environment.NewLine, new string[] {
                        "比如：",
                        "第一天：坐高铁去成都，下午逛宽窄巷子",
                        "第二天：熊猫基地，晚上吃火锅"
                    });
                    customBox.ForeColor = Color.FromArgb(100, 112, 128);
                }
            };
            pCustom.Controls.Add(customBox);
            boxes["custom"] = customBox;
            hints["custom"] = "";

            // 通用两项（放最后，所有类型都看到）
            var pCommon = new Panel();
            pCommon.Width = 540;
            pCommon.BackColor = Color.Transparent;
            pCommon.Left = 0; pCommon.Top = 0;
            bodyPanel.Controls.Add(pCommon);
            AddField(pCommon, "rest", "每天几点休息", "比如：晚上 11 点前睡，别熬夜");
            AddField(pCommon, "extra", "还想补充什么", "随便写");

            var foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 56;
            foot.BackColor = Color.FromArgb(16, 20, 26);
            Controls.Add(foot);

            var ok = new Button();
            ok.Text = "生成";
            ok.Left = 350;
            ok.Top = 10;
            ok.Width = 92;
            ok.Height = 36;
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Color.FromArgb(76, 194, 255);
            ok.ForeColor = Color.FromArgb(11, 26, 34);
            ok.Click += (s, e) => { Collect(); Ok = true; Close(); };
            foot.Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "取消";
            cancel.Left = 452;
            cancel.Top = 10;
            cancel.Width = 92;
            cancel.Height = 36;
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Color.FromArgb(42, 50, 61);
            cancel.ForeColor = Color.FromArgb(230, 237, 243);
            cancel.Click += (s, e) => Close();
            foot.Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            Load += (s, e) =>
            {
                // Dock 顺序：把体块提到最前，别让头部和底部盖住它
                bodyPanel.BringToFront();
                RefreshChips();
                LayoutGroups();
            };
        }

        void RefreshChips()
        {
            foreach (Control c in chips.Controls)
            {
                var b = c as Button;
                if (b == null) continue;
                bool on = b.Text == Style;
                b.BackColor = on ? Color.FromArgb(76, 194, 255) : Color.FromArgb(32, 40, 51);
                b.ForeColor = on ? Color.FromArgb(11, 26, 34) : Color.FromArgb(220, 228, 238);
            }
            LayoutGroups();
        }

        void LayoutGroups()
        {
            bool custom = Style == "自定义";
            bool trip = (Style == "旅行" || Style == "混合") && !custom;
            bool study = (Style == "学习" || Style == "混合") && !custom;
            bool rest = (Style == "休息" || Style == "混合") && !custom;
            pTrip.Visible = trip;
            pStudy.Visible = study;
            pRest.Visible = rest;
            if (pCustom != null) pCustom.Visible = custom;

            // 混合全显示时字段太多，用滚动；其它类型靠上排
            int y = 4;
            foreach (var p in new Panel[] { pTrip, pStudy, pRest, pCustom })
            {
                if (!p.Visible) continue;
                p.Top = y;
                p.Height = 0;
                foreach (Control c in p.Controls)
                {
                    int bottom = c.Top + c.Height;
                    if (bottom > p.Height) p.Height = bottom;
                }
                y += p.Height + 6;
            }

            // 通用组跟到最后
            foreach (Control c in bodyPanel.Controls)
            {
                var p = c as Panel;
                if (p == null || p == pTrip || p == pStudy || p == pRest || p == pCustom) continue;
                p.Top = y;
                int h = 0;
                foreach (Control cc in p.Controls)
                {
                    int bottom = cc.Top + cc.Height;
                    if (bottom > h) h = bottom;
                }
                p.Height = h;
                y += h + 6;
            }
        }

        void AddField(Panel host, string key, string label, string hint)
        {
            int y = 0;
            foreach (Control c in host.Controls)
            {
                int b = c.Top + c.Height;
                if (b > y) y = b;
            }

            var l = Ui.L(label, 8.5f, Color.FromArgb(170, 184, 200), false);
            l.Left = 0;
            l.Top = y + 4;
            l.Width = 520;
            l.Height = 18;
            host.Controls.Add(l);

            var tb = new TextBox();
            tb.Left = 0;
            tb.Top = y + 24;
            tb.Width = 520;
            tb.Height = 26;
            tb.BackColor = Color.FromArgb(28, 34, 43);
            tb.ForeColor = Color.FromArgb(100, 112, 128);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = Ui.F(9.5f, false);
            tb.Text = hint;
            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == hint) { tb.Text = ""; tb.ForeColor = Color.FromArgb(230, 237, 243); }
            };
            tb.LostFocus += (s, e) =>
            {
                if (tb.Text.Trim().Length == 0) { tb.Text = hint; tb.ForeColor = Color.FromArgb(100, 112, 128); }
            };
            host.Controls.Add(tb);

            boxes[key] = tb;
            hints[key] = hint;
        }

        string Val(string key)
        {
            TextBox tb;
            if (!boxes.TryGetValue(key, out tb)) return "";
            var t = tb.Text.Trim();
            var h = hints.ContainsKey(key) ? hints[key] : "";
            return t == h ? "" : t;
        }

        void Collect()
        {
            Dest = Val("dest");
            People = Val("people");
            Budget = Val("budget");
            Hobby = Val("hobby");
            Stay = Val("stay");
            Transport = Val("transport");
            Food = Val("food");
            MustGo = Val("mustgo");
            BackPlan = Val("backplan");

            Subject = Val("subject");
            DailyHours = Val("hours");
            Goal = Val("goal");
            Weak = Val("weak");
            Where = Val("where");

            Sleep = Val("sleep");
            WantDo = Val("wantdo");
            DontDo = Val("dontdo");
            MustFinish = Val("mustfinish");

            RestTime = Val("rest");
            Extra = Val("extra");

            if (boxes.ContainsKey("custom"))
            {
                var ct = boxes["custom"].Text.Trim();
                CustomText = ct.StartsWith("比如：") ? "" : ct;
            }
        }
    }
}
