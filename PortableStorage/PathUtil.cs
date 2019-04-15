
namespace PortableStorage
{
    public class PathUtil
    {
        public static string RemoveLastSeparator(string path)
        {
            return path.TrimEnd(Storage.SeparatorChar);

        }

        public static string AddLastSeparator(string path)
        {
            return path.TrimEnd(Storage.SeparatorChar) + '/';
        }
    }
}
