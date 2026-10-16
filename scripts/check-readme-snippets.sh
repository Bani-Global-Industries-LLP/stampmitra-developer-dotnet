#!/usr/bin/env bash
# Fails when a README.md ```csharp block is not mirrored (and therefore compiled) in tests/StampMitra.Tests/ReadmeSnippets.cs.
# Run it after editing README.md; also run by CI as part of the normal test suite.
set -euo pipefail
cd "$(dirname "$0")/.."
exec dotnet test tests/StampMitra.Tests --configuration Release --framework net8.0 --filter "FullyQualifiedName~ReadmeSnippetsTests"
