using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

namespace Test;

public class CustomFilterBinder : FilterBinder
{
    public override Expression BindDynamicPropertyAccessQueryNode(SingleValueOpenPropertyAccessNode openNode, QueryBinderContext context)
    {
        if (openNode.Source is SingleComplexNode complexNode
            && complexNode.TypeReference.Definition is IEdmComplexType complexType
            && complexType.FullName() == "Test.LocalizableString")
        {
            var locale = openNode.Name;

            Expression source = Bind(complexNode.Source, context);
            var namePropertyAccess = Expression.Property(source, complexNode.Property.Name);

            var jsonExtractMethod = GetExtactJsonMethod();
            var jsonPath = Expression.Constant(locale);
            var callJsonExtract = Expression.Call(
                jsonExtractMethod,
                Expression.Convert(namePropertyAccess, typeof(JsonDocument)),
                //namePropertyAccess,
                Expression.Convert(jsonPath, typeof(string))
            );

            return callJsonExtract;
        }

        var expr =  base.BindDynamicPropertyAccessQueryNode(openNode, context);
        return expr;

        //if (node.Source is SingleValuePropertyAccessNode parentNode
        //    && parentNode.Property.Name == "name"
        //    && node.Property.Name == "en")
        //{
        //    var param = Expression.Parameter(typeof(Customer), "c");

        //    // Build EF.Functions.JsonValue(c.ExtendedProperties, "$.en")
        //    var extendedProperties = Expression.Property(param, nameof(Customer.ExtendedProperties));

        //    var jsonPath = Expression.Constant("$.en");

        //    var efFunctions = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions)));
        //    var jsonValueMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.JsonValue),
        //        new[] { typeof(DbFunctions), typeof(string), typeof(string) });

        //    var callJsonValue = Expression.Call(
        //        jsonValueMethod,
        //        efFunctions,
        //        extendedProperties,
        //        jsonPath
        //    );

        //    return callJsonValue;
        //}

        //return base.BindSingleValuePropertyAccessNode(node);
    }
    public override Expression BindPropertyAccessQueryNode(SingleValuePropertyAccessNode propertyAccessNode, QueryBinderContext context)
    {
        return base.BindPropertyAccessQueryNode(propertyAccessNode, context);
    }
    public override Expression BindCollectionOpenPropertyAccessNode(CollectionOpenPropertyAccessNode openCollectionNode, QueryBinderContext context)
    {
        return base.BindCollectionOpenPropertyAccessNode(openCollectionNode, context);
    }

    // jsonb_extract_path_text ( from_json jsonb, VARIADIC path_elems text[] ) → text
    // Extracts JSON sub-object at the specified path as text. (This is functionally equivalent to the #>> operator.)
    // See: https://www.postgresql.org/docs/current/functions-json.html
    [DbFunction("jsonb_extract_path_text", IsBuiltIn = true)]
    // Npgsql supports mapping of JsonDocument to jsonb
    // See: https://www.npgsql.org/efcore/mapping/json.html?tabs=data-annotations%2Cjsondocument#jsondocument-dom-mapping
    public static string ExtractJson(JsonDocument json, string locale) => throw new NotSupportedException();

    public static MethodInfo GetExtactJsonMethod() =>
        typeof(CustomFilterBinder).GetMethod(nameof(ExtractJson), BindingFlags.Static | BindingFlags.Public);

    //protected override Expression BindSingleValuePropertyAccessNode(SingleValuePropertyAccessNode node)
    //{
    //    // Check if the path is name/en
    //    if (node.Source is SingleValuePropertyAccessNode parentNode
    //        && parentNode.Property.Name == "name"
    //        && node.Property.Name == "en")
    //    {
    //        var param = Expression.Parameter(typeof(Customer), "c");

    //        // Build EF.Functions.JsonValue(c.ExtendedProperties, "$.en")
    //        var extendedProperties = Expression.Property(param, nameof(Customer.ExtendedProperties));

    //        var jsonPath = Expression.Constant("$.en");

    //        var efFunctions = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions)));
    //        var jsonValueMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.JsonValue),
    //            new[] { typeof(DbFunctions), typeof(string), typeof(string) });

    //        var callJsonValue = Expression.Call(
    //            jsonValueMethod,
    //            efFunctions,
    //            extendedProperties,
    //            jsonPath
    //        );

    //        return callJsonValue;
    //    }

    //    return base.BindSingleValuePropertyAccessNode(node);
    //}

    public class LocalizableStringConverter : ValueConverter<LocalizableString, string>
    {
        public LocalizableStringConverter()
            : base(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<LocalizableString>(v, (JsonSerializerOptions)null))
        {
        }
    }
}