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
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using static Test.JsonExtractTextExpression;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;

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
            optionsBuilder.UseNpgsql("Host=localhost;Database=TestDb;Username=postgres;Password=postgres", o =>
            {
                o.UseRelationalNulls(true);
            })
                .ReplaceService<SqlNullabilityProcessor, CustomSqlNullabilityProcessor>() // new
                .ReplaceService<NpgsqlQuerySqlGenerator, CustomNpgsqlQuerySqlGenerator>() // new

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

        //.Property(c => c.Name).HasColumnType("jsonb")
        modelBuilder.Entity<Customer>().Property(c => c.Name).HasJsonConversion();

        // See: https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping#mapping-a-method-to-a-custom-sql
        modelBuilder.HasDbFunction(
            CustomFilterBinder.GetExtactJsonMethod())
            .HasTranslation(
            args =>
                new JsonExtractTextExpression(
                        args.First(),
                        args.Skip(1).First(),
                        new StringTypeMapping("jsonb", System.Data.DbType.String)
                    )
            );
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

public class JsonExtractTextExpression : SqlExpression
{
    public SqlExpression JsonColumn { get; }
    public SqlExpression Path { get; }

    public JsonExtractTextExpression(SqlExpression jsonColumn, SqlExpression path, RelationalTypeMapping typeMapping)
        : base(typeof(string), typeMapping)
    {
        JsonColumn = jsonColumn;
        Path = path;
    }

    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var json = (SqlExpression)visitor.Visit(JsonColumn);
        var path = (SqlExpression)visitor.Visit(Path);

        if (json != JsonColumn || path != Path)
        {
            return new JsonExtractTextExpression(json, path, TypeMapping);
        }

        return this;
    }

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(JsonColumn);
        expressionPrinter.Append(" ->> ");
        expressionPrinter.Visit(Path);
    }

    //protected override Expression Accept(ExpressionVisitor visitor)
    //{
    //    if (visitor is QuerySqlGenerator querySqlGenerator)
    //    {
    //        return querySqlGenerator is NpgsqlQuerySqlGenerator npgsqlGenerator
    //            ? npgsqlGenerator.VisitJsonExtractText(this)
    //            : querySqlGenerator.VisitExtension(this);
    //    }

    //    return base.Accept(visitor);
    //}

    public override bool Equals(object obj)
    {
        return obj is JsonExtractTextExpression other &&
            base.Equals(other) &&
            JsonColumn.Equals(other.JsonColumn) &&
            Path.Equals(other.Path);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), JsonColumn, Path);
    }

    public class CustomNpgsqlQuerySqlGenerator : NpgsqlQuerySqlGenerator
    {
        public CustomNpgsqlQuerySqlGenerator(
            QuerySqlGeneratorDependencies dependencies,
            IRelationalTypeMappingSource typeMappingSource,
            bool reverseNullOrderingEnabled,
            Version postgresVersion)
            : base(dependencies, typeMappingSource, reverseNullOrderingEnabled, postgresVersion)
        {
        }

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (extensionExpression is JsonExtractTextExpression jsonExtract)
            {
                Sql.Append("(");
                Visit(jsonExtract.JsonColumn);
                Sql.Append(" ->> ");
                Visit(jsonExtract.Path);
                Sql.Append(")");

                return extensionExpression;
            }

            return base.VisitExtension(extensionExpression);
        }
    }
}

public class CustomSqlNullabilityProcessor : NpgsqlSqlNullabilityProcessor
{
    public CustomSqlNullabilityProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, bool useRelationalNulls)
        : base(dependencies, useRelationalNulls)
    {
    }

    protected override SqlExpression VisitCustomSqlExpression(
        SqlExpression sqlExpression,
        bool allowOptimizedExpansion,
        out bool nullable)
    {
        if (sqlExpression is JsonExtractTextExpression)
        {
            nullable = true; // or false depending on your logic
            return sqlExpression; // Return the same expression or a modified one
        }

        return base.VisitCustomSqlExpression(sqlExpression, allowOptimizedExpansion, out nullable);
    }
}