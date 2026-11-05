using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace JiaQi
{
    static class SlideTest
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            var plans = PlanStore.Load(@"D:\RuanJian\jiaqi\desktop\build\plans.json");
            if (plans.Count == 0) { MessageBox.Show("没有计划"); return; }
            var pl = plans[0];
            if (pl.items.Count == 0) { MessageBox.Show("没有条目"); return; }
            try
            {
                using (var f = new SlideForm(pl, new List<PlanItem>(pl.items)))
                    f.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("崩了: " + ex.GetType().Name + " / " + ex.Message);
            }
        }
    }
}
