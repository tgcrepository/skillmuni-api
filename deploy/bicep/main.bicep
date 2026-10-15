// SkillMuni API platform on Azure: Container Apps (gateway, Identity, Learner, CV, Gamification, Assessment, Learning, Muni, Platform), MySQL Flexible Server, Service Bus,
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

// Events the Gamification service turns into XP, badges and streaks (BRD §4; docs/EVENTS.md).
var gamificationEvents = [
  'identity.learner.registered'
  'identity.learner.erased'
  'learner.quest.step-completed'
  'learner.quest.completed'
  'learner.profile.task-completed'
  'learner.job-profile.completed'
  'learner.programme.enrolled'
  'learner.feedback.sent'
  'learner.photo.changed'
  'assessment.island.cleared'
  'assessment.attempt.submitted'
  'learning.activity.recorded'
  'learning.brief.completed'
  'learning.quiz.submitted'
  'learning.module.completed'
  'learning.course.completed'
  'learning.first-moves.claimed'
  'careers.application.sent'
  'careers.fest.booked'
]
var gamificationEventsQuoted = [for e in gamificationEvents: '\'${e}\'']

// Events the Assessment service consumes (Career Drivers journey rows and erasure).
var assessmentEvents = [
  'identity.learner.registered'
  'identity.learner.erased'
]
var assessmentEventsQuoted = [for e in assessmentEvents: '\'${e}\'']

// Events the Learning service subscribes to (erasure, and the Lobby's "Discover your Career Drivers" first move).
var learningEvents = [
  'identity.learner.erased'
  'assessment.attempt.submitted'
]

var learningEventsQuoted = [for e in learningEvents: '\'${e}\'']

// Events Muni copies into its learner projection (name, XP totals, finished quests, archetype) or erases on.
var muniEvents = [
  'identity.learner.registered'
  'identity.learner.erased'
  'gamification.xp.awarded'
  'learner.quest.completed'
  'assessment.attempt.submitted'
]

var muniEventsQuoted = [for e in muniEvents: '\'${e}\'']

// Events Platform turns into bell notifications or erases on.
var platformEvents = [
  'identity.learner.erased'
  'gamification.badge.earned'
]

var platformEventsQuoted = [for e in platformEvents: '\'${e}\'']

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

resource assessmentAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-assessment'
  location: location
}

resource learningAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-learning'
  location: location
}

resource gatewayAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-gateway'
  location: location
}

resource muniAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-muni'
  location: location
}

resource platformAppId 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${prefix}-platform'
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

resource acrPullAssessment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, assessmentAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: assessmentAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullLearning 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, learningAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: learningAppId.properties.principalId
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

resource acrPullMuni 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, muniAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: muniAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullPlatform 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, platformAppId.id, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: platformAppId.properties.principalId
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

resource vaultAssessment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, assessmentAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: assessmentAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultLearning 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, learningAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: learningAppId.properties.principalId
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

resource vaultMuni 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, muniAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: muniAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource vaultPlatform 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, platformAppId.id, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: platformAppId.properties.principalId
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

resource assessmentDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'assessment'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource learningDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'learning'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource muniDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'muni'
  properties: { charset: 'utf8mb4', collation: 'utf8mb4_0900_ai_ci' }
}

resource platformDatabase 'Microsoft.DBforMySQL/flexibleServers/databases@2023-12-30' = {
  parent: mysql
  name: 'platform'
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

// Identity consumes only the Learner photo (AuthUser.photoUrl, A-277).
resource identitySubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'identity'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource identitySubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: identitySubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label = \'learner.photo.changed\'' }
  }
}

resource identityReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(identitySubscription.id, identityAppId.id, roles.serviceBusDataReceiver)
  scope: identitySubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
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

resource assessmentSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'assessment'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource assessmentSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: assessmentSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(assessmentEventsQuoted, ', ')})' }
  }
}

resource assessmentSendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, assessmentAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: assessmentAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource assessmentReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(assessmentSubscription.id, assessmentAppId.id, roles.serviceBusDataReceiver)
  scope: assessmentSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: assessmentAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource learningSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'learning'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource learningSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: learningSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(learningEventsQuoted, ', ')})' }
  }
}

resource learningSendsEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventsTopic.id, learningAppId.id, roles.serviceBusDataSender)
  scope: eventsTopic
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: learningAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource learningReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(learningSubscription.id, learningAppId.id, roles.serviceBusDataReceiver)
  scope: learningSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: learningAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource muniSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'muni'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource muniSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: muniSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(muniEventsQuoted, ', ')})' }
  }
}

resource muniReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(muniSubscription.id, muniAppId.id, roles.serviceBusDataReceiver)
  scope: muniSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: muniAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource platformSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: eventsTopic
  name: 'platform'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

resource platformSubscriptionFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: platformSubscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: { sqlExpression: 'sys.Label IN (${join(platformEventsQuoted, ', ')})' }
  }
}

resource platformReceivesEvents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(platformSubscription.id, platformAppId.id, roles.serviceBusDataReceiver)
  scope: platformSubscription
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: platformAppId.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Platform reads the notifications queue Identity sends codes and links to (B-09).
resource platformReceivesNotifications 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(notificationsQueue.id, platformAppId.id, roles.serviceBusDataReceiver)
  scope: notificationsQueue
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataReceiver)
    principalId: platformAppId.properties.principalId
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

resource redisAccessAssessment 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-assessment'
  dependsOn: [redisAccessGamification] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: assessmentAppId.properties.principalId
    objectIdAlias: 'ca-assessment'
  }
}

resource redisAccessLearning 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-learning'
  dependsOn: [redisAccessAssessment] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: learningAppId.properties.principalId
    objectIdAlias: 'ca-learning'
  }
}

resource redisAccessMuni 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-muni'
  dependsOn: [redisAccessLearning] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: muniAppId.properties.principalId
    objectIdAlias: 'ca-muni'
  }
}

resource redisAccessPlatform 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
  parent: redis
  name: 'ca-platform'
  dependsOn: [redisAccessMuni] // Redis applies one access-policy change at a time.
  properties: {
    accessPolicyName: 'Data Contributor'
    objectId: platformAppId.properties.principalId
    objectIdAlias: 'ca-platform'
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
  dependsOn: [acrPullIdentity, vaultIdentity, redisAccessIdentity, identityReceivesEvents]
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
      { name: 'ServiceBus__Subscription', value: identitySubscription.name }
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

module assessmentApp 'modules/container-app.bicep' = {
  name: 'ca-assessment'
  dependsOn: [acrPullAssessment, vaultAssessment, assessmentSendsEvents, assessmentReceivesEvents, redisAccessAssessment]
  params: {
    name: 'ca-assessment'
    location: location
    environmentId: apps.id
    identityId: assessmentAppId.id
    identityClientId: assessmentAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-assessment:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'assessment-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/assessment-db-connection', identity: assessmentAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: assessmentAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Assessment', secretRef: 'assessment-db' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: assessmentSubscription.name }
    ])
  }
}

module learningApp 'modules/container-app.bicep' = {
  name: 'ca-learning'
  dependsOn: [acrPullLearning, vaultLearning, learningSendsEvents, learningReceivesEvents, redisAccessLearning]
  params: {
    name: 'ca-learning'
    location: location
    environmentId: apps.id
    identityId: learningAppId.id
    identityClientId: learningAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-learning:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'learning-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/learning-db-connection', identity: learningAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: learningAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Learning', secretRef: 'learning-db' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: learningSubscription.name }
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
      { name: 'ReverseProxy__Clusters__assessment__Destinations__primary__Address', value: 'http://ca-assessment/' }
      { name: 'ReverseProxy__Clusters__learning__Destinations__primary__Address', value: 'http://ca-learning/' }
      { name: 'ReverseProxy__Clusters__muni__Destinations__primary__Address', value: 'http://ca-muni/' }
      { name: 'ReverseProxy__Clusters__platform__Destinations__primary__Address', value: 'http://ca-platform/' }
    ], originEnv)
  }
}

module muniApp 'modules/container-app.bicep' = {
  name: 'ca-muni'
  dependsOn: [acrPullMuni, vaultMuni, muniReceivesEvents, redisAccessMuni]
  params: {
    name: 'ca-muni'
    location: location
    environmentId: apps.id
    identityId: muniAppId.id
    identityClientId: muniAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-muni:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'muni-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/muni-db-connection', identity: muniAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: muniAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Muni', secretRef: 'muni-db' }
      { name: 'Muni__LearnerBaseAddress', value: 'http://ca-learner/' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: muniSubscription.name }
    ])
  }
}

module platformApp 'modules/container-app.bicep' = {
  name: 'ca-platform'
  dependsOn: [acrPullPlatform, vaultPlatform, platformReceivesEvents, platformReceivesNotifications, redisAccessPlatform]
  params: {
    name: 'ca-platform'
    location: location
    environmentId: apps.id
    identityId: platformAppId.id
    identityClientId: platformAppId.properties.clientId
    image: '${registry.properties.loginServer}/skillmuni-platform:${imageTag}'
    registryServer: registry.properties.loginServer
    externalIngress: false
    minReplicas: isProd ? 2 : 1
    secrets: [
      { name: 'platform-db', keyVaultUrl: '${vault.properties.vaultUri}secrets/platform-db-connection', identity: platformAppId.id }
      { name: 'auth-public-key', keyVaultUrl: '${vault.properties.vaultUri}secrets/auth-signing-public-key', identity: platformAppId.id }
    ]
    env: concat(commonEnv, [
      { name: 'ConnectionStrings__Platform', secretRef: 'platform-db' }
      { name: 'Auth__SigningPublicKeyPem', secretRef: 'auth-public-key' }
      { name: 'ServiceBus__FullyQualifiedNamespace', value: '${bus.name}.servicebus.windows.net' }
      { name: 'ServiceBus__Subscription', value: platformSubscription.name }
      { name: 'ServiceBus__NotificationsQueue', value: notificationsQueue.name }
    ])
  }
}

output gatewayUrl string = 'https://${gatewayApp.outputs.fqdn}'
output keyVaultName string = vault.name
output registryLoginServer string = registry.properties.loginServer
output mysqlHost string = mysql.properties.fullyQualifiedDomainName
