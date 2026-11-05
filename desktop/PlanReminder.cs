using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace JiaQi
{
    /// 到点提醒：托盘气泡 + 响铃（可自定义铃声，可截取片段）。
    class PlanReminder : IDisposable
    {
        NotifyIcon tray;
        Timer tick;
        HashSet<string> firedToday = new HashSet<string>();
        List<Plan> plans;
        string dateKey = "";

        public event Action<Plan> OnRemind;

        public PlanReminder(List<Plan> list)
        {
            plans = list;

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Information;
            tray.Text = "假期表 · 计划提醒";
            tray.Visible = true;
            tray.BalloonTipClicked += (s, e) =>
            {
                if (OnRemind != null && lastPlan != null) OnRemind(lastPlan);
            };

            tick = new Timer();
            tick.Interval = 30000;          // 30 秒看一次
            tick.Tick += (s, e) => Check();
            tick.Start();
        }

        Plan lastPlan;

        public void Check()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (today != dateKey) { dateKey = today; firedToday.Clear(); }

            string hm = DateTime.Now.ToString("HH:mm");
            foreach (var p in plans)
            {
                if (!p.remind || string.IsNullOrEmpty(p.remindTime)) continue;
                if (p.start.Length > 0 && string.CompareOrdinal(today, p.start) < 0) continue;
                if (p.end.Length > 0 && string.CompareOrdinal(today, p.end) > 0) continue;
                if (string.CompareOrdinal(hm, p.remindTime) < 0) continue;

                string key = p.id + "|" + today;
                if (firedToday.Contains(key)) continue;
                firedToday.Add(key);

                Fire(p);
            }
        }

        void Fire(Plan p)
        {
            lastPlan = p;
            string body = p.title + " · 今天 " + p.remindTime;
            if (p.items.Count > 0)
            {
                // 找今天的条目
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                foreach (var it in p.items)
                {
                    if (it.date == today && it.title.Length > 0)
                    {
                        body = p.title + "\n" + it.title + (it.note.Length > 0 ? "\n" + it.note : "");
                        break;
                    }
                }
            }
            try
            {
                tray.ShowBalloonTip(8000, "假期计划", body, ToolTipIcon.Info);
            }
            catch { }

            if (p.sound) PlayRing(p);

            if (OnRemind != null) OnRemind(p);
        }

        public static void PlayRing(Plan p)
        {
            try
            {
                if (!string.IsNullOrEmpty(p.soundPath) && File.Exists(p.soundPath))
                {
                    // 有自定义铃声：用 WMP 从起点播到终点
                    var wf = new WmpForm(p.soundPath, p.soundStart, p.soundEnd);
                    wf.ShowInTaskbar = false;
                    wf.StartPosition = FormStartPosition.Manual;
                    wf.Location = new Point(-2000, -2000);   // 挪出屏幕：只出声，不挡人
                    wf.Size = new Size(320, 200);
                    wf.Show();
                    return;
                }
            }
            catch { }

            try { SystemSounds.Exclamation.Play(); } catch { }
        }

        public void Dispose()
        {
            try { if (tick != null) tick.Stop(); } catch { }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch { }
        }
    }
}
