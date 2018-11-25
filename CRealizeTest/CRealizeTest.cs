namespace CRealizeTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal enum MyEnum
    {
        Foo,
        Bar
    }

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    internal struct BigBoy
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        public string One;

        public uint Two;

        public override bool Equals(object obj)
        {
            if (!(obj is BigBoy))
            {
                return false;
            }

            var boy = (BigBoy)obj;
            bool eq = One == boy.One &&
                      Two == boy.Two;

            return eq;
        }
    }

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    internal class Node
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        public string Var;

        public long Boo { get; internal set; }

        public BigBoy BigBoy { get; set; }

        public Dictionary<string, int> Dict;

        public Tuple<long, string> Tuple;

        public IList<Node> Children { get; set; }

        public override bool Equals(object obj)
        {
            var node = obj as Node;
            bool eq = node != null &&
                      Var == node.Var &&
                      Boo == node.Boo &&
                      (Tuple?.Equals(node.Tuple) ?? (Tuple == node.Tuple)) &&
                      BigBoy.Equals(node.BigBoy) &&
                      (Dict?.SequenceEqual(node.Dict) ?? (Dict == node.Dict)) &&
                      (Children?.SequenceEqual(node.Children) ?? (Children == node.Children));

            return eq;
        }
    }

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    internal class Leaf : Node
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        public Leaf()
        {
            Hidden = 99;
            _alsoHidden = "Now you don't";
            FooOrBar = MyEnum.Foo;
            Str = null;
        }

        public Leaf(int number) : this()
        {
            Number = number;
        }

        public string Str { get; set; }

        public int Number { get; private set; }

        public MyEnum[] Arr;

        public string Derivative
        {
            get
            {
                return _alsoHidden;
            }
        }

        public MyEnum FooOrBar { get; set; }

        private long Hidden { get; set; }

        private string _alsoHidden;

        public override bool Equals(object obj)
        {
            var leaf = obj as Leaf;
            bool eq = leaf != null &&
                      base.Equals(obj) &&
                      Str == leaf.Str &&
                      Number == leaf.Number &&
                      Derivative == leaf.Derivative &&
                      FooOrBar == leaf.FooOrBar &&
                      (Arr?.SequenceEqual(leaf.Arr) ?? (Arr == leaf.Arr)) &&
                      Hidden == leaf.Hidden &&
                      _alsoHidden == leaf._alsoHidden;

            return eq;
        }
    }

    [TestClass]
    public class CRealizeTest
    {
        [TestMethod]
        public void JSONTest()
        {
            Node nodeIn = new Node()
            {
                Var = null,
                Boo = 12345678910,
                BigBoy = new BigBoy
                {
                    One = "Hey",
                    Two = 987
                },
                Children = new List<Node>
                {
                    new Leaf(123)
                    {
                        Str = "Teacher",
                        Var = "Leave us kids",
                        FooOrBar = MyEnum.Bar,
                        Dict = new Dictionary<string, int> { { "one", 1 }, { "two", 2 } }
                    },
                    new Node
                    {
                        BigBoy = new BigBoy
                        {
                            One = "Alone",
                            Two = 678
                        },
                        Tuple = Tuple.Create(789L, "789"),
                        Children = new List<Node>
                        {
                            new Leaf(456)
                            {
                                FooOrBar = MyEnum.Bar,
                                Arr = new MyEnum[] { MyEnum.Foo, MyEnum.Bar }
                            }
                        }
                    }
                }
            };

            string json = CRealize.Convert.ToJSON(nodeIn, true);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            Node nodeOut = CRealize.Convert.FromJSON<Node>(json);
            Assert.IsFalse(nodeOut == null);
            Assert.IsTrue(nodeOut.Equals(nodeIn));
        }
    }
}
