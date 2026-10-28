=====================================================================
SPINE API — COMPLETE LEARNING NOTES ON SOURCE CODE
=====================================================================
Author: Shravan Ashok
Coverage: Sprint 1 through Sprint 6
=====================================================================


=====================================================================
PART 1 — DOMAIN ENTITIES & DB CONTEXT
=====================================================================

--- Spine.Core/Entities/User.cs ---
  - Represents the User.
  - Key fields: Id, Email, DisplayName, PrivacySetting, IsEmailVerified.

--- Spine.Core/Entities/Book.cs ---
  - The Global Catalog.
  - Key fields: Id, Isbn, Title, Author, CoverUrl, Description.

--- Spine.Core/Entities/UserBook.cs ---
  - The Join table (Personal Library).
  - Key fields: UserId, BookId, Status, CurrentPage, PersonalRating, 
    PrivateNotes, LentTo, IsDeleted.

--- Spine.Core/Entities/Friendship.cs ---
  - The Social Graph.
  - Fields: RequesterId, ReceiverId, Status (Pending, Accepted, Blocked).

--- Spine.Core/Entities/Activity.cs ---
  - The Social Feed.
  - Fields: UserId, ActionType, BookId, CreatedAt.

--- Spine.Infrastructure/Data/AppDbContext.cs ---
  - `OnModelCreating`:
    - `e.HasIndex(x => new { x.UserId, x.Status })` -> Composite index.
    - `e.HasIndex(x => new { x.UserId, x.BookId }).IsUnique()` -> Prevents adding a book twice.
    - Friendship: `HasOne(x => x.Requester).WithMany().OnDelete(Restrict)`.


=====================================================================
PART 2 — AUTH & ACCOUNT CONTROLLERS
=====================================================================

--- Spine.Api/Controllers/AuthController.cs ---
  - `POST /register`:
    - Generates `EmailVerificationToken`.
    - Saves user with `IsEmailVerified = false`.
    - Publishes `UserRegisteredEvent` to Kafka.
    - Returns 200 OK (no JWT yet).
  - `POST /login`:
    - Finds user by `Identifier` (Email OR DisplayName).
    - If `!user.IsEmailVerified`, returns 401.
    - Verifies password, returns JWT.
  - `GET /verify-email`:
    - Looks up user by `EmailVerificationToken`.
    - Sets `IsEmailVerified = true`, clears token.

--- Spine.Api/Controllers/AccountController.cs ---
  - Protected with `[Authorize]`.
  - `GetCurrentUserId()`: Extracts the `sub` claim.
  - `PUT /email`: Requires current password, checks new email isn't taken.


=====================================================================
PART 3 — KAFKA & THE WORKER
=====================================================================

--- Spine.Api/Services/KafkaProducerService.cs ---
  - Uses `IProducer<Null, string>`.
  - `PublishPasswordResetEventAsync()` -> topic: `password-reset-events`.
  - `PublishUserRegisteredEventAsync()` -> topic: `user-registered-events`.
  - `PublishBookFinishedEventAsync()` -> topic: `book-finished-events`.
  - Marked as `virtual` so we can mock it in tests.

--- Spine.Worker/KafkaConsumerWorker.cs ---
  - Inherits `BackgroundService`.
  - Subscribes to all three topics.
  - **The Singleton/Scoped Fix:** Injects `IServiceScopeFactory`. Inside the loop, uses `scope.ServiceProvider.GetRequiredService<AppDbContext>()` to get the DB.
  - **The Topic Error Fix:** Wraps `consumer.Consume()` in a `try/catch (ConsumeException)` to handle "Topic not found" gracefully.

--- Spine.Worker/Services/EmailService.cs ---
  - Uses `IResend`.
  - `SendVerificationEmailAsync()`: Creates HTML with the link `http://localhost:5173/verify-email?token={token}`.


=====================================================================
PART 4 — EXTERNAL APIS & LIBRARY MANAGEMENT
=====================================================================

--- Spine.Api/Services/OpenLibraryService.cs ---
  - `GetBookByIsbnAsync()`:
    1. Calls `/isbn/{isbn}.json`.
    2. Calls `/authors/{key}.json` for Author.
    3. Calls `/works/{key}.json` for Description.
  - Returns a clean `BookSearchResultDto`.

--- Spine.Api/Controllers/UserBooksController.cs ---
  - `POST /`: Checks Global Catalog. Saves Book if new. Links to User.
  - `GET /`: Supports `?status=`, `?search=`, `?sort=`.
  - `PUT /{id}`:
    - Saves old status.
    - Updates fields.
    - If `oldStatus != "Finished" && newStatus == "Finished"`, publishes `BookFinishedEvent` to Kafka.

--- Spine.Api/Controllers/ReviewsController.cs ---
  - `POST /book/{bookId}`: Creates a review.
  - `GET /book/{bookId}`: Returns all reviews and `AverageRating` (using LINQ `Average()`).


=====================================================================
PART 5 — SOCIAL LAYER & ACTIVITY FEED
=====================================================================

--- Spine.Api/Controllers/FriendshipsController.cs ---
  - `POST /request/{targetId}`: Creates a Friendship with `Status = "Pending"`.
  - `PUT /{id}/accept`: Sets `Status = "Accepted"`.
  - `GET /`: Returns a list of accepted friends.
  - `GET /{friendId}/library`:
    - Checks if they are friends (if not -> 403).
    - Checks if friend's `PrivacySetting == "Private"` (if yes -> 403).
    - Returns books, but sets `PrivateNotes = null` in the projection.

--- Spine.Worker/KafkaConsumerWorker.cs (Activity Feed Logic) ---
  - On `book-finished-events`:
    - Queries `Friendships` to find all users where `Status == "Accepted"`.
    - Inserts an `Activity` row for the user AND every friend.


=====================================================================
PART 6 — INTEGRATION TESTS & TRICKY FIXES
=====================================================================

--- Spine.Tests/Integration/CustomWebApplicationFactory.cs ---
  - Replaces real Npgsql with InMemory.
  - Swaps real Kafka with `MockKafkaProducerService`.
  - `var dbName = "SpineTestDb_" + Guid.NewGuid();` is outside the lambda.

--- Spine.Tests/Integration/TestHelpers.cs ---
  - `RegisterAndGetTokenAsync()`:
    - Registers user.
    - Directly sets `IsEmailVerified = true` in the InMemory DB to bypass email sending.
    - Logs in to get the JWT.

=====================================================================
END OF NOTES
=====================================================================