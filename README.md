<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" />
  <img src="https://img.shields.io/badge/Stripe-008CDD?style=for-the-badge&logo=stripe&logoColor=white" />
  <img src="https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white" />
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
| Database | PostgreSQL + Entity Framework Core |
| Payments | Stripe |
| Email | Resend |
| Auth | Session-based + BCrypt |
| Testing | xUnit + Moq + FluentAssertions |
| Deploy | Docker + GitHub Actions CI/CD + Railway |

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) (or use SQLite for local dev)

### Run Locally

```bash
# Clone the repo
git clone https://github.com/your-username/KryskataFund.git
cd KryskataFund

# Restore dependencies
dotnet restore

# Set up user secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=kryskatafund;Username=postgres;Password=yourpassword"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Resend:ApiKey" "re_..."

# Run the app
dotnet run --project KryskataFund/KryskataFund.csproj
```

The app will be available at `https://localhost:5001`.

### Run with Docker

```bash
docker build -t kryskatafund .
docker run -p 8080:8080 \
  -e DATABASE_URL="postgresql://user:password@host:5432/kryskatafund" \
  -e STRIPE_SECRET_KEY="sk_test_..." \
  kryskatafund
```

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
├── Data/              # DbContext, migrations, and seeder
├── Filters/           # Custom auth filters
├── wwwroot/           # Static assets (CSS, JS, images)
├── Dockerfile         # Multi-stage production build
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
