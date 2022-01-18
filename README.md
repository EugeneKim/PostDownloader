# Post Downloader

What is Post Downloader?
Post Downloader is a simple micro service that uses Azure Batch Service.

What does Post Downloader use/implement?
- Docker Container: A containerized application for online REST API access to fetch/merge data(posts) then upload to the Azure blob container. The container accesses the free REST API [{JSON} PlaceHolder](https://jsonplaceholder.typicode.com).
- ACR: Azure container registry that the docker container is registred to from [docker](https://www.docker.com/).
- Azure Batch: Batch service that creates the pool of the virtual machines using the docker container and scheules the batch job.

### Source Folder Structure
```
PostDownloader
  +-- BatchSubmitter: Folder of the project that submits the batch.
  |  +-- *.cs: C# source files
  |  +-- appsettings.json: App settings file. Update with your own settings.
  +-- PostDownloader: Folder of the project that implements the containerized application.
  |  +-- *.cs: C# source files.
  |  +-- Dockfile: Docker file.
```

### Notice

The base image of the container is [mcr.microsoft.com/dotnet/runtime:6.0](mcr.microsoft.com/dotnet/runtime:6.0) that is the latest dotnet version at the time of writing. The Azure virtual machines marketplace image is Linux (Ubuntu).

The BatchSubmitter project uses [Azure Blob Storage client *v12* SDK](https://www.nuget.org/packages/Azure.Storage.Blobs/) that replaces and makes breaking changes on the previous version of SDK (v11).
Refer to the preceding table to see how they are different.

|  Package  |    v11 Microsoft.Azure.Storage.Blob   |   v12   Azure.Storage.Blobs   |
|:---------:|:-------------------------------------:|:-----------------------------:|
| Namespace | Microsoft.Azure.Storage.Blob.Protocol | Azure.Storage.Blobs.Models    |
| Namespace | Microsoft.Azure.Storage.Blob          | Azure.Storage.Blobs           |
| Namespace | Microsoft.Azure.Storage               | Azure                         |
| Class     | CloudBlobClient                       | BlobServiceClient             |
| Class     | CloudBlobContainer                    | BlobContainerClient           |
| Class     | CloudBlockBlob                        | BlobClient or BlockBlobClient |
| Class     | StorageException                      | RequestFailedException        |
| Class     | BlobErrorCodeStrings                  | BlobErrorCode                 |

### Step-by-step instructions
*Replace any angle brackets with proper values.*

1. Update appsettings.json in BatchSubmitter project.
Update the empty values in the appsettings.json with your own. Refer to the comment in AppSettings.cs to understand the values.

2. Build PostDownloader project.
This step requires publish with release build.
```
dotnet publish --configuration Release --output .\publish
```

3. Build an image from the Dockerfile.
```
docker build -t <tag> .
```
You can use docker run to test the image locally.
```
docker run -it --rm <tag> /post:1
```

4. Create a new resource group. (Skip this if you want to use existing one.)
```
az group create --name <resource group name> --location <location>
```

5. Create a new Azure container registry in the resource group. (Skip this if you want to use existing one. *The registry name should be globally unique.*)
```
az acr create --resource-group <resource group name> --name <registry name> --sku <sku>
```

6. Tag the image.
```
docker tag post-downloader <registry name>.azurecr.io/<tag>
```

7. Log in to an Azure container registry.
```
az acr login -n <registry name> -u <user name> -p <password>
```
*To get user name and password of the ACR, go to ACR > Access Keys > Enable Admin User in Azure Portal.*

8. Push image to ACR.
```
docker push <registry name>.azurecr.io/<tag>
```

You can use the following commands to test the image locally.
```
az acr repository list --name <registry name> --output table
```
```
docker run <registry name>.azurecr.io/<tag> /post:1
```

9. Create a new Azure stroage account.
```
az storage account create --name stbatchcontainerdemo --sku Standard_LRS --resource-group rg-batchcontainer-demo --location eastus
```

10. Create a batch account
```
az batch account create --name btbatchcontainerdemo --storage-account stbatchcontainerdemo --resource-group rg-batchcontainer-demo --location eastus
```

11. Run BatchSubmitter then check the status of Azure Batch and output file in Azure blob container.
You should ensure that multiple posts (post_*.json) and a single merged post (merged.json) blobs in the blob container.

### Useful Tools
- [Azure Storage Explorer](https://azure.microsoft.com/en-au/features/storage-explorer/): Desktop tool to conveniently manage Azure storage resources.
- [Batch Explorer](https://azure.github.io/BatchExplorer/): Desktop tool to conveniently manage Azure batch service.