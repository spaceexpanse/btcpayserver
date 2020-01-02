﻿using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Controllers.RestApi.Models
{
    public class CreateStoreRequest
    {
        [Required]
        [MaxLength(50)]
        [MinLength(1)]
        public string StoreName { get; set; }
    }
}