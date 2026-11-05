using System;
using System.Collections.Generic;
using System.Text;

namespace JiaQi
{
    static class GenTest
    {
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            var pages = PlanGen.Generate("2027-01-18", "2027-02-21", "混合");
            Console.WriteLine("生成页数: " + pages.Count);
            foreach (var p in pages)
            {
                Console.WriteLine("  " + p.date + "  " + p.title + "  (" + p.elements.Count + " 个元素)");
                if (p.note.Length > 0) Console.WriteLine("       " + p.note);
            }
            Console.WriteLine("--- 学习型 ---");
            foreach (var p in PlanGen.Generate("2027-01-18", "2027-01-31", "学习"))
                Console.WriteLine("  " + p.date + "  " + p.title);
            Console.WriteLine("--- 旅行型 ---");
            foreach (var p in PlanGen.Generate("2027-01-18", "2027-02-10", "旅行"))
                Console.WriteLine("  " + p.date + "  " + p.title);
        }
    }
}
