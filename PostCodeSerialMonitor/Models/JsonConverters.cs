using System;
using System.Buffers.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace PostCodeSerialMonitor.Models;

public class Base64UrlByteArrayConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(Assets.Resources.ExpectedStringToken);
        }

        string base64String = reader.GetString()
            ?? throw new InvalidDataException(Assets.Resources.FailedReadingString);
        

        try
        {
            return Base64Url.DecodeFromChars(base64String);
        }
        catch (FormatException ex)
        {
            throw new JsonException(Assets.Resources.InvalidBase64String, ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        string base64String = Base64Url.EncodeToString(value);
        writer.WriteStringValue(base64String);
    }
}