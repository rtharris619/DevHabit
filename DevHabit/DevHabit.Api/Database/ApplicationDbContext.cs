﻿using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Entry> Entries { get; set; }
    public DbSet<EntryImportJob> EntryImportJobs { get; set; }
    public DbSet<GitHubAccessToken> GitHubAccessTokens { get; set; }
    public DbSet<Habit> Habits { get; set; }
    public DbSet<HabitTag> HabitTags { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schemas.Application);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
