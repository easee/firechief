# Notion Integration Configuration

This file contains the text and configuration used for the FireChief Notion integration.

## Integration Name
**FireChief Bot**

## Logo
See `fire_chief_bot.png` in this directory.

## Description
Manages automated Fire Chief rotation scheduling. Reads team roster, tracks assignment history, and creates weekly roster entries.

## Detailed Description
The FireChief Bot integration automates support rotation by:
- Reading team member availability and preferences
- Tracking assignment history for fair rotation
- Creating weekly roster entries automatically
- Updating last-served dates for team members

This integration is designed to work with GitHub Actions to provide fully automated weekly Fire Chief assignments.

## Integration Type
**Internal Integration** (for your workspace only)

## Capabilities Required
- [x] Read content
- [x] Update content
- [x] Insert content

## Required Databases

### 1. Team Roster Database
Must be connected to this integration.

**Required Properties:**
- Name (Title) - Team member name
- Email (Email) - Contact email
- Active (Checkbox) - Whether member is available for duty
- Volunteer (Checkbox) - Whether member volunteers for next rotation
- Last Chief Date (Date) - Last date served as Fire Chief
- Last Backup Date (Date) - Last date served as Backup

### 2. Weekly Roster Database
Must be connected to this integration.

**Required Properties:**
- Week Start (Date) - Monday of the rotation week
- Chief (Person or Relation) - Assigned Fire Chief
- Backup (Person or Relation) - Assigned Backup
- Status (Select) - Assignment status (Scheduled, Active, Completed)

## Setup Instructions

1. Create the integration at https://www.notion.so/my-integrations
2. Copy the Internal Integration Token
3. Add the integration to both databases:
   - Open database → ... menu → Connections → Add FireChief Bot
4. Get database IDs from URLs (see setup guide)
5. Add credentials to GitHub Secrets

## Permissions
This integration needs:
- Read access to Team Roster (to find available members)
- Write access to Team Roster (to update last-served dates)
- Write access to Weekly Roster (to create new assignments)

## Security Notes
- Integration token should be stored in GitHub Secrets, never committed to code
- Token provides full access to connected databases only
- Rotate token if compromised
- Limit integration to only required databases

## Support
For issues or questions, see repository: [Add your repo URL]
