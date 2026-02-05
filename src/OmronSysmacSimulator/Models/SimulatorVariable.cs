namespace OmronSysmacSimulator.Models
{
    /// <summary>
    /// Represents metadata about a PLC variable retrieved from the simulator.
    /// </summary>
    public class SimulatorVariable
    {
        /// <summary>
        /// Gets or sets the variable revision identifier.
        /// </summary>
        public string Revision { get; set; }

        /// <summary>
        /// Gets or sets the memory address string.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Gets or sets the PLC data type.
        /// </summary>
        public PlcDataType DataType { get; set; }

        /// <summary>
        /// Gets or sets the low index for array types.
        /// </summary>
        public int? LowIndex { get; set; }

        /// <summary>
        /// Gets or sets the high index for array types.
        /// </summary>
        public int? HighIndex { get; set; }

        /// <summary>
        /// Creates a new SimulatorVariable.
        /// </summary>
        public SimulatorVariable()
        {
        }

        /// <summary>
        /// Creates a new SimulatorVariable with the specified parameters.
        /// </summary>
        public SimulatorVariable(string revision, string address, int size)
        {
            Revision = revision;
            Address = address;
            Size = size;
        }
    }
}
