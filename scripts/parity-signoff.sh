#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
JAR_PATH="${BASICPROP_JAR:-/home/simon/temp/BasicProp/basicProp-1.3.jar}"
PROBE_DIR="$ROOT_DIR/tools/basicprop-probe"
PROBE_SRC="$PROBE_DIR/BasicPropProbe.java"
PUBLISH_DIR="$ROOT_DIR/artifacts/signalweave-desktop-linux-x64"

echo "[1/5] Building solution"
dotnet build "$ROOT_DIR/SignalWeave.sln"

echo "[2/5] Running core parity tests"
dotnet test "$ROOT_DIR/tests/SignalWeave.Core.Tests/SignalWeave.Core.Tests.csproj"

if [[ ! -f "$JAR_PATH" ]]; then
    echo "BasicProp JAR not found at $JAR_PATH" >&2
    exit 1
fi

echo "[3/5] Compiling BasicProp probe"
javac -cp "$JAR_PATH" "$PROBE_SRC"

echo "[4/5] Running checked-in probe experiments"
while IFS= read -r probe_file; do
    echo "  - $(basename "$probe_file")"
    java -cp "$JAR_PATH:$PROBE_DIR" BasicPropProbe run "$probe_file" > /dev/null
done < <(find "$PROBE_DIR/examples" -maxdepth 1 -name '*.bppr' | sort)

echo "[5/5] Publishing Linux desktop bundle"
rm -rf "$PUBLISH_DIR"
dotnet publish \
    "$ROOT_DIR/src/SignalWeave.Desktop/SignalWeave.Desktop.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=false \
    -o "$PUBLISH_DIR"

echo
echo "Parity sign-off completed successfully."
echo "Published desktop bundle: $PUBLISH_DIR"
