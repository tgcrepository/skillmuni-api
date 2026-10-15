// SkillMuni API platform on Azure: Container Apps (gateway, Identity, Learner, CV, Gamification), MySQL Flexible Server, Service Bus,
// Key Vault, Blob Storage, Document Intelligence, Redis, Container Registry and monitoring. Each app has its own user-assigned managed identity
// with roles scoped to the single resource it needs (CLAUDE.md §6).
//
//   az deployment group create -g rg-skillmuni-dev -f deploy/bicep/main.bicep -p environmentName=dev imageTag=<sha>
//
// Secrets are not parameters: put them in the Key Vault this template creates (see deploy/README.md), then
// deploy again so the container apps pick them up.

targetScope = 'resourceGroup'

@description('Short environment name: dev, test or prod.')
@allowed(['dev', 'test', 'prod'])
param environmentName string

param location string = resourceGroup().location

@description('Image tag to deploy (usually the git SHA).')
param imageTag string

@description('Entra ID object id of the group that administers MySQL.')
param mysqlAdminGroupObjectId string

@description('Display name of that group.')
param mysqlAdminGroupName string

@description('MySQL password administrator, used once to create the per-service users. Pass from Key Vault in the parameters file.')
@secure()
param mysqlAdminPassword string

@description('Frontend origins allowed to call the API with cookies.')
param allowedOrigins array = []

var prefix = 'skillmuni-${environmentName}'
var compact = replace(prefix, '-', '')
var isProd = environmentName == 'prod'

var roles = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  serviceBusDataSender: '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
  serviceBusDataReceiver: '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  cognitiveServicesUser: 'a97b65f3-24c7-4388-baec-2e87135dc908'
}

// Events the Learner service subscribes to (its Service Bus subscription filters on the message subject).
var learnerEvents = [
  'identity.learner.registered'
  'identity.contact.verified'
  'identity.claim.confirmed'
  'identity.learner.erased'
  'gamification.xp.awarded'
  'assessment.attempt.submitted'
  'cv.parse.completed'
]
var learnerEventsQuoted = [for e in learnerEvents: '\'${e}\'']

// Events the Gamification service turns into XP, badges and streaks.
var gamificationEvents = [
  'identity.learner.registered'
  'identity.claim.confirmed'
  'identity.learner.erased'
  'learner.quest.step-completed'
  'learner.quest.completed'
  'learner.profile.task-completed'
  'learner.programme.enrolled'
  'learner.feedback.sent'
  'learner.photo.changed'
  'assessment.attempt.submitted'
  'learning.activity.recorded'
  'learning.brief.completed'
  'learning.quiz.submitted'
  'learning.module.completed'
  'learning.course.completed'
]
var gamificationEventsQuoted = [for e in gamificationEvents: '\'${e}\'']

// Events the Careers service projects for the Launchpad and My Matches, plus erasure.
var careersEvents = [
  'assessment.attempt.submitted'
  'learner.job-profile.completed'
  'identity.claim.confirmed'
  'identity.learner.erased'
]
var careersEventsQuoted = [for e in careersEvents: '\'${e}\'']

// ---------- Monitoring ----------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${prefix}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: isProd ? 90 : 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${prefix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// ---------- Identities (one per app) ----------
resource identityAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-identity'
  location: location
}

resource learnerAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-learner'
  location: location
}

resource cvAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-cv'
  location: location
}

resource gamificationAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-gamification'
  location: location
}

resource careersAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-careers'
  location: location
}

resource gatewayAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-gateway'
  location: location
}

// ---------- Registry ----------
resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'cr${compact}'
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
}

resource acrPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identityAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: identityAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullLearner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, learnerAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: learnerAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullCv 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, cvAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: cvAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullGamification 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, gamificationAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: gamificationAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullCareers 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, careersAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: careersAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullGateway 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, gatewayAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: gatewayAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Key Vault (RBAC) ----------
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${take(compact, 21)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: isProd ? true : null
  }
}

resource vaultIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identityAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: identityAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultLearner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, learnerAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: learnerAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultCv 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, cvAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: cvAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultGamification 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, gamificationAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: gamificationAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultCareers 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, careersAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: careersAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultGateway 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, gatewayAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: gatewayAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- MySQL Flexible Server: one server, one schema per service ----------
resource mysqlServerId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-mysql'
  location: location
}

resource mysql 'Microsoft.DBforMySQL/flexibleServers@2023-12-30' = {
  name: 'mysql-${prefix}'
  location: location
  sku: isProd ? { name: 'Standard_D2ds_v4', tier: 'GeneralPurpose' } : { name: 'Standard_B1ms', tier: 'Burstable' }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${mysqlServerId.id}': {} }
  }
  properties: {
    version: '8.0.21'
    administratorLogin: 'skillmuniadmin'
    administratorLoginPassword: mysqlAdminPassword
    storage: { storageSizeGB: isProd ? 128 : 32, autoGrow: 'Enabled' }
    backup: { backupRetentionDays: isProd ? 35 : 7, geoRedundantBackup: isProd ? 'Enabled' : 'Disabled' }
    highAvailability: { mode: isProd ? 'ZoneRedundant' : 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource mysqlEntraAdmin 'Microsoft.DBforMySQL/flexibleServers/administrators@2023-12-30' = {
  parent: mysql
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    identityResourceId: mysqlServerId.id
    login: mysqlAdminGroupName
    sid: mysqlAdminGroupObjectId
    tenantId: subscription().tenantId
  }
}

resource mysqlAzureServices 'Microsoft.DBforMySQL/flexibleServers/firewallRules@2023-12-30' = {
  parent: mysql
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource identityDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'identity'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource learnerDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'learner'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource cvDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'cv'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource gamificationDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'gamification'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource careersDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'careers'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

// ---------- Service Bus: one topic for events, one queue for notifications ----------
resource bus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-${prefix}'
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
  properties: { disableLocalAuth: true, minimumTlsVersion: '1.2' }
}

resource eventsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: bus
  name: 'skillmuni-events'
  properties: {
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    defaultMessageTimeToLive: 'P14D'
  }
}

resource notificationsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: bus
  name: 'notifications'
  properties: {
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: false
    defaultMessageTimeToLive: 'PT1H'
  }
}

resource identitySendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, identityAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: identityAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource identitySendsNotifications 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(notificationsQueue.id, identityAppId.id, roles.serviceBusDataSender)
  scope: notificationsQueue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: identityAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource learnerSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'learner'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

// Replaces the catch-all $Default rule, so the subscription only receives the events Learner handles.
resource learnerSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: learnerSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(learnerEventsQuoted, ', ')})' }
  }
}

resource learnerSendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, learnerAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: learnerAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource learnerReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(learnerSubscription.id, learnerAppId.id, roles.serviceBusDataReceiver)
  scope: learnerSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: learnerAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource cvSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'cv'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource cvSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: cvSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label = \'identity.learner.erased\'' }
  }
}

resource cvSendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, cvAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: cvAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource cvReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cvSubscription.id, cvAppId.id, roles.serviceBusDataReceiver)
  scope: cvSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: cvAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource gamificationSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'gamification'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource gamificationSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: gamificationSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(gamificationEventsQuoted, ', ')})' }
  }
}

resource gamificationSendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, gamificationAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: gamificationAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource gamificationReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(gamificationSubscription.id, gamificationAppId.id, roles.serviceBusDataReceiver)
  scope: gamificationSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: gamificationAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource careersSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'careers'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource careersSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: careersSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(careersEventsQuoted, ', ')})' }
  }
}

resource careersSendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, careersAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: careersAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource careersReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(careersSubscription.id, careersAppId.id, roles.serviceBusDataReceiver)
  scope: careersSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: careersAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Blob Storage (profile photos, certificate files, CVs): private, Entra-only ----------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${take(compact, 22)}'
  location: location
  sku: { name: isProd ? 'Standard_ZRS' : 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: true, days: 7 }
  }
}

resource learnerFiles 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'learner-files'
  properties: { publicAccess: 'None' }
}

resource learnerWritesFiles 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(learnerFiles.id, learnerAppId.id, roles.storageBlobDataContributor)
  scope: learnerFiles
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
    principalId: learnerAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource cvFiles 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'cv-files'
  properties: { publicAccess: 'None' }
}

resource cvWritesFiles 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cvFiles.id, cvAppId.id, roles.storageBlobDataContributor)
  scope: cvFiles
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
    principalId: cvAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource careersFiles 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'careers-files'
  properties: { publicAccess: 'None' }
}

resource careersWritesFiles 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(careersFiles.id, careersAppId.id, roles.storageBlobDataContributor)
  scope: careersFiles
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
    principalId: careersAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Document Intelligence (CV text reading): Entra-only, no keys ----------
resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: 'di-${prefix}'
  location: location
  kind: 'FormRecognizer'
  sku: { name: isProd ? 'S0' : 'F0' }
  properties: {
    customSubDomainName: 'di-${prefix}'
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource cvReadsDocuments 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(documentIntelligence.id, cvAppId.id, roles.cognitiveServicesUser)
  scope: documentIntelligence
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.cognitiveServicesUser)
    principalId: cvAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Redis (tenant theme, market config, badge catalogue, leaderboards) ----------
resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: 'redis-${prefix}'
  location: location
  properties: {
    sku: isProd ? { name: 'Standard', family: 'C', capacity: 1 } : { name: 'Basic', family: 'C', capacity: 0 }
    minimumTlsVersion: '1.2'
    enableNonSslPort: false
    disableAccessKeyAuthentication: true
    redisConfiguration: { 'aad-enabled': 'true' }
  }
}

// Each service signs in to Redis with its own managed identity (Entra); access keys are disabled.
resource redisAccessIdentity 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-identity'
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: identityAppId.properties.principalId
    objectIdAlias: 'ca-identity'
  }
}

resource redisAccessLearner 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-learner'
  dependsOn: [redisAccessIdentity] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: learnerAppId.properties.principalId
    objectIdAlias: 'ca-learner'
  }
}

resource redisAccessCv 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-cv'
  dependsOn: [redisAccessLearner] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: cvAppId.properties.principalId
    objectIdAlias: 'ca-cv'
  }
}

resource redisAccessGamification 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-gamification'
  dependsOn: [redisAccessCv] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: gamificationAppId.properties.principalId
    objectIdAlias: 'ca-gamification'
  }
}

resource redisAccessCareers 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-careers'
  dependsOn: [redisAccessGamification] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: careersAppId.properties.principalId
    objectIdAlias: 'ca-careers'
  }
}

// ---------- Container Apps ----------
resource apps 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${prefix}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

var originEnv = [for (origin, i) in allowedOrigins: { name: 'Gateway__AllowedOrigins__${i}', value: origin }]

var commonEnv = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Staging' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
  { name: 'Redis__Host', value: '${redis.properties.hostName}:${redis.properties.sslPort}' }
]

module identityApp 'modules/container-app.bicep' = {
  name: 'ca-identity'
  dependsOn: [acrPullIdentity, vaultIdentity, redisAccessIdentity]
  params: {
    name: 'ca-identity'
    location: location
    environmentId: apps.id
    identityId: identityAppId.id
    identityClientId: identityAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-identity:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'identity-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/identity-db-connection', identity: identityAppId.id }
      { name: 'identity-hash-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/identity-hash-key', identity: identityAppId.id }
      { name: 'auth-private-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-private-key', identity: identityAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: identityAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Identity', secretRef: 'identity-db' }
      { name: 'Identity__Security__HashKey', secretRef: 'identity-hash-key' }
      { name: 'Auth__SigningPrivateKeyPem', secretRef: 'auth-private-key' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
    ])
  }
}

module learnerApp 'modules/container-app.bicep' = {
  name: 'ca-learner'
  dependsOn: [acrPullLearner, vaultLearner, learnerSendsEvents, learnerReceivesEvents, learnerWritesFiles, redisAccessLearner]
  params: {
    name: 'ca-learner'
    location: location
    environmentId: apps.id
    identityId: learnerAppId.id
    identityClientId: learnerAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-learner:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'learner-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/learner-db-connection', identity: learnerAppId.id }
      { name: 'learner-encryption-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/learner-encryption-key', identity: learnerAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: learnerAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Learner', secretRef: 'learner-db' }
      { name: 'Learner__Security__EncryptionKey', secretRef: 'learner-encryption-key' }
      { name: 'Learner__Storage__ServiceUri', value: storage.properties.primaryEndpoints.blob }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: learnerSubscription.name }
    ])
  }
}

module cvApp 'modules/container-app.bicep' = {
  name: 'ca-cv'
  dependsOn: [acrPullCv, vaultCv, cvSendsEvents, cvReceivesEvents, cvWritesFiles, cvReadsDocuments, redisAccessCv]
  params: {
    name: 'ca-cv'
    location: location
    environmentId: apps.id
    identityId: cvAppId.id
    identityClientId: cvAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-cv:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'cv-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/cv-db-connection', identity: cvAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: cvAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Cv', secretRef: 'cv-db' }
      { name: 'Cv__Storage__ServiceUri', value: storage.properties.primaryEndpoints.blob }
      { name: 'Cv__Reader__Endpoint', value: documentIntelligence.properties.endpoint }
      { name: 'Cv__IdentityBaseAddress', value: 'http://ca-identity/' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: cvSubscription.name }
    ])
  }
}

module gamificationApp 'modules/container-app.bicep' = {
  name: 'ca-gamification'
  dependsOn: [acrPullGamification, vaultGamification, gamificationSendsEvents, gamificationReceivesEvents, redisAccessGamification]
  params: {
    name: 'ca-gamification'
    location: location
    environmentId: apps.id
    identityId: gamificationAppId.id
    identityClientId: gamificationAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-gamification:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'gamification-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/gamification-db-connection', identity: gamificationAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: gamificationAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Gamification', secretRef: 'gamification-db' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: gamificationSubscription.name }
    ])
  }
}

module careersApp 'modules/container-app.bicep' = {
  name: 'ca-careers'
  dependsOn: [acrPullCareers, vaultCareers, careersSendsEvents, careersReceivesEvents, careersWritesFiles, redisAccessCareers]
  params: {
    name: 'ca-careers'
    location: location
    environmentId: apps.id
    identityId: careersAppId.id
    identityClientId: careersAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-careers:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'careers-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/careers-db-connection', identity: careersAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: careersAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Careers', secretRef: 'careers-db' }
      { name: 'Careers__Storage__ServiceUri', value: storage.properties.primaryEndpoints.blob }
      { name: 'Careers__LearnerBaseAddress', value: 'http://ca-learner/' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: careersSubscription.name }
    ])
  }
}

module gatewayApp 'modules/container-app.bicep' = {
  name: 'ca-gateway'
  dependsOn: [acrPullGateway, vaultGateway]
  params: {
    name: 'ca-gateway'
    location: location
    environmentId: apps.id
    identityId: gatewayAppId.id
    identityClientId: gatewayAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-gateway:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: true
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: gatewayAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'Gateway__IdentityBaseAddress', value: 'http://ca-identity/' }
      { name: 'ReverseProxy__Clusters__identity__Destinations__primary__Address', value: 'http://ca-identity/' }
      { name: 'ReverseProxy__Clusters__learner__Destinations__primary__Address', value: 'http://ca-learner/' }
      { name: 'ReverseProxy__Clusters__cv__Destinations__primary__Address', value: 'http://ca-cv/' }
      { name: 'ReverseProxy__Clusters__gamification__Destinations__primary__Address', value: 'http://ca-gamification/' }
      { name: 'ReverseProxy__Clusters__careers__Destinations__primary__Address', value: 'http://ca-careers/' }
    ], originEnv)
  }
}

output gatewayUrl string = 'https://${gatewayApp.outputs.fqdn}'
output keyVaultName string = vault.name
output registryLoginServer string = registry.properties.loginServer
output mysqlHost string = mysql.properties.fullyQualifiedDomainName
