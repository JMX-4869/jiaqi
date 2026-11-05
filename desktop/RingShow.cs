using System;
using System.Windows.Forms;

namespace JiaQi
{
    static class RingShow
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            var plans = PlanStore.Load(@"D:\RuanJian\jiaqi\desktop\build\plans.json");
            if (plans.Count == 0) { MessageBox.Show("没有计划"); return; }
            var p = plans[0];
            p.soundPath = @"C:\Windows\Media\Alarm01.wav";
            p.soundStart = 0.5;
            p.soundEnd = 1.5;
            using (var f = new RingSetting(p, () => { }))
                f.ShowDialog();
        }
    }
}
