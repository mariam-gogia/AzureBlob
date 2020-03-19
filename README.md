# AzureBlob
Web API for Storing and Managing files using Azure Blob Storage

***Project***
.NET Core based API project that uses Azure Blob Storage to persist data needed for the content files resource.
Project implements OpenAPI and Swagger UI to interact with API resources and to provide documentation.

The application is deployed as Microsoft Azure App Service and can be found at: 
https://blobassignment.azurewebsites.net/index.html

**Framework:**
Visual Studio 2019, C#, .NET Core API 3.1, Azure Blob Storage.

**Overview:**

The application stores, updates, deletes, and retrieves files from Azure Storage containers. 
It automatically detects content type of the file.
Communication is done entirely using JSON objects.
Each of the http action is reviewed in detail below.

Project uses customized error responses, details about error responses are provided below.

**1. API operation - Create & Update**

Implemented as HTTP PUT action.

URI Parameters: fileName, containerName
Request Body: file upload

Responses:
1. 201 (Created) - if the content file is successfully created.

2. 400 (Bad Request) – returned if the task has one or more invalid parameters
Response body example for 400(Bad Request):

<pre>
{ 
       “errorNumber” : error number
       “parameterName” : “parameter name”,
       “parameterValue” :“value provided that cause the error”,
       “errorDescription” : “error description ”
}
</pre>

3. 204 (NoContent) – returns if the user successfully updates the file.


**2. API Operation – Update **

If user provides the name of the existing container and file, user can alter the content of the file (overwrite)
Implemented as HTTP PATCH action

URI Parameters: fileName, containerName
Request body: file upload

Responses:
(Error messages have same response bodies as shown above API operation)
<pre>
1. 204(No Content) – returns in case of success
2. 400 (Bad Request) - returns if parameters are invalid
3. 404 (NotFound) – returns if entity cannot be found
</pre>

**3. API Operation – Delete**

Deletes the blob (file) from the contianer
Implemented as HTTP DELETE action

URI parameter: fileName, containerName

1. 204 (NoContent) - returns if deleted successfully 
2. 404 (Not Found) - returns if the entity to be deleted cannot be found

**4. API Operation – Retrieve by FileName**

Gets a file from the container with specified fileName
Implemented as HTTP GET action

URI parameters: fileName, containerName

1. 200 (OK) - returns the file content
2. 404 (Not Found) - returns if the entity cannot be found

**5. API Operation – Retrieve all**

Retrieves all files from the container
Implemented as HTTP GET action

URI parameter: containerName

Responses:
<pre>
1. 200 (OK) response body:
[ 
   { 
      "name": "fileName"
   },
   { 
      "name": "fileName"
]
   
</pre>
2. 404 (NotFound) - returns if the container cannot be found


**Errors**

All http 400 and 404 errors return response body using the template below

<pre>
Error Response JSON:
{
	"errorNumber": error number,
	"parameterName": " name of parameter that caused the error" ,
	"parameterValue":"value of parameter that caused the error",
	"errorDescription":"Description of the error"
}
</pre>



