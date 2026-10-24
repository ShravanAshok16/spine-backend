=====================================================================
SPINE API — COMPLETE LEARNING NOTES ON SOURCE CODE
=====================================================================
Author: Shravan Ashok
Coverage: Sprint 1 & Sprint 2 (Days 1–4)
=====================================================================


=====================================================================
PART 1 — DOMAIN ENTITIES & DB CONTEXT
=====================================================================

--- Spine.Core/Entities/User.cs ---
  - Represents the User table.
  - Fields: Id (Guid), Email, PasswordHash, FirstName, LastName,
    PhoneNumber, DisplayName, PrivacySetting, CreatedAt, IsActive.
  - Key concept: PrivacySetting (Public, FriendsOnly, Private) 
    determines who can see the user's library.

--- Spine.Core/Entities/Book.cs ---
  - The Global Catalog of books. 
  - Fields: Id, Isbn, Title, Author, CoverUrl, Description, PageCount,
    SeriesId, SeriesPosition.
  - Key concept: Books are shared across all users to save database space.
    If two users add "The Hobbit", there is only ONE row in the Books table.

--- Spine.Core/Entities/UserBook.cs ---
  - The Join table between User and Book (The Personal Library).
  - Fields: Id, UserId, BookId, Status, CurrentPage, PersonalRating,
    PrivateNotes, LentTo, IsDeleted.
  - Key concept: UserId is a FK to Users. BookId is a FK to Books.

--- Spine.Infrastructure/Data/AppDbContext.cs ---
  - The bridge between C# and PostgreSQL.
  - `OnModelCreating` configuration:
    - `e.HasIndex(x => new { x.UserId, x.Status })` → Composite index for fast dashboard queries.
    - `e.HasIndex(x => new { x.UserId, x.BookId }).IsUnique()` → Prevents a user from adding the same book to their library twice.
    - Friendship setup:
      `e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);`
      *Why Restrict?* If a user deletes their account, we don't want to accidentally delete all their friends' accounts. We prevent the delete until friendships are resolved.


=====================================================================
PART 2 — AUTH & ACCOUNT CONTROLLERS
=====================================================================

--- Spine.Api/Services/TokenService.cs ---
  - Generates the JWT.
  - Uses `SymmetricSecurityKey` with `Jwt:Key` from appsettings.
  - Claims included: `sub` (User.Id), `email`.
  - Expiry read from config (`Jwt:ExpiresMinutes`, default 30).

--- Spine.Api/Controllers/AuthController.cs ---
  - `POST /api/Auth/register`:
    - Checks if email exists using `await _db.Users.AnyAsync(...)`.
    - Defaults `DisplayName` to FirstName + LastName if left blank.
    - Hashes password: `BCrypt.Net.BCrypt.HashPassword(request.Password)`.
    - Returns `AuthResponse` with the JWT.
  - `POST /api/Auth/login`:
    - Uses `_db.Users.FirstOrDefaultAsync(u => u.Email == email)`.
    - Verifies password: `BCrypt.Net.BCrypt.Verify(...)`.
    - Returns 401 Unauthorized with a generic error if failed.

--- Spine.Api/Controllers/AccountController.cs ---
  - Has `[Authorize]` at the class level. Every endpoint requires a valid JWT.
  - `GetCurrentUserId()`: Helper method that extracts the `sub` claim from the JWT. Throws an error if it doesn't exist.
  - `PUT /api/Account/email`:
    - Requires `CurrentPassword` to prevent stolen tokens from changing emails.
    - Checks if the new email is already taken by someone else.
  - `PUT /api/Account/password`:
    - Verifies current password.
    - Hashes new password and saves it.


=====================================================================
PART 3 — KAFKA EVENT-DRIVEN ARCHITECTURE
=====================================================================

--- Spine.Api/Events/PasswordResetRequestedEvent.cs ---
  - The DTO representing the message sent to Kafka.
  - Fields: UserId, Email, Token, RequestedAt.

--- Spine.Api/Services/KafkaProducerService.cs ---
  - The Producer (Pub/Sub).
  - Uses `IProducer<Null, string>` from `Confluent.Kafka`.
  - Connects to `localhost:9092` (from appsettings).
  - `PublishPasswordResetEventAsync()`:
    - Serializes the event to JSON using `JsonSerializer.Serialize()`.
    - Calls `_producer.ProduceAsync("password-reset-events", message)`.

--- Spine.Api/Controllers/AccountController.cs (Forgot Password) ---
  - `POST /api/Account/forgot-password`:
    - Has `[AllowAnonymous]` because the user can't log in to request a reset.
    - Finds user by email. (If null, returns 200 OK anyway to prevent email enumeration).
    - Generates a token (`Guid.NewGuid().ToString("N")`).
    - Saves the token to `PasswordResetTokens` (expires in 15 mins).
    - Calls `_kafkaProducer.PublishPasswordResetEventAsync()`.
    - Returns 200 OK immediately. (No waiting for email).

--- Spine.Worker/Worker.cs ---
  - The Background Consumer (Subscriber).
  - Inherits from `BackgroundService`.
  - `ExecuteAsync()`:
    - Sets up `ConsumerConfig` with `GroupId = "spine-email-worker-group"` and `AutoOffsetReset = Earliest`.
    - Subscribes to `"password-reset-events"`.
    - Runs a `while (!stoppingToken.IsCancellationRequested)` loop.
    - Calls `consumer.Consume(stoppingToken)` to block and wait for a message.
    - Logs `consumeResult.Message.Value`.
    - *(Future: Parse JSON and send an email via SendGrid).*


=====================================================================
PART 4 — INTEGRATION TESTS & TRICKY FIXES
=====================================================================

--- Spine.Tests/Integration/CustomWebApplicationFactory.cs ---
  - Replaces the real Npgsql database with an InMemory database.
  - Key fix 1: `if (!builder.Environment.IsEnvironment("Testing"))` in Program.cs skips the Npgsql registration.
  - Key fix 2: `var dbName = "SpineTestDb_" + Guid.NewGuid();` is declared OUTSIDE the lambda. If it's inside, a new database is created for every single HTTP request, causing login to fail immediately after registration.

--- Spine.Tests/Integration/TestHelpers.cs ---
  - `RegisterAndGetTokenAsync()`: A helper method to register a user and return the JWT.
  - `AuthenticateClient()`: Attaches the token to `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token)`.

--- Spine.Api/Validators/RegisterRequestValidator.cs ---
  - Checks Email format, Password complexity (length, uppercase, number, special char), and matching ConfirmPassword.
  - Mistake made: We cannot use `.MustAsync()` to check if an email exists in the database here. ASP.NET Core's validation pipeline is synchronous and will throw `AsyncValidatorInvokedSynchronouslyException`. Database checks must be done inside the Controller.

=====================================================================
END OF NOTES
=====================================================================
