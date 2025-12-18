# AI Coding Instructions for FireChief

This file guides AI assistants on coding standards for this repository. Compatible with Claude, GitHub Copilot, and other AI coding tools.

---
**Language: C# 12+ (.NET 10)**
**applyTo: `**/*.cs`**
---

## Core Principles

- **Immutability First**: Default to immutable types. Use `record` over `class`.
- **Clarity and Intent**: Code should be self-documenting. Use patterns that clearly express business logic.
- **Single Responsibility**: One type per file. Each component should have a single, clear purpose.
- **Modern C#**: Leverage latest features (primary constructors, collection expressions, pattern matching, required properties).

## Domain Modeling

### Records
Prefer immutable records with primary constructors:

```csharp
// Simple record - no validation needed
public record TeamMember(
    string Id,
    string Name,
    string SlackId,
    DateTime? LastChiefDate,
    bool IsActive,
    bool IsVolunteer
);

// Configuration record with required properties
public sealed record AppConfig
{
    public required string NotionToken { get; init; }
    public required string TeamDatabaseId { get; init; }
    public required string RosterDatabaseId { get; init; }
}
```

### Static Factories
When validation is required, use static factory classes:

```csharp
public record Email
{
    private Email(string value) => Value = value;
    public string Value { get; }
}

public static class EmailFactory
{
    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Email>("Email cannot be empty");
        return Result.Success(new Email(value));
    }
}
```

### Discriminated Unions
For complex domain concepts with mutually exclusive states:

```csharp
public abstract record Result;
public record Success(string Message) : Result;
public record Failure(string Error) : Result;
public record Pending(DateTime Until) : Result;
```

## Modern C# Features

### Primary Constructors
For dependency injection and initialization:

```csharp
public sealed class AssignmentService(
    NotionService notion,
    SlackService slack,
    ILogger<AssignmentService> logger)
{
    // Dependencies are automatically available as fields
    public async Task ProcessAsync()
    {
        logger.LogInformation("Processing assignment");
        await notion.GetMembersAsync();
    }
}
```

### Collection Expressions
```csharp
// Lists
List<string> names = ["Alice", "Bob", "Charlie"];

// Dictionaries with collection initializers
Dictionary<string, PropertyValue> props = new()
{
    ["Week"] = new DatePropertyValue { Date = new Date { Start = weekStart } },
    ["Status"] = new SelectPropertyValue { Select = new SelectOption { Name = "Planned" } }
};

// Arrays
PropertyValue[] relations = [new ObjectId { Id = chiefId }];
```

### Pattern Matching
```csharp
// Switch expressions
var message = mode switch
{
    "assign" => "Running assignment",
    "remind-friday" => "Sending reminder",
    _ => "Unknown mode"
};

// Property patterns
if (config is { NotionToken.Length: > 0 })
{
    // Valid token
}

// List patterns
if (candidates is [var chief, var backup, ..])
{
    // At least 2 candidates
}

// Null-conditional with pattern
if (props.GetValueOrDefault("Week") is DatePropertyValue { Date.Start: not null } d)
{
    var date = d.Date.Start.Value;
}
```

### Required Properties
Enforce initialization of critical configuration:

```csharp
public sealed record AppConfig
{
    public required string NotionToken { get; init; }
    public required string TeamDatabaseId { get; init; }
}

// Compiler error if not initialized
var config = new AppConfig(); // ❌ Error
var config = new AppConfig { NotionToken = "...", TeamDatabaseId = "..." }; // ✅
```

## Application Structure

### Dependency Injection

**Options Pattern for Configuration:**
```csharp
// Registration
builder.Services.Configure<AppConfig>(
    builder.Configuration.GetSection("FireChief")
);

// Consumption
public class MyService(IOptions<AppConfig> config)
{
    private readonly AppConfig _config = config.Value;
}
```

**Refit HTTP Clients:**
```csharp
builder.Services.AddRefitClient<ISlackClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IOptions<AppConfig>>().Value;
        client.BaseAddress = new Uri(config.WebhookUrl);
    });
```

### Program.cs with Top-Level Statements
```csharp
using FireChief;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("FireChief"));
builder.Services.AddSingleton<NotionService>();
builder.Services.AddSingleton<AssignmentService>();

using var host = builder.Build();

var service = host.Services.GetRequiredService<AssignmentService>();
await service.RunAsync();
```

## Error Handling

### Result Types (Not Exceptions)
Use `Result<T>` from `CSharpFunctionalExtensions` for predictable failures:

```csharp
public async Task<Result<Assignment>> CreateAssignmentAsync(string memberId)
{
    var member = await _repository.GetByIdAsync(memberId);
    if (member is null)
        return Result.Failure<Assignment>("Member not found");
    
    if (!member.IsActive)
        return Result.Failure<Assignment>("Member is inactive");
    
    return Result.Success(new Assignment(member.Id));
}

// Pattern match results
var result = await CreateAssignmentAsync("123");
return result.Match(
    onSuccess: assignment => Ok(assignment),
    onFailure: error => BadRequest(error)
);
```

### When to Throw Exceptions
Only for unexpected, unrecoverable errors:
- Out of memory
- Configuration errors at startup
- External system failures (DB connection lost)

## Testing

### Test Naming
Use descriptive sentences with underscores:

```csharp
[Fact]
public async Task Should_select_volunteer_as_chief_when_available()
{
    // Arrange
    var sut = new AssignmentService(notion, slack, logger);
    
    // Act
    var result = await sut.SelectChiefAsync(members);
    
    // Assert
    result.Should().BeSuccess();
}
```

### Structure
- **Arrange-Act-Assert**: Clear separation
- **System Under Test**: Name it `sut`
- **One logical assertion per test**

## Code Organization

```
/Models/              - Domain records
  TeamMember.cs
  RosterEntry.cs
  AppConfig.cs
  
/Services/            - Business logic
  NotionService.cs
  SlackService.cs
  AssignmentService.cs
  
/Clients/             - External API interfaces
  ISlackClient.cs
  
Program.cs            - Entry point with DI
appsettings.json      - Configuration
```

**One Type Per File**: `TeamMember.cs` contains only the `TeamMember` record (and optionally its factory).

## Naming Conventions

- **Records**: PascalCase nouns (`TeamMember`, `RosterEntry`)
- **Services**: `*Service` suffix (`NotionService`, `AssignmentService`)
- **Repositories**: `*Repository` suffix (if needed)
- **Factories**: `*Factory` suffix (`EmailFactory`)
- **Interfaces**: `I*` prefix (`ISlackClient`)

## Git Commits

Write commit messages like a short email:

```
Subject line (≤50 chars, present tense, capitalized)

Body paragraph explaining why this change was needed. Reference
design docs if applicable. Wrap at ~72 characters.

Fixes #123
```

**Do NOT add AI attribution** (e.g., "Generated with Claude")

## Anti-Patterns

❌ Mutable classes with setters  
❌ Throwing exceptions for business logic  
❌ Multiple responsibilities in one class  
❌ Direct `new()` calls when validation is required  
❌ Legacy `new List<T>()` instead of collection expressions  
❌ `null` returns instead of `Result<T>` for failures  

## Preferred Patterns

✅ Immutable records with primary constructors  
✅ `Result<T>` for operations that can fail  
✅ Pattern matching for flow control  
✅ Options pattern for configuration  
✅ Collection expressions `[]`  
✅ Primary constructors for DI  
✅ Extension methods for behavior  
✅ `required` for mandatory properties  

---

*For more detailed examples, see `claude.md`*
