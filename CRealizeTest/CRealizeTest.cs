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

        public long? Three;

        public Guid? Guid1;

        public Guid Guid2;

        public override bool Equals(object obj) =>
            obj is BigBoy boy &&
            One == boy.One &&
            Two == boy.Two &&
            Three == boy.Three &&
            Guid1 == boy.Guid1 &&
            Guid2 == boy.Guid2;
    }

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    internal class Node
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        public string ABCDVar;

        public long Boo { get; internal set; }

        public DateTime? SomeTime1;

        public BigBoy BigBoy { get; set; }

        public Dictionary<string, int> Dict;

        public Tuple<long, string> Tuple;

        public IList<Node> Children { get; set; }

        public override bool Equals(object obj) =>
            obj is Node node &&
            ABCDVar == node.ABCDVar &&
            Boo == node.Boo &&
            SomeTime1 == node.SomeTime1 &&
            (Tuple?.Equals(node.Tuple) ?? (Tuple == node.Tuple)) &&
            BigBoy.Equals(node.BigBoy) &&
            (Dict?.SequenceEqual(node.Dict) ?? (Dict == node.Dict)) &&
            (Children?.SequenceEqual(node.Children) ?? (Children == node.Children));
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

        public BigBoy? NullableBigBoy { get; set; }

        public string Derivative => _alsoHidden;

        public MyEnum FooOrBar { get; set; }

        private long Hidden { get; set; }

        private string _alsoHidden;

        public override bool Equals(object obj) =>
            base.Equals(obj) &&
            obj is Leaf leaf &&
            Str == leaf.Str &&
            Derivative == leaf.Derivative &&
            FooOrBar == leaf.FooOrBar &&
            (Arr?.SequenceEqual(leaf.Arr) ?? (Arr == leaf.Arr)) &&
            NullableBigBoy.HasValue == leaf.NullableBigBoy.HasValue &&
            (!NullableBigBoy.HasValue || NullableBigBoy.Value.Equals(leaf.NullableBigBoy.Value)) &&
            Hidden == leaf.Hidden &&
            _alsoHidden == leaf._alsoHidden;
    }

    [TestClass]
    public class CRealizeTest
    {
        [TestMethod]
        public void JSONTest()
        {
            Node nodeIn = new Node()
            {
                ABCDVar = null,
                Boo = 12345678910,
                BigBoy = new BigBoy
                {
                    One = "Hey",
                    Two = 987,
                    Three = -654,
                    Guid1 = Guid.NewGuid(),
                    Guid2 = Guid.NewGuid()
                },
                Children = new List<Node>
                {
                    new Leaf(123)
                    {
                        Str = "Teacher",
                        ABCDVar = "Leave us kids",
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
                            new Leaf()
                            {
                                FooOrBar = MyEnum.Bar,
                                SomeTime1 = DateTime.UtcNow,
                                Arr = new MyEnum[] { MyEnum.Foo, MyEnum.Bar },
                                NullableBigBoy = new BigBoy() { Two = 2 }
                            }
                        }
                    }
                }
            };

            string json = CRealize.Convert.ToJSON(nodeIn, true);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            Node nodeOut = CRealize.Convert.FromJSON<Node>(json);
            Assert.IsNotNull(nodeOut);

            // Not supposed to be serialized due to being internal
            Assert.AreEqual(0, nodeOut.Boo);
            nodeOut.Boo = nodeIn.Boo;

            // Not supposed to be serialized due to being private; won't be compared below
            Assert.AreEqual(0, ((Leaf)nodeOut.Children[0]).Number);

            Assert.IsTrue(nodeOut.Equals(nodeIn));

            // Make sure we can deserialize and serialize again a future value.
            json = json.Replace("\"Foo\"", "\"FutureEnumValue\"");
            json = CRealize.Convert.ToJSON(CRealize.Convert.FromJSON<Node>(json));
            Assert.IsTrue(json.Contains("\"FutureEnumValue\""));
        }
    }
}
