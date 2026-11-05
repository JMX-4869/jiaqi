using System;
using System.Collections.Generic;

namespace JiaQi
{
    /// 计划生成：按表单填的条件排页，不联网。
    static class PlanGen
    {
        static readonly string[] StudyAM = {
            "背单词 / 课文", "做一套卷子", "整理错题本", "看一节网课", "练字半小时",
        };
        static readonly string[] StudyPM = {
            "下午自由活动", "约同学打球", "看一部纪录片", "帮忙做家务", "练琴 / 画画",
        };
        static readonly string[] Rest = {
            "睡到自然醒", "在家做一顿饭", "翻翻一直没看的书", "和朋友见个面",
            "整理房间，扔掉不用的东西", "出门散步，别带耳机", "看一部老电影",
        };

        public static List<PlanItem> Generate(string start, string end, PlanGenForm g)
        {
            var outList = new List<PlanItem>();
            DateTime st, en;
            if (!DateTime.TryParse(start, out st)) return outList;
            if (!DateTime.TryParse(end, out en)) return outList;
            int days = (int)(en - st).TotalDays + 1;
            if (days <= 0) return outList;

            string style = g != null ? g.Style : "混合";

            if (style == "自定义" && g != null && g.CustomText != null && g.CustomText.Trim().Length > 0)
                return GenCustom(st, en, g.CustomText);

            bool trip = style.Contains("旅行");
            bool study = style.Contains("学习");
            bool rest = style.Contains("休息") || (!trip && !study);

            string dest = g != null ? g.Dest : "";
            string people = g != null ? g.People : "";
            string budget = g != null ? g.Budget : "";
            string hobby = g != null ? g.Hobby : "";
            string stay = g != null ? g.Stay : "";
            string transport = g != null ? g.Transport : "";
            string food = g != null ? g.Food : "";
            string mustGo = g != null ? g.MustGo : "";
            string backPlan = g != null ? g.BackPlan : "";
            string restTime = g != null ? g.RestTime : "";
            string extra = g != null ? g.Extra : "";

            string place = dest.Length > 0 ? dest : "外面";

            int pageCount = trip
                ? Math.Min(10, Math.Max(4, days / 3 + 2))
                : Math.Min(8, Math.Max(3, days / 4 + 2));
            int step = Math.Max(1, days / Math.Max(1, pageCount - 1));

            for (int i = 0; i < pageCount; i++)
            {
                int offset = i == pageCount - 1 ? days - 1 : Math.Min(days - 1, i * step);
                var d = st.AddDays(offset);
                var it = new PlanItem
                {
                    id = "g" + DateTime.Now.Ticks + "_" + i,
                    date = d.ToString("yyyy-MM-dd"),
                };

                if (i == 0)
                {
                    if (trip)
                    {
                        it.title = "出发 · " + (transport.Length > 0 ? transport + "去" : "去") + place;
                        it.note = "行李：证件、充电器、常用药";
                        if (people.Length > 0) it.note += "\n同行：" + people;
                        if (stay.Length > 0) it.note += "\n住：" + stay;
                        if (budget.Length > 0) it.note += "\n预算：" + budget;
                        if (food.Length > 0) it.note += "\n吃：" + food;
                    }
                    else
                    {
                        it.title = "假期开始 · 先歇一天";
                        it.note = "别急着排满。房间收拾一下，睡个够。";
                    }
                }
                else if (i == pageCount - 1)
                {
                    if (trip)
                    {
                        it.title = "回程 · 收拾行李";
                        it.note = "把照片存一份。车票/机票时间再确认一次。";
                        if (backPlan.Length > 0) it.note += "\n" + backPlan;
                        if (restTime.Length > 0) it.note += "\n" + restTime;
                    }
                    else
                    {
                        it.title = "收心 · 准备开学";
                        it.note = "作业过一遍有没有漏的，校服洗干净，作息往回调两天。";
                        if (restTime.Length > 0) it.note += "\n" + restTime;
                    }
                }
                else if (trip)
                {
                    it.title = place + " · 第 " + (i + 1) + " 天";
                    string act = TripByHobby(hobby, i - 1);
                    if (i == 1 && mustGo.Length > 0) act = mustGo;
                    it.note = act;
                    string eat = food.Length > 0 ? food : EatByHobby(hobby, i - 1);
                    string nl2 = ((char)10).ToString();
                    it.note += nl2 + "吃：" + eat;
                    if (restTime.Length > 0) it.note += "\n" + restTime;
                }
                else if (study)
                {
                    it.title = "上午学，下午玩";
                    it.note = "上午：" + StudyAM[(i - 1) % StudyAM.Length]
                        + "\n下午：" + StudyPM[(i - 1) % StudyPM.Length];
                    if (restTime.Length > 0) it.note += "\n" + restTime;
                }
                else
                {
                    it.title = "慢慢过";
                    it.note = Rest[(i - 1) % Rest.Length];
                    if (hobby.Length > 0) it.note += "\n还想做：" + hobby;
                    if (restTime.Length > 0) it.note += "\n" + restTime;
                }

                if (extra.Length > 0 && i == 0) it.note += "\n补充：" + extra;

                it.elements.Add(new PageElement
                {
                    id = "e" + DateTime.Now.Ticks + "_" + i,
                    type = "text",
                    x = 60, y = 60, w = 760, h = 70,
                    text = it.title,
                    fontSize = "28",
                });
                if (it.note.Length > 0)
                {
                    it.elements.Add(new PageElement
                    {
                        id = "en" + DateTime.Now.Ticks + "_" + i,
                        type = "text",
                        x = 60, y = 150, w = 760, h = 260,
                        text = it.note,
                        fontSize = "17",
                    });
                }
                outList.Add(it);
            }
            return outList;
        }

        /// 用户自己写的内容：一行一页，按假期长度铺开。
        public static List<PlanItem> GenCustom(DateTime st, DateTime en, string text)
        {
            var outList = new List<PlanItem>();
            var pool = new List<string>();
            string nl = ((char)10).ToString();
            string cr = ((char)13).ToString();
            foreach (var raw in text.Replace(cr, "").Split(new string[] { nl }, StringSplitOptions.None))
            {
                var t = raw.Trim();
                if (t.Length > 0) pool.Add(t);
            }
            if (pool.Count == 0) return outList;

            int days = (int)(en - st).TotalDays + 1;
            if (days < 1) days = 1;
            int n = pool.Count;

            for (int i = 0; i < n; i++)
            {
                int offset = n == 1 ? 0 : (int)Math.Round((double)i * (days - 1) / (n - 1));
                if (offset >= days) offset = days - 1;
                var d = st.AddDays(offset);

                var it = new PlanItem
                {
                    id = "c" + DateTime.Now.Ticks + "_" + i,
                    date = d.ToString("yyyy-MM-dd"),
                };

                string line = pool[i];
                int colon = line.IndexOf('：');
                if (colon <= 0) colon = line.IndexOf(':');
                if (colon > 0 && colon < line.Length - 1 && colon <= 10)
                {
                    it.title = line.Substring(0, colon).Trim();
                    it.note = line.Substring(colon + 1).Trim();
                }
                else
                {
                    it.title = line.Length > 24 ? line.Substring(0, 24) : line;
                    it.note = line.Length > 24 ? line : "";
                }

                it.elements.Add(new PageElement
                {
                    id = "ce" + DateTime.Now.Ticks + "_" + i,
                    type = "text",
                    x = 60, y = 60, w = 760, h = 70,
                    text = it.title,
                    fontSize = "28",
                });
                if (it.note.Length > 0)
                {
                    it.elements.Add(new PageElement
                    {
                        id = "cn" + DateTime.Now.Ticks + "_" + i,
                        type = "text",
                        x = 60, y = 150, w = 760, h = 260,
                        text = it.note,
                        fontSize = "17",
                    });
                }
                outList.Add(it);
            }
            return outList;
        }

        /// 吃什么的建议：按用户写的美食偏好挑，没写就给通用本地菜。
        static string EatByHobby(string hobby, int idx)
        {
            string h = hobby == null ? "" : hobby;
            var pool = new List<string>();
            if (h.Contains("火锅")) pool.Add("找一家本地人排队的火锅店");
            if (h.Contains("面")) pool.Add("来一碗本地招牌面");
            if (h.Contains("烧烤") || h.Contains("串")) pool.Add("晚上去撸串");
            if (h.Contains("甜") || h.Contains("糕") || h.Contains("奶茶")) pool.Add("找家老字号甜品/糕点");
            if (h.Contains("辣")) pool.Add("点一份本地辣菜，别点太满");
            if (pool.Count == 0)
            {
                pool.Add("早饭吃本地早点，别在酒店吃");
                pool.Add("中午找家苍蝇馆子，看哪家人多进哪家");
                pool.Add("晚上逛夜市，边走边吃");
                pool.Add("找一道这道城市的招牌菜");
            }
            int k = idx % pool.Count;
            if (k < 0) k = -k;
            return pool[k];
        }

        /// 按用户写的兴趣挑活动；没写就按通用行程。
        static string TripByHobby(string hobby, int idx)
        {
            string h = hobby == null ? "" : hobby;
            var pool = new List<string>();
            if (h.Contains("吃") || h.Contains("美食") || h.Contains("小吃"))
                pool.Add("找本地人吃的店，别去网红店排队");
            if (h.Contains("拍") || h.Contains("摄影"))
                pool.Add("挑一个高处等日落，带上相机");
            if (h.Contains("博物") || h.Contains("展"))
                pool.Add("去博物馆 / 美术馆，提前看要不要预约");
            if (h.Contains("山") || h.Contains("徒步") || h.Contains("自然"))
                pool.Add("走一段山路，早点出发避开中午");
            if (h.Contains("书") || h.Contains("书店"))
                pool.Add("找一家独立书店待一下午");
            if (h.Contains("咖啡"))
                pool.Add("找一家本地咖啡馆坐坐");
            if (pool.Count == 0)
            {
                pool.Add("上午逛老街，下午随便走走");
                pool.Add("去当地人多的市场看看");
                pool.Add("找一家评价好的本地菜馆");
            }
            int k = idx % pool.Count;
            if (k < 0) k = -k;
            return pool[k];
        }
    }
}
