using System.ComponentModel.DataAnnotations.Schema;

namespace Test;

public class Customer
{
    public int Id { get; set; }
    
    public required LocalizableString Name { get; set; }
}