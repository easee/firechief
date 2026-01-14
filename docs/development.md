# Local Development Guide

This guide walks you through setting up FireChief for local development and testing with your own Notion databases and Slack channels.

## Prerequisites

- .NET 10 SDK
- Notion account
- Slack workspace with admin permissions

## 1. Create Notion Databases

### Team Database

Create a Notion database with the following properties:

| Property Name | Type | Description |
|--------------|------|-------------|
| **Name** | Title | Team member's name |
| **Slack ID** | Text | Slack user ID (format: `U01234ABCDE`) |
| **Active** | Checkbox | Whether member is eligible for rotation |
| **Volunteer** | Checkbox | Set by member to volunteer for next week |
| **Recent Chief Date** | Date | Last time this person was Fire Chief |
| **Chief Count** | Number | Total number of times served as Fire Chief |

**Sample entry:**
- Name: `Alice Johnson`
- Slack ID: `U01234ABCDE`
- Active: ✅
- Volunteer: ☐
- Recent Chief Date: `2025-12-09`
- Chief Count: `5`

**Getting your Team Database ID:**
1. Open the database as a full page in Notion
2. Copy the URL: `https://notion.so/workspace/abc123def456?v=...`
3. The database ID is the part between the last `/` and `?`: `abc123def456`

### Weekly Roster Database

Create a second Notion database with these properties:

| Property Name | Type | Description |
|--------------|------|-------------|
| **Week** | Date | Start date of the week (Monday) |
| **Chief** | Relation | Relation to Team Database |
| **Backup** | Relation | Relation to Team Database |
| **Status** | Select | Options: `Planned`, `Active`, `Completed` |
| **Public Message TS** | Text | Slack message timestamp (internal use) |
| **Internal Message TS** | Text | Slack message timestamp (internal use) |

**Setting up the Relation properties:**
1. Create a new `Relation` property
2. Select your Team Database
3. Choose "Show on Team Database" if you want bi-directional linking
4. Repeat for both `Chief` and `Backup` properties

**Getting your Roster Database ID:**
Same process as Team Database - extract from the URL.

## 2. Create Notion Integration

1. Go to [Notion Integrations](https://www.notion.so/my-integrations)
2. Click **"+ New integration"**
3. Name it `FireChief Dev` (or similar)
4. Select your workspace
5. Set capabilities:
   - ✅ Read content
   - ✅ Update content
   - ✅ Insert content
6. Click **Submit**
7. Copy the **Internal Integration Token** (starts with `secret_`)

**Connect databases to integration:**
1. Open each database in Notion
2. Click `•••` (more menu) in the top right
3. Select **"Add connections"**
4. Find and select your `FireChief Dev` integration
5. Repeat for both databases

## 3. Create Slack Bot

### Create Slack App

1. Go to [Slack API Apps](https://api.slack.com/apps)
2. Click **"Create New App"** → **"From scratch"**
3. Name: `FireChief Dev`
4. Select your workspace
5. Or, alternatively, just add the existing FireChief Bot to a channel of choice

### Configure Bot Permissions

Go to **OAuth & Permissions** and add these Bot Token Scopes:

- `chat:write` - Post messages
- `chat:write.public` - Post in channels bot isn't a member of
- `channels:write` - Set channel topics in public channels
- `groups:write` - Set channel topics in private channels
- `users:read` - Read user information for mentions

### Install Bot to Workspace

1. Go to **OAuth & Permissions**
2. Click **"Install to Workspace"**
3. Review permissions and click **"Allow"**
4. Copy the **Bot User OAuth Token** (starts with `xoxb-`)

### Create Test Channels

Create two Slack channels for testing:

1. **Internal Channel**: `#firechief-dev-internal` (can be private)
   - For detailed notifications and reminders

2. **Public Channel**: `#firechief-dev-public` (can be private for testing)
   - For public announcements

**Getting Channel IDs:**
1. Right-click on the channel name
2. Select **"View channel details"**
3. Scroll to the bottom - the channel ID is shown (format: `C01234ABCDE`)

**Add bot to channels:**
- For public channels: Mention `@FireChief Dev` in the channel
- For private channels: Go to channel details → Integrations → Add apps

## 4. Configure Local Environment

### Set up User Secrets

.NET User Secrets keeps sensitive data out of your source code. You must set the `DOTNET_ENVIRONMENT` variable for user secrets to work:

```bash
# Set environment to Development
export DOTNET_ENVIRONMENT=Development

# Navigate to project directory
cd src/

# Initialize user secrets
dotnet user-secrets init

# Set Notion configuration
dotnet user-secrets set "FireChief:NotionToken" "secret_your_notion_token"
dotnet user-secrets set "FireChief:TeamDatabaseId" "your-team-database-id"
dotnet user-secrets set "FireChief:RosterDatabaseId" "your-roster-database-id"

# Set Slack configuration
dotnet user-secrets set "FireChief:SlackBotToken" "xoxb-your-slack-bot-token"
dotnet user-secrets set "FireChief:SlackInternalChannelId" "C01234INTERNAL"
dotnet user-secrets set "FireChief:SlackPublicChannelId" "C01234PUBLIC"
```

**Verify your secrets:**

```bash
dotnet user-secrets list
```

### Alternative: Environment Variables

If you prefer environment variables:

```bash
export FireChief__NotionToken="secret_your_notion_token"
export FireChief__TeamDatabaseId="your-team-database-id"
export FireChief__RosterDatabaseId="your-roster-database-id"
export FireChief__SlackBotToken="xoxb-your-slack-bot-token"
export FireChief__SlackInternalChannelId="C01234INTERNAL"
export FireChief__SlackPublicChannelId="C01234PUBLIC"
```

Note: Use double underscores `__` for nested configuration in environment variables.

## 5. Running Locally

### Restore Dependencies

```bash
cd src/
dotnet restore
```

### Run Assignment Workflow

Test the Friday assignment workflow:

```bash
# Ensure DOTNET_ENVIRONMENT is set
export DOTNET_ENVIRONMENT=Development

# Run assignment
dotnet run -- assign
```

Expected behavior:
- Selects a Fire Chief and Backup from your team database for next Monday
- Creates a roster entry in Notion and increments Chief Count
- Posts messages to both Slack channels
- Sets channel topics with Fire Chief mention
- Sends handover coordination message to internal channel

### Run Monday Reminder

Test the Monday reminder:

```bash
dotnet run -- remind-monday
```

Expected behavior:
- Finds the current week's assignment
- Sends welcome reminder to Fire Chief in internal channel
- Sets channel topic with Fire Chief mention

### View Available Commands

```bash
dotnet run -- help
```

## 6. Testing Tips

### Populate Test Data

Add at least 2 team members to your Team Database to test the rotation logic:

1. **Member 1:**
   - Name: Your name
   - Slack ID: Your Slack user ID
   - Active: ✅
   - Recent Chief Date: Leave empty or set to a past date

2. **Member 2:**
   - Name: Test user
   - Slack ID: Another user's ID
   - Active: ✅
   - Recent Chief Date: Leave empty or set to a past date

### Get Your Slack User ID

1. Click your profile picture in Slack
2. Select **"Profile"**
3. Click `•••` (more menu) → **"Copy member ID"**

### Test Volunteer System

1. Check the `Volunteer` checkbox for a team member
2. Run `dotnet run -- assign`
3. The volunteer should be selected as Fire Chief
4. The checkbox will be automatically unchecked after assignment

### Test Rotation Logic

1. Run assignment multiple times
2. Check that it rotates fairly based on `Recent Chief Date`
3. Verify the date updates in Notion after each assignment

### Clear Test Data

To reset and test again:
1. Delete entries from the Weekly Roster database
2. Clear or adjust `Recent Chief Date` in Team Database
3. Uncheck any `Volunteer` flags

## 7. Development Workflow

### Watch Mode

For rapid development, use watch mode:

```bash
dotnet watch run -- assign
```

Code changes will trigger automatic rebuilds.

### Debugging

Add breakpoints and run with debugging:

```bash
# Visual Studio Code
1. Open Command Palette (Cmd/Ctrl + Shift + P)
2. Select "Debug: Start Debugging"
3. Choose ".NET Core Launch"

# Visual Studio
F5 to start debugging
```

### Logging

The application uses structured logging. Set log level in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "FireChief": "Debug"
    }
  }
}
```

## 8. Troubleshooting

### "Configuration binding failed"

- Ensure `DOTNET_ENVIRONMENT=Development` is set
- Verify user secrets are configured correctly
- Run `dotnet user-secrets list` to confirm values

### "Notion API error: Unauthorized"

- Check your Notion token is valid
- Ensure the integration is connected to both databases
- Verify database IDs are correct

### "Slack API error: not_in_channel"

- Add the bot to your test channels
- For private channels, explicitly add the app via Integrations

### "No active members found"

- Ensure at least one team member has `Active` checkbox checked
- Verify the Team Database ID is correct

### Messages not posting

- Verify Slack bot token and channel IDs
- Check bot permissions include `chat:write` and `chat:write.public`
- Ensure bot is added to channels

## 9. Differences from Production

When running locally:

- Uses your test Notion databases (no risk to production data)
- Posts to your test Slack channels (no team interruption)
- Can test volunteer system without affecting real rotation
- Safe to experiment and iterate rapidly

## 10. Contributing Changes

Once you've tested your changes locally:

1. Commit your code (secrets are never committed)
2. Push to a branch
3. Open a pull request
4. GitHub Actions will test in a production-like environment

## Resources

- [Notion API Documentation](https://developers.notion.com/)
- [Slack API Documentation](https://api.slack.com/)
- [.NET User Secrets Guide](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [FireChief Concept](./concept.md) - Understanding the Fire Chief role
- [Setup Guide](./setup.md) - Production deployment
