namespace CRealize.Formats
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Web.Script.Serialization;

    internal class JSONPrototype : IPrototype
    {
        public JSONPrototype(Dictionary<string, object> root = null)
        {
            _children = root ?? new Dictionary<string, object>();
        }

        public void SetChild(string name, IPrototype value)
        {
            _children[TransformName(name)] = ((JSONPrototype)value).Children;
        }

        public void SetChild(string name, IEnumerable values, Type type = null)
        {
            object valueOut = MakeValue(values, type, true, 0);
            if (valueOut != null)
                _children[TransformName(name)] = valueOut;
        }

        public void SetChild(string name, object value, Type type = null)
        {
            object valueOut = MakeValue(value, type, true, 0);
            if (valueOut != null)
                _children[TransformName(name)] = valueOut;
        }

        public bool GetChild(string name, out object value)
        {
            value = null;

            name = TransformName(name);
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_children.TryGetValue(name, out object rawValue))
                return false;

            value = MakeValue(rawValue, null, false, 0);

            return true;
        }

        public string GetTypeName()
        {
            for (int i = TypeParamNames.Length - 1; i >= 0; --i)
            {
                if (_children.TryGetValue(TypeParamNames[i], out object value) && value != null && value is string)
                    return (string)value;
            }

            return null;
        }

        public bool SetTypeName(string typeName, IList<string> knownChildNames)
        {
            bool succeeded = false;

            for (int i = 0; i < TypeParamNames.Length; ++i)
            {
                if (knownChildNames.Any((v) => TransformName(v) == TypeParamNames[i]))
                    continue;

                if (_children.ContainsKey(TypeParamNames[i]))
                    continue;

                _children[TypeParamNames[i]] = typeName;

                succeeded = true;

                break;
            }

            return succeeded;
        }

        public Dictionary<string, object> Children => _children;

        private object MakeValue(object value, Type type, bool asInput, int depth)
        {
            if (value == null || depth > MaxDepth)
                return null;

            if (type == null)
                type = value.GetType();

            if (asInput)
            {
                JSONPrototype proto = value as JSONPrototype;
                if (proto != null)
                    return proto.Children;
            }
            else
            {
                Dictionary<string, object> subObj = value as Dictionary<string, object>;
                if (subObj != null)
                    return new JSONPrototype(subObj);
            }

            if (type.IsEnum)
                return value.ToString();

            if (type == typeof(string))
                return value as string;
            
            {
                IEnumerable arr = value as IEnumerable;
                if (arr != null)
                {
                    bool allTypesSame = IsIEnumerableOfT(type);
                    Type underlyingType = null;
                    ArrayList arrOut = new ArrayList();
                    foreach (object item in arr)
                    {
                        if (underlyingType == null || !allTypesSame)
                            underlyingType = item.GetType();

                        arrOut.Add(MakeValue(item, underlyingType, asInput, depth + 1));
                    }

                    return arrOut;
                }
            }

            return value;
        }

        private string TransformName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            int capCount = 0;
            for (int i = 0; i < name.Length; ++i)
            {
                if (char.ToLower(name[i]) != name[i])
                    ++capCount;
                else
                    break;
            }

            if (capCount > 1 && capCount != name.Length)
                --capCount; // presume abbreviation followed by camel-case

            StringBuilder sb = new StringBuilder(name.Length);
            for (int i = 0; i < capCount; ++i)
                sb.Append(char.ToLower(name[i]));

            sb.Append(name.Substring(capCount));

            return sb.ToString();
        }

        private bool IsIEnumerableOfT(Type type)
        {
            return type.GetInterfaces().Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        private static readonly string[] TypeParamNames = { "type", "type___" };

        private static readonly int MaxDepth = 64;

        private Dictionary<string, object> _children;
    }

    internal class JSON : IFormat
    {
        public bool IsBinary => false;

        public IPrototype CreatePrototype()
        {
            return new JSONPrototype();
        }

        public object SerializePrototype(IPrototype proto, bool pretty)
        {
            JSONPrototype jsonProto = proto as JSONPrototype;
            if (jsonProto == null)
                return null;

            string json = _js.Serialize(jsonProto.Children);
            if (json == null)
                return null;

            return pretty ? Prettify(json) : json;
        }

        public IPrototype DeserializePrototype(object input)
        {
            string inputStr = input as string;
            if (string.IsNullOrEmpty(inputStr))
                return null;

            Dictionary<string, object> root = _js.Deserialize<Dictionary<string, object>>(inputStr);
            if (root == null)
                return null;

            return new JSONPrototype(root);
        }

        private string Prettify(string json)
        {
            var stringBuilder = new StringBuilder();

            bool escaping = false;
            bool inQuotes = false;
            int indentation = 0;

            foreach (char character in json)
            {
                if (escaping)
                {
                    escaping = false;
                    stringBuilder.Append(character);
                }
                else
                {
                    if (character == '\\')
                    {
                        escaping = true;
                        stringBuilder.Append(character);
                    }
                    else if (character == '\"')
                    {
                        inQuotes = !inQuotes;
                        stringBuilder.Append(character);
                    }
                    else if (!inQuotes)
                    {
                        if (character == ',')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append(Environment.NewLine);
                            stringBuilder.Append(' ', indentation * 2);
                        }
                        else if (character == '[' || character == '{')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append(Environment.NewLine);
                            stringBuilder.Append(' ', ++indentation * 2);
                        }
                        else if (character == ']' || character == '}')
                        {
                            stringBuilder.Append(Environment.NewLine);
                            stringBuilder.Append(' ', --indentation * 2);
                            stringBuilder.Append(character);
                        }
                        else if (character == ':')
                        {
                            stringBuilder.Append(character);
                            stringBuilder.Append(' ');
                        }
                        else
                        {
                            stringBuilder.Append(character);
                        }
                    }
                    else
                    {
                        stringBuilder.Append(character);
                    }
                }
            }

            return stringBuilder.ToString();
        }

        private JavaScriptSerializer _js = new JavaScriptSerializer();
    }
}
