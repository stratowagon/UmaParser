using System.Text.Json.Serialization;

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
