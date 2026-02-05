using System;
using System.Text;
using OmronSysmacSimulator.Exceptions;

namespace OmronSysmacSimulator.Converters
{
    /// <summary>
    /// Serializes and deserializes .NET objects to/from PLC byte format.
    /// </summary>
    public static class PlcSerializer
    {
        /// <summary>
        /// Serializes an object to PLC byte format.
        /// </summary>
        /// <typeparam name="T">The type to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The byte array representation.</returns>
        public static byte[] Serialize<T>(T value)
        {
            return Serialize(value, typeof(T));
        }

        /// <summary>
        /// Serializes an object to PLC byte format.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="type">The type to serialize as.</param>
        /// <returns>The byte array representation.</returns>
        public static byte[] Serialize(object value, Type type)
        {
            var layout = TypeLayoutResolver.Resolve(type);
            var buffer = new byte[layout.TotalSize];
            
            if (layout.Members.Count == 0)
            {
                // Primitive type - serialize directly
                SerializePrimitive(value, type, buffer, 0);
            }
            else
            {
                // Complex type - serialize each member
                foreach (var member in layout.Members)
                {
                    object memberValue = member.GetValue(value);
                    SerializeMember(memberValue, member, buffer);
                }
            }
            
            return buffer;
        }

        /// <summary>
        /// Deserializes a byte array to an object.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="data">The byte array.</param>
        /// <returns>The deserialized object.</returns>
        public static T Deserialize<T>(byte[] data)
        {
            return (T)Deserialize(data, typeof(T));
        }

        /// <summary>
        /// Deserializes a byte array to an object.
        /// </summary>
        /// <param name="data">The byte array.</param>
        /// <param name="type">The type to deserialize to.</param>
        /// <returns>The deserialized object.</returns>
        public static object Deserialize(byte[] data, Type type)
        {
            var layout = TypeLayoutResolver.Resolve(type);

            if (data.Length < layout.TotalSize)
            {
                throw new SysmacTypeException(
                    $"Buffer too small to deserialize {type.Name}. Expected {layout.TotalSize} bytes, got {data.Length}.",
                    type);
            }

            if (layout.Members.Count == 0)
            {
                // Primitive type - deserialize directly
                return DeserializePrimitive(data, 0, type);
            }

            // Complex type - create instance and deserialize each member
            object instance = Activator.CreateInstance(type);

            foreach (var member in layout.Members)
            {
                object memberValue = DeserializeMember(data, member);
                member.SetValue(instance, memberValue);
            }

            return instance;
        }

        /// <summary>
        /// Gets the serialized size in bytes for a type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The size in bytes.</returns>
        public static int GetSize<T>()
        {
            return TypeLayoutResolver.GetSize<T>();
        }

        /// <summary>
        /// Gets the serialized size in bytes for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The size in bytes.</returns>
        public static int GetSize(Type type)
        {
            return TypeLayoutResolver.GetSize(type);
        }

        /// <summary>
        /// Validates that a type can be serialized.
        /// </summary>
        /// <typeparam name="T">The type to validate.</typeparam>
        /// <returns>Null if valid, error message if invalid.</returns>
        public static string Validate<T>()
        {
            try
            {
                TypeLayoutResolver.Resolve<T>();
                return null;
            }
            catch (SysmacTypeException ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Returns a human-readable description of the type layout.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="maxChunkSize">Optional chunk size for showing chunk boundaries.</param>
        /// <returns>A formatted string describing the layout.</returns>
        public static string Describe<T>(int? maxChunkSize = null)
        {
            return TypeLayoutResolver.Describe<T>(maxChunkSize);
        }

        private static void SerializeMember(object value, MemberLayout member, byte[] buffer)
        {
            int offset = member.Offset;

            // Handle null values
            if (value == null)
            {
                // Leave as zeros
                return;
            }

            // Handle arrays
            if (member.ArrayLength.HasValue)
            {
                SerializeArray(value, member, buffer, offset);
                return;
            }

            // Handle strings
            if (member.PlcType == PlcDataType.String)
            {
                SerializeString((string)value, buffer, offset, member.StringMaxLength.Value);
                return;
            }

            // Handle nested structs
            if (member.NestedLayout != null)
            {
                byte[] nestedBytes = Serialize(value, member.ClrType);
                Buffer.BlockCopy(nestedBytes, 0, buffer, offset, nestedBytes.Length);
                return;
            }

            // Handle enums
            if (member.ClrType.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(member.ClrType);
                var underlyingValue = Convert.ChangeType(value, underlyingType);
                SerializePrimitive(underlyingValue, underlyingType, buffer, offset);
                return;
            }

            // Handle primitives
            SerializePrimitive(value, member.ClrType, buffer, offset);
        }

        private static void SerializeArray(object arrayObj, MemberLayout member, byte[] buffer, int offset)
        {
            var array = (Array)arrayObj;
            var elementType = member.ClrType.GetElementType();
            int elementSize = member.Size / member.ArrayLength.Value;

            int count = Math.Min(array.Length, member.ArrayLength.Value);
            
            for (int i = 0; i < count; i++)
            {
                object element = array.GetValue(i);
                int elementOffset = offset + (i * elementSize);

                if (element == null)
                    continue;

                if (member.NestedLayout != null)
                {
                    byte[] elementBytes = Serialize(element, elementType);
                    Buffer.BlockCopy(elementBytes, 0, buffer, elementOffset, elementBytes.Length);
                }
                else if (elementType.IsEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType(elementType);
                    var underlyingValue = Convert.ChangeType(element, underlyingType);
                    SerializePrimitive(underlyingValue, underlyingType, buffer, elementOffset);
                }
                else
                {
                    SerializePrimitive(element, elementType, buffer, elementOffset);
                }
            }
        }

        private static void SerializePrimitive(object value, Type type, byte[] buffer, int offset)
        {
            if (type == typeof(bool))
            {
                buffer[offset] = (bool)value ? (byte)1 : (byte)0;
            }
            else if (type == typeof(sbyte))
            {
                buffer[offset] = unchecked((byte)(sbyte)value);
            }
            else if (type == typeof(byte))
            {
                buffer[offset] = (byte)value;
            }
            else if (type == typeof(short))
            {
                WriteInt16(buffer, offset, (short)value);
            }
            else if (type == typeof(ushort))
            {
                WriteUInt16(buffer, offset, (ushort)value);
            }
            else if (type == typeof(int))
            {
                WriteInt32(buffer, offset, (int)value);
            }
            else if (type == typeof(uint))
            {
                WriteUInt32(buffer, offset, (uint)value);
            }
            else if (type == typeof(long))
            {
                WriteInt64(buffer, offset, (long)value);
            }
            else if (type == typeof(ulong))
            {
                WriteUInt64(buffer, offset, (ulong)value);
            }
            else if (type == typeof(float))
            {
                WriteSingle(buffer, offset, (float)value);
            }
            else if (type == typeof(double))
            {
                WriteDouble(buffer, offset, (double)value);
            }
            else
            {
                throw new SysmacTypeException($"Cannot serialize primitive type: {type.Name}", type);
            }
        }

        private static void SerializeString(string value, byte[] buffer, int offset, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return;

            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            int copyLength = Math.Min(stringBytes.Length, maxLength);
            Buffer.BlockCopy(stringBytes, 0, buffer, offset, copyLength);
            // Remaining bytes are already zero (null padding)
        }

        private static object DeserializeMember(byte[] data, MemberLayout member)
        {
            int offset = member.Offset;

            // Handle arrays
            if (member.ArrayLength.HasValue)
            {
                return DeserializeArray(data, member, offset);
            }

            // Handle strings
            if (member.PlcType == PlcDataType.String)
            {
                return DeserializeString(data, offset, member.StringMaxLength.Value);
            }

            // Handle nested structs
            if (member.NestedLayout != null)
            {
                byte[] nestedData = new byte[member.Size];
                Buffer.BlockCopy(data, offset, nestedData, 0, member.Size);
                return Deserialize(nestedData, member.ClrType);
            }

            // Handle enums
            if (member.ClrType.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(member.ClrType);
                object underlyingValue = DeserializePrimitive(data, offset, underlyingType);
                return Enum.ToObject(member.ClrType, underlyingValue);
            }

            // Handle primitives
            return DeserializePrimitive(data, offset, member.ClrType);
        }

        private static object DeserializeArray(byte[] data, MemberLayout member, int offset)
        {
            var elementType = member.ClrType.GetElementType();
            int arrayLength = member.ArrayLength.Value;
            int elementSize = member.Size / arrayLength;

            var array = Array.CreateInstance(elementType, arrayLength);

            for (int i = 0; i < arrayLength; i++)
            {
                int elementOffset = offset + (i * elementSize);
                object element;

                if (member.NestedLayout != null)
                {
                    byte[] elementData = new byte[elementSize];
                    Buffer.BlockCopy(data, elementOffset, elementData, 0, elementSize);
                    element = Deserialize(elementData, elementType);
                }
                else if (elementType.IsEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType(elementType);
                    object underlyingValue = DeserializePrimitive(data, elementOffset, underlyingType);
                    element = Enum.ToObject(elementType, underlyingValue);
                }
                else
                {
                    element = DeserializePrimitive(data, elementOffset, elementType);
                }

                array.SetValue(element, i);
            }

            return array;
        }

        private static object DeserializePrimitive(byte[] data, int offset, Type type)
        {
            if (type == typeof(bool))
            {
                return data[offset] != 0;
            }
            if (type == typeof(sbyte))
            {
                return unchecked((sbyte)data[offset]);
            }
            if (type == typeof(byte))
            {
                return data[offset];
            }
            if (type == typeof(short))
            {
                return ReadInt16(data, offset);
            }
            if (type == typeof(ushort))
            {
                return ReadUInt16(data, offset);
            }
            if (type == typeof(int))
            {
                return ReadInt32(data, offset);
            }
            if (type == typeof(uint))
            {
                return ReadUInt32(data, offset);
            }
            if (type == typeof(long))
            {
                return ReadInt64(data, offset);
            }
            if (type == typeof(ulong))
            {
                return ReadUInt64(data, offset);
            }
            if (type == typeof(float))
            {
                return ReadSingle(data, offset);
            }
            if (type == typeof(double))
            {
                return ReadDouble(data, offset);
            }

            throw new SysmacTypeException($"Cannot deserialize primitive type: {type.Name}", type);
        }

        private static string DeserializeString(byte[] data, int offset, int maxLength)
        {
            // Find null terminator or use max length
            int length = 0;
            while (length < maxLength && data[offset + length] != 0)
            {
                length++;
            }

            if (length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(data, offset, length);
        }

        // Little-endian read/write methods
        private static void WriteInt16(byte[] buffer, int offset, short value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt64(byte[] buffer, int offset, long value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
        }

        private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
        }

        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        private static void WriteDouble(byte[] buffer, int offset, double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, buffer, offset, 8);
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return (short)(data[offset] | (data[offset + 1] << 8));
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static long ReadInt64(byte[] data, int offset)
        {
            uint lo = ReadUInt32(data, offset);
            uint hi = ReadUInt32(data, offset + 4);
            return (long)((ulong)hi << 32 | lo);
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            uint lo = ReadUInt32(data, offset);
            uint hi = ReadUInt32(data, offset + 4);
            return (ulong)hi << 32 | lo;
        }

        private static float ReadSingle(byte[] data, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToSingle(data, offset);
            }
            byte[] bytes = new byte[4];
            Buffer.BlockCopy(data, offset, bytes, 0, 4);
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static double ReadDouble(byte[] data, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToDouble(data, offset);
            }
            byte[] bytes = new byte[8];
            Buffer.BlockCopy(data, offset, bytes, 0, 8);
            Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }
    }
}
