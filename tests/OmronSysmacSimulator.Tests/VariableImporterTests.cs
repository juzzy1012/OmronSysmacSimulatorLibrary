using System;
using System.IO;
using Xunit;

namespace OmronSysmacSimulator.Tests
{
    public class VariableImporterTests
    {
        [Theory]
        [InlineData("BOOL", PlcDataType.Bool)]
        [InlineData("SINT", PlcDataType.SInt)]
        [InlineData("INT", PlcDataType.Int)]
        [InlineData("DINT", PlcDataType.DInt)]
        [InlineData("LINT", PlcDataType.LInt)]
        [InlineData("USINT", PlcDataType.USInt)]
        [InlineData("UINT", PlcDataType.UInt)]
        [InlineData("UDINT", PlcDataType.UDInt)]
        [InlineData("ULINT", PlcDataType.ULInt)]
        [InlineData("REAL", PlcDataType.Real)]
        [InlineData("LREAL", PlcDataType.LReal)]
        [InlineData("STRING", PlcDataType.String)]
        public void ParseDataType_StandardTypes_ReturnsCorrectType(string typeString, PlcDataType expected)
        {
            var result = VariableImporter.ParseDataType(typeString);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("bool")]
        [InlineData("Bool")]
        [InlineData("BOOL")]
        public void ParseDataType_CaseInsensitive(string typeString)
        {
            var result = VariableImporter.ParseDataType(typeString);
            Assert.Equal(PlcDataType.Bool, result);
        }

        [Fact]
        public void ParseDataType_UnknownType_ReturnsStruct()
        {
            var result = VariableImporter.ParseDataType("MyCustomType");
            Assert.Equal(PlcDataType.Struct, result);
        }

        [Fact]
        public void ParseLine_SimpleVariable_ReturnsOneEntry()
        {
            var result = VariableImporter.ParseLine("myVariable\tINT");
            
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("myVariable", result[0].Name);
            Assert.Equal(PlcDataType.Int, result[0].Type);
        }

        [Fact]
        public void ParseLine_ArrayVariable_ReturnsMultipleEntries()
        {
            var result = VariableImporter.ParseLine("myArray\tREAL[0..4]");
            
            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            Assert.Equal("myArray[0]", result[0].Name);
            Assert.Equal("myArray[1]", result[1].Name);
            Assert.Equal("myArray[2]", result[2].Name);
            Assert.Equal("myArray[3]", result[3].Name);
            Assert.Equal("myArray[4]", result[4].Name);
            
            foreach (var entry in result)
            {
                Assert.Equal(PlcDataType.Real, entry.Type);
            }
        }

        [Fact]
        public void ParseLine_EmptyLine_ReturnsNull()
        {
            Assert.Null(VariableImporter.ParseLine(""));
            Assert.Null(VariableImporter.ParseLine("   "));
            Assert.Null(VariableImporter.ParseLine(null));
        }

        [Fact]
        public void ParseLine_SingleToken_ReturnsNull()
        {
            Assert.Null(VariableImporter.ParseLine("onlyOneName"));
        }

        [Fact]
        public void ParseStream_MultipleLines_ReturnsAllVariables()
        {
            string content = @"Name	Type	Comment
var1	INT	First variable
var2	REAL	Second variable
var3	BOOL	Third variable";

            using (var reader = new StringReader(content))
            {
                var result = VariableImporter.ParseStream(reader);
                
                Assert.Equal(3, result.Count);
                Assert.Equal("var1", result[0].Name);
                Assert.Equal(PlcDataType.Int, result[0].Type);
                Assert.Equal("var2", result[1].Name);
                Assert.Equal(PlcDataType.Real, result[1].Type);
                Assert.Equal("var3", result[2].Name);
                Assert.Equal(PlcDataType.Bool, result[2].Type);
            }
        }

        [Fact]
        public void ParseStream_WithArrays_ExpandsCorrectly()
        {
            string content = @"Name	Type	Comment
counter	INT	Simple counter
values	REAL[0..2]	Array of values
flag	BOOL	Simple flag";

            using (var reader = new StringReader(content))
            {
                var result = VariableImporter.ParseStream(reader);
                
                // counter (1) + values[0..2] (3) + flag (1) = 5
                Assert.Equal(5, result.Count);
                Assert.Equal("counter", result[0].Name);
                Assert.Equal("values[0]", result[1].Name);
                Assert.Equal("values[1]", result[2].Name);
                Assert.Equal("values[2]", result[3].Name);
                Assert.Equal("flag", result[4].Name);
            }
        }

        [Fact]
        public void ParseStream_EmptyContent_ReturnsEmptyList()
        {
            using (var reader = new StringReader(""))
            {
                var result = VariableImporter.ParseStream(reader);
                Assert.Empty(result);
            }
        }

        [Fact]
        public void ParseStream_OnlyHeader_ReturnsEmptyList()
        {
            using (var reader = new StringReader("Name\tType\tComment"))
            {
                var result = VariableImporter.ParseStream(reader);
                Assert.Empty(result);
            }
        }

        [Fact]
        public void DataTypeToString_AllTypes_ReturnsExpectedStrings()
        {
            Assert.Equal("BOOL", VariableImporter.DataTypeToString(PlcDataType.Bool));
            Assert.Equal("SINT", VariableImporter.DataTypeToString(PlcDataType.SInt));
            Assert.Equal("INT", VariableImporter.DataTypeToString(PlcDataType.Int));
            Assert.Equal("DINT", VariableImporter.DataTypeToString(PlcDataType.DInt));
            Assert.Equal("LINT", VariableImporter.DataTypeToString(PlcDataType.LInt));
            Assert.Equal("USINT", VariableImporter.DataTypeToString(PlcDataType.USInt));
            Assert.Equal("UINT", VariableImporter.DataTypeToString(PlcDataType.UInt));
            Assert.Equal("UDINT", VariableImporter.DataTypeToString(PlcDataType.UDInt));
            Assert.Equal("ULINT", VariableImporter.DataTypeToString(PlcDataType.ULInt));
            Assert.Equal("REAL", VariableImporter.DataTypeToString(PlcDataType.Real));
            Assert.Equal("LREAL", VariableImporter.DataTypeToString(PlcDataType.LReal));
            Assert.Equal("STRING", VariableImporter.DataTypeToString(PlcDataType.String));
            Assert.Equal("STRUCT", VariableImporter.DataTypeToString(PlcDataType.Struct));
            Assert.Equal("ARRAY", VariableImporter.DataTypeToString(PlcDataType.Array));
        }

        [Fact]
        public void ParseFile_NonExistentFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(() => 
                VariableImporter.ParseFile("non_existent_file.txt"));
        }

        [Fact]
        public void ParseLine_ArrayWithNonZeroStart_ExpandsCorrectly()
        {
            var result = VariableImporter.ParseLine("data\tINT[5..8]");
            
            Assert.Equal(4, result.Count);
            Assert.Equal("data[5]", result[0].Name);
            Assert.Equal("data[6]", result[1].Name);
            Assert.Equal("data[7]", result[2].Name);
            Assert.Equal("data[8]", result[3].Name);
        }

        [Fact]
        public void ParseLine_CustomType_ReturnsStructType()
        {
            var result = VariableImporter.ParseLine("motor1\tMotorController");
            
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("motor1", result[0].Name);
            Assert.Equal(PlcDataType.Struct, result[0].Type);
        }

        [Fact]
        public void ParseLine_ExtraWhitespace_HandlesCorrectly()
        {
            var result = VariableImporter.ParseLine("  myVar  \t  REAL  \tSome comment here");
            
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("myVar", result[0].Name);
            Assert.Equal(PlcDataType.Real, result[0].Type);
        }
    }
}
