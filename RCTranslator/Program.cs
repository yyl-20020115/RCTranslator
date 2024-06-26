﻿using System.Text;
using System.Text.Encodings.Web;
using OllamaAPI;
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
    public static bool AskHuman = true;

    public static string TrimTail(string text, out string tail)
    {
        tail = string.Empty;

        var s = text.LastIndexOf('(');
        var e = text.LastIndexOf(')');
        if (s >= 0 && e >= s)
        {
            tail = text.Substring(s, e - s + 1);
            return text[..s];
        }
        else
        {
            return text;
        }
    }

    public static string[] Split(string text, char seperator, char quote, StringSplitOptions options = StringSplitOptions.None)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var parts = new List<string>();
        var builder = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == quote)
            {
                builder.Clear();
                builder.Append(c);
                for (i++; i < text.Length; i++)
                {
                    c = text[i];
                    builder.Append(c);
                    if (c == quote)
                    {
                        parts.Add(builder.ToString());
                        builder.Clear();
                        break;
                    }
                }
            }
            else if(c == seperator)
            {
                if(builder.Length > 0)
                {
                    parts.Add(builder.ToString());
                    builder.Clear();
                }
                continue;
            }
            else
            {
                builder.Append(c);
            }
        }
        if (builder.Length > 0)
        {
            parts.Add(builder.ToString());
        }

        return [.. parts];
    }
    public static string DoAsk(string result, ref string part)
    {
        var original = part;
        if (part.Length > 0 && result.Length > 0)
        {
            Console.WriteLine($"\"{part}\" 被翻译为 \"{result}\",是否正确？\n正确请按回车，否则输入正确的翻译。\n输一个空格并回车，对原文不做翻译。");
            var answer = Console.ReadLine();
            if (answer == " ")
            {
                result = original;
            }
            else if (!string.IsNullOrEmpty(answer))
            {
                result = answer;
            }
            Console.WriteLine($"结果:\"{original}\" 被翻译为 \"{result}\"\n\n");
        }
        return result;
    }
    public static string GetHeading(string text) => text[..(text.Length - text.TrimStart().Length)];

    public static async Task<int> Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Encoding encoding = Encoding.GetEncoding("GB2312");


        var lines = File.ReadAllLines("output.txt", encoding);
        var count = lines.Count();
        if(count>0 && lines[^1].Trim() != "")
        {
            //count++;
        }
        var sectionType = SectionType.Init;

        using var writer = new StreamWriter("output.txt",
            encoding,
            new FileStreamOptions { 
                Access = FileAccess.Write, 
                Mode = FileMode.Append,
                Share = FileShare.ReadWrite });

        using var reader = new StreamReader("input.txt", 
            encoding,false, new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.Read
            });

        var index = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            index++;
            var skip = index <= count;
            Console.WriteLine(line);
            var _head = GetHeading(line);
            var _line = line.Trim();
            if (_line.Length == 0
                || _line.StartsWith("//")
                || _line.StartsWith('#'))
            {
                if (!skip)
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
            else if (
                _line.StartsWith("//")
                || _line.StartsWith('#')
                || _line.Length == 0
                )
            {
                continue;
            }
            if (skip) continue;
            if (sectionType == SectionType.Init)
            {
                writer.WriteLine(line);
            }
            else if (sectionType == SectionType.Version)
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
                    if (_line.StartsWith("CAPTION "))
                    {
                        var part = _line[8..].Trim('"');
                        var result = part.Length > 0 ? await OllamaAPIProvider.CallOllama(OllamaAPIProvider.APIUrl, $"请翻译:{part}") : string.Empty;
                        result = AskHuman ? DoAsk(result, ref part) : result;
                        writer.WriteLine($"CAPTION \"{result}\"");
                    }
                    else
                    {
                        var parts = Split(_line[16..], ',', '"', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1 && parts[0].StartsWith('"') && parts[0].EndsWith('"'))
                        {
                            parts[0] = parts[0].Trim('"');
                            var result = parts[0].Length > 0 ? await OllamaAPIProvider.CallOllama(OllamaAPIProvider.APIUrl, $"请翻译:{parts[0]}") : string.Empty;
                            result = AskHuman ? DoAsk(result, ref parts[0]) : result;
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
                if (_line.StartsWith("POPUP "))
                {
                    var parts = Split(_line, ' ', '"', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        parts[1] = parts[1].Trim('"');
                        parts[1] = TrimTail(parts[1], out var tail);
                        var result = parts[1].Length > 0 ? await OllamaAPIProvider.CallOllama(OllamaAPIProvider.APIUrl, $"请翻译:{parts[1]}") : string.Empty;
                        result = AskHuman ? DoAsk(result, ref parts[1]) : result;
                        parts[1] = $"\"{result + tail}\"";
                        writer.WriteLine(_head + string.Join(' ', parts));
                    }
                    else
                    {
                        writer.WriteLine(line);
                    }
                }
                else if (_line.StartsWith("MENUITEM "))
                {
                    var pos = _line.IndexOf(' ');
                    var trailing = _line[(pos + 1)..];
                    var parts = Split(trailing, ',', '"', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        parts[0] = parts[0].Trim('"');
                        parts[0] = TrimTail(parts[0], out var tail);
                        var result = parts[0].Length > 0 ? await OllamaAPIProvider.CallOllama(OllamaAPIProvider.APIUrl, $"请翻译:{parts[0]}") : string.Empty;
                        result = AskHuman ? DoAsk(result, ref parts[0]) : result;
                        parts[0] = $"\"{result + tail}\"";
                        writer.WriteLine(_head + "MENUITEM " + string.Join(',', parts));
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
                var parts = Split(_line, ' ', '"', StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length == 1) //if in two lines
                {
                    var following = reader.ReadLine();
                    if (following != null) 
                    {
                        line += ' ';
                        line += following.Trim();
                        _line = line.Trim();
                        parts = Split(_line, ' ', '"', StringSplitOptions.RemoveEmptyEntries);
                    }
                    else
                    {
                        break;
                    }
                }
                if (parts.Length == 2 && parts[1].StartsWith('"') && parts[1].EndsWith('"'))
                {
                    parts[1] = parts[1].Trim('"');
                    var result = await OllamaAPIProvider.CallOllama(OllamaAPIProvider.APIUrl, $"请翻译:{parts[1]}");
                    result = AskHuman ? DoAsk(result, ref parts[1]) : result;
                    parts[1] = $"\"{result}\"";
                    writer.WriteLine(_head + string.Join(' ', parts));
                }
                else
                {
                    writer.WriteLine(line);
                }
            }
            if (!skip)
            {
                writer.Flush();
            }
        }
        return 0;
    }
}