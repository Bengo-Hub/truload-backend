#!/usr/bin/env bash
# =============================================================================
# TruLoad Backend - Test Runner
# =============================================================================
# Run tests separately from the main build to keep local builds fast.
# Usage:
#   ./scripts/run_tests.sh [options]
#
# Options:
#   --unit              Run unit tests only
#   --integration       Run integration tests only
#   --e2e               Run end-to-end tests only
#   --coverage          Generate coverage report
#   --no-build          Skip test project build (assumes already built)
#   --watch             Run in watch mode
#   --help              Show this help message
#
# Environment Variables:
#   BUILD_CONFIGURATION Default: Release (Debug for --watch)
#   VERBOSITY          Default: normal (quiet, minimal, normal, detailed, diagnostic)
#
# Examples:
#   ./scripts/run_tests.sh                  # Run all tests
#   ./scripts/run_tests.sh --unit           # Run only unit tests
#   ./scripts/run_tests.sh --watch          # Run in watch mode
#   ./scripts/run_tests.sh --coverage       # Generate coverage report
# =============================================================================

set -euo pipefail

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

info() { echo -e "${BLUE}[INFO]${NC} $1"; }
success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Defaults
BUILD_CONFIGURATION=${BUILD_CONFIGURATION:-"Release"}
VERBOSITY=${VERBOSITY:-"normal"}
RUN_UNIT=true
RUN_INTEGRATION=false
RUN_E2E=false
GENERATE_COVERAGE=false
SKIP_BUILD=false
WATCH_MODE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --unit)
      RUN_UNIT=true
      RUN_INTEGRATION=false
      RUN_E2E=false
      shift
      ;;
    --integration)
      RUN_UNIT=false
      RUN_INTEGRATION=true
      RUN_E2E=false
      shift
      ;;
    --e2e)
      RUN_UNIT=false
      RUN_INTEGRATION=false
      RUN_E2E=true
      shift
      ;;
    --coverage)
      GENERATE_COVERAGE=true
      shift
      ;;
    --no-build)
      SKIP_BUILD=true
      shift
      ;;
    --watch)
      WATCH_MODE=true
      BUILD_CONFIGURATION="Debug"
      shift
      ;;
    --help)
      grep '^#' "$0" | grep -v '#!/' | sed 's/^# //'
      exit 0
      ;;
    *)
      error "Unknown option: $1"
      exit 1
      ;;
  esac
done

info "Test runner configuration:"
info "  Build configuration: ${BUILD_CONFIGURATION}"
info "  Unit tests: ${RUN_UNIT}"
info "  Integration tests: ${RUN_INTEGRATION}"
info "  E2E tests: ${RUN_E2E}"
info "  Coverage: ${GENERATE_COVERAGE}"
info "  Watch mode: ${WATCH_MODE}"

TEST_PROJECT="Tests/truload-backend.Tests.csproj"

if [[ ! -f "$TEST_PROJECT" ]]; then
  error "Test project not found: $TEST_PROJECT"
  exit 1
fi

cd "$(dirname "$0")/.."

# Build test project if not skipped
if [[ "$SKIP_BUILD" == "false" ]]; then
  info "Building test project..."
  dotnet build "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" --no-restore
  success "Test project built successfully"
fi

# Run tests
RUN_TESTS=false

if [[ "$RUN_UNIT" == "true" ]]; then
  RUN_TESTS=true
  info "Running unit tests..."
  if [[ "$WATCH_MODE" == "true" ]]; then
    dotnet watch test --project "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" \
      --filter "Category=Unit" --verbosity "$VERBOSITY" || warn "Some unit tests failed"
  else
    dotnet test "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" \
      --filter "Category=Unit" --verbosity "$VERBOSITY" \
      --no-build --logger "console;verbosity=detailed" || warn "Some unit tests failed"
  fi
fi

if [[ "$RUN_INTEGRATION" == "true" ]]; then
  RUN_TESTS=true
  info "Running integration tests..."
  if [[ "$WATCH_MODE" == "true" ]]; then
    warn "Watch mode not supported for integration tests"
  else
    dotnet test "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" \
      --filter "Category=Integration" --verbosity "$VERBOSITY" \
      --no-build --logger "console;verbosity=detailed" || warn "Some integration tests failed"
  fi
fi

if [[ "$RUN_E2E" == "true" ]]; then
  RUN_TESTS=true
  info "Running e2e tests..."
  if [[ "$WATCH_MODE" == "true" ]]; then
    warn "Watch mode not supported for e2e tests"
  else
    dotnet test "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" \
      --filter "Category=E2E" --verbosity "$VERBOSITY" \
      --no-build --logger "console;verbosity=detailed" || warn "Some e2e tests failed"
  fi
fi

# If no specific test category was requested, run all tests
if [[ "$RUN_TESTS" == "false" ]]; then
  info "Running all tests..."
  if [[ "$WATCH_MODE" == "true" ]]; then
    dotnet watch test --project "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" \
      --verbosity "$VERBOSITY" || warn "Some tests failed"
  else
    COVERAGE_ARGS=""
    if [[ "$GENERATE_COVERAGE" == "true" ]]; then
      COVERAGE_ARGS="--collect:\"XPlat Code Coverage\""
    fi

    dotnet test "$TEST_PROJECT" -c "$BUILD_CONFIGURATION" \
      --verbosity "$VERBOSITY" \
      --no-build \
      --logger "console;verbosity=detailed" \
      $COVERAGE_ARGS || warn "Some tests failed"
  fi
fi

if [[ "$GENERATE_COVERAGE" == "true" ]]; then
  COVERAGE_DIR="Tests/TestResults"
  if [[ -d "$COVERAGE_DIR" ]]; then
    success "Coverage report generated in $COVERAGE_DIR"
    info "To view coverage report, open:"
    find "$COVERAGE_DIR" -name "coverage.cobertura.xml" -o -name "index.html" | head -3
  fi
fi

success "Test run complete"
