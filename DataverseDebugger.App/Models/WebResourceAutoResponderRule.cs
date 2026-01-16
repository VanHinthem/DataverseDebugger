using System.Text.Json.Serialization;

namespace DataverseDebugger.App.Models
{
    public enum WebResourceMatchType
    {
        Exact,
        Wildcard,
        Regex
    }

    public enum WebResourceActionType
    {
        ServeLocalFile,
        ServeFromFolder,
        ProxyToUrl
    }

    public class WebResourceAutoResponderRule
    {
        public bool Enabled { get; set; } = true;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WebResourceMatchType MatchType { get; set; } = WebResourceMatchType.Exact;

        public string Pattern { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WebResourceActionType ActionType { get; set; } = WebResourceActionType.ServeLocalFile;

        public string ActionValue { get; set; } = string.Empty;

        public WebResourceAutoResponderRule Clone()
        {
            return new WebResourceAutoResponderRule
            {
                Enabled = Enabled,
                MatchType = MatchType,
                Pattern = Pattern,
                ActionType = ActionType,
                ActionValue = ActionValue
            };
        }
    }
}
