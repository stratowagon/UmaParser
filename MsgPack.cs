using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using MessagePack;

namespace MsgPack
{
    /// <summary>
    /// Loads a MessagePack binary blob, decodes it, and converts it to JSON.
    /// Designed for large structures and easy extension to strongly-typed classes.
    /// </summary>
    public static class MsgPackToJsonConverter
    {
        /// <summary>
        /// Loads the .bin file, deserializes MessagePack, and returns both the raw object 
        /// and the formatted JSON string.
        /// </summary>
        /// <param name="binFilePath">Full path to the captured .bin file.</param>
        /// <returns>A tuple containing the deserialized object and the JSON string.</returns>
        public static (object? DeserializedObject, string JsonString) ConvertToJson(string binFilePath)
        {
            if (!File.Exists(binFilePath))
                throw new FileNotFoundException("Binary blob file not found.", binFilePath);

            byte[] data = File.ReadAllBytes(binFilePath);

            // Deserialize MessagePack (typeless for maximum compatibility with unknown structures)
            object? obj = MessagePackSerializer.Deserialize<object>(data, MessagePackSerializerOptions.Standard);

            // Convert to indented JSON for readability
            string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = true,
                MaxDepth = 64,                    // Sufficient for large nested structures
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Preserve all characters
            });

            return (obj, json);
        }

        /// <summary>
        /// Converts the .bin file to JSON and optionally saves it to disk.
        /// </summary>
        /// <param name="binFilePath">Path to the .bin file.</param>
        /// <param name="outputJsonPath">Optional custom path for the .json file. If null, creates a sibling .json file.</param>
        /// <returns>The generated JSON string.</returns>
        public static string SaveAsJson(string binFilePath, string? outputJsonPath = null)
        {
            var (obj, json) = ConvertToJson(binFilePath);

            if (string.IsNullOrEmpty(outputJsonPath))
            {
                outputJsonPath = Path.ChangeExtension(binFilePath, ".json");
            }

            File.WriteAllText(outputJsonPath!, json);

            Console.WriteLine($"[SUCCESS] Converted and saved: {outputJsonPath}");
            return json;
        }

        /// <summary>
        /// Example helper: Batch-convert all .bin files in a folder (useful for your C:\Captures\ directory).
        /// </summary>
        public static void BatchConvertFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            foreach (string binFile in Directory.GetFiles(folderPath, "capture_*.bin"))
            {
                try
                {
                    SaveAsJson(binFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to process {binFile}: {ex.Message}");
                }
            }
        }
    }
}