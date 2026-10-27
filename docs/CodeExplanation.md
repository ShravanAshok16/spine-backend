=====================================================================
SPINE API — COMPLETE LEARNING NOTES ON SOURCE CODE
=====================================================================
Author: Shravan Ashok
Coverage: Sprint 1 through Sprint 4
=====================================================================


=====================================================================
PART 1 — DOMAIN ENTITIES & DB CONTEXT
=====================================================================

--- Spine.Core/Entities/User.cs ---
  - Represents the User.
  - Fields: Id, Email, PasswordHash, FirstName, LastName, PhoneNumber,
    DisplayName, PrivacySetting, IsEmailVerified, EmailVerificationToken.
  - Key concept: PrivacySetting (Public, FriendsOnly, Private) determines
    who can see the user's library.

--- Spine.Core/Entities/Book.cs ---
  - The Global Catalog.
  - Fields: Id, Isbn, Title, Author, CoverUrl, Description, PageCount.
  - Key concept: Books are shared across all users. Two users adding "The
    Hobbit" results in ONE row in the Books table.

--- Spine.Core/Entities/UserBook.cs ---
  - The Join table (Personal Library).
  - Fields: Id, UserId, BookId, Status (Reading/Finished), CurrentPage,
    PersonalRating (1-5), PrivateNotes, LentTo, IsDeleted.

--- Spine.Infrastructure/Data/AppDbContext.cs ---
  - `OnModelCreating` configuration:
    - `e.HasIndex(x => new { x.UserId, x.Status })` → Fast dashboard queries.
    - `e.HasIndex(x => new { x.UserId, x.BookId }).IsUnique()` → Prevents
      adding the same book twice.
    - Friendship: `HasOne(x => x.Requester).WithMany().OnDelete(Restrict)`.


=====================================================================
PART 2 — AUTH & ACCOUNT CONTROLLERS
=====================================================================

--- Spine.Api/Controllers/AuthController.cs ---
  - `POST /api/Auth/register`:
    - Generates an `EmailVerificationToken` (`Guid.NewGuid().ToString("N")`).
    - Saves user with `IsEmailVerified = false`.
    - Calls `_kafkaProducer.PublishUserRegisteredEventAsync(verifyEvent)`.
    - Returns 200 OK (does NOT return a JWT yet).
  - `POST /api/Auth/login`:
    - Finds user by `Identifier` (Email OR DisplayName).
    - If `!user.IsEmailVerified`, returns 401 Unauthorized.
    - Verifies password with BCrypt, returns JWT.
  - `GET /api/Auth/verify-email`:
    - Looks up user by `EmailVerificationToken`.
    - Sets `IsEmailVerified = true`, clears the token, saves.

--- Spine.Api/Controllers/AccountController.cs ---
  - Protected with `[Authorize]`.
  - `GetCurrentUserId()`: Extracts the `sub` claim from the JWT.
  - `PUT /email`: Requires current password, checks new email isn't taken.
  - `PUT /password`: Requires current password, hashes new password.


=====================================================================
PART 3 — KAFKA & THE WORKER
=====================================================================

--- Spine.Api/Services/KafkaProducerService.cs ---
  - Uses `IProducer<Null, string>`.
  - `PublishPasswordResetEventAsync()` → Sends to `password-reset-events`.
  - `PublishUserRegisteredEventAsync()` → Sends to `user-registered-events`.
  - Marked as `virtual` so it can be mocked in tests.

--- Spine.Worker/KafkaConsumerWorker.cs ---
  - Inherits `BackgroundService`.
  - Subscribes to `["password-reset-events", "user-registered-events"]`.
  - The `while` loop contains a `try/catch (ConsumeException)`. If a topic
    doesn't exist yet, it waits 2 seconds and retries instead of crashing.
  - Resolves `EmailService` via `_serviceProvider.CreateScope()` to
    avoid scoped dependency errors in a singleton background worker.

--- Spine.Worker/Services/EmailService.cs ---
  - Uses Resend SDK.
  - `SendVerificationEmailAsync()`: Generates HTML email with a link to
    `http://localhost:5173/verify-email?token={token}`.


=====================================================================
PART 4 — EXTERNAL APIS & LIBRARY MANAGEMENT
=====================================================================

--- Spine.Api/Services/OpenLibraryService.cs ---
  - Uses `HttpClient`.
  - `GetBookByIsbnAsync()`:
    1. Calls `/isbn/{isbn}.json` for Title and PageCount.
    2. Calls `/authors/{key}.json` for Author Name.
    3. Calls `/works/{key}.json` for Description.
  - Returns a clean `BookSearchResultDto`.

--- Spine.Api/Controllers/UserBooksController.cs ---
  - `POST /api/UserBooks`:
    - Calls OpenLibraryService.
    - Checks if Book exists in Global Catalog (`_db.Books.AnyAsync(b => b.Isbn == isbn)`).
    - If not, creates it. Then links it to User via `UserBook`.
  - `GET /api/UserBooks`:
    - Supports `?status=`, `?search=`, and `?sort=title_asc`.
    - Uses `.Include(ub => ub.Book)` to join the Global Catalog data.
  - `PUT /api/UserBooks/{id}`:
    - Updates CurrentPage, Rating, and Notes.

--- Spine.Api/Controllers/ReviewsController.cs ---
  - `POST /api/Reviews/book/{bookId}`: Creates a 1-5 star review.
  - `GET /api/Reviews/book/{bookId}`: Returns all reviews and calculates
    `AverageRating` using LINQ `Average()`.


=====================================================================
PART 5 — INTEGRATION TESTS & TRICKY FIXES
=====================================================================

--- Spine.Tests/Integration/CustomWebApplicationFactory.cs ---
  - Replaces real Npgsql with InMemory.
  - Swaps real Kafka with `MockKafkaProducerService`.
  - `var dbName = "SpineTestDb_" + Guid.NewGuid();` is outside the lambda
    to keep the DB persistent across HTTP requests.

--- Spine.Tests/Integration/TestHelpers.cs ---
  - `RegisterAndGetTokenAsync()`:
    - Registers user.
    - Bypasses email verification by directly setting `IsEmailVerified = true` in the InMemory DB.
    - Logs in to get the JWT.

=====================================================================
END OF NOTES
=====================================================================