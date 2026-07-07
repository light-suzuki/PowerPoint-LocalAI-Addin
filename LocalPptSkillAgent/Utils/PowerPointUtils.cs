using System;
using System.Runtime.InteropServices;

namespace LocalPptSkillAgent.Utils
{
    public static class PowerPointUtils
    {
        public static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        public static T Try<T>(Func<T> action, T fallback = default(T))
        {
            try { return action(); } catch (COMException) { return fallback; }
        }
    }
}
