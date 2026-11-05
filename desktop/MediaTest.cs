using System;
using System.Diagnostics;

namespace JiaQi
{
    static class MediaTest
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            string f = @"C:\Windows\Media\Alarm01.wav";
            Console.WriteLine("测试: " + f);
            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = f;
                psi.UseShellExecute = true;
                var p = Process.Start(psi);
                Console.WriteLine("启动成功: " + (p == null ? "(null)" : p.ProcessName));
                System.Threading.Thread.Sleep(1200);
                if (p != null && !p.HasExited) { p.Kill(); Console.WriteLine("已关闭"); }
            }
            catch (Exception ex)
            {
                Console.WriteLine("失败: " + ex.GetType().Name + " / " + ex.Message);
            }
        }
    }
}
