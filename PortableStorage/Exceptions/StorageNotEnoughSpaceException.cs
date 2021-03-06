﻿using System;

namespace PortableStorage.Exceptions
{
    public class StorageNotEnoughSpaceException : Exception
    {
        public long MinSpace { get; private set; }
        public StorageNotEnoughSpaceException(long minSpace)
            : base($"Free space is too low. It must be more than: {minSpace / 1000000} MB!")
        {
            MinSpace = minSpace;
        }

        public StorageNotEnoughSpaceException()
        {
        }

        public StorageNotEnoughSpaceException(string message) : base(message)
        {
        }

        public StorageNotEnoughSpaceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

}
