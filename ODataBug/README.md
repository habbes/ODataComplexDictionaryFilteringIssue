# OData + EF Core + SQLite + ComplexType Bug

This project demonstrates the bug we're running into with the above combination.

To see the bug, do `dotnet run .\Test.csproj`, and then open a browser to

```
http://localhost:5212/odata/customers?$filter=name/en%20eq%20%27Robert%20Williams%27
```

Note the exception in the output.

To see how we expect things to work, do `dotnet run .\Test.csproj -- InMemoryDb=true`

This configures the application to use an in memory database, rather than SQLite: Microsoft.EntityFrameworkCore.InMemory vs Microsoft.EntityFrameworkCore.Sqlite
