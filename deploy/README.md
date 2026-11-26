# PrIMA On-Premise Production Deployment Packaging Bundle

This packaging bundle provides the complete set of infrastructure templates, reverse-proxy configurations, service management scripts, and database administration assets required to deploy **PrIMA (Proactive Invariant Monitoring Assistant)** in hospital on-premise environments (`DEC-008`, `DEC-009`).

---

## 1. Bundle Directory Structure

```
deploy/
├── appsettings.Production.json          # Production configuration template
├── README.md                            # This deployment and operations guide
├── docker-compose.prod.yml              # Turnkey multi-container composition
├── docker/
│   ├── Dockerfile.api                   # Multi-stage Dockerfile for PrIMA API
│   ├── Dockerfile.detectors             # Multi-stage Dockerfile for Detector Runner
│   └── Dockerfile.frontend              # Multi-stage Dockerfile for Vue 3 SPA + Nginx
├── iis/
│   └── web.config                       # Windows Server IIS URL Rewrite & Reverse Proxy
├── nginx/
│   └── prima.conf                       # Production Nginx reverse-proxy & TLS config
├── sql/
│   ├── provision-simrs-readonly-login.sql # DBA script for read-only user provisioning
│   └── maintenance-retention-pruning.sql  # SQL Server Agent daily pruning job
└── windows-service/
    └── install-prima-service.ps1        # PowerShell service installer with crash recovery
```

---

## 2. Pre-Deployment: DBA SIMRS Read-Only Provisioning (`DEC-008`, `BR-PRIMA-007`)

Before starting the PrIMA API or Detector runners, the hospital DBA must provision the dedicated read-only database login:

1. Open `deploy/sql/provision-simrs-readonly-login.sql` in SQL Server Management Studio (SSMS).
2. Replace `<REPLACE_WITH_STRONG_DBA_GENERATED_PASSWORD>` with a cryptographically secure 32+ character password.
3. Execute the script under a `sysadmin` or `db_owner` account against the SIMRS SQL Server instance.
4. Verify the output permissions:
   - **Granted:** `SELECT` strictly on `simrs.acc_journal_entries`, `simrs.ar_receivables`, `simrs.trx_patient_bills`, `simrs.emr_sync_logs`, `simrs.hfis_doctor_schedules`, and `simrs.infra_print_queues`.
   - **Denied:** Explicit `DENY INSERT, UPDATE, DELETE, ALTER, CONTROL, DROP` and database-wide `DENY EXECUTE`.

---

## 3. Deployment Option A: Windows Server (IIS + Windows Service)

### 3.1 Prerequisites
- Windows Server 2019 or 2022
- ASP.NET Core Hosting Bundle 8.0
- IIS 10 with **URL Rewrite Module 2.1** and **Application Request Routing (ARR) 3.0**
  - *ARR Proxy Configuration:* Open IIS Manager → Server Node → **Application Request Routing Cache** → **Server Proxy Settings** → Check **Enable proxy** → Apply.

### 3.2 Installation Steps

1. **Publish Binaries:**
   ```powershell
   # In repository root
   dotnet publish src/backend/PrIMA.Api/PrIMA.Api.csproj -c Release -o C:\PrIMA\api
   dotnet publish src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj -c Release -o C:\PrIMA\detectors
   
   # Build frontend SPA
   cd src\frontend
   npm ci
   npm run build
   # Copy dist contents to C:\PrIMA\wwwroot
   Copy-Item -Recurse dist\* C:\PrIMA\wwwroot\
   ```

2. **Configure Production Settings:**
   - Copy `deploy/appsettings.Production.json` to `C:\PrIMA\api\appsettings.Production.json`.
   - Update connection strings, JWT signing keys, and retention parameters.

3. **Install Windows Services:**
   Open PowerShell as Administrator:
   ```powershell
   cd deploy\windows-service
   .\install-prima-service.ps1 -Action Install -InstallPath "C:\PrIMA"
   ```
   This configures:
   - `PrIMAApi`: Web API & supervisor service on `http://127.0.0.1:5000` with 60-second crash auto-restart.
   - `PrIMADetectors`: Detector runner & webhook listener on port 5090 with 60-second crash auto-restart.
   - Windows Firewall inbound rules for ports 5000 and 5090.

4. **Configure IIS Site:**
   - Create a new IIS website pointing its physical path to `C:\PrIMA\wwwroot`.
   - Copy `deploy/iis/web.config` into `C:\PrIMA\wwwroot\web.config`.
   - Bind the website to port 443 with the hospital's SSL certificate.

---

## 4. Deployment Option B: Linux / Containerized (Docker Compose)

### 4.1 Prerequisites
- Docker Engine 24+ and Docker Compose v2+
- Valid TLS certificates placed in `deploy/ssl/` (`prima.crt` and `prima.key`)

### 4.2 Installation Steps

1. **Set Environment Secrets:**
   Create an `.env` file in `deploy/`:
   ```bash
   MSSQL_SA_PASSWORD=YourStrongHospitalPassword2026!
   PRIMA_JWT_SIGNING_KEY=your-32-byte-base64-random-production-key
   PRIMA_ENROLLMENT_TOKEN=PRM-TOKEN-...
   ```

2. **Deploy the Stack:**
   ```bash
   cd deploy
   docker compose -f docker-compose.prod.yml up -d --build
   ```

3. **Verify Containers:**
   ```bash
   docker compose -f docker-compose.prod.yml ps
   ```
   All services (`prima-db`, `prima-api`, `prima-detectors`, `prima-frontend`) should show healthy / running.

---

## 5. Storage Hygiene & Database Retention Maintenance (`DEC-009`)

While `MonitoringSupervisorWorker` executes daily background pruning within the API, hospital DBAs can also schedule the native SQL Server Agent job for off-peak execution:

1. Open `deploy/sql/maintenance-retention-pruning.sql` in SSMS.
2. Confirm `@DatabaseName = N'PrIMA'` matches your database name.
3. Execute the script to create the job `PrIMA_Daily_Retention_Pruning`.
4. The job runs every morning at **02:00:00 AM**, deleting clean `Passed` runs older than 90 days in safe 1,000-row chunks. All runs tied to issue occurrences or failure investigations are permanently retained (`BR-PRIMA-009`).

---

## 6. Verification & Operational Health Check

- **Deep SQL Health Probe:**
  ```bash
  curl -k -i https://prima.hospital.internal/health
  # Expected: HTTP/1.1 200 OK
  # Payload: { "status": "Healthy", "database": "Connected", "timestampUtc": "..." }
  ```
- **Webhook Re-run Verification:**
  ```bash
  curl -i -X POST http://127.0.0.1:5090/webhooks/journal-balance-detector
  # Expected: HTTP/1.1 200 OK with inspection findings payload
  ```
