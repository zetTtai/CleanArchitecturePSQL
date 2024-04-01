<h1>CLEAN ARCHITECTURE TEMPLATE + POSTGRESQL</h1>

Hello everyone, this template was inspired by the well-known template created by [Jason Taylor](https://github.com/jasontaylordev). It aims to provide a straightforward and efficient approach to creating applications using Clean Architecture and ASP.NET Core. For more details and to appreciate the original work, please visit the [original template repository](https://github.com/jasontaylordev/CleanArchitecture) and consider giving it a star!

This variant of the template is designed to work with PostgreSQL, specially using [Supabase](https://supabase.com/). While the original repository offers comprehensive details about the template, this adaptation focuses on integrating PostgreSQL for those interested in utilizing this database system for their projects. If you find this project useful, your support through starring this project, as well as Jason Taylor's project, would be much appreciated!


<h1>INDEX</h1>

- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Installation](#installation)
- [Migrating from SQLServer to PostgreSQL](#migrating-from-sqlserver-to-postgresql)
- [Adapting Functional Tests](#adapting-functional-tests)
- [Generate New Migrations and Update Database Using Entity Framework](#generate-new-migrations-and-update-database-using-entity-framework)
    - [Installing dotnet-ef](#installing-dotnet-ef)
    - [Generating Migrations and Updating Database](#generating-migrations-and-updating-database)
    - [Update Database](#update-database)
    - [Before Building the Application](#before-building-the-application)
- [Using Supabase](#using-supabase)
    - [Prerequisites](#prerequisites-1)
    - [Configuring appsettings.json](#configuring-appsettingsjson)
    - [Important Security Note](#important-security-note)
    - [Testing Connection and Generating Migrations](#testing-connection-and-generating-migrations)
    - [Setup Script for Supabase](#setup-script-for-supabase)
- [Extra](#extra)
    - [What is the difference between the original?](#what-is-the-difference-between-the-original)
    - [Deploying using this repository](#deploying-using-this-repository)


# Getting Started

### Prerequisites

Before getting started, ensure you have the following installed:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) - Use the latest version available
- [Node.js](https://nodejs.org/en/) - Install the latest LTS version if using Angular or React
- [Docker](https://www.docker.com/) - Necessary for containerization

For deployment only: 

- [Azure Command-line Interface](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (CLI) (latest version)
- [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4) (latest version)
- [GitHub Command-line Interface](https://cli.github.com/) (CLI) (latest version)


### Installation

To adapt this template for PostgreSQL, start by creating a new solution that uses SQLServer. You can install the necessary .NET template with the following command:

```bash
dotnet new install Clean.Architecture.Solution.Template::8.0.5
```

I recommend you to create a new repository using `GitHub Command-line Interface` because it is a faster way to create it:

```bash
gh auth login
gh repo create YourProjectName --public --clone
cd YourProjectName
```

Next, create a new solution using one of these commands, depending on your project needs (Do it inside of YourProjectName folder):

For Angular and ASP.NET Core:

```bash
dotnet new ca-sln -cf Angular
```

For React and ASP.NET Core:

```bash
dotnet new ca-sln -cf React
```

For an ASP.NET Core Web API:

```bash
dotnet new ca-sln -cf None
```

To verify the template works with SQLServer by default, use the following commands:

```bash
dotnet build -tl
cd src\Web
dotnet run
```

# Migrating from SQLServer to PostgreSQL

To adapt the template to PostgreSQL, follow these steps:

1. **Remove Existing Migrations:** Delete the contents within `src\Infrastructure\Migrations`.

2. **Install PostgreSQL NuGet Package:** Execute the following command in the `src\Infrastructure` directory:

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Optionally, if you're not using SQLServer, you can remove its package:

```bash
dotnet remove package Microsoft.EntityFrameworkCore.SqlServer
```

3. **Modify `DependencyInjection.cs`:** Update the file located in `src\Infrastructure` to use Npgsql by changing `UseSqlServer` to `UseNpgsql`:

```csharp
services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
    options.UseNpgsql(connectionString); // Modified line
});
```

4. **Update Connection String:** In `src\Web\appsettings.json`, modify the `DefaultConnection` to reflect your PostgreSQL settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=exampledb;Username=user;Password=pass"
  }
}
```

And that's all thanks to this kind of architecture! But sadly, we are not done yet. In order to correcly build our project we need to do more changes.

# Adapting Functional Tests

Everytime we build the application, all test must be passed, otherwise it won't finish the building.

To accommodate PostgreSQL, we will have to modify the following files:
-  `tests\Application.FunctionalTests\TestcontainersTestDatabase.cs`
-  `tests\Application.FunctionalTests\SqlServerTestDatabase.cs`
-  `tests\Application.FunctionalTests\CustomWebApplicationFactory.cs`

This template includes setting up a Docker container for PostgreSQL to ensure tests run in isolated environments and we won't need to create a database locally for tests purposes.

Firstly, we need to install `Testcontainers.PostgreSql` and `Npgsql` in our `Application.FunctionalTests` project with these commands:

```bash
cd tests\Application.FunctionalTests
dotnet add package Testcontainers.PostgreSql
dotnet add package Npgsql
```
(Optional: remove unused MsSql)
```bash
cd tests\Application.FunctionalTests
dotnet remove package Testcontainers.MsSql
```

Open the `TestcontainersTestDatabase.cs` file and modify its content, you can copy and paste this code, check comments to notice what we have changed.

```csharp
using System.Data.Common;
using CleanArchitecturePSQL.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace CleanArchitecturePSQL.Application.FunctionalTests;

public class TestcontainersTestDatabase : ITestDatabase
{
    private readonly PostgreSqlContainer _container; // MsSqlContainer
    private NpgsqlConnection _connection = null!;
    private string _connectionString = null!;
    private Respawner _respawner = null!;

    public TestcontainersTestDatabase()
    {
        _container = new PostgreSqlBuilder() // MsSqlBuilder
            .WithAutoRemove(true)
            .Build();
    }

    public async Task InitialiseAsync()
    {
        await _container.StartAsync();

        _connectionString = _container.GetConnectionString();

        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync(); // Added

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString) // UseSqlServer
            .Options;

        var context = new ApplicationDbContext(options);

        await context.Database.MigrateAsync(); // Modified

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres, // Added
            SchemasToInclude = ["public"], // Added
            TablesToIgnore = ["__EFMigrationsHistory"] // (Optional) Equal to: new Respawn.Graph.Table[] { "__EFMigrationsHistory" }
        });
    }

    public DbConnection GetConnection()
    {
        return _connection;
    }

    public async Task ResetAsync()
    {
        await _respawner.ResetAsync(_connection); // await _respawner.ResetAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}

```

This file will be used in `TestDatabaseFactory.cs` and it will create a Docker container that holds a PostgreSQL database, which means that now all our tests will be executed in this type of database.

Some worth-mentioning points to notice in these changes are, for example, the fact that with `NpgsqlConnection()`, we have to **open the connection** (`await _connection.OpenAsync();`), which wasn't required in the original template. Another important change is the one made in the ResetAsync function; if we use a string (`_connectionString`), Respawner will automatically try to create a **SqlConnection**, so we have to use the `DbConnection` object (`_connection`).


Secondly, we can remove `SqlServerTestDatabase.cs` because it is unused during the tests and we no need it. If you really want to keep the file I recommend you to do similar changes as we have done to `TestcontainersTestDatabase.cs` 


Finally, we need to modify `CustomWebApplicationFactory.cs`. In the `ConfigureWebHost` function it creates an ApplicationDbContext so we need to modify again the expected database:
```csharp
using System.Data.Common;
using CleanArchitecturePSQL.Application.Common.Interfaces;
using CleanArchitecturePSQL.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanArchitecturePSQL.Application.FunctionalTests;

using static Testing;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection;

    public CustomWebApplicationFactory(DbConnection connection)
    {
        _connection = connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services
                .RemoveAll<IUser>()
                .AddTransient(provider => Mock.Of<IUser>(s => s.Id == GetUserId()));

            services
                .RemoveAll<DbContextOptions<ApplicationDbContext>>()
                .AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                    options.UseNpgsql(_connection); // UseSqlServer
                });
        });
    }
}
```

# Generate New Migrations and Update Database Using Entity Framework

This template supports code-first databases, enabling automatic migration generation and application to the database with code changes. It's recommended to perform the following commands in the **root folder** of your project.

### Installing dotnet-ef

First, install `dotnet-ef` globally, if it's not already installed:

```bash
dotnet tool install --global dotnet-ef
```

### Generating Migrations and Updating Database

When modifications are made in the Domain layer, execute `dotnet ef migrations` and `dotnet ef database update`. Here's how to create a migration named "InitialMigrationPostgreSQL":

```bash
dotnet ef migrations add "InitialMigrationPostgreSQL" --project src\Infrastructure --startup-project src\Web --output-dir Data\Migrations
```

### Update Database

```bash
dotnet ef database update --project src\Infrastructure --startup-project src\Web
```

After removing SQLServer migrations as previously guided, create a new InitialMigration for PostgreSQL. These commands utilize the `DefaultConnection` specified in `src/Web/appsettings.json` (Refer to the `Migrating from SQLServer to PostgreSQL` section).

### Before Building the Application

If a PostgreSQL database is set up on your machine, navigate to the **root folder** and launch the application using the Visual Studio IDE or the following commands:

```bash
dotnet build -tl
cd src\Web
dotnet run
```

Do not forget to have Docker Desktop open or the service enable before running the application.


# Using Supabase

Supabase is an open-source alternative to Firebase, offering a generous free plan perfect for development. This choice is aimed at providing a more accessible start in the early stages of project development, leveraging PostgreSQL databases.

### Prerequisites

1. **Supabase Account**: Create an account on Supabase and set up a development database.
2. **Azure Account**: Ensure you have an Azure account with an active subscription.

### Configuring appsettings.json

Modify the `DefaultConnection` in `src\Web\appsettings.json` to match your Supabase database connection details. You can find these details under **Project Settings > Configuration > Database** in your Supabase project dashboard.

Your `src\Web\appsettings.json` should resemble the following, with your connection details:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=***.supabase.com;Database=***;Username=***;Password=***;SSL Mode=Require;Trust Server Certificate=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### Important Security Note

This connection string contains sensitive information. **Never** upload it to a public repository. Ensure your commits exclude this file if it contains actual connection details. Also, include `"SSL Mode=Require;Trust Server Certificate=true;"` in the connection string to connect to Supabase servers.

### Testing Connection and Generating Migrations

With the database connection established, you can proceed to generate Entity Framework migrations. Refer to the "Generating Migrations and Updating Database" section for commands.

### Setup Script for Supabase

The `setup.ps1` script in the [repository]() is tailored for setting up the project with Supabase. To use this script:

1. Modify `.azure` folder with the one that can be found in this [repository](https://github.com/zetTtai/CleanArchitecturePSQL/tree/main/.azure/bicep), This folder contains a modified version of **main.bicep** from the original template that is used to create an AppServices (F1, free tier) in Azure for each environment.
2. Modify `.scripts` folder with the one that can be found in this [repository](https://github.com/zetTtai/CleanArchitecturePSQL/tree/main/.scripts). This folder contians a modified version of **setup.ps1** from the original template.
3. Modify `.github` folder with the one that can be found in this [repository](https://github.com/zetTtai/CleanArchitecturePSQL/tree/main/.github). This folder contians a modified version of **deploy.yml** from the original template.
4. Modify `.scripts\environments.json` with your connection strings for different environments:

```json
{
  "Dev": {
    "Name": "Development",
    "ConnectionString": "Host=***.supabase.com;Database=***;Username=***;Password=***"
  },
  "Stg": {
    "Name": "Staging",
    "ConnectionString": "Host=***.supabase.com;Database=***;Username=***;Password=***"
  },
  "Prd": {
    "Name": "Production",
    "ConnectionString": "Host=***.supabase.com;Database=***;Username=***;Password=***"
  }
}
```

These connection strings contain sensitive information. **Never** upload them to a public repository. Ensure your commits exclude this file if it contains actual connection details. In this section there is **no need** to include `"SSL Mode=Require;Trust Server Certificate=true;"` in the connection strings. (This will be automatically added in the workflows)

5. **Modify** `DependencyInjection.cs`:
```csharp
public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
{
  var connectionString = Environment.GetEnvironmentVariable("APPSETTING_CONNECTION_STRING")
        ?? configuration.GetConnectionString("DefaultConnection"); // Modified
  ...
```

6. **Open** a terminal (Powershell 7 or higher) as **administrator**.
7. **Log in** with GitHub CLI:
```bash
gh auth login
```
8. **Log in** with Azure CLI:
```bash
az login --use-device-code
```
9. **Execute** setup script (I strongly recommend you to check the official documentation of [deployment](https://github.com/jasontaylordev/CleanArchitecture/wiki/Deployment) before executing this script)
```bash
.\scripts\setup.ps1
```
10. After the setup is complete, make your initial commit to the **main** branch. Note that committing to a branch named **master** will not trigger the workflow unless you've adjusted the workflow trigger settings accordingly. This step allows you to proceed with the deployment.

11. It's common for the **first deployment attempt to fail** if the Azure resources are not yet available. In such cases, verify the existence of these resources in your Azure account and **rerun all the jobs** in the workflow, not just the ones that failed. This ensures that any temporary issues with resource availability are resolved, and the deployment process can complete successfully.


# Extra

### What is the difference between the original?

These changes make it easier to create free tier app services. An environment variable named **"APPSETTING_CONNECTION_STRING"** is introduced, containing the connection string relevant to the current environment.

Within the build.yml file, we define two environment variables of the same name. One is tailored for use in the GitHub Actions environment, while the other is for the Azure resource environment.

For local development purposes, you can establish an environment variable called **"APPSETTING_CONNECTION_STRING"** on your computer. This serves as an alternative to directly interacting with the appsettings.json file, with the stipulation that its contents are kept private and not uploaded to any public repositories.

### Deploying using this [repository](https://github.com/zetTtai/CleanArchitecturePSQL)

To test this template, modify the `.scripts\environments.json` and `src\Web\appsettings.json` files, as they contain placeholder connection strings that will prevent successful deployment.

After making the necessary adjustments to these files, proceed by following the instructions in the Setup Script for Supabase to deploy the project.





