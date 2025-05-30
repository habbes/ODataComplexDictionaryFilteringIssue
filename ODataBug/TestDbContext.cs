using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

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
            // Tells EF Core to convert the Name property to a JSON string in the database
            modelBuilder.Entity<Customer>()
                .Property(c => c.Name)
                .HasConversion<LocalizableStringConverter>();

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
            modelBuilder.Entity<Customer>()
                .Property(c => c.Name)
                .HasColumnType("jsonb")
                .HasConversion<LocalizableStringConverter>();

            // See: https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
            modelBuilder.HasDbFunction(
                PostgresCustomerFilterBinder.GetJsonExtractMethod());
        }
    }
}

public class LocalizableStringConverter : ValueConverter<LocalizableString, string>
{
    public LocalizableStringConverter()
        : base(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<LocalizableString>(v, (JsonSerializerOptions)null))
    {
    }
}