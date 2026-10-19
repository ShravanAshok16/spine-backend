using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Spine.Core.Entities;

namespace Spine.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Book> Books => Set<Book>();
        public DbSet<Series> Series => Set<Series>();
        public DbSet<UserBook> UserBooks => Set<UserBook>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. User
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.Email).HasMaxLength(256).IsRequired();
                e.Property(x => x.DisplayName).HasMaxLength(100);
            });

            // 2. Book
            modelBuilder.Entity<Book>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Isbn); // Not unique, some books might lack it or share it
                e.Property(x => x.Title).HasMaxLength(500).IsRequired();
                e.Property(x => x.Author).HasMaxLength(300).IsRequired();

                e.HasOne(x => x.Series)
                 .WithMany(s => s.Books)
                 .HasForeignKey(x => x.SeriesId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // 3. UserBook
            modelBuilder.Entity<UserBook>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.Status }); // Fast dashboard queries
                e.HasIndex(x => new { x.UserId, x.BookId }).IsUnique(); // User can only own a book once

                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Book)
                 .WithMany()
                 .HasForeignKey(x => x.BookId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // 4. Friendship (The tricky one: two FKs to User)
            modelBuilder.Entity<Friendship>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Requester)
                 .WithMany()
                 .HasForeignKey(x => x.RequesterId)
                 .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete friendships if a user is deleted

                e.HasOne(x => x.Receiver)
                 .WithMany()
                 .HasForeignKey(x => x.ReceiverId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // 5. Review
            modelBuilder.Entity<Review>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.BookId); // Fast fetching of reviews for a book
                e.Property(x => x.Rating).IsRequired();
                e.Property(x => x.ReviewText).HasMaxLength(2000);
            });

            // 6. Tags (Many-to-Many with UserBook)
            modelBuilder.Entity<Tag>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            });

            modelBuilder.Entity<UserBook>()
                .HasMany(x => x.Tags)
                .WithMany(t => t.UserBooks)
                .UsingEntity(j => j.ToTable("UserBookTags"));

            // 7. Password Reset Token
            modelBuilder.Entity<PasswordResetToken>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}