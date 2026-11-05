using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace JiaQi
{
    /// 像 PPT 那样的编辑画布：一页 16:9，往上摆文字块、图片、视频、音乐。
    /// 不是全屏演示，就是个编辑器。
    class PageEditor : Form
    {
        Plan plan;
        int pageIdx;
        Panel canvas;
        Panel thumbRail;
        Label pageLab;
        PageElement selected;
        Action onSave;

        const int CW = 880;
        const int CH = 495;

        public PageEditor(Plan p, int startIdx, Action saveCallback)
        {
            plan = p;
            pageIdx = Math.Max(0, Math.Min(startIdx, Math.Max(0, plan.items.Count - 1)));
            onSave = saveCallback;

            Text = plan.title + " · 编辑";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1180, 780);
            MinimumSize = new Size(900, 620);
            BackColor = Color.FromArgb(16, 20, 26);
            ForeColor = Color.FromArgb(230, 237, 243);
            KeyPreview = true;

            // 顶部工具条
            var bar = new Panel();
            bar.Height = 48;
            bar.Dock = DockStyle.Top;
            bar.BackColor = Color.FromArgb(22, 28, 36);
            Controls.Add(bar);

            int bx = 10;
            AddTool(bar, "+ 文字", ref bx, () => AddElement("text"));
            AddTool(bar, "+ 图片", ref bx, () => AddElement("image"));
            AddTool(bar, "+ 视频", ref bx, () => AddElement("video"));
            AddTool(bar, "+ 音乐", ref bx, () => AddElement("audio"));
            AddTool(bar, "删除选中", ref bx, () => DelSelected());
            AddTool(bar, "← 上一页", ref bx, () => GoPage(-1));
            AddTool(bar, "下一页 →", ref bx, () => GoPage(1));
            AddTool(bar, "＋ 新页", ref bx, () => NewPage());

            pageLab = Ui.L("", 10f, Color.FromArgb(139, 148, 158), false);
            pageLab.AutoSize = false;
            pageLab.Left = bx + 8;
            pageLab.Top = 14;
            pageLab.Width = 200;
            bar.Controls.Add(pageLab);

            // 左侧页列表
            thumbRail = new Panel();
            thumbRail.Width = 150;
            thumbRail.Dock = DockStyle.Left;
            thumbRail.BackColor = Color.FromArgb(19, 24, 31);
            thumbRail.AutoScroll = true;
            Controls.Add(thumbRail);

            // 画布外层（居中）
            var host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.FromArgb(12, 15, 20);
            host.Resize += (s, e) => CenterCanvas();
            Controls.Add(host);

            canvas = new Panel();
            canvas.Width = CW;
            canvas.Height = CH;
            canvas.BackColor = Color.FromArgb(8, 10, 14);
            canvas.BorderStyle = BorderStyle.FixedSingle;
            host.Controls.Add(canvas);

            canvas.MouseClick += (s, e) => { Select(null); };
            canvas.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) Select(null); };

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) DelSelected();
                else if (e.KeyCode == Keys.PageDown) GoPage(1);
                else if (e.KeyCode == Keys.PageUp) GoPage(-1);
            };

            Load += (s, e) => { CenterCanvas(); Render(); };
        }

        void CenterCanvas()
        {
            // 构造期间 host 加进来会先触发一次 Resize，这时 canvas 还没建好
            if (canvas == null || canvas.Parent == null) return;
            canvas.Left = Math.Max(0, (canvas.Parent.ClientSize.Width - CW) / 2);
            canvas.Top = 16;
        }

        void AddTool(Panel bar, string text, ref int bx, Action act)
        {
            var b = new Button();
            b.Text = text;
            b.AutoSize = false;
            b.Width = 92;
            b.Height = 32;
            b.Left = bx;
            b.Top = 8;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(38, 46, 58);
            b.ForeColor = Color.FromArgb(225, 232, 240);
            b.Font = Ui.F(9f, false);
            b.Click += (s, e) => act();
            bar.Controls.Add(b);
            bx += 97;
        }

        void GoPage(int d)
        {
            int n = pageIdx + d;
            if (n < 0 || n >= plan.items.Count) return;
            SaveCurrent();
            pageIdx = n;
            selected = null;
            Render();
        }

        void NewPage()
        {
            SaveCurrent();
            plan.items.Add(new PlanItem
            {
                id = "i" + DateTime.Now.Ticks,
                title = "第 " + (plan.items.Count + 1) + " 页",
            });
            pageIdx = plan.items.Count - 1;
            selected = null;
            Render();
            SaveCurrent();
        }

        void Render()
        {
            canvas.Controls.Clear();
            pageLab.Text = plan.items.Count == 0 ? "还没有页"
                : ("第 " + (pageIdx + 1) + " / " + plan.items.Count + " 页");

            RenderThumbs();

            if (plan.items.Count == 0) return;
            var it = plan.items[pageIdx];

            // 默认文本：日期 + 标题（如果页里没元素，就显示成提示）
            if (it.elements.Count == 0)
            {
                var hint = Ui.L("这一页是空的\n用上面的按钮加文字、图片、视频、音乐", 12f,
                    Color.FromArgb(88, 100, 116), false);
                hint.Left = 40;
                hint.Top = 200;
                hint.AutoSize = true;
                hint.TextAlign = ContentAlignment.MiddleLeft;
                canvas.Controls.Add(hint);
                return;
            }

            foreach (var el in it.elements)
                AddElementView(el);
        }

        void RenderThumbs()
        {
            thumbRail.Controls.Clear();
            int y = 10;
            for (int i = 0; i < plan.items.Count; i++)
            {
                int idx = i;
                var th = new Panel();
                th.Width = 126;
                th.Height = 74;
                th.Left = 10;
                th.Top = y;
                th.BackColor = i == pageIdx ? Color.FromArgb(34, 52, 68) : Color.FromArgb(26, 32, 41);
                th.BorderStyle = BorderStyle.FixedSingle;
                th.Cursor = Cursors.Hand;

                var num = Ui.L((i + 1).ToString(), 11f,
                    i == pageIdx ? Color.FromArgb(120, 210, 255) : Color.FromArgb(120, 132, 148), true);
                num.Left = 8; num.Top = 4;
                num.AutoSize = true;
                th.Controls.Add(num);

                var name = Ui.L(plan.items[i].title, 8f, Color.FromArgb(150, 160, 175), false);
                name.Left = 8; name.Top = 28;
                name.AutoSize = false;
                name.Width = 108;
                name.Height = 40;
                th.Controls.Add(name);

                th.Click += (s, e) =>
                {
                    SaveCurrent();
                    pageIdx = idx;
                    selected = null;
                    Render();
                };
                thumbRail.Controls.Add(th);
                y += 80;
            }
        }

        void AddElementView(PageElement el)
        {
            Control c = null;
            if (el.type == "text")
            {
                var lab = new Label();
                lab.Text = el.text;
                lab.ForeColor = Color.FromArgb(235, 242, 250);
                int fs = 18;
                try { fs = int.Parse(el.fontSize); } catch { }
                lab.Font = new Font("Microsoft YaHei", fs, FontStyle.Regular);
                lab.BackColor = Color.Transparent;
                lab.AutoSize = false;
                lab.TextAlign = ContentAlignment.TopLeft;
                c = lab;
            }
            else if (el.type == "image")
            {
                var pic = new PictureBox();
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.BackColor = Color.FromArgb(20, 25, 32);
                if (File.Exists(el.path))
                {
                    try
                    {
                        using (var fs2 = new FileStream(el.path, FileMode.Open, FileAccess.Read))
                        using (var tmp = Image.FromStream(fs2))
                            pic.Image = new Bitmap(tmp);
                    }
                    catch { }
                }
                else
                {
                    var miss = new Label();
                    miss.Text = "图片找不到\n" + Path.GetFileName(el.path);
                    miss.ForeColor = Color.FromArgb(200, 120, 120);
                    miss.Dock = DockStyle.Fill;
                    miss.TextAlign = ContentAlignment.MiddleCenter;
                    pic.Controls.Add(miss);
                }
                c = pic;
            }
            else
            {
                // 视频/音乐：显示成一块可点开的卡片
                var btn = new Button();
                bool isV = el.type == "video";
                btn.Text = (isV ? "▶ 视频" : "♪ 音乐") + "\n" + Path.GetFileName(el.path);
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = Color.FromArgb(30, 40, 52);
                btn.ForeColor = Color.FromArgb(190, 205, 220);
                btn.Font = Ui.F(10f, false);
                btn.Click += (s, e) =>
                {
                    try
                    {
                        using (var wf = new WmpForm(el.path))
                            wf.ShowDialog(this);
                    }
                    catch
                    {
                        MediaOpen.Open(el.path);
                    }
                };
                c = btn;
            }

            c.Left = el.x;
            c.Top = el.y;
            c.Width = Math.Max(20, el.w);
            c.Height = Math.Max(16, el.h);
            c.Tag = el;
            c.MouseDown += (s, e) => { Select(el); BeginDrag(c, e); };
            canvas.Controls.Add(c);
        }

        Point dragScreenStart;
        int dragOrigX, dragOrigY;
        bool dragging;

        void BeginDrag(Control c, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragScreenStart = Cursor.Position;
            var pe = c.Tag as PageElement;
            if (pe != null) { dragOrigX = pe.x; dragOrigY = pe.y; }
            c.BringToFront();
            c.MouseMove += DragMove;
            c.MouseUp += DragEnd;
        }

        void DragMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            var c = sender as Control;
            if (c == null) return;
            var el = c.Tag as PageElement;
            if (el == null) return;
            int dx = Cursor.Position.X - dragScreenStart.X;
            int dy = Cursor.Position.Y - dragScreenStart.Y;
            el.x = Math.Max(0, Math.Min(CW - el.w, dragOrigX + dx));
            el.y = Math.Max(0, Math.Min(CH - el.h, dragOrigY + dy));
            c.Left = el.x;
            c.Top = el.y;
        }

        void DragEnd(object sender, MouseEventArgs e)
        {
            dragging = false;
            var c = sender as Control;
            if (c != null)
            {
                c.MouseMove -= DragMove;
                c.MouseUp -= DragEnd;
            }
            SaveCurrent();
        }

        void Select(PageElement el)
        {
            selected = el;
            foreach (Control c in canvas.Controls)
            {
                if (c.Tag is PageElement)
                    c.BackColor = c.Tag == el ? Color.FromArgb(40, 60, 80) : Color.Transparent;
                if (c is Button && c.Tag is PageElement) { }
            }
        }

        void OnCanvasMouseMove(MouseEventArgs e)
        {
            if (!dragging || selected == null) return;
        }

        void DelSelected()
        {
            if (selected == null || plan.items.Count == 0) return;
            plan.items[pageIdx].elements.Remove(selected);
            selected = null;
            Render();
            SaveCurrent();
        }

        void AddElement(string type)
        {
            if (plan.items.Count == 0) { MessageBox.Show("先建一页。"); return; }
            var it = plan.items[pageIdx];

            if (type == "text")
            {
                string t = AskText("加文字", "写点什么", "在这里打字");
                if (t == null) return;
                it.elements.Add(new PageElement
                {
                    id = "e" + DateTime.Now.Ticks, type = "text",
                    x = 40, y = 40, w = 500, h = 60, text = t, fontSize = "18",
                });
            }
            else
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "选文件";
                    if (type == "image") dlg.Filter = "图片（全格式）|*.jpg;*.jpeg;*.jpe;*.jfif;*.pjpeg;*.pjp;*.png;*.apng;*.gif;*.bmp;*.dib;*.webp;*.tif;*.tiff;*.ico;*.svg;*.svgz;*.heic;*.heif;*.avif;*.raw;*.cr2;*.nef;*.dng;*.psd;*.xcf|所有文件|*.*";
                    else if (type == "video") dlg.Filter = "视频（全格式）|*.mp4;*.m4v;*.mov;*.qt;*.avi;*.mkv;*.wmv;*.flv;*.f4v;*.webm;*.mpg;*.mpeg;*.mpe;*.ts;*.m2ts;*.mts;*.3gp;*.3g2;*.rm;*.rmvb;*.asf;*.vob;*.ogv;*.divx;*.dv;*.mxf|所有文件|*.*";
                    else dlg.Filter = "音频（全格式）|*.mp3;*.wav;*.wave;*.flac;*.m4a;*.aac;*.ogg;*.oga;*.opus;*.wma;*.ape;*.aiff;*.aif;*.amr;*.mid;*.midi;*.ac3;*.dts;*.mka;*.ra;*.au;*.caf;*.wv;*.tta;*.spx|所有文件|*.*";
                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    int w = type == "image" ? 260 : 200;
                    int h = type == "image" ? 160 : 80;
                    it.elements.Add(new PageElement
                    {
                        id = "e" + DateTime.Now.Ticks, type = type,
                        x = 60, y = 60, w = w, h = h, path = dlg.FileName,
                    });
                }
            }
            Render();
            SaveCurrent();
        }

        void SaveCurrent()
        {
            if (onSave != null) onSave();
        }

        static string AskText(string title, string label, string initial)
        {
            using (var f = new InputForm(title, label, initial))
            {
                if (f.ShowDialog() == DialogResult.OK) return f.Value;
                return null;
            }
        }
    }
}
