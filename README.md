# OmronSysmacSimulator

[![NuGet](https://img.shields.io/nuget/v/OmronSysmacSimulator.svg)](https://www.nuget.org/packages/OmronSysmacSimulator/)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL%203.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET%20Standard-2.0-purple.svg)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)

A C# library for communicating with the Omron Sysmac Studio Simulator via `NexSocket.dll`. It enables reading and writing PLC variables for testing and development without physical hardware.

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Defining PLC Types](#defining-plc-types)
- [Type Mapping](#type-mapping)
- [Attributes](#attributes)
- [Enums](#enums)
- [Arrays](#arrays)
- [Connection Options](#connection-options)
- [Async Operations](#async-operations)
- [Diagnostics](#diagnostics)
- [Variable Import](#variable-import)
- [Raw Byte Access](#raw-byte-access)
- [Error Handling](#error-handling)
- [Contributing](#contributing)
- [License](#license)
- [Acknowledgments](#acknowledgments)

## Features

- **Simple API** - Connect, read, and write with minimal code
- **Type-safe** - Generic `Read<T>` and `Write<T>` methods with automatic serialization
- **Complex Types** - Support for user-defined types, nested structures, arrays, and enums
- **Order-based Layout** - Use `[Order]` attributes to define struct member ordering
- **Auto-chunking** - Handles large data transfers automatically by splitting into chunks
- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and modern .NET
- **Async Support** - All operations have async counterparts

## Prerequisites

- [Sysmac Studio](https://www.ia.omron.com/products/family/3651/) installed (provides `NexSocket.dll`)
- Sysmac Studio Simulator running


## Quick Start

```csharp
using OmronSysmacSimulator;

// Connect to the simulator
using var client = new SysmacSimulatorClient();
client.Connect();

// Read a primitive value
float temperature = client.Read<float>("TemperatureSensor");
Console.WriteLine($"Temperature: {temperature}");

// Write a value
client.Write("TemperatureSetpoint", 25.5f);

// Read a complex structure
var status = client.Read<MachineStatus>("StatusData");
Console.WriteLine($"Counter: {status.Counter}");
```

## Defining PLC Types

Use the `[Order]` attribute to specify the byte-order of members:

```csharp
using OmronSysmacSimulator.Attributes;

public class MachineStatus
{
    [Order(0)]
    public int Counter { get; set; }

    [Order(1)]
    public float Temperature { get; set; }

    [Order(2)]
    public bool IsRunning { get; set; }

    [Order(3)]
    public SensorData[] Sensors { get; set; } = new SensorData[4];

    [Order(4)]
    public AlarmInfo Alarms { get; set; } = new AlarmInfo();
}

public class SensorData
{
    [Order(0)]
    public int Id { get; set; }

    [Order(1)]
    public float Value { get; set; }

    [Order(2)]
    public bool IsValid { get; set; }
}

public class AlarmInfo
{
    [Order(0)]
    public int ActiveCount { get; set; }

    [Order(1)]
    public bool HasCritical { get; set; }
}
```

## Type Mapping

| C# Type | PLC Type | Size |
|---------|----------|------|
| `bool` | BOOL | 1 byte |
| `sbyte` | SINT | 1 byte |
| `byte` | USINT | 1 byte |
| `short` | INT | 2 bytes |
| `ushort` | UINT | 2 bytes |
| `int` | DINT | 4 bytes |
| `uint` | UDINT | 4 bytes |
| `long` | LINT | 8 bytes |
| `ulong` | ULINT | 8 bytes |
| `float` | REAL | 4 bytes |
| `double` | LREAL | 8 bytes |
| `string` | STRING | Requires `[PlcString(n)]` |
| `enum` | Based on underlying type |
| `class` | Nested struct | Calculated recursively |
| `T[]` | Array | Element size x length |

## Attributes

### `[Order(int)]`

**Required** for all members in a PLC structure. Specifies the serialization order (0-based).

```csharp
[Order(0)]
public int First { get; set; }

[Order(1)]
public float Second { get; set; }
```

### `[PlcString(int maxLength)]`

**Required** for string members. Specifies the maximum byte length.

```csharp
[Order(0)]
[PlcString(50)]
public string Name { get; set; }
```

### `[ArrayLength(int)]`

Optional. Specifies array length when it can't be inferred from the initializer.

```csharp
[Order(0)]
[ArrayLength(10)]
public int[] Values { get; set; }
```

### `[PlcType(PlcDataType)]`

Optional. Overrides the automatically inferred PLC type.

```csharp
[Order(0)]
[PlcType(PlcDataType.UDInt)]
public int Counter { get; set; }  // Treated as UDINT instead of DINT
```

## Enums

Enums are serialized based on their underlying type:

```csharp
public enum MachineState : short  // Uses INT (2 bytes)
{
    Idle = 0,
    Running = 1,
    Faulted = 2
}

public class Status
{
    [Order(0)]
    public int Id { get; set; }

    [Order(1)]
    public MachineState State { get; set; }  // 2 bytes
}
```

## Arrays

Arrays are supported for both primitives and complex types:

```csharp
public class DataBlock
{
    [Order(0)]
    public int Count { get; set; }

    [Order(1)]
    public float[] Values { get; set; } = new float[10];  // Length inferred from initializer

    [Order(2)]
    public SensorData[] Sensors { get; set; } = new SensorData[5];  // Nested struct array
}
```

## Connection Options

```csharp
var options = new SysmacConnectionOptions
{
    IpAddress = "127.0.0.1",
    Port = 7000,
    DllPath = @"C:\Program Files\OMRON\Sysmac Studio\MATLAB\Win64\NexSocket.dll",
    TimeoutMs = 5000,
    MaxChunkSizeOverride = 0,  // 0 = auto-detect
    SkipChunkDetection = false
};

using var client = new SysmacSimulatorClient(options);
client.Connect();
```

## Async Operations

```csharp
using var client = new SysmacSimulatorClient();
await client.ConnectAsync();

var value = await client.ReadAsync<float>("temperature");
await client.WriteAsync("setpoint", 100.0f);
```

## Diagnostics

### Type Layout Description

```csharp
string layout = PlcSerializer.Describe<MachineStatus>(512);
Console.WriteLine(layout);

// Output:
// MachineStatus (total: 56 bytes, 1 chunk @ 512 bytes)
//   Offset 0: Counter (Int32, 4 bytes) [Chunk 1]
//   Offset 4: Temperature (Single, 4 bytes) [Chunk 1]
//   ...
```

### Type Validation

```csharp
string error = PlcSerializer.Validate<MyType>();
if (error != null)
{
    Console.WriteLine($"Type validation failed: {error}");
}
```

### Chunk Size Info

```csharp
client.Connect();
Console.WriteLine($"Max chunk size: {client.DetectedMaxChunkSize} bytes");
Console.WriteLine($"Auto-detected: {client.ChunkSizeWasAutoDetected}");
```

## Variable Import

Import variable definitions from Sysmac Studio CX-Designer export:

```csharp
client.Connect();
client.ImportVariablesFromFile("global_variables.txt");
```

## Raw Byte Access

For advanced use cases:

```csharp
// Read raw bytes
byte[] data = client.ReadBytes("myVariable", 100);

// Write raw bytes
client.WriteBytes("myVariable", new byte[] { 0x01, 0x02, 0x03 });
```

## Error Handling

```csharp
try
{
    client.Connect();
    var value = client.Read<MyType>("variable");
}
catch (SysmacConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
catch (SysmacCommunicationException ex)
{
    Console.WriteLine($"Communication error: {ex.Message}");
}
catch (SysmacVariableException ex)
{
    Console.WriteLine($"Variable '{ex.VariableName}' error: {ex.Message}");
}
catch (SysmacTypeException ex)
{
    Console.WriteLine($"Type error in {ex.OffendingType?.Name}: {ex.Message}");
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

Please ensure your code:
- Follows the existing code style
- Includes appropriate tests
- Updates documentation as needed

## License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

This library is a C# port based on the Python [omron_sysmac_simulator](https://github.com/aphyt/omron_sysmac_simulator) library by [APHYT](https://github.com/aphyt), which was originally based on work by [Simumatik](https://simumatik.com/).
