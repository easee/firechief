# Slack Bot Configuration

This file contains the text and configuration used for the FireChief Slack bot.

## App Name
**FireChief Bot**

## App Icon
See `fire_chief_bot.png` in this directory.

## Short Description
Automates weekly Fire Chief rotation assignments and reminders for support duty scheduling.

## Long Description
FireChief Bot automates your team's support rotation schedule. Each Monday, it selects the next Fire Chief and Backup based on fair rotation and volunteer preferences, then announces assignments to your team channels. On Fridays, it sends handover reminders to ensure smooth transitions.

Key features:
- Fair rotation based on previous assignments
- Volunteer priority system
- Automatic Slack announcements with pinning
- Notion database integration for tracking
- Handover reminders

The bot helps teams maintain consistent support coverage while minimizing coordination overhead.

## Display Name
**FireChief Bot**

## Bot Username
`@firechief`

## Required Bot Token Scopes
- `chat:write` - Post messages to channels
- `chat:write.public` - Post to public channels without joining
- `pins:write` - Pin important messages
- `users:read` - Read user information for @mentions

## Suggested Features to Enable
- [ ] Always Show My Bot as Online
- [ ] Bots (enable bot functionality)
- [ ] Permissions (OAuth scopes)

## App Home Settings

### Home Tab
Not required for this bot.

### Messages Tab
Enable: **Allow users to send Slash commands and messages from the messages tab**

### About This App (Public Metadata)

**App Directory Description:**
```
Automates weekly Fire Chief support rotation. Selects team members fairly, announces assignments every Monday, and sends Friday handover reminders. Integrates with Notion for tracking.
```

**Configuration Tips:**
```
1. Set up Notion integration first
2. Add required scopes: chat:write, pins:write, users:read
3. Invite bot to your team channels
4. Configure GitHub Actions with bot token
5. Customize schedules in workflow files
```

## Installation Instructions (for workspace admins)

1. Create the app at https://api.slack.com/apps
2. Add the required bot scopes (see above)
3. Install to workspace
4. Copy the Bot User OAuth Token (starts with `xoxb-`)
5. Invite the bot to your channels: `/invite @firechief`
6. Add the token to GitHub Secrets for automation

## Support
For issues or questions, see repository: [Add your repo URL]
