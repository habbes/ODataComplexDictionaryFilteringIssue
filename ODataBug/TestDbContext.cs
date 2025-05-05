using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Test;

public class TestDbContext(IConfiguration configuration) : DbContext
{
    private readonly string dbKind = configuration.GetValue<string>("DbKind")!;

    public DbSet<Customer> Customers { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (dbKind == "memory")
        {
            optionsBuilder.UseInMemoryDatabase("TestDb");
        }
        else if (dbKind == "sqlite")
        {
            optionsBuilder.UseSqlite("Data Source=Test.db")
                .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Trace)
                .EnableSensitiveDataLogging();
        }
        else if (dbKind == "posgres")
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=TestDb;Username=postgres;Password=postgres")
                .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Trace)
                .EnableSensitiveDataLogging();
        }
        else
        {
            throw new NotSupportedException($"Database kind '{dbKind}' is not supported.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>()
            //.OwnsOne(customer => customer.Name,
            //    nameBuilder =>
            //    {
            //        nameBuilder.ToJson();
            //        nameBuilder.Ignore(n => n.ExtendedProperties);
            //        //nameBuilder.OwnsOne(n => n.ExtendedProperties);
            //    }
            //)
            .HasData(
                new Customer
                {
                    Id = 1,
                    Name = new LocalizableString(new Dictionary<string, string>
                    {
                        { "en", "John Doe" },
                        { "fr", "Jean Dupont" }
                    })
                },
                new Customer
                {
                    Id = 2,
                    Name = new LocalizableString(new Dictionary<string, string>
                    {
                        { "en", "Robert Williams" },
                        { "fr", "Robert Guillaume" }
                    })
                },
                new Customer
                {
                    Id = 3,
                    Name = new LocalizableString(new Dictionary<string, string>
                    {
                        { "en", "John Smith" },
                        { "fr", "Jean Lefevre" }
                    })
                });
        modelBuilder.Entity<Customer>().Property(c => c.Name).HasJsonConversion();

        // See: https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
        // See: https://www.sqlite.org/json1.html#the_json_extract_function
        modelBuilder.HasDbFunction(
            CustomFilterBinder.GetJsonExtractMethod())
            .HasName("json_extract");

        modelBuilder.Entity<Customer>()
        .Property<string>("NameJson") // shadow or real property
        .HasColumnName("Name") // actual database column
        .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
    }
}

public static class Extensions
{
    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions();

    public static PropertyBuilder<LocalizableString> HasJsonConversion(this PropertyBuilder<LocalizableString> propertyBuilder)
    {
        var converter = new ValueConverter<LocalizableString, string>(
            v => JsonSerializer.Serialize(v.ExtendedProperties, _serializerOptions),
            v => new LocalizableString(JsonSerializer.Deserialize<Dictionary<string, string>>(v, _serializerOptions) ?? new()));

        var comparer = new ValueComparer<LocalizableString>(
            (l, r) => LocalizableString.AreEqual(l, r),
            v => JsonSerializer.Serialize(v.ExtendedProperties, _serializerOptions).GetHashCode(),
            v => new LocalizableString(v.ExtendedProperties.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value)));

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueConverter(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);
        return propertyBuilder;
    }
}
