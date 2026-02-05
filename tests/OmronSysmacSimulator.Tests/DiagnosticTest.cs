using System;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using OmronSysmacSimulator;

namespace OmronSysmacSimulator.Tests
{
    /// <summary>
    /// Diagnostic test to help debug simulator communication issues.
    /// Run with: dotnet test --filter "FullyQualifiedName~DiagnosticTest" -v n
    /// </summary>
    [Trait("Category", "Diagnostic")]
    [Collection("Simulator")]  // Prevent parallel execution with other simulator tests
    public class DiagnosticTest
    {
        private readonly ITestOutputHelper _output;

        public DiagnosticTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public void Diagnose_SimulatorConnection()
        {
            const string dllPath = @"C:\Program Files\OMRON\Sysmac Studio\MATLAB\Win64\NexSocket.dll";
            
            _output.WriteLine("=== Sysmac Simulator Diagnostic ===");
            _output.WriteLine($"DLL Path: {dllPath}");
            _output.WriteLine($"DLL Exists: {File.Exists(dllPath)}");

            if (!File.Exists(dllPath))
            {
                Skip.If(true, "NexSocket.dll not found");
                return;
            }

            try
            {
                using var client = new SysmacSimulatorClient();
                _output.WriteLine("Client created successfully");
                
                client.Connect();
                _output.WriteLine("Connected to simulator successfully");

                // Test reading an existing variable (user confirmed "TestReal" exists)
                var testNames = new[]
                {
                    "TestReal",               // User confirmed this exists
                    "TestBoolTrue",           // From our test file
                };

                foreach (var name in testNames)
                {
                    try
                    {
                        _output.WriteLine($"Attempting to read: '{name}'");
                        // Try reading as float since TestReal is a REAL type
                        var value = client.Read<float>(name);
                        _output.WriteLine($"  SUCCESS! Value: {value}");
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  FAILED: {ex.Message}");
                    }
                }

                _output.WriteLine("Diagnostic complete");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Connection failed: {ex.GetType().Name}: {ex.Message}");
                Skip.If(true, $"Cannot connect: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests reading with YOUR variable names. Edit the 'testNames' array
        /// to include variable names that exist in your Sysmac project.
        /// </summary>
        [SkippableFact]
        public void Diagnose_YourVariables()
        {
            const string dllPath = @"C:\Program Files\OMRON\Sysmac Studio\MATLAB\Win64\NexSocket.dll";

            if (!File.Exists(dllPath))
            {
                Skip.If(true, "NexSocket.dll not found");
                return;
            }

            _output.WriteLine("=== Your Variables Diagnostic ===");
            _output.WriteLine("Edit this test to add your own variable names from your Sysmac project.");

            try
            {
                using var client = new SysmacSimulatorClient();
                client.Connect();
                _output.WriteLine("Connected to simulator");

                // Test raw byte read of large struct (408 bytes: 4 + 400 + 4)
                _output.WriteLine("\n--- Testing TestLargeStructVar Raw Read ---");
                try
                {
                    var rawBytes = client.ReadBytes("TestLargeStructVar", 408);
                    // Show first 8 bytes (Header) and last 8 bytes (Footer)
                    _output.WriteLine($"  Total bytes: {rawBytes.Length}");
                    _output.WriteLine($"  Header bytes (0-3): {BitConverter.ToString(rawBytes, 0, 4)} = {BitConverter.ToInt32(rawBytes, 0)}");
                    _output.WriteLine($"  Footer bytes (404-407): {BitConverter.ToString(rawBytes, 404, 4)} = {BitConverter.ToInt32(rawBytes, 404)}");
                    
                    // Test serialization of a large struct  
                    _output.WriteLine("\n--- Testing Serialization ---");
                    // Note: Using inline class to avoid dependency on IntegrationTests types
                    var layout = OmronSysmacSimulator.Converters.TypeLayoutResolver.Resolve<TestLargeStruct>();
                    _output.WriteLine($"  TypeLayoutResolver size: {layout.TotalSize}");
                    
                    var testStruct = new TestLargeStruct { Header = 99999, Footer = 88888, Data = new float[100] };
                    var serialized = OmronSysmacSimulator.Converters.PlcSerializer.Serialize(testStruct);
                    _output.WriteLine($"  Serialized length: {serialized.Length}");
                    _output.WriteLine($"  Serialized header bytes (0-3): {BitConverter.ToString(serialized, 0, 4)} = {BitConverter.ToInt32(serialized, 0)}");
                    _output.WriteLine($"  Serialized footer bytes: {BitConverter.ToString(serialized, serialized.Length - 4, 4)} = {BitConverter.ToInt32(serialized, serialized.Length - 4)}");
                    
                    // Try typed Write<T>
                    _output.WriteLine("\n--- Testing Write<T> to TestLargeStructVar ---");
                    client.Write("TestLargeStructVar", testStruct);
                    _output.WriteLine("  Write<T> completed");
                    
                    var readBack = client.ReadBytes("TestLargeStructVar", 8);
                    _output.WriteLine($"  Read back header: {BitConverter.ToString(readBack, 0, 4)} = {BitConverter.ToInt32(readBack, 0)}");
                    
                    // Restore original
                    client.WriteBytes("TestLargeStructVar", rawBytes);
                    _output.WriteLine("  Restored original");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                        _output.WriteLine($"  Inner: {ex.InnerException.Message}");
                }
                
                // Test writing a simple int value first
                _output.WriteLine("\n--- Testing Simple Write to TestWriteDInt ---");
                try
                {
                    var originalBytes = client.ReadBytes("TestWriteDInt", 4);
                    _output.WriteLine($"  Original: {BitConverter.ToInt32(originalBytes, 0)}");
                    
                    byte[] newBytes = BitConverter.GetBytes(77777);
                    client.WriteBytes("TestWriteDInt", newBytes);
                    _output.WriteLine($"  Wrote: 77777");
                    
                    var readBackBytes = client.ReadBytes("TestWriteDInt", 4);
                    _output.WriteLine($"  Read back: {BitConverter.ToInt32(readBackBytes, 0)}");
                    
                    // Restore
                    client.WriteBytes("TestWriteDInt", originalBytes);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                }
                
                // Test nested struct - raw bytes to see layout
                _output.WriteLine("\n--- Testing Nested Struct Read (raw bytes) ---");
                try
                {
                    // Get the variable info to see reported size
                    var varInfo = typeof(SysmacSimulatorClient)
                        .GetMethod("FetchVariableInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(client, new object[] { "TestNestedStructVar" });
                    
                    if (varInfo != null)
                    {
                        var sizeInBits = varInfo.GetType().GetProperty("SizeInBits")?.GetValue(varInfo);
                        _output.WriteLine($"  PLC reported size: {sizeInBits} bits = {Convert.ToInt32(sizeInBits) / 8} bytes");
                    }
                    
                    // Expected layout: OuterId(4), Inner.Id(4), Inner.Temp(4), Inner.IsActive(1), OuterFlag(1) = 14 bytes min
                    // But with PLC padding could be 16 or more
                    var rawBytes = client.ReadBytes("TestNestedStructVar", 24);
                    _output.WriteLine($"  Raw bytes (24): {BitConverter.ToString(rawBytes)}");
                    
                    // Parse what we see
                    _output.WriteLine($"  Bytes 0-3 (OuterId): {BitConverter.ToInt32(rawBytes, 0)}");
                    _output.WriteLine($"  Bytes 4-7 (Inner.Id): {BitConverter.ToInt32(rawBytes, 4)}");
                    _output.WriteLine($"  Bytes 8-11 (Inner.Temp): {BitConverter.ToSingle(rawBytes, 8)}");
                    _output.WriteLine($"  Byte 12 (Inner.IsActive?): {rawBytes[12]}");
                    _output.WriteLine($"  Bytes 13-15 (padding?): {BitConverter.ToString(rawBytes, 13, 3)}");
                    _output.WriteLine($"  Bytes 16-19: {BitConverter.ToString(rawBytes, 16, 4)} = value {BitConverter.ToInt32(rawBytes, 16)}");
                    _output.WriteLine($"  Bytes 20-23: {BitConverter.ToString(rawBytes, 20, 4)}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Connection failed: {ex.Message}");
                Skip.If(true, $"Cannot connect: {ex.Message}");
            }
        }
    }
}
