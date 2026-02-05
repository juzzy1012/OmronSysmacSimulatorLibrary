using System;
using Xunit;
using OmronSysmacSimulator.Converters;
using OmronSysmacSimulator.Attributes;

namespace OmronSysmacSimulator.Tests
{
    public class PlcSerializerTests
    {
        #region Primitive Type Tests

        [Fact]
        public void Serialize_Bool_True_ReturnsCorrectBytes()
        {
            var result = PlcSerializer.Serialize(true);
            Assert.Single(result);
            Assert.Equal(0x01, result[0]);
        }

        [Fact]
        public void Serialize_Bool_False_ReturnsCorrectBytes()
        {
            var result = PlcSerializer.Serialize(false);
            Assert.Single(result);
            Assert.Equal(0x00, result[0]);
        }

        [Fact]
        public void Deserialize_Bool_True_ReturnsTrue()
        {
            var data = new byte[] { 0x01 };
            var result = PlcSerializer.Deserialize<bool>(data);
            Assert.True(result);
        }

        [Fact]
        public void Deserialize_Bool_False_ReturnsFalse()
        {
            var data = new byte[] { 0x00 };
            var result = PlcSerializer.Deserialize<bool>(data);
            Assert.False(result);
        }

        [Theory]
        [InlineData((short)0)]
        [InlineData((short)1)]
        [InlineData((short)-1)]
        [InlineData(short.MaxValue)]
        [InlineData(short.MinValue)]
        public void RoundTrip_Short_PreservesValue(short value)
        {
            var serialized = PlcSerializer.Serialize(value);
            Assert.Equal(2, serialized.Length);
            var deserialized = PlcSerializer.Deserialize<short>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void RoundTrip_Int_PreservesValue(int value)
        {
            var serialized = PlcSerializer.Serialize(value);
            Assert.Equal(4, serialized.Length);
            var deserialized = PlcSerializer.Deserialize<int>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(-1L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void RoundTrip_Long_PreservesValue(long value)
        {
            var serialized = PlcSerializer.Serialize(value);
            Assert.Equal(8, serialized.Length);
            var deserialized = PlcSerializer.Deserialize<long>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(0.0f)]
        [InlineData(1.0f)]
        [InlineData(-1.0f)]
        [InlineData(3.14159f)]
        [InlineData(float.MaxValue)]
        [InlineData(float.MinValue)]
        public void RoundTrip_Float_PreservesValue(float value)
        {
            var serialized = PlcSerializer.Serialize(value);
            Assert.Equal(4, serialized.Length);
            var deserialized = PlcSerializer.Deserialize<float>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(3.141592653589793)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        public void RoundTrip_Double_PreservesValue(double value)
        {
            var serialized = PlcSerializer.Serialize(value);
            Assert.Equal(8, serialized.Length);
            var deserialized = PlcSerializer.Deserialize<double>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Fact]
        public void Serialize_Int_IsLittleEndian()
        {
            // 0x12345678 in little-endian should be: 78 56 34 12
            var result = PlcSerializer.Serialize(0x12345678);
            Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, result);
        }

        #endregion

        #region Complex Type Tests

        public class SimpleStruct
        {
            [Order(0)]
            public int Value1 { get; set; }
            
            [Order(1)]
            public float Value2 { get; set; }
            
            [Order(2)]
            public bool Value3 { get; set; }
        }

        [Fact]
        public void GetSize_SimpleStruct_ReturnsCorrectSize()
        {
            // int (4) + float (4) + bool (1) = 9 bytes, padded to 12 for Omron PLC alignment
            var size = PlcSerializer.GetSize<SimpleStruct>();
            Assert.Equal(12, size);
        }

        [Fact]
        public void RoundTrip_SimpleStruct_PreservesValues()
        {
            var original = new SimpleStruct
            {
                Value1 = 42,
                Value2 = 3.14f,
                Value3 = true
            };

            var serialized = PlcSerializer.Serialize(original);
            Assert.Equal(12, serialized.Length); // Padded to 4-byte boundary

            var deserialized = PlcSerializer.Deserialize<SimpleStruct>(serialized);
            Assert.Equal(original.Value1, deserialized.Value1);
            Assert.Equal(original.Value2, deserialized.Value2);
            Assert.Equal(original.Value3, deserialized.Value3);
        }

        #endregion

        #region Nested Struct Tests

        public class InnerStruct
        {
            [Order(0)]
            public int Id { get; set; }
            
            [Order(1)]
            public float Level { get; set; }
        }

        public class OuterStruct
        {
            [Order(0)]
            public int Prefix { get; set; }
            
            [Order(1)]
            public InnerStruct Inner { get; set; } = new InnerStruct();
            
            [Order(2)]
            public bool Suffix { get; set; }
        }

        [Fact]
        public void GetSize_NestedStruct_ReturnsCorrectSize()
        {
            // int (4) + InnerStruct (int 4 + float 4 = 8) + bool (1) = 13, padded to 16 for Omron PLC alignment
            var size = PlcSerializer.GetSize<OuterStruct>();
            Assert.Equal(16, size);
        }

        [Fact]
        public void RoundTrip_NestedStruct_PreservesValues()
        {
            var original = new OuterStruct
            {
                Prefix = 100,
                Inner = new InnerStruct { Id = 1, Level = 50.5f },
                Suffix = true
            };

            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<OuterStruct>(serialized);

            Assert.Equal(original.Prefix, deserialized.Prefix);
            Assert.Equal(original.Inner.Id, deserialized.Inner.Id);
            Assert.Equal(original.Inner.Level, deserialized.Inner.Level);
            Assert.Equal(original.Suffix, deserialized.Suffix);
        }

        #endregion

        #region Array Tests

        public class StructWithArray
        {
            [Order(0)]
            public int Count { get; set; }
            
            [Order(1)]
            public float[] Values { get; set; } = new float[5];
        }

        [Fact]
        public void GetSize_StructWithArray_ReturnsCorrectSize()
        {
            // int (4) + float[5] (4 * 5 = 20) = 24 bytes
            var size = PlcSerializer.GetSize<StructWithArray>();
            Assert.Equal(24, size);
        }

        [Fact]
        public void RoundTrip_StructWithArray_PreservesValues()
        {
            var original = new StructWithArray
            {
                Count = 5,
                Values = new float[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f }
            };

            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<StructWithArray>(serialized);

            Assert.Equal(original.Count, deserialized.Count);
            Assert.Equal(original.Values.Length, deserialized.Values.Length);
            for (int i = 0; i < original.Values.Length; i++)
            {
                Assert.Equal(original.Values[i], deserialized.Values[i]);
            }
        }

        public class StructWithNestedArray
        {
            [Order(0)]
            public InnerStruct[] Items { get; set; } = new InnerStruct[3];
        }

        [Fact]
        public void RoundTrip_StructWithNestedArray_PreservesValues()
        {
            var original = new StructWithNestedArray
            {
                Items = new InnerStruct[]
                {
                    new InnerStruct { Id = 1, Level = 10.0f },
                    new InnerStruct { Id = 2, Level = 20.0f },
                    new InnerStruct { Id = 3, Level = 30.0f }
                }
            };

            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<StructWithNestedArray>(serialized);

            Assert.Equal(3, deserialized.Items.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(original.Items[i].Id, deserialized.Items[i].Id);
                Assert.Equal(original.Items[i].Level, deserialized.Items[i].Level);
            }
        }

        #endregion

        #region Enum Tests

        public enum TestStatus : short
        {
            Idle = 0,
            Running = 1,
            Faulted = 2
        }

        public class StructWithEnum
        {
            [Order(0)]
            public int Id { get; set; }
            
            [Order(1)]
            public TestStatus Status { get; set; }
        }

        [Fact]
        public void GetSize_StructWithEnum_ReturnsCorrectSize()
        {
            // int (4) + short enum (2) = 6, padded to 8 for Omron PLC alignment
            var size = PlcSerializer.GetSize<StructWithEnum>();
            Assert.Equal(8, size);
        }

        [Fact]
        public void RoundTrip_StructWithEnum_PreservesValues()
        {
            var original = new StructWithEnum
            {
                Id = 42,
                Status = TestStatus.Running
            };

            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<StructWithEnum>(serialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Status, deserialized.Status);
        }

        [Theory]
        [InlineData(TestStatus.Idle)]
        [InlineData(TestStatus.Running)]
        [InlineData(TestStatus.Faulted)]
        public void RoundTrip_AllEnumValues_PreservesValues(TestStatus status)
        {
            var original = new StructWithEnum { Id = 1, Status = status };
            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<StructWithEnum>(serialized);
            Assert.Equal(status, deserialized.Status);
        }

        #endregion

        #region String Tests

        public class StructWithString
        {
            [Order(0)]
            public int Id { get; set; }
            
            [Order(1)]
            [PlcString(20)]
            public string Name { get; set; }
            
            [Order(2)]
            public bool Active { get; set; }
        }

        [Fact]
        public void GetSize_StructWithString_ReturnsCorrectSize()
        {
            // int (4) + string (20) + bool (1) = 25, padded to 28 for Omron PLC alignment
            var size = PlcSerializer.GetSize<StructWithString>();
            Assert.Equal(28, size);
        }

        [Fact]
        public void RoundTrip_StructWithString_PreservesValues()
        {
            var original = new StructWithString
            {
                Id = 1,
                Name = "TestName",
                Active = true
            };

            var serialized = PlcSerializer.Serialize(original);
            Assert.Equal(28, serialized.Length); // Padded to 4-byte boundary

            var deserialized = PlcSerializer.Deserialize<StructWithString>(serialized);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Active, deserialized.Active);
        }

        [Fact]
        public void Serialize_StringLongerThanMax_TruncatesCorrectly()
        {
            var original = new StructWithString
            {
                Id = 1,
                Name = "This is a very long string that exceeds the maximum length",
                Active = true
            };

            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<StructWithString>(serialized);

            // Name should be truncated to 20 chars
            Assert.Equal(20, deserialized.Name.Length);
            Assert.Equal("This is a very long ", deserialized.Name);
        }

        #endregion

        #region Complex Real-World Type Tests

        public class Bunker
        {
            [Order(0)]
            public int Id { get; set; }
            
            [Order(1)]
            public float Level { get; set; }
            
            [Order(2)]
            public bool IsEmpty { get; set; }
        }

        public class MachineMod
        {
            [Order(0)]
            public int MachineId { get; set; }
            
            [Order(1)]
            public bool Running { get; set; }
        }

        public class PLCCyclicLike
        {
            [Order(0)]
            public MachineMod Machine { get; set; } = new MachineMod();
            
            [Order(1)]
            public Bunker[] Bunkers { get; set; } = new Bunker[3];
            
            [Order(2)]
            public bool LogReadRequest { get; set; }
            
            [Order(3)]
            public int Watchdog { get; set; }
        }

        [Fact]
        public void GetSize_PLCCyclicLike_ReturnsCorrectSize()
        {
            // With Omron PLC 4-byte alignment:
            // MachineMod: int (4) + bool (1) = 5 -> padded to 8
            // Bunker: int (4) + float (4) + bool (1) = 9 -> padded to 12
            // Bunker[3]: 12 * 3 = 36
            // bool: 1 at offset 44
            // int: 4 at offset 48 (aligned)
            // Total: 52
            var size = PlcSerializer.GetSize<PLCCyclicLike>();
            Assert.Equal(52, size);
        }

        [Fact]
        public void RoundTrip_PLCCyclicLike_PreservesAllValues()
        {
            var original = new PLCCyclicLike
            {
                Machine = new MachineMod { MachineId = 1, Running = true },
                Bunkers = new Bunker[]
                {
                    new Bunker { Id = 1, Level = 50.0f, IsEmpty = false },
                    new Bunker { Id = 2, Level = 0.0f, IsEmpty = true },
                    new Bunker { Id = 3, Level = 75.5f, IsEmpty = false }
                },
                LogReadRequest = true,
                Watchdog = 12345
            };

            var serialized = PlcSerializer.Serialize(original);
            var deserialized = PlcSerializer.Deserialize<PLCCyclicLike>(serialized);

            Assert.Equal(original.Machine.MachineId, deserialized.Machine.MachineId);
            Assert.Equal(original.Machine.Running, deserialized.Machine.Running);
            
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(original.Bunkers[i].Id, deserialized.Bunkers[i].Id);
                Assert.Equal(original.Bunkers[i].Level, deserialized.Bunkers[i].Level);
                Assert.Equal(original.Bunkers[i].IsEmpty, deserialized.Bunkers[i].IsEmpty);
            }
            
            Assert.Equal(original.LogReadRequest, deserialized.LogReadRequest);
            Assert.Equal(original.Watchdog, deserialized.Watchdog);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void Validate_ValidType_ReturnsNull()
        {
            var result = PlcSerializer.Validate<SimpleStruct>();
            Assert.Null(result);
        }

        public class TypeWithoutOrderAttributes
        {
            public int Value1 { get; set; }
            public int Value2 { get; set; }
        }

        [Fact]
        public void Validate_TypeWithoutOrderAttributes_ReturnsError()
        {
            var result = PlcSerializer.Validate<TypeWithoutOrderAttributes>();
            Assert.NotNull(result);
            Assert.Contains("no members with [Order] attribute", result);
        }

        #endregion
    }
}
