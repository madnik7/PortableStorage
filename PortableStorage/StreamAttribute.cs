using System;

namespace PortableStorage
{
    [Flags]
    public enum StreamAttribute
    {
        Hidden      = 0x001,
        System      = 0x002
    }
}
