using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Morphant.Build.Tasks;

internal static class PhysicalDirectory
{
    public static bool IgnoreCase => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static StringComparison Comparison => IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Resolve(string value, string baseDirectory, string property)
    {
        string full;
        try
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
                throw new ArgumentException();
            value = value.Replace('\\', Path.DirectorySeparatorChar);
            full = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(baseDirectory, value));
            var components = full.Substring(Path.GetPathRoot(full)!.Length)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (components.Any(component => component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException();
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new SnapshotException("MORPHANTMSB006", $"{property} must name one valid directory path.");
        }

        var suffix = new Stack<string>();
        var existing = Trim(full);
        while (!Directory.Exists(existing))
        {
            if (IsLink(existing))
                throw new SnapshotException("MORPHANTMSB016", $"{property} contains a broken or cyclic link at '{existing}'.");
            if (File.Exists(existing))
                throw new SnapshotException("MORPHANTMSB015", $"{property} '{existing}' names a file, not a directory.");
            var parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent))
                throw new IOException($"Cannot resolve directory '{existing}'.");
            suffix.Push(Path.GetFileName(existing));
            existing = parent;
        }

        var resolved = ResolveExisting(existing);
        while (suffix.Count > 0)
            resolved = Path.Combine(resolved, suffix.Pop());
        return Trim(resolved);
    }

    public static bool Equal(string left, string right) => string.Equals(left, right, Comparison);
    public static bool Inside(string candidate, string parent) => candidate.StartsWith(
        parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, Comparison);
    public static bool Overlap(string left, string right) => Equal(left, right) || Inside(left, right) || Inside(right, left);

    // Unlike URI conversion, this preserves literal %, #, ? and semicolon characters.
    public static string Relative(string from, string to)
    {
        if (!Equal(Path.GetPathRoot(from)!, Path.GetPathRoot(to)!))
            return to.Replace(Path.DirectorySeparatorChar, '/');
        var start = from.Split(Path.DirectorySeparatorChar);
        var end = to.Split(Path.DirectorySeparatorChar);
        var common = 0;
        while (common < start.Length && common < end.Length && Equal(start[common], end[common]))
            common++;
        return string.Join("/", Enumerable.Repeat("..", start.Length - common).Concat(end.Skip(common)));
    }

    public static bool IsLink(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (IOException exception) when ((exception.HResult & 0xffff) ==
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 1921 :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 62 : 40))
        {
            throw new SnapshotException("MORPHANTMSB016", $"Managed path '{path}' contains a cyclic link.");
        }
    }

    public static void EnsureNoLinks(string root, string candidate, string description)
    {
        if (!Equal(root, candidate) && !Inside(candidate, root))
            throw new SnapshotException("MORPHANTMSB003", $"{description} '{candidate}' escapes '{root}'.");
        var current = root;
        Check(current);
        foreach (var component in Relative(root, candidate).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            Check(current);
        }

        void Check(string path)
        {
            if (IsLink(path))
                throw new SnapshotException("MORPHANTMSB016", $"{description} contains a link at managed path '{path}'.");
            if (File.Exists(path))
                throw new SnapshotException("MORPHANTMSB015", $"{description} '{path}' names a file, not a directory.");
        }
    }

    private static string Trim(string path) => path.Length == Path.GetPathRoot(path)?.Length
        ? path : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string ResolveExisting(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            if (handle.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot resolve directory '{path}'.");
            var buffer = new StringBuilder(32768);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0 || length >= buffer.Capacity)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot resolve directory '{path}'.");
            var result = buffer.ToString();
            if (result.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
                return @"\\" + result.Substring(8);
            return result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result.Substring(4) : result;
        }

        var pointer = RealPath(path, IntPtr.Zero);
        if (pointer == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot resolve directory '{path}'.");
        try { return Marshal.PtrToStringAnsi(pointer)!; }
        finally { Free(pointer); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr RealPath(string path, IntPtr resolved);
    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);
}
