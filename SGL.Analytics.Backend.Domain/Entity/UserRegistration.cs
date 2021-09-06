using SGL.Analytics.Backend.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SGL.Analytics.Backend.Domain.Entity {

	public class UserRegistration {
		public Guid Id { get; set; }
		public Guid AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; } = null!;
		public string Username { get; set; }

		public ICollection<ApplicationUserPropertyInstance> AppSpecificProperties { get; set; } = null!;

		public UserRegistration(Guid id, Guid appId, string username) {
			Id = id;
			AppId = appId;
			Username = username;
		}

		public void ValidateProperties() {
			foreach (var propDef in App.UserProperties.Where(p => p.Required)) {
				var propInst = AppSpecificProperties.Where(pi => pi.DefinitionId == propDef.Id).ToList();
				if (propInst.Count == 0) {
					throw new RequiredPropertyMissingException(propDef.Name);
				}
				else if (propInst.Single().IsNull()) {
					throw new RequiredPropertyNullException(propDef.Name);
				}
			}
			foreach (var propInst in AppSpecificProperties) {
				if (!App.UserProperties.Any(pd => pd.Id == propInst.DefinitionId)) {
					throw new UndefinedPropertyException(propInst.Definition.Name);
				}
			}
		}
	}
}
