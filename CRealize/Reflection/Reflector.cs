namespace CRealize.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal class Reflector
    {
        public Reflector()
        {
        }

        public IList<MemberInfo> GetSerializableMembers(Type type)
        {
            List<MemberInfo> members = new List<MemberInfo>();

            if (type == null)
                return members;

            BindingFlags binding = BindingFlags.Instance | BindingFlags.Public;

            members.AddRange(type.GetProperties(binding).Where((p) => IsSerializableProperty(type, p, true)));
            members.AddRange(type.GetFields(binding));

            return members;
        }

        public IList<Tuple<MemberInfo, object>> GetSerializableValues(object obj)
        {
            List<Tuple<MemberInfo, object>> members = new List<Tuple<MemberInfo, object>>();

            if (obj == null)
                return members;

            Type type = obj.GetType();

            BindingFlags binding = BindingFlags.Instance | BindingFlags.Public;

            IEnumerable<PropertyInfo> props = type.GetProperties(binding).Where((p) => IsSerializableProperty(type, p, false));
            foreach (PropertyInfo prop in props)
            {
                try
                {
                    members.Add(Tuple.Create<MemberInfo, object>(prop, prop.GetValue(obj)));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Invalid value {prop.PropertyType.Name} {prop.Name} caused exception {ex.GetType().Name} {ex.Message}");
                }
            }

            FieldInfo[] fields = type.GetFields(binding);
            foreach (FieldInfo field in fields)
            {
                try
                {
                    members.Add(Tuple.Create<MemberInfo, object>(field, field.GetValue(obj)));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Invalid value {field.FieldType.Name} {field.Name} caused exception {ex.GetType().Name} {ex.Message}");
                }
            }

            return members;
        }

        public Type GetNominalMemberType(MemberInfo member)
        {
            if (member == null)
                return null;

            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                    return property.PropertyType;
            }

            {
                FieldInfo field = member as FieldInfo;
                if (field != null)
                    return field.FieldType;
            }

            return null;
        }

        public object ConvertValue(object value, Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                type = underlying;

            if (IsBasic(type))
            {
                try
                {
                    value = Convert.ChangeType(value, type);
                }
                catch (Exception)
                {
                }
            }

            return value;
        }

        public bool SetSerializableValue(object obj, MemberInfo member, object value, Type nominalMemberType = null)
        {
            if (obj == null || member == null)
                return false;

            if (nominalMemberType == null)
                nominalMemberType = GetNominalMemberType(member);

            value = ConvertValue(value, nominalMemberType);

            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    try
                    {
                        property.SetValue(obj, value);
                    }
                    catch (Exception)
                    {
                        return false;
                    }

                    return true;
                }
            }

            {
                FieldInfo field = member as FieldInfo;
                if (field != null)
                {
                    try
                    {
                        field.SetValue(obj, value);
                    }
                    catch (Exception)
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        public bool IsProperty(MemberInfo member)
        {
            return (member.MemberType & MemberTypes.Property) != 0;
        }

        public bool IsVariable(MemberInfo member)
        {
            return (member.MemberType & MemberTypes.Field) != 0;
        }

        public bool IsBasic(Type type)
        {
            return (type == typeof(string) || type.IsEnum || type.IsPrimitive);
        }

        public bool IsTuple(Type type)
        {
            return type.IsConstructedGenericType && type.Name.StartsWith("Tuple`");
        }

        public bool IsKeyValuePair(Type type)
        {
            return type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
        }

        public bool IsGenericList(Type type)
        {
            if (!type.IsConstructedGenericType)
                return false;

            Type genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(IList<>) || genericType == typeof(List<>);
        }

        public bool IsGenericCollection(Type type)
        {
            if (!type.IsConstructedGenericType)
                return false;

            Type genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(ICollection<>) || genericType == typeof(IReadOnlyCollection<>);
        }

        public bool IsGenericDictionary(Type type)
        {
            if (!type.IsConstructedGenericType)
                return false;

            Type genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(IDictionary<,>) || genericType == typeof(Dictionary<,>);
        }

        public bool IsGenericIEnumerable(Type type)
        {
            if (!type.IsConstructedGenericType)
                return false;

            Type genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(IEnumerable<>);
        }

        public bool ImplementsInterface(Type type, Type iface)
        {
            return type == iface || type.FindInterfaces((t, c) => t == iface, null).Any();
        }

        public bool IsCompatibleType(Type derivedType, Type baseType)
        {
            if (derivedType == null || baseType == null)
                return false;

            return derivedType == baseType || baseType.IsAssignableFrom(derivedType);
        }

        public Type GetUnderlyingEnumerableType(Type type)
        {
            List<Type> interfaceTypes = type.GetInterfaces().ToList();
            if (type.IsInterface)
                interfaceTypes.Add(type);

            return interfaceTypes.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                      ?.FirstOrDefault()
                      ?.GetGenericArguments()
                      ?.FirstOrDefault();
        }

        public Type[] GetUnderlyingDictionaryTypes(Type type)
        {
            List<Type> interfaceTypes = type.GetInterfaces().ToList();
            if (type.IsInterface)
                interfaceTypes.Add(type);

            return interfaceTypes.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                      ?.FirstOrDefault()
                      ?.GetGenericArguments()
                      ?.ToArray();
        }

        private bool IsSerializableProperty(Type type, PropertyInfo property, bool requireWriteable)
        {
            // All Tuple and KeyValuePair properties; all publicly writable properties for other types;
            // also all publicly readable properties with attribute ForceSerialize.
            return IsTuple(type) || IsKeyValuePair(type) ||
                   (property.CanWrite && (property.SetMethod?.IsPublic ?? false)) ||
                   (!requireWriteable && property.CanRead && (property.GetMethod?.IsPublic ?? false) &&
                    property.GetCustomAttribute<ForceSerializeAttribute>() != null);
        }
    }
}
