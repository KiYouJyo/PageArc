# PageArc Windows Integration

PageArc v0.6 integrates the existing reader/library pipeline with Windows shell activation without adding a second navigation or reader UI.

## File associations

The packaged manifest template is `Packaging/PageArc.Package.appxmanifest`. It declares one PageArc ebook association containing:

- `.epub`
- `.fb2`
- `.mobi`
- `.azw`
- `.azw3`
- `.lit`

The association only selects PageArc as an eligible Windows opener. The activated path is still validated by `BookFormatRegistry`, imported through `LibraryService`, and opened through the existing `FlowReaderEngine` path. Source files are never modified by activation.

## Protocol deep links

PageArc registers the `pagearc:` protocol in packaged builds.

Stable forms are:

- `pagearc://book/<library-id>` — reopen an existing PageArc library book.
- `pagearc://open?book=<library-id>` — equivalent explicit book form.
- `pagearc://open?path=<encoded-local-path>` — import/open a supported local ebook path.

Unknown or unresolved protocol requests fall back to the library instead of creating a separate error surface.

## Single instance

`WindowsAppLifecycleService` uses a stable `AppInstance` key (`PageArc.Main`).

The primary instance subscribes to redirected activation events. A later instance attempts to redirect its activation and then exits; redirect failure does not create a second independent PageArc UI. Early activation events are queued until the main window exists, and activation execution is serialized so concurrent shell launches cannot race the library/import state.

Unpackaged development builds retain a command-line fallback if packaged Windows App Lifecycle registration is unavailable.

## Activation routing

`AppActivationRequestParser` converts Windows activation data into a small format-neutral request model:

- Launch
- Files
- Book
- Protocol

`App.xaml.cs` owns the routing boundary. File activation imports through `LibraryService.ImportDetailedAsync`, preserving duplicate detection and metadata handling, then opens the first resolved book in the existing `MainWindow` reader. Book-ID activation resolves an existing library record and uses the same `OpenBook` method.

## Jump List

Successful normal book opens call `JumpListService.RecordRecentBookAsync`. The service stores at most eight recent PageArc books and uses `pagearc://book/<id>` arguments so a Jump List click returns to the same library identity instead of depending on a possibly moved filename.

Jump List failures are non-fatal and are logged through startup diagnostics.

## Explorer integration

The registered file association supplies the native Windows Open/Open with path. PageArc's library context menu also exposes “Show file location” through Explorer without modifying or moving the source ebook.

## Packaging and identity

The repository keeps an explicit package-manifest template instead of forcing normal unpackaged development builds to carry a package identity. Signed acceptance copies/transforms the template into `Package.appxmanifest` for the acceptance identity. A production Store/GitHub package should apply its assigned package identity while retaining the association/protocol `Extensions` section.

## Acceptance

The v0.6 signed Windows acceptance gate validates:

1. Release tests plus Debug/Release x64 builds.
2. MSIX creation and signing by the configured PageArc acceptance certificate.
3. Signed package installation.
4. Installed manifest file-association and protocol registrations.
5. Packaged app launch.
6. `pagearc:` protocol activation redirected to the same PageArc process.
7. Externally activated supported ebook paths routed through the shared library import path.
8. Artifact generation for manual acceptance.

No new visible in-app UI was added for v0.6; shell integration returns users to the existing Figma-approved PageArc surfaces.
