using System;
using System.IO;
using System.Text;

namespace JiaQi
{
    /// 铃声截取。只处理 WAV（PCM）：按秒裁一段出来，写成新 wav。
    /// 其他格式（mp3/m4a…）没法在纯 .NET 里解码，只能整段用。
    static class RingTrim
    {
        public class WavInfo
        {
            public int sampleRate;
            public int channels;
            public int bitsPerSample;
            public double seconds;
            public int dataOffset;
            public int dataLength;
        }

        public static WavInfo Inspect(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    var riff = new string(br.ReadChars(4));
                    if (riff != "RIFF") return null;
                    br.ReadInt32();
                    var wave = new string(br.ReadChars(4));
                    if (wave != "WAVE") return null;

                    var info = new WavInfo();
                    while (fs.Position + 8 <= fs.Length)
                    {
                        var id = new string(br.ReadChars(4));
                        int size = br.ReadInt32();
                        if (id == "fmt ")
                        {
                            br.ReadInt16();                       // audio format
                            info.channels = br.ReadInt16();
                            info.sampleRate = br.ReadInt32();
                            br.ReadInt32();                       // byte rate
                            br.ReadInt16();                       // block align
                            info.bitsPerSample = br.ReadInt16();
                            int extra = size - 16;
                            if (extra > 0) br.ReadBytes(extra);
                        }
                        else if (id == "data")
                        {
                            info.dataOffset = (int)fs.Position;
                            info.dataLength = size;
                            int bytesPerSec = info.sampleRate * info.channels * (info.bitsPerSample / 8);
                            info.seconds = bytesPerSec > 0 ? (double)size / bytesPerSec : 0;
                            return info;
                        }
                        else
                        {
                            fs.Seek(size, SeekOrigin.Current);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// 从 start 秒到 end 秒裁出来，存成 dst。成功返回 true。
        public static bool Cut(string src, string dst, double start, double end)
        {
            var info = Inspect(src);
            if (info == null) return false;
            if (end <= start) return false;
            if (start < 0) start = 0;
            if (end > info.seconds) end = info.seconds;

            int bytesPerFrame = info.channels * (info.bitsPerSample / 8);
            int bytesPerSec = info.sampleRate * bytesPerFrame;
            int off = (int)(start * bytesPerSec);
            int len = (int)((end - start) * bytesPerSec);
            off -= off % bytesPerFrame;
            len -= len % bytesPerFrame;
            if (off + len > info.dataLength) len = info.dataLength - off;
            if (len <= 0) return false;

            try
            {
                using (var fs = new FileStream(src, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                using (var os = new FileStream(dst, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(os))
                {
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + len);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);
                    bw.Write((short)1);                       // PCM
                    bw.Write((short)info.channels);
                    bw.Write(info.sampleRate);
                    bw.Write(info.sampleRate * bytesPerFrame);
                    bw.Write((short)bytesPerFrame);
                    bw.Write((short)info.bitsPerSample);
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(len);
                    fs.Seek(info.dataOffset + off, SeekOrigin.Begin);
                    var buf = new byte[64 * 1024];
                    int remain = len;
                    while (remain > 0)
                    {
                        int n = br.Read(buf, 0, Math.Min(buf.Length, remain));
                        if (n <= 0) break;
                        bw.Write(buf, 0, n);
                        remain -= n;
                    }
                }
                return true;
            }
            catch { return false; }
        }
    }
}
