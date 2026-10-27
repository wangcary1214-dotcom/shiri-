# Code signing policy

## Current status

Windows signatures are **not active**. No SignPath approval or certificate has been granted to Shiri, and unsigned files must not be described as signed. This repository prepares the source/build/policy information for a possible application; publishing source does not itself submit or approve an application.

Repository: https://github.com/wangcary1214-dotcom/shiri- . Source license: MIT for Shiri-owned code; third-party and artwork exceptions are described in [LICENSE_SCOPE.md](LICENSE_SCOPE.md).

## Maintainer roles

- Author / committer: [wangcary1214-dotcom](https://github.com/wangcary1214-dotcom).
- Reviewer: the repository owner reviews external contributions before merging.
- Proposed signing approver: the same repository owner, with actual SignPath role assignment and multi-factor authentication to be completed in the provider account.

Every production signing request requires explicit maintainer approval. A successful build alone does not authorize signing.

## Proposed scope and process

Only Shiri's own Windows executable, built on GitHub-hosted Windows runners from the recorded source commit, may be submitted for Shiri signing. Third-party WebView2/OCR executables and DLLs retain their own publisher and licenses. Upstream floral code provenance and artwork scope must be disclosed to the Foundation and reviewed before enabling production signing.

The source build uploads an unsigned GitHub artifact for provenance. Optional SignPath signing fails if required settings are absent. A returned executable must pass Authenticode, expected-publisher and timestamp checks before being presented as signed. No private signing key is stored in the source repository. The current workflow creates build artifacts, not a public release automatically.

## Attribution and privacy

Required attribution after a sponsorship is approved: “Free code signing provided by SignPath.io, certificate by SignPath Foundation”. This is a planned statement and does not claim current sponsorship.

[Privacy policy](PRIVACY.md). Application content is stored locally; networked dependency downloads at build time, WebView2's own runtime behavior, and explicitly clicked external links are disclosed there.

[SignPath Foundation conditions](https://signpath.org/terms) and [GitHub origin verification](https://docs.signpath.io/trusted-build-systems/github) govern eligibility. Code signing identifies a publisher and file integrity; SmartScreen also considers reputation and may still warn.
