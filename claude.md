# Claude AI Coding Instructions for FireChief

This file provides coding guidelines for AI assistants working on the FireChief project.

---
**Target: C# 14, .NET 10**
**applyTo: `**/*.cs`**
---

## Core Principles

- **Immutability First**: Default to immutable types. Use `record` over `class`.
- **Clarity and Intent**: Code should be self-documenting. Use patterns that clearly express business logic.
- **Single Responsibility**: One type per file. Each class/record should have a single, clear purpose.
- **Modern C#**: Leverage latest language features (primary constructors, collection expressions, pattern matching, required properties).

## Domain Modeling

### Records with Static Factories

- **Simple Records**: Use primary constructors for records without validation.
  ```csharp
  public record TeamMember(
      string Id, 
      string Name, 
      DateTime? LastChiefDate
  );
  ```

- **Records with Validation**: Create a companion static factory class when validation is required.
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
          if (!value.Contains('@'))
              return Result.Failure<Email>("Invalid email format");
          return Result.Success(new Email(value));
      }
  }
  ```

- **Immutable Collections**: Use `ImmutableList<T>`, `ImmutableArray<T>`, or collection expressions `[]`.
- **Extension Methods**: Add behavior to records via extension methods in separate static classes.

### Discriminated Unions

- Implement as abstract record with concrete record subtypes:
  ```csharp
  public abstract record NotificationResult;
  public record NotificationSent(string MessageId) : NotificationResult;
  public record NotificationFailed(string Error) : NotificationResult;
  public record NotificationSkipped(string Reason) : NotificationResult;
  ```

## Modern C# Features

### Primary Constructors
Use for dependency injection and simple initialization:
```csharp
public sealed class AssignmentService(
    NotionService notion,
    SlackService slack,
    ILogger<AssignmentService> logger)
{
    public async Task ProcessAsync()
    {
        logger.LogInformation("Processing");
        // notion, slack are available as fields
    }
}
```

### Collection Expressions
```csharp
// Old
var list = new List<string> { "a", "b" };
var dict = new Dictionary<string, int> { ["key"] = 1 };

// New
List<string> list = ["a", "b"];
Dictionary<string, int> dict = new() { ["key"] = 1 };
```

### Pattern Matching
```csharp
// Switch expressions
var message = status switch
{
    "active" => "Running",
    "paused" => "Waiting",
    _ => "Unknown"
};

// Property patterns
if (config is { NotionToken.Length: > 0, TeamDatabaseId: not null })
{
    // Valid configuration
}

// List patterns
if (candidates is [var first, var second, ..])
{
    // At least 2 items
}
```

### Required Properties
```csharp
public sealed record AppConfig
{
    public required string NotionToken { get; init; }
    public required string TeamDatabaseId { get; init; }
}
```

## Error Handling

### Use Result Types
- **Never throw exceptions for business logic failures**
- Use `Result<T>` from `CSharpFunctionalExtensions` package
- Pattern match on results:

```csharp
public async Task<Result<Assignment>> CreateAssignmentAsync(string memberId)
{
    var member = await GetMemberAsync(memberId);
    if (member is null)
        return Result.Failure<Assignment>("Member not found");
    
    if (!member.IsActive)
        return Result.Failure<Assignment>("Member is not active");
    
    return Result.Success(new Assignment(member.Id));
}

// Usage
var result = await CreateAssignmentAsync("123");
return result.Match(
    onSuccess: assignment => Ok(assignment),
    onFailure: error => BadRequest(error)
);
```

## Dependency Injection Patterns

### Options Pattern
```csharp
// Configure
builder.Services.Configure<AppConfig>(
    builder.Configuration.GetSection("FireChief")
);

// Consume
public class MyService(IOptions<AppConfig> config)
{
    private readonly AppConfig _config = config.Value;
}
```

### Refit Client Registration
```csharp
builder.Services.AddRefitClient<ISlackClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IOptions<AppConfig>>().Value;
        client.BaseAddress = new Uri(config.SlackWebhookUrl);
    });
```

## Code Organization

- **One type per file**: `TeamMember.cs` contains only `TeamMember` record
- **Factory in same file**: `EmailFactory` goes in `Email.cs`
- **Service naming**: `*Service` for business logic, `*Repository` for data access
- **Clear file structure**:
  ```
  /Models          - Domain records
  /Services        - Business logic
  /Configuration   - Config classes
  Program.cs       - Entry point with DI setup
  ```

## Testing Patterns

- Use descriptive test names: `Should_select_volunteer_when_available()`
- Arrange-Act-Assert structure
- Name system under test as `sut`

```csharp
[Fact]
public async Task Should_select_volunteer_as_chief()
{
    // Arrange
    var members = new List<TeamMember>
    {
        new("1", "Alice", "", null, true, false),
        new("2", "Bob", "", null, true, true) // volunteer
    };
    var sut = new AssignmentService(notion, slack, logger);
    
    // Act
    var result = await sut.SelectChiefAsync(members);
    
    // Assert
    result.Value.Name.Should().Be("Bob");
}
```

## Anti-Patterns to Avoid

❌ Mutable classes with setters
❌ Throwing exceptions for business logic
❌ Direct constructor calls when validation is needed
❌ Multiple responsibilities in one class
❌ Nullable reference types without proper handling
❌ Legacy collection initialization

## Preferred Patterns

✅ Immutable records with primary constructors
✅ Result types for operations that can fail
✅ Pattern matching for flow control
✅ Options pattern for configuration
✅ Extension methods for behavior
✅ Collection expressions for initialization
✅ Primary constructors for DI
