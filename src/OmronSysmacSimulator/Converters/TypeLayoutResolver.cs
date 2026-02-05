using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OmronSysmacSimulator.Attributes;
using OmronSysmacSimulator.Exceptions;

namespace OmronSysmacSimulator.Converters
{
    /// <summary>
    /// Represents a member in a PLC structure layout.
    /// </summary>
    public class MemberLayout
    {
        /// <summary>
        /// The property or field info.
        /// </summary>
        public MemberInfo Member { get; set; }

        /// <summary>
        /// The order value from the Order attribute.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// The byte offset within the structure.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// The size in bytes.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// The PLC data type.
        /// </summary>
        public PlcDataType PlcType { get; set; }

        /// <summary>
        /// The .NET type of the member.
        /// </summary>
        public Type ClrType { get; set; }

        /// <summary>
        /// For arrays, the number of elements.
        /// </summary>
        public int? ArrayLength { get; set; }

        /// <summary>
        /// For strings, the maximum length.
        /// </summary>
        public int? StringMaxLength { get; set; }

        /// <summary>
        /// For nested structures, the nested layout.
        /// </summary>
        public TypeLayout NestedLayout { get; set; }

        /// <summary>
        /// Gets the value of this member from an instance.
        /// </summary>
        public object GetValue(object instance)
        {
            if (Member is PropertyInfo prop)
                return prop.GetValue(instance);
            if (Member is FieldInfo field)
                return field.GetValue(instance);
            throw new InvalidOperationException($"Unknown member type: {Member.MemberType}");
        }

        /// <summary>
        /// Sets the value of this member on an instance.
        /// </summary>
        public void SetValue(object instance, object value)
        {
            if (Member is PropertyInfo prop)
                prop.SetValue(instance, value);
            else if (Member is FieldInfo field)
                field.SetValue(instance, value);
            else
                throw new InvalidOperationException($"Unknown member type: {Member.MemberType}");
        }
    }

    /// <summary>
    /// Represents the complete layout of a PLC structure type.
    /// </summary>
    public class TypeLayout
    {
        /// <summary>
        /// The .NET type this layout represents.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// The ordered list of members.
        /// </summary>
        public List<MemberLayout> Members { get; set; } = new List<MemberLayout>();

        /// <summary>
        /// The total size in bytes.
        /// </summary>
        public int TotalSize { get; set; }
    }

    /// <summary>
    /// Resolves the byte layout of types based on [Order] attributes.
    /// </summary>
    public static class TypeLayoutResolver
    {
        private static readonly ConcurrentDictionary<Type, TypeLayout> _cache = 
            new ConcurrentDictionary<Type, TypeLayout>();

        /// <summary>
        /// Resolves the layout for a type.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>The type layout.</returns>
        public static TypeLayout Resolve<T>()
        {
            return Resolve(typeof(T));
        }

        /// <summary>
        /// Resolves the layout for a type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>The type layout.</returns>
        public static TypeLayout Resolve(Type type)
        {
            return _cache.GetOrAdd(type, t => BuildLayout(t));
        }

        /// <summary>
        /// Gets the size in bytes for a type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The size in bytes.</returns>
        public static int GetSize<T>()
        {
            return Resolve<T>().TotalSize;
        }

        /// <summary>
        /// Gets the size in bytes for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The size in bytes.</returns>
        public static int GetSize(Type type)
        {
            return Resolve(type).TotalSize;
        }

        /// <summary>
        /// Clears the layout cache.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static TypeLayout BuildLayout(Type type)
        {
            // Handle primitive types directly
            if (TryGetPrimitiveSize(type, out int primitiveSize, out PlcDataType primitiveType))
            {
                return new TypeLayout
                {
                    Type = type,
                    TotalSize = primitiveSize,
                    Members = new List<MemberLayout>()
                };
            }

            // Handle complex types with [Order] attributes
            var layout = new TypeLayout { Type = type };
            var members = new List<(MemberInfo member, int order, Type memberType)>();

            // Get all properties and fields with [Order] attribute
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var orderAttr = prop.GetCustomAttribute<OrderAttribute>();
                if (orderAttr != null)
                {
                    members.Add((prop, orderAttr.Value, prop.PropertyType));
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var orderAttr = field.GetCustomAttribute<OrderAttribute>();
                if (orderAttr != null)
                {
                    members.Add((field, orderAttr.Value, field.FieldType));
                }
            }

            if (members.Count == 0)
            {
                throw new SysmacTypeException(
                    $"Type '{type.Name}' has no members with [Order] attribute. " +
                    "Add [Order(n)] attributes to properties or fields to define the layout.",
                    type);
            }

            // Sort by order
            members = members.OrderBy(m => m.order).ToList();

            // Check for duplicate orders
            var duplicates = members.GroupBy(m => m.order).Where(g => g.Count() > 1).ToList();
            if (duplicates.Any())
            {
                var dup = duplicates.First();
                throw new SysmacTypeException(
                    $"Type '{type.Name}' has duplicate [Order({dup.Key})] attributes on members: " +
                    string.Join(", ", dup.Select(d => d.member.Name)),
                    type);
            }

            int currentOffset = 0;

            foreach (var (member, order, memberType) in members)
            {
                // Calculate alignment requirement for this member
                int alignment = GetMemberAlignment(memberType);
                
                // Align currentOffset to the member's alignment boundary
                if (alignment > 1 && currentOffset % alignment != 0)
                {
                    currentOffset = ((currentOffset / alignment) + 1) * alignment;
                }

                var memberLayout = new MemberLayout
                {
                    Member = member,
                    Order = order,
                    Offset = currentOffset,
                    ClrType = memberType
                };

                // Determine size and type
                ResolveMemberLayout(memberLayout, member, memberType, type);

                layout.Members.Add(memberLayout);
                currentOffset += memberLayout.Size;
            }

            // Pad total size to 4-byte boundary (Omron struct alignment)
            if (currentOffset % 4 != 0)
            {
                currentOffset = ((currentOffset / 4) + 1) * 4;
            }

            layout.TotalSize = currentOffset;
            return layout;
        }

        /// <summary>
        /// Gets the alignment requirement for a member type.
        /// Omron PLCs use natural alignment: 2-byte for INT/UINT, 4-byte for DINT/REAL, etc.
        /// </summary>
        private static int GetMemberAlignment(Type memberType)
        {
            // Handle arrays - align based on element type
            if (memberType.IsArray)
            {
                return GetMemberAlignment(memberType.GetElementType());
            }

            // Handle enums - align based on underlying type
            if (memberType.IsEnum)
            {
                return GetMemberAlignment(Enum.GetUnderlyingType(memberType));
            }

            // Handle nested structs - 4-byte alignment
            if (memberType.IsClass || (memberType.IsValueType && !memberType.IsPrimitive && memberType != typeof(decimal)))
            {
                return 4;
            }

            // Primitive types
            if (memberType == typeof(bool) || memberType == typeof(byte) || memberType == typeof(sbyte))
                return 1;
            if (memberType == typeof(short) || memberType == typeof(ushort))
                return 2;
            if (memberType == typeof(int) || memberType == typeof(uint) || memberType == typeof(float))
                return 4;
            if (memberType == typeof(long) || memberType == typeof(ulong) || memberType == typeof(double))
                return 4; // Omron uses 4-byte alignment even for 8-byte types

            // Default to 4-byte alignment
            return 4;
        }

        private static void ResolveMemberLayout(MemberLayout memberLayout, MemberInfo member, Type memberType, Type parentType)
        {
            // Check for PlcType override
            var plcTypeAttr = member.GetCustomAttribute<PlcTypeAttribute>();

            // Handle arrays
            if (memberType.IsArray)
            {
                var elementType = memberType.GetElementType();
                int arrayLength = GetArrayLength(member, parentType);
                
                memberLayout.ArrayLength = arrayLength;
                memberLayout.PlcType = PlcDataType.Array;

                if (TryGetPrimitiveSize(elementType, out int elementSize, out _))
                {
                    memberLayout.Size = elementSize * arrayLength;
                }
                else
                {
                    // Nested struct array
                    var nestedLayout = Resolve(elementType);
                    memberLayout.NestedLayout = nestedLayout;
                    memberLayout.Size = nestedLayout.TotalSize * arrayLength;
                }
                return;
            }

            // Handle enums
            if (memberType.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(memberType);
                if (TryGetPrimitiveSize(underlyingType, out int enumSize, out PlcDataType enumPlcType))
                {
                    memberLayout.Size = enumSize;
                    memberLayout.PlcType = plcTypeAttr?.Type ?? enumPlcType;
                }
                return;
            }

            // Handle strings
            if (memberType == typeof(string))
            {
                var stringAttr = member.GetCustomAttribute<PlcStringAttribute>();
                if (stringAttr == null)
                {
                    throw new SysmacTypeException(
                        $"String member '{member.Name}' in type '{parentType.Name}' requires [PlcString(maxLength)] attribute.",
                        parentType, member.Name);
                }
                memberLayout.Size = stringAttr.MaxLength;
                memberLayout.StringMaxLength = stringAttr.MaxLength;
                memberLayout.PlcType = PlcDataType.String;
                return;
            }

            // Handle primitives
            if (TryGetPrimitiveSize(memberType, out int size, out PlcDataType plcType))
            {
                memberLayout.Size = size;
                memberLayout.PlcType = plcTypeAttr?.Type ?? plcType;
                return;
            }

            // Handle nested structs/classes
            if (memberType.IsClass || memberType.IsValueType)
            {
                var nestedLayout = Resolve(memberType);
                memberLayout.NestedLayout = nestedLayout;
                memberLayout.Size = nestedLayout.TotalSize;
                memberLayout.PlcType = PlcDataType.Struct;
                return;
            }

            throw new SysmacTypeException(
                $"Cannot determine size for member '{member.Name}' of type '{memberType.Name}' in '{parentType.Name}'.",
                parentType, member.Name);
        }

        private static int GetArrayLength(MemberInfo member, Type parentType)
        {
            // Check for ArrayLength attribute first
            var arrayLengthAttr = member.GetCustomAttribute<ArrayLengthAttribute>();
            if (arrayLengthAttr != null)
            {
                return arrayLengthAttr.Length;
            }

            // Try to infer from default instance
            try
            {
                var instance = Activator.CreateInstance(parentType);
                object value = null;
                
                if (member is PropertyInfo prop)
                    value = prop.GetValue(instance);
                else if (member is FieldInfo field)
                    value = field.GetValue(instance);

                if (value is Array array)
                {
                    return array.Length;
                }
            }
            catch
            {
                // Ignore - will throw below
            }

            throw new SysmacTypeException(
                $"Cannot determine array length for member '{member.Name}' in type '{parentType.Name}'. " +
                "Either initialize the array in the property/field declaration or add [ArrayLength(n)] attribute.",
                parentType, member.Name);
        }

        private static bool TryGetPrimitiveSize(Type type, out int size, out PlcDataType plcType)
        {
            if (type == typeof(bool))
            {
                size = 1;
                plcType = PlcDataType.Bool;
                return true;
            }
            if (type == typeof(sbyte))
            {
                size = 1;
                plcType = PlcDataType.SInt;
                return true;
            }
            if (type == typeof(byte))
            {
                size = 1;
                plcType = PlcDataType.USInt;
                return true;
            }
            if (type == typeof(short))
            {
                size = 2;
                plcType = PlcDataType.Int;
                return true;
            }
            if (type == typeof(ushort))
            {
                size = 2;
                plcType = PlcDataType.UInt;
                return true;
            }
            if (type == typeof(int))
            {
                size = 4;
                plcType = PlcDataType.DInt;
                return true;
            }
            if (type == typeof(uint))
            {
                size = 4;
                plcType = PlcDataType.UDInt;
                return true;
            }
            if (type == typeof(long))
            {
                size = 8;
                plcType = PlcDataType.LInt;
                return true;
            }
            if (type == typeof(ulong))
            {
                size = 8;
                plcType = PlcDataType.ULInt;
                return true;
            }
            if (type == typeof(float))
            {
                size = 4;
                plcType = PlcDataType.Real;
                return true;
            }
            if (type == typeof(double))
            {
                size = 8;
                plcType = PlcDataType.LReal;
                return true;
            }

            size = 0;
            plcType = PlcDataType.Struct;
            return false;
        }

        /// <summary>
        /// Returns a human-readable description of the type layout.
        /// </summary>
        /// <typeparam name="T">The type to describe.</typeparam>
        /// <param name="maxChunkSize">Optional chunk size for showing chunk boundaries.</param>
        /// <returns>A formatted string describing the layout.</returns>
        public static string Describe<T>(int? maxChunkSize = null)
        {
            return Describe(typeof(T), maxChunkSize);
        }

        /// <summary>
        /// Returns a human-readable description of the type layout.
        /// </summary>
        /// <param name="type">The type to describe.</param>
        /// <param name="maxChunkSize">Optional chunk size for showing chunk boundaries.</param>
        /// <returns>A formatted string describing the layout.</returns>
        public static string Describe(Type type, int? maxChunkSize = null)
        {
            var layout = Resolve(type);
            var lines = new List<string>();

            string header = $"{type.Name} (total: {layout.TotalSize} bytes";
            if (maxChunkSize.HasValue && layout.TotalSize > maxChunkSize.Value)
            {
                int chunks = (layout.TotalSize + maxChunkSize.Value - 1) / maxChunkSize.Value;
                header += $", {chunks} chunks @ {maxChunkSize.Value} bytes";
            }
            header += ")";
            lines.Add(header);

            foreach (var member in layout.Members)
            {
                string typeDesc = GetTypeDescription(member);
                string line = $"  Offset {member.Offset}: {member.Member.Name} ({typeDesc}, {member.Size} bytes)";
                
                if (maxChunkSize.HasValue)
                {
                    int chunk = member.Offset / maxChunkSize.Value + 1;
                    line += $" [Chunk {chunk}]";
                }
                
                lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string GetTypeDescription(MemberLayout member)
        {
            if (member.ArrayLength.HasValue)
            {
                var elementType = member.ClrType.GetElementType();
                return $"{elementType.Name}[{member.ArrayLength.Value}]";
            }
            if (member.NestedLayout != null)
            {
                return member.ClrType.Name;
            }
            return member.PlcType.ToString();
        }
    }
}
