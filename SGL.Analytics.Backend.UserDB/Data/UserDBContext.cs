using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;

public class UserDBContext : DbContext {
	public UserDBContext(DbContextOptions<UserDBContext> options)
		: base(options) {
	}

	public DbSet<UserRegistration> UserRegistrations => Set<UserRegistration>();
	public DbSet<ApplicationWithUserProperties> Applications => Set<ApplicationWithUserProperties>();
	public DbSet<ApplicationUserPropertyDefinition> ApplicationUserPropertyDefinitions => Set<ApplicationUserPropertyDefinition>();
	public DbSet<ApplicationUserPropertyInstance> ApplicationUserPropertyInstances => Set<ApplicationUserPropertyInstance>();

}
