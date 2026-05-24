#!/usr/bin/env bash
set -euo pipefail

# Runs unit tests with XPlat Code Coverage and fails when line coverage is
# below the configured threshold. Override with COVERAGE_THRESHOLD=85 if needed.

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

project="tests/UnitTests/CloudNativePaymentsLab.UnitTests.csproj"
results_dir="TestResults/Coverage/Unit"
threshold="${COVERAGE_THRESHOLD:-80}"

rm -rf "$results_dir"
mkdir -p "$results_dir"

dotnet test "$project" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$results_dir" \
    --settings tools/quality/unit.coverlet.runsettings

coverage_file="$(find "$results_dir" -name 'coverage.cobertura.xml' -print -quit)"

if [[ -z "${coverage_file:-}" ]]; then
    echo "Quality gate failed: coverage file was not generated for unit tests."
    exit 1
fi

tools/quality/validate-coverage-threshold.sh "$coverage_file" "$threshold" "unit tests"

if dotnet tool list | grep -q '^dotnet-reportgenerator-globaltool'; then
    dotnet tool run reportgenerator \
        "-reports:$coverage_file" \
        "-targetdir:$results_dir/Report" \
        "-reporttypes:TextSummary;HtmlInline_AzurePipelines"
    echo "Unit coverage report generated at $results_dir/Report."
else
    echo "ReportGenerator is not installed. Run: dotnet tool restore"
fi
