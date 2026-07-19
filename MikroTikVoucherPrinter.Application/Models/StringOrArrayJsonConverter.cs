using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikroTikVoucherPrinter.Application.Models;

/// <summary>
/// JsonConverter يقبل حقل releaseNotes كـ:
///   - نص فردي:      "ملاحظات الإصدار"         → ["ملاحظات الإصدار"]
///   - مصفوفة نصوص: ["سطر 1", "سطر 2"]          → ["سطر 1", "سطر 2"]
///   - null / مفقود:  (لا شيء)                  → []
///
/// هذا يضمن التوافق مع الإصدارات القديمة من update.json التي تستخدم نصاً فردياً.
/// </summary>
internal sealed class StringOrArrayJsonConverter : JsonConverter<List<string>>
{
    public override List<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            // مصفوفة: ["سطر 1", "سطر 2"]
            case JsonTokenType.StartArray:
            {
                var result = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                        result.Add(reader.GetString() ?? string.Empty);
                }
                return result;
            }

            // نص فردي: "ملاحظات الإصدار"
            case JsonTokenType.String:
            {
                var value = reader.GetString();
                return string.IsNullOrWhiteSpace(value)
                    ? new List<string>()
                    : new List<string> { value };
            }

            // null أو أي نوع آخر
            default:
                reader.Skip();
                return new List<string>();
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<string> value,
        JsonSerializerOptions options)
    {
        // دائماً نكتب مصفوفة
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
