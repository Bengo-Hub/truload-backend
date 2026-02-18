# =============================================================================
# TruLoad Backend - Test Runner (PowerShell)
# =============================================================================
# Run tests separately from the main build to keep local builds fast.
# Usage:
#   .\scripts\run_tests.ps1 [options]
#
# Options:
#   -Unit              Run unit tests only
#   -Integration       Run integration tests only
#   -E2E               Run end-to-end tests only
#   -Coverage          Generate coverage report
#   -NoBuild           Skip test project build (assumes already built)
#   -Watch             Run in watch mode
#
# Environment Variables:
#   BUILD_CONFIGURATION Default: Release (Debug for -Watch)
#   VERBOSITY          Default: normal (quiet, minimal, normal, detailed, diagnostic)
#
# Examples:
#   .\scripts\run_tests.ps1                  # Run all tests
#   .\scripts\run_tests.ps1 -Unit            # Run only unit tests
#   .\scripts\run_tests.ps1 -Watch           # Run in watch mode
#   .\scripts\run_tests.ps1 -Coverage        # Generate coverage report
# =============================================================================

param(
    [switch]$Unit,
    [switch]$Integration,
    [switch]$E2E,
    [switch]$Coverage,
    [switch]$NoBuild,
    [switch]$Watch
)

$ErrorActionPreference = "Stop"

# Color output
function Write-Info { Write-Host "[INFO] $args" -ForegroundColor Blue }
function Write-Success { Write-Host "[SUCCESS] $args" -ForegroundColor Green }
function Write-Warn { Write-Host "[WARN] $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "[ERROR] $args" -ForegroundColor Red }

# Defaults
$BuildConfiguration = if ($env:BUILD_CONFIGURATION) { $env:BUILD_CONFIGURATION } else { "Release" }
$Verbosity = if ($env:VERBOSITY) { $env:VERBOSITY } else { "normal" }
$RunUnit = -not $Integration -and -not $E2E
$RunIntegration = $Integration
$RunE2E = $E2E
$GenerateCoverage = $Coverage
$SkipBuild = $NoBuild
$WatchMode = $Watch

if ($WatchMode) {
    $BuildConfiguration = "Debug"
}

Write-Info "Test runner configuration:"
Write-Info "  Build configuration: $BuildConfiguration"
Write-Info "  Unit tests: $RunUnit"
Write-Info "  Integration tests: $RunIntegration"
Write-Info "  E2E tests: $RunE2E"
Write-Info "  Coverage: $GenerateCoverage"
Write-Info "  Watch mode: $WatchMode"

$TestProject = "Tests/truload-backend.Tests.csproj"

if (-not (Test-Path $TestProject)) {
    Write-Error "Test project not found: $TestProject"
    exit 1
}

# Change to script directory (project root)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Split-Path -Parent $ScriptDir)

# Build test project if not skipped
if (-not $SkipBuild) {
    Write-Info "Building test project..."
    & dotnet build $TestProject -c $BuildConfiguration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build test project"
        exit 1
    }
    Write-Success "Test project built successfully"
}

# Run tests
$RunTests = $false

if ($RunUnit) {
    $RunTests = $true
    Write-Info "Running unit tests..."
    if ($WatchMode) {
        & dotnet watch test --project $TestProject -c $BuildConfiguration `
            --filter "Category=Unit" --verbosity $Verbosity
    } else {
        & dotnet test $TestProject -c $BuildConfiguration `
            --filter "Category=Unit" --verbosity $Verbosity `
            --no-build --logger "console;verbosity=detailed"
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "Some unit tests failed"
    }
}

if ($RunIntegration) {
    $RunTests = $true
    Write-Info "Running integration tests..."
    if ($WatchMode) {
        Write-Warn "Watch mode not supported for integration tests"
    } else {
        & dotnet test $TestProject -c $BuildConfiguration `
            --filter "Category=Integration" --verbosity $Verbosity `
            --no-build --logger "console;verbosity=detailed"
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Some integration tests failed"
        }
    }
}

if ($RunE2E) {
    $RunTests = $true
    Write-Info "Running e2e tests..."
    if ($WatchMode) {
        Write-Warn "Watch mode not supported for e2e tests"
    } else {
        & dotnet test $TestProject -c $BuildConfiguration `
            --filter "Category=E2E" --verbosity $Verbosity `
            --no-build --logger "console;verbosity=detailed"
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Some e2e tests failed"
        }
    }
}

# If no specific test category was requested, run all tests
if (-not $RunTests) {
    Write-Info "Running all tests..."
    if ($WatchMode) {
        & dotnet watch test --project $TestProject -c $BuildConfiguration --verbosity $Verbosity
    } else {
        $CoverageArgs = @()
        if ($GenerateCoverage) {
            $CoverageArgs = '--collect:XPlat Code Coverage'
        }

        & dotnet test $TestProject -c $BuildConfiguration `
            --verbosity $Verbosity `
            --no-build `
            --logger "console;verbosity=detailed" `
            @CoverageArgs
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Some tests failed"
        }
    }
}

if ($GenerateCoverage) {
    $CoverageDir = "Tests/TestResults"
    if (Test-Path $CoverageDir) {
        Write-Success "Coverage report generated in $CoverageDir"
        Write-Info "To view coverage report, open:"
        Get-ChildItem -Path $CoverageDir -Recurse -Include "coverage.cobertura.xml", "index.html" | ForEach-Object { Write-Info "  $_" }
    }
}

Write-Success "Test run complete"
