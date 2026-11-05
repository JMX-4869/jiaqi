using System;
using System.Windows.Forms;

namespace JiaQi
{
    /// Windows Media Player 的 ActiveX 宿主。能直接在画布上放视频/放声音。
    class AxWmp : AxHost
    {
        public AxWmp() : base("6BF52A52-394A-11d3-B153-00C04F79FAA6") { }

        public object Player { get { return GetOcx(); } }

        public void SeekTo(double seconds)
        {
            try
            {
                object ocx = GetOcx();
                if (ocx == null) return;
                object ctl = ocx.GetType().InvokeMember("controls",
                    System.Reflection.BindingFlags.GetProperty, null, ocx, null);
                if (ctl != null)
                    ctl.GetType().InvokeMember("currentPosition",
                        System.Reflection.BindingFlags.SetProperty, null, ctl, new object[] { seconds });
            }
            catch { }
        }

        public double Position()
        {
            try
            {
                object ocx = GetOcx();
                if (ocx == null) return 0;
                object ctl = ocx.GetType().InvokeMember("controls",
                    System.Reflection.BindingFlags.GetProperty, null, ocx, null);
                if (ctl == null) return 0;
                object v = ctl.GetType().InvokeMember("currentPosition",
                    System.Reflection.BindingFlags.GetProperty, null, ctl, null);
                return Convert.ToDouble(v);
            }
            catch { return 0; }
        }

        public void Pause()
        {
            try
            {
                object ocx = GetOcx();
                if (ocx == null) return;
                object ctl = ocx.GetType().InvokeMember("controls",
                    System.Reflection.BindingFlags.GetProperty, null, ocx, null);
                if (ctl != null)
                    ctl.GetType().InvokeMember("pause", System.Reflection.BindingFlags.InvokeMethod, null, ctl, null);
            }
            catch { }
        }

        public void Play(string path)
        {
            try
            {
                object ocx = GetOcx();
                if (ocx == null) return;
                Type t = ocx.GetType();
                t.InvokeMember("URL", System.Reflection.BindingFlags.SetProperty, null, ocx, new object[] { path });
                object ctl = t.InvokeMember("controls", System.Reflection.BindingFlags.GetProperty, null, ocx, null);
                if (ctl != null)
                    ctl.GetType().InvokeMember("play", System.Reflection.BindingFlags.InvokeMethod, null, ctl, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("播放失败: " + ex.Message);
            }
        }
    }
}
