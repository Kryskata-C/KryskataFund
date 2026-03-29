using KryskataFund.Models;
using KryskataFund.Constants;
using KryskataFund.Services;
using System.Security.Cryptography;
using System.Text;

namespace KryskataFund.Data
{
    public static class DbSeeder
    {

        public static void Seed(ApplicationDbContext context)
        {
            if (context.Users.Any() && context.Funds.Any())
                return;

            var users = SeedUsers(context);
            var funds = SeedFunds(context, users);
            SeedDonations(context, funds, users);
            SeedFundUpdates(context, funds);
            SeedFundMilestones(context, funds);

            context.SaveChanges();
        }

        private static List<User> SeedUsers(ApplicationDbContext context)
        {
            if (context.Users.Any())
                return context.Users.ToList();

            var users = new List<User>
            {
                new User { Email = "admin@kryskatafund.com", PasswordHash = PasswordHasher.HashPassword("admin"), IsAdmin = true, CreatedAt = DateTime.UtcNow.AddDays(-90) },
                new User { Email = "maria.ivanova@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new User { Email = "stefan.petrov@yahoo.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-55) },
                new User { Email = "elena.dimitrova@abv.bg", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-50) },
                new User { Email = "ivan.kolev@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-45) },
                new User { Email = "nikoleta.georgieva@mail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-40) },
                new User { Email = "dimitar.todorov@outlook.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-35) },
                new User { Email = "viktoria.stoyanova@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new User { Email = "alex.marinov@proton.me", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-25) },
                new User { Email = "desislava.hristova@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new User { Email = "georgi.angelov@yahoo.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-15) },
                new User { Email = "kristina.vasileva@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-12) },
                new User { Email = "boyan.iliev@abv.bg", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new User { Email = "radostina.pencheva@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new User { Email = "plamen.kostov@yahoo.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-7) },
                new User { Email = "tsvetana.nikolova@outlook.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new User { Email = "yordan.mitev@proton.me", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new User { Email = "silvia.atanasova@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-4) },
                new User { Email = "krasimir.dzhambov@abv.bg", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new User { Email = "milena.stoeva@gmail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new User { Email = "valentin.boyanov@mail.com", PasswordHash = PasswordHasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow.AddDays(-1) },
            };

            context.Users.AddRange(users);
            context.SaveChanges();
            return users;
        }

        private static List<Fund> SeedFunds(ApplicationDbContext context, List<User> users)
        {
            if (context.Funds.Any())
                return context.Funds.ToList();

            var funds = new List<Fund>
            {
                new Fund
                {
                    Title = "Help Stray Dogs in Plovdiv",
                    Description = "Our mission is to rescue, vaccinate, and find loving homes for stray dogs roaming the streets of Plovdiv. Every donation helps provide food, medical care, and shelter for these animals in need.",
                    Category = "Animals",
                    GoalAmount = 8000,
                    RaisedAmount = 5200,
                    SupportersCount = 7,
                    CreatorId = users[1].Id,
                    CreatorName = "@maria.ivanova",
                    ImageUrl = "https://images.unsplash.com/photo-1548199973-03cce0bbc87b?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-20),
                    EndDate = DateTime.UtcNow.AddDays(40),
                    CategoryColor = CategoryColors.Colors["Animals"]
                },
                new Fund
                {
                    Title = "Robotics Club for Village Schools",
                    Description = "We want to bring robotics education to rural schools in Bulgaria. The funds will be used to purchase Arduino kits, 3D printers, and training materials for teachers in 5 village schools.",
                    Category = "Education",
                    GoalAmount = 12000,
                    RaisedAmount = 3600,
                    SupportersCount = 6,
                    CreatorId = users[2].Id,
                    CreatorName = "@stefan.petrov",
                    ImageUrl = "https://images.unsplash.com/photo-1485827404703-89b55fcc595e?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    EndDate = DateTime.UtcNow.AddDays(45),
                    CategoryColor = CategoryColors.Colors["Education"]
                },
                new Fund
                {
                    Title = "Emergency Surgery for Baby Lina",
                    Description = "Baby Lina needs urgent heart surgery abroad. Her family cannot afford the treatment costs. Every lev counts towards saving this precious little girl's life. Please help us reach our goal.",
                    Category = "Health",
                    GoalAmount = 25000,
                    RaisedAmount = 21250,
                    SupportersCount = 8,
                    CreatorId = users[3].Id,
                    CreatorName = "@elena.dimitrova",
                    ImageUrl = "https://images.unsplash.com/photo-1584515933487-779824d29309?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-25),
                    EndDate = DateTime.UtcNow.AddDays(35),
                    CategoryColor = CategoryColors.Colors["Health"]
                },
                new Fund
                {
                    Title = "Street Art Festival Burgas 2026",
                    Description = "Join us in transforming Burgas into an open-air gallery! We are organizing a street art festival featuring local and international artists who will create murals across the city center.",
                    Category = "Creative",
                    GoalAmount = 6000,
                    RaisedAmount = 5700,
                    SupportersCount = 7,
                    CreatorId = users[4].Id,
                    CreatorName = "@ivan.kolev",
                    ImageUrl = "https://images.unsplash.com/photo-1499781350541-7783f6c6a0c8?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-18),
                    EndDate = DateTime.UtcNow.AddDays(42),
                    CategoryColor = CategoryColors.Colors["Creative"]
                },
                new Fund
                {
                    Title = "Solar Panels for Grandma Penka",
                    Description = "Grandma Penka lives alone in a small village and struggles to pay her electricity bills. Let's help her install solar panels so she can live comfortably and independently.",
                    Category = "Community",
                    GoalAmount = 15000,
                    RaisedAmount = 6750,
                    SupportersCount = 6,
                    CreatorId = users[5].Id,
                    CreatorName = "@nikoleta.georgieva",
                    ImageUrl = "https://images.unsplash.com/photo-1509391366360-2e959784a276?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-22),
                    EndDate = DateTime.UtcNow.AddDays(38),
                    CategoryColor = CategoryColors.Colors["Community"]
                },
                new Fund
                {
                    Title = "Build a Skatepark in Varna",
                    Description = "Varna's skating community has been dreaming of a proper skatepark for years. Help us build a modern concrete skatepark near the Sea Garden that everyone can enjoy for free!",
                    Category = "Just for fun",
                    GoalAmount = 20000,
                    RaisedAmount = 17000,
                    SupportersCount = 8,
                    CreatorId = users[6].Id,
                    CreatorName = "@dimitar.todorov",
                    ImageUrl = "https://images.unsplash.com/photo-1564429238961-bf8e8a5a0670?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow.AddDays(30),
                    CategoryColor = CategoryColors.Colors["Just for fun"]
                },
                new Fund
                {
                    Title = "AI Study Group Laptops",
                    Description = "Our university AI study group needs better hardware to train machine learning models. Help us buy 5 laptops with dedicated GPUs so students can learn and experiment with AI.",
                    Category = "Technology",
                    GoalAmount = 5000,
                    RaisedAmount = 4750,
                    SupportersCount = 6,
                    CreatorId = users[7].Id,
                    CreatorName = "@viktoria.stoyanova",
                    ImageUrl = "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-12),
                    EndDate = DateTime.UtcNow.AddDays(48),
                    CategoryColor = CategoryColors.Colors["Technology"]
                },
                new Fund
                {
                    Title = "Organic Honey Startup",
                    Description = "I dream of starting an organic honey business in the Rhodope Mountains. The funds will help me purchase beehives, equipment, and get organic certification for my first harvest.",
                    Category = "Dreams",
                    GoalAmount = 3500,
                    RaisedAmount = 1575,
                    SupportersCount = 5,
                    CreatorId = users[8].Id,
                    CreatorName = "@alex.marinov",
                    ImageUrl = "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    EndDate = DateTime.UtcNow.AddDays(50),
                    CategoryColor = CategoryColors.Colors["Dreams"]
                },
                new Fund
                {
                    Title = "Free Coding Bootcamp for Teens",
                    Description = "We are launching a free 8-week coding bootcamp for teenagers aged 14-18. Funds cover instructor salaries, venue rental, and providing each student with course materials and snacks.",
                    Category = "Education",
                    GoalAmount = 9500,
                    RaisedAmount = 7125,
                    SupportersCount = 7,
                    CreatorId = users[9].Id,
                    CreatorName = "@desislava.hristova",
                    ImageUrl = "https://images.unsplash.com/photo-1515879218367-8466d910auj7?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-14),
                    EndDate = DateTime.UtcNow.AddDays(46),
                    CategoryColor = CategoryColors.Colors["Education"]
                },
                new Fund
                {
                    Title = "Mountain Rescue Dog Training",
                    Description = "Help us train 3 rescue dogs for mountain search and rescue operations in the Balkans. Funds cover professional training, equipment, GPS collars, and veterinary care for a full year.",
                    Category = "Animals",
                    GoalAmount = 7000,
                    RaisedAmount = 5250,
                    SupportersCount = 7,
                    CreatorId = users[10].Id,
                    CreatorName = "@georgi.angelov",
                    ImageUrl = "https://images.unsplash.com/photo-1587300003388-59208cc962cb?w=800&h=450&fit=crop",
                    CreatedAt = DateTime.UtcNow.AddDays(-17),
                    EndDate = DateTime.UtcNow.AddDays(43),
                    CategoryColor = CategoryColors.Colors["Animals"]
                },
            };

            context.Funds.AddRange(funds);
            context.SaveChanges();
            return funds;
        }

        private static void SeedDonations(ApplicationDbContext context, List<Fund> funds, List<User> users)
        {
            if (context.Donations.Any())
                return;

            var donations = new List<Donation>();

            // Fund 0: Help Stray Dogs in Plovdiv (raised 5200, 7 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[0].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-18) },
                new Donation { FundId = funds[0].Id, UserId = users[4].Id, DonorName = "@ivan.kolev", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-16) },
                new Donation { FundId = funds[0].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 200, CreatedAt = DateTime.UtcNow.AddDays(-14) },
                new Donation { FundId = funds[0].Id, UserId = users[7].Id, DonorName = "@viktoria.stoyanova", Amount = 1500, CreatedAt = DateTime.UtcNow.AddDays(-12) },
                new Donation { FundId = funds[0].Id, UserId = users[8].Id, DonorName = "@alex.marinov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Donation { FundId = funds[0].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Donation { FundId = funds[0].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            });

            // Fund 1: Robotics Club for Village Schools (raised 3600, 6 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[1].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-13) },
                new Donation { FundId = funds[1].Id, UserId = users[3].Id, DonorName = "@elena.dimitrova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-11) },
                new Donation { FundId = funds[1].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 200, CreatedAt = DateTime.UtcNow.AddDays(-9) },
                new Donation { FundId = funds[1].Id, UserId = users[6].Id, DonorName = "@dimitar.todorov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-7) },
                new Donation { FundId = funds[1].Id, UserId = users[8].Id, DonorName = "@alex.marinov", Amount = 900, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new Donation { FundId = funds[1].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            });

            // Fund 2: Emergency Surgery for Baby Lina (raised 21250, 8 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[2].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-23) },
                new Donation { FundId = funds[2].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 5000, CreatedAt = DateTime.UtcNow.AddDays(-21) },
                new Donation { FundId = funds[2].Id, UserId = users[4].Id, DonorName = "@ivan.kolev", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-19) },
                new Donation { FundId = funds[2].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 3000, CreatedAt = DateTime.UtcNow.AddDays(-17) },
                new Donation { FundId = funds[2].Id, UserId = users[6].Id, DonorName = "@dimitar.todorov", Amount = 2500, CreatedAt = DateTime.UtcNow.AddDays(-15) },
                new Donation { FundId = funds[2].Id, UserId = users[7].Id, DonorName = "@viktoria.stoyanova", Amount = 2750, CreatedAt = DateTime.UtcNow.AddDays(-13) },
                new Donation { FundId = funds[2].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 3000, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Donation { FundId = funds[2].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-7) },
            });

            // Fund 3: Street Art Festival Burgas 2026 (raised 5700, 7 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[3].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-16) },
                new Donation { FundId = funds[3].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-14) },
                new Donation { FundId = funds[3].Id, UserId = users[3].Id, DonorName = "@elena.dimitrova", Amount = 700, CreatedAt = DateTime.UtcNow.AddDays(-12) },
                new Donation { FundId = funds[3].Id, UserId = users[6].Id, DonorName = "@dimitar.todorov", Amount = 1500, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Donation { FundId = funds[3].Id, UserId = users[7].Id, DonorName = "@viktoria.stoyanova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Donation { FundId = funds[3].Id, UserId = users[8].Id, DonorName = "@alex.marinov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new Donation { FundId = funds[3].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            });

            // Fund 4: Solar Panels for Grandma Penka (raised 6750, 6 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[4].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new Donation { FundId = funds[4].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-17) },
                new Donation { FundId = funds[4].Id, UserId = users[3].Id, DonorName = "@elena.dimitrova", Amount = 750, CreatedAt = DateTime.UtcNow.AddDays(-14) },
                new Donation { FundId = funds[4].Id, UserId = users[6].Id, DonorName = "@dimitar.todorov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-11) },
                new Donation { FundId = funds[4].Id, UserId = users[8].Id, DonorName = "@alex.marinov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Donation { FundId = funds[4].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 1500, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            });

            // Fund 5: Build a Skatepark in Varna (raised 17000, 8 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[5].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-28) },
                new Donation { FundId = funds[5].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 3000, CreatedAt = DateTime.UtcNow.AddDays(-25) },
                new Donation { FundId = funds[5].Id, UserId = users[3].Id, DonorName = "@elena.dimitrova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-22) },
                new Donation { FundId = funds[5].Id, UserId = users[4].Id, DonorName = "@ivan.kolev", Amount = 2500, CreatedAt = DateTime.UtcNow.AddDays(-19) },
                new Donation { FundId = funds[5].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 1500, CreatedAt = DateTime.UtcNow.AddDays(-16) },
                new Donation { FundId = funds[5].Id, UserId = users[8].Id, DonorName = "@alex.marinov", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-13) },
                new Donation { FundId = funds[5].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 3000, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Donation { FundId = funds[5].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-7) },
            });

            // Fund 6: AI Study Group Laptops (raised 4750, 6 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[6].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Donation { FundId = funds[6].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Donation { FundId = funds[6].Id, UserId = users[4].Id, DonorName = "@ivan.kolev", Amount = 1250, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new Donation { FundId = funds[6].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-4) },
                new Donation { FundId = funds[6].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new Donation { FundId = funds[6].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            });

            // Fund 7: Organic Honey Startup (raised 1575, 5 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[7].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 100, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Donation { FundId = funds[7].Id, UserId = users[3].Id, DonorName = "@elena.dimitrova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new Donation { FundId = funds[7].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 250, CreatedAt = DateTime.UtcNow.AddDays(-4) },
                new Donation { FundId = funds[7].Id, UserId = users[6].Id, DonorName = "@dimitar.todorov", Amount = 225, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new Donation { FundId = funds[7].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            });

            // Fund 8: Free Coding Bootcamp for Teens (raised 7125, 7 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[8].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-12) },
                new Donation { FundId = funds[8].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 1500, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Donation { FundId = funds[8].Id, UserId = users[4].Id, DonorName = "@ivan.kolev", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Donation { FundId = funds[8].Id, UserId = users[5].Id, DonorName = "@nikoleta.georgieva", Amount = 1125, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new Donation { FundId = funds[8].Id, UserId = users[7].Id, DonorName = "@viktoria.stoyanova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-4) },
                new Donation { FundId = funds[8].Id, UserId = users[8].Id, DonorName = "@alex.marinov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new Donation { FundId = funds[8].Id, UserId = users[10].Id, DonorName = "@georgi.angelov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            });

            // Fund 9: Mountain Rescue Dog Training (raised 5250, 7 donations)
            donations.AddRange(new[]
            {
                new Donation { FundId = funds[9].Id, UserId = users[1].Id, DonorName = "@maria.ivanova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-15) },
                new Donation { FundId = funds[9].Id, UserId = users[2].Id, DonorName = "@stefan.petrov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-13) },
                new Donation { FundId = funds[9].Id, UserId = users[3].Id, DonorName = "@elena.dimitrova", Amount = 750, CreatedAt = DateTime.UtcNow.AddDays(-11) },
                new Donation { FundId = funds[9].Id, UserId = users[4].Id, DonorName = "@ivan.kolev", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-9) },
                new Donation { FundId = funds[9].Id, UserId = users[6].Id, DonorName = "@dimitar.todorov", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-7) },
                new Donation { FundId = funds[9].Id, UserId = users[7].Id, DonorName = "@viktoria.stoyanova", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new Donation { FundId = funds[9].Id, UserId = users[9].Id, DonorName = "@desislava.hristova", Amount = 1000, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            });

            context.Donations.AddRange(donations);
        }

        private static void SeedFundUpdates(ApplicationDbContext context, List<Fund> funds)
        {
            if (context.FundUpdates.Any())
                return;

            var updates = new List<FundUpdate>
            {
                new FundUpdate
                {
                    FundId = funds[0].Id,
                    Title = "First 10 dogs rescued!",
                    Content = "Thanks to your generous donations, we have rescued and vaccinated 10 stray dogs so far. Three of them already found forever homes!",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                },
                new FundUpdate
                {
                    FundId = funds[2].Id,
                    Title = "Surgery date confirmed",
                    Content = "We are happy to announce that Baby Lina's surgery has been scheduled for next month. The hospital in Vienna has confirmed the date. We still need to reach our goal for post-operative care.",
                    CreatedAt = DateTime.UtcNow.AddDays(-8)
                },
                new FundUpdate
                {
                    FundId = funds[2].Id,
                    Title = "Lina is getting stronger",
                    Content = "Little Lina had a great check-up today. The doctors are optimistic about the surgery outcome. Thank you all for the overwhelming support!",
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new FundUpdate
                {
                    FundId = funds[5].Id,
                    Title = "Location approved by municipality",
                    Content = "Great news! The Varna municipality has approved our proposed location near the Sea Garden. Construction plans are being finalized.",
                    CreatedAt = DateTime.UtcNow.AddDays(-12)
                },
                new FundUpdate
                {
                    FundId = funds[6].Id,
                    Title = "Almost there!",
                    Content = "We are at 95% of our goal! The laptops have been selected and we are ready to place the order as soon as we reach the target.",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new FundUpdate
                {
                    FundId = funds[8].Id,
                    Title = "Instructors confirmed",
                    Content = "We have confirmed 3 experienced software developers as volunteer instructors for the bootcamp. The curriculum will cover HTML, CSS, JavaScript, and Python basics.",
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new FundUpdate
                {
                    FundId = funds[9].Id,
                    Title = "Training has begun",
                    Content = "Our three rescue dogs - Rex, Luna, and Balkan - have started their professional training program. They are making great progress with basic search patterns!",
                    CreatedAt = DateTime.UtcNow.AddDays(-9)
                },
            };

            context.FundUpdates.AddRange(updates);
        }

        private static void SeedFundMilestones(ApplicationDbContext context, List<Fund> funds)
        {
            if (context.FundMilestones.Any())
                return;

            var milestones = new List<FundMilestone>
            {
                // Help Stray Dogs - raised 5200
                new FundMilestone
                {
                    FundId = funds[0].Id,
                    Title = "First rescue batch - food and vaccines",
                    TargetAmount = 2000,
                    Description = "Purchase food supplies and vaccines for the first 10 dogs.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-14),
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                },
                new FundMilestone
                {
                    FundId = funds[0].Id,
                    Title = "Temporary shelter setup",
                    TargetAmount = 5000,
                    Description = "Set up a temporary shelter with kennels and heating.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-5),
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                },
                new FundMilestone
                {
                    FundId = funds[0].Id,
                    Title = "Sterilization program",
                    TargetAmount = 8000,
                    Description = "Fund sterilization procedures for all rescued dogs.",
                    IsReached = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                },

                // Emergency Surgery for Baby Lina - raised 21250
                new FundMilestone
                {
                    FundId = funds[2].Id,
                    Title = "Initial medical consultations",
                    TargetAmount = 5000,
                    Description = "Cover costs for specialist consultations and diagnostic tests.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-20),
                    CreatedAt = DateTime.UtcNow.AddDays(-25)
                },
                new FundMilestone
                {
                    FundId = funds[2].Id,
                    Title = "Surgery costs covered",
                    TargetAmount = 18000,
                    Description = "Cover the full surgery costs at the Vienna clinic.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-8),
                    CreatedAt = DateTime.UtcNow.AddDays(-25)
                },
                new FundMilestone
                {
                    FundId = funds[2].Id,
                    Title = "Post-operative care fund",
                    TargetAmount = 25000,
                    Description = "Cover rehabilitation and follow-up visits after surgery.",
                    IsReached = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-25)
                },

                // Build a Skatepark in Varna - raised 17000
                new FundMilestone
                {
                    FundId = funds[5].Id,
                    Title = "Design and permits",
                    TargetAmount = 5000,
                    Description = "Professional skatepark design and municipal permits.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-18),
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                },
                new FundMilestone
                {
                    FundId = funds[5].Id,
                    Title = "Foundation and basic ramps",
                    TargetAmount = 15000,
                    Description = "Concrete foundation and construction of basic ramp structures.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-8),
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                },

                // Free Coding Bootcamp for Teens - raised 7125
                new FundMilestone
                {
                    FundId = funds[8].Id,
                    Title = "Venue and materials secured",
                    TargetAmount = 4000,
                    Description = "Rent the venue and purchase all course materials.",
                    IsReached = true,
                    ReachedAt = DateTime.UtcNow.AddDays(-6),
                    CreatedAt = DateTime.UtcNow.AddDays(-14)
                },
                new FundMilestone
                {
                    FundId = funds[8].Id,
                    Title = "Instructor compensation",
                    TargetAmount = 8000,
                    Description = "Compensate instructors for their 8 weeks of teaching.",
                    IsReached = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-14)
                },
            };

            context.FundMilestones.AddRange(milestones);
        }
    }
}
