using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OmronSysmacSimulator.Native
{
    /// <summary>
    /// P/Invoke wrapper for NexSocket.dll functions.
    /// </summary>
    internal class NexSocketNative : IDisposable
    {
        private IntPtr _dllHandle;
        private bool _disposed;
        private bool _initialized;

        // Delegate types for the native functions
        private delegate int NexSock_initializeDelegate();
        private delegate int NexSock_terminateDelegate();
        private delegate int NexSockClient_connectDelegate(ref short handle, byte[] ipAddress, short port);
        private delegate int NexSock_closeDelegate(short handle);
        private delegate int NexSock_sendDelegate(short handle, byte[] data, int length);
        private delegate int NexSock_receiveDelegate(short handle, byte[] buffer, int bufferSize);

        // Cached delegates
        private NexSock_initializeDelegate _initialize;
        private NexSock_terminateDelegate _terminate;
        private NexSockClient_connectDelegate _connect;
        private NexSock_closeDelegate _close;
        private NexSock_sendDelegate _send;
        private NexSock_receiveDelegate _receive;

        /// <summary>
        /// Gets whether the native library has been loaded.
        /// </summary>
        public bool IsLoaded => _dllHandle != IntPtr.Zero;

        /// <summary>
        /// Loads the NexSocket.dll from the specified path.
        /// </summary>
        /// <param name="dllPath">Full path to NexSocket.dll</param>
        public void Load(string dllPath)
        {
            if (_dllHandle != IntPtr.Zero)
                return;

            if (!File.Exists(dllPath))
                throw new FileNotFoundException($"NexSocket.dll not found at: {dllPath}", dllPath);

            _dllHandle = LoadLibrary(dllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to load NexSocket.dll. Error code: {error}");
            }

            // Load function pointers
            _initialize = GetDelegate<NexSock_initializeDelegate>("NexSock_initialize");
            _terminate = GetDelegate<NexSock_terminateDelegate>("NexSock_terminate");
            _connect = GetDelegate<NexSockClient_connectDelegate>("NexSockClient_connect");
            _close = GetDelegate<NexSock_closeDelegate>("NexSock_close");
            _send = GetDelegate<NexSock_sendDelegate>("NexSock_send");
            _receive = GetDelegate<NexSock_receiveDelegate>("NexSock_receive");
        }

        private T GetDelegate<T>(string functionName) where T : Delegate
        {
            IntPtr procAddress = GetProcAddress(_dllHandle, functionName);
            if (procAddress == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to find function '{functionName}' in NexSocket.dll. Error code: {error}");
            }
            return Marshal.GetDelegateForFunctionPointer<T>(procAddress);
        }

        /// <summary>
        /// Initializes the NexSocket library.
        /// </summary>
        public void Initialize()
        {
            EnsureLoaded();
            int result = _initialize();
            if (result < 0)
                throw new InvalidOperationException($"NexSock_initialize failed with code: {result}");
            _initialized = true;
        }

        /// <summary>
        /// Terminates the NexSocket library.
        /// </summary>
        public void Terminate()
        {
            if (_initialized && _dllHandle != IntPtr.Zero)
            {
                _terminate();
                _initialized = false;
            }
        }

        /// <summary>
        /// Connects to the simulator.
        /// </summary>
        /// <param name="handle">Output: connection handle</param>
        /// <param name="ipAddress">IP address string</param>
        /// <param name="port">Port number</param>
        public void Connect(ref short handle, string ipAddress, int port)
        {
            EnsureLoaded();
            byte[] ipBytes = System.Text.Encoding.UTF8.GetBytes(ipAddress + "\0");
            int result = _connect(ref handle, ipBytes, (short)port);
            if (result < 0)
                throw new InvalidOperationException($"NexSockClient_connect failed with code: {result}");
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="handle">Connection handle</param>
        public void Close(short handle)
        {
            if (_dllHandle != IntPtr.Zero)
            {
                _close(handle);
            }
        }

        /// <summary>
        /// Sends data through the socket.
        /// </summary>
        /// <param name="handle">Connection handle</param>
        /// <param name="data">Data to send</param>
        /// <returns>Number of bytes sent</returns>
        public int Send(short handle, byte[] data)
        {
            EnsureLoaded();
            return _send(handle, data, data.Length);
        }

        /// <summary>
        /// Sends a string command through the socket.
        /// </summary>
        /// <param name="handle">Connection handle</param>
        /// <param name="command">Command string</param>
        /// <returns>Number of bytes sent</returns>
        public int Send(short handle, string command)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command);
            return Send(handle, data);
        }

        /// <summary>
        /// Receives data from the socket.
        /// </summary>
        /// <param name="handle">Connection handle</param>
        /// <param name="buffer">Buffer to receive data into</param>
        /// <returns>Number of bytes received, 0 if complete, negative on error</returns>
        public int Receive(short handle, byte[] buffer)
        {
            EnsureLoaded();
            return _receive(handle, buffer, buffer.Length);
        }

        private void EnsureLoaded()
        {
            if (_dllHandle == IntPtr.Zero)
                throw new InvalidOperationException("NexSocket.dll has not been loaded. Call Load() first.");
        }

        /// <summary>
        /// Disposes the native library handle.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Terminate();
                if (_dllHandle != IntPtr.Zero)
                {
                    FreeLibrary(_dllHandle);
                    _dllHandle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        // Native Windows API imports
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    }
}
