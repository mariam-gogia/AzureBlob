using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System.IO;
using System.Net;
using Microsoft.Extensions.Configuration;
using ContentFiles.DataTransferObjects;

namespace ContentFiles.Controllers
{
    [Route("api/v1")]
    [Produces("application/json")]
    [ApiController]
    public class ContentFilesController : ControllerBase
    {
        private const string GetByContainerAndIdRouteName = "GetByContainerAndIdRouteName";

        private const string GetFileByIdRouteName = "GetFileByIdRouteName";

        // private const string GetFFileByIdRouteName = "GetFileByIdRouteName";
        public IConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets the storage connection string.
        /// </summary>
        /// <value>
        /// The storage connection string.
        /// </value>
        public string StorageConnectionString
        {
            get
            {
                return Configuration.GetConnectionString("DefaultConnection");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentFilesController" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>

        public ContentFilesController(IConfiguration configuration)
        {
            Configuration = configuration;
        }


        /// <summary>
        /// Defines the blob note response providing only the name
        /// </summary>
        public class BlobName
        {
            /// <summary>
            /// Gets or sets the blob name.
            /// </summary>
            /// <value>The blob's name.</value>
            public string Name { get; set; }
        }

        /// <summary>
        /// Creates a new container and file if they do not exist, if the file exists - it updates it.
        /// </summary>
        /// <param name="file"> File upload </param>
        /// <param name="containername"> Name of the container</param>
        /// <param name="fileName"> Name of the file </param>
        /// <returns> The location of the created blob  </returns>
        [Route("{containername}/[controller]/{fileName}", Name = GetByContainerAndIdRouteName)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [HttpPut]
        public async Task<IActionResult> PutFile(IFormFile file, [FromRoute]string containername, [FromRoute] string fileName)
        {
            // validate container name
            ErrorResponse error = ValidateContainerName(containername);
            if(error.errorNumber == 7)
            {
                return BadRequest(error);
            }

            // checking the validity of paramteres provided
            ErrorResponse errorCheck = VerifyParameters(file, containername, fileName);
            if (errorCheck.errorNumber != 0)
            {
                return BadRequest(errorCheck);
            }
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);

            // creating the blob clinet
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // retrieve a reference to a container
            CloudBlobContainer container = blobClient.GetContainerReference(containername);

            // create the container if it doesn't already exist
            await container.CreateIfNotExistsAsync();

            // if the containername contains word 'public' - set permissions on the blob to allow public access
            if (containername.Contains("public"))
            {
                await container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }

            // retrieve a reference to a blob named the blob specified by the caller
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
           
            // check if the blob already exists
            bool fileAlreadyExists = blockBlob.Exists();

            // upload the file
            using (Stream uploadedFileStream = file.OpenReadStream())
            {
                blockBlob.Properties.ContentType = file.ContentType;
                await blockBlob.UploadFromStreamAsync(uploadedFileStream);
            }

            // if such blob already exists than it was updated -  return NoContent
            if (fileAlreadyExists)
            {
                return StatusCode((int)HttpStatusCode.NoContent);
            }
            // if new blob with public access was created in 201 location header return blob uri
            else if(containername.Contains("public"))
            {
                return Created(blockBlob.Uri, null);
            }

            // otherwise return "created" with route uri
            return CreatedAtRoute(routeName: GetByContainerAndIdRouteName, routeValues: null, null);
        }
        
        // PATCH
        /// <summary>
        /// Updates the already existing file's content
        /// </summary>
        /// <param name="file"> Name of the file upload </param>
        /// <param name="containername"> Name of the container </param>
        /// <param name="fileName"> Name of the file to be updated </param>
        /// <returns> update confirmation </returns>
        [Route("{containername}/[controller]/{fileName}")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [HttpPatch]
        public async Task<IActionResult> UpdateFile(IFormFile file, [FromRoute]string containername, [FromRoute] string fileName)
        {
            // validate container name
            ErrorResponse error = ValidateContainerName(containername);
            if (error.errorNumber == 7)
            {
                return BadRequest(error);
            }

            // validating parameters provided
            ErrorResponse errorCheck = VerifyParameters(file, containername, fileName);
            if (errorCheck.errorNumber != 0)
            {
                return BadRequest(errorCheck);
            }

            ErrorResponse errorResponse = new ErrorResponse();
           
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);

            // creating the blob clinet
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // retrieve a reference to a container
            CloudBlobContainer container = blobClient.GetContainerReference(containername);
            // if the container does not exist return NotFound error
            if (!container.Exists())
            {
                errorResponse.errorNumber = 4;
                errorResponse.parameterName = "containername";
                errorResponse.parameterValue = containername;
                errorResponse.errorDescription = "The entity could not be found";
                return NotFound(errorResponse);
            }

            else
            {
                // retrieve a reference to a blob named the blob specified by the caller
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                // if such blob exists - update the file content
                if (blockBlob.Exists())
                {
                    using (Stream uploadedFileStream = file.OpenReadStream())
                    {
                        blockBlob.Properties.ContentType = file.ContentType;
                        await blockBlob.UploadFromStreamAsync(uploadedFileStream);
                    }
                }
                // if not, return file NotFound error
                else
                {
                    errorResponse.errorNumber = 4;
                    errorResponse.parameterName = "fileName";
                    errorResponse.parameterValue = fileName;
                    errorResponse.errorDescription = "The entity could not be found";
                    return NotFound(errorResponse);
                }
            }
            return StatusCode((int)HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Helper method to verify validity of user entered parameters
        /// </summary>
        /// <param name="file"> Name of the file upload </param>
        /// <param name="containername"> Name of the container </param>
        /// <param name="fileName"> Name of the file </param>
        /// <returns> ErrorResponse object with proper error given the parameters </returns>
        private ErrorResponse VerifyParameters(IFormFile file, string containername, string fileName)
        {
            ErrorResponse errorResponse = new ErrorResponse();
            if (file == null)
            {
                errorResponse.errorNumber = 6;
                errorResponse.parameterName = "fileData";
                errorResponse.parameterValue = null;
                errorResponse.errorDescription = "The Parameter cannot be null";

            }

            if (fileName.Length > 75)
            {
                errorResponse.errorNumber = 2;
                errorResponse.parameterName = "fileName";
                errorResponse.parameterValue = fileName;
                errorResponse.errorDescription = "The parameter value is too large ";

            }

            if (containername.Length < 3)
            {
                errorResponse.errorNumber = 5;
                errorResponse.parameterName = "containername";
                errorResponse.parameterValue = containername;
                errorResponse.errorDescription = "The parameter value is too small ";

            }

            if (containername.Length > 63)
            {
                errorResponse.errorNumber = 2;
                errorResponse.parameterName = "containername";
                errorResponse.parameterValue = containername;
                errorResponse.errorDescription = "The parameter value is too large ";

            }

            return errorResponse;
        }
       /// <summary>
       /// Validating container name
       /// </summary>
       /// <param name="containername"> the name of the container</param>
       /// <returns>returns error if container has invalid characters</returns>
        private ErrorResponse ValidateContainerName(string containername)
        {
            // validate container name
            ErrorResponse error = new ErrorResponse();
            try
            {
                NameValidator.ValidateContainerName(containername);
            }
            catch (ArgumentException)
            {
                error.errorNumber = 7;
                error.parameterName = "containername";
                error.parameterValue = containername;
                error.errorDescription = "Invalid characters";
            }
            return error;
        }
        //DELETE
        /// <summary>
        /// Deletes the existing file in the container
        /// </summary>
        /// <param name="containername"> Name of the container </param>
        /// <param name="fileName"> Name of the file </param>
        /// <returns> Deletes the file </returns>
        [Route("{containername}/[controller]/{fileName}")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [HttpDelete]
        public IActionResult Delete([FromRoute]string containername, [FromRoute] string fileName)
        {
            // validate container name
            ErrorResponse error = ValidateContainerName(containername);
            if (error.errorNumber == 7)
            {
                return BadRequest(error);
            }

            // verify validity of provided parameters
            ErrorResponse errorCheck = VerifyParameters(null, containername, fileName);
            if (errorCheck.errorNumber != 0 && errorCheck.errorNumber != 6)
            {
                return BadRequest(errorCheck);
            }

            ErrorResponse errorResponse = new ErrorResponse();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);

            // creating the blob clinet
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // retrieve a reference to a container
            CloudBlobContainer container = blobClient.GetContainerReference(containername);
            // if such container does not exist - return NotFound
            if (!container.Exists())
            {
                errorResponse.errorNumber = 4;
                errorResponse.parameterName = "containername";
                errorResponse.parameterValue = containername;
                errorResponse.errorDescription = "The entity could not be found";
                return NotFound(errorResponse);
            }

            else
            {
                // retrieve a reference to a blob named the blob specified by the caller
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                // if such file exists - delete
                if (blockBlob.Exists())
                {
                    blockBlob.Delete();
                }
                // if it does not exist - return file NotFound
                else
                {
                    errorResponse.errorNumber = 4;
                    errorResponse.parameterName = "fileName";
                    errorResponse.parameterValue = fileName;
                    errorResponse.errorDescription = "The entity could not be found";
                    return NotFound(errorResponse);
                }
            }
            return StatusCode((int)HttpStatusCode.NoContent);
        }


        // GET by id
        /// <summary>
        /// Retrieves file by its name
        /// </summary>
        /// <param name="containername"> Name of the container</param>
        /// <param name="fileName"> Name of the file</param>
        /// <returns> return content file </returns>
        [Route("{containername}/[controller]/{fileName}")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet]
        public async Task<IActionResult> GetByFileNameAsync([FromRoute]string containername, [FromRoute] string fileName)
        {
            // validate container name
            ErrorResponse error = ValidateContainerName(containername);
            if (error.errorNumber == 7)
            {
                return BadRequest(error);
            }
            // verifying validity of provided parameters
            ErrorResponse errorCheck = VerifyParameters(null, containername, fileName);
            if (errorCheck.errorNumber != 0 && errorCheck.errorNumber != 6)
            {
                return BadRequest(errorCheck);
            }
           
            ErrorResponse errorResponse = new ErrorResponse();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);

            // creating the blob clinet
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // retrieve a reference to a container
            CloudBlobContainer container = blobClient.GetContainerReference(containername);
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
            // if the contaiener does not exist, return - NotFound
            if (!container.Exists())
            {
                errorResponse.errorNumber = 4;
                errorResponse.parameterName = "containername";
                errorResponse.parameterValue = containername;
                errorResponse.errorDescription = "The entity could not be found";
                return NotFound(errorResponse);
            }

            // if the file does not exist, return - NotFound
            if (!blockBlob.Exists())
            {
                errorResponse.errorNumber = 4;
                errorResponse.parameterName = "fileName";
                errorResponse.parameterValue = fileName;
                errorResponse.errorDescription = "The entity could not be found";
                return NotFound(errorResponse);
            }

            // otherwise get the file
            MemoryStream memoryStream = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            // retrieve and return the blob content
            return new FileStreamResult(memoryStream, blockBlob.Properties.ContentType);
        }
        /// <summary>
        /// Retrieves all files in the container
        /// </summary>
        /// <param name="containername"> Name of the container </param>
        /// <returns> List of file names in the container</returns>
        [Route("{containername}/[controller]")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromRoute]string containername)
        {
            // validate container name
            ErrorResponse error = ValidateContainerName(containername);
            if (error.errorNumber == 7)
            {
                return BadRequest(error);
            }
            // dummy value for verifyParameres method
            string test = "dummy";
            // method call to verify the validity of containername
            ErrorResponse errorCheck = VerifyParameters(null, containername, test);
            if (errorCheck.errorNumber != 0 && errorCheck.errorNumber != 6)
            {
                return BadRequest(errorCheck);
            }

            ErrorResponse errorResponse = new ErrorResponse();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            // creating the blob clinet
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            // retrieve a reference to a container
            CloudBlobContainer container = blobClient.GetContainerReference(containername);
            // if the container does not exist - return NotFound error
            if (!container.Exists())
            {
                errorResponse.errorNumber = 4;
                errorResponse.parameterName = "containername";
                errorResponse.parameterValue = containername;
                errorResponse.errorDescription = "The entity could not be found";
                return NotFound(errorResponse);
            }
            // otherwise, create a list of blobs
            List<BlobName> blobNames = new List<BlobName>();
            await GetBlobNames(container, blobNames);
            // return blob names
            if(blobNames.Count > 0)
            {
                return new ObjectResult(blobNames.ToArray());
            }
            // return OK even if the container is empty
            return StatusCode((int)HttpStatusCode.OK);
        }

        /// <summary>
        /// Gets the BLOB names.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="blobNames">The BLOB names.</param>
        private static async Task GetBlobNames(CloudBlobContainer container, List<BlobName> blobNames)
        {
            if (await container.ExistsAsync())
            {
                BlobRequestOptions options = new BlobRequestOptions();
                OperationContext context = new OperationContext();

                // Loop over items within the container and output the length and URI.
                BlobResultSegment result = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.Metadata, null, null, options, context);
                if (result?.Results != null)
                {
                    foreach (var blob in result.Results)
                    {
                        if (blob is CloudBlockBlob)
                        {
                            blobNames.Add(new BlobName() { Name = ((CloudBlockBlob)(blob)).Name });
                        }
                    }
                }
            }
        }
    }
}


