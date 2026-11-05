using System;
using System.Windows.Forms;

namespace JiaQi
{
    static class EditorTest
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            var plans = PlanStore.Load(@"D:\RuanJian\jiaqi\desktop\build\plans.json");
            if (plans.Count == 0) { MessageBox.Show("没有计划"); return; }
            var pl = plans[0];
            if (pl.items.Count == 0) { MessageBox.Show("没有页"); return; }
            try
            {
                using (var f = new PageEditor(pl, 0, () => PlanStore.Save(@"D:\RuanJian\jiaqi\desktop\build\plans.json", plans)))
                    f.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("崩了: " + ex.GetType().Name + " / " + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }
}
