<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white" />
  <img src="https://img.shields.io/badge/Stripe-008CDD?style=for-the-badge&logo=stripe&logoColor=white" />
  <img src="https://img.shields.io/badge/Railway-0B0D0E?style=for-the-badge&logo=railway&logoColor=white" />
</p>

# KryskataFund

A full-featured crowdfunding platform built with ASP.NET Core 8. Create campaigns, accept donations via Stripe, engage your community, and track progress toward your goals — all in one place.

---

## Features

**Fundraising Campaigns**
- Create funds with goals, deadlines, descriptions, and images
- Category-based organization with color coding
- Progress tracking with live percentage and days remaining
- Milestones, updates, and collaborator support
- Embeddable widgets for external sites

**Payments & Donations**
- Stripe Checkout integration for secure payments
- One-time and recurring donation support
- Anonymous donation option
- Email receipts via Resend

**Community**
- User profiles with customizable avatars
- Comments and discussions on funds
- Direct messaging between users
- Follow system and leaderboards
- Fund sharing via messages

**Admin Dashboard**
- Full user, fund, and donation management
- Analytics with category breakdowns
- Fund verification workflow
- Recent activity feed

**Security**
- BCrypt password hashing
- Anti-forgery token validation on all forms
- HTML sanitization (XSS protection)
- Secure session management (HttpOnly, SameSite=Strict)
- Custom `RequireSignIn` and `RequireAdmin` filters

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8.0 (MVC) |
| Database | SQL Server (local) / PostgreSQL (Railway prod) + Entity Framework Core |
| Payments | Stripe |
| Email | Resend |
| Auth | Session-based + BCrypt |
| Testing | xUnit + Moq + FluentAssertions |
| Deploy | GitHub Actions CI/CD + Railway |

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB — ships with Visual Studio, or install standalone via [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads) → "LocalDB"

### Run Locally

```bash
# Clone the repo
git clone https://github.com/your-username/KryskataFund.git
cd KryskataFund

# Restore and run — LocalDB and the database are created automatically on first launch
dotnet restore
dotnet run --project KryskataFund/KryskataFund.csproj
```

The app will be available at `https://localhost:5001`.

Optional — set these secrets if you want Stripe payments and email receipts working:

```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project KryskataFund
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..." --project KryskataFund
dotnet user-secrets set "Resend:ApiKey" "re_..." --project KryskataFund
```

> **Provider note:** The app defaults to SQL Server. When deployed to Railway,
> the `DATABASE_URL` env var is set to a `postgresql://...` URL and the app
> automatically switches to the PostgreSQL provider — no code change needed.

### Run Tests

```bash
dotnet test
```

---

## Project Structure

```
KryskataFund/
├── Controllers/       # 13 MVC controllers
├── Models/            # Entity models (Fund, User, Donation, Message, ...)
├── Services/          # Business logic layer with DI
├── Views/             # Razor templates organized by controller
├── Data/              # DbContext and seeder (schema created via EnsureCreated)
├── Filters/           # Custom auth filters
├── wwwroot/           # Static assets (CSS, JS, images)
├── Dockerfile         # Production image (used by Railway)
└── .github/workflows/ # CI/CD pipeline
KryskataFund.Tests/    # xUnit test suite
```

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `DATABASE_URL` | PostgreSQL connection string |
| `Stripe__SecretKey` | Stripe secret API key |
| `Stripe__PublishableKey` | Stripe publishable key |
| `Stripe__WebhookSecret` | Stripe webhook signing secret |
| `Resend__ApiKey` | Resend email API key |

---

## Deployment

The project ships with a **GitHub Actions** pipeline that builds, tests, and deploys on every push to `master`. Production is hosted on **Railway** with Docker containerization.

---

## License

This project is open source. See [LICENSE](LICENSE) for details.
