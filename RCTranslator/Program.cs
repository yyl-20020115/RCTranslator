using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace RCTranslator;

public class Program
{
    public enum SectionType : int
    {
        Init = -1,
        Version = 0,
        Dialog = 1,
        Menu = 2,
        StringTable = 3
    }
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
        var lines = response.Split("\n");
        var list = new List<Response>();
        foreach (var _line in lines)
        {
            if (string.IsNullOrEmpty(_line)) continue;
            var res = JsonSerializer.Deserialize<Response>(_line, options);
            if (res != null) list.Add(res);
        }
        return string.Join(string.Empty, list);
    }
    public static async Task<int> Main(string[] args)
    {
        var sectionType = SectionType.Init;

        using var writer = new StreamWriter("output.txt", new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.Create, Share = FileShare.ReadWrite });
        using var reader = new StreamReader("input.txt", new FileStreamOptions { Share = FileShare.Read });

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var _line = line.Trim();

            if (_line.Length == 0
                || _line.StartsWith("//")
                || _line.StartsWith('#')
                )
            {
                writer.WriteLine(line);
            }

            if (_line.StartsWith("// Version"))
            {
                sectionType = SectionType.Version;
                continue;
            }
            else if (_line.StartsWith("// Dialog"))
            {
                sectionType = SectionType.Dialog;
                continue;
            }
            else if (_line.StartsWith("// Menu"))
            {
                sectionType = SectionType.Menu;
                continue;
            }
            else if (_line.StartsWith("// String Table"))
            {
                sectionType = SectionType.StringTable;
                continue;
            }
            else if (_line.StartsWith("//") || _line.StartsWith('#') || _line.Length == 0)
            {
                continue;
            }

            if (sectionType == SectionType.Version)
            {
                if (!_line.StartsWith("//"))
                    writer.WriteLine(line);
            }
            else if (sectionType == SectionType.Dialog)
            {
                if (_line.StartsWith("LTEXT ")
                    || _line.StartsWith("CTEXT ")
                    || _line.StartsWith("DEFPUSHBUTTON ")
                    || _line.StartsWith("PUSHBUTTON ")
                    || _line.StartsWith("GROUPBOX ")
                    || _line.StartsWith("CONTROL ")
                    || _line.StartsWith("CAPTION ")
                    || _line.StartsWith("EDITTEXT ")
                    || _line.StartsWith("LISTBOX ")
                    || _line.StartsWith("COMBOBOX ")
                    || _line.StartsWith("ICON ")
                    )
                {
                    if(_line.StartsWith("CAPTION "))
                    {
                        var part = _line[8..].Trim('"');
                        var result = await CallOllama(apiUrl, $"请翻译:{part}");
                        if (string.IsNullOrEmpty(result)) result = part;
                        writer.WriteLine($"CAPTION \"{result}\"");
                    }
                    else
                    {
                        var parts = _line[16..].Split(',');
                        if (parts.Length >= 1 && parts[0].StartsWith('"') && parts[0].EndsWith('"'))
                        {
                            parts[0] = parts[0].Trim('"');
                            var result = parts[0].Length>0 ? await CallOllama(apiUrl, $"请翻译:{parts[0]}"):string.Empty;
                            parts[0] = $"\"{result}\"";
                            writer.WriteLine(line[..20] + string.Join(',', parts));
                        }
                        else
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
                else
                {
                    writer.WriteLine(line);
                }

            }
            else if (sectionType == SectionType.Menu)
            {
                if (_line.StartsWith("POPUP ")
                    || _line.StartsWith("MENUITEM ")
                    )
                {
                    var parts = _line.Split(' ');
                    if (parts.Length == 2)
                    {
                        parts[1] = parts[1].Trim('"');
                        var result = await CallOllama(apiUrl, $"请翻译:{parts[1]}");
                        parts[1] = $"\"{result}\"";
                        writer.WriteLine(string.Join(' ', parts));
                    }
                    else
                    {
                        writer.WriteLine(line);
                    }
                }
                else
                {
                    writer.WriteLine(line);
                }
            }
            else if (sectionType == SectionType.StringTable)
            {
                var parts = _line.Split(' ');
                if (parts.Length == 2)
                {
                    parts[1] = parts[1].Trim('"');
                    var result = await CallOllama(apiUrl, $"请翻译:{parts[1]}");
                    if (!string.IsNullOrEmpty(result))
                    {
                        parts[1] = $"\"{result}\"";
                    }
                    writer.WriteLine(string.Join(' ', parts));
                }
                else
                {
                    writer.WriteLine(line);
                }
            }




            writer.Flush();
        }


        return 0;
    }



}