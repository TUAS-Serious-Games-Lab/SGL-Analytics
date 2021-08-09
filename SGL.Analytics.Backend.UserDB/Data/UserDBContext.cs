using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Model;

    public class UserDBContext : DbContext
    {
        public UserDBContext (DbContextOptions<UserDBContext> options)
            : base(options)
        {
        }

        public DbSet<SGL.Analytics.Backend.Model.UserRegistration> UserRegistration { get; set; }
    }
