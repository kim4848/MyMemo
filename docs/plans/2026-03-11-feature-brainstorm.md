# MyMemo Feature Brainstorm

_2026-03-11_

Feature ideas for MyMemo, organized by theme. Each feature includes a brief description, the value it brings, and a rough complexity estimate (S/M/L).

---

## 1. Core Recording & Transcription

### 1.1 Live Transcription Preview (M)
Stream partial transcription results to the frontend during recording instead of waiting until a chunk is fully processed. Show a rolling transcript on the RecorderPage so users can verify the mic is picking up speech correctly.

**Value:** Immediate feedback during recording. Catch mic problems early.

### 1.2 Multi-language Auto-detection (M)
Detect the spoken language per chunk instead of assuming Danish. Fall back to a user-configured default. Display detected language in the session detail view.

**Value:** Support bilingual meetings (Danish/English) without manual configuration.

### 1.3 Custom Vocabulary / Glossary (S)
Let users define domain-specific terms, names, and acronyms that the transcription should prefer. Pass these as prompt hints to Whisper.

**Value:** Better accuracy for company-specific jargon, product names, and abbreviations.

### 1.4 Noise / Confidence Indicator (S)
Surface the per-chunk confidence scores already stored in the database. Show a quality indicator on the session detail page so users know which sections may need review.

**Value:** Helps users focus review effort on low-confidence segments.

### 1.5 Pause & Resume Recording (S)
Allow pausing a session without stopping it. Skip silence during pauses to save transcription cost and reduce noise in the output.

**Value:** Natural breaks (coffee, sidebar) don't pollute the transcript.

---

## 2. Memo & Output Enhancements

### 2.1 Editable Memos (M)
Add an inline markdown editor on the SessionDetailPage so users can correct, annotate, or restructure the generated memo before exporting.

**Value:** Users can fix errors and add context without leaving the app.

### 2.2 Regenerate Memo with Different Mode (S)
Let users regenerate a memo in a different output mode (e.g. switch from "summary" to "full") or with an updated context prompt without re-transcribing.

**Value:** Get both a summary and a full transcript from the same session cheaply.

### 2.3 Follow-up Questions / Chat with Transcript (L)
After a session is complete, let users ask questions about the transcript: "What did we decide about the budget?" — answered by the LLM using the full transcript as context.

**Value:** Quick retrieval of specific information from long meetings.

### 2.4 Action Item Extraction & Tracking (M)
Automatically extract action items (who, what, when) from the memo. Display them as a checklist. Optionally sync to external task tools.

**Value:** Meetings produce accountable follow-ups without manual work.

### 2.5 Email Memo to Participants (S)
Add a "Send via email" button that lets users enter recipient addresses and send the memo as a formatted email.

**Value:** Share meeting notes with people who don't have MyMemo accounts.

### 2.6 Custom Output Templates (M)
Let users define their own LLM prompt templates beyond "full" / "summary" / "product-planning". For example: "standup notes", "1:1 recap", "client call brief".

**Value:** Tailor output to specific meeting types without changing code.

---

## 3. Organization & Search

### 3.1 Full-text Search Across Sessions (M)
Index all transcriptions and memos for full-text search. Let users search from the dashboard with highlighted results linking to the relevant session.

**Value:** Find past discussions quickly as session count grows.

### 3.2 Tags & Folders (S)
Let users organize sessions with tags (e.g. "project-x", "weekly-sync") and/or folders. Filter the dashboard by tag.

**Value:** Keep the dashboard manageable as usage scales.

### 3.3 Favorites & Pinning (S)
Pin important sessions to the top of the dashboard.

**Value:** Quick access to frequently referenced sessions.

### 3.4 Session Archive (S)
Move old sessions to an archive view rather than deleting them. Archived sessions don't clutter the main dashboard but remain searchable.

**Value:** Clean dashboard without losing history.

---

## 4. Collaboration & Sharing

### 4.1 Shared Sessions (M)
Generate a shareable link for a session (read-only). Recipients can view the memo and transcript without an account.

**Value:** Share meeting notes with external stakeholders easily.

### 4.2 Team Workspaces (L)
Group users into teams/organizations. Sessions within a workspace are visible to all members. Role-based access (admin, member, viewer).

**Value:** Enables team-wide adoption instead of individual use.

### 4.3 Comments & Annotations (M)
Let users (or shared viewers) add inline comments on specific sections of a memo or transcript.

**Value:** Collaborative review of meeting notes.

---

## 5. Integrations

### 5.1 Calendar Integration (M)
Connect to Outlook/Google Calendar. Auto-create a session when a meeting starts, pre-fill title and participants from the calendar event.

**Value:** Zero-friction recording setup — just click "record" when the meeting starts.

### 5.2 Slack / Teams Notifications (S)
Post a summary to a Slack channel or Teams chat when a memo is generated.

**Value:** Meeting notes reach the team where they already work.

### 5.3 Task Manager Sync (M)
Push extracted action items to Todoist, Jira, Asana, or Microsoft To Do.

**Value:** Action items land directly in the team's task tracker.

### 5.4 Webhook / Zapier Support (M)
Fire a webhook when a memo is generated. Enables users to build custom automations via Zapier, Make, or Power Automate.

**Value:** Extensibility without building every integration natively.

### 5.5 Google Docs Export (S)
Export memos directly to a Google Doc, similar to the existing OneNote integration.

**Value:** Support users in the Google Workspace ecosystem.

---

## 6. Analytics & Insights

### 6.1 Meeting Analytics Dashboard (M)
Show aggregate stats: total meeting hours, average meeting duration, sessions per week, most active days. Trend charts over time.

**Value:** Help users understand their meeting load and patterns.

### 6.2 Keyword / Topic Trends (L)
Extract key topics from memos over time. Show which topics are discussed most frequently and how they trend.

**Value:** Organizational awareness of recurring themes and priorities.

### 6.3 Speaker Talk-time Breakdown (S)
For sessions using speaker diarization, show a pie chart of talk-time per speaker.

**Value:** Quantify meeting participation balance.

---

## 7. Mobile & Desktop

### 7.1 Progressive Web App (PWA) (S)
Add a web manifest and service worker so the app can be installed on mobile home screens and works offline for viewing cached sessions.

**Value:** Mobile access without building a native app.

### 7.2 Electron Desktop App (L)
_Already planned for V2._ Native system audio capture without screen-share dialogs. Tray icon for quick start/stop.

**Value:** Frictionless recording on desktop, especially for virtual meetings.

### 7.3 Mobile Recording (M)
Optimize the RecorderPage for mobile browsers. Support recording in-person meetings from a phone placed on the table.

**Value:** Capture in-person meetings without a laptop.

---

## 8. Quality of Life

### 8.1 Session Templates (S)
Save a set of session settings (output mode, audio source, context) as a reusable template. One click to start a "Weekly Standup" or "Client Call".

**Value:** Faster session creation for recurring meeting types.

### 8.2 Auto-title Sessions (S)
Use the LLM to generate a descriptive title from the first few minutes of transcription, replacing the generic timestamp-based default.

**Value:** Meaningful session names without manual effort.

### 8.3 Keyboard Shortcuts (S)
Global shortcuts for start/stop/pause recording. Essential for the desktop app, useful in the browser too.

**Value:** Operate the recorder without switching windows.

### 8.4 Dark Mode (S)
Add a dark theme toggle. Respect system preference by default.

**Value:** Comfortable use in low-light environments and user preference.

### 8.5 Notification when Memo is Ready (S)
Browser notification (or push notification for PWA) when transcription and memo generation are complete.

**Value:** Users don't have to keep checking back — they get notified.

---

## Prioritization Suggestion

High-impact, lower-effort features to consider first:

| Priority | Feature | Complexity |
|----------|---------|------------|
| 1 | Pause & Resume Recording (1.5) | S |
| 2 | Regenerate Memo with Different Mode (2.2) | S |
| 3 | Auto-title Sessions (8.2) | S |
| 4 | Tags & Folders (3.2) | S |
| 5 | Notification when Memo Ready (8.5) | S |
| 6 | Editable Memos (2.1) | M |
| 7 | Full-text Search (3.1) | M |
| 8 | Action Item Extraction (2.4) | M |
| 9 | Calendar Integration (5.1) | M |
| 10 | Follow-up Questions / Chat (2.3) | L |
