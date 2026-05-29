#!/bin/bash
# Install pre-commit hook for CSharpier formatting enforcement.
# Run once per clone: bash scripts/install-hooks.sh

set -e

HOOK_SRC="$(dirname "$0")/../.git/hooks/pre-commit"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TARGET="$REPO_ROOT/.git/hooks/pre-commit"

if [ ! -f "$TARGET" ]; then
    echo "Installing pre-commit hook..."
    cat > "$TARGET" << 'HOOK'
#!/bin/bash
# Pre-commit hook: run CSharpier on staged .cs files

set -e

if ! dotnet csharpier --version &>/dev/null; then
    echo "[pre-commit] WARNING: CSharpier not found. Install: dotnet tool install -g csharpier"
    exit 0
fi

staged=$(git diff --cached --name-only --diff-filter=ACMR -- '*.cs')
if [ -z "$staged" ]; then
    exit 0
fi

echo "[pre-commit] Formatting staged .cs files..."
echo "$staged" | while IFS= read -r f; do echo "  $f"; done
dotnet csharpier $staged 2>/dev/null || true
echo "$staged" | xargs git add
echo "[pre-commit] Done."
exit 0
HOOK
    chmod +x "$TARGET"
    echo "Pre-commit hook installed."
else
    echo "Pre-commit hook already exists."
fi
