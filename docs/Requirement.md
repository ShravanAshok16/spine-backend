# Spine — Requirements Document

> Version: 1.0
> Last Updated: 2026-10-24

## 1. Project Overview

Spine is a full-stack, mobile-first application designed to help avid readers digitize their physical book collections, track reading progress, and share their libraries with friends. It consists of a .NET 10 REST API, a React Web App, and a React Native Mobile App (for barcode scanning).

## 2. Functional Requirements

### 2.1 User Management & Auth
- **Registration:** Email, Password, First Name, Last Name, Phone Number.
- **Login:** JWT-based.
- **Account Management:** Users can update their display name, email, and password.
- **Privacy:** Users can set their library to `Public`, `FriendsOnly`, or `Private`.
- **Password Reset:** Event-driven reset link sent via Kafka and Email.

### 2.2 Library Management
- **Add Books:**
  - Scan ISBN barcode via mobile app.
  - Search by Title/Author.
  - Manual entry (if the Open Library API fails).
- **Track Progress:** Mark books as Unread, Reading, Finished, or Did Not Finish (DNF). Track current page.
- **Tags/Shelves:** Users can create custom tags (e.g., "Sci-Fi", "Borrowed", "To Buy").
- **Lending:** Mark a book as "Lent to [Friend's Name]".
- **Export:** Export the library to a CSV file.

### 2.3 Book Details & Discovery
- **Metadata:** View Summary, Page Count, Series info, and Cover Image.
- **Where to Buy:** Generate search links for Amazon and Flipkart using the book's ISBN.
- **Reviews:** Users can leave a public 1-5 star rating and written review. Private notes stay private.

### 2.4 Social Features
- **Friendships:** Send, accept, and decline friend requests.
- **Shared Library:** View friends' libraries based on their privacy settings.
- **Activity Feed:** See when friends finish a book or leave a 5-star review.

## 3. Non-Functional Requirements

### 3.1 Performance
- Barcode scanning must resolve to a book in < 2 seconds (using Redis caching for repeated ISBN scans).
- API responses must be paginated to prevent heavy payloads.

### 3.2 Scalability & Reliability
- Password reset emails must be asynchronous (via Kafka) so the API never blocks.
- Database must handle many-to-many relationships efficiently (Global Catalog pattern).

### 3.3 Security
- Passwords hashed with BCrypt.
- JWT expiration (30 mins).
- Rate limiting on the Forgot Password endpoint.
- Strict data filtering (Private notes must never be sent to the frontend if a friend is viewing the library).

## 4. Out of Scope (For V1)
- E-reader integration.
- Audiobook tracking.
- Selling or trading books directly in the app.
- Admin panel.

## 5. Success Metrics
- Users can scan a book and add it to their library in under 5 seconds.
- Lighthouse score > 90 for the Web Frontend.
- 9 backend integration tests passing in CI/CD.