using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmronSysmacSimulator.Converters;
using OmronSysmacSimulator.Exceptions;
using OmronSysmacSimulator.Models;
using OmronSysmacSimulator.Native;
using OmronSysmacSimulator.Transfer;

namespace OmronSysmacSimulator
{
    /// <summary>
    /// Client for communicating with the Omron Sysmac Studio Simulator.
    /// </summary>
    public class SysmacSimulatorClient : IDisposable
    {
        private readonly SysmacConnectionOptions _options;
        private readonly NexSocketNative _nativeSocket;
        private readonly ChunkedTransfer _chunkedTransfer;
        private readonly Dictionary<string, SimulatorVariable> _variableCache;
        private readonly object _syncLock = new object();
        
        private short _handle;
        private bool _disposed;
        private bool _connected;

        /// <summary>
        /// Gets whether the client is connected to the simulator.
        /// </summary>
        public bool IsConnected => _connected;

        /// <summary>
        /// Gets the detected maximum chunk size in bytes.
        /// </summary>
        public int DetectedMaxChunkSize => _chunkedTransfer.MaxChunkSize;

        /// <summary>
        /// Gets whether the chunk size was auto-detected.
        /// </summary>
        public bool ChunkSizeWasAutoDetected => _chunkedTransfer.WasAutoDetected;

        /// <summary>
        /// Creates a new SysmacSimulatorClient with default options.
        /// </summary>
        public SysmacSimulatorClient() : this(new SysmacConnectionOptions())
        {
        }

        /// <summary>
        /// Creates a new SysmacSimulatorClient with custom options.
        /// </summary>
        /// <param name="options">Connection options.</param>
        public SysmacSimulatorClient(SysmacConnectionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _nativeSocket = new NexSocketNative();
            _chunkedTransfer = new ChunkedTransfer();
            _variableCache = new Dictionary<string, SimulatorVariable>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Connects to the Sysmac simulator.
        /// </summary>
        /// <exception cref="SysmacConnectionException">Thrown when connection fails.</exception>
        public void Connect()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SysmacSimulatorClient));

            if (_connected)
                return;

            try
            {
                // Load and initialize the native DLL
                _nativeSocket.Load(_options.DllPath);
                _nativeSocket.Initialize();

                // Connect to the simulator
                _nativeSocket.Connect(ref _handle, _options.IpAddress, _options.Port);
                _connected = true;

                // Configure chunking
                if (_options.MaxChunkSizeOverride > 0)
                {
                    _chunkedTransfer.SetFixedChunkSize(_options.MaxChunkSizeOverride);
                }
                else if (!_options.SkipChunkDetection)
                {
                    // Auto-detect max chunk size
                    DetectChunkSize();
                }
            }
            catch (Exception ex) when (!(ex is SysmacException))
            {
                throw new SysmacConnectionException(
                    $"Failed to connect to simulator at {_options.IpAddress}:{_options.Port}. {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Connects to the Sysmac simulator asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Connect(), cancellationToken);
        }

        /// <summary>
        /// Disconnects from the simulator.
        /// </summary>
        public void Disconnect()
        {
            if (!_connected)
                return;

            try
            {
                _nativeSocket.Close(_handle);
                _nativeSocket.Terminate();
            }
            finally
            {
                _connected = false;
            }
        }

        /// <summary>
        /// Reads a value from a PLC variable.
        /// Works with both primitive types and complex types decorated with [Order] attributes.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <returns>The deserialized value.</returns>
        public T Read<T>(string variableName)
        {
            EnsureConnected();

            var varInfo = GetOrCacheVariableInfo(variableName);
            int totalSize = PlcSerializer.GetSize<T>();

            byte[] data = ReadBytesInternal(varInfo, totalSize);
            return PlcSerializer.Deserialize<T>(data);
        }

        /// <summary>
        /// Reads a value from a PLC variable asynchronously.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The deserialized value.</returns>
        public Task<T> ReadAsync<T>(string variableName, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Read<T>(variableName), cancellationToken);
        }

        /// <summary>
        /// Writes a value to a PLC variable.
        /// Works with both primitive types and complex types decorated with [Order] attributes.
        /// </summary>
        /// <typeparam name="T">The type to write.</typeparam>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="value">The value to write.</param>
        public void Write<T>(string variableName, T value)
        {
            EnsureConnected();

            var varInfo = GetOrCacheVariableInfo(variableName);
            byte[] data = PlcSerializer.Serialize(value);

            WriteBytesInternal(varInfo, data);
        }

        /// <summary>
        /// Writes a value to a PLC variable asynchronously.
        /// </summary>
        /// <typeparam name="T">The type to write.</typeparam>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task WriteAsync<T>(string variableName, T value, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Write(variableName, value), cancellationToken);
        }

        /// <summary>
        /// Reads raw bytes from a PLC variable.
        /// </summary>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The raw byte array.</returns>
        public byte[] ReadBytes(string variableName, int length)
        {
            EnsureConnected();

            var varInfo = GetOrCacheVariableInfo(variableName);
            return ReadBytesInternal(varInfo, length);
        }

        /// <summary>
        /// Reads raw bytes from a PLC variable asynchronously.
        /// </summary>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The raw byte array.</returns>
        public Task<byte[]> ReadBytesAsync(string variableName, int length, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ReadBytes(variableName, length), cancellationToken);
        }

        /// <summary>
        /// Writes raw bytes to a PLC variable.
        /// </summary>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="data">The bytes to write.</param>
        public void WriteBytes(string variableName, byte[] data)
        {
            EnsureConnected();

            var varInfo = GetOrCacheVariableInfo(variableName);
            WriteBytesInternal(varInfo, data);
        }

        /// <summary>
        /// Writes raw bytes to a PLC variable asynchronously.
        /// </summary>
        /// <param name="variableName">The name of the PLC variable.</param>
        /// <param name="data">The bytes to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task WriteBytesAsync(string variableName, byte[] data, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => WriteBytes(variableName, data), cancellationToken);
        }

        /// <summary>
        /// Imports variable definitions from a Sysmac Studio export file.
        /// </summary>
        /// <param name="filePath">Path to the exported variables file.</param>
        public void ImportVariablesFromFile(string filePath)
        {
            var variables = VariableImporter.ParseFile(filePath);
            foreach (var (name, type) in variables)
            {
                // Pre-fetch variable info if connected
                if (_connected)
                {
                    try
                    {
                        GetOrCacheVariableInfo(name);
                    }
                    catch
                    {
                        // Variable might not exist in current project
                    }
                }
            }
        }

        /// <summary>
        /// Gets information about a PLC variable.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <returns>The variable metadata.</returns>
        public SimulatorVariable GetVariableInfo(string variableName)
        {
            EnsureConnected();
            return GetOrCacheVariableInfo(variableName);
        }

        /// <summary>
        /// Clears the variable information cache.
        /// </summary>
        public void ClearVariableCache()
        {
            lock (_syncLock)
            {
                _variableCache.Clear();
            }
        }

        /// <summary>
        /// Disposes the client and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Disconnect();
            _nativeSocket?.Dispose();
            _disposed = true;
        }

        #region Private Methods

        private void EnsureConnected()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SysmacSimulatorClient));
            if (!_connected)
                throw new SysmacConnectionException("Not connected to simulator. Call Connect() first.");
        }

        private void DetectChunkSize()
        {
            // For now, use default since we can't easily test without a known variable
            // In practice, we'd probe with a test read
            _chunkedTransfer.SetFixedChunkSize(ChunkedTransfer.DefaultChunkSize);
        }

        private SimulatorVariable GetOrCacheVariableInfo(string variableName)
        {
            lock (_syncLock)
            {
                if (_variableCache.TryGetValue(variableName, out var cached))
                {
                    return cached;
                }
            }

            // Fetch from simulator
            var varInfo = FetchVariableInfo(variableName);

            lock (_syncLock)
            {
                _variableCache[variableName] = varInfo;
            }

            return varInfo;
        }

        private SimulatorVariable FetchVariableInfo(string variableName)
        {
            string command = $"GetVarAddrText 1 VAR://{variableName}";
            var (response, error) = SendCommand(command);

            if (error != null)
            {
                throw new SysmacVariableException(
                    $"Failed to get variable info for '{variableName}': {error}", variableName);
            }

            if (response.Count < 3)
            {
                throw new SysmacVariableException(
                    $"Invalid response for variable '{variableName}'. Variable may not exist. Got {response.Count} response chunks.", variableName);
            }

            try
            {
                // Python: revision = response[0].decode('utf-8')
                // Python: address = response[2].decode('utf-8')[:-1]
                // The [:-1] removes the last character (typically newline)
                string revisionRaw = Encoding.UTF8.GetString(response[0]);
                string addressRaw = Encoding.UTF8.GetString(response[2]);
                
                string revision = revisionRaw.TrimEnd('\0');
                // Match Python's [:-1] - remove exactly one character from the end
                string address = addressRaw.Length > 0 
                    ? addressRaw.Substring(0, addressRaw.Length - 1) 
                    : addressRaw;
                
                // Parse size from address (last component is size in bits)
                int size = ParseSizeFromAddress(address);

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SysmacSimulator] Variable '{variableName}':");
                System.Diagnostics.Debug.WriteLine($"  Revision raw bytes: {BitConverter.ToString(response[0])}");
                System.Diagnostics.Debug.WriteLine($"  Address raw bytes: {BitConverter.ToString(response[2])}");
                System.Diagnostics.Debug.WriteLine($"  Revision: '{revision}'");
                System.Diagnostics.Debug.WriteLine($"  Address: '{address}'");
                System.Diagnostics.Debug.WriteLine($"  Size: {size} bytes");
#endif

                return new SimulatorVariable(revision, address, size);
            }
            catch (Exception ex)
            {
                throw new SysmacVariableException(
                    $"Failed to parse variable info for '{variableName}': {ex.Message}", variableName);
            }
        }

        private int ParseSizeFromAddress(string address)
        {
            // Address format: "x,x,x,offset,sizeBits"
            // Size is typically in bits at the end
            var parts = address.Split(',');
            if (parts.Length >= 5)
            {
                if (int.TryParse(parts[4].Trim(), out int sizeBits))
                {
                    return sizeBits / 8;
                }
            }
            return 0; // Unknown size
        }

        private byte[] ReadBytesInternal(SimulatorVariable varInfo, int length)
        {
            return _chunkedTransfer.ReadChunked(
                varInfo.Address,
                length,
                (address, chunkSize) => ReadSingleChunk(varInfo.Revision, address, chunkSize));
        }

        private byte[] ReadSingleChunk(string revision, string address, int size)
        {
            // Command format: AsyncReadMemText revision 1 address,2
            string command = $"AsyncReadMemText {revision} 1 {address},2";
            var (response, error) = SendCommand(command);

            if (error != null)
            {
                throw new SysmacCommunicationException($"Read failed: {error}", error);
            }

            if (response.Count == 0 || response[0].Length == 0)
            {
                throw new SysmacCommunicationException("Read returned no data.");
            }

            return response[0];
        }

        private void WriteBytesInternal(SimulatorVariable varInfo, byte[] data)
        {
            _chunkedTransfer.WriteChunked(
                varInfo.Address,
                data,
                (address, chunk) => WriteSingleChunk(varInfo.Revision, address, chunk));
        }

        private void WriteSingleChunk(string revision, string address, byte[] data)
        {
            // Command format: AsyncWriteMemText revision 1 address,2,hexData
            string hexData = BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
            string command = $"AsyncWriteMemText {revision} 1 {address},2,{hexData}";
            
            var (_, error) = SendCommand(command);

            if (error != null)
            {
                throw new SysmacCommunicationException($"Write failed: {error}", error);
            }
        }

        private (List<byte[]> response, string error) SendCommand(string command)
        {
            lock (_syncLock)
            {
                var response = new List<byte[]>();
                string error = null;

                byte[] buffer = new byte[512];
                
                _nativeSocket.Send(_handle, command);

                while (true)
                {
                    int bytesReceived = _nativeSocket.Receive(_handle, buffer);
                    
                    if (bytesReceived == 0)
                    {
                        // Complete
                        break;
                    }
                    else if (bytesReceived < 0)
                    {
                        // Error
                        error = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                        break;
                    }
                    else
                    {
                        byte[] chunk = new byte[bytesReceived];
                        Buffer.BlockCopy(buffer, 0, chunk, 0, bytesReceived);
                        response.Add(chunk);
                    }
                }

                return (response, error);
            }
        }

        #endregion
    }
}
