# Finite Automata Simulator

## Overview

Finite Automatons is a comprehensive web application for creating, visualizing, executing, and sharing finite automata (DFA, NFA, and Epsilon-NFA). The application provides an interactive visual editor, step-by-step execution, automaton conversion and minimization, collaborative features with role-based access control, and extensive import/export capabilities.

The project is built as a hybrid ASP.NET Core MVC and Razor Pages application with comprehensive unit and integration test coverage.

## Key Features

### Automaton Operations
- **Multiple Automaton Types**: Full support for DFA, NFA, and Epsilon-NFA
- **Type Conversion**: Convert NFA/Epsilon-NFA → DFA with detailed transformation tracking
- **DFA Minimization**: Minimize DFAs using state equivalence algorithms
- **Step-by-step Execution**: Interactive execution with visual state tracking
- **Full Execution**: Batch processing with acceptance/rejection results
- **Regex to Automaton**: Convert regular expressions to epsilon-NFA (development/testing helper)

### Interactive Editor
- **Visual Canvas**: Interactive drag-and-drop state and transition editor
- **Real-time Validation**: Instant feedback on automaton validity
- **Automaton Analysis**: Detect determinism, reachability, and completeness
- **Automaton Presets**: Quick-start templates for common automaton patterns
- **Random Generation**: Generate random automatons with customizable parameters

### Persistence & Organization
- **Saved Automatons**: Save and manage personal automatons with groups
- **Import/Export**: Support for JSON, custom text format, and file-based operations
- **Group Management**: Organize automatons into hierarchical groups
- **Execution History**: Track and review execution states

### Collaboration Features
- **Shared Automaton Groups**: Create collaborative workspaces
- **Role-Based Access Control**: Owner, Admin, Editor, Contributor, and Viewer roles
- **Email Invitations**: Invite team members via email
- **Shareable Links**: Generate time-limited invite links with configurable roles
- **Real-time Collaboration**: Multiple users can work on shared automatons
- **Audit Trail**: Track all changes and member activities

### Input Generation
- **Pattern-Based Generation**: Generate test inputs matching specific patterns
- **Accept/Reject Samples**: Generate inputs guaranteed to accept or reject
- **Bulk Testing**: Test automatons against multiple inputs

### Observability & Monitoring
- **OpenTelemetry Integration**: Distributed tracing for all operations
- **Audit Logging**: Comprehensive audit trail for security-sensitive operations
- **File-Based Logging**: Structured logs written to `./observability/` in production
- **Activity Tracking**: Detailed operation tracking with correlation IDs

## Technology Stack

- **.NET 10** / **C# 14**
- **ASP.NET Core** (Hybrid MVC + Razor Pages)
- **Entity Framework Core** with SQL Server
- **ASP.NET Core Identity** for authentication and authorization
- **OpenTelemetry** for tracing, logging, and metrics
- **Bootstrap 5** for responsive UI
- **xUnit + Shouldly** for testing
- **Docker** support with docker-compose

## Project Structure

```
FiniteAutomatons/
├── FiniteAutomatons/              # Main web application
│   ├── Controllers/               # MVC controllers
│   │   ├── AutomatonCreationController.cs
│   │   ├── AutomatonExecutionController.cs
│   │   ├── AutomatonConversionController.cs
│   │   ├── AutomatonGenerationController.cs
│   │   ├── SavedAutomatonController.cs
│   │   ├── SharedAutomatonController.cs
│   │   ├── ImportExportController.cs
│   │   └── ...
│   ├── Views/                     # Razor views
│   │   ├── Home/
│   │   ├── SavedAutomaton/
│   │   ├── SharedAutomaton/
│   │   ├── AutomatonCreation/
│   │   └── Shared/
│   ├── wwwroot/                   # Static files
│   │   ├── js/                    # Client-side JavaScript
│   │   └── css/
│   ├── Filters/                   # Action filters
│   ├── Middleware/                # Custom middleware
│   └── Program.cs                 # Application entry point
│
├── FiniteAutomatons.Core/         # Domain models and core logic
│   ├── Models/
│   │   ├── Domain/                # Domain entities (State, Transition, IAutomaton)
│   │   ├── Database/              # Database entities
│   │   ├── ViewModel/             # View models
│   │   ├── DTOs/                  # Data transfer objects
│   │   └── Serialization/         # Serialization logic
│   ├── Interfaces/                # Core interfaces
│   └── Utilities/                 # Helper classes
│
├── FiniteAutomatons.Services/     # Application services
│   ├── Services/                  # Business logic services
│   │   ├── AutomatonExecutionService.cs
│   │   ├── AutomatonConversionService.cs
│   │   ├── AutomatonMinimizationService.cs
│   │   ├── AutomatonValidationService.cs
│   │   ├── AutomatonGeneratorService.cs
│   │   ├── SavedAutomatonService.cs
│   │   ├── SharedAutomatonService.cs
│   │   ├── SharedAutomatonSharingService.cs
│   │   ├── AutomatonFileService.cs
│   │   ├── InputGenerationService.cs
│   │   └── ...
│   ├── Interfaces/                # Service interfaces
│   └── Observability/             # Audit decorators and logging
│
├── FiniteAutomatons.Data/         # Data access layer
│   ├── ApplicationDbContext.cs    # EF Core DbContext
│   └── Migrations/                # Database migrations
│
├── FiniteAutomatons.UnitTests/    # Unit tests
│   ├── Controllers/               # Controller tests
│   ├── Services/                  # Service tests
│   └── Core/                      # Core logic tests
│
└── FiniteAutomatons.IntegrationTests/  # Integration tests
    ├── AutomatonApiTests/
    ├── AutomatonOperations/
    ├── AutomationExecution/
    ├── AutomationFETests/
    └── RegexApiTests/
```


## Prerequisites

- **.NET 10 SDK** or later
- **SQL Server** instance (LocalDB, SQL Server Express, or full SQL Server)
- **Node.js** (optional, for frontend tooling)
- **Docker** (optional, for containerized deployment)

## Configuration

### Database Configuration
The connection string is read from the `DatabaseSettings` section in `appsettings.json`. You can override settings locally using `dev.json` in the content root (this file is gitignored).

Example `dev.json`:
```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=FiniteAutomatonsDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### Environment-Specific Behavior
- **Development**: Uses in-memory audit service and console-based OpenTelemetry exporters
- **Production/Staging**: Writes traces, logs, and audits to `./observability/` directory

### Email Configuration (for invitations)
Configure SMTP settings in `appsettings.json` for email invitations:
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.example.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "FromAddress": "noreply@example.com"
  }
}
```

## Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/mlcousek/FiniteAutomatons.git
cd FiniteAutomatons
```

### 2. Set Up the Database
```bash
cd FiniteAutomatons
dotnet ef database update -p ../FiniteAutomatons.Data -s .
```

This will create the database schema and apply all migrations.

### 3. Run the Application
```bash
dotnet run
```

Or use Visual Studio / VS Code to run the project.

### 4. Access the Application
Open your browser and navigate to:
- `https://localhost:5001` (HTTPS)
- `http://localhost:5000` (HTTP)

### Using Docker
```bash
docker-compose up
```

This will start the application and SQL Server in containers.

## Key API Endpoints

### Automaton Operations
- `POST /AutomatonCreation/Create` - Create a new automaton
- `POST /AutomatonExecution/ExecuteStep` - Execute one step
- `POST /AutomatonExecution/ExecuteAll` - Execute to completion
- `POST /AutomatonExecution/Reset` - Reset execution state
- `POST /AutomatonConversion/ConvertToDFA` - Convert NFA to DFA
- `POST /AutomatonConversion/MinimizeDFA` - Minimize DFA
- `POST /AutomatonGeneration/Generate` - Generate random automaton

### Saved Automatons
- `GET /SavedAutomaton/SavedAutomatons` - List saved automatons
- `POST /SavedAutomaton/Save` - Save an automaton
- `POST /SavedAutomaton/Load` - Load a saved automaton
- `POST /SavedAutomaton/Delete` - Delete a saved automaton
- `POST /SavedAutomaton/CreateGroup` - Create a group
- `POST /SavedAutomaton/ExportGroup` - Export group as JSON

### Shared Automatons (Collaboration)
- `GET /SharedAutomaton/Index` - List shared groups
- `POST /SharedAutomaton/CreateGroup` - Create shared group
- `GET /SharedAutomaton/ManageMembers` - Manage group members
- `POST /SharedAutomaton/InviteByEmail` - Invite member via email
- `POST /SharedAutomaton/GenerateInviteLink` - Generate shareable link
- `POST /SharedAutomaton/UpdateMemberRole` - Change member role
- `POST /SharedAutomaton/RemoveMember` - Remove member
- `GET /SharedAutomaton/AcceptInvitation` - Accept email invitation
- `GET /SharedAutomaton/JoinViaLink` - Join via shareable link

### Import/Export
- `POST /ImportExport/ImportJson` - Import automaton from JSON
- `POST /ImportExport/ImportText` - Import from custom text format
- `GET /ImportExport/ExportJson` - Export as JSON
- `GET /ImportExport/ExportText` - Export as text
- `POST /ImportExport/ImportFile` - Upload and import file

### Input Generation
- `POST /InputGeneration/GenerateAccepting` - Generate accepting inputs
- `POST /InputGeneration/GenerateRejecting` - Generate rejecting inputs
- `POST /InputGeneration/GenerateRandom` - Generate random inputs

### Development/Testing Endpoints (Development Only)
- `GET /_tests/audit-correlation` - Verify audit/tracing behavior
- `POST /_tests/build-from-regex` - Convert regex to epsilon-NFA

## Role-Based Access Control

Shared automaton groups support the following roles:

| Role | View | Execute | Add | Edit | Manage Members | Delete |
|------|------|---------|-----|------|----------------|--------|
| **Owner** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Admin** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Editor** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Contributor** | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Viewer** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |

## Testing

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test --filter "FullyQualifiedName~FiniteAutomatons.UnitTests"
```

### Run Integration Tests Only
```bash
dotnet test --filter "FullyQualifiedName~FiniteAutomatons.IntegrationTests"
```

### Test Coverage
Generate code coverage report:
```bash
dotnet tool install -g dotnet-coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

### Test Categories
- **Unit Tests**: Controllers, Services, Core logic, Serialization
- **Integration Tests**: End-to-end API tests, Database tests, Frontend tests
- **Test Coverage**: Comprehensive coverage of business logic and critical paths

## Database Migrations

### Create a New Migration
```bash
dotnet ef migrations add MigrationName -p FiniteAutomatons.Data -s FiniteAutomatons
```

### Apply Migrations
```bash
dotnet ef database update -p FiniteAutomatons.Data -s FiniteAutomatons
```

### Rollback Migration
```bash
dotnet ef database update PreviousMigrationName -p FiniteAutomatons.Data -s FiniteAutomatons
```

## Observability

### Development Environment
- Traces and logs output to console
- In-memory audit service
- OpenTelemetry console exporters

### Production Environment
- Structured logs written to `./observability/logs/`
- Audit trail written to `./observability/audit/`
- Distributed traces written to `./observability/traces/`
- Correlation IDs track operations across services

### Viewing Logs
```bash
# View recent logs
cat ./observability/logs/app-YYYYMMDD.log

# View audit trail
cat ./observability/audit/audit-YYYYMMDD.log

# View traces
cat ./observability/traces/trace-YYYYMMDD.json
```

## Architecture Highlights

### Service Layer
All business logic is encapsulated in service classes with clear interfaces, enabling:
- Easy unit testing with mocks
- Decorator pattern for cross-cutting concerns (audit, logging)
- Separation of concerns

### Observability Decorators
Key services are wrapped with audit decorators:
- `AutomatonExecutionServiceAuditorDecorator`
- `AutomatonConversionServiceAuditorDecorator`
- `AutomatonGeneratorServiceAuditorDecorator`

### Domain Models
Core automaton logic (`DFA`, `NFA`, `EpsilonNFA`) is implemented as pure domain models with no framework dependencies.

### Client-Side Architecture
- Modular ES6 modules for canvas, input handling, and controls
- Progressive enhancement with server-side rendering fallback
- Bootstrap 5 for responsive design

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork & Branch**: Create a feature branch from `master`
2. **Follow Conventions**: Match existing code style and patterns
3. **Add Tests**: Include unit tests for new functionality
4. **Update Docs**: Update README if adding new features
5. **Pull Request**: Submit PR with clear description

### Code Style
- Follow the conventions in `.github/copilot-instructions.md`
- Use C# 14 features where appropriate
- Follow SOLID principles
- Prefer immutability and records for DTOs
- Add XML documentation for public APIs

## License

This project is licensed under the MIT License.

## Acknowledgments

Built with modern .NET best practices, leveraging:
- ASP.NET Core MVC & Razor Pages
- Entity Framework Core
- OpenTelemetry
- Bootstrap 5
- xUnit & Shouldly
