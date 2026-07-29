using System.Runtime.InteropServices;
using Xunit;

namespace PathLib.UnitTest.Utils
{
    public sealed class WindowsOnlyFact : FactAttribute
    {
        public WindowsOnlyFact() {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Skip = "Ignore on non-Windows platforms";
            }
        }
    }

    public sealed class PosixOnlyFact : FactAttribute
    {
        public PosixOnlyFact() {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Skip = "Ignore on non-Posix platforms";
            }
        }
    }
}