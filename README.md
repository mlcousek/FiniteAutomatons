# Finite Automata Simulator

## Overview

Finite Automatons is a web application for creating, visualizing and executing finite automata (DFA, NFA, and Epsilon-NFA). It supports interactive creation and editing of small automata, step-by-step execution, conversion between automaton types (including NFA/Epsilon-NFA → DFA), and minimization of DFAs.

The project includes a Razor Pages + MVC application, unit tests and integration tests.

## Features

- Support for DFA, NFA and Epsilon-NFA.
- Convert NFA / Epsilon-NFA to DFA and minimize DFAs.
- Step-by-step execution and full execution of automata on provided inputs.
- Create automata interactively using the web UI, import/export from forms/files.
- Convert regular expressions to automatons (development/testing helper service).
- Persisted data via Entity Framework Core (SQL Server) and ASP.NET Core Identity for authentication.
- Observability: OpenTelemetry tracing/console exporter in development; file-based traces, logs and audits in non-development environments.
- Comprehensive unit and integration test coverage.

## Technologies

- .NET 9 / C# 13
- ASP.NET Core (Razor Pages + MVC controllers)
- Entity Framework Core (SQL Server)
- ASP.NET Core Identity
- OpenTelemetry for tracing and logging
- xUnit + Shouldly for tests

## Repository layout

- `FiniteAutomatons/` - main web application (Razor Pages + MVC controllers)
- `FiniteAutomatons.Core/` - core domain models and shared types
- `FiniteAutomatons.Services/` - application services (execution, conversion, minimization, validation, etc.)
- `FiniteAutomatons.Data/` - EF Core DbContext and DB models
- `FiniteAutomatons.UnitTests/` - unit tests
- `FiniteAutomatons.IntegrationTests/` - integration tests

## Prerequisites

- .NET 9 SDK
- SQL Server instance (LocalDB or full SQL Server) for running the app against a real database

## Configuration

- Connection string is read from the `DatabaseSettings` configuration section. By default the project reads configuration from `appsettings.json` and will optionally pick up a `dev.json` from the content root if present (useful for local overrides).
- When running in development, the app uses in-memory observability collectors and an in-memory audit service. In non-development environments the app writes traces, logs and audits to `./observability/` files under the app content root.

## Running locally

1. From the repository root, run the web project:

   - Using `dotnet`: `cd FiniteAutomatons` then `dotnet run`

2. If you need a database, ensure your `DatabaseSettings` connection string points to a reachable SQL Server. If the database schema is not present, create migrations and update the database:

   - `dotnet ef migrations add InitialCreate -p FiniteAutomatons.Data -s FiniteAutomatons`
   - `dotnet ef database update -p FiniteAutomatons.Data -s FiniteAutomatons`

   (Adjust migration/project names as needed.)

3. Open a browser and navigate to `https://localhost:5001` (or the port reported by `dotnet run`).

## Development / Debugging endpoints (development environment only)

- `GET /_tests/audit-correlation` — simple endpoint used by tests to verify audit/tracing behavior.
- `POST /_tests/build-from-regex` — accepts a raw regular expression in the request body and returns a JSON description of the constructed epsilon-NFA (useful for development and tests).

## Important controller endpoints used by tests / UI

- `POST /Automaton/ConvertToDFA` — converts the posted automaton model to a DFA and returns the UI/html.
- `POST /Automaton/ExecuteAll` — executes the automaton for the provided input and returns the UI/html with execution results.

## Tests

- Run all tests from the solution root: `dotnet test`
- Integration tests require development environment settings (they rely on the test server configuration).

## Contributing

Contributions are welcome. Please follow standard GitHub fork/branch/PR workflow. Add tests for new behaviors and ensure existing tests pass.

## License

This project is licensed under the MIT License.
