using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.Backend.Entity {
	[Index(nameof(AppId), nameof(Username), IsUnique = true)]
	public class UserRegistration {
		[Key]
		public Guid Id { get; set; }
		public int AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; } = null!;
		public string Username { get; set; }

		public ICollection<ApplicationUserPropertyInstance> AppSpecificProperties { get; set; } = null!;

		public UserRegistration(Guid id, int appId, string username) {
			Id = id;
			AppId = appId;
			Username = username;
		}
	}
}
