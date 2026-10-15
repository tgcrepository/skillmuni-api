# Deploy

## Azure (Bicep)
1. Create the resource group and deploy once: `az deployment group create -g <rg> -f deploy/bicep/main.bicep -p environmentName=dev imageTag=<sha> mysqlAdminGroupObjectId=<id> mysqlAdminGroupName=<name> mysqlAdminPassword=<from your vault>`.
2. Add these secrets to the Key Vault it created (name in the `keyVaultName` output):
   - `identity-db-connection`: `server=<mysqlHost>;database=identity;user=identity_svc;password=…;SslMode=Required`
   - `identity-hash-key`: `openssl rand -base64 32`
   - `auth-signing-private-key` / `auth-signing-public-key`: RSA 2048 PEM pair (base64 is fine)
   - `learner-db-connection`: `server=<mysqlHost>;database=learner;user=learner_svc;password=…;SslMode=Required`
   - `learner-encryption-key`: `openssl rand -base64 32` (AES-256 key for bank account numbers; keep a backup,
     losing it makes stored account numbers unreadable)
   - `cv-db-connection`: `server=<mysqlHost>;database=cv;user=cv_svc;password=…;SslMode=Required`
   - `gamification-db-connection`: `server=<mysqlHost>;database=gamification;user=gamification_svc;password=…;SslMode=Required`
   - `muni-db-connection`: `server=<mysqlHost>;database=muni;user=muni_svc;password=…;SslMode=Required`
   - `platform-db-connection`: `server=<mysqlHost>;database=platform;user=platform_svc;password=…;SslMode=Required`
3. Create the per-service MySQL user (grants on its own schema only): `identity_svc` with
   `SELECT, INSERT, UPDATE, DELETE` on `identity.*`, `learner_svc` on `learner.*` and `cv_svc` on `cv.*`, `gamification_svc` on `gamification.*`, `muni_svc` on `muni.*`, `platform_svc` on `platform.*`; a separate migration
   user with DDL rights runs the EF bundles.
4. Push images `skillmuni-identity`, `skillmuni-learner`, `skillmuni-cv`, `skillmuni-gamification`, `skillmuni-muni`, `skillmuni-platform` and `skillmuni-gateway` to the registry, run the migration
   bundles, deploy again.

Each app has its own managed identity: AcrPull on the registry and Key Vault Secrets User on the vault, plus
- Identity: Service Bus Data Sender on the events topic and the notifications queue.
- Learner: Data Sender on the events topic, Data Receiver on its `learner` subscription (filtered to the six events
  it handles), Storage Blob Data Contributor on the `learner-files` container only.
- CV: Data Sender on the events topic, Data Receiver on its `cv` subscription, Blob Data Contributor on `cv-files`
  only, Cognitive Services User on the Document Intelligence account (no keys: local auth is off).
- Gamification: Data Sender on the events topic, Data Receiver on its `gamification` subscription.
- Muni: Data Receiver on its `muni` subscription (publishes nothing).
- Platform: Data Receiver on its `platform` subscription and on the `notifications` queue (publishes nothing).

## Local
`deploy/local` holds the MySQL init script and the Service Bus emulator config used by `docker-compose.yml`.
