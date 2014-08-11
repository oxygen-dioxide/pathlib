﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using PathLib.Utils;

namespace PathLib
{
    /// <summary>
    /// Base class for common methods in concrete paths.
    /// </summary>
    public abstract class ConcretePath<TPath, TPurePath> : 
        IPath<TPath>, 
        IPurePath<TPurePath> 
        where TPath : IPath
        where TPurePath : IPurePath<TPurePath>
    {
        /// <inheritdoc/>
        public readonly TPurePath PurePath;

        /// <inheritdoc/>
        protected ConcretePath(TPurePath purePath)
        {
            PurePath = purePath;
        }

        /// <inheritdoc/>
        public IEnumerable<TPath> ListDir(string pattern)
        {
            if (!IsDir())
            {
                throw new ArgumentException("Glob may only be called on directories.");
            }
            foreach (var path in ListDir())
            {
                if (PathUtils.Glob(pattern, path.ToString()))
                {
                    yield return path;
                }
            }
        }

        IEnumerable<IPath> IPath.ListDir(string pattern)
        {
            return LinqBridge.Select(ListDir(pattern), p => (IPath)p);
        }

        /// <inheritdoc/>
        protected abstract StatInfo Stat(bool flushCache);

        /// <inheritdoc/>
        public StatInfo Stat()
        {
            return Stat(false);
        }

        /// <inheritdoc/>
        public StatInfo Restat()
        {
            return Stat(true);
        }

        /// <inheritdoc/>
        public void Chmod(int mode)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool Exists()
        {
            return IsDir() || IsFile();
        }

        /// <inheritdoc/>
        public bool IsFile()
        {
            return File.Exists(PurePath.ToPosix());
        }

        /// <inheritdoc/>
        public bool IsDir()
        {
            return System.IO.Directory.Exists(PurePath.ToPosix());
        }

        /// <inheritdoc/>
        public IEnumerable<TPath> ListDir()
        {
            if (!IsDir())
            {
                throw new ConstraintException("Path object must be a directory.");
            }
            foreach (var directory in System.IO.Directory.GetFileSystemEntries(PurePath.ToString()))
            {
                yield return PathFactory(PurePath.Filename, directory);
            }
        }

        /// <inheritdoc/>
        IEnumerable<IPath> IPath.ListDir()
        {
            return LinqBridge.Select(ListDir(), p => (IPath)p);
        }

        /// <inheritdoc/>
        protected abstract TPath PathFactory(params string[] path);

        /// <inheritdoc/>
        protected abstract TPath PathFactory(TPurePath path);

        /// <inheritdoc/>
        public abstract TPath Resolve();

        IPath IPath.Resolve()
        {
            return Resolve();
        }

        /// <inheritdoc/>
        public abstract bool IsSymlink();

        /// <inheritdoc/>
        public void Lchmod(int mode)
        {
            Resolve().Chmod(mode);
        }

        /// <inheritdoc/>
        public StatInfo Lstat()
        {
            return Resolve().Stat();
        }

        /// <inheritdoc/>
        public void Mkdir(bool makeParents = false)
        {
            // Iteratively check whether or not each directory in the path exists
            // and create them if they do not.
            if (makeParents)
            {
                foreach (var dir in Parents())
                {
                    if(!dir.IsDir())
                    {
                        System.IO.Directory.CreateDirectory(dir.ToString());
                    }
                }
            }
            if (!IsDir())
            {
                System.IO.Directory.CreateDirectory(PurePath.ToPosix());
            }
        }

        /// <inheritdoc/>
        public Stream Open(FileMode mode)
        {
            return File.Open(PurePath.ToString(), mode);
        }

        /// <inheritdoc/>
        public abstract TPath ExpandUser();

        IPath IPath.ExpandUser()
        {
            return ExpandUser();
        }

        /// <inheritdoc/>
        public abstract IDisposable SetCurrentDirectory();


        #region IPurePath implementation

        /// <inheritdoc/>
        public string Dirname
        {
            get { return PurePath.Dirname; }
        }

        /// <inheritdoc/>
        public string Directory
        {
            get { return PurePath.Directory; }
        }

        /// <inheritdoc/>
        public string Filename
        {
            get { return PurePath.Filename; }
        }

        /// <inheritdoc/>
        public string Basename
        {
            get { return PurePath.Basename; }
        }

        /// <inheritdoc/>
        public string Extension
        {
            get { return PurePath.Extension; }
        }

        /// <inheritdoc/>
        public string[] Extensions
        {
            get { return PurePath.Extensions; }
        }

        /// <inheritdoc/>
        public string Root
        {
            get { return PurePath.Root; }
        }

        /// <inheritdoc/>
        public string Drive
        {
            get { return PurePath.Drive; }
        }

        /// <inheritdoc/>
        public string Anchor
        {
            get { return PurePath.Anchor; }
        }

        /// <inheritdoc/>
        public string ToPosix()
        {
            return PurePath.ToPosix();
        }

        /// <inheritdoc/>
        public bool IsAbsolute()
        {
            return PurePath.IsAbsolute();
        }

        /// <inheritdoc/>
        public bool IsReserved()
        {
            return PurePath.IsReserved();
        }

        /// <inheritdoc/>
        IPurePath IPurePath.Join(params string[] paths)
        {
            return PurePath.Join(paths);
        }

        /// <inheritdoc/>
        IPurePath IPurePath.Join(params IPurePath[] paths)
        {
            return PurePath.Join(paths);
        }

        /// <inheritdoc/>
        bool IPurePath.TrySafeJoin(string relativePath, out IPurePath joined)
        {
            return PurePath.TrySafeJoin(relativePath, out joined);
        }

        /// <inheritdoc/>
        bool IPurePath.TrySafeJoin(IPurePath relativePath, out IPurePath joined)
        {
            return PurePath.TrySafeJoin(relativePath, out joined);
        }

        /// <inheritdoc/>
        public bool Match(string pattern)
        {
            return PurePath.Match(pattern);
        }

        /// <inheritdoc/>
        public IEnumerable<string> Parts
        {
            get { return PurePath.Parts; }
        }

        /// <inheritdoc/>
        IPurePath IPurePath.NormCase()
        {
            return PurePath.NormCase();
        }

        /// <inheritdoc/>
        IPurePath IPurePath.NormCase(CultureInfo currentCulture)
        {
            return PurePath.NormCase(currentCulture);
        }

        /// <inheritdoc/>
        IPurePath IPurePath.Parent()
        {
            return PurePath.Parent();
        }

        /// <inheritdoc/>
        IPurePath IPurePath.Parent(int nthParent)
        {
            return PurePath.Parent(nthParent);
        }

        /// <inheritdoc/>
        IEnumerable<IPurePath> IPurePath.Parents()
        {
            return LinqBridge.Select(PurePath.Parents(), p => (IPurePath)p);
        }

        /// <inheritdoc/>
        public Uri ToUri()
        {
            return PurePath.ToUri();
        }

        /// <inheritdoc/>
        IPurePath IPurePath.Relative()
        {
            return PurePath.Relative();
        }

        /// <inheritdoc/>
        IPurePath IPurePath.RelativeTo(IPurePath parent)
        {
            return PurePath.RelativeTo(parent);
        }

        /// <inheritdoc/>
        IPurePath IPurePath.WithDirname(string newDirName)
        {
            return PurePath.WithDirname(newDirName);
        }

        /// <inheritdoc/>
        IPurePath IPurePath.WithFilename(string newFilename)
        {
            return PurePath.WithFilename(newFilename);
        }

        /// <inheritdoc/>
        IPurePath IPurePath.WithExtension(string newExtension)
        {
            return PurePath.WithExtension(newExtension);
        }

        /// <inheritdoc/>
        public bool HasComponents(PathComponent components)
        {
            return PurePath.HasComponents(components);
        }

        /// <inheritdoc/>
        public string GetComponents(PathComponent components)
        {
            return PurePath.GetComponents(components);
        }

        #endregion


        #region TPurePath implementation

        /// <inheritdoc/>
        public bool TrySafeJoin(string path, out TPurePath joined)
        {
            return PurePath.TrySafeJoin(path, out joined);
        }

        /// <inheritdoc/>
        public bool TrySafeJoin(IPurePath path, out TPurePath joined)
        {
            return PurePath.TrySafeJoin(path, out joined);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.Join(params string[] paths)
        {
            return PurePath.Join(paths);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.Join(params IPurePath[] paths)
        {
            return PurePath.Join(paths);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.NormCase()
        {
            return PurePath.NormCase();
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.NormCase(CultureInfo currentCulture)
        {
            return PurePath.NormCase(currentCulture);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.Parent()
        {
            return PurePath.Parent();
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.Parent(int nthParent)
        {
            return PurePath.Parent(nthParent);
        }

        /// <inheritdoc/>
        IEnumerable<TPurePath> IPurePath<TPurePath>.Parents()
        {
            return PurePath.Parents();
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.Relative()
        {
            return PurePath.Relative();
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.RelativeTo(IPurePath parent)
        {
            return PurePath.RelativeTo(parent);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.WithDirname(string newDirName)
        {
            return PurePath.WithDirname(newDirName);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.WithFilename(string newFilename)
        {
            return PurePath.WithFilename(newFilename);
        }

        /// <inheritdoc/>
        TPurePath IPurePath<TPurePath>.WithExtension(string newExtension)
        {
            return PurePath.WithExtension(newExtension);
        }

        #endregion


        #region TPath implementation

        /// <inheritdoc/>
        public TPath Join(params string[] paths)
        {
            return PathFactory(PurePath.Join(paths));
        }

        /// <inheritdoc/>
        public TPath Join(params IPurePath[] paths)
        {
            return PathFactory(PurePath.Join(paths));
        }

        /// <inheritdoc/>
        public TPath NormCase()
        {
            return PathFactory(PurePath.NormCase());
        }

        /// <inheritdoc/>
        public TPath NormCase(CultureInfo currentCulture)
        {
            return PathFactory(PurePath.NormCase(currentCulture));
        }

        /// <inheritdoc/>
        public TPath Parent()
        {
            return PathFactory(PurePath.Parent());
        }

        /// <inheritdoc/>
        public TPath Parent(int nthParent)
        {
            return PathFactory(PurePath.Parent(nthParent));
        }

        /// <inheritdoc/>
        public IEnumerable<TPath> Parents()
        {
            return LinqBridge.Select(PurePath.Parents(), PathFactory);
        }

        /// <inheritdoc/>
        public TPath Relative()
        {
            return PathFactory(PurePath.Relative());
        }

        /// <inheritdoc/>
        public TPath RelativeTo(IPurePath parent)
        {
            return PathFactory(PurePath.RelativeTo(parent));
        }

        /// <inheritdoc/>
        public TPath WithDirname(string newDirName)
        {
            return PathFactory(PurePath.WithDirname(newDirName));
        }

        /// <inheritdoc/>
        public TPath WithFilename(string newFilename)
        {
            return PathFactory(PurePath.WithFilename(newFilename));
        }

        /// <inheritdoc/>
        public TPath WithExtension(string newExtension)
        {
            return PathFactory(PurePath.WithExtension(newExtension));
        }

        #endregion
    }
}
