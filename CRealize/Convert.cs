namespace CRealize
{
    using System;

    public static class Convert
    {
        public static string ToJSON(object obj, bool pretty = false)
        {
            return GetJSONSerializer().SerializeToString(obj, pretty);
        }

        public static string ToJSONAs<T>(object obj, bool pretty = false)
        {
            return GetJSONSerializer().SerializeToString(obj, pretty, typeof(T));
        }

        public static T FromJSON<T>(string str) where T : new()
        {
            return GetJSONSerializer().Deserialize<T>(str);
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

        private static object _guard = new object();
        private static Serializer _jsonSerializer;
    }
}
