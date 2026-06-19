using System.Text.Json;
using System.Text.Json.Serialization;
using UmaParser.ObjectModel;

namespace UmaParser.DataModel.ResponseData
{
    /// <summary>
    /// Polymorphic converter that automatically normalizes both:
    ///   - Full responses with headers (old CE Lua capture)
    ///   - Headerless responses (new horseACT hook)
    /// </summary>
    public class UmaApiResponseConverter : JsonConverter<UmaApiResponse>
    {
        public override UmaApiResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;

            // Normalize: if there's a "data" property, use only its content (strip headers)
            // Otherwise, use the entire JSON (headerless capture)
            JsonElement payload = root;

            if (root.TryGetProperty("data", out JsonElement dataElement))
            {
                payload = dataElement;
            }

            // Now run your existing polymorphic detection on the normalized payload
            string payloadJson = payload.GetRawText();

            // Check for known types using your existing detection logic
            if (IsTeamTrialResponse(payload))
            {
                var teamData = JsonSerializer.Deserialize<TeamRaceData>(payloadJson, options);
                if (teamData != null)
                {
                    return new TeamTrialResult(teamData);
                }
            }

            // Fallback for unknown response types
            return JsonSerializer.Deserialize<FallbackUmaApiResponse>(payloadJson, options);
        }

        private static bool IsTeamTrialResponse(JsonElement element)
        {
            // Your existing detection heuristic
            return element.TryGetProperty("race_start_params_array", out _);
        }

        public override void Write(Utf8JsonWriter writer, UmaApiResponse value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}