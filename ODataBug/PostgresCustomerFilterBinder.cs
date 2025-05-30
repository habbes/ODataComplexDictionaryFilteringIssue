using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Test;

public class PostgresCustomerFilterBinder : FilterBinder
{
    public override Expression BindDynamicPropertyAccessQueryNode(SingleValueOpenPropertyAccessNode openNode, QueryBinderContext context)
    {
        // We want to convert an OData expression like "Name/en"
        // to a Postgres expression like "jsonb_extract_path_text(Name, 'en')" where Name should be of type jsonb.
        if (openNode.Source is SingleComplexNode complexNode
            && complexNode.TypeReference.Definition is IEdmComplexType complexType
            && complexType.FullName() == "Test.LocalizableString")
        {
            // This is the name of the dynamic property access, e.g. "en", "fr", etc.
            var locale = openNode.Name;

            Expression source = Bind(complexNode.Source, context);

            var callJsonValue = Expression.Call(
                // this is our custom JsonExtract method that we configured to map to json_extract
                GetJsonExtractMethod(),
                // Since our method takes a JsonDocument, we have to convert Customer.Name property
                // of type LocalizableString to JsonDocument`. This works because we have
                // defined an explicit conversion operator in LocalizableString.
                Expression.Convert(
                    Expression.Property(source, complexNode.Property.Name),
                    typeof(JsonDocument)),
                Expression.Convert(
                    Expression.Constant(locale),
                    typeof(string)));

            return callJsonValue;
        }

        return base.BindDynamicPropertyAccessQueryNode(openNode, context);
    }

    // jsonb_extract_path_text ( from_json jsonb, VARIADIC path_elems text[] ) → text
    // Extracts JSON sub-object at the specified path as text. (This is functionally equivalent to the #>> operator.)
    // See: https://www.postgresql.org/docs/current/functions-json.html
    [DbFunction("jsonb_extract_path_text", IsBuiltIn = true)]
    // Npgsql supports mapping of JsonDocument to jsonb
    // See: https://www.npgsql.org/efcore/mapping/json.html?tabs=data-annotations%2Cjsondocument#jsondocument-dom-mapping
    public static string JsonExtract(JsonDocument json, string locale) =>
        throw new NotSupportedException("This method should not be called client-side. It should be translated to a DB function.");

    public static MethodInfo GetJsonExtractMethod() =>
        typeof(PostgresCustomerFilterBinder).GetMethod(
            nameof(JsonExtract),
            BindingFlags.Static | BindingFlags.Public,
            [typeof(JsonDocument), typeof(string)]);
}