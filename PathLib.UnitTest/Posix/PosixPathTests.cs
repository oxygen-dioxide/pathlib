using System;
using System.IO;
using Xunit;
using PathLib;
using Path = System.IO.Path;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit.Sdk;
using PathLib.UnitTest.Utils;

namespace PathLib.UnitTest.Posix;

public class PosixPathTestsFixture : IDisposable
{
    public string TempFolder { get; set; }

    public PosixPathTestsFixture()
    {
        do
        {
            TempFolder = Path.Combine(Path.GetTempPath(), "pathlib_" + Guid.NewGuid().ToString());
        } while (Directory.Exists(TempFolder));
        Directory.CreateDirectory(TempFolder);
        TempFolder = Directory.ResolveLinkTarget(TempFolder, returnFinalTarget: true)?.FullName ?? TempFolder;
    }

    public void Dispose()
    {
        Directory.Delete(TempFolder, true);
    }
}

public class PosixPathTests : IClassFixture<PosixPathTestsFixture>
{
    private readonly PosixPathTestsFixture _fixture;

    public PosixPathTests(PosixPathTestsFixture fixture)
    {
        _fixture = fixture;
    }

    [PosixOnlyFact]
    public void Stat_With_MissingFile_GivesError()
    {
        var path = new PosixPath(Path.Combine(_fixture.TempFolder, "does_not_exist"));
        path.Exists().Should().BeFalse();

        Assert.Throws<FileNotFoundException>(() => path.Stat());
    }

    [PosixOnlyFact]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public async Task Stat_WithFile_ReturnsStatInfo()
    {
        const string contents = "Hello World";
        var fname = Guid.NewGuid().ToString();
        var path = Path.Combine(_fixture.TempFolder, fname);
        await File.WriteAllTextAsync(path, contents);

        using(var process = new Process())
        {
            process.StartInfo.FileName = "stat";
            process.StartInfo.Arguments = $"--printf=\"%d %i 0x%f %h %u %g %s %.X %.Y %.Z %.W\\n\" \"{path}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);

            var parts = output.Trim().Split(' ');
            Assert.Equal(11, parts.Length);
            var st_dev = parts[0];
            var st_ino = parts[1];
            var rawMode = Convert.ToUInt32(parts[2], 16);
            var st_mode = Convert.ToString(rawMode & 0xfff, 8).PadLeft(4, '0');
            var st_nlink = parts[3];
            var st_uid = parts[4];
            var st_gid = parts[5];
            var st_size = parts[6];

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var st_atim = ParseTimestamp(parts[7], epoch);
            var st_mtim = ParseTimestamp(parts[8], epoch);
            var st_ctim = ParseTimestamp(parts[9], epoch);

            var info = new PosixPath(path).Stat();

            Assert.Equal(st_dev, info.Device.ToString());
            Assert.Equal(st_ino, info.Inode.ToString());
            Assert.Equal(st_mode, info.Mode);
            Assert.Equal(st_nlink, info.NumLinks.ToString());
            Assert.Equal(st_uid, info.Uid.ToString());
            Assert.Equal(st_gid, info.Gid.ToString());
            Assert.Equal(st_size, info.Size.ToString());
            Assert.Equal(st_atim, info.ATime);
            Assert.Equal(st_mtim, info.MTime);
            Assert.Equal(st_ctim, info.CTime);
        }
    }

    [PosixOnlyFact]
    public void ExpandUser_WithHomeDirSet_ReplacesPath()
    {
        var root = Environment.GetEnvironmentVariable("HOME");
        var expected = new PosixPath(root, "tmp");

        var actual = new PosixPath("~/tmp").ExpandUser();

        Assert.Equal(expected, actual);
    }

    [PosixOnlyFact]
    public void ExpandUser_WithNoHomeDirSet_ReplacesPath()
    {
        var root = Environment.GetEnvironmentVariable("HOME");
        var expected = new PosixPath(root, "tmp");
        Environment.SetEnvironmentVariable("HOME", null);

        var actual = new PosixPath("~/tmp").ExpandUser();

        Assert.Equal(expected, actual);
    }

    [PosixOnlyFact]
    public void IsSymlink_WithFile_ReturnsFalse()
    {
        var path = new PosixPath("/dev/mem");
        Assert.False(path.IsSymlink());
    }

    [PosixOnlyFact]
    public void IsSymlink_WithDirectory_ReturnsFalse()
    {
        var path = new PosixPath("/dev/usb");
        Assert.False(path.IsSymlink());
    }

    [PosixOnlyFact]
    public void IsSymlink_WithMissingFile_ReturnsFalse()
    {
        var path = new PosixPath(Path.Combine(_fixture.TempFolder, "does-not-exist"));
        Assert.False(path.IsSymlink());
    }

    [PosixOnlyFact]
    public void IsSymlink_WithSymlink_ReturnsTrue()
    {
        var path = new PosixPath("/dev/stdout");
        Assert.True(path.IsSymlink());
    }

    [PosixOnlyFact]
    public async Task FileType_WithRegularFile_ReturnsRegularFile()
    {
        const string contents = "Hello World";
        var fname = Guid.NewGuid().ToString();
        var path = Path.Combine(_fixture.TempFolder, fname);
        await File.WriteAllTextAsync(path, contents);
        var fileType = new PosixPath(path).GetFileType();
        Assert.Equal(PathLib.Posix.FileType.RegularFile, fileType);
    }

    [PosixOnlyFact]
    public void FileType_WithDirectory_ReturnsDirectory()
    {
        var fileType = new PosixPath(_fixture.TempFolder).GetFileType();
        Assert.Equal(PathLib.Posix.FileType.Directory, fileType);
    }

    [PosixOnlyFact]
    public void FileType_WithCharacterDevice_ReturnsCharacterDevice()
    {
        var fileType = new PosixPath("/dev/null").GetFileType();
        Assert.Equal(PathLib.Posix.FileType.CharacterDevice, fileType);
    }


    [PosixOnlyFact]
    public void FileType_WithSocket_ReturnsSocket()
    {
        var fname = Guid.NewGuid().ToString();
        var path = Path.Combine(_fixture.TempFolder, fname);
        var pipeServer = new NamedPipeServerStream(path, PipeDirection.InOut);

        var fileType = new PosixPath(path).GetFileType();
        Assert.Equal(PathLib.Posix.FileType.Socket, fileType);
    }


    [PosixOnlyFact]
    public void FileType_WithPipe_ReturnsFifo()
    {
        var fname = Guid.NewGuid().ToString();
        var path = Path.Combine(_fixture.TempFolder, fname);
        var err = mkfifo(path, 0x1ED); // 0o755
        if (err != 0)
        {
            var actualError = Marshal.GetLastWin32Error();
            throw new ApplicationException("Error: " + actualError);
        }

        var fileType = new PosixPath(path).GetFileType();
        Assert.Equal(PathLib.Posix.FileType.Fifo, fileType);
    }

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Auto, CallingConvention=CallingConvention.Cdecl)]
    private static extern int mkfifo(string path, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int mknod(string pathname, uint mode, ulong dev);

    private static DateTime ParseTimestamp(string value, DateTime epoch)
    {
        var dotIndex = value.IndexOf('.');
        var seconds = long.Parse(value.AsSpan(0, dotIndex));
        var nanosStr = value.AsSpan(dotIndex + 1);
        var nanos = long.Parse(nanosStr);
        return epoch.AddSeconds(seconds).AddTicks(nanos / 100);
    }

    private static string ParseDevice(string statOutput)
    {
        // New format: "Device: 8,33"
        var m = Regex.Match(statOutput, @"Device:\s(\d+),(\d+)");
        if (m.Success)
        {
            var major = int.Parse(m.Groups[1].Value);
            var minor = int.Parse(m.Groups[2].Value);
            return (major * 256 + minor).ToString();
        }
        // Old format: "Device: 801h/513d"
        m = Regex.Match(statOutput, @"Device:\s\w+/(\d+)d");
        return m.Groups[1].Value;
    }

    private static string? FindBlockDevice()
    {
        // Try to create a temporary block device node
        var tempPath = Path.Combine(Path.GetTempPath(), "pathlib_test_" + Guid.NewGuid().ToString());
        // mode = S_IFBLK (0x6000) | 0x1A4 (0666)
        // dev = makedev(7, 0) -> loop0 major 7, minor 0
        var result = mknod(tempPath, 0x6000 | 0x1A4, (7UL << 32) | 0);
        if (result == 0)
        {
            return tempPath;
        }
        // mknod failed (likely EPERM), search for existing block devices
        if (Directory.Exists("/dev"))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries("/dev"))
            {
                try
                {
                    var ft = new PosixPath(entry).GetFileType();
                    if (ft == PathLib.Posix.FileType.BlockDevice)
                    {
                        return entry;
                    }
                }
                catch
                {
                    // ignore errors
                }
            }
        }
        return null;
    }

    [PosixOnlyFact]
    public void FileType_WithFileNotExist_ReturnsFileNotExist()
    {
        var fname = Guid.NewGuid().ToString();
        var path = Path.Combine(_fixture.TempFolder, fname);

        var fileType = new PosixPath(path).GetFileType();
        Assert.Equal(PathLib.Posix.FileType.DoesNotExist, fileType);
    }

    [PosixOnlyFact]
    public void SetCurrentDirectory_WithDirectory_SetsEnvironmentVariable()
    {
        const string newCwd = @"/";
        var path = new PosixPath(newCwd);
        using (path.SetCurrentDirectory())
        {
            Assert.Equal(newCwd, Environment.CurrentDirectory);
        }
    }

    [PosixOnlyFact]
    public void SetCurrentDirectory_UponDispose_RestoresEnvironmentVariable()
    {
        var oldCwd = Environment.CurrentDirectory;
        var path = new PosixPath(@"/");
        var tmp = path.SetCurrentDirectory();

        tmp.Dispose();

        Assert.Equal(oldCwd, Environment.CurrentDirectory);
    }

    [PosixOnlyFact]
    public void JoinIPath_WithAnotherPath_ReturnsWindowsPath()
    {
        IPath path = new PosixPath(@"/tmp");
        IPath other = new PosixPath(@"/tmp");

        var final = path.Join(other);

        Assert.True(final is PosixPath);
    }

    [PosixOnlyFact]
    public void JoinIPath_WithAnotherPathByDiv_ReturnsWindowsPath()
    {
        IPath path = new PosixPath(@"/tmp");
        IPath other = new PosixPath(@"/tmp");

        var final = path / other;

        Assert.True(final is PosixPath);
    }

    [PosixOnlyFact]
    public void JoinIPath_WithStringByDiv_ReturnsWindowsPath()
    {
        IPath path = new PosixPath(@"/tmp");
        var other = @"/tmp";

        var final = path / other;

        Assert.True(final is PosixPath);
    }

    [PosixOnlyFact]
    public void JoinPosixPath_WithStringByDiv_ReturnsPosixPath()
    {
        var path = new PosixPath(@"/tmp");
        var other = @"/";

        var final = path / other;

        Assert.True(final is PosixPath);
    }

    [PosixOnlyFact]
    public void ResolvePosixPath_Dot_ReturnsWorkingDirectory()
    {
        var path = new PosixPath(".");
        var expected = new PosixPath(Environment.CurrentDirectory);
        var actual = path.Resolve();
        Assert.Equal(expected, actual);
    }

    [PosixOnlyFact]
    public void ResolvePosixPath_DotDot_ReturnsParentOfWorkingDirectory()
    {
        var path = new PosixPath("..");
        var expected = new PosixPath(Environment.CurrentDirectory).Parent();
        var actual = path.Resolve();
        Assert.Equal(expected, actual);
    }

    [PosixOnlyFact]
    public void ResolvePosixPath_IntermediateDotDot_IsRemoved()
    {
        var path = new PosixPath("/tmp/../tmp");
        var expected = new PosixPath("/tmp");
        var actual = path.Resolve();
        Assert.Equal(expected, actual);
    }

    [PosixOnlyFact]
    public async void ResolvePosixPath_SymlinkFile_ReturnsResolvedPath()
    {
        var tempFolder = new PosixPath(_fixture.TempFolder);
        var target = tempFolder.Join("target");
        var link = tempFolder.Join("link");

        await File.WriteAllTextAsync(target.ToString(), string.Empty);
        File.CreateSymbolicLink(link.ToString(), target.ToString());

        try
        {
            Assert.Equal(target, link.Resolve());
        }
        finally
        {
            link.Delete();
            target.Delete();
        }
    }

    [PosixOnlyFact]
    public async void ResolvePosixPath_SymlinkDirectory_ReturnsResolvedPath()
    {
        // /tmp/foo/folder1/target/hello.txt
        // /tmp/foo/folder2 -> folder1
        var tempFolder = new PosixPath(_fixture.TempFolder);
        var folder1 = tempFolder.Join("folder1");
        Directory.CreateDirectory(folder1.ToString());

        var target = folder1.Join("hello.txt");
        await File.WriteAllTextAsync(target.ToString(), "hello world");

        var folder2 = tempFolder.Join("folder2");
        Directory.CreateSymbolicLink(folder2.ToString(), folder1.ToString());

        var link = folder2.Join("hello.txt");

        try
        {
            Assert.Equal(target, link.Resolve());
        }
        finally
        {
            folder2.Delete();
            target.Delete();
            folder1.Delete();
        }
    }
}
