using System;
using System.Drawing;
using System.Windows.Forms;

namespace JiaQi
{
    /// 新建 / 编辑计划：名字 + 日历选起止（不再手打日期）
    class PlanDialog : Form
    {
        public string PlanTitle = "";
        public DateTime StartDate;
        public DateTime EndDate;
        public bool Ok = false;

        public string StartStr { get { return StartDate.ToString("yyyy-MM-dd"); } }
        public string EndStr { get { return EndDate.ToString("yyyy-MM-dd"); } }

        TextBox nameBox;
        MonthCalendar calStart, calEnd;
        Label rangeLab;

        public PlanDialog(string title, string initName, string initStart, string initEnd)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(16, 20, 26);
            ForeColor = Color.FromArgb(230, 237, 243);

            DateTime today = DateTime.Today;
            if (!DateTime.TryParse(initStart, out StartDate)) StartDate = today;
            if (!DateTime.TryParse(initEnd, out EndDate)) EndDate = StartDate.AddDays(20);

            var l1 = Ui.L("计划名字", 9f, Color.FromArgb(170, 184, 200), false);
            l1.Left = 18; l1.Top = 14; l1.AutoSize = true;
            Controls.Add(l1);

            nameBox = new TextBox();
            nameBox.Left = 18; nameBox.Top = 36; nameBox.Width = 660;
            nameBox.Text = initName ?? "";
            nameBox.Font = Ui.F(10f, false);
            nameBox.BackColor = Color.FromArgb(28, 34, 43);
            nameBox.ForeColor = Color.FromArgb(230, 237, 243);
            nameBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(nameBox);

            var l2 = Ui.L("开始", 9f, Color.FromArgb(170, 184, 200), false);
            l2.Left = 18; l2.Top = 74; l2.AutoSize = true;
            Controls.Add(l2);

            var l3 = Ui.L("结束", 9f, Color.FromArgb(170, 184, 200), false);
            l3.Left = 366; l3.Top = 74; l3.AutoSize = true;
            Controls.Add(l3);

            calStart = MakeCal(StartDate);
            calStart.Left = 16; calStart.Top = 94;
            calStart.DateSelected += (s, e) =>
            {
                if (calEnd.SelectionStart < calStart.SelectionStart)
                    calEnd.SetDate(calStart.SelectionStart);
                UpdateRange();
            };
            Controls.Add(calStart);

            calEnd = MakeCal(EndDate);
            calEnd.Left = 356; calEnd.Top = 94;
            calEnd.DateSelected += (s, e) =>
            {
                if (calEnd.SelectionStart < calStart.SelectionStart)
                    calStart.SetDate(calEnd.SelectionStart);
                UpdateRange();
            };
            Controls.Add(calEnd);

            rangeLab = Ui.L("", 9.5f, Color.FromArgb(76, 194, 255), true);
            rangeLab.Left = 18; rangeLab.Top = 322; rangeLab.Width = 660; rangeLab.Height = 24;
            rangeLab.AutoSize = false;
            Controls.Add(rangeLab);

            var ok = new Button();
            ok.Text = "确定";
            ok.Left = 480; ok.Top = 350; ok.Width = 92; ok.Height = 34;
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Color.FromArgb(76, 194, 255);
            ok.ForeColor = Color.FromArgb(11, 26, 34);
            ok.Click += (s, e) =>
            {
                Ok = true;
                PlanTitle = nameBox.Text.Trim();
                if (PlanTitle.Length == 0) PlanTitle = "假期计划";
                StartDate = calStart.SelectionStart.Date;
                EndDate = calEnd.SelectionStart.Date;
                if (EndDate < StartDate) EndDate = StartDate;
                Close();
            };
            Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "取消";
            cancel.Left = 582; cancel.Top = 350; cancel.Width = 92; cancel.Height = 34;
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Color.FromArgb(42, 50, 61);
            cancel.ForeColor = Color.FromArgb(230, 237, 243);
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            Load += (s, e) => UpdateRange();
        }

        MonthCalendar MakeCal(DateTime d)
        {
            var c = new MonthCalendar();
            c.MaxSelectionCount = 1;
            c.SetDate(d);
            c.BackColor = Color.FromArgb(28, 34, 43);
            c.ForeColor = Color.FromArgb(230, 237, 243);
            c.TitleBackColor = Color.FromArgb(32, 40, 51);
            c.TitleForeColor = Color.FromArgb(230, 237, 243);
            c.TrailingForeColor = Color.FromArgb(90, 100, 115);
            return c;
        }

        void UpdateRange()
        {
            DateTime a = calStart.SelectionStart.Date;
            DateTime b = calEnd.SelectionStart.Date;
            if (b < a) { var t = a; a = b; b = t; }
            int days = (int)(b - a).TotalDays + 1;
            rangeLab.Text = Model.FullPretty(a.ToString("yyyy-MM-dd")) + "  →  "
                + Model.FullPretty(b.ToString("yyyy-MM-dd")) + "   共 " + days + " 天";
        }
    }
}
