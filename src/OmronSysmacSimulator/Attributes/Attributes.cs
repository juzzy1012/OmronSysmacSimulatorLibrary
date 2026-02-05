using System;

namespace OmronSysmacSimulator.Attributes
{
    /// <summary>
    /// Specifies the order of a member within a PLC structure for serialization.
    /// Members are serialized in ascending order value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class OrderAttribute : Attribute
    {
        /// <summary>
        /// Gets the order value for this member.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Creates a new OrderAttribute with the specified order value.
        /// </summary>
        /// <param name="order">The order of this member in the serialization sequence (0-based).</param>
        public OrderAttribute(int order)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), "Order must be non-negative.");
            Value = order;
        }
    }

    /// <summary>
    /// Optionally overrides the inferred PLC data type for a member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class PlcTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets the PLC data type.
        /// </summary>
        public PlcDataType Type { get; }

        /// <summary>
        /// Creates a new PlcTypeAttribute with the specified PLC type.
        /// </summary>
        /// <param name="type">The PLC data type to use for this member.</param>
        public PlcTypeAttribute(PlcDataType type)
        {
            Type = type;
        }
    }

    /// <summary>
    /// Specifies the length of an array member when it cannot be inferred from the initializer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ArrayLengthAttribute : Attribute
    {
        /// <summary>
        /// Gets the array length.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Creates a new ArrayLengthAttribute with the specified length.
        /// </summary>
        /// <param name="length">The number of elements in the array.</param>
        public ArrayLengthAttribute(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Array length must be positive.");
            Length = length;
        }
    }

    /// <summary>
    /// Specifies the maximum length of a PLC string member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class PlcStringAttribute : Attribute
    {
        /// <summary>
        /// Gets the maximum string length in bytes.
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// Creates a new PlcStringAttribute with the specified maximum length.
        /// </summary>
        /// <param name="maxLength">The maximum length of the string in bytes.</param>
        public PlcStringAttribute(int maxLength)
        {
            if (maxLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
            MaxLength = maxLength;
        }
    }
}
