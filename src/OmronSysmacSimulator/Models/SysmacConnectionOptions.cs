namespace OmronSysmacSimulator.Models
{
    /// <summary>
    /// Configuration options for connecting to the Sysmac simulator.
    /// </summary>
    public class SysmacConnectionOptions
    {
        /// <summary>
        /// Gets or sets the IP address of the simulator. Default: "127.0.0.1"
        /// </summary>
        public string IpAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Gets or sets the port number. Default: 7000
        /// </summary>
        public int Port { get; set; } = 7000;

        /// <summary>
        /// Gets or sets the path to NexSocket.dll.
        /// Default: "C:\Program Files\OMRON\Sysmac Studio\MATLAB\Win64\NexSocket.dll"
        /// </summary>
        public string DllPath { get; set; } = @"C:\Program Files\OMRON\Sysmac Studio\MATLAB\Win64\NexSocket.dll";

        /// <summary>
        /// Gets or sets the override for maximum chunk size in bytes.
        /// Set to 0 for auto-detection (default).
        /// </summary>
        public int MaxChunkSizeOverride { get; set; } = 0;

        /// <summary>
        /// Gets or sets the timeout in milliseconds for each operation.
        /// Default: 5000ms
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Gets or sets whether to skip chunk size auto-detection.
        /// When true, uses a conservative default of 512 bytes.
        /// </summary>
        public bool SkipChunkDetection { get; set; } = false;

        /// <summary>
        /// Creates a new instance with default settings.
        /// </summary>
        public SysmacConnectionOptions()
        {
        }

        /// <summary>
        /// Creates a new instance with custom IP and port.
        /// </summary>
        /// <param name="ipAddress">The IP address of the simulator.</param>
        /// <param name="port">The port number.</param>
        public SysmacConnectionOptions(string ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;
        }
    }
}
