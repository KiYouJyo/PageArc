# PageArc v1.3

PageArc v1.3 upgrades the Data management section and the complete-backup/WebDAV workflow.

## Data management UI

- The Data management card now **directly reuses the structural contract from UrbanPlanToolbox** at commit `249bbf99088e5edc92b9a6f9b7635ca777cf847e`.
- Copied values include the outer settings-card style, 16 DIP card padding, 8 DIP corner radius, 16/4 DIP section spacing, 14 DIP inner-panel padding, 12/3/2 DIP panel spacing, 12 DIP column gap, 8 DIP action gap, status typography, default native button sizing, and the 520 DIP two-column breakpoint.
- The local panel keeps the same three-action pattern; the WebDAV panel keeps the same four-action pattern and inline `InfoBar` feedback.

## Backup and WebDAV

- `.pagearcbackup` packages contain book files together with reading progress, bookmarks, annotations and notes.
- WebDAV accepts a folder URL or direct archive URL and transfers the complete package.
- The cloud panel supports two-way sync, explicit restore-from-cloud, archive inspection, and configuration.
- Restored books are stored in PageArc's durable managed library and are not removed by cache cleanup.

## Version

- Product: `1.3`
- Assembly / GitHub MSIX: `1.3.0.0`
- Microsoft Store identity: `2026.904.130.0`
