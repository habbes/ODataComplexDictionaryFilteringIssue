using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Test;

public class Customer
{
    public int Id { get; set; }
    
    public required LocalizableString Name { get; set; }

    //[Column("Name")]
    [IgnoreDataMember]
    [JsonIgnore]
    public string? NameJson { get; set; }
}