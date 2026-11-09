using System;
using NUnit.Framework;
using OmiLAXR.Types;

namespace OmiLAXR.Tests.Types
{
    public class SerializableDateTime_Tests
    {
        [Test]
        public void SerializableDateTime_Tests_Round_Trips_DateTime()
        {
            // Arrange
            var original = new DateTime(2024, 2, 29, 23, 59, 58, 123, DateTimeKind.Utc);

            // Act
            SerializableDateTime sdt = original;
            DateTime back = sdt;

            // Assert
            Assert.AreEqual(original, back);
            Assert.AreEqual(DateTimeKind.Utc, back.Kind);
            Assert.AreEqual(2024, sdt.year);
            Assert.AreEqual(123, sdt.millisecond);
        }

        [Test]
        public void SerializableDateTime_Tests_Default_Is_Start_Of_2025()
        {
            // Act
            var sdt = new SerializableDateTime();

            // Assert
            Assert.AreEqual(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), sdt.ToDateTime());
        }
    }
}
