#!/bin/sh
set -e

dotnet test --filter Fast=Fast --no-build
dotnet test --filter Integration=Integration --no-build -v n
echo "Running external tests set to: $TESTS_RUN_EXTERNAL_INTEGRATION"

if [[ "$TESTS_RUN_EXTERNAL_INTEGRATION" == "true" ]]; then
    dotnet test --filter ExternalIntegration=ExternalIntegration --no-build -v n
fi
