# EventPlatform

[![CI](https://github.com/KutayP/EventPlatform/actions/workflows/ci.yml/badge.svg)](https://github.com/KutayP/EventPlatform/actions/workflows/ci.yml)

A full-stack social event platform: users discover and create events, join them, chat with other participants in real time, add friends, and get notified about activity and new events near them that match their interests.

**Live demo:** https://eventplatform-wvls.onrender.com

**Demo login:** `demo` / `Demo1234!` (password, personal info and event editing/deleting are disabled for this account; the sample data is reset from time to time)

> The demo runs on free hosting tiers. If it has been idle, the first request can take up to a minute while the server and database wake up.

![Home page](docs/screenshots/home.png)

## Features

- **Events:** create events with a Google Maps location picker, browse, search and filter by category; readable, non-sequential URLs (`/etkinlik/kadikoy-sahil-yuruyusu-k7x2m9ab`); "My events" page; upcoming events I created or joined are shown on the home page as a reminder
- **Participation and chat:** join / leave events; one real-time chat room per event (SignalR / WebSockets) with message history and unread tracking
- **Profiles and privacy:** public profile pages (`/u/username`) that can be limited to friends only; only first name and last initial are shown, never e-mail, birth date or gender
- **Friends:** friend requests (send, accept, decline, cancel, remove), typo-tolerant user search (PostgreSQL `pg_trgm`, Turkish-character aware), a home feed of events my friends created or joined
- **Notifications:** real-time bell with unread badge (SignalR) for new participants, friend requests (accept / decline right in the dropdown), accepted requests, unread chat messages and recommended events; a full notification history page
- **Recommendations:** users can opt in to notifications for new events in their interests within a chosen distance of their location; the location settings page previews nearby upcoming events
- **Settings:** profile info, interests, profile visibility, notification types, location and radius, appearance (light / dark / follow device, with selectable color palettes), password change with show / hide toggle
- **Moderation:** events publish immediately; admins can unpublish or delete them and read an event's chat in read-only mode

| Events | Event details and chat |
|---|---|
| ![Events](docs/screenshots/events.png) | ![Event details and chat](docs/screenshots/event-details-chat.png) |

| Create event with map | Profile |
|---|---|
| ![Create event](docs/screenshots/event-create-map.png) | ![Profile](docs/screenshots/profile.png) |

| Notifications | Location and nearby events |
|---|---|
| ![Notifications](docs/screenshots/notifications.png) | ![Location settings](docs/screenshots/settings-location.png) |

| Dark theme |
|---|
| ![Dark theme](docs/screenshots/dark-theme.png) |

## Tech stack

| Area | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core MVC, SignalR |
| Data | Entity Framework Core (database-first), PostgreSQL (Neon), `pg_trgm` |
| Frontend | Razor views, Bootstrap 5.3 (color modes), Bootstrap Icons, vanilla JavaScript, flatpickr |
| Maps | Google Maps JavaScript API (Places, Geocoding) |
| Auth | Cookie authentication, BCrypt password hashing |
| Testing / CI | xUnit, GitHub Actions (build, tests, Docker image build) |
| Deployment | Docker, Render |

## Security and privacy

- Passwords hashed with BCrypt; identical error message for unknown user and wrong password
- Anti-forgery tokens on all state-changing requests; open-redirect protection (`Url.IsLocalUrl`)
- Server-side validation with view models (no over-posting; roles and ownership are never taken from forms)
- Ownership and access checks inside the database queries (editing, deleting, notifications, friend requests); someone else's resource returns 404
- Output encoding in Razor and `textContent` in chat to prevent XSS; the notification dropdown is rendered on the server, not built from strings in JavaScript
- Cookies: `HttpOnly`, `Secure`, `SameSite=Lax`, sliding expiration
- Chat authorization re-checked on the server for every hub call (only the creator and participants of a published event)
- Event URLs use a random 8-character code instead of sequential ids
- Profile visibility (public / friends only); the user's location is stored rounded to about 1 km, shown to no one and used only for matching
- Search input is passed only as SQL parameters; `LIKE` wildcards are escaped
- Data-protection keys stored in the database and encrypted with a certificate, so sessions survive restarts
- The public demo account cannot change its password or personal info, or edit / delete events (checked on the server, with a banner on every page)
- All secrets (connection string, API keys, certificate) come from user-secrets locally and environment variables in production; none are committed

## Run locally

Requirements: .NET 10 SDK and a PostgreSQL database (with the `pg_trgm` extension).

```cmd
cd EventPlatform.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your PostgreSQL connection string>"
dotnet user-secrets set "GoogleMaps:ApiKey" "<your Google Maps API key>"
dotnet run
```

Then open http://localhost:5120.

Run the tests from the repository root:

```cmd
dotnet test
```

## Deployment

The app is containerized (`Dockerfile`) and deployed on Render, with PostgreSQL on Neon. Configuration is supplied through environment variables (`ConnectionStrings__DefaultConnection`, `GoogleMaps__ApiKey`, `DataProtection__CertificateBase64`, `DataProtection__CertificatePassword`). `/healthz` is the health-check endpoint. GitHub Actions builds the solution, runs the tests and builds the Docker image on every push to `main`.

## Design notes

Architecture decisions, database choices and the reasoning behind the security and privacy measures are written down in [docs/PROJE-NOTLARI.md](docs/PROJE-NOTLARI.md) (in Turkish).
