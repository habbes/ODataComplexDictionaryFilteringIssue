using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Test;

public class Customer
{
    public int Id { get; set; }

    public LocalizableString Name { get; set; }
}