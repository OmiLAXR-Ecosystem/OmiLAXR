using System;
using NUnit.Framework;
using OmiLAXR.Extensions;

namespace OmiLAXR.Tests.Extensions
{
    public class TimeExt_Tests
    {
        [Test]
        public void TimeExt_Tests_ToRfc3339_Formats_Utc_DateTime_With_Milliseconds()
        {
            // Arrange
            var dateTime = new DateTime(2025, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc);

            // Assert
            Assert.AreEqual("2025-01-02T03:04:05.006Z", dateTime.ToRfc3339());
        }

        [Test]
        public void TimeExt_Tests_ToRfc3339_Converts_DateTimeOffset_To_Utc()
        {
            // Arrange
            var dto = new DateTimeOffset(2025, 1, 2, 5, 4, 5, 6, TimeSpan.FromHours(2));

            // Assert
            Assert.AreEqual("2025-01-02T03:04:05.006Z", dto.ToRfc3339());
        }

        [Test]
        public void TimeExt_Tests_ToIso8601_Zero_Is_PT0S()
        {
            Assert.AreEqual("PT0S", TimeSpan.Zero.ToIso8601());
        }

        [Test]
        public void TimeExt_Tests_ToIso8601_Days_Only_Has_No_Time_Part()
        {
            Assert.AreEqual("P2D", TimeSpan.FromDays(2).ToIso8601());
        }

        [Test]
        public void TimeExt_Tests_ToIso8601_All_Components()
        {
            // Arrange
            var timeSpan = new TimeSpan(1, 2, 3, 4, 500);

            // Assert
            Assert.AreEqual("P1DT2H3M4.5S", timeSpan.ToIso8601());
        }

        [Test]
        public void TimeExt_Tests_ToIso8601_Skips_Zero_Components()
        {
            Assert.AreEqual("PT2H", TimeSpan.FromHours(2).ToIso8601());
            Assert.AreEqual("PT5M", TimeSpan.FromMinutes(5).ToIso8601());
            Assert.AreEqual("PT0.25S", TimeSpan.FromMilliseconds(250).ToIso8601());
        }
    }
}
