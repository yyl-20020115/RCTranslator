using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace OllamaAPI;

public static class OllamaAPIProvider
{
    public static readonly string APIUrl = "http://localhost:11434/api/chat";

    public static readonly HttpClient client = new();

    public static readonly JsonSerializerOptions options = new()
    {
        Encoder = JavaScriptEncoder.Create(new System.Text.Unicode.UnicodeRange(0, 0xffff)),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true, // 格式化输出
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static async Task<string> DoCallAPI(string uri, string text)
    {
        var content = new StringContent(text, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(uri, content);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : "";
    }
    public class Message
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
        public override string ToString() => this.Content;

    }
    public class Request
    {
        public string Model { get; set; } = "";
        public List<Message> Messages { get; set; } = [];
        public bool Stream { get; set; } = false;
    }
    public class Response
    {
        public string Model { get; set; } = "";
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public Message Message { get; set; } = new();
        public string DoneReason { get; set; } = "";
        public long TotalDuration { get; set; } = 0;
        public long LoadDuration { get; set; } = 0;
        public long PromptEvalCount { get; set; } = 0;
        public long PromptEvalDuration { get; set; } = 0;
        public long EvalCount { get; set; } = 0;
        public long EvalDuration { get; set; } = 0;
        public override string ToString() => this.Message?.ToString() ?? "";

    }
    public static async Task<string> CallOllama(string apiUrl, string qeustion, string model = "qwen", string role = "user")
    {
        var text = JsonSerializer.Serialize(new Request { Model = model, Messages = [new() { Content = qeustion, Role = role }], Stream = false }, options);
        var response = await DoCallAPI(apiUrl, text);
        var lines = response.Split('\n');
        var list = new List<Response>();
        foreach (var _line in lines)
        {
            if (string.IsNullOrEmpty(_line)) continue;
            var res = JsonSerializer.Deserialize<Response>(_line, options);
            if (res != null) list.Add(res);
        }
        return string.Join(string.Empty, list);
    }


}
