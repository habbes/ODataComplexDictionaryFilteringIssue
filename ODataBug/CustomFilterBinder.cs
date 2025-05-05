using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Linq.Expressions;

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
        }
        return base.BindDynamicPropertyAccessQueryNode(openNode, context);
    }
    public override Expression BindPropertyAccessQueryNode(SingleValuePropertyAccessNode propertyAccessNode, QueryBinderContext context)
    {
        return base.BindPropertyAccessQueryNode(propertyAccessNode, context);
    }
    public override Expression BindCollectionOpenPropertyAccessNode(CollectionOpenPropertyAccessNode openCollectionNode, QueryBinderContext context)
    {
        return base.BindCollectionOpenPropertyAccessNode(openCollectionNode, context);
    }
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
}