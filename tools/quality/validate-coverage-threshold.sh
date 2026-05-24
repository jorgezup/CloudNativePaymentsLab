#!/usr/bin/env bash
set -euo pipefail

# Reads Cobertura XML produced by coverlet.collector and validates line-rate.

coverage_file="${1:?coverage file is required}"
threshold="${2:?coverage threshold is required}"
label="${3:-tests}"

if [[ ! -f "$coverage_file" ]]; then
    echo "Quality gate failed: coverage file not found: $coverage_file"
    exit 1
fi

line_rate="$(grep -o 'line-rate="[0-9.]*"' "$coverage_file" | head -n 1 | cut -d '"' -f 2)"

if [[ -z "$line_rate" ]]; then
    echo "Quality gate failed: could not read line-rate from $coverage_file."
    exit 1
fi

coverage_percent="$(awk -v rate="$line_rate" 'BEGIN { printf "%.2f", rate * 100 }')"
passes_threshold="$(awk -v coverage="$coverage_percent" -v threshold="$threshold" 'BEGIN { print (coverage >= threshold) ? "yes" : "no" }')"

if [[ "$passes_threshold" != "yes" ]]; then
    echo "Quality gate failed: $label line coverage is ${coverage_percent}%, below the required ${threshold}%."
    exit 1
fi

echo "Quality gate passed: $label line coverage is ${coverage_percent}%."
