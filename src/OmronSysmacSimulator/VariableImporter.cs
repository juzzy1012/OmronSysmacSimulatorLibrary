using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OmronSysmacSimulator
{
    /// <summary>
    /// Imports variable definitions from Sysmac Studio export files.
    /// </summary>
    public static class VariableImporter
    {
        /// <summary>
        /// Parses a Sysmac Studio CX-Designer export file.
        /// </summary>
        /// <param name="filePath">Path to the export file.</param>
        /// <returns>List of variable names and their PLC types.</returns>
        public static List<(string Name, PlcDataType Type)> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Variable file not found: {filePath}", filePath);

            using (var reader = new StreamReader(filePath))
            {
                return ParseStream(reader);
            }
        }

        /// <summary>
        /// Parses variable definitions from a stream.
        /// </summary>
        /// <param name="reader">The text reader.</param>
        /// <returns>List of variable names and their PLC types.</returns>
        public static List<(string Name, PlcDataType Type)> ParseStream(TextReader reader)
        {
            var result = new List<(string Name, PlcDataType Type)>();
            
            // Skip header line
            string line = reader.ReadLine();
            if (line == null)
                return result;

            // Parse each line
            while ((line = reader.ReadLine()) != null)
            {
                var parsed = ParseLine(line);
                if (parsed != null)
                {
                    result.AddRange(parsed);
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a single line from the export file.
        /// </summary>
        /// <param name="line">The line to parse.</param>
        /// <returns>List of variable entries (may be multiple for arrays), or null if line is invalid.</returns>
        public static List<(string Name, PlcDataType Type)> ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Format: VariableName\tDataType\t...
            var tokens = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
                return null;

            string name = tokens[0].Trim();
            string typeStr = tokens[1].Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(typeStr))
                return null;

            var result = new List<(string Name, PlcDataType Type)>();

            // Check for array type: TYPE[low..high]
            var arrayMatch = Regex.Match(typeStr, @"^(\w+)\[(\d+)\.\.(\d+)\]$");
            if (arrayMatch.Success)
            {
                string baseType = arrayMatch.Groups[1].Value;
                int lowIndex = int.Parse(arrayMatch.Groups[2].Value);
                int highIndex = int.Parse(arrayMatch.Groups[3].Value);
                PlcDataType plcType = ParseDataType(baseType);

                for (int i = lowIndex; i <= highIndex; i++)
                {
                    result.Add(($"{name}[{i}]", plcType));
                }
            }
            else
            {
                PlcDataType plcType = ParseDataType(typeStr);
                result.Add((name, plcType));
            }

            return result;
        }

        /// <summary>
        /// Parses a PLC data type string to a PlcDataType enum value.
        /// </summary>
        /// <param name="typeString">The type string (e.g., "REAL", "INT", "BOOL").</param>
        /// <returns>The corresponding PlcDataType.</returns>
        public static PlcDataType ParseDataType(string typeString)
        {
            switch (typeString.ToUpperInvariant())
            {
                case "BOOL":
                    return PlcDataType.Bool;
                case "SINT":
                    return PlcDataType.SInt;
                case "INT":
                    return PlcDataType.Int;
                case "DINT":
                    return PlcDataType.DInt;
                case "LINT":
                    return PlcDataType.LInt;
                case "USINT":
                    return PlcDataType.USInt;
                case "UINT":
                    return PlcDataType.UInt;
                case "UDINT":
                    return PlcDataType.UDInt;
                case "ULINT":
                    return PlcDataType.ULInt;
                case "REAL":
                    return PlcDataType.Real;
                case "LREAL":
                    return PlcDataType.LReal;
                case "STRING":
                    return PlcDataType.String;
                default:
                    // Unknown type - treat as struct
                    return PlcDataType.Struct;
            }
        }

        /// <summary>
        /// Converts a PlcDataType to its string representation.
        /// </summary>
        /// <param name="type">The PLC data type.</param>
        /// <returns>The string representation.</returns>
        public static string DataTypeToString(PlcDataType type)
        {
            switch (type)
            {
                case PlcDataType.Bool:
                    return "BOOL";
                case PlcDataType.SInt:
                    return "SINT";
                case PlcDataType.Int:
                    return "INT";
                case PlcDataType.DInt:
                    return "DINT";
                case PlcDataType.LInt:
                    return "LINT";
                case PlcDataType.USInt:
                    return "USINT";
                case PlcDataType.UInt:
                    return "UINT";
                case PlcDataType.UDInt:
                    return "UDINT";
                case PlcDataType.ULInt:
                    return "ULINT";
                case PlcDataType.Real:
                    return "REAL";
                case PlcDataType.LReal:
                    return "LREAL";
                case PlcDataType.String:
                    return "STRING";
                case PlcDataType.Struct:
                    return "STRUCT";
                case PlcDataType.Array:
                    return "ARRAY";
                default:
                    return type.ToString();
            }
        }
    }
}
