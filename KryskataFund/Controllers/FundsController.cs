using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;

namespace KryskataFund.Controllers
{
    public class FundsController : Controller
    {
        // Fake data for now
        private static readonly List<Fund> _fakeFunds = new()
        {
            new Fund
            {
                Id = 1,
                Title = "Send Alex to their first year of Computer Science",
                Description = "Help Alex cover tuition, dorm and a second-hand laptop so they can finally start building the apps they've always dreamed of. Alex has been passionate about coding since age 12, teaching themselves through free online resources. Now they've been accepted to university but need support to make this dream a reality. Every contribution brings them closer to becoming a software developer who can give back to the community.",
                Category = "Education",
                GoalAmount = 10000,
                RaisedAmount = 6800,
                SupportersCount = 182,
                CreatorName = "@alex.codes",
                DaysLeft = 4,
                CategoryColor = "#4ade80"
            },
            new Fund
            {
                Id = 2,
                Title = "Support Sofia's recovery after surgery",
                Description = "Sofia is recovering from a major surgery and needs help with physiotherapy and bills for the next few months. After a successful operation, Sofia now faces months of rehabilitation. The medical bills have been overwhelming, and she needs continued physical therapy to regain full mobility. Your support will help cover her treatment costs and allow her to focus on recovery.",
                Category = "Health",
                GoalAmount = 10000,
                RaisedAmount = 9200,
                SupportersCount = 402,
                CreatorName = "@friendsOfSofia",
                DaysLeft = 1,
                CategoryColor = "#f97316"
            },
            new Fund
            {
                Id = 3,
                Title = "Build a warm shelter for stray dogs in Sofia",
                Description = "Local volunteers are transforming an empty space into a safe, heated shelter for stray dogs before winter hits. Our team has been rescuing and caring for stray dogs for years, but we desperately need a permanent shelter. The funds will cover construction materials, heating equipment, and initial supplies to house up to 30 dogs through the harsh winter months.",
                Category = "Animals",
                GoalAmount = 10000,
                RaisedAmount = 4300,
                SupportersCount = 93,
                CreatorName = "@streetPawsBG",
                DaysLeft = 10,
                CategoryColor = "#22d3ee"
            },
            new Fund
            {
                Id = 4,
                Title = "Short film: \"404 – Dream Not Found\"",
                Description = "A student-made sci-fi short about life, latency and trying to connect when everything keeps buffering. Set in a near-future where human connections are mediated entirely through technology, our protagonist must find a way to reach someone important before the system crashes. This 15-minute film explores themes of isolation, hope, and what it means to truly connect.",
                Category = "Creative",
                GoalAmount = 10000,
                RaisedAmount = 12000,
                SupportersCount = 321,
                CreatorName = "@filmCrew",
                DaysLeft = 0,
                CategoryColor = "#a855f7"
            },
            new Fund
            {
                Id = 5,
                Title = "From bedroom beats to first studio session",
                Description = "Help this bedroom producer book time in a real studio and release their first EP on streaming platforms. I've been making music in my room for 3 years, learning everything from YouTube tutorials. Now I'm ready to take the next step and record my songs professionally. The funds will cover studio time, mixing, mastering, and distribution fees.",
                Category = "Dreams",
                GoalAmount = 5000,
                RaisedAmount = 1750,
                SupportersCount = 64,
                CreatorName = "@lofiRoom",
                DaysLeft = 21,
                CategoryColor = "#facc15"
            },
            new Fund
            {
                Id = 6,
                Title = "Help me finally buy that RGB keyboard",
                Description = "Is it necessary? No. Is it responsible? Also no. Is it extremely on brand for this platform? Absolutely. Look, I've been using the same membrane keyboard since 2015. My friends all have clicky RGB setups and I'm tired of being the only one without the aesthetic. Will this make me a better person? Probably not. Will it make me happier? Definitely.",
                Category = "Just for fun",
                GoalAmount = 1000,
                RaisedAmount = 150,
                SupportersCount = 23,
                CreatorName = "@rgbEnjoyer",
                DaysLeft = 30,
                CategoryColor = "#ef4444"
            }
        };

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Details(int id)
        {
            var fund = _fakeFunds.FirstOrDefault(f => f.Id == id);

            if (fund == null)
            {
                return NotFound();
            }

            return View(fund);
        }
    }
}
