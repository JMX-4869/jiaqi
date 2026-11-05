using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace JiaQi
{
    /// 提醒铃声：选本地文件、试听、截取一段。
    class RingSetting : Form
    {
        Plan plan;
        Action onSave;
        Label fileLab, infoLab;
        TextBox startBox, endBox;

        public RingSetting(Plan p, Action save)
        {
            plan = p;
            onSave = save;

            Text = "提醒铃声 · " + plan.title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(16, 21, 28);
            ForeColor = Color.FromArgb(230, 237, 243);

            int y = 16;
            var l1 = Ui.L("当前铃声", 9f, Color.FromArgb(139, 148, 158), false);
            l1.Left = 18; l1.Top = y; l1.AutoSize = true;
            Controls.Add(l1);
            y += 24;

            fileLab = Ui.L("", 10f, Color.FromArgb(230, 237, 243), true);
            fileLab.Left = 18; fileLab.Top = y; fileLab.Width = 560; fileLab.Height = 22;
            fileLab.AutoSize = false;
            Controls.Add(fileLab);
            y += 28;

            infoLab = Ui.L("", 8.5f, Color.FromArgb(120, 132, 148), false);
            infoLab.Left = 18; infoLab.Top = y; infoLab.Width = 560; infoLab.Height = 40;
            infoLab.AutoSize = false;
            Controls.Add(infoLab);
            y += 46;

            var pick = Btn("选本地音频…", 18, y, 130);
            pick.Click += (s, e) => PickFile();
            Controls.Add(pick);

            var preview = Btn("试听整段", 156, y, 110);
            preview.BackColor = Color.FromArgb(40, 48, 60);
            preview.Click += (s, e) => Preview(0, 0);
            Controls.Add(preview);

            var reset = Btn("恢复系统提示音", 274, y, 140);
            reset.BackColor = Color.FromArgb(40, 48, 60);
            reset.Click += (s, e) =>
            {
                plan.soundPath = "";
                plan.soundStart = 0;
                plan.soundEnd = 0;
                Save();
                Render();
            };
            Controls.Add(reset);
            y += 52;

            var l2 = Ui.L("截取片段（只对 WAV 有效，其他格式只能整段用）", 9f,
                Color.FromArgb(139, 148, 158), false);
            l2.Left = 18; l2.Top = y; l2.AutoSize = true;
            Controls.Add(l2);
            y += 26;

            var a = Ui.L("从第", 9f, Color.FromArgb(200, 210, 220), false);
            a.Left = 18; a.Top = y + 4; a.AutoSize = true;
            Controls.Add(a);

            startBox = new TextBox();
            startBox.Left = 66; startBox.Top = y; startBox.Width = 70;
            startBox.Text = plan.soundStart.ToString("0.##");
            startBox.BackColor = Color.FromArgb(28, 34, 43);
            startBox.ForeColor = Color.FromArgb(230, 237, 243);
            startBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(startBox);

            var b = Ui.L("秒，到第", 9f, Color.FromArgb(200, 210, 220), false);
            b.Left = 146; b.Top = y + 4; b.AutoSize = true;
            Controls.Add(b);

            endBox = new TextBox();
            endBox.Left = 220; endBox.Top = y; endBox.Width = 70;
            endBox.Text = plan.soundEnd.ToString("0.##");
            endBox.BackColor = Color.FromArgb(28, 34, 43);
            endBox.ForeColor = Color.FromArgb(230, 237, 243);
            endBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(endBox);

            var c = Ui.L("秒（0 = 整段）", 9f, Color.FromArgb(200, 210, 220), false);
            c.Left = 300; c.Top = y + 4; c.AutoSize = true;
            Controls.Add(c);

            var prev2 = Btn("试听这段", 18, y + 38, 110);
            prev2.BackColor = Color.FromArgb(40, 48, 60);
            prev2.Click += (s, e) =>
            {
                double st, en;
                double.TryParse(startBox.Text, out st);
                double.TryParse(endBox.Text, out en);
                if (en > 0 && en <= st) { MessageBox.Show("结束要比开始晚。"); return; }
                Preview(st, en);
            };
            Controls.Add(prev2);

            var cut = Btn("截取并保存", 136, y + 38, 130);
            cut.Click += (s, e) =>
            {
                double st, en;
                double.TryParse(startBox.Text, out st);
                double.TryParse(endBox.Text, out en);
                if (plan.soundPath.Length == 0) { MessageBox.Show("先选一个音频文件。"); return; }
                if (en <= st) { MessageBox.Show("结束要比开始晚。"); return; }

                string outPath = Path.Combine(Application.StartupPath, "rings");
                try
                {
                    if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);
                    string dst = Path.Combine(outPath,
                        Path.GetFileNameWithoutExtension(plan.soundPath) + "_cut.wav");
                    if (!RingTrim.Cut(plan.soundPath, dst, st, en))
                    {
                        MessageBox.Show("截取失败。\n\n只支持 WAV（PCM）格式；mp3/m4a 这类要先转成 wav。", "截取");
                        return;
                    }
                    plan.soundPath = dst;
                    plan.soundStart = 0;
                    plan.soundEnd = 0;
                    Save();
                    Render();
                    MessageBox.Show("截好了：\n" + dst, "完成");
                }
                catch (Exception ex) { MessageBox.Show("出错：" + ex.Message); }
            };
            Controls.Add(cut);

            var close = Btn("关闭", 274, y + 38, 100);
            close.BackColor = Color.FromArgb(40, 48, 60);
            close.Click += (s, e) => Close();
            Controls.Add(close);

            Load += (s, e) => Render();
        }

        Button Btn(string text, int left, int top, int width)
        {
            var b = new Button();
            b.Text = text;
            b.Left = left;
            b.Top = top;
            b.Width = width;
            b.Height = 34;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(76, 194, 255);
            b.ForeColor = Color.FromArgb(11, 26, 34);
            return b;
        }

        void Render()
        {
            if (plan.soundPath.Length == 0)
            {
                fileLab.Text = "系统提示音（默认）";
                infoLab.Text = "点「选本地音频」换成自己的铃声。";
                return;
            }
            fileLab.Text = Path.GetFileName(plan.soundPath);
            var info = RingTrim.Inspect(plan.soundPath);
            if (info != null)
                infoLab.Text = "时长 " + info.seconds.ToString("0.0") + " 秒 · "
                    + info.sampleRate + " Hz · " + info.channels + " 声道"
                    + (plan.soundEnd > 0 ? " · 只播 " + plan.soundStart.ToString("0.##")
                        + "—" + plan.soundEnd.ToString("0.##") + " 秒" : "");
            else
                infoLab.Text = "这个格式读不出时长（不是 WAV），只能整段播。";
        }

        void PickFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选一个音频";
                dlg.Filter = "音频（全格式）|*.wav;*.wave;*.mp3;*.m4a;*.aac;*.flac;*.ogg;*.oga;*.opus;*.wma;*.aiff;*.aif;*.amr;*.mid;*.midi;*.ac3;*.mka|所有文件|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                plan.soundPath = dlg.FileName;
                plan.soundStart = 0;
                plan.soundEnd = 0;
                Save();
                Render();
            }
        }

        void Preview(double start, double end)
        {
            if (plan.soundPath.Length == 0)
            {
                System.Media.SystemSounds.Exclamation.Play();
                return;
            }
            try
            {
                var wf = new WmpForm(plan.soundPath, start, end);
                wf.Show(this);
            }
            catch (Exception ex) { MessageBox.Show("试听失败：" + ex.Message); }
        }

        void Save()
        {
            if (onSave != null) onSave();
        }
    }
}
