using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.Backend.Model {
	[Index(nameof(AppId), nameof(Username), IsUnique = true)]
	public class UserRegistration {
		[Key]
		public Guid Id { get; set; }
		public int AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; }
		public string Username { get; set; }

		public ICollection<ApplicationUserPropertyInstance> AppSpecificProperties { get; set; }

		public UserRegistration(Guid id, int appId, ApplicationWithUserProperties app, string username,
			ICollection<ApplicationUserPropertyInstance> appSpecificProperties) {
			Id = id;
			AppId = appId;
			App = app;
			Username = username;
			AppSpecificProperties = appSpecificProperties;
		}
	}
}
