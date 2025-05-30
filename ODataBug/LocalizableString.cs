using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Test;

[JsonConverter(typeof(LocalizableStringJsonConverter))]
public sealed class LocalizableString
{
    //public LocalizableString()
    //{
    //}

    //public LocalizableString(IDictionary<string, string> dictionary)
    //{
    //    foreach (var kvp in dictionary)
    //    {
    //        ExtendedProperties[kvp.Key] = kvp.Value;
    //    }
    //}

    //public Dictionary<string, object> ExtendedProperties { get; set; } = new();
    private ReadOnlyDictionary<string, string> _dictionary;


    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizableString"/> class.
    /// </summary>
    public LocalizableString()
    {
        _dictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizableString"/> class from the specified dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary that contains the data to copy.</param>
    public LocalizableString(IDictionary<string, string> dictionary)
    {
        _dictionary = new ReadOnlyDictionary<string, string>(dictionary);
    }

    [NotMapped]
    /// <summary>
    /// Gets the extended properties used with OData query.
    /// </summary>
    public Dictionary<string, object> ExtendedProperties
    {
        get
        {
            return _dictionary.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
        }
        set
        {
            _dictionary = new ReadOnlyDictionary<string, string>(value.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value));
        }
    }

    public static explicit operator string(LocalizableString d)
    {
        return JsonSerializer.Serialize(d);
    }

    public static explicit operator LocalizableString(string b) => JsonSerializer.Deserialize<LocalizableString>(b);

    public static explicit operator JsonDocument(LocalizableString d)
    {
        var json = JsonSerializer.Serialize(d);
        return JsonDocument.Parse(json);
    }

    internal static bool AreEqual(LocalizableString? left, LocalizableString? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left._dictionary.Count != right._dictionary.Count)
        {
            return false;
        }

        foreach (var (key, value) in left._dictionary)
        {
            if (!right._dictionary.TryGetValue(key, out var otherValue))
            {
                return false;
            }
            if (!string.Equals(value, otherValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the translation for the specified language.
    /// </summary>
    /// <param name="key">The language id.</param>
    /// <returns>The translation.</returns>
    /// <exception cref="KeyNotFoundException">The language id does not exist.</exception>"
    public object this[string key]
    {
        get { return _dictionary[key]; }
    }


    class LocalizableStringJsonConverter : JsonConverter<LocalizableString>
    {
        public override LocalizableString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options) ?? new Dictionary<string, string>();
            return new LocalizableString(dict);
        }

        public override void Write(Utf8JsonWriter writer, LocalizableString value, JsonSerializerOptions options)
        {

            JsonSerializer.Serialize(writer, value._dictionary, options);
        }

        
    }
}

//class LocalizableStringJsonConverter : JsonConverter<LocalizableString>
//{
//    public override LocalizableString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//    {
//        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
//        return new LocalizableString
//        {
//            ExtendedProperties = dict ?? new Dictionary<string, object>()
//        };
//    }

//    public override void Write(Utf8JsonWriter writer, LocalizableString value, JsonSerializerOptions options)
//    {
//        JsonSerializer.Serialize(writer, value.ExtendedProperties, options);
//    }
//}

