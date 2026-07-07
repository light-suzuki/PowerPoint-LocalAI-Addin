using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Utils
{
    public static class JsonUtils
    {
        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.Indented);
        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(ExtractJson(json));

        public static string ExtractJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            var firstObject = value.IndexOf('{');
            var firstArray = value.IndexOf('[');
            var start = firstObject < 0 ? firstArray : firstArray < 0 ? firstObject : System.Math.Min(firstObject, firstArray);
            var end = value.LastIndexOf(firstArray >= 0 && firstArray == start ? ']' : '}');
            return start >= 0 && end > start ? value.Substring(start, end - start + 1) : value;
        }

        public static bool IsValidJson(string json, out string error)
        {
            try { JToken.Parse(ExtractJson(json)); error = null; return true; }
            catch (JsonException ex) { error = ex.Message; return false; }
        }
    }
}
