using System.Text.Json;
using System.Text.Json.Serialization;
using UmaParser.ObjectModel;

namespace UmaParser.DataModel.ResponseData
{
    public abstract class UmaApiResponse
    {
        [JsonPropertyName("response_code")]
        public int ResponseCode { get; set; }

        [JsonPropertyName("data_headers")]
        public DataHeaders DataHeaders { get; set; } = new();

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        // List of registered type detectors for dynamic deserialization of the "data" field.
        internal static readonly List<Func<JsonElement, Type?>> TypeDetectors = new();

        public static void RegisterDetector(Func<JsonElement, Type?> detector)
        {
            if (detector != null && !TypeDetectors.Contains(detector))
            {
                TypeDetectors.Add(detector);
            }
        }

        public static void RegisterAllKnownTypes()
        {
            // Register in the order you want them evaluated (more specific first)
            UmaApiResponse.RegisterDetector(TeamTrialResult.DetermineType);
            // ... add every derived class here ...
        }
    }

    public class DataHeaders
    {
        [JsonPropertyName("viewer_id")] public long ViewerId { get; set; }
        [JsonPropertyName("sid")] public string Sid { get; set; } = string.Empty;
        [JsonPropertyName("servertime")] public long ServerTime { get; set; }
        [JsonPropertyName("result_code")] public int ResultCode { get; set; }

        [JsonPropertyName("notifications")]
        public Notifications Notifications { get; set; } = new();
    }

    public class Notifications
    {
        [JsonPropertyName("unread_information_exists")]
        public int UnreadInformationExists { get; set; }
    }
}
