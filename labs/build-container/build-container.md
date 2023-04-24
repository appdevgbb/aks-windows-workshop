# Lab: Build Application Components and Prerequisites

In this lab we will build Docker containers for the JabbR ASP.Net application and deploy the Azure SQL DB backend that will support it.

## Prerequisites

- Azure Account

## Instructions

1. Create Azure Container Registry (ACR)
    * Use the same resource group that was created above
    * In this step, you will need a unique name for your ACR instance. Use the following step to create the ACR name and then deploy.

    ```bash
    # Use the UNIQUE_SUFFIX from the first lab. Validate that the value is still set.
    echo $UNIQUE_SUFFIX
    # Set Azure Container Registry Name
    export ACRNAME=$UNIQUE_SUFFIX
    # Check ACR Name (Can Only Container lowercase)
    echo $ACRNAME
    # Persist for Later Sessions in Case of Timeout
    echo export ACRNAME=$UNIQUE_SUFFIX >> ~/workshopvars.env
    # Create Azure Container Registry
    az acr create --resource-group $RGNAME --name $ACRNAME --sku Basic 
    ```

1. Create Docker containers in ACR

    In this step we will create a Docker container image for our application. We will use ACR Builder functionality to build and store these images in the cloud.

    >**NOTE:** The directory name 'JabbR' referenced below is case sensitive, so make sure you clone with the proper case before trying to run ACR build.

    ```bash
    cd ./aks-windows-workshop/src
    az acr build --image jabbr:1.0 --registry $ACRNAME --platform Windows --verbose .
    ```

    You can see the status of the builds by running the command below.

    ```bash
    az acr task list-runs -r $ACRNAME -o table

    az acr task logs -r $ACRNAME --run-id <run id>
    ```

    Browse to your ACR instance in the Azure portal and validate that the images are in "Repositories."

**NOTE: The above build will take up to 25 minutes, so while that completes you can move on to the next lab and create your AKS cluster.

## Troubleshooting / Debugging

* Make sure all of you ACR Task commands are pointing to the correct Azure Container Registry. You can check repositories by navigating to your ACR in the Azure Portal UI.

## Docs / References

* Azure Container Registry Docs. https://docs.microsoft.com/en-us/azure/container-registry 

#### Next Lab: [Create AKS Cluster](../create-aks-cluster/README.md)
