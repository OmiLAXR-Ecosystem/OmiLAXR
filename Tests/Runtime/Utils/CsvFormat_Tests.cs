using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OmiLAXR.Utils;

namespace OmiLAXR.Tests.Utils
{
    public class CsvFormat_Tests
    {
        private static readonly string NL = Environment.NewLine;

        private static Dictionary<string, object> Row(params (string key, object value)[] pairs)
        {
            var dict = new Dictionary<string, object>();
            foreach (var (key, value) in pairs)
                dict[key] = value;
            return dict;
        }

        [Test]
        public void CsvFormat_Tests_AddRow_Expands_Headers_And_Fills_Missing_Values()
        {
            // Arrange
            var csv = new CsvFormat("a");

            // Act
            csv.AddRow(Row(("a", 1)));
            csv.AddRow(Row(("b", 2)));

            // Assert
            CollectionAssert.AreEqual(new[] { "a", "b" }, csv.Headers);
            Assert.AreEqual("a,b" + NL + "1," + NL + ",2" + NL, csv.ToString());
            CollectionAssert.AreEqual(new[] { "1,", ",2" }, csv.GetRowStrings());
            Assert.AreEqual("1,", csv.GetFirstRow());
        }

        [Test]
        public void CsvFormat_Tests_FormatCsvValue_Escapes_Special_Characters()
        {
            // Assert
            Assert.AreEqual("", CsvFormat.FormatCsvValue(null));
            Assert.AreEqual("plain", CsvFormat.FormatCsvValue("plain"));
            Assert.AreEqual("\"a,b\"", CsvFormat.FormatCsvValue("a,b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", CsvFormat.FormatCsvValue("say \"hi\""));
            Assert.AreEqual("\"line\nbreak\"", CsvFormat.FormatCsvValue("line\nbreak"));
        }

        [Test]
        public void CsvFormat_Tests_Output_Escapes_Values_Exactly_Once()
        {
            // Arrange
            var csv = new CsvFormat("text");
            csv.AddRow(Row(("text", "a,b")));

            // Assert
            Assert.AreEqual("\"a,b\"", csv.GetFirstRow());
            CollectionAssert.AreEqual(new[] { "\"a,b\"" }, csv.GetRowStrings());
            Assert.AreEqual("text" + NL + "\"a,b\"" + NL, csv.ToString());
            Assert.AreEqual("\"a,b\"" + NL, csv.GetRowsString());
        }

        [Test]
        public void CsvFormat_Tests_Header_Patterns_Filter_New_Headers_And_Output()
        {
            // Arrange
            var csv = new CsvFormat("keep_1", "drop_1")
            {
                IncludedHeaderPattern = new Regex("^keep|^drop"),
                ExcludedHeaderPattern = new Regex("^drop")
            };

            // Act
            csv.AddRow(Row(("keep_1", 1), ("drop_1", 2), ("keep_2", 3), ("other", 4)));

            // Assert
            CollectionAssert.AreEqual(new[] { "keep_1", "drop_1", "keep_2" }, csv.Headers);
            Assert.AreEqual("keep_1,keep_2", csv.GetHeadersString());
            Assert.AreEqual("1,3", csv.GetFirstRow());
            CollectionAssert.AreEqual(new[] { "1,3" }, csv.GetRowStrings());
        }

        [Test]
        public void CsvFormat_Tests_Custom_Separator()
        {
            // Arrange
            var csv = new CsvFormat(10, "a", "b") { Separator = ";" };
            csv.AddRow(Row(("a", 1), ("b", 2)));

            // Assert
            Assert.AreEqual("a;b", csv.GetHeadersString());
            Assert.AreEqual("1;2", csv.GetFirstRow());
        }

        [Test]
        public void CsvFormat_Tests_RenameHeader_Keeps_Values()
        {
            // Arrange
            var csv = new CsvFormat("old");
            csv.AddRow(Row(("old", 1)));

            // Act
            csv.RenameHeader("old", "new");

            // Assert
            Assert.AreEqual("new", csv.GetHeadersString());
            Assert.AreEqual("1", csv.GetFirstRow());
            Assert.Throws<ArgumentException>(() => csv.RenameHeader("missing", "x"));
        }

        [Test]
        public void CsvFormat_Tests_FormatHeaders_Keeps_Values()
        {
            // Arrange
            var csv = new CsvFormat("a", "b");
            csv.AddRow(Row(("a", 1), ("b", 2)));

            // Act
            csv.FormatHeaders(h => h.ToUpperInvariant());

            // Assert
            Assert.AreEqual("A,B", csv.GetHeadersString());
            Assert.AreEqual("1,2", csv.GetFirstRow());
            CollectionAssert.AreEqual(new[] { "1,2" }, csv.GetRowStrings());
        }

        [Test]
        public void CsvFormat_Tests_DropHeader_Removes_Column()
        {
            // Arrange
            var csv = new CsvFormat("a", "b", "c");
            csv.AddRow(Row(("a", 1), ("b", 2), ("c", 3)));

            // Act
            csv.DropHeader("b");

            // Assert
            Assert.AreEqual("a,c", csv.GetHeadersString());
            Assert.AreEqual("1,3", csv.GetFirstRow());
            Assert.Throws<ArgumentException>(() => csv.DropHeader("b"));
        }

        [Test]
        public void CsvFormat_Tests_SetHeaders_And_ClearRows()
        {
            // Arrange
            var csv = new CsvFormat("a");
            csv.AddRow(Row(("a", 1)));

            // Act
            csv.ClearRows();
            csv.SetHeaders("x", "y");

            // Assert
            Assert.AreEqual(0, csv.Rows.Count);
            Assert.AreEqual("", csv.GetFirstRow());
            Assert.AreEqual("x,y", csv.GetHeadersString());

            // Act
            csv.SetHeaders(null);

            // Assert
            Assert.AreEqual(0, csv.Headers.Count);
        }

        [Test]
        public void CsvFormat_Tests_Append_Aligns_Rows_To_Union_Of_Headers()
        {
            // Arrange
            var left = new CsvFormat("a", "b");
            left.AddRow(Row(("a", 1), ("b", 2)));
            var right = new CsvFormat("b", "c");
            right.AddRow(Row(("b", 3), ("c", 4)));

            // Act
            left.Append(right);
            left.Append(null);
            left.Append(new CsvFormat("ignored"));

            // Assert
            Assert.AreEqual("a,b,c" + NL + "1,2," + NL + ",3,4" + NL, left.ToString());
        }

        [Test]
        public void CsvFormat_Tests_MergeMany_Combines_All()
        {
            // Arrange
            var first = new CsvFormat("a");
            first.AddRow(Row(("a", 1)));
            var second = new CsvFormat("b");
            second.AddRow(Row(("b", 2)));

            // Act
            var merged = CsvFormat.MergeMany(new[] { first, second });

            // Assert
            Assert.AreEqual("a,b" + NL + "1," + NL + ",2" + NL, merged.ToString());
            Assert.AreNotSame(first.Rows[0], merged.Rows[0]);
        }

        [Test]
        public void CsvFormat_Tests_FromJson_Object_Serializes_Nested_Values()
        {
            // Act
            var csv = CsvFormat.FromJson("{\"id\":1,\"meta\":{\"x\":true},\"tags\":[1,2]}");

            // Assert
            Assert.AreEqual("id,meta,tags", csv.GetHeadersString());
            Assert.AreEqual("1,\"{\"\"x\"\":true}\",\"[1,2]\"", csv.GetFirstRow());
        }

        [Test]
        public void CsvFormat_Tests_FromJson_Flatten()
        {
            // Act
            var csv = CsvFormat.FromJson("{\"id\":1,\"meta\":{\"x\":true}}", flatten: true);

            // Assert
            Assert.AreEqual("id,meta_x", csv.GetHeadersString());
            Assert.AreEqual("1,True", csv.GetFirstRow());
        }

        [Test]
        public void CsvFormat_Tests_FromJson_Array_Expands_First_Array_Property()
        {
            // Arrange
            const string json = "[{\"session\":\"s1\",\"events\":[{\"e\":\"a\"},{\"e\":\"b\"}]},{\"session\":\"s2\"},42]";

            // Act
            var csv = CsvFormat.FromJson(json);

            // Assert
            Assert.AreEqual(3, csv.Rows.Count);
            CollectionAssert.AreEquivalent(new[] { "session", "e" }, csv.Headers);
            var rows = csv.GetRowStrings();
            var sessionIndex = csv.Headers.IndexOf("session");
            Assert.AreEqual("s1", rows[0].Split(',')[sessionIndex]);
            Assert.AreEqual("s2", rows[2].Split(',')[sessionIndex]);
        }

        [Test]
        public void CsvFormat_Tests_FromJson_Rejects_Primitive_Root()
        {
            Assert.Throws<ArgumentException>(() => CsvFormat.FromJson("42"));
        }
    }
}
