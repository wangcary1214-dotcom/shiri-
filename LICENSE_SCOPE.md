# License scope

The root MIT license applies to Shiri's own Windows program source and integration/build code published in this repository, under the copyright of wangcary1214-dotcom.

## Floral Notepaper

Adapted upstream source in Windows/desktop/floral/src/components/Tile.tsx and src/features/markdown, plus related color utilities, retains Copyright (c) 2026 Achilng and its MIT license. Full text: Windows/desktop/floral/LICENSE.floral-notepaper. Upstream: https://github.com/Achilng/floral-notepaper ; reference commit 23e6df7bfaf785c0bbf7f7c2c05b6d01a53a59e9. Tauri/Rust executable shell and the upstream signing identity are not included.

## Pet artwork and character

Pet pictures are not copied into this source repository or relicensed under MIT. Build-time restoration references the project's existing public web asset repository at a fixed commit and verifies the approved frame hashes. The character is known as DeepSeek Whale-chan / 大肥鱼; the local historical notes refer to the Neko3000 community character profile for characterization, not a new copyright grant: https://github.com/Neko3000/deepseek-whalechan . No separate permissive image/character license has been verified by this source publication. Existing availability on a public website is not an assertion that third parties may reuse the artwork under MIT. Confirm applicable artwork/character permissions before redistributing modified art or representing the full application as entirely MIT-licensed. This scope must be disclosed during signing-provider review.

Original Shiri dialogue code/content is separate from the character artwork. The 71 original frame files are not edited by this publication. The source build manifest pins their existing public bytes.

## Other dependencies

React/Markdown/KaTeX and build dependencies retain their respective licenses, as declared in the lockfile and THIRD_PARTY_NOTICES.md. No node_modules is published here. WebView2 SDK binaries are restored from Microsoft's NuGet package with a pinned SHA256; Microsoft's license and notice must accompany runtime SDK DLL distribution. WebView2 Runtime is supplied by Microsoft under its own terms. The optional PaddleOCR-json engine is separately distributed under its own licenses; do not sign upstream DLLs or executables as Shiri-owned binaries.
