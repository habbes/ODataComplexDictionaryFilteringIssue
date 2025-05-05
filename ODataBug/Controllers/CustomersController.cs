using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Test.Controllers;

[ApiController]
[Route("[controller]")]
public class CustomersController(TestDbContext context) : ODataController
{
    [EnableQuery]
    public ActionResult<IEnumerable<Customer>> Get()
    {
        return Ok(context.Customers);
    }

    public ActionResult<Customer> Post(Customer customer)
    {
        context.Customers.Add(customer);
        context.SaveChanges();
        return Created(customer);
    }
}
