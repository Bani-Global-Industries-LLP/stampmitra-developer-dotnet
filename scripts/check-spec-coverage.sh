#!/usr/bin/env bash
# Fails (exit 1) when an operation in spec/openapi.json has no SDK method, or the SDK calls a route that is not in the spec.
# Usage: scripts/check-spec-coverage.sh   (needs the .NET SDK)
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test tests/StampMitra.Tests --framework net8.0 --filter "Category=SpecCoverage" --logger "console;verbosity=normal"
