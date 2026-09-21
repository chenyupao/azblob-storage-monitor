@description('Azure region for the Logic App.')
param location string

@description('Logic App workflow name.')
param logicAppName string

@description('Resource tags.')
param tags object

resource logicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: location
  tags: tags
  properties: {
    state: 'Enabled'
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {}
      triggers: {
        manual: {
          type: 'Request'
          kind: 'Http'
          inputs: {
            schema: {
              type: 'object'
              properties: {
                monitorName: {
                  type: 'string'
                }
                containerName: {
                  type: 'string'
                }
                subscribers: {
                  type: 'array'
                  items: {
                    type: 'string'
                  }
                }
                subject: {
                  type: 'string'
                }
                summary: {
                  type: 'string'
                }
                totalGrowthBytes: {
                  type: 'integer'
                }
                totalGrowthPercent: {
                  type: 'number'
                }
                samples: {
                  type: 'array'
                }
              }
            }
          }
        }
      }
      actions: {
        Configure_email_action: {
          type: 'Compose'
          inputs: 'Replace this Compose action with an authenticated email connector action. The alert payload is available from triggerBody().'
          runAfter: {}
        }
        Accepted: {
          type: 'Response'
          kind: 'Http'
          inputs: {
            statusCode: 202
            body: {
              status: 'accepted'
              message: 'Workflow deployed. Configure_email_action must be replaced before production use.'
            }
          }
          runAfter: {
            Configure_email_action: [
              'Succeeded'
            ]
          }
        }
      }
      outputs: {}
    }
  }
}

output logicAppName string = logicApp.name
