using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.OData.ModelBuilder;
using Test;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TestDbContext>();

var modelBuilder = new ODataConventionModelBuilder();
Type localizableStringType = typeof(LocalizableString);
modelBuilder.EntitySet<Customer>("Customers");
var nameProp = modelBuilder.EntityType<Customer>().ComplexProperty(c => c.Name);

builder.Services.AddControllers().AddOData(
    options => options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(null).AddRouteComponents(
        "odata",
        modelBuilder.GetEdmModel(),
        svc =>
        {
            // Enable this if testing with sqlite database
            //svc.AddSingleton<IFilterBinder, SqliteCustomFilterBinder>();
            // Enable this if testing with postgres database
            svc.AddSingleton<IFilterBinder, PostgresCustomerFilterBinder>();
        }));

var app = builder.Build();


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

using var scope = app.Services.CreateScope();
{
    var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();
}

app.Run();
