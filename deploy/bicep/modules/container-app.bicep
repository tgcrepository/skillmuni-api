// One SkillMuni service as a Container App: image from ACR through its managed identity, secrets as Key Vault
// references, liveness/readiness probes on /health/live and /health/ready.

param name string
param location string
param environmentId string
param identityId string
param identityClientId string
param image string
param registryServer string
param externalIngress bool
param minReplicas int = 1
param maxReplicas int = 5
param secrets array = []
param env array = []

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: externalIngress
        targetPort: 8080
        transport: 'auto'
        allowInsecure: !externalIngress
      }
      registries: [
        { server: registryServer, identity: identityId }
      ]
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: name
          image: image
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: concat(env, [{ name: 'AZURE_CLIENT_ID', value: identityClientId }])
          probes: [
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 10 }
            { type: 'Readiness', httpGet: { path: '/health/ready', port: 8080 }, periodSeconds: 10 }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          { name: 'http', http: { metadata: { concurrentRequests: '50' } } }
        ]
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
