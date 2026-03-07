# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## About This Project

UdapEd is a multi-platform educational and testing tool for **UDAP (Unified Data Access and Protection)** and **FHIR (Fast Healthcare Interoperability Resources)** workflows. It implements the UDAP security specification for healthcare data exchange, including client certificate registration, authorization code flows, mTLS, and CDS Hooks.

## Build Commands

```bash
# Restore dependencies
dotnet restore UdapEd.sln

# Build entire solution
dotnet build UdapEd.sln

# Build specific project
dotnet build Server/UdapEd.Server.csproj

# Run the web server (Blazor WASM hosted)
cd Server && dotnet run

# Build MAUI app for Windows
dotnet build -c Release -f net9.0-windows10.0.19041.0 UdapEdAppMaui/UdapEdAppMaui.csproj
```

## Test Commands

```bash
# Run all tests
dotnet test UdapEd.sln

# Run a specific test project
dotnet test _tests/UtilityTests/UtilityTests.csproj

# Run a single test by name
dotnet test _tests/UtilityTests/UtilityTests.csproj --filter "FullyQualifiedName~TestName"
```

Test coverage uses Coverlet. MAUI tests use a visual runner via `Xunit.Runners.Maui` (see `_tests/UdapEdMauiTestHost/README.md`).

## Solution Structure

| Project | Framework | Role |
|---------|-----------|------|
| `Server/` | net9.0 | ASP.NET Core host; serves Blazor WASM and exposes proxy API endpoints |
| `Client/` | net9.0 | Blazor WebAssembly app; runs in browser |
| `Shared/` | net9.0 | Razor component library shared between Client, Server, and MAUI |
| `UdapEdAppMaui/` | net9.0-*platform* | .NET MAUI desktop/mobile app consuming Shared |
| `_tests/UtilityTests/` | net9.0 | XUnit tests for FHIR utilities, certificates, controllers |

## Architecture

### Hosting Model
The Server project hosts a Blazor WebAssembly app (Client). The Server also acts as a **backend proxy** for UDAP operations that require server-side secrets (certificates, token exchange), while the Client handles UI state and rendering. The MAUI app reuses the same Razor components from Shared.

### Service Layer (in `Shared/`)
All business logic lives behind interfaces in `Shared/`:
- `IAccessService` — authorization flows (auth code, client credentials, SMART)
- `IDiscoveryService` — UDAP discovery endpoint queries
- `IFhirService` — FHIR resource operations
- `ICertificationService` — client certificate management and UDAP registration
- `ICdsService` — CDS Hooks request/response
- `IMutualTlsService` — mTLS operations

Server and MAUI register different implementations of these interfaces (server-side HTTP vs. MAUI local implementations).

### State Management
`AppSharedState` (in Shared) holds application-wide UI state and is injected as a scoped service. `Blazored.LocalStorage` persists settings between sessions.

### Key UDAP Packages
The project consumes the `Udap.*` NuGet packages from the sibling `udap-tools` ecosystem:
- `Udap.Client` — UDAP discovery and dynamic client registration
- `Udap.Common`, `Udap.Model` — shared UDAP types and certificate utilities
- `Udap.Smart.Model` — SMART on FHIR model types
- `Udap.CdsHooks.Model` — CDS Hooks model types

### FHIR Integration
Uses **Firely** (`Hl7.Fhir.*`) for FHIR resource parsing, validation, and FhirPath evaluation. FHIR HTTP responses use a custom decompression handler and content negotiation headers.

### Cryptography
`BouncyCastle.Cryptography` is used for certificate operations (trust chain building, CRL checking). There is a `CrlCacheService` for caching certificate revocation lists.

### UI
- **MudBlazor** — primary component library for Blazor UI
- **Microsoft FluentUI** — secondary UI components
- **BlazorMonaco** — embedded Monaco editor for JSON/FHIR editing

## Important Configuration

- **`global.json`** — pins .NET SDK to `8.0.x` rollForward policy
- **`nuget.config`** — uses official NuGet.org feed only
- **`cloudbuild.yaml`** — GCP Cloud Run deployment (us-west1, max 1 instance)
- **`.github/workflows/`** — CI runs on Windows runners; release workflow pushes Docker image to GHCR
- User secrets are configured per-project via `UserSecretsId` in csproj files for local development credentials

## Docker

```bash
# Build using Server Dockerfile
docker build -f Server/Dockerfile -t udaped .

# GCP variant
docker build -f Server/Dockerfile.gcp -t udaped-gcp .
```

The Server Dockerfile uses a multi-stage build: SDK image for build, `aspnet:9.0` for runtime. Exposes ports 8080 and 8081.
