# PageArc v1.3.1

PageArc v1.3.1 completes the WebDAV archive workflow introduced in v1.3.

## WebDAV synchronization

- Sync now reads the remote archive list first and builds a local complete snapshot before deciding whether an upload is necessary.
- The latest cloud archive is downloaded and compared with the local snapshot using a stable SHA-256 content fingerprint that ignores export timestamps but includes reading data and archived ebook bytes.
- If local and cloud content are identical, PageArc reports that no differences were found and **does not upload a duplicate archive**.
- If differences exist, PageArc merges local and cloud reading data/book files. A new cloud archive is uploaded only when the merged result contains data not already present in the latest cloud archive.
- Folder-based WebDAV configurations now keep timestamped/versioned archive history using names such as `PageArc-20260904T010203Z-v1.3.1.pagearcbackup`.
- A visible progress bar reports listing, snapshot creation, download, comparison, merge, restore, and upload stages; byte transfer progress is reported for downloads and uploads.

## Cloud archive restore and management

The restore picker and archive-management dialogs are directly ported from UrbanPlanToolbox commit
`249bbf99088e5edc92b9a6f9b7635ca777cf847e`:

- single-selection `ListView`;
- `MinWidth = 520`, `MaxHeight = 360`;
- each item shows local timestamp, app version and file size on the first line, then the archive filename;
- Restore remains disabled until a selection is made;
- Manage archives uses Restore / Delete / Close with Restore and Delete disabled until selection;
- deleting an archive refreshes the list.

PageArc keeps its own safe merge semantics when restoring: existing local books are not deleted, and recovered book files are written to the durable managed-library directory.

## Version

- Product / assembly: `1.3.1`
- GitHub / acceptance MSIX: `1.3.1.0`
- Microsoft Store identity: `2026.904.131.0`
