using System;

namespace PortableStorage
{
    [Flags]
    public enum StreamAttributes
    {
        Hidden      = 0x001,
        System      = 0x002
    }
}
