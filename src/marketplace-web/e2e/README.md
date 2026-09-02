# Staging Smoke / E2E suite (Playwright)

Această suită rulează după fiecare deployment pe **Staging** și validează că aplicația
deployată este complet funcțională:

1. **Golden path** — se navighează la `STAGING_URL`, se face login cu un cont de test și
   se verifică că aplicația se încarcă fără erori în consolă (`e2e/golden-path.spec.ts`).
2. **Integrare externă SEAP + PostgreSQL** — se rulează fluxul „Caută licitații (Suceava)"
   (filtru județ **Suceava** + **Caută în SEAP** în UI-ul actual) și se așteaptă ca
   „tabelul" de rezultate să afișeze **măcar un rezultat**. Asta confirmă conectivitatea
   reală a serverului către e-licitatie.ro (SEAP) și citirea din baza PostgreSQL
   (`e2e/seap-search.spec.ts`).

## Variabile de mediu obligatorii

| Variabilă | Descriere | Exemplu |
|---|---|---|
| `STAGING_URL` | URL-ul de bază al aplicației Staging | `http://3.64.174.227:8081` |
| `E2E_TEST_EMAIL` | E-mailul contului de test din Staging | `smoke@compaspot.test` |
| `E2E_TEST_PASSWORD` | Parola contului de test din Staging | `…` |

`playwright.config.ts` eșuează imediat cu un mesaj clar dacă vreuna lipsește.

## Instalare

```bash
pnpm install            # instalează @playwright/test (devDependency)
pnpm run test:e2e:install   # descarcă browserul Chromium (doar prima dată / pe runner-ul CI)
```

## Rulare

```bash
# PowerShell (Windows)
$env:STAGING_URL = 'http://3.64.174.227:8081'
$env:E2E_TEST_EMAIL = 'smoke@compaspot.test'
$env:E2E_TEST_PASSWORD = '<parola>'
pnpm run test:e2e:staging

# bash / GitHub Actions
STAGING_URL="http://3.64.174.227:8081" \
E2E_TEST_EMAIL="smoke@compaspot.test" \
E2E_TEST_PASSWORD="<parola>" \
pnpm run test:e2e:staging
```

Scriptul din `package.json` este pur `playwright test --config=playwright.config.ts`;
environment-ul de Staging este țintit prin `STAGING_URL` + credențialele de test.

## Prerechizite pe Staging

- **Cont de test cu o firmă înregistrată** pentru fluxul complet „Suceava": editorul de
  filtre avansate pe județe este vizibil doar pentru utilizatorii cu companie. Fără
  companie, testul SEAP rulează o căutare live nefiltrată (validează tot SEAP + PostgreSQL)
  și marchează acest lucru cu o notă în raport.
- **Căutarea live în SEAP** trebuie să ajungă la `https://e-licitatie.ro` din containerul
  API (config `Seap__BaseUrl`, implicit corect la deploy) — fără un fallback local.
- Pagina `/seap/tenders` necesită autentificare; API-ul `GET /api/v1/seap/tenders` nu este
  gate-uit pe feature flags, deci golden-path-ul funcționează indiferent de `SeapIngestionEnabled`.

## Integrarea în deploy

`.github/workflows/deploy.yml` conține un pas `Run Staging smoke tests (Playwright E2E)`
care rulează suita doar când `inputs.environment == 'staging'`, folosind portul 8081
deschis temporar către IP-ul runner-ului. Credențialele contului de test trebuie definite
ca GitHub **secrets**: `E2E_TEST_EMAIL` și `E2E_TEST_PASSWORD`.

## Artefacte

Rezultatele apar în consolă (`list` reporter) și într-un raport HTML local
(`playwright-report/`, ignorat în git). Pe eșec se rețin screenshot + video.
