using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace ContentFiles.DataTransferObjects
{
    public class ErrorResponse
    {
        /// <summary>
        /// The number representing the error
        /// </summary>
        [Required]
        public int errorNumber { get; set; }

        /// <summary>
        /// Name of the parameter where the error occured
        /// </summary>
    
        [StringLength(1024)]
        public string parameterName { get; set; }

        /// <summary>
        /// The value that caused the error in the parameter
        /// </summary>
        [StringLength(2028)]
        public string parameterValue { get; set; }

        /// <summary>
        /// The descreption of the error
        /// </summary>
        [Required]
        [StringLength(1024)]
        public string errorDescription { get; set; }
    }
}