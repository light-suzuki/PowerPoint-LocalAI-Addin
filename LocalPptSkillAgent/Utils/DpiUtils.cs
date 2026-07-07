using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalPptSkillAgent.Utils
{
    /// <summary>
    /// 高DPI対応ヘルパー。PowerPoint(Per-Monitor V2)内でSystem-AwareのままHWNDを作ると
    /// ビットマップ拡大でぼやけるため、UI生成時にスレッドのDPIコンテキストをPMv2へ切り替える。
    /// </summary>
    public static class DpiUtils
    {
        private static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        /// <summary>actionをPer-Monitor V2のDPIコンテキストで実行する。非対応OSではそのまま実行。</summary>
        public static T RunWithPerMonitorV2<T>(Func<T> action)
        {
            var previous = IntPtr.Zero;
            var switched = false;
            try
            {
                previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
                switched = previous != IntPtr.Zero;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            try { return action(); }
            finally { if (switched) { try { SetThreadDpiAwarenessContext(previous); } catch { } } }
        }

        /// <summary>96dpi基準のピクセル値を、コントロールの実DPIへスケールする。</summary>
        public static int Scale(Control control, int px96)
        {
            var dpi = control?.DeviceDpi ?? 96;
            return (int)Math.Round(px96 * dpi / 96.0);
        }
    }
}
