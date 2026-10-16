# Releasing

Releases are cut by pushing a `vX.Y.Z` tag. The `Release` workflow builds, tests and packs; publishing to nuget.org uses
**NuGet trusted publishing (OIDC)**: no long-lived API key is stored in this repository or its secrets. Until the one-time setup
below is done **and** the repository variable `NUGET_PUBLISH` is `true`, the workflow only builds, tests and packs (the `publish` job is skipped).

## One-time setup (maintainer)

1. **nuget.org account and package ownership.** Sign in at https://www.nuget.org with the organisation account (or create an organisation
   and add the maintainers). The first push of `StampMitra` and `StampMitra.Extensions.DependencyInjection` makes that account/organisation the owner.
2. **Reserve the ID prefix `StampMitra`.** Prefix reservation stops anyone else publishing `StampMitra.*` packages and gives
   ours the verified-owner mark. Request it from the nuget.org account menu ("Prefix reservation" / "Request ID prefix reservation",
   https://learn.microsoft.com/nuget/nuget-org/id-prefix-reservation) for the pattern `StampMitra` (matches `StampMitra` and `StampMitra.*`),
   explaining that it is the StampMitra developer SDK; the nuget.org team reviews it and notifies you by email. Do this **before** the first
   publish if you can: a prefix cannot be reserved for IDs that other accounts already own.
3. **Trusted publishing policy.** On nuget.org open your username > *Trusted Publishing* and add a policy: repository owner
   `Bani-Global-Industries-LLP`, repository `stampmitra-developer-dotnet`, workflow file `release.yml`, no environment (or the environment
   you add to the workflow). Policies are per package owner; one policy covers both packages in this repository. If nuget.org does not
   allow a policy to create a package ID that does not exist yet, publish `0.1.0` once manually
   (`dotnet nuget push` with a short-lived, glob-scoped API key you revoke immediately) and use trusted publishing from then on.
4. **Repository settings** (Settings > Secrets and variables > Actions > Variables):
   - `NUGET_USER` = the nuget.org *profile name* (not an email) that owns the policy.
   - `NUGET_PUBLISH` = `true` (set it last; this arms the publish job).
5. Optionally protect the `v*` tag pattern (Settings > Rules) and require approval via a `nuget` environment.

## Per release

1. Move the *Unreleased* notes into a new section in `CHANGELOG.md`.
2. Bump `<Version>` in both `src/*/*.csproj` and `StampMitraClient.Version` in `src/StampMitra/StampMitraClient.cs`
   (the workflow fails if the tag, the package versions and the constant disagree).
3. `git tag v0.1.0 && git push origin v0.1.0`.
4. The workflow runs the full suite, packs `.nupkg` + `.snupkg` (symbols, SourceLink), attaches them to a GitHub release and, when armed,
   pushes them to nuget.org with a one-hour token minted by `NuGet/login`.

`dotnet pack` output is deterministic (`ContinuousIntegrationBuild` is set on GitHub Actions) so the published binaries can be reproduced from the tag.
