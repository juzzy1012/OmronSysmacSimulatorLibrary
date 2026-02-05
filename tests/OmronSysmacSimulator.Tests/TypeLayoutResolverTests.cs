using System;
using Xunit;
using OmronSysmacSimulator.Converters;
using OmronSysmacSimulator.Attributes;
using OmronSysmacSimulator.Exceptions;

namespace OmronSysmacSimulator.Tests
{
    public class TypeLayoutResolverTests
    {
        #region Test Types

        public class SimpleOrderedType
        {
            [Order(0)]
            public int First { get; set; }

            [Order(1)]
            public float Second { get; set; }

            [Order(2)]
            public bool Third { get; set; }
        }

        public class OutOfOrderType
        {
            [Order(2)]
            public bool Third { get; set; }

            [Order(0)]
            public int First { get; set; }

            [Order(1)]
            public float Second { get; set; }
        }

        public class TypeWithMixedMembers
        {
            [Order(0)]
            public int OrderedProperty { get; set; }

            public int UnorderedProperty { get; set; } // Should be ignored

            [Order(1)]
            public float AnotherOrderedProperty { get; set; }
        }

        public class NestedChild
        {
            [Order(0)]
            public int Value { get; set; }
        }

        public class NestedParent
        {
            [Order(0)]
            public int ParentValue { get; set; }

            [Order(1)]
            public NestedChild Child { get; set; } = new NestedChild();
        }

        public class TypeWithArray
        {
            [Order(0)]
            public int Count { get; set; }

            [Order(1)]
            public float[] Values { get; set; } = new float[10];
        }

        public class TypeWithArrayAttribute
        {
            [Order(0)]
            [ArrayLength(5)]
            public int[] Items { get; set; }
        }

        #endregion

        [Fact]
        public void Resolve_SimpleType_ReturnsCorrectLayout()
        {
            var layout = TypeLayoutResolver.Resolve<SimpleOrderedType>();

            Assert.Equal(typeof(SimpleOrderedType), layout.Type);
            Assert.Equal(3, layout.Members.Count);
            Assert.Equal(12, layout.TotalSize); // int(4) + float(4) + bool(1) + padding(3)
        }

        [Fact]
        public void Resolve_SimpleType_MembersInOrderSequence()
        {
            var layout = TypeLayoutResolver.Resolve<SimpleOrderedType>();

            Assert.Equal("First", layout.Members[0].Member.Name);
            Assert.Equal("Second", layout.Members[1].Member.Name);
            Assert.Equal("Third", layout.Members[2].Member.Name);
        }

        [Fact]
        public void Resolve_SimpleType_OffsetsAreCorrect()
        {
            var layout = TypeLayoutResolver.Resolve<SimpleOrderedType>();

            Assert.Equal(0, layout.Members[0].Offset); // First: 0
            Assert.Equal(4, layout.Members[1].Offset); // Second: 4 (after int)
            Assert.Equal(8, layout.Members[2].Offset); // Third: 8 (after int + float)
        }

        [Fact]
        public void Resolve_SimpleType_SizesAreCorrect()
        {
            var layout = TypeLayoutResolver.Resolve<SimpleOrderedType>();

            Assert.Equal(4, layout.Members[0].Size); // int
            Assert.Equal(4, layout.Members[1].Size); // float
            Assert.Equal(1, layout.Members[2].Size); // bool
        }

        [Fact]
        public void Resolve_OutOfOrderType_SortsCorrectly()
        {
            var layout = TypeLayoutResolver.Resolve<OutOfOrderType>();

            Assert.Equal("First", layout.Members[0].Member.Name);
            Assert.Equal("Second", layout.Members[1].Member.Name);
            Assert.Equal("Third", layout.Members[2].Member.Name);
        }

        [Fact]
        public void Resolve_MixedMembers_IgnoresUnordered()
        {
            var layout = TypeLayoutResolver.Resolve<TypeWithMixedMembers>();

            Assert.Equal(2, layout.Members.Count);
            Assert.Equal("OrderedProperty", layout.Members[0].Member.Name);
            Assert.Equal("AnotherOrderedProperty", layout.Members[1].Member.Name);
        }

        [Fact]
        public void Resolve_NestedType_IncludesNestedSize()
        {
            var layout = TypeLayoutResolver.Resolve<NestedParent>();

            Assert.Equal(2, layout.Members.Count);
            Assert.Equal(8, layout.TotalSize); // int(4) + NestedChild(int:4)
            
            var childMember = layout.Members[1];
            Assert.NotNull(childMember.NestedLayout);
            Assert.Equal(4, childMember.Size);
        }

        [Fact]
        public void Resolve_TypeWithArray_CalculatesArraySize()
        {
            var layout = TypeLayoutResolver.Resolve<TypeWithArray>();

            Assert.Equal(2, layout.Members.Count);
            Assert.Equal(44, layout.TotalSize); // int(4) + float[10](40)
            
            var arrayMember = layout.Members[1];
            Assert.Equal(10, arrayMember.ArrayLength);
            Assert.Equal(40, arrayMember.Size);
        }

        [Fact]
        public void Resolve_TypeWithArrayAttribute_UsesAttributeLength()
        {
            var layout = TypeLayoutResolver.Resolve<TypeWithArrayAttribute>();

            var arrayMember = layout.Members[0];
            Assert.Equal(5, arrayMember.ArrayLength);
            Assert.Equal(20, arrayMember.Size); // int[5] = 4 * 5
        }

        [Fact]
        public void Resolve_PrimitiveType_ReturnsSizeOnly()
        {
            var intLayout = TypeLayoutResolver.Resolve<int>();
            Assert.Equal(4, intLayout.TotalSize);
            Assert.Empty(intLayout.Members);

            var boolLayout = TypeLayoutResolver.Resolve<bool>();
            Assert.Equal(1, boolLayout.TotalSize);

            var doubleLayout = TypeLayoutResolver.Resolve<double>();
            Assert.Equal(8, doubleLayout.TotalSize);
        }

        [Fact]
        public void Resolve_CachesResults()
        {
            TypeLayoutResolver.ClearCache();

            var layout1 = TypeLayoutResolver.Resolve<SimpleOrderedType>();
            var layout2 = TypeLayoutResolver.Resolve<SimpleOrderedType>();

            Assert.Same(layout1, layout2);
        }

        [Fact]
        public void GetSize_ReturnsCorrectValue()
        {
            Assert.Equal(4, TypeLayoutResolver.GetSize<int>());
            Assert.Equal(12, TypeLayoutResolver.GetSize<SimpleOrderedType>()); // 9 bytes padded to 12
        }

        public class TypeWithNoOrderAttributes
        {
            public int Value1 { get; set; }
            public int Value2 { get; set; }
        }

        [Fact]
        public void Resolve_TypeWithNoOrderAttributes_Throws()
        {
            var exception = Assert.Throws<SysmacTypeException>(
                () => TypeLayoutResolver.Resolve<TypeWithNoOrderAttributes>());
            
            Assert.Contains("no members with [Order] attribute", exception.Message);
        }

        public class TypeWithDuplicateOrders
        {
            [Order(0)]
            public int First { get; set; }

            [Order(0)]
            public int Second { get; set; }
        }

        [Fact]
        public void Resolve_TypeWithDuplicateOrders_Throws()
        {
            var exception = Assert.Throws<SysmacTypeException>(
                () => TypeLayoutResolver.Resolve<TypeWithDuplicateOrders>());
            
            Assert.Contains("duplicate [Order(0)]", exception.Message);
        }

        public class TypeWithStringNoAttribute
        {
            [Order(0)]
            public string Name { get; set; }
        }

        [Fact]
        public void Resolve_StringWithoutPlcStringAttribute_Throws()
        {
            var exception = Assert.Throws<SysmacTypeException>(
                () => TypeLayoutResolver.Resolve<TypeWithStringNoAttribute>());
            
            Assert.Contains("[PlcString(maxLength)]", exception.Message);
        }

        public class TypeWithString
        {
            [Order(0)]
            [PlcString(50)]
            public string Name { get; set; }
        }

        [Fact]
        public void Resolve_StringWithAttribute_SetsCorrectSize()
        {
            var layout = TypeLayoutResolver.Resolve<TypeWithString>();

            Assert.Single(layout.Members);
            Assert.Equal(50, layout.Members[0].Size);
            Assert.Equal(50, layout.Members[0].StringMaxLength);
        }

        [Fact]
        public void Describe_ReturnsFormattedString()
        {
            var description = TypeLayoutResolver.Describe<SimpleOrderedType>();

            Assert.Contains("SimpleOrderedType", description);
            Assert.Contains("12 bytes", description); // 9 bytes padded to 12
            Assert.Contains("Offset 0", description);
            Assert.Contains("First", description);
        }

        [Fact]
        public void Describe_WithChunkSize_ShowsChunkInfo()
        {
            var description = TypeLayoutResolver.Describe<SimpleOrderedType>(4);

            Assert.Contains("chunks", description);
            Assert.Contains("[Chunk", description);
        }

        public enum TestEnumInt : int
        {
            Value1 = 0,
            Value2 = 1
        }

        public enum TestEnumShort : short
        {
            Value1 = 0,
            Value2 = 1
        }

        public class TypeWithEnums
        {
            [Order(0)]
            public TestEnumInt IntEnum { get; set; }

            [Order(1)]
            public TestEnumShort ShortEnum { get; set; }
        }

        [Fact]
        public void Resolve_Enums_UseUnderlyingTypeSize()
        {
            var layout = TypeLayoutResolver.Resolve<TypeWithEnums>();

            Assert.Equal(8, layout.TotalSize); // int(4) + short(2) = 6, padded to 8
            Assert.Equal(4, layout.Members[0].Size);
            Assert.Equal(2, layout.Members[1].Size);
        }
    }
}
