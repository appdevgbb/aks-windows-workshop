# Lab: Workload Identity


## Prerequisites

- Azure Account

## Instructions

When authenticating to external systems from your application (i.e. Databases, APIs and other services), you may need the ability to have your application use an Azure Active Directory identity. The [Workload Identity]() open source project enables a Kubernetes native solution for federation of Kubernetes Service Accounts with Azure Active Directory identities. This allows you to use tokens issued by your Kuberentes cluster for calls to Azure Active Directory secured endpoints via Service Account to AAD Identity federation. 

In this lab we will create a cluster enabled with Workload Identity, create an Azure Key Vault as a test target, and then build a simple application that uses a federated service account to retrieve a secret from Azure Key Vault.

>**Note:** If you've already completed the [Create AKS Cluster](../create-aks-cluster/create-aks-cluster.md) lab, you can skip the cluster creation step below and jump to the ['Enable Workload Identity on an Existing Cluster'](#enable-workload-identity-on-an-existing-cluster) lab.

### Cluster Creation

Now lets create the AKS cluster with the OIDC Issure and Workload Identity add-on enabled.

```bash
RGNAME=WorkloadIdentityRG
LOC=eastus
CLUSTERNAME=wilab
WINDOWS_ADMIN_NAME=griffith
UNIQUE_ID=$CLUSTERNAME$RANDOM

# Create the resource group
az group create -g $RGNAME -l $LOCATION

# Create the cluster with the OIDC Issuer and Workload Identity enabled
az aks create -g $RGNAME -n $CLUSTERNAME \
--node-count 1 \
--enable-oidc-issuer \
--enable-workload-identity \
--generate-ssh-keys \
--windows-admin-username $WINDOWS_ADMIN_NAME \
--network-plugin azure

# Add a windows pool
az aks nodepool add \
--resource-group $RGNAME \
--cluster-name $CLUSTERNAME \
--os-type Windows \
--name npwin \
--node-count 1

# Get the cluster credentials
az aks get-credentials -g $RGNAME -n $CLUSTERNAME --admin
```

### Enable Workload Identity on an Existing Cluster

If you've already run through the cluster creation lab of this workshop, you don't need to create a new AKS cluster to use Workload Identity. You can just enable on your existing cluster.

Open up your cloud shell and run the following:

```bash
# First, make sure you've reloaded your environment variables
source ~/workshopvars.env

# Update the AKS Cluster to enable the OIDC Issuer and Workload Identity
az aks update -n $CLUSTERNAME -g $RGNAME \
--enable-oidc-issuer \
--enable-workload-identity
```

### Set up the identity 

In order to federate a managed identity with a Kubernetes Service Account we need to get the AKS OIDC Issure URL, create the Managed Identity and Service Account and then create the federation.

```bash
# Get the OIDC Issuer URL
export AKS_OIDC_ISSUER="$(az aks show -n $CLUSTERNAME -g $RGNAME --query "oidcIssuerProfile.issuerUrl" -otsv)"

# Create the managed identity
az identity create --name wi-demo-identity --resource-group $RGNAME --location $LOCATION

# Get identity client ID
export USER_ASSIGNED_CLIENT_ID=$(az identity show --resource-group $RGNAME --name wi-demo-identity --query 'clientId' -o tsv)

echo export USER_ASSIGNED_CLIENT_ID=$USER_ASSIGNED_CLIENT_ID >> ~/workshopvars.env

# Create a service account to federate with the managed identity
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ServiceAccount
metadata:
  annotations:
    azure.workload.identity/client-id: ${USER_ASSIGNED_CLIENT_ID}
  labels:
    azure.workload.identity/use: "true"
  name: wi-demo-sa
  namespace: default
EOF

# Federate the identity
az identity federated-credential create \
--name wi-demo-federated-id \
--identity-name wi-demo-identity \
--resource-group $RGNAME \
--issuer ${AKS_OIDC_ISSUER} \
--subject system:serviceaccount:default:wi-demo-sa
```

### Create the Key Vault and Secret

```bash
# Generate a name for Key Vault
KEY_VAULT_NAME=kv$RGNAME
echo export KEY_VAULT_NAME=kv$RGNAME >> ~/workshopvars.env

# Create a key vault
az keyvault create --name $KEY_VAULT_NAME --resource-group $RGNAME --location $LOCATION

# Create a secret
az keyvault secret set --vault-name $KEY_VAULT_NAME --name "Secret" --value "Hello"

# Grant access to the secret for the managed identity
az keyvault set-policy --name $KEY_VAULT_NAME --secret-permissions get --spn "${USER_ASSIGNED_CLIENT_ID}"

# Get the version ID
az keyvault secret show --vault-name $KEY_VAULT_NAME --name "Secret" -o tsv --query id
https://wi-demo-keyvault.vault.azure.net/secrets/Secret/ded8e5e3b3e040e9bfa5c47d0e28848a

# The version ID is the last part of the resource id above
# We'll use this later
VERSION_ID=ded8e5e3b3e040e9bfa5c47d0e28848a
```

## Create the sample app

```bash
# Create and test a new console app
dotnet new console -n keyvault-console-app
cd keyvault-console-app
dotnet run

# Add the Key Vault and Azure Identity Packages
dotnet add package Azure.Security.KeyVault.Secrets
dotnet add package Azure.Identity
```

Edit the app as follows:

```csharp
using System;
using System.IO;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

class Program
    {
        static void Main(string[] args)
        {
            //Get env variables
            string? secretName = Environment.GetEnvironmentVariable("SECRET_NAME");;
            string? keyVaultName = Environment.GetEnvironmentVariable("KEY_VAULT_NAME");;
            string? versionID = Environment.GetEnvironmentVariable("VERSION_ID");;
            
            //Create Key Vault Client
            var kvUri = String.Format("https://{0}.vault.azure.net", keyVaultName);
            SecretClientOptions options = new SecretClientOptions()
            {
                Retry =
                {
                    Delay= TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 5,
                    Mode = RetryMode.Exponential
                 }
            };

            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential(),options);

            // Get the secret value in a loop
            while(true){
            Console.WriteLine("Retrieving your secret from " + keyVaultName + ".");
            KeyVaultSecret secret = client.GetSecret(secretName, versionID);
            Console.WriteLine("Your secret is '" + secret.Value + "'.");
            System.Threading.Thread.Sleep(5000);
            }

        }
    }
```

Create a new Dockerfile with the following:

>*NOTE:* Make sure the dotnet version matches the version you used to generate the code (i.e. 6.0 or 7.0) in both FROM lines below.

```bash
FROM mcr.microsoft.com/dotnet/sdk:6.0-windowsservercore-ltsc2022 AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-windowsservercore-ltsc2022
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "keyvault-console-app.dll"]
```

Build the image. I'll create an Azure Container Registry and build there, and then link that ACR to my AKS cluster.

```bash
# Create a name for the ACR
ACR_NAME=acr$UNIQUE_SUFFIX
echo export ACR_NAME=acr$UNIQUE_SUFFIX >> ~/workshopvars.env

# Create the ACR
az acr create -g $RGNAME -n $ACR_NAME --sku Standard

# Build the image
az acr build -g $RGNAME -t kvtest --platform windows -r $ACR_NAME .

# Link the ACR to the AKS cluster
az aks update -g $RGNAME -n $CLUSTERNAME --attach-acr $ACR_NAME
```

Now deploy a pod that gets the value using the service account identity.

```bash

cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: wi-kv-test
  namespace: default
  labels:
    azure.workload.identity/use: "true"
spec:
  nodeSelector:
    "kubernetes.io/os": "windows"
  serviceAccountName: wi-demo-sa
  containers:
    - image: $ACR_NAME.azurecr.io/kvtest
      name: wi-kv-test
      env:
      - name: KEY_VAULT_NAME
        value: $KEY_VAULT_NAME
      - name: SECRET_NAME
        value: Secret
      - name: VERSION_ID
        value: ${VERSION_ID}
EOF

# Check the pod logs
kubectl logs -f wi-kv-test

# Sample Output
Retrieving your secret from wi-demo-keyvault.
Your secret is 'Hello'.
```

### Conclusion

Congrats! You should now have a working pod that uses MSAL along with a Kubernetes Service Account federated to an Azure Managed Identity to access and Azure Key Vault Secret.