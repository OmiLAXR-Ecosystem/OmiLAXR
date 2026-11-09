using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OmiLAXR.Extensions;

namespace OmiLAXR.Tests.Extensions
{
    public class CollectionExt_Tests
    {
        [Test]
        public void ArrayExt_AreValuesEqual_Compares_Sequences()
        {
            // Arrange
            var a = new[] { 1, 2, 3 };

            // Assert
            Assert.IsTrue(a.AreValuesEqual(a));
            Assert.IsTrue(a.AreValuesEqual(new[] { 1, 2, 3 }));
            Assert.IsFalse(a.AreValuesEqual(new[] { 3, 2, 1 }));
            Assert.IsFalse(a.AreValuesEqual(null));
            Assert.IsFalse(((int[])null).AreValuesEqual(a));
            Assert.IsTrue(((int[])null).AreValuesEqual(null));
        }

        [Test]
        public void DateTimeExt_Min_And_Max()
        {
            // Arrange
            var early = new DateTime(2025, 1, 1);
            var mid = new DateTime(2025, 6, 1);
            var late = new DateTime(2025, 12, 31);

            // Assert
            Assert.AreEqual(early, DateTimeExt.Min(mid, early));
            Assert.AreEqual(late, DateTimeExt.Max(late, mid));
            Assert.AreEqual(early, DateTimeExt.Min(mid, late, early));
            Assert.AreEqual(late, DateTimeExt.Max(mid, late, early));
        }

        [Test]
        public void DateTimeExt_GetFirstNotNull()
        {
            // Arrange
            var value = new DateTime(2025, 1, 1);

            // Assert
            Assert.AreEqual(value, DateTimeExt.GetFirstNotNull(null, value, DateTime.MaxValue));
            Assert.IsNull(DateTimeExt.GetFirstNotNull(null, null));
        }

        [Test]
        public void JsonExt_Flatten_Nested_Objects_And_Arrays()
        {
            // Arrange
            var json = JToken.Parse("{\"a\":{\"b\":1,\"c\":[true,\"x\"]},\"d\":null}");

            // Act
            var flat = json.Flatten();

            // Assert
            Assert.AreEqual(4, flat.Count);
            Assert.AreEqual(1L, flat["a_b"]);
            Assert.AreEqual(true, flat["a_c_0"]);
            Assert.AreEqual("x", flat["a_c_1"]);
            Assert.IsTrue(flat.ContainsKey("d"));
            Assert.IsNull(flat["d"]);
        }

        [Test]
        public void JsonExt_Flatten_Uses_Prefix()
        {
            // Act
            var flat = JToken.Parse("{\"x\":2}").Flatten("root_");

            // Assert
            Assert.AreEqual(2L, flat["root_x"]);
        }
    }
}
