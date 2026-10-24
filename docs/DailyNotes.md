=====================================================================
SPINE API — DAILY NOTES, SETUP & RETROSPECTIVE
=====================================================================
Author: Shravan Ashok
Project: Spine (Book Library & Social Tracker)
Coverage: Sprint 1 & Sprint 2 (Days 1–4)
=====================================================================


=====================================================================
PART 0 — COMPLETE SETUP INSTRUCTIONS FROM SCRATCH
=====================================================================

--- 0.1 Prerequisites ---
1. .NET 10 SDK (dotnet --version should print 10.x.x)
2. Docker Desktop (Required for local Kafka)
3. Neon Account (Free Serverless PostgreSQL)

--- 0.2 Configure Secrets (User Secrets) ---
Run these in the root `Spine` folder:
  dotnet user-secrets init --project Spine.Api
  dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" --project Spine.Api
  dotnet user-secrets set "Jwt:Key" "thisIsMySuperSecretJwtKeyForSpineApp_ChangeMe_12345" --project Spine.Api

--- 0.3 Start Kafka ---
We use Docker to run a local Kafka broker and UI.
  docker compose up -d
Access Kafka UI at: http://localhost:8080

--- 0.4 Apply Migrations ---
  dotnet ef database update --project Spine.Infrastructure --startup-project Spine.Api

--- 0.5 Run the Application ---
Because of Event-Driven Architecture, you need TWO terminals:
Terminal 1 (API):    dotnet run --project Spine.Api
Terminal 2 (Worker): dotnet run --project Spine.Worker


=====================================================================
PART 1 — SPRINT 1: DATABASE & AUTHENTICATION
=====================================================================

--- Day 1: Database Schema & Foundation ---
What we did: Designed 9 complex relational tables and applied the initial migration to Neon.

Key Architectural Decisions:
1. **Global Catalog Pattern:** We created a `Books` table (Global) and a `UserBooks` table (Join). This prevents saving "Harry Potter" 1,000 times for 1,000 users. It's saved once, and users link to it. This drastically reduces database size and makes searching faster.
2. **Soft Deletes:** `UserBooks` has `IsDeleted` and `DeletedAt`. We never hard-delete user data immediately. This allows for a "Trash Bin" feature.
3. **Self-Referencing Graph:** The `Friendships` table has TWO foreign keys to `Users` (`RequesterId` and `ReceiverId`). 
4. **Many-to-Many:** `Tags` to `UserBooks` requires an automatic join table (`UserBookTags`).

--- Day 2 & 3: Auth & Account Management ---
What we did: Built JWT auth, BCrypt hashing, and self-service account updates.

Key Architectural Decisions:
1. **JWT (JSON Web Tokens):** Stateless authentication. The token contains the User's ID (`sub` claim) and Email.
2. **BCrypt:** Passwords are hashed with a work factor. Never stored in plain text.
3. **FluentValidation:** Kept validation strictly to *formatting* (e.g., password length). Database checks (like "is this email already taken?") were kept in the Controller.
4. **Integration Tests:** Wrote 9 tests covering the happy paths, wrong passwords, and unauthorized access.

---

=====================================================================
PART 2 — SPRINT 2: EVENT-DRIVEN ARCHITECTURE (KAFKA)
=====================================================================

--- Day 1–3: Kafka Producer & Consumer ---
What we did: Decoupled the email-sending process from the HTTP request using Apache Kafka.

Why Event-Driven?
If the API sends a password reset email synchronously (SMTP), the user waits 2–3 seconds. If the email provider (SendGrid/Resend) is down, the request times out or crashes. 
With Kafka:
1. API saves token to DB.
2. API drops a JSON message in Kafka.
3. API returns `200 OK` instantly (takes ~50ms).
4. `Spine.Worker` (a separate background process) reads Kafka and sends the email.

How we built it:
- **Producer:** `KafkaProducerService` uses `IProducer<Null, string>` to publish a `PasswordResetRequestedEvent` to the `password-reset-events` topic.
- **Consumer:** `Spine.Worker` inherits from `BackgroundService`. It sets up a `ConsumerConfig` with `GroupId = "spine-email-worker-group"` and `AutoOffsetReset = Earliest`. It listens in an infinite `while` loop and logs the messages.


=====================================================================
PART 3 — PROBLEMS FACED AND HOW WE FIXED THEM
=====================================================================

1. **Docker CLI Error: `docker-compose: not recognized`**
   - Cause: Docker Desktop recently replaced the `docker-compose` command with `docker compose`.
   - Fix: Run `docker compose up -d` (with a space).

2. **Docker Error: `docker: not recognized`**
   - Cause: Docker Desktop wasn't installed, or the terminal didn't have the PATH updated.
   - Fix: Installed Docker Desktop, restarted VS Code, and added `C:\Program Files\Docker\Docker\resources\bin` to the system PATH.

3. **FluentValidation Error: `AsyncValidatorInvokedSynchronouslyException`**
   - Cause: We tried to use `.MustAsync()` to check if an email existed in the DB inside `RegisterRequestValidator`. ASP.NET's auto-validation pipeline is synchronous and crashes on async rules.
   - Fix: Removed `.MustAsync()` from the validator. Moved the database duplicate check into `AuthController.Register` using `await _db.Users.AnyAsync(...)`.

4. **Test Error: `Npgsql` and `InMemory` providers registered together**
   - Cause: `WebApplicationFactory` starts `Program.cs` (which registers Npgsql) and then the test factory tries to add InMemory. EF Core refuses to have two database providers.
   - Fix: Added `if (!builder.Environment.IsEnvironment("Testing"))` around the Npgsql registration in `Program.cs`. The test factory sets the environment to "Testing", so Npgsql is skipped entirely.

5. **Test Error: `Register 200` followed by `Login 401`**
   - Cause: `options.UseInMemoryDatabase("Db_" + Guid.NewGuid())` was inside a lambda. The Guid ran on EVERY request, creating a fresh, empty database for every single HTTP call.
   - Fix: Captured the DB name *outside* the lambda: `var dbName = "Db_" + Guid.NewGuid();` so it stays consistent for the whole test run.

6. **Build Error: `GetCurrentUserId` does not exist**
   - Cause: We accidentally pasted the `AccountController` code (which uses JWT claims) into `AuthController` (which doesn't require auth).
   - Fix: Moved the `GetCurrentUserId()` helper and the `[Authorize]` protected endpoints back into `AccountController`.

7. **Build Error: `The type or namespace name 'UpdateProfileRequest' could not be found`**
   - Cause: Forgot to create the DTO files for the Account endpoints.
   - Fix: Created `UpdateProfileRequest.cs`, `UpdateEmailRequest.cs`, and `UpdatePasswordRequest.cs` in `Spine.Api/Models`.

8. **Error: `The file is locked by "Spine.Api"`**
   - Cause: `dotnet watch` was running in another terminal, locking the `.dll` files so the test runner couldn't compile.
   - Fix: Stopped the API (Ctrl+C) before running `dotnet test`.

9. **Test Error: `Login_ValidCredentials_ReturnsToken` failed**
   - Cause: The `TestHelpers.RegisterAndGetTokenAsync()` method registered the user, but the returned token wasn't being attached to the client correctly.
   - Fix: Used `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);` inside the helper.

10. **Warning: `MSB3277: Found conflicts between different versions of Microsoft.EntityFrameworkCore.Relational`**
    - Cause: `Npgsql.EntityFrameworkCore.PostgreSQL` pulled in EF Core 10.0.4, while the rest of the project used 10.0.13.
    - Fix: Ignored for now (harmless). In production, you can fix it by adding an explicit `<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.13" />` to `Spine.Infrastructure.csproj`.

11. **README File Encoding (Mojibake)**
    - Cause: Saving the markdown file with Windows-1252 encoding turned emojis (🌐) and tree branches (├──) into gibberish (рџЊђ, в”њв”Ђв”Ђ).
    - Fix: In VS Code, clicked the encoding in the bottom right, chose "Save with Encoding", and selected "UTF-8".


=====================================================================
PART 4 — WHAT WENT WELL & KEY TAKEAWAYS
=====================================================================

What Went Well:
- **Architecture:** The separation of `Spine.Api` and `Spine.Worker` works perfectly. It's a real-world microservice pattern.
- **Event-Driven Resilience:** We proved that if the API receives a request, it drops it in Kafka and returns instantly. Even if the worker crashes, the message is safe in Kafka and will be processed when it restarts.
- **Testing:** The integration tests caught several bugs (like the InMemory database reset issue) before they ever hit production.
- **Security:** Using User Secrets and `[Authorize]` attributes means we have zero secrets in Git, and data is protected by default.

Key Takeaways for the Future:
- Never put database logic inside a FluentValidation rule. Use the Controller.
- In .NET testing, environment variables and class-level variables must be carefully managed so state doesn't accidentally reset between HTTP calls.
- Kafka is not just for Big Data; it's a powerful tool for any app that needs to do background work (like sending emails) without blocking the user.

=====================================================================
END OF NOTES
=====================================================================
