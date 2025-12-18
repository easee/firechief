# FireChief Bot Setup Guide

This guide explains how to set up automated Fire Chief assignments using the FireChief bot with GitHub Actions.

## Prerequisites

Before setting up the bot, ensure you have:

- A Notion workspace with two databases: Team Roster and Weekly Roster
- A Slack workspace with a bot app created
- Admin access to your GitHub repository
- The FireChief bot code deployed to your repository

## Overview

The FireChief bot runs automatically on:

- **Every Monday at 8:00 AM UTC** - Creates weekly Fire Chief assignment
- **Every Friday at 3:00 PM UTC** - Sends a handover reminder to current Chief

You can also trigger workflows manually from the GitHub Actions tab.

## Step 1: Notion Setup

### Create Required Databases

You need two Notion databases:

#### 1. Team Roster Database

- Contains your team members
- Required properties:
  - Name (text)
  - Email (email)
  - Active (checkbox)
  - Volunteer (checkbox)
  - Last Chief Date (date)
  - Last Backup Date (date)

#### 2. Weekly Roster Database

- Tracks weekly assignments
- Required properties:
  - Week Start (date)
  - Chief (person/relation)
  - Backup (person/relation)
  - Status (select)

### Create Notion Integration

1. Go to <https://www.notion.so/my-integrations>
2. Click **New integration**
3. Give it a name (e.g., "FireChief Bot")
4. Select the workspace
5. Copy the **Internal Integration Token** (starts with `ntn_` or `secret_`)

### Connect Databases to Integration

1. Open each database in Notion
2. Click the **...** menu → **Connections** → Add your FireChief integration
3. The integration now has access to read/write these databases

### Get Database IDs

For each database:

1. Open the database as a full page in Notion
2. Look at the URL: `https://www.notion.so/{DATABASE_ID}?v=...`
3. Extract the 32-character ID
4. Format with dashes: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`

Example: `940314ed-a82f-467d-8cdc-dd1d226fd869`

## Step 2: Slack Setup

### Create Slack App

1. Go to <https://api.slack.com/apps>
2. Click **Create New App** → **From scratch**
3. Give it a name (e.g., "FireChief Bot")
4. Select your workspace

### Add Bot Scopes

1. Go to **OAuth & Permissions**
2. Scroll to **Scopes** → **Bot Token Scopes**
3. Add these scopes:
   - `chat:write` - Post messages
   - `chat:write.public` - Post to public channels without joining
   - `pins:write` - Pin messages
   - `users:read` - Read user information

### Install to Workspace

1. Go to **Install App**
2. Click **Install to Workspace**
3. Authorize the app
4. Copy the **Bot User OAuth Token** (starts with `xoxb-`)

### Get Channel IDs

For each channel you want the bot to post to:

1. Right-click on the channel → **View channel details**
2. Scroll to the bottom
3. Copy the **Channel ID** (format: `C01234ABCDE`)

### Invite Bot to Channels

In each channel, type:

```
/invite @FireChief
```

## Step 3: GitHub Secrets

Go to your repository on GitHub:

1. Navigate to **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret**
3. Add the following secrets:

| Secret Name                 | Description                             | Example Value                          |
|-----------------------------|-----------------------------------------|----------------------------------------|
| `NOTION_TOKEN`              | Notion integration token                | `ntn_...` or `secret_...`              |
| `TEAM_DATABASE_ID`          | Team members database ID (with dashes)  | `940314ed-a82f-467d-8cdc-dd1d226fd869` |
| `ROSTER_DATABASE_ID`        | Weekly roster database ID (with dashes) | `79ca9a81-5431-4fb1-8fb4-85053a1e7ebb` |
| `SLACK_BOT_TOKEN`           | Slack bot OAuth token                   | `xoxb-...`                             |
| `SLACK_INTERNAL_CHANNEL_ID` | Internal team channel ID                | `C0A41K0U0FP`                          |
| `SLACK_PUBLIC_CHANNEL_ID`   | Public announcement channel ID          | `C0A41K0U0FP`                          |

## Step 4: Deploy Workflows

The workflow files are located in `.github/workflows/`:

- `firechief.yml` - Monday assignment workflow
- `friday-reminder.yml` - Friday reminder workflow

If they're not already in your repository:

```bash
git add .github/workflows/
git commit -m "Add GitHub Actions workflows for Fire Chief automation"
git push
```

## Step 5: Verify Setup

1. Go to your repository on GitHub
2. Click the **Actions** tab
3. You should see two workflows:
   - "Fire Chief - Monday Assignment"
   - "Fire Chief - Friday Reminder"

## Step 6: Test Manually (Recommended)

Before waiting for the scheduled run:

1. Go to the **Actions** tab
2. Select "Fire Chief - Monday Assignment"
3. Click **Run workflow** → **Run workflow**
4. Monitor the execution logs for any errors

## Schedule Configuration

### Monday Assignment

- **Cron**: `0 8 * * 1`
- **Time**: Every Monday at 8:00 AM UTC
- **Actions**:
  - Unpins all previous messages
  - Selects Fire Chief and Backup
  - Creates Notion roster entry
  - Posts and pins messages to Slack

### Friday Reminder

- **Cron**: `0 15 * * 5`
- **Time**: Every Friday at 3:00 PM UTC
- **Actions**:
  - Finds current week's Fire Chief
  - Sends handover reminder
  - Pins the reminder message

### Adjusting Schedule

Edit the cron expressions in the workflow files:

```yaml
on:
  schedule:
    - cron: '0 8 * * 1'  # minute hour day month weekday
```

**Examples**:

- `0 9 * * 1` - Monday at 9:00 AM UTC
- `30 7 * * 1` - Monday at 7:30 AM UTC
- `0 8 * * MON` - Alternative syntax for Monday

**Cron syntax**: `minute hour day month weekday`

- `0-59` for minutes
- `0-23` for hours (UTC)
- `1-31` for day of month
- `1-12` for month
- `0-6` for day of week (0 = Sunday) or MON, TUE, etc.

## Troubleshooting

### Workflow fails with "Invalid request URL"

- Check that database IDs have dashes in the correct format
- Verify IDs in GitHub Secrets match your Notion databases
- Test the Notion API manually with curl if needed

### Workflow fails with "missing_scope"

- Ensure Slack bot has all required scopes (`chat:write`, `pins:write`)
- Go to **OAuth & Permissions** in Slack app settings
- Reinstall the app to workspace after adding scopes

### Workflow fails with "channel_not_found"

- Verify channel IDs are correct
- Ensure bot is invited to both channels (`/invite @FireChief`)
- Check if channel is private (bot needs explicit invite)

### Workflow fails with "unauthorized" or "invalid_auth"

- Verify the `SLACK_BOT_TOKEN` is the **Bot User OAuth Token**, not the app-level token
- Check that the token starts with `xoxb-`
- Regenerate the token if needed

### Check detailed logs

1. Go to the **Actions** tab
2. Click on the failed workflow run
3. Expand the failing step to see detailed logs
4. Look for specific error messages

## Security Notes

- Secrets are encrypted and never exposed in logs
- `appsettings.json` is in `.gitignore` to prevent committing secrets
- Workflows create a config file dynamically from secrets at runtime
- Never print secrets in workflow logs for debugging
- Use GitHub's masked logging for sensitive values

## Next Steps

After setup is complete:

- Test both workflows manually to verify they work
- Check Slack channels for successful messages
- Verify Notion databases are updated correctly
- Review the [Usage Guide](./usage.md) for day-to-day operations
