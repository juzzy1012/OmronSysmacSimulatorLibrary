namespace OmronSysmacSimulator
{
    /// <summary>
    /// PLC data types supported by the Sysmac simulator.
    /// </summary>
    public enum PlcDataType
    {
        /// <summary>Boolean (1 byte: 0x00 = false, 0x01 = true)</summary>
        Bool,
        
        /// <summary>Signed 8-bit integer (-128 to 127)</summary>
        SInt,
        
        /// <summary>Signed 16-bit integer (-32768 to 32767)</summary>
        Int,
        
        /// <summary>Signed 32-bit integer</summary>
        DInt,
        
        /// <summary>Signed 64-bit integer</summary>
        LInt,
        
        /// <summary>Unsigned 8-bit integer (0 to 255)</summary>
        USInt,
        
        /// <summary>Unsigned 16-bit integer (0 to 65535)</summary>
        UInt,
        
        /// <summary>Unsigned 32-bit integer</summary>
        UDInt,
        
        /// <summary>Unsigned 64-bit integer</summary>
        ULInt,
        
        /// <summary>32-bit floating point (IEEE 754)</summary>
        Real,
        
        /// <summary>64-bit floating point (IEEE 754)</summary>
        LReal,
        
        /// <summary>String (UTF-8 encoded, null-padded)</summary>
        String,
        
        /// <summary>Nested structure type</summary>
        Struct,
        
        /// <summary>Array of elements</summary>
        Array
    }
}
