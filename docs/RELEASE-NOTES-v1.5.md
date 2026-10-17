# PageArc v1.5.0 — faster book opening

## Faster first page
- The reader opens the saved EPUB/FB2/MOBI/AZW3/LIT section immediately instead of waiting for full-book page measurement.
- An estimated book-wide page map is refined as sections are visited. EPUB estimates now use uncompressed spine item sizes.
- EPUB extraction is cached across launches when the original file size and modified time have not changed.
- EPUB decompression and manifest parsing run off the WinUI UI thread, and generated chapter HTML is no longer unnecessarily written back to disk.
- Tabs restored from the previous session are initialized only when selected.
- Kindle parser skips copying source books to its workspace when the cached copy has not changed.

## Reading data
- The reader no longer runs a pre-display pagination loop that could overwrite the saved chapter and progress.
- Reading position is still preserved in section/fraction form across layout changes.

## Versions
- App / GitHub MSIX: `1.5.0.0`
- Microsoft Store package: `2026.1017.150.0`
- Optional PageArc.ConversionRuntime remains separate and unchanged.
