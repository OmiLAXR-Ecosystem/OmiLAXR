using System;
using NUnit.Framework;
using OmiLAXR.Types;

namespace OmiLAXR.Tests.Types
{
    public class Duration_Tests
    {
        [Test]
        public void Duration_Tests_Factory_Methods_Set_Value_And_Unit()
        {
            // Act
            var ms = Duration.FromMilliseconds(5);
            var s = Duration.FromSeconds(5);
            var min = Duration.FromMinutes(5);
            var h = Duration.FromHours(5);
            var d = Duration.FromDays(5);

            // Assert
            Assert.AreEqual(Duration.DurationUnit.Milliseconds, ms.Unit);
            Assert.AreEqual(Duration.DurationUnit.Seconds, s.Unit);
            Assert.AreEqual(Duration.DurationUnit.Minutes, min.Unit);
            Assert.AreEqual(Duration.DurationUnit.Hours, h.Unit);
            Assert.AreEqual(Duration.DurationUnit.Days, d.Unit);
            Assert.AreEqual(5, d.Value);
        }

        [Test]
        public void Duration_Tests_ToMilliseconds_Converts_All_Units()
        {
            // Assert
            Assert.AreEqual(250, Duration.FromMilliseconds(250).ToMilliseconds());
            Assert.AreEqual(1_500, Duration.FromSeconds(1.5).ToMilliseconds());
            Assert.AreEqual(120_000, Duration.FromMinutes(2).ToMilliseconds());
            Assert.AreEqual(3_600_000, Duration.FromHours(1).ToMilliseconds());
            Assert.AreEqual(86_400_000, Duration.FromDays(1).ToMilliseconds());
        }

        [Test]
        public void Duration_Tests_Converts_To_Larger_Units()
        {
            // Arrange
            var oneDay = Duration.FromDays(1);

            // Assert
            Assert.AreEqual(86_400, oneDay.ToSeconds(), 1e-9);
            Assert.AreEqual(1_440, oneDay.ToMinutes(), 1e-9);
            Assert.AreEqual(24, oneDay.ToHours(), 1e-9);
            Assert.AreEqual(1, oneDay.ToDays(), 1e-9);
            Assert.AreEqual(1, Duration.FromDays(7).ToWeeks(), 1e-9);
            Assert.AreEqual(1, Duration.FromDays(365.25).ToYears(), 1e-9);
            Assert.AreEqual(12, Duration.FromDays(365.2425).ToMonths(), 1e-9);
        }

        [Test]
        public void Duration_Tests_ToTimeSpan_Converts_All_Units()
        {
            // Assert
            Assert.AreEqual(TimeSpan.FromMilliseconds(10), Duration.FromMilliseconds(10).ToTimeSpan());
            Assert.AreEqual(TimeSpan.FromSeconds(10), Duration.FromSeconds(10).ToTimeSpan());
            Assert.AreEqual(TimeSpan.FromMinutes(10), Duration.FromMinutes(10).ToTimeSpan());
            Assert.AreEqual(TimeSpan.FromHours(10), Duration.FromHours(10).ToTimeSpan());
            Assert.AreEqual(TimeSpan.FromDays(10), Duration.FromDays(10).ToTimeSpan());
        }

        [Test]
        public void Duration_Tests_Invalid_Unit_Throws()
        {
            // Arrange
            var invalid = new Duration(1, (Duration.DurationUnit)99);

            // Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => invalid.ToMilliseconds());
            Assert.Throws<ArgumentOutOfRangeException>(() => invalid.ToTimeSpan());
        }

        [Test]
        public void Duration_Tests_ToString_Returns_Iso8601()
        {
            // Assert
            Assert.AreEqual("PT0S", Duration.FromSeconds(0).ToString());
            Assert.AreEqual("PT1M30S", Duration.FromSeconds(90).ToString());
            Assert.AreEqual("PT1.5S", Duration.FromMilliseconds(1500).ToString());
            Assert.AreEqual("P1DT1H", Duration.FromHours(25).ToString());
        }

        [Test]
        public void Duration_Tests_Equality_Is_Unit_Agnostic()
        {
            // Arrange
            var a = Duration.FromSeconds(60);
            var b = Duration.FromMinutes(1);

            // Assert
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsFalse(a.Equals("60"));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreEqual(0, a.CompareTo(b));
        }

        [Test]
        public void Duration_Tests_Comparison_Operators()
        {
            // Arrange
            var shorter = Duration.FromSeconds(59);
            var longer = Duration.FromMinutes(1);

            // Assert
            Assert.IsTrue(shorter < longer);
            Assert.IsTrue(shorter <= longer);
            Assert.IsTrue(longer > shorter);
            Assert.IsTrue(longer >= shorter);
            Assert.IsTrue(longer <= Duration.FromSeconds(60));
            Assert.IsTrue(longer >= Duration.FromSeconds(60));
            Assert.IsFalse(longer < Duration.FromSeconds(60));
            Assert.AreEqual(-1, shorter.CompareTo(longer));
            Assert.AreEqual(1, longer.CompareTo(shorter));
        }

        [Test]
        public void Duration_Tests_Conversions_Use_Milliseconds()
        {
            // Act
            Duration fromDouble = 1500.0;
            Duration fromFloat = 250f;

            // Assert
            Assert.AreEqual(Duration.DurationUnit.Milliseconds, fromDouble.Unit);
            Assert.AreEqual(1500.0, fromDouble.Value);
            Assert.AreEqual(250.0, fromFloat.Value);
            Assert.AreEqual(2000.0, (double)Duration.FromSeconds(2));
            Assert.AreEqual(2000f, (float)Duration.FromSeconds(2));
        }
    }
}
