using System;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.Backend.Model
{
    public class LogMetadata
    {
        [Key]
        public Guid Id { get; set; }
    }
}
