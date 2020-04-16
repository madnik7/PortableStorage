
using System;

namespace PortableStorage
{
    public static class PathUtil
    {
        public static string RemoveLastSeparator(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            path = path.Replace('\\', Storage.SeparatorChar);
            return path.TrimEnd(Storage.SeparatorChar);
        }

        public static string AddLastSeparator(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            path = path.Replace('\\', Storage.SeparatorChar);
            return path.TrimEnd(Storage.SeparatorChar) + '/';
        }
    }
}
