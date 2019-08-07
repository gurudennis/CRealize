namespace CRealize
{
    using System;
    using System.Linq;
    using System.Reflection;

    public static class Convert
    {
        public static string ToJSON(object obj, bool pretty = false)
        {
            return GetJSONSerializer().SerializeToString(obj, pretty);
        }

        public static string ToJSONAs<T>(T obj, bool pretty = false)
        {
            return GetJSONSerializer().SerializeToString(obj, pretty, typeof(T));
        }

        public static T FromJSON<T>(string str)
        {
            return GetJSONSerializer().Deserialize<T>(str);
        }

        public static object FromJSON(string str, Type type)
        {
            MethodInfo method = typeof(Serializer)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Deserialize" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string));

            return method.MakeGenericMethod(type).Invoke(GetJSONSerializer(), new object[] { str });
        }

        public static string PrettifyJSON(string json)
        {
            return Formats.JSON.Prettify(json);
        }

        private static Serializer GetJSONSerializer()
        {
            if (_jsonSerializer == null)
            {
                lock (_guard)
                {
                    if (_jsonSerializer == null)
                        _jsonSerializer = new Serializer(Format.JSON);
                }
            }

            return _jsonSerializer;
        }

        private static readonly object _guard = new object();
        private static Serializer _jsonSerializer;
    }
}
