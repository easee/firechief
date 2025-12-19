using CSharpFunctionalExtensions;
using FireChief;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refit;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("FireChief"));
builder.Services.AddRefitClient<ISlackClient>()
	.ConfigureHttpClient(client =>
	{
		client.BaseAddress = new Uri("https://slack.com/api/");
	});

// Services
builder.Services.AddSingleton<NotionService>();
builder.Services.AddSingleton<SlackService>();
builder.Services.AddSingleton<AssignmentService>();

// Build host
using IHost host = builder.Build();

// Execute command based on arguments
string mode = args.Length > 0 ? args[0].ToLower() : "help";
AssignmentService assignmentService = host.Services.GetRequiredService<AssignmentService>();

int result = await (mode switch
{
	"assign" or "remind-monday" => ExecuteAssignmentAsync(assignmentService),
	"remind-friday" => ExecuteFridayReminderAsync(assignmentService),
	_ => ShowUsage()
});

Environment.Exit(result);
return;

static async Task<int> ExecuteAssignmentAsync(AssignmentService service)
{
	Result<AssignmentOutcome> assignment = await service.RunAssignmentAsync();

	return assignment switch
	{
		{ IsFailure: true, Error: var error } =>
			HandleFailure($"❌ Assignment failed: {error}"),

		{ IsSuccess: true, Value: AssignmentCreated created } => HandleSuccess(
			created.Assignment.Chief.Id == created.Assignment.Backup.Id
				? $"✅ Assignment created: {created.Assignment.Chief.Name} (Chief, no backup available)"
				: $"✅ Assignment created: {created.Assignment.Chief.Name} (Chief), {created.Assignment.Backup.Name} (Backup)"
		),

		{ IsSuccess: true, Value: AssignmentAlreadyExists exists } =>
			HandleSuccess($"ℹ️ Assignment already exists for {exists.WeekStart:yyyy-MM-dd}"),

		{ IsSuccess: true, Value: InsufficientCandidates insufficient } =>
			HandleFailure($"❌ Need at least 2 candidates, found {insufficient.AvailableCount}"),

		_ => HandleFailure("❌ Unknown outcome")
	};
}

static async Task<int> ExecuteFridayReminderAsync(AssignmentService service)
{
	Result reminder = await service.RunFridayReminderAsync();

	return reminder switch
	{
		{ IsSuccess: true } => HandleSuccess("✅ Friday reminder sent"),
		{ IsFailure: true, Error: var error } => HandleFailure($"❌ Reminder failed: {error}"),

		_ => HandleFailure("❌ Unknown error")
	};
}

static Task<int> ShowUsage()
{
	Console.WriteLine(
		"""
		FireChief Bot - Automated Fire Chief Assignment

		Usage: dotnet run -- [command]

		Commands:
		  assign          Run weekly assignment workflow
		  remind-monday   Alias for 'assign'
		  remind-friday   Send Friday handover reminder
		  help            Show this help message

		Examples:
		  dotnet run -- assign
		  dotnet run -- remind-friday
		""");
	return Task.FromResult(0);
}

static int HandleSuccess(string message)
{
	Console.WriteLine(message);
	return 0;
}

static int HandleFailure(string message)
{
	Console.Error.WriteLine(message);
	return 1;
}
