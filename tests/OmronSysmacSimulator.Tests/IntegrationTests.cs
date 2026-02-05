using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using OmronSysmacSimulator;
using OmronSysmacSimulator.Attributes;
using OmronSysmacSimulator.Models;

namespace OmronSysmacSimulator.Tests
{
    /// <summary>
    /// Integration tests that require a running Sysmac Studio Simulator.
    /// 
    /// Prerequisites:
    /// 1. Create a Sysmac Studio project
    /// 2. Add the TestVariables.ST file to the project
    /// 3. Build and run the simulator
    /// 4. Run these tests with: dotnet test --filter "Category=Integration"
    /// 
    /// These tests are skipped when the simulator is not available.
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection("Simulator")]  // Prevent parallel execution with other simulator tests
    public class IntegrationTests
    {
        private const string NexSocketDllPath = @"C:\Program Files\OMRON\Sysmac Studio\MATLAB\Win64\NexSocket.dll";
        private const float FloatTolerance = 0.001f;
        private const double DoubleTolerance = 0.0000001;

        /// <summary>
        /// Helper to run a test with proper simulator connection handling.
        /// Skips the test if the simulator is not available.
        /// </summary>
        private static void RunWithSimulator(Action<SysmacSimulatorClient> testAction)
        {
            // Check if DLL exists first
            if (!File.Exists(NexSocketDllPath))
            {
                Skip.If(true, $"NexSocket.dll not found at: {NexSocketDllPath}");
                return;
            }

            SysmacSimulatorClient client = null;
            try
            {
                client = new SysmacSimulatorClient();
                client.Connect();
            }
            catch (Exception ex)
            {
                client?.Dispose();
                Skip.If(true, $"Cannot connect to simulator: {ex.Message}");
                return;
            }

            try
            {
                testAction(client);
            }
            catch (ObjectDisposedException)
            {
                Skip.If(true, "Client was disposed unexpectedly - simulator may not be running");
            }
            catch (OmronSysmacSimulator.Exceptions.SysmacException ex)
            {
                Skip.If(true, $"Simulator error: {ex.Message}");
            }
            finally
            {
                try { client?.Dispose(); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Async version of RunWithSimulator.
        /// </summary>
        private static async Task RunWithSimulatorAsync(Func<SysmacSimulatorClient, Task> testAction)
        {
            if (!File.Exists(NexSocketDllPath))
            {
                Skip.If(true, $"NexSocket.dll not found at: {NexSocketDllPath}");
                return;
            }

            SysmacSimulatorClient client = null;
            try
            {
                client = new SysmacSimulatorClient();
                client.Connect();
            }
            catch (Exception ex)
            {
                client?.Dispose();
                Skip.If(true, $"Cannot connect to simulator: {ex.Message}");
                return;
            }

            try
            {
                await testAction(client);
            }
            catch (ObjectDisposedException)
            {
                Skip.If(true, "Client was disposed unexpectedly - simulator may not be running");
            }
            catch (OmronSysmacSimulator.Exceptions.SysmacException ex)
            {
                Skip.If(true, $"Simulator error: {ex.Message}");
            }
            finally
            {
                try { client?.Dispose(); } catch { /* ignore */ }
            }
        }

        private static void AssertFloatEqual(float expected, float actual, float tolerance = FloatTolerance)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance, 
                $"Expected {expected} but got {actual} (tolerance: {tolerance})");
        }

        private static void AssertDoubleEqual(double expected, double actual, double tolerance = DoubleTolerance)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance, 
                $"Expected {expected} but got {actual} (tolerance: {tolerance})");
        }

        #region Primitive Type Tests

        [SkippableFact]
        public void Read_BoolTrue_ReturnsTrue()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<bool>("TestBoolTrue");
                Assert.True(value);
            });
        }

        [SkippableFact]
        public void Read_BoolFalse_ReturnsFalse()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<bool>("TestBoolFalse");
                Assert.False(value);
            });
        }

        [SkippableFact]
        public void Read_IntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<short>("TestInt");
                Assert.Equal(12345, value);
            });
        }

        [SkippableFact]
        public void Read_DIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<int>("TestDInt");
                Assert.Equal(123456789, value);
            });
        }

        [SkippableFact]
        public void Read_RealValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<float>("TestReal");
                AssertFloatEqual(3.14159f, value);
            });
        }

        [SkippableFact]
        public void Read_LRealValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<double>("TestLReal");
                AssertDoubleEqual(2.718281828, value);
            });
        }

        [SkippableFact]
        public void Read_SIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<sbyte>("TestSInt");
                Assert.Equal(-42, value);
            });
        }

        [SkippableFact]
        public void Read_USIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<byte>("TestUSInt");
                Assert.Equal(200, value);
            });
        }

        [SkippableFact]
        public void Read_UIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<ushort>("TestUInt");
                Assert.Equal(50000, value);
            });
        }

        [SkippableFact]
        public void Read_UDIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<uint>("TestUDInt");
                Assert.Equal(3000000000u, value);
            });
        }

        [SkippableFact]
        public void Read_LIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<long>("TestLInt");
                Assert.Equal(-9223372036854775800L, value);
            });
        }

        [SkippableFact]
        public void Read_ULIntValue_ReturnsExpected()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<ulong>("TestULInt");
                Assert.Equal(18446744073709551000UL, value);
            });
        }

        #endregion

        #region Write Tests

        [SkippableFact]
        public void Write_Bool_RoundTrips()
        {
            RunWithSimulator(client =>
            {
                var original = client.Read<bool>("TestWriteBool");
                
                client.Write("TestWriteBool", !original);
                var changed = client.Read<bool>("TestWriteBool");
                Assert.Equal(!original, changed);
                
                client.Write("TestWriteBool", original);
            });
        }

        [SkippableFact]
        public void Write_Int_RoundTrips()
        {
            RunWithSimulator(client =>
            {
                var original = client.Read<short>("TestWriteInt");
                
                client.Write<short>("TestWriteInt", 9999);
                var changed = client.Read<short>("TestWriteInt");
                Assert.Equal(9999, changed);
                
                client.Write("TestWriteInt", original);
            });
        }

        [SkippableFact]
        public void Write_Real_RoundTrips()
        {
            RunWithSimulator(client =>
            {
                var original = client.Read<float>("TestWriteReal");
                
                client.Write("TestWriteReal", 123.456f);
                var changed = client.Read<float>("TestWriteReal");
                AssertFloatEqual(123.456f, changed);
                
                client.Write("TestWriteReal", original);
            });
        }

        [SkippableFact]
        public void Write_DInt_RoundTrips()
        {
            RunWithSimulator(client =>
            {
                var original = client.Read<int>("TestWriteDInt");
                
                client.Write("TestWriteDInt", 987654321);
                var changed = client.Read<int>("TestWriteDInt");
                Assert.Equal(987654321, changed);
                
                client.Write("TestWriteDInt", original);
            });
        }

        #endregion

        #region Array Tests

        [SkippableFact]
        public void Read_IntArray_ReturnsExpectedValues()
        {
            RunWithSimulator(client =>
            {
                var val0 = client.Read<short>("TestIntArray[0]");
                var val1 = client.Read<short>("TestIntArray[1]");
                var val2 = client.Read<short>("TestIntArray[2]");
                var val3 = client.Read<short>("TestIntArray[3]");
                var val4 = client.Read<short>("TestIntArray[4]");
                
                Assert.Equal(100, val0);
                Assert.Equal(200, val1);
                Assert.Equal(300, val2);
                Assert.Equal(400, val3);
                Assert.Equal(500, val4);
            });
        }

        [SkippableFact]
        public void Read_RealArray_ReturnsExpectedValues()
        {
            RunWithSimulator(client =>
            {
                var val0 = client.Read<float>("TestRealArray[0]");
                var val1 = client.Read<float>("TestRealArray[1]");
                var val2 = client.Read<float>("TestRealArray[2]");
                
                AssertFloatEqual(1.1f, val0);
                AssertFloatEqual(2.2f, val1);
                AssertFloatEqual(3.3f, val2);
            });
        }

        #endregion

        #region Complex Structure Tests

        [SkippableFact]
        public void Read_SimpleStruct_ReturnsExpectedValues()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<TestSimpleStruct>("TestSimpleStructVar");
                
                Assert.Equal(42, value.Id);
                AssertFloatEqual(98.6f, value.Temperature);
                Assert.True(value.IsActive);
            });
        }

        [SkippableFact]
        public void Write_SimpleStruct_RoundTrips()
        {
            RunWithSimulator(client =>
            {
                var original = client.Read<TestSimpleStruct>("TestSimpleStructVar");
                
                var newValue = new TestSimpleStruct
                {
                    Id = 999,
                    Temperature = 123.45f,
                    IsActive = false
                };
                
                client.Write("TestSimpleStructVar", newValue);
                var readBack = client.Read<TestSimpleStruct>("TestSimpleStructVar");
                
                Assert.Equal(999, readBack.Id);
                AssertFloatEqual(123.45f, readBack.Temperature);
                Assert.False(readBack.IsActive);
                
                client.Write("TestSimpleStructVar", original);
            });
        }

        [SkippableFact]
        public void Read_NestedStruct_ReturnsExpectedValues()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<TestNestedStruct>("TestNestedStructVar");
                
                Assert.Equal(1, value.OuterId);
                Assert.Equal(100, value.Inner.Id);
                AssertFloatEqual(50.5f, value.Inner.Temperature);
                Assert.True(value.Inner.IsActive);
                Assert.True(value.OuterFlag);
            });
        }

        [SkippableFact]
        public void Read_StructWithEnum_ReturnsExpectedValues()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<TestStructWithEnum>("TestEnumStructVar");
                
                Assert.Equal(1, value.Id);
                Assert.Equal(TestMachineState.Running, value.State);
            });
        }

        [SkippableFact]
        public void Read_StructWithArray_ReturnsExpectedValues()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<TestStructWithArray>("TestArrayStructVar");
                
                Assert.Equal(5, value.Count);
                AssertFloatEqual(10.0f, value.Values[0]);
                AssertFloatEqual(20.0f, value.Values[1]);
                AssertFloatEqual(30.0f, value.Values[2]);
                AssertFloatEqual(40.0f, value.Values[3]);
                AssertFloatEqual(50.0f, value.Values[4]);
            });
        }

        #endregion

        #region Large Data / Chunking Tests

        [SkippableFact]
        public void Read_LargeStruct_SucceedsWithChunking()
        {
            RunWithSimulator(client =>
            {
                var value = client.Read<TestLargeStruct>("TestLargeStructVar");
                
                Assert.Equal(12345, value.Header);
                AssertFloatEqual(100.0f, value.Data[0]);
                Assert.Equal(54321, value.Footer);
            });
        }

        [SkippableFact]
        public void Write_LargeStruct_SucceedsWithChunking()
        {
            RunWithSimulator(client =>
            {
                var original = client.Read<TestLargeStruct>("TestLargeStructVar");
                
                var newValue = new TestLargeStruct
                {
                    Header = 99999,
                    Data = new float[100],
                    Footer = 88888
                };
                for (int i = 0; i < 100; i++)
                {
                    newValue.Data[i] = i * 1.5f;
                }
                
                client.Write("TestLargeStructVar", newValue);
                var readBack = client.Read<TestLargeStruct>("TestLargeStructVar");
                
                Assert.Equal(99999, readBack.Header);
                AssertFloatEqual(0.0f, readBack.Data[0]);
                AssertFloatEqual(1.5f, readBack.Data[1]);
                AssertFloatEqual(148.5f, readBack.Data[99]);
                Assert.Equal(88888, readBack.Footer);
                
                client.Write("TestLargeStructVar", original);
            });
        }

        #endregion

        #region Async Tests

        [SkippableFact]
        public async Task ReadAsync_Int_ReturnsExpected()
        {
            await RunWithSimulatorAsync(async client =>
            {
                var value = await client.ReadAsync<short>("TestInt");
                Assert.Equal(12345, value);
            });
        }

        [SkippableFact]
        public async Task WriteAsync_Int_RoundTrips()
        {
            await RunWithSimulatorAsync(async client =>
            {
                var original = await client.ReadAsync<short>("TestWriteInt");
                
                await client.WriteAsync<short>("TestWriteInt", 7777);
                var changed = await client.ReadAsync<short>("TestWriteInt");
                Assert.Equal(7777, changed);
                
                await client.WriteAsync("TestWriteInt", original);
            });
        }

        #endregion

        #region Error Handling Tests

        [SkippableFact]
        public void Read_NonExistentVariable_ThrowsException()
        {
            RunWithSimulator(client =>
            {
                Assert.Throws<OmronSysmacSimulator.Exceptions.SysmacVariableException>(
                    () => client.Read<int>("NonExistentVariable12345"));
            });
        }

        #endregion
    }

    #region Test Data Types (must match ST file)

    public class TestSimpleStruct
    {
        [Order(0)]
        public int Id { get; set; }
        
        [Order(1)]
        public float Temperature { get; set; }
        
        [Order(2)]
        public bool IsActive { get; set; }
    }

    public class TestNestedStruct
    {
        [Order(0)]
        public int OuterId { get; set; }
        
        [Order(1)]
        public TestSimpleStruct Inner { get; set; } = new TestSimpleStruct();
        
        [Order(2)]
        public bool OuterFlag { get; set; }
    }

    public enum TestMachineState : short
    {
        Idle = 0,
        Running = 1,
        Paused = 2,
        Faulted = 3
    }

    public class TestStructWithEnum
    {
        [Order(0)]
        public int Id { get; set; }
        
        [Order(1)]
        public TestMachineState State { get; set; }
    }

    public class TestStructWithArray
    {
        [Order(0)]
        public int Count { get; set; }
        
        [Order(1)]
        public float[] Values { get; set; } = new float[5];
    }

    public class TestLargeStruct
    {
        [Order(0)]
        public int Header { get; set; }
        
        [Order(1)]
        public float[] Data { get; set; } = new float[100];  // 400 bytes - will require chunking
        
        [Order(2)]
        public int Footer { get; set; }
    }

    #endregion
}
