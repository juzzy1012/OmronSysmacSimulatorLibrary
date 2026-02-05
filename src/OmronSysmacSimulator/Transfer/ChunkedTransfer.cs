using System;

namespace OmronSysmacSimulator.Transfer
{
    /// <summary>
    /// Handles chunked read/write operations for large data transfers.
    /// </summary>
    public class ChunkedTransfer
    {
        /// <summary>
        /// Default safe chunk size (conservative value).
        /// </summary>
        public const int DefaultChunkSize = 512;

        /// <summary>
        /// Minimum chunk size to test.
        /// </summary>
        public const int MinChunkSize = 256;

        /// <summary>
        /// Maximum chunk size to test during auto-detection.
        /// </summary>
        public const int MaxDetectionChunkSize = 4096;

        /// <summary>
        /// Gets the maximum chunk size in bytes.
        /// </summary>
        public int MaxChunkSize { get; private set; } = DefaultChunkSize;

        /// <summary>
        /// Gets whether the chunk size was auto-detected.
        /// </summary>
        public bool WasAutoDetected { get; private set; }

        /// <summary>
        /// Sets a fixed chunk size, disabling auto-detection.
        /// </summary>
        /// <param name="size">The chunk size in bytes.</param>
        public void SetFixedChunkSize(int size)
        {
            if (size < MinChunkSize)
                throw new ArgumentOutOfRangeException(nameof(size), $"Chunk size must be at least {MinChunkSize} bytes.");
            
            MaxChunkSize = size;
            WasAutoDetected = false;
        }

        /// <summary>
        /// Auto-detects the maximum reliable chunk size by testing increasing sizes.
        /// </summary>
        /// <param name="testChunkSize">Function that tests if a given chunk size works. Returns true on success.</param>
        public void DetectMaxChunkSize(Func<int, bool> testChunkSize)
        {
            // Test sizes in increasing order
            int[] candidates = { 256, 512, 1024, 2048, 4096 };
            int lastGood = MinChunkSize;

            foreach (var size in candidates)
            {
                try
                {
                    if (testChunkSize(size))
                    {
                        lastGood = size;
                    }
                    else
                    {
                        // Failed at this size, stop testing
                        break;
                    }
                }
                catch
                {
                    // Error at this size, stop testing
                    break;
                }
            }

            MaxChunkSize = lastGood;
            WasAutoDetected = true;
        }

        /// <summary>
        /// Reads data in chunks if necessary.
        /// </summary>
        /// <param name="baseAddress">The base memory address string.</param>
        /// <param name="totalSize">Total number of bytes to read.</param>
        /// <param name="readBytes">Function to read bytes at a given address with given size.</param>
        /// <returns>The complete data buffer.</returns>
        public byte[] ReadChunked(string baseAddress, int totalSize, Func<string, int, byte[]> readBytes)
        {
            if (totalSize <= MaxChunkSize)
            {
                // Single read
                return readBytes(baseAddress, totalSize);
            }

            // Multiple chunks
            var result = new byte[totalSize];
            int offset = 0;

            while (offset < totalSize)
            {
                int chunkSize = Math.Min(MaxChunkSize, totalSize - offset);
                string chunkAddress = CalculateOffsetAddress(baseAddress, offset);

                byte[] chunk = readBytes(chunkAddress, chunkSize);
                
                if (chunk == null || chunk.Length == 0)
                {
                    throw new InvalidOperationException($"Failed to read chunk at offset {offset}");
                }

                Buffer.BlockCopy(chunk, 0, result, offset, Math.Min(chunk.Length, chunkSize));
                offset += chunkSize;
            }

            return result;
        }

        /// <summary>
        /// Writes data in chunks if necessary.
        /// </summary>
        /// <param name="baseAddress">The base memory address string.</param>
        /// <param name="data">The data to write.</param>
        /// <param name="writeBytes">Function to write bytes at a given address.</param>
        public void WriteChunked(string baseAddress, byte[] data, Action<string, byte[]> writeBytes)
        {
            if (data.Length <= MaxChunkSize)
            {
                // Single write
                writeBytes(baseAddress, data);
                return;
            }

            // Multiple chunks
            int offset = 0;

            while (offset < data.Length)
            {
                int chunkSize = Math.Min(MaxChunkSize, data.Length - offset);
                string chunkAddress = CalculateOffsetAddress(baseAddress, offset);

                byte[] chunk = new byte[chunkSize];
                Buffer.BlockCopy(data, offset, chunk, 0, chunkSize);

                writeBytes(chunkAddress, chunk);
                offset += chunkSize;
            }
        }

        /// <summary>
        /// Calculates the number of chunks needed for a given size.
        /// </summary>
        /// <param name="totalSize">Total size in bytes.</param>
        /// <returns>Number of chunks.</returns>
        public int CalculateChunkCount(int totalSize)
        {
            if (totalSize <= MaxChunkSize)
                return 1;
            return (totalSize + MaxChunkSize - 1) / MaxChunkSize;
        }

        /// <summary>
        /// Calculates a memory address with a byte offset added.
        /// </summary>
        /// <param name="baseAddress">The base address string (e.g., "1,0,0,0,80").</param>
        /// <param name="byteOffset">The byte offset to add.</param>
        /// <returns>The new address string.</returns>
        /// <remarks>
        /// The address format from NexSocket is typically: "revision,?,?,byteOffset,size"
        /// We need to parse and modify the byte offset component.
        /// </remarks>
        public static string CalculateOffsetAddress(string baseAddress, int byteOffset)
        {
            if (byteOffset == 0)
                return baseAddress;

            // Address format: comma-separated values, byte offset is typically the 4th component
            // Example: "1,0,0,0,80" -> "1,0,0,10,80" (offset by 10 bytes)
            var parts = baseAddress.Split(',');
            
            if (parts.Length < 4)
            {
                // Unknown format - try simple append
                throw new InvalidOperationException(
                    $"Cannot calculate offset address. Unknown address format: {baseAddress}");
            }

            // The 4th component (index 3) is typically the byte offset
            if (int.TryParse(parts[3], out int currentOffset))
            {
                parts[3] = (currentOffset + byteOffset).ToString();
                return string.Join(",", parts);
            }

            throw new InvalidOperationException(
                $"Cannot parse byte offset from address: {baseAddress}");
        }
    }
}
