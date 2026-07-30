using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using PathLib.Utils;

namespace PathLib
{
    // https://pathlib.readthedocs.org/en/latest/
    // https://docs.python.org/3/library/pathlib.html#module-pathlib
    // http://www.dotnetperls.com/path
    /// <summary>
    /// Base class containing common IPurePath code.
    /// </summary>
    public abstract class PurePath<TPath> : IPurePath<TPath>, IXmlSerializable
        where TPath : PurePath<TPath>
    {
        // Drive + Root + Tail (ImmutableList of path segments)

        private const string UriPrefix = "file://";

        #region ctors

        /// <summary>
        /// Create a path in the current working directory.
        /// </summary>
        protected PurePath()
        {
            Drive = "";
            Root = "";
            Tail = ImmutableList.Create(PathUtils.CurrentDirectoryIdentifier);
        }

        /// <summary>
        /// Create a path by joining the given path strings.
        /// </summary>
        /// <param name="parser">Parses parts out of a path.</param>
        /// <param name="paths">Paths to combine.</param>
        protected PurePath(IPathParser parser, params string[] paths)
        {
            string rawPath = null;
            if (paths.Length > 1)
            {
                var components = paths.Select(p =>
                    PurePathFactory(NormalizeSeparators(p)));
                var path = JoinInternal(components);
                rawPath = path.ToString();
                Assimilate(path);
            }
            else if (paths.Length == 1 && !String.IsNullOrEmpty(paths[0]))
            {
                rawPath = NormalizeSeparators(paths[0]);
                Drive = "";
                Root = "";
                Tail = ImmutableList<string>.Empty;
            }
            else
            {
                Drive = "";
                Root = "";
                rawPath = PathUtils.CurrentDirectoryIdentifier;
            }
            if (rawPath != null &&
                rawPath.StartsWith(NormalizeSeparators(UriPrefix)))
            {
                rawPath = rawPath.Substring(UriPrefix.Length);
            }
            Initialize(rawPath, parser);
        }

        /// <summary>
        /// Create a new PurePath from the specified components.
        /// </summary>
        /// <param name="drive"></param>
        /// <param name="root"></param>
        /// <param name="tail"></param>
        protected PurePath(string drive, string root, ImmutableList<string> tail)
        {
            Drive = drive ?? "";
            Root = root ?? "";
            Tail = tail ?? ImmutableList<string>.Empty;
        }

        /// <summary>
        /// Create a path by joining the given IPurePaths.
        /// </summary>
        /// <param name="paths">Paths to combine.</param>
        protected PurePath(params IPurePath[] paths)
        {
            Assimilate(JoinInternal(paths));
        }

        /// <summary>
        /// Replace the current path components with the given path's.
        /// </summary>
        /// <param name="path"></param>
        private void Assimilate(IPurePath path)
        {
            Drive = path.Drive ?? "";
            Root = path.Root ?? "";
            Tail = (path.Tail as ImmutableList<string>)
                ?? path.Tail?.ToImmutableList()
                ?? ImmutableList<string>.Empty;
        }

        private void Initialize(string rawPath, IPathParser parser)
        {
            if (rawPath == null)
            {
                return;
            }

            Drive = parser.ParseDrive(rawPath) ?? "";
            Root = parser.ParseRoot(rawPath) ?? "";

            if (Drive.Length + Root.Length >= rawPath.Length)
            {
                return;
            }

            rawPath = rawPath.Substring(Drive.Length + Root.Length);

            char reservedCharacter;
            if (parser.ReservedCharactersInPath(rawPath, out reservedCharacter))
            {
                throw new InvalidPathException(rawPath, String.Format(
                    "Path contains reserved character '{0}'.", reservedCharacter));
            }

            // Remove trailing separator (matching Python's pathlib behavior)
            if (rawPath.EndsWith(PathSeparator))
            {
                rawPath = rawPath.TrimEnd(PathSeparator.ToCharArray());
            }

            // Parse into tail segments
            var tail = parser.ParseTail(rawPath) ?? ImmutableList<string>.Empty;

            // Handle hidden files: if last segment starts with '.'
            // and the basename would be empty, treat it as a single
            // hidden file name (not an extension)
            if (tail.Count > 0)
            {
                var last = tail[tail.Count - 1];
                if (last != "." && last != "..")
                {
                    int dotIndex = last.LastIndexOf('.');
                    if (dotIndex == 0)
                    {
                        // .hiddenfile - keep it as a single segment
                        // (no extension splitting needed since it's
                        // already in the tail as a filename)
                    }
                }
            }

            Tail = NormalizeTail(tail);
        }

        #endregion

        #region Basic components of path

        /// <inheritdoc/>
        public string Drive { get; private set; }

        /// <inheritdoc/>
        public string Root { get; private set; }

        /// <inheritdoc/>
        public ImmutableList<string> Tail { get; private set; }

        /// <inheritdoc/>
        IReadOnlyList<string> IPurePath.Tail => Tail;

        #endregion

        /// <inheritdoc/>
        public string Anchor { get { return Drive + Root; } }

        /// <inheritdoc/>
        public string Dirname
        {
            get
            {
                if (Tail.Count == 0) return String.Empty;
                if (Tail.Count == 1 && (Tail[0] == "." || Tail[0] == ".."))
                {
                    return Tail[0];
                }
                if (Tail.Count <= 1) return String.Empty;
                return String.Join(PathSeparator, Tail.Take(Tail.Count - 1));
            }
        }

        /// <inheritdoc/>
        public string Directory
        {
            get
            {
                if (!String.IsNullOrEmpty(Dirname))
                {
                    return Anchor + Dirname;
                }
                return String.Empty;
            }
        }

        /// <inheritdoc/>
        public string Filename
        {
            get
            {
                if (Tail.Count == 0)
                {
                    return String.Empty;
                }
                return Tail[Tail.Count - 1];
            }
        }

        /// <inheritdoc/>
        public string Basename
        {
            get
            {
                if (Tail.Count == 0) return "";
                var last = Tail[Tail.Count - 1];
                if (Tail.Count == 1 && (last == "." || last == "..")) return "";
                if (last == "." || last == "..") return last;
                int dotIndex = last.LastIndexOf('.');
                if (dotIndex <= 0) return last;
                return last.Substring(0, dotIndex);
            }
        }

        /// <inheritdoc/>
        public string Extension
        {
            get
            {
                if (Tail.Count == 0) return "";
                var last = Tail[Tail.Count - 1];
                if (last == "." || last == "..") return "";
                int dotIndex = last.LastIndexOf('.');
                if (dotIndex <= 0) return "";
                return last.Substring(dotIndex);
            }
        }

        /// <inheritdoc/>
        public string BasenameWithoutExtensions
        {
            get
            {
                var parts = Filename.Split(PathUtils.ExtensionDelimiter);
                if (parts[0] == String.Empty && parts.Length > 1)
                {
                    return PathUtils.ExtensionDelimiter + parts[1];
                }
                return parts[0];
            }
        }

        /// <inheritdoc/>
        public string[] Extensions
        {
            get
            {
                var parts = Filename.Split(
                    new[] { PathUtils.ExtensionDelimiter },
                    StringSplitOptions.RemoveEmptyEntries);
                var ret = new string[parts.Length - 1];
                for (var i = 0; i < ret.Length; i++)
                {
                    ret[i] = PathUtils.ExtensionDelimiter + parts[i + 1];
                }
                return ret;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> Parts
        {
            get
            {
                if (_cachedParts == null)
                {
                    lock (_partsLock)
                    {
                        if (_cachedParts == null)
                        {
                            _cachedParts = BuildTailParts();
                        }
                    }
                }
                return _cachedParts;
            }
        }
        private IEnumerable<string> _cachedParts;
        private readonly object _partsLock = new object();

        private IEnumerable<string> BuildTailParts()
        {
            if (Anchor != String.Empty)
            {
                yield return Anchor;
            }

            foreach (var part in Tail)
            {
                yield return part;
            }
        }

        /// <inheritdoc/>
        public string ToPosix()
        {
            if (_cachedPosix == null)
            {
                lock (_toPosixLock)
                {
                    if (_cachedPosix == null)
                    {
                        _cachedPosix = ToString().Replace(@"\", "/");
                    }
                }
            }
            return _cachedPosix;
        }
        private string _cachedPosix;
        private readonly object _toPosixLock = new object();

        /// <inheritdoc/>
        public TPath Join(params string[] paths)
        {
            return JoinInternal(
                new[] { (TPath)this }
                    .Concat(paths.Select(PurePathFactory)));
        }

        public static PurePath<TPath> operator/ (PurePath<TPath> lvalue, PurePath<TPath> rvalue)
        {
            return lvalue.JoinInternal(new[] { lvalue, rvalue });
        }

        public static PurePath<TPath> operator/ (PurePath<TPath> lvalue, string rvalue)
        {
            return lvalue.JoinInternal(new[] { lvalue, lvalue.PurePathFactory(rvalue) });
        }

        IPurePath IPurePath.Join(params string[] paths)
        {
            return Join(paths);
        }

        /// <inheritdoc/>
        public TPath Join(params IPurePath[] paths)
        {
            return JoinInternal(new[] { this }.Concat(paths));
        }

        IPurePath IPurePath.Join(params IPurePath[] paths)
        {
            return Join(paths);
        }

        private TPath JoinInternal(IEnumerable<string> paths)
        {
            return JoinInternal(paths.Select(PurePathFactory));
        }

        private TPath JoinInternal(IEnumerable<TPath> paths)
        {
            return JoinInternal(paths.Select(p => (IPurePath)p));
        }

        private TPath JoinInternal(IEnumerable<IPurePath> paths)
        {
            var pathsList = new List<IPurePath>(paths);
            var combined = PathUtils.Combine(pathsList, PathSeparator);
            if (combined == null)
            {
                return PurePathFactoryFromComponents(
                    "", "", ImmutableList<string>.Empty);
            }

            var path = PurePathFactory(combined);
            if (path.Drive == String.Empty)
            {
                var drive = pathsList
                    .Where(p => p.Drive != String.Empty)
                    .Select(p => p.Drive)
                    .LastOrDefault();
                if (drive != null)
                {
                    path = PurePathFactoryFromComponents(path, drive);
                }
            }

            return path;
        }

        /// <inheritdoc/>
        public bool TrySafeJoin(string relativePath, out TPath joined)
        {
            var toJoin = PurePathFactory(relativePath);
            string combined;
            if (!PathUtils.TrySafeCombine(this, toJoin, PathSeparator, out combined))
            {
                joined = null;
                return false;
            }

            joined = PurePathFactory(combined);
            return true;
        }

        /// <inheritdoc/>
        public bool TrySafeJoin(IPurePath relativePath, out TPath joined)
        {
            string combined;
            if (!PathUtils.TrySafeCombine(this, relativePath, PathSeparator, out combined))
            {
                joined = null;
                return false;
            }

            joined = PurePathFactory(combined);
            return true;
        }

        bool IPurePath.TrySafeJoin(string relativePath, out IPurePath joined)
        {
            TPath subPath;
            if (TrySafeJoin(relativePath, out subPath))
            {
                joined = subPath;
                return true;
            }
            joined = null;
            return false;
        }

        bool IPurePath.TrySafeJoin(IPurePath relativePath, out IPurePath joined)
        {
            TPath subPath;
            if (TrySafeJoin(relativePath, out subPath))
            {
                joined = subPath;
                return true;
            }
            joined = null;
            return false;
        }

        private string NormalizeSeparators(string path)
        {
            if (path is null)
            {
                throw new InvalidPathException("", "Path component was null");
            }
            foreach (var separator in PathUtils.PathSeparatorsForNormalization)
            {
                path = path.Replace(separator, PathSeparator);
            }
            return path;
        }

        private ImmutableList<string> NormalizeTail(ImmutableList<string> tail)
        {
            if (tail.Count == 0) return tail;

            var builder = ImmutableList.CreateBuilder<string>();
            for (var i = 0; i < tail.Count; i++)
            {
                var part = tail[i];
                if (part == String.Empty)
                {
                    continue;
                }
                // Remove "." from dirname parts only, preserve as filename
                if (part == PathUtils.CurrentDirectoryIdentifier && i < tail.Count - 1)
                {
                    continue;
                }
                builder.Add(part);
            }
            return builder.ToImmutable();
        }

        /// <inheritdoc/>
        public TPath Parent()
        {
            return Parent(1);
        }

        IPurePath IPurePath.Parent()
        {
            return Parent();
        }

        /// <inheritdoc/>
        public TPath Parent(int nthParent)
        {
            return Parents().Skip(nthParent - 1).FirstOrDefault();
        }

        IPurePath IPurePath.Parent(int nthParent)
        {
            return Parent(nthParent);
        }

        /// <inheritdoc/>
        public IEnumerable<TPath> Parents()
        {
            var maxPathLength = Parts.Count() - 1;
            for (var i = maxPathLength; i > 0; i--)
            {
                yield return PurePathFactory(
                    JoinInternal(
                        Parts.Take(i))
                    .ToString());
            }
        }

        IEnumerable<IPurePath> IPurePath.Parents()
        {
            return Parents().Select(p => (IPurePath)p);
        }

        /// <inheritdoc/>
        public Uri ToUri()
        {
            if (!IsAbsolute())
            {
                throw new InvalidOperationException(
                    "Cannot create a URI from a relative path.");
            }
            return new Uri(UriPrefix + ToPosix());
        }

        /// <inheritdoc/>
        public TPath RelativeTo(IPurePath parent)
        {
            if (!ComponentComparer.Equals(parent.Drive, Drive) ||
                !ComponentComparer.Equals(parent.Root, Root))
            {
                throw new ArgumentException(String.Format(
                    "'{0}' does not share the same root/drive as '{1}', " +
                    "thus cannot be relative.", this, parent));
            }

            var parentRelative = parent.Relative().ToString();

            if (parentRelative == String.Empty)
            {
                return Relative();
            }

            // Walk parent dirs using the relative path parts
            var parentDirEnum = parentRelative.Split(
                PathSeparator[0]).GetEnumerator();
            var thisDirname = Tail.Take(
                Tail.Count > 0 ? Tail.Count - 1 : 0).ToList();
            var thisDirEnum = thisDirname.GetEnumerator();

            while (parentDirEnum.MoveNext())
            {
                if (!thisDirEnum.MoveNext() ||
                    !ComponentComparer.Equals(parentDirEnum.Current, thisDirEnum.Current))
                {
                    throw new ArgumentException(String.Format(
                        "'{0}' does not start with '{1}'", this, parent));
                }
            }

            var builder = new StringBuilder();
            while (thisDirEnum.MoveNext())
            {
                if (builder.Length != 0)
                {
                    builder.Append(PathSeparator);
                }
                builder.Append(thisDirEnum.Current);
            }

            // Build result: remaining dir parts + original filename
            var resultParts = builder.Length > 0
                ? builder.ToString().Split(PathSeparator[0])
                : Array.Empty<string>();
            var resultTail = resultParts.ToImmutableList().Add(Filename);
            return PurePathFactoryFromComponents("", "", resultTail);
        }

        IPurePath IPurePath.RelativeTo(IPurePath parent)
        {
            return RelativeTo(parent);
        }

        /// <inheritdoc/>
        public TPath WithDirname(IPurePath newDirname)
        {
            var formatted = newDirname.GetComponents(
                    PathComponent.Dirname | PathComponent.Filename);
            if (IsAbsolute() || !newDirname.IsAbsolute())
            {
                return PurePathFactoryFromComponents(this,
                    tail: BuildTailFromDirname(formatted, Filename));
            }
            return PurePathFactoryFromComponents(this,
                newDirname.Drive,
                newDirname.Root,
                BuildTailFromDirname(formatted, Filename));
        }

        /// <inheritdoc/>
        public TPath WithDirname(string newDirname)
        {
            return WithDirname(PurePathFactory(newDirname));
        }

        IPurePath IPurePath.WithDirname(string newDirname)
        {
            return String.IsNullOrEmpty(newDirname)
                ? this
                : WithDirname(PurePathFactory(newDirname));
        }

        IPurePath IPurePath.WithDirname(IPurePath newDirname)
        {
            return WithDirname(newDirname);
        }

        private ImmutableList<string> BuildTailFromDirname(string dirnamePart, string filename)
        {
            var parts = dirnamePart.Split(new[] { PathSeparator[0] },
                StringSplitOptions.RemoveEmptyEntries);
            return parts.ToImmutableList().Add(filename);
        }

        /// <inheritdoc/>
        public TPath WithExtension(string newExtension)
        {
            var fname = PurePathFactory(newExtension);
            // Check that newExtension contains only basename/extension parts
            if (fname.Drive != "" || fname.Root != "" ||
                (fname.Tail.Count > 1) ||
                (fname.Tail.Count == 1 && fname.Dirname != ""))
            {
                throw new InvalidPathException(newExtension,
                    "Path must contain only extension.");
            }
            if (fname.Extension != String.Empty)
            {
                // Multiple extensions... place the extras on the basename
                var newLast = Basename + PrependWithDot(fname.Basename) +
                    PrependWithDot(fname.Extension);
                return PurePathFactoryFromComponents(this,
                    tail: Tail.SetItem(Tail.Count - 1, newLast));
            }

            var last = Tail[Tail.Count - 1];
            var nameWithoutExt = Path.GetFileNameWithoutExtension(last);
            var newLast2 = nameWithoutExt + PrependWithDot(fname.Basename);
            return PurePathFactoryFromComponents(this,
                tail: Tail.SetItem(Tail.Count - 1, newLast2));
        }

        private static string PrependWithDot(string extension)
        {
            if (extension.StartsWith("" + PathUtils.ExtensionDelimiter))
            {
                return extension;
            }
            return PathUtils.ExtensionDelimiter + extension;
        }

        IPurePath IPurePath.WithExtension(string newExtension)
        {
            return WithExtension(newExtension);
        }

        /// <inheritdoc/>
        public TPath WithFilename(string newFilename)
        {
            if (String.IsNullOrEmpty(newFilename))
            {
                return PurePathFactoryFromComponents(this,
                    tail: Tail.Count > 0
                        ? Tail.RemoveAt(Tail.Count - 1)
                        : ImmutableList<string>.Empty);
            }

            var fname = PurePathFactory(newFilename);
            if (fname.Drive != "" || fname.Root != "" ||
                (fname.Tail.Count > 1) ||
                (fname.Tail.Count == 1 && fname.Dirname != ""))
            {
                throw new ArgumentException(String.Format(
                    "New filename '{0}' must contain only basename and/or extension.", newFilename),
                    "newFilename");
            }

            var newTail = Tail.Count > 0
                ? Tail.SetItem(Tail.Count - 1, newFilename)
                : ImmutableList.Create(newFilename);
            return PurePathFactoryFromComponents(this,
                tail: newTail);
        }

        IPurePath IPurePath.WithFilename(string newFilename)
        {
            return WithFilename(newFilename);
        }

        /// <inheritdoc/>
        public bool HasComponents(PathComponent components)
        {
            if ((components & PathComponent.Drive) == PathComponent.Drive
                && Drive != String.Empty)
            {
                return true;
            }
            if ((components & PathComponent.Root) == PathComponent.Root
                && Root != String.Empty)
            {
                return true;
            }
            if ((components & PathComponent.Dirname) == PathComponent.Dirname
                && Dirname != String.Empty)
            {
                return true;
            }
            if ((components & PathComponent.Basename) == PathComponent.Basename
                && Basename != String.Empty)
            {
                return true;
            }
            if ((components & PathComponent.Extension) == PathComponent.Extension
                && Extension != String.Empty)
            {
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public string GetComponents(PathComponent components)
        {
            var builder = new StringBuilder();

            if ((components & PathComponent.Drive) == PathComponent.Drive)
            {
                builder.Append(Drive);
            }
            if ((components & PathComponent.Root) == PathComponent.Root)
            {
                builder.Append(Root);
            }
            string path = null;
            if ((components & PathComponent.Dirname) == PathComponent.Dirname)
            {
                path = Dirname;
            }
            if ((components & PathComponent.Basename) == PathComponent.Basename
                && Basename != String.Empty)
            {
                path = !String.IsNullOrEmpty(path)
                    ? PathUtils.Combine(path, Basename, PathSeparator)
                    : Basename;
            }
            if ((components & PathComponent.Extension) == PathComponent.Extension)
            {
                path += Extension;
            }
            if (path != null)
            {
                builder.Append(path);
            }
            return builder.ToString();
        }

        private string _cachedToString;
        /// <inheritdoc/>
        public override string ToString()
        {
            if (_cachedToString == null)
            {
                _cachedToString = Drive + Root +
                    (Tail.Count > 0
                        ? String.Join(PathSeparator, Tail)
                        : "");
            }
            return _cachedToString;
        }

        /// <inheritdoc/>
        protected abstract string PathSeparator { get; }

        /// <summary>
        /// Allows comparisons between components to be made regardless of
        /// current filesystem rules.
        /// </summary>
        protected abstract StringComparer ComponentComparer { get; }

        /// <inheritdoc/>
        public bool IsAbsolute()
        {
            return !String.IsNullOrEmpty(Root);
        }

        #region Equality Members

        #endregion


        /// <inheritdoc/>
        public abstract bool IsReserved();

        /// <inheritdoc/>
        public abstract bool Match(string pattern);

        /// <inheritdoc/>
        public TPath NormCase()
        {
            return NormCase(CultureInfo.CurrentCulture);
        }

        /// <inheritdoc/>
        public abstract TPath NormCase(CultureInfo currentCulture);

        IPurePath IPurePath.NormCase(CultureInfo currentCulture)
        {
            return NormCase(currentCulture);
        }

        IPurePath IPurePath.NormCase()
        {
            return NormCase(CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Create an instance of your own IPurePath implementation
        /// when given the path to use.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected abstract TPath PurePathFactory(string path);

        /// <inheritdoc/>
        protected TPath PurePathFactoryFromComponents(
            IPurePath original,
            string drive = null,
            string root = null,
            string tail = null)
        {
            if (tail != null)
            {
                // tail parameter provided as a string - split into parts
                var tailParts = tail.Split(new[] { PathSeparator[0] },
                    StringSplitOptions.RemoveEmptyEntries);
                return PurePathFactoryFromComponents(
                    drive ?? (original != null ? original.Drive : ""),
                    root ?? (original != null ? original.Root : ""),
                    tailParts.ToImmutableList());
            }

            // No explicit tail string - use the original's tail
            var origTail = original != null
                ? (original.Tail as ImmutableList<string>) ?? original.Tail.ToImmutableList()
                : ImmutableList<string>.Empty;
            return PurePathFactoryFromComponents(
                drive ?? (original != null ? original.Drive : ""),
                root ?? (original != null ? original.Root : ""),
                origTail);
        }

        /// <summary>
        /// Overload that accepts a component override via string.
        /// </summary>
        protected TPath PurePathFactoryFromComponents(
            IPurePath original,
            ImmutableList<string> tail)
        {
            return PurePathFactoryFromComponents(
                original?.Drive ?? "",
                original?.Root ?? "",
                tail);
        }

        /// <summary>
        /// Create a path from components with optional overrides.
        /// drive/root override, tail is a pre-built list.
        /// </summary>
        protected TPath PurePathFactoryFromComponents(
            IPurePath original,
            string drive,
            string root,
            ImmutableList<string> tail)
        {
            return PurePathFactoryFromComponents(
                drive ?? (original?.Drive ?? ""),
                root ?? (original?.Root ?? ""),
                tail);
        }

        /// <inheritdoc/>
        protected abstract TPath PurePathFactoryFromComponents(
            string drive,
            string root,
            ImmutableList<string> tail);

        /// <inheritdoc/>
        public TPath Relative()
        {
            return PurePathFactoryFromComponents("", "", Tail);
        }

        IPurePath IPurePath.Relative()
        {
            return Relative();
        }

        #region Xml Serialization

        /// <inheritdoc/>
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        /// <inheritdoc/>
        public virtual void ReadXml(System.Xml.XmlReader reader)
        {
            var path = PurePathFactory(reader.ReadString());
            reader.ReadEndElement();

            Assimilate(path);
        }

        /// <inheritdoc/>
        public void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteString(ToString());
        }

        #endregion
    }
}
