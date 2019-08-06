namespace CRealize
{
    using System;
    using System.Collections.Generic;

    // Converts enum values to strings and back.
    // This is needed because of the enum fragility problem.
    // Consider services S1, S2, S3 that communicate through an interface with property X
    // of type enum E, serializing it to JSON or XML and deserializing it from JSON or XML.
    // S1 and S3 are running version 2 of the interface where E has 3 values, V1, V2 and V3.
    // S2 is running version 1 of the interface where E has 2 values, V1 and V2.
    // If S1 calls S2 with {"X":"V3"}, and S2 deserializes it using Enum.Parse, it will fail.
    // If S2 deserializes it using this class, it will succeed.
    // If S2 serializes it using this class, it will also succeed and pass {"X":"V3"} to S3.
    public class EnumConverter
    {
        // Run built-in Enum.Parse.
        // If it fails, invent and memorize a new value for the enum that corresponds to the string.
        public object StringToEnum(Type enumType, string stringValue, bool ignoreCase = false)
        {
            try
            {
                return Enum.Parse(enumType, stringValue, ignoreCase);
            }
            catch (ArgumentException)
            {
                return ParseIntoInventedValues(enumType, stringValue, ignoreCase);
            }
        }

        // If this value was invented by a previous call to StringToEnum,
        // return the string that was passed to it during this previous call.
        // Else call the built-in Enum.ToString.
        public string EnumToString(object enumValue)
        {
            return InventedValueToString(enumValue) ?? enumValue.ToString();
        }

        private object ParseIntoInventedValues(Type enumType, string stringValue, bool ignoreCase)
        {
            lock (_guard)
            {
                Dictionary<Type, Dictionary<string, long>> typeStringToUnderlyingValue = ignoreCase
                    ? _typeCaseInsensitiveStringToUnderlyingValue
                    : _typeCaseSensitiveStringToUnderlyingValue;

                if (!typeStringToUnderlyingValue.TryGetValue(
                    enumType,
                    out Dictionary<string, long> stringToUnderlyingValue))
                {
                    stringToUnderlyingValue = new Dictionary<string, long>(
                        ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                    typeStringToUnderlyingValue.Add(enumType, stringToUnderlyingValue);
                }

                if (!stringToUnderlyingValue.TryGetValue(stringValue, out long underlyingValue))
                {
                    underlyingValue = GenerateNewUnderlyingValue(enumType);
                    stringToUnderlyingValue.Add(
                        stringValue,
                        underlyingValue);
                }

                if (!_typeUnderlyingValueToString.TryGetValue(
                    enumType,
                    out Dictionary<long, string> underlyingValueToString))
                {
                    underlyingValueToString = new Dictionary<long, string>();
                    _typeUnderlyingValueToString.Add(enumType, underlyingValueToString);
                }

                if (!underlyingValueToString.ContainsKey(underlyingValue))
                    underlyingValueToString.Add(underlyingValue, stringValue);

                return Enum.ToObject(enumType, underlyingValue);
            }
        }

        private string InventedValueToString(object enumValue)
        {
            lock (_guard)
            {
                return _typeUnderlyingValueToString.TryGetValue(
                           enumValue.GetType(),
                           out Dictionary<long, string> underlyingValueToString) &&
                       underlyingValueToString.TryGetValue(System.Convert.ToInt64(enumValue), out string stringValue)
                    ? stringValue
                    : null;
            }
        }

        private long GenerateNewUnderlyingValue(Type enumType)
        {
            Type underlyingType = Enum.GetUnderlyingType(enumType);

            if (!_typeToUnderlyingValues.TryGetValue(enumType, out long[] definedEnumValues))
            {
                Array array = Enum.GetValues(enumType);
                definedEnumValues = new long[array.Length];
                for (int i = 0; i < definedEnumValues.Length; ++i)
                {
                    object value = array.GetValue(i);
                    if (underlyingType == typeof(ulong) && (ulong)value > long.MaxValue)
                        continue;
                    definedEnumValues[i] = System.Convert.ToInt64(value);
                }

                Array.Sort(definedEnumValues);

                _typeToUnderlyingValues.Add(enumType, definedEnumValues);
            }

            _typeUnderlyingValueToString.TryGetValue(
                enumType,
                out Dictionary<long, string> underlyingValueToString);

            if (!_typeToNextUnderlyingValue.TryGetValue(enumType, out long underlyingValue))
                underlyingValue = MinValue(underlyingType);

            long maxUnderlyingValue = MaxValue(underlyingType);

            bool found = false;
            while (!found && underlyingValue <= maxUnderlyingValue)
            {
                found = Array.BinarySearch(definedEnumValues, underlyingValue) < 0 &&
                        (underlyingValueToString == null || !underlyingValueToString.ContainsKey(underlyingValue));

                if (!found)
                    ++underlyingValue;
            }

            if (!found)
                throw new InvalidOperationException(
                    "Cannot find a value not in the underlying values of " + enumType.FullName);

            if (underlyingValue == maxUnderlyingValue)
                throw new InvalidOperationException(
                    "Used up all possible underlying values of " + enumType.FullName);

            _typeToNextUnderlyingValue[enumType] = underlyingValue + 1;

            return underlyingValue;
        }

        private static long MinValue(Type underlyingType)
        {
            if (underlyingType == typeof(sbyte))
                return sbyte.MinValue;
            else if (underlyingType == typeof(byte))
                return byte.MinValue;
            else if (underlyingType == typeof(short))
                return short.MinValue;
            else if (underlyingType == typeof(ushort))
                return ushort.MinValue;
            else if (underlyingType == typeof(int))
                return int.MinValue;
            else if (underlyingType == typeof(uint))
                return uint.MinValue;
            else if (underlyingType == typeof(long))
                return long.MinValue;
            else if (underlyingType == typeof(ulong))
                return (long)ulong.MinValue;
            else
                throw new ArgumentException(nameof(underlyingType));
        }

        private static long MaxValue(Type underlyingType)
        {
            if (underlyingType == typeof(sbyte))
                return sbyte.MaxValue;
            else if (underlyingType == typeof(byte))
                return byte.MaxValue;
            else if (underlyingType == typeof(short))
                return short.MaxValue;
            else if (underlyingType == typeof(ushort))
                return ushort.MaxValue;
            else if (underlyingType == typeof(int))
                return int.MaxValue;
            else if (underlyingType == typeof(uint))
                return uint.MaxValue;
            else if (underlyingType == typeof(long))
                return long.MaxValue;
            else if (underlyingType == typeof(ulong))
                return long.MaxValue; // The difference won't matter to us.
            else
                throw new ArgumentException(nameof(underlyingType));
        }

        private readonly object _guard = new object();

        private readonly Dictionary<Type, Dictionary<long, string>> _typeUnderlyingValueToString =
            new Dictionary<Type, Dictionary<long, string>>();

        private readonly Dictionary<Type, Dictionary<string, long>> _typeCaseSensitiveStringToUnderlyingValue =
            new Dictionary<Type, Dictionary<string, long>>();

        private readonly Dictionary<Type, Dictionary<string, long>> _typeCaseInsensitiveStringToUnderlyingValue =
            new Dictionary<Type, Dictionary<string, long>>();

        private readonly Dictionary<Type, long[]> _typeToUnderlyingValues = new Dictionary<Type, long[]>();
        private readonly Dictionary<Type, long> _typeToNextUnderlyingValue = new Dictionary<Type, long>();
    }
}
