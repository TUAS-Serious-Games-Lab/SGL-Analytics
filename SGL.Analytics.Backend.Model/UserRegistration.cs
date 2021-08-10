using System;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.Backend.Model {
	public class UserRegistration {
		[Key]
		public Guid Id { get; set; }
	}
}
