namespace CRealize.Formats
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal interface IPrototype
    {
        void SetChild(string name, IPrototype value);

        void SetChild(string name, IEnumerable values, Type type = null);

        void SetChild(string name, object value, Type type = null);

        bool GetChild(string name, out object value);

        string GetTypeName();

        bool SetTypeName(string typeName, IList<string> knownChildNames);
    }

    internal interface IFormat
    {
        bool IsBinary { get; }

        IPrototype CreatePrototype();

        object SerializePrototype(IPrototype proto, bool pretty);

        IPrototype DeserializePrototype(object input);
    }

    internal static class Factory
    {
        public static IFormat CreateFormat(EnumConverter enumConverter, Format format)
        {
            if (format == Format.JSON)
                return new JSON(enumConverter);

            return null;
        }
    }
}
