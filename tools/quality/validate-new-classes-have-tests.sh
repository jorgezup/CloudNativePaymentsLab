#!/usr/bin/env bash
set -euo pipefail

# Validates that newly added production classes have a matching test file.
# This is intentionally convention-based for the POC: ClassName.cs expects
# tests/**/ClassNameTests.cs.

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

declare -A added_files=()

collect_added_files() {
    local diff_args=("$@")

    while IFS=$'\t' read -r status path; do
        if [[ "$status" == "A" && "$path" == src/*.cs ]]; then
            added_files["$path"]=1
        fi
    done < <(git diff --name-status --diff-filter=A "${diff_args[@]}")
}

# Pre-commit path: staged files.
collect_added_files --cached

# Manual/pre-push safety net: uncommitted added files.
collect_added_files

# Pre-push path: files added in commits not present upstream.
if git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' >/dev/null 2>&1; then
    upstream="$(git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}')"
    merge_base="$(git merge-base HEAD "$upstream")"
    collect_added_files "$merge_base"..HEAD
fi

is_ignored_file() {
    local path="$1"
    local file_name
    file_name="$(basename "$path")"

    [[ "$file_name" == "Program.cs" ]] && return 0
    [[ "$path" == *"/Migrations/"* ]] && return 0
    [[ "$path" == *"/Properties/"* ]] && return 0
    [[ "$file_name" =~ (Dto|DTO|Request|Response|Options|Constants|Configuration|Settings)\.cs$ ]] && return 0
    [[ "$file_name" =~ (DependencyInjection|ServiceCollectionExtensions|Endpoint|Endpoints)\.cs$ ]] && return 0
    [[ "$file_name" =~ ^I[A-Z].*\.cs$ ]] && return 0

    return 1
}

declares_relevant_type() {
    local path="$1"
    local class_name
    class_name="$(basename "$path" .cs)"

    grep -Eq "(class|record|struct)[[:space:]]+$class_name([[:space:]]|[:({<])" "$path"
}

missing_tests=()

for path in "${!added_files[@]}"; do
    if is_ignored_file "$path"; then
        continue
    fi

    if ! declares_relevant_type "$path"; then
        continue
    fi

    class_name="$(basename "$path" .cs)"
    expected_test="${class_name}Tests.cs"

    if ! find tests -type f -name "$expected_test" | grep -q .; then
        missing_tests+=("$path -> tests/**/$expected_test")
    fi
done

if (( ${#missing_tests[@]} > 0 )); then
    echo "Quality gate failed: new relevant production classes need matching tests."
    echo
    printf 'Missing test: %s\n' "${missing_tests[@]}"
    echo
    echo "Create the expected test file or update tools/quality/validate-new-classes-have-tests.sh if this class is intentionally ignored."
    exit 1
fi

echo "Quality gate passed: new relevant production classes have matching tests."
