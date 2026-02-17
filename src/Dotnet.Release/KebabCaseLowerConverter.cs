using System.Text.Json.Serialization;

using System.Text.Json;

namespace Dotnet.Release;

public class KebabCaseLowerStringEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public KebabCaseLowerStringEnumConverter() : base(JsonNamingPolicy.KebabCaseLower)
    { }
}
