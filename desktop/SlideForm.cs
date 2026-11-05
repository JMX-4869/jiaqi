using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace JiaQi
{
    /// 幻灯片/PPT 式浏览：一页一个条目，左右键翻页，按 Esc 退出。
    class SlideForm : Form
    {
        Plan plan;
        List<PlanItem> items;
        int idx;
        PictureBox pic;
        Label dateLab, titleLab, noteLab, mediaLab, pageLab;
        Panel stage;

        public SlideForm(Plan p, List<PlanItem> list)
        {
            plan = p;
            items = list;
            Text = plan.title + " · 幻灯片";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            Size = new Size(1100, 760);
            MinimumSize = new Size(640, 480);
            BackColor = Color.FromArgb(10, 13, 17);
            ForeColor = Color.FromArgb(230, 237, 243);
            KeyPreview = true;
            DoubleBuffered = true;

            stage = new Panel();
            stage.Dock = DockStyle.Fill;
            stage.BackColor = Color.FromArgb(10, 13, 17);
            Controls.Add(stage);

            // 布局：Dock=Top 是「后加的在上面」，所以按 反序 添加
            var bottom = new Panel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = Color.FromArgb(10, 13, 17);
            bottom.Padding = new Padding(80, 40, 80, 40);
            stage.Controls.Add(bottom);

            pic = new PictureBox();
            pic.Dock = DockStyle.Bottom;
            pic.Height = 420;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.BackColor = Color.FromArgb(10, 13, 17);
            bottom.Controls.Add(pic);

            mediaLab = Ui.L("", 10f, Color.FromArgb(110, 122, 138), false);
            mediaLab.Dock = DockStyle.Bottom;
            mediaLab.AutoSize = false;
            mediaLab.Height = 60;
            bottom.Controls.Add(mediaLab);

            noteLab = Ui.L("", 13f, Color.FromArgb(160, 172, 190), false);
            noteLab.Dock = DockStyle.Top;
            noteLab.AutoSize = false;
            noteLab.Height = 110;
            bottom.Controls.Add(noteLab);

            titleLab = Ui.L("", 30f, Color.FromArgb(235, 242, 250), true);
            titleLab.Dock = DockStyle.Top;
            titleLab.AutoSize = false;
            titleLab.Height = 70;
            bottom.Controls.Add(titleLab);

            dateLab = Ui.L("", 11f, Color.FromArgb(76, 194, 255), true);
            dateLab.Dock = DockStyle.Top;
            dateLab.AutoSize = false;
            dateLab.Height = 28;
            bottom.Controls.Add(dateLab);

            pageLab = Ui.L("", 10f, Color.FromArgb(90, 102, 117), false);
            pageLab.Dock = DockStyle.Bottom;
            pageLab.AutoSize = false;
            pageLab.Height = 26;
            pageLab.TextAlign = ContentAlignment.MiddleCenter;
            stage.Controls.Add(pageLab);

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down || e.KeyCode == Keys.Space) Next(1);
                else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) Next(-1);
                else if (e.KeyCode == Keys.Home) { idx = 0; Render(); }
                else if (e.KeyCode == Keys.End) { idx = items.Count - 1; Render(); }
            };
            Click += (s, e) => Next(1);
            stage.Click += (s, e) => Next(1);

            Load += (s, e) => Render();
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F11)
                {
                    if (FormBorderStyle == FormBorderStyle.None)
                    {
                        FormBorderStyle = FormBorderStyle.Sizable;
                        Bounds = new Rectangle(80, 60, 1100, 760);
                    }
                    else
                    {
                        FormBorderStyle = FormBorderStyle.None;
                        Bounds = Screen.FromControl(this).Bounds;
                    }
                }
            };
        }

        void Next(int d)
        {
            int n = idx + d;
            if (n < 0) n = 0;
            if (n >= items.Count) n = items.Count - 1;
            if (n == idx && d > 0) { Close(); return; }   // 最后一页再按右键就退出
            idx = n;
            Render();
        }

        void Render()
        {
            if (items.Count == 0) return;
            var it = items[idx];
            dateLab.Text = it.date.Length > 0
                ? Model.FullPretty(it.date) + (it.time.Length > 0 ? "  " + it.time : "") + "   " + plan.title
                : plan.title;
            titleLab.Text = it.title;
            noteLab.Text = it.note;

            string mt = "";
            foreach (var m in it.media)
            {
                string kind = m.type == "image" ? "图" : (m.type == "video" ? "视频" : "音乐");
                mt += kind + " · " + Path.GetFileName(m.path) + "\n";
            }
            mediaLab.Text = mt;

            // 图片
            Image old = pic.Image;
            pic.Image = null;
            if (old != null) old.Dispose();

            string img = null;
            foreach (var m in it.media)
                if (m.type == "image" && File.Exists(m.path)) { img = m.path; break; }
            if (img != null)
            {
                try
                {
                    using (var fs = new FileStream(img, FileMode.Open, FileAccess.Read))
                    using (var tmp = Image.FromStream(fs))
                        pic.Image = new Bitmap(tmp);
                }
                catch { }
            }
            pic.Visible = pic.Image != null;
            pageLab.Text = (idx + 1) + " / " + items.Count + "     ← → 翻页    Esc 退出";
        }
    }
}
