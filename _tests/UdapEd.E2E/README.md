# UdapEd end-to-end tests

Playwright tests that drive the UdapEd UI through real UDAP flows against the deployed FhirLabs servers
(fhirlabs.net FHIR proxy, securedcontrols.net authorization server, idp1.securedcontrols.net).

| Test | Flow |
|---|---|
| `ClientCredentialsTests.Register_And_Get_Access_Token` | discovery → DCR for `client_credentials` → token request |

The tests are skipped unless `UDAPED_URL` is set, so `dotnet test UdapEd.sln` never touches the live servers by accident.

## One-time setup

```bash
dotnet build _tests/UdapEd.E2E
pwsh _tests/UdapEd.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
```

## Run against a local UdapEd

```bash
# terminal 1
cd Server
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7041 dotnet run --no-launch-profile

# terminal 2
UDAPED_URL=https://localhost:7041 dotnet test _tests/UdapEd.E2E
```

PowerShell: `$env:UDAPED_URL="https://localhost:7041"; dotnet test _tests/UdapEd.E2E`

## Run against the deployed UdapEd

```bash
UDAPED_URL=https://fhirlabs-udaped-v46zp6zteq-uw.a.run.app dotnet test _tests/UdapEd.E2E
```

The tests find UI elements by their `data-testid` attributes, so the deployed UdapEd must include them
(any build after the commit that added them).

## Settings

| Variable | Default | Meaning |
|---|---|---|
| `UDAPED_URL` | *(none: tests skip)* | UdapEd under test |
| `UDAPED_FHIR_BASE_URL` | `https://fhirlabs.net/fhir/r4` | FHIR server whose UDAP metadata starts each flow |
| `UDAPED_COMMUNITY` | *(server default)* | UDAP community to query |
| `UDAPED_CLIENT_CERT` | `FhirLabs Community` | UdapEd pre-packaged client certificate |
| `UDAPED_CC_SAN` | `https://fhirlabs.net:7016/fhir/r4` | subject alt name for the client_credentials registration |
| `UDAPED_HEADED` | `0` | `1` shows the browser |
| `UDAPED_SLOWMO` | `0` | milliseconds between browser actions, for watching a run |

Registering again with the same subject alt name in the same community updates the existing client on the
authorization server instead of creating another, so repeated runs do not accumulate registrations.

## When a test fails

The Playwright trace (screenshots, DOM snapshots, network) is written to
`bin/Debug/net10.0/TestResults/traces/<test>-<time>.zip` and its path is printed in the test output. Open it with:

```bash
pwsh _tests/UdapEd.E2E/bin/Debug/net10.0/playwright.ps1 show-trace <path-to-zip>
```
