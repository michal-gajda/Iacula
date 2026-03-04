# Iacula

```powershell
git init
dotnet new gitignore
dotnet new sln --name Iacula
```

```powershell
dotnet new classlib --framework net10.0 --output src/Domain --name Iacula.Domain
dotnet sln add src/Domain
```

```powershell
dotnet new classlib --framework net10.0 --output src/Application --name Iacula.Application
dotnet sln add src/Application
dotnet add src/Application reference src/Domain
dotnet add src/Application package AutoMapper
dotnet add src/Application package FluentValidation.DependencyInjectionExtensions
dotnet add src/Application package MediatR
```

```powershell
dotnet new classlib --framework net10.0 --output src/Shared --name Iacula.Shared
dotnet sln add src/Shared
```

```powershell
dotnet new classlib --framework net10.0 --output src/Infrastructure --name Iacula.Infrastructure
dotnet sln add src/Infrastructure
dotnet add src/Infrastructure reference src/Application
dotnet add src/Infrastructure reference src/Shared
dotnet add src/Infrastructure package Microsoft.Extensions.Configuration.Binder
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Tools
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.InMemory
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Infrastructure package MassTransit.RabbitMQ --version 8.5.8
```

```powershell
dotnet new mstest --framework net10.0 --output tests/Application.FunctionalTests --name Iacula.Application.FunctionalTests
dotnet sln add tests/Application.FunctionalTests
dotnet add tests/Application.FunctionalTests reference src/Infrastructure
dotnet add tests/Application.FunctionalTests package Microsoft.Extensions.DependencyInjection
dotnet add tests/Application.FunctionalTests package Microsoft.Extensions.Logging.Debug
dotnet add tests/Application.FunctionalTests package Shouldly
```

```powershell
dotnet new webapi --framework net10.0 --no-https --use-program-main --use-controllers --output src/WebApi --name Iacula.WebApi
dotnet sln add src/WebApi
dotnet add src/WebApi reference src/Infrastructure
dotnet add src/WebApi package Microsoft.AspNetCore.Components.WebAssembly.Server
dotnet add src/WebApi package Microsoft.AspNetCore.OpenApi
dotnet add src/WebApi package Swashbuckle.AspNetCore
dotnet add src/WebApi package OpenTelemetry.Extensions.Hosting
dotnet add src/WebApi package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add src/WebApi package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/WebApi package OpenTelemetry.Instrumentation.Http
dotnet add src/WebApi package OpenTelemetry.Instrumentation.Runtime
dotnet add src/WebApi package OpenTelemetry.Instrumentation.Process --version 1.15.0-beta.1
```

```powershell
dotnet new blazorwasm --framework net10.0 --no-https --use-program-main --output src/WebUI --name Iacula.WebUI
dotnet sln add src/WebUI
dotnet add src/WebApi reference src/WebUI
```
