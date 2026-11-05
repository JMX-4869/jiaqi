using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace JiaQi
{
    /// 打开图片/视频/音频：先走 shell 默认程序，不行再退回 explorer，再不行提示。
    static class MediaOpen
    {
        public static void Open(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                MessageBox.Show("文件不在了：\n" + path, "打不开");
                return;
            }

            // 1) 交给系统默认程序
            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = path;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(path);
                Process.Start(psi);
                return;
            }
            catch { }

            // 2) 让资源管理器去挑关联程序
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true,
                });
                return;
            }
            catch { }

            // 3) 直接从常见播放器起
            string[] players = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Windows Media Player\wmplayer.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Windows Media Player\wmplayer.exe"),
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
            };
            foreach (var pl in players)
            {
                try
                {
                    if (!File.Exists(pl)) continue;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pl,
                        Arguments = "\"" + path + "\"",
                        UseShellExecute = true,
                    });
                    return;
                }
                catch { }
            }

            MessageBox.Show("系统里没有能打开这个格式的程序。\n\n" + path
                + "\n\n装个播放器（比如 VLC）再试。", "打不开");
        }
    }
}
