# FireChief Bot Usage Guide

This guide covers day-to-day usage of the FireChief bot and how to interact with the automated rotation system.

## How the Bot Works

### Weekly Flow

**Monday Morning (8:00 AM UTC)**

1. Bot unpins previous week's messages
2. Selects next Fire Chief and Backup based on priority:
   - Volunteers (anyone who checked "Volunteer" in Notion)
   - Fair rotation (longest time since last assignment)
   - Random selection if there are ties
3. Creates entry in Weekly Roster database
4. Posts announcements to Slack channels
5. Pins messages for visibility

**Friday Afternoon (3:00 PM UTC)**

1. Bot identifies current week's Fire Chief
2. Sends handover reminder message
3. Pins reminder for visibility
4. Chief prepares handover notes

### Assignment Priority Logic

The bot follows this priority order when selecting Fire Chief and Backup:

1. **Volunteers First** – Anyone with "Volunteer" checkbox enabled
2. **Fairness Second** – Team members who haven't served recently (longest time since last role)
3. **Random Selection** – If multiple people have the same "last served" date, random selection applies
4. **Active Members Only** – Only considers team members with "Active" checkbox enabled

## Volunteering for Fire Chief

To volunteer for next week:

1. Open the **Team Roster** database in Notion
2. Find your row
3. Check the **Volunteer** checkbox
4. The bot will prioritize you in the next assignment

**Note**: The volunteer flag is automatically cleared after you're assigned, so you need to check it again if you want to volunteer for another week.

## Manual Override

If you need to swap Fire Chief duties or make manual adjustments:

### Option 1: Swap in Notion (Preferred)

1. Open the **Weekly Roster** database
2. Find the current or upcoming week's entry
3. Change the **Chief** or **Backup** fields to different team members
4. Update the **Status** if needed
5. Communicate the change to your team

### Option 2: Volunteer for Next Week

1. Check the **Volunteer** box in Team Roster (see above)
2. Ask the current assignee to uncheck their volunteer box if needed
3. The bot will respect this on Monday

### Option 3: Trigger Manual Assignment

1. Go to GitHub repository → **Actions** tab
2. Select "Fire Chief - Monday Assignment" workflow
3. Click **Run workflow** → **Run workflow**
4. This will create a new assignment immediately

## Understanding Slack Messages

### Monday Assignment Message

The bot posts two messages:

**Public Channel** (e.g., `#team-software`):

```text
🔥 This week's Fire Chief: @alice
🛡️ Backup: @bob

The Fire Chief is your go-to person for support requests this week!
```

**Internal Channel** (e.g., `#team-software-internal`):

```text
🔥 Fire Chief Assignment - Week of Jan 15, 2025

🧑‍🚒 Chief: @alice
🛡️ Backup: @bob

Remember to:
- Monitor #team-software for requests
- Triage and create Linear issues
- Shield the team from interruptions
- Prepare handover for next Friday
```

### Friday Reminder Message

**Internal Channel**:

```text
🔥 Fire Chief Handover Reminder

@alice - Your Fire Chief rotation ends this week!

Please prepare your handover:
- [ ] Review open support tickets
- [ ] Check pending Linear issues
- [ ] Brief on ongoing critical issues
- [ ] Schedule handover with next Chief

Next Chief will be assigned Monday morning.
```

## Pinned Messages

The bot automatically pins important messages:

- Monday's assignment announcement (unpins previous week)
- Friday's handover reminder

If you need to unpin manually:

- Hover over the message → **...** → **Remove from channel**

## Team Roster Management

Keep your team roster up to date in Notion:

### Active Status

- Check **Active** for team members currently available for Fire Chief duty
- Uncheck for team members on leave, contractors, or those who shouldn't be assigned

### Last Served Dates

- The bot automatically updates **Last Chief Date** and **Last Backup Date**
- These ensure fair rotation over time
- Don't manually change unless correcting a mistake

### Adding New Team Members

1. Add a new row to Team Roster database
2. Fill in: Name, Email
3. Check **Active**
4. Leave dates empty (they'll be set after first assignment)

### Removing Team Members

- Uncheck **Active** instead of deleting the row
- This preserves history and prevents accidental reassignment

## Troubleshooting

### I wasn't notified of my assignment

- Check if you're in both Slack channels
- Verify your Slack handle matches your Notion name
- Check pinned messages in the channel

### The wrong person was assigned

- Check the Team Roster for "Volunteer" flags
- Verify "Active" status is correct for all members
- Use manual override (see above) to fix immediately
- Review logs in GitHub Actions for assignment logic

### Bot didn't run this week

- Check GitHub Actions tab for workflow status
- Verify workflows are enabled (Actions → Enable workflow)
- Check if there were any failures (red X) and review logs
- Manually trigger the workflow if needed

### I need to swap with someone

- Coordinate directly with your team
- Update the Weekly Roster in Notion manually
- Announce the swap in your team channel

## Best Practices

### For Current Fire Chief

- Check Slack channels regularly throughout the day
- Create Linear issues immediately for tracking
- Tag appropriate team members when assigning work
- Keep handover notes throughout the week (don't wait until Friday)
- Block calendar time for Fire Chief duties

### For the Team

- Respect the Fire Chief's role – direct support requests to them first
- Tag the Fire Chief in channels for visibility
- Volunteer when you have lighter project workload
- Keep your Active status current in Notion

### For Handover

- Schedule 30 minutes for handover meeting
- Walk through open issues and context
- Share any ongoing incidents or blockers
- Highlight any patterns or recurring issues from the week

## Advanced: Customizing Bot Behavior

If you need to modify how the bot works:

### Changing Selection Logic

Edit `src/Services/AssignmentService.cs` to adjust:

- Priority weighting
- Tie-breaking logic
- Filtering criteria

### Modifying Messages

Edit `src/Services/SlackService.cs` to change:

- Message templates
- Emoji usage
- Formatting

### Adjusting Schedules

Edit `.github/workflows/*.yml` files to change:

- Cron schedules
- Workflow triggers
- Environment variables

After making changes, commit and push to trigger GitHub Actions to use the new configuration.

## Getting Help

If you encounter issues:

1. Check [Troubleshooting](#troubleshooting) section above
2. Review GitHub Actions logs for errors
3. Check [Setup Guide](./setup.md) for configuration issues
4. Reach out to your team's bot administrator
5. File an issue in the repository

## Reference

- [Fire Chief Concept](./concept.md) – Understanding the role
- [Setup Guide](./setup.md) - Initial configuration
- [GitHub Actions Workflows](../.github/workflows/) – Workflow definitions
