using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace JiaQi
{
    /// 内嵌播放视频/音频。也可以只播某一段（铃声截取预览用）。
    class WmpForm : Form
    {
        AxWmp ax;
        string path;
        double segStart, segEnd;
        Timer segTimer;

        public WmpForm(string file) : this(file, 0, 0) { }

        public WmpForm(string file, double start, double end)
        {
            path = file;
            segStart = start;
            segEnd = end;
            Text = Path.GetFileName(file);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(880, 560);
            MinimumSize = new Size(420, 300);
            BackColor = Color.FromArgb(12, 15, 20);
            ForeColor = Color.FromArgb(230, 237, 243);

            bool embedded = true;
            try
            {
                ax = new AxWmp();
                ax.Dock = DockStyle.Fill;
                Controls.Add(ax);
            }
            catch (Exception ex)
            {
                embedded = false;
                var lab = new Label();
                lab.Text = "内嵌播放器起不来（" + ex.Message + "），用系统播放器打开吧。";
                lab.Dock = DockStyle.Top;
                lab.Height = 60;
                lab.ForeColor = Color.FromArgb(200, 140, 140);
                Controls.Add(lab);

                var btn = new Button();
                btn.Text = "用系统播放器打开";
                btn.Dock = DockStyle.Top;
                btn.Height = 40;
                btn.Click += (s, e) => MediaOpen.Open(path);
                Controls.Add(btn);
            }

            Shown += (s, e) =>
            {
                if (embedded && ax != null)
                {
                    ax.Play(path);
                    if (segStart > 0) ax.SeekTo(segStart);
                }
            };

            // 只播一段：到点就停
            if (segEnd > 0 && segEnd > segStart)
            {
                segTimer = new Timer();
                segTimer.Interval = 200;
                segTimer.Tick += (s, e) =>
                {
                    try
                    {
                        if (ax == null) return;
                        double pos = ax.Position();
                        if (pos >= segEnd) { ax.Pause(); segTimer.Stop(); }
                    }
                    catch { }
                };
                segTimer.Start();
            }

            FormClosed += (s, e) =>
            {
                try { if (segTimer != null) segTimer.Stop(); } catch { }
                try { if (ax != null) ax.Dispose(); } catch { }
            };
        }
    }
}
