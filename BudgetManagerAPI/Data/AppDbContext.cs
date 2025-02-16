﻿using BudgetManagerAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagerAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Goal> Goals { get; set; }
        public virtual DbSet<RevokedToken> RevokedTokens { get; set; }
        public virtual DbSet<MonthlyBudget> MonthlyBudgets { get; set; }
        public virtual DbSet<Alert> Alerts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Goal>()
                .Property(g => g.CurrentProgress)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Goal>()
                .Property(g => g.TargetAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MonthlyBudget>()
                .Property(b => b.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasMany(u => u.Transactions)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Categories)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<User>()
                .HasMany(u => u.Goals)
                .WithOne(g => g.User)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<MonthlyBudget>(entity =>
            {
                entity.HasOne(mb => mb.User)
                    .WithMany(u => u.MonthlyBudgets)
                    .HasForeignKey(mb => mb.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(mb => mb.Category)
                    .WithMany(c => c.MonthlyBudgets)
                    .HasForeignKey(mb => mb.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

            });

        }
    }
}
