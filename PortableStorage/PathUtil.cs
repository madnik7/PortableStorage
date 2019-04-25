
namespace PortableStorage
{
    public class PathUtil
    {
        public static string RemoveLastSeparator(string path)
        {
            path = path.Replace('\\', Storage.SeparatorChar);
            return path.TrimEnd(Storage.SeparatorChar);
        }

        public static string AddLastSeparator(string path)
        {
            path = path.Replace('\\', Storage.SeparatorChar);
            return path.TrimEnd(Storage.SeparatorChar) + '/';
        }
    }
}
