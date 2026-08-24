# PageArc 1.1 acceptance checklist

The local artifacts are unsigned preparation candidates. Do not treat package generation as install, launch, update, Store submission, certification, GitHub release, or public availability.

## Shell regression

- Start from a saved Light application theme while Windows itself is Dark, and repeat with the inverse combination.
- Cold-launch PageArc at wide and compact window widths. Confirm the navigation pane background and all menu labels use one readable theme from the first visible frame.
- Minimize/restore, deactivate/reactivate, resize across the pane breakpoint, switch Light/Dark/System, and relaunch. Confirm there is no flash or persistent pane mismatch.

## GitHub channel update

- Sign the 1.1 GitHub MSIX with the same trusted publisher certificate as the installed lower-version GitHub build.
- Publish a test Release containing a directly downloadable x64 `.msixbundle` or `.msix`; do not rely on `.appinstaller` or a ZIP wrapper.
- From the signed lower-version build, check for updates. Confirm PageArc stays in-app through check, download, deployment progress, and the restart-required state.
- Restart from the update button and verify package/application version `1.1.0.0`, retained LocalState/library data, file/protocol activation, and rollback/error messaging for an invalid signature or interrupted download.

## Microsoft Store channel update

- Upload the generated `.msixupload` only after signing/Partner Center preflight as applicable; wait for an actual flight/listing update newer than the installed Store package.
- From a Store-installed lower-version build, check for updates. Confirm the OS consent UI is owned by the PageArc window and no Store application/page is opened.
- Complete download/install, restart, and verify Store package version `2026.824.138.0` with display/application version `1.1`, retained LocalState/library data, and correct behavior for cancel, offline, Store service unavailable, and no-update states.

## Package and feature smoke tests

- Verify signature, identity, publisher, architecture, and `zh-CN`/`ja-JP`/`en-US` resources for each signed artifact.
- Install and launch each channel independently; do not install one channel over the other unless explicitly testing a supported migration.
- Exercise EPUB reading plus MOBI/AZW3/FB2/LIT opening, cover extraction, and all 20 calibre conversion directions. Confirm the package-size reductions did not remove dynamically loaded runtime files.
- Check Start/taskbar/file-association icons, splash rendering, Settings/About layout, WebDAV configuration, and reader theme default.
