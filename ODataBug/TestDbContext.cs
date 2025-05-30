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
        else if (dbKind == "postgres")
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

        if (dbKind == "sqlite")
        {
            // SQLite does not have a built-in JSON type, JSON values are stored as TEXT.
            // So we need to do things a bit manually to get things to work.
            // Tells EF Core to convert the Name property to a JSON string in the database
            modelBuilder.Entity<Customer>().Property(c => c.Name).HasJsonConversion();

            // I had also tried to use the OwnsOne and ToJson() method, but EF Core
            // was not able to translate the expression tree from the filter binder to SQL
            // i.e. modelBuilder.Entity<Customer>().OwnsOne(c => c.Name, nameBuilder => nameBuilder.ToJson());

            // See: https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
            modelBuilder.HasDbFunction(
                SqliteCustomFilterBinder.GetJsonExtractMethod());
        }
        else if (dbKind == "postgres")
        {
            // PostgreSQL has native jsonb type, so we can use it directly.
            // EF core will use the LocalizableString type's JsonConverter to convert name
            // columns to JSON.
            modelBuilder.Entity<Customer>().Property(c => c.Name).HasColumnType("jsonb").HasConversion<LocalizableStringJsonConverter>();

            // See: https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
            modelBuilder.HasDbFunction(
                PostgresCustomerFilterBinder.GetJsonExtractMethod());
        }
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
