using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using static Test.CustomFilterBinder;

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
        //modelBuilder.Entity<Customer>()
        //    .OwnsOne(customer => customer.Name,
        //        nameBuilder =>
        //        {
        //            nameBuilder.ToJson();
        //            nameBuilder.Ignore(n => n.ExtendedProperties);
        //            //nameBuilder.OwnsOne(n => n.ExtendedProperties);
        //        }
        //    );

        //modelBuilder.Entity<Customer>().Property(c => c.Name).HasColumnType("jsonb");
            //.HasData(
            //    new Customer
            //    {
            //        Id = 1,
            //        Name = new LocalizableString(new Dictionary<string, string>
            //        {
            //            { "en", "John Doe" },
            //            { "fr", "Jean Dupont" }
            //        })
            //    },
            //    new Customer
            //    {
            //        Id = 2,
            //        Name = new LocalizableString(new Dictionary<string, string>
            //        {
            //            { "en", "Robert Williams" },
            //            { "fr", "Robert Guillaume" }
            //        })
            //    },
            //    new Customer
            //    {
            //        Id = 3,
            //        Name = new LocalizableString(new Dictionary<string, string>
            //        {
            //            { "en", "John Smith" },
            //            { "fr", "Jean Lefevre" }
            //        })
            //    });
        modelBuilder.Entity<Customer>().Property(c => c.Name).HasJsonConversion();

        modelBuilder.HasDbFunction(
            CustomFilterBinder.GetExtactJsonMethod(), b => b.HasName("jsonb_extract_path_text"));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<LocalizableString>()
            .HaveConversion<LocalizableStringConverter>()
            .HaveColumnType("jsonb"); // for Postgres
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

public class JsonExtractTextTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo _methodInfo = GetExtactJsonMethod();

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public JsonExtractTextTranslator(ISqlExpressionFactory sqlExpressionFactory)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
    }

    public SqlExpression Translate(
        SqlExpression instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method == _methodInfo)
        {
            // Create JSONB ->> TEXT translation
            var jsonColumn = arguments[0];
            var propertyName = arguments[1];

            return _sqlExpressionFactory.Function(
                "->>",
                new[] { jsonColumn, propertyName },
                nullable: false,
                argumentsPropagateNullability: null,
                returnType: typeof(string)
            );
        }

        return null!;
    }
}