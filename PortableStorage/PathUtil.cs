
namespace PortableStorage
{
    public class PathUtil
    {
        public static string RemoveLastSeparator(string path)
        {
            var path2 = path.Replace('\\', '/');
            if (path2.Length > 0 && path2[path2.Length - 1] == '/')
                return path.Substring(0, path.Length - 1);
            return path;
        }

        public static string AddLastSeparator(string path, char directorySeparatorChar)
        {
            if (path.Length > 0 && path[path.Length - 1] != directorySeparatorChar)
                path += directorySeparatorChar.ToString();
            return path;
        }
    }
}
