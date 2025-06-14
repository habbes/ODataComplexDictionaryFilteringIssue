﻿using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Test;

public class SqliteCustomFilterBinder : FilterBinder
{
    public override Expression BindDynamicPropertyAccessQueryNode(SingleValueOpenPropertyAccessNode openNode, QueryBinderContext context)
    {
        // We want to convert an OData expression like "Name/en" to a SQLite expression like "json_extract(Name, '$.en')".
        if (openNode.Source is SingleComplexNode complexNode
            && complexNode.TypeReference.Definition is IEdmComplexType complexType
            && complexType.FullName() == "Test.LocalizableString")
        {
            // This is the name of the dynamic property access, e.g. "en", "fr", etc.
            var locale = openNode.Name;

            var jsonPath = Expression.Constant($"$.{locale}");

            Expression source = Bind(complexNode.Source, context);
            var callJsonValue = Expression.Call(
                // this is our custom JsonExtract method that we configured to map to json_extract
                GetJsonExtractMethod(),
                // Since our method takes a string, we have to convert Customer.Name property
                // of type LocalizableString to string. This works because we have
                // defined an explicit conversion operator in LocalizableString.
                Expression.Convert(
                    Expression.Property(source, complexNode.Property.Name),
                    typeof(string)),
                jsonPath);

            return callJsonValue;
        }
        return base.BindDynamicPropertyAccessQueryNode(openNode, context);
    }

    // We map to this method to SQLite's json_extract function.
    // We have also have to register this method with EF Core so it knows how to translate it to SQL.
    // See: https://www.sqlite.org/json1.html#the_json_extract_function
    [DbFunction("json_extract")]
    public static string JsonExtract(string json, string path) =>
        throw new NotSupportedException("This method should not be called client-side. It should be translated to a DB function.");

    public static MethodInfo GetJsonExtractMethod() =>
        typeof(SqliteCustomFilterBinder).GetMethod(
            nameof(JsonExtract),
            BindingFlags.Static | BindingFlags.Public,
            [typeof(string), typeof(string)]);
}