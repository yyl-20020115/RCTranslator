using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace RCTranslator;

public class Program
{
    public static readonly string apiUrl = "http://localhost:11434/api/chat";


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
        // 设置请求的 URI。如果这是对API的POST请求，需要确保URI指向接收POST数据的端点。


        // 创建HttpContent，这里以StringContent为例，它会将字符串作为请求的主体发送。
        var content = new StringContent(text, Encoding.UTF8, "application/json");

        // 发送POST请求
        var response = await client.PostAsync(uri, content);

        // 确保HTTP响应状态为200-299范围，表示成功。
        if (response.IsSuccessStatusCode)
        {
            // 读取响应内容（如果需要）
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            return responseBody;
        }
        else
        {
            Console.WriteLine("Error: " + response.StatusCode);
            return "";
        }
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
        var lines = response.Split("\n");
        var list = new List<Response>();
        foreach (var _line in lines)
        {
            if (string.IsNullOrEmpty(_line)) continue;
            var res = JsonSerializer.Deserialize<Response>(_line, options);
            list.Add(res);
        }
        return string.Join("", list);
    }
    public static async Task<int> Main(string[] args)
    {

        using var writer = new StreamWriter("output.txt", new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.Create, Share = FileShare.ReadWrite });
        using var reader = new StreamReader("input.txt", new FileStreamOptions { Share = FileShare.Read });

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var ret = await CallOllama(apiUrl, $"请翻译{line}");

            writer.WriteLine(ret);
            writer.Flush();
        }


        return 0;
    }



}