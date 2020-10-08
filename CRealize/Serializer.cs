namespace CRealize
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;

    public enum Format
    {
        JSON
    }

    public sealed class ForceSerializeAttribute : Attribute
    {
    }

    public sealed class SerializeAsStringAttribute : Attribute
    {
    }

    public class Serializer
    {
        public Serializer(Format format)
        {
            _enumConverter = new EnumConverter();
            _format = Formats.Factory.CreateFormat(_enumConverter, format);
            if (_format == null)
                throw new ApplicationException($"Failed to recognize format {format}");

            _reflector = new Reflection.Reflector();
        }

        public string SerializeToString(object obj, bool pretty = false, Type asType = null)
        {
            object resultObj = SerializeInternal(obj, pretty, asType);
            if (resultObj == null)
                return null;

            if (!_format.IsBinary)
                return resultObj as string;

            return System.Convert.ToBase64String(resultObj as byte[]);
        }

        public byte[] SerializeToBuffer(object obj, Type asType = null)
        {
            object resultObj = SerializeInternal(obj, false, asType);
            if (resultObj == null)
                return null;

            if (_format.IsBinary)
                return resultObj as byte[];

            return Encoding.UTF8.GetBytes(resultObj as string);
        }

        public T Deserialize<T>(byte[] buf)
        {
            object input = buf;
            if (!_format.IsBinary)
                input = Encoding.UTF8.GetString(buf);

            return DeserializeInternal<T>(input);
        }

        public T Deserialize<T>(string str)
        {
            object input = str;
            if (_format.IsBinary)
                input = System.Convert.FromBase64String(str);

            return DeserializeInternal<T>(input);
        }

        private object SerializeInternal(object obj, bool pretty, Type asType)
        {
            if (obj == null)
                return null;

            Formats.IPrototype proto = BuildPrototype(obj, asType, 0);
            if (proto == null)
                return null;

            return _format.SerializePrototype(proto, pretty);
        }

        private Formats.IPrototype BuildPrototype(object obj, Type nominalType, int depth)
        {
            if (obj == null || depth > MaxDepth)
                return null;

            Formats.IPrototype proto = _format.CreatePrototype();
            if (proto == null)
                return null;

            IList<Tuple<MemberInfo, object>> values = _reflector.GetSerializableValues(obj);
            foreach (Tuple<MemberInfo, object> value in values)
            {
                if (string.IsNullOrEmpty(value.Item1.Name))
                    continue;

                Type nominalMemberType = _reflector.GetNominalMemberType(value.Item1);

                object valueObj = BuildValue(value.Item2, nominalMemberType, depth + 1);
                if (valueObj == null)
                    continue;

                proto.SetChild(value.Item1.Name, valueObj);
            }

            Type actualType = obj.GetType();
            if (nominalType != null && nominalType != actualType)
            {
                string actualTypeName = GetPolymorphicTypeName(actualType, nominalType);
                if (!string.IsNullOrEmpty(actualTypeName))
                    proto.SetTypeName(actualTypeName, values.Select((v) => v.Item1.Name).ToList());
            }

            return proto;
        }

        private IEnumerable BuildArray(IEnumerable objs, int depth)
        {
            if (objs == null || depth > MaxDepth)
                return null;

            Type nominalUnderlyingType = _reflector.GetUnderlyingEnumerableType(objs.GetType());

            ArrayList arr = new ArrayList();

            foreach (object obj in objs)
            {
                object valueObj = BuildValue(obj, nominalUnderlyingType, depth);
                if (valueObj == null)
                    continue;

                arr.Add(valueObj);
            }

            return arr;
        }

        private object BuildValue(object obj, Type nominalType, int depth)
        {
            if (obj == null || depth > MaxDepth)
                return null;

            Type type = obj.GetType();
            if (type == typeof(DateTime))
            {
                return ((DateTime)obj).ToUniversalTime().Ticks;
            }
            else if (type == typeof(Guid))
            {
                return ((Guid)obj).ToString();
            }
            else if (type == typeof(IPAddress))
            {
                return ((IPAddress)obj).ToString();
            }
            else if (type == typeof(Version))
            {
                return ((Version)obj).ToString();
            }
            else if (type.GetCustomAttribute<SerializeAsStringAttribute>() != null)
            {
                return obj.ToString();
            }
            else if (!_reflector.IsBasic(type))
            {
                if (obj is IEnumerable)
                    return BuildArray(obj as IEnumerable, depth);
                else if (type.IsClass || type.IsInterface || type.IsValueType)
                    return BuildPrototype(obj, nominalType, depth);
            }

            return obj;
        }

        public T DeserializeInternal<T>(object input)
        {
            Formats.IPrototype proto = _format.DeserializePrototype(input);
            if (proto == null)
                return default(T);

            object result = BindPrototype(proto, typeof(T), 0);
            if (result == null)
                return default(T);

            return (T)result;
        }

        private object BindPrototype(Formats.IPrototype proto, Type type, int depth)
        {
            if (proto == null || type == null || depth > MaxDepth)
                return null;

            string actualTypeName = proto.GetTypeName();
            if (!string.IsNullOrEmpty(actualTypeName))
            {
                Type actualType = GetPolymorphicType(actualTypeName);
                if (actualType != null && _reflector.IsCompatibleType(actualType, type))
                    type = actualType;
            }

            IList<Tuple<MemberInfo, Type, object>> memberValues = new List<Tuple<MemberInfo, Type, object>>();

            IList<MemberInfo> members = _reflector.GetSerializableMembers(type);
            if (members != null && members.Count > 0)
            {
                foreach (MemberInfo member in members)
                {
                    if (!proto.GetChild(member.Name, out object memberValue))
                        continue;

                    Type nominalMemberType = _reflector.GetNominalMemberType(member);
                    if (nominalMemberType == null)
                        continue;

                    memberValue = BindValue(memberValue, nominalMemberType, depth + 1);

                    memberValues.Add(Tuple.Create(member, nominalMemberType, memberValue));
                }
            }

            return CreateInstance(type, memberValues);
        }

        private IEnumerable BindArray(IEnumerable objs, Type type, int depth)
        {
            if (objs == null || depth > MaxDepth)
                return null;

            Type underlyingType = _reflector.GetUnderlyingEnumerableType(type);
            if (underlyingType == null)
                return null;

            IList arr = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(underlyingType));

            foreach (object obj in objs)
            {
                object valueObj = BindValue(obj, underlyingType, depth);
                if (valueObj == null)
                    continue;

                arr.Add(valueObj);
            }

            return CreateEnumerable(type, underlyingType, arr);
        }

        private object BindValue(object obj, Type type, int depth)
        {
            if (obj == null || depth > MaxDepth)
                return null;

            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(DateTime))
            {
                return new DateTime((long)(_reflector.ConvertValue(obj, typeof(long)) ?? (long)0), DateTimeKind.Utc);
            }
            else if (type == typeof(Guid))
            {
                return Guid.TryParse(obj as string ?? string.Empty, out Guid g) ? g : Guid.Empty;
            }
            else if (type == typeof(IPAddress))
            {
                return IPAddress.TryParse(obj as string ?? string.Empty, out IPAddress ip) ? ip : null;
            }
            else if (type == typeof(Version))
            {
                return Version.TryParse(obj as string ?? string.Empty, out Version ver) ? ver : null;
            }
            else if (type.GetCustomAttribute<SerializeAsStringAttribute>() != null && obj is string)
            {
                return Activator.CreateInstance(type, obj);
            }
            else if (_reflector.IsBasic(type))
            {
                if (type.IsEnum)
                {
                    try
                    {
                        return _enumConverter.StringToEnum(type, obj as string);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
                else if (obj != null && obj.GetType() != type)
                {
                    return _reflector.ConvertValue(obj, type);
                }
            }
            else
            {
                if (obj is IEnumerable)
                    return BindArray(obj as IEnumerable, type, depth);
                else if (obj is Formats.IPrototype)
                    return BindPrototype(obj as Formats.IPrototype, type, depth);
            }

            return obj;
        }

        private string GetPolymorphicTypeName(Type actualType, Type nominalType)
        {
            if (actualType == null || nominalType == null)
                return null;

            // TODO: Support smarter relative type names where possible.

            return $"{actualType.Assembly.GetName().Name}/{actualType.FullName}";
        }

        private Type GetPolymorphicType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // TODO: Support smarter relative type names where possible.

            int separatorPos = typeName.IndexOf('/');
            if (separatorPos < 0 || separatorPos == typeName.Length - 1)
                return null;

            string shortAssemblyName = typeName.Substring(0, separatorPos);
            string shortNamespaceAndTypeName = typeName.Substring(separatorPos + 1);

#pragma warning disable CS0618 // Type or member is obsolete
            Assembly a = Assembly.LoadWithPartialName(shortAssemblyName);
#pragma warning restore CS0618 // Type or member is obsolete

            if (a == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == shortAssemblyName)
                    {
                        a = assembly;
                        break;
                    }
                }
            }

            if (a == null)
                return null;

            return a.GetType(shortNamespaceAndTypeName);
        }

        private object CreateInstance(Type type, IList<Tuple<MemberInfo, Type, object>> memberValues)
        {
            if (_reflector.IsTuple(type))
            {
                if (memberValues == null)
                    return null;

                object[] parameters = new object[type.GenericTypeArguments.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    var memberValue = memberValues.FirstOrDefault((m) => m.Item1.Name == $"Item{i + 1}");
                    if (memberValue != null)
                        parameters[i] = _reflector.ConvertValue(memberValue.Item3, memberValue.Item2);
                }

                return Activator.CreateInstance(type, parameters);
            }
            else if (_reflector.IsKeyValuePair(type))
            {
                if (memberValues == null)
                    return null;

                object[] parameters = new object[2];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    string paramName = i == 0 ? "Key" : "Value";
                    var memberValue = memberValues.FirstOrDefault((m) => m.Item1.Name == paramName);
                    if (memberValue != null)
                        parameters[i] = _reflector.ConvertValue(memberValue.Item3, memberValue.Item2);
                }

                return Activator.CreateInstance(type, parameters);
            }

            object result = Activator.CreateInstance(type);
            if (result == null)
                return null;

            if (memberValues != null)
            {
                for (int i = 0; i < memberValues.Count; ++i)
                    _reflector.SetSerializableValue(result, memberValues[i].Item1, memberValues[i].Item3, memberValues[i].Item2);
            }

            return result;
        }

        private IEnumerable CreateEnumerable(Type type, Type underlyingType, IList values)
        {
            if (_reflector.IsGenericList(type) || _reflector.IsGenericCollection(type) || _reflector.IsGenericIEnumerable(type))
                return values; // optimization: the input happens to have the same type as the output

            if (type.IsArray)
            {
                object result = Activator.CreateInstance(type, (object)values.Count);
                if (result == null)
                    return null;

                MethodInfo set = type.GetMethod("Set", new Type[] { typeof(int), underlyingType });
                if (set == null)
                    return null;

                for (int i = 0; i < values.Count; ++i)
                    set.Invoke(result, new object[] { i, values[i] });

                return result as IEnumerable;
            }

            {
                Type collectionType = typeof(ICollection<>).MakeGenericType(underlyingType);
                if (_reflector.ImplementsInterface(type, collectionType))
                {
                    object result = null;
                    if (_reflector.IsGenericDictionary(type))
                        result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(_reflector.GetUnderlyingDictionaryTypes(type)));
                    else
                        result = Activator.CreateInstance(type);

                    if (result == null)
                        return null;

                    MethodInfo add = collectionType.GetMethod("Add", new Type[] { underlyingType });
                    if (add == null)
                        return null;

                    foreach (object value in values)
                        add.Invoke(result, new object[] { value });

                    return result as IEnumerable;
                }
            }

            return null;
        }

        private static readonly int MaxDepth = 64;

        private readonly Formats.IFormat _format;
        private readonly Reflection.Reflector _reflector;
        private readonly EnumConverter _enumConverter;
    }
}
