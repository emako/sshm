#!/usr/bin/env bash
# Publish Sshm as Native AOT self-contained single-file executables (Linux/macOS).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/Sshm/Sshm.csproj"
OUTPUT_ROOT="${OUTPUT_ROOT:-$ROOT/publish}"
TARGET="${1:-all}"

ALL_TARGETS=(win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64)

get_native_aot_rids() {
    local os arch
    os="$(uname -s)"
    arch="$(uname -m)"
    case "$os" in
        Linux)
            case "$arch" in
                x86_64|amd64) echo "linux-x64 linux-arm64" ;;
                aarch64|arm64) echo "linux-arm64 linux-x64" ;;
                *) echo "linux-$arch" ;;
            esac
            ;;
        Darwin)
            case "$arch" in
                x86_64) echo "osx-x64 osx-arm64" ;;
                arm64|aarch64) echo "osx-arm64 osx-x64" ;;
                *) echo "osx-$arch" ;;
            esac
            ;;
        MINGW*|MSYS*|CYGWIN*)
            case "$arch" in
                x86_64|amd64) echo "win-x64" ;;
                aarch64|arm64) echo "win-arm64 win-x64" ;;
                *) echo "win-$arch" ;;
            esac
            ;;
        *)
            echo ""
            ;;
    esac
}

parse_targets() {
    local input=() part item
    IFS=',' read -ra parts <<< "${TARGET// /,}"
    for part in "${parts[@]}"; do
        part="$(echo "$part" | xargs)"
        [[ -n "$part" ]] && input+=("$part")
    done

    if ((${#input[@]} == 0)); then
        echo "No publish target specified." >&2
        exit 1
    fi

    if [[ "${input[0]}" == "all" && ${#input[@]} -eq 1 ]]; then
        printf '%s\n' "${ALL_TARGETS[@]}"
        return
    fi

    printf '%s\n' "${input[@]}"
}

remove_unused_publish_artifacts() {
    local output_dir="$1"
    local name path
    for name in libonigwrap.dll libonigwrap.so libonigwrap.dylib; do
        path="$output_dir/$name"
        if [[ -f "$path" ]]; then
            rm -f "$path"
            echo "Removed unused artifact: $path"
        fi
    done
}

    case "$1" in
        win-*) echo "sshm.exe" ;;
        *) echo "sshm" ;;
    esac
}

BUILDABLE="$(get_native_aot_rids)"
mapfile -t TARGETS < <(parse_targets)

for runtime in "${TARGETS[@]}"; do
    supported=false
    for rid in $BUILDABLE; do
        if [[ "$runtime" == "$rid" ]]; then
            supported=true
            break
        fi
    done
    if [[ "$supported" != true ]]; then
        echo "Native AOT cannot cross-compile to: $runtime" >&2
        echo "Build this target on the matching OS. This machine can AOT publish: $BUILDABLE" >&2
        exit 1
    fi
done

PUBLISHED=()
for runtime in "${TARGETS[@]}"; do
    output_dir="$OUTPUT_ROOT/$runtime"
    mkdir -p "$output_dir"

    echo
    echo "==> Publishing Native AOT $runtime to: $output_dir"

    args=(
        publish "$PROJECT"
        -c Release
        -r "$runtime"
        -p:PublishAot=true
        -p:PublishSingleFile=true
        -p:IncludeNativeLibrariesForSelfExtract=true
        -p:DebugType=none
        -p:DebugSymbols=false
        -o "$output_dir"
    )

    if [[ "${NO_COMPRESSION:-0}" != "1" ]]; then
        args+=(-p:EnableCompressionInSingleFile=true)
    fi

    dotnet "${args[@]}"

    output_name="$(publish_output_name "$runtime")"
    output_path="$output_dir/$output_name"
    if [[ ! -f "$output_path" ]]; then
        echo "Expected output not found: $output_path" >&2
        exit 1
    fi

    remove_unused_publish_artifacts "$output_dir"

    size_mb="$(du -m "$output_path" | cut -f1)"
    echo "Done: $output_path (${size_mb} MB)"
    PUBLISHED+=("$output_path")
done

echo
echo "Published ${#PUBLISHED[@]} Native AOT target(s):"
for path in "${PUBLISHED[@]}"; do
    echo "  $path"
done
