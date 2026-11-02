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

## The E2E client certificate

The tests register with their own FhirLabs client certificate, issued by the SureFhirLabs CA through UdapEd's
"Build FhirLabs Community Certificates" (`/Infrastructure/BuildMyTestCertificatePackage`). It carries one subject
alt name per flow, so each flow has its own registration and none overwrites another's (or anyone's manual ones):

- `https://fhirlabs.net/udaped-e2e/client-credentials`
- `https://fhirlabs.net/udaped-e2e/authorization-code`
- `https://fhirlabs.net/udaped-e2e/tiered-oauth`

It is issued automatically the first time a test needs it (or when it is within 30 days of expiry or its SANs no
longer match), by the deployed UdapEd, which can read the CA from GCP. It is saved outside the repository, by default
`%LOCALAPPDATA%\UdapEd.E2E\udaped-e2e-client.p12`, and uploaded into the UdapEd under test with the Upload button
and password dialog. Delete the file to get a fresh one.

Because the certificate chains to the SureFhirLabs CA, discovery uses the `udap://fhirlabs.net/` community;
fhirlabs.net's default community is a different one.

Registering again with the same subject alt name in the same community updates the existing client on the
authorization server instead of creating another, so repeated runs do not accumulate registrations.

## Settings

| Variable | Default | Meaning |
|---|---|---|
| `UDAPED_URL` | *(none: tests skip)* | UdapEd under test |
| `UDAPED_FHIR_BASE_URL` | `https://fhirlabs.net/fhir/r4` | FHIR server whose UDAP metadata starts each flow |
| `UDAPED_COMMUNITY` | `udap://fhirlabs.net/` | UDAP community to discover |
| `UDAPED_CC_SAN` | `https://fhirlabs.net/udaped-e2e/client-credentials` | SAN for the client_credentials registration |
| `UDAPED_CODE_SAN` | `https://fhirlabs.net/udaped-e2e/authorization-code` | SAN for the authorization_code registration |
| `UDAPED_TIERED_SAN` | `https://fhirlabs.net/udaped-e2e/tiered-oauth` | SAN for the Tiered OAuth registration |
| `UDAPED_E2E_CERT` | `%LOCALAPPDATA%\UdapEd.E2E\udaped-e2e-client.p12` | where the E2E certificate is kept |
| `UDAPED_E2E_CERT_PASSWORD` | `udap-test` | its password |
| `UDAPED_CERT_ISSUER_URL` | the deployed UdapEd | UdapEd that issues the E2E certificate |
| `UDAPED_HEADED` | `0` | `1` shows the browser |
| `UDAPED_SLOWMO` | `0` | milliseconds between browser actions, for watching a run |

## When a test fails

The Playwright trace (screenshots, DOM snapshots, network) is written to
`bin/Debug/net10.0/TestResults/traces/<test>-<time>.zip` and its path is printed in the test output. Open it with:

```bash
pwsh _tests/UdapEd.E2E/bin/Debug/net10.0/playwright.ps1 show-trace <path-to-zip>
```
