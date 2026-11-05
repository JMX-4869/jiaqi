using System;
using System.IO;
using System.Text;

namespace JiaQi
{
    static class RingTest
    {
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            string src = @"C:\Windows\Media\Alarm01.wav";
            Console.WriteLine("源: " + src);
            var info = RingTrim.Inspect(src);
            if (info == null) { Console.WriteLine("读不出 WAV 头"); return; }
            Console.WriteLine("时长 " + info.seconds.ToString("0.00") + "s  "
                + info.sampleRate + "Hz  " + info.channels + "ch  " + info.bitsPerSample + "bit");

            string dst = Path.Combine(Path.GetTempPath(), "ring_cut_test.wav");
            bool ok = RingTrim.Cut(src, dst, 0.5, 1.5);
            Console.WriteLine("截取 0.5-1.5s: " + (ok ? "成功" : "失败"));
            if (ok)
            {
                var i2 = RingTrim.Inspect(dst);
                Console.WriteLine("截出来: " + new FileInfo(dst).Length + " 字节, 时长 "
                    + (i2 != null ? i2.seconds.ToString("0.00") + "s" : "?"));
                Console.WriteLine("路径: " + dst);
            }
        }
    }
}
