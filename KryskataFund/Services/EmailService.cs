using System.Text;
using System.Text.Json;
using KryskataFund.Services.Interfaces;

namespace KryskataFund.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration configuration)
        {
            _apiKey = configuration["Resend:ApiKey"] ?? "";
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        private async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                var payload = new
                {
                    from = "KryskataFund <noreply@kryskatafund.com>",
                    to = new[] { to },
                    subject,
                    html = htmlBody
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("https://api.resend.com/emails", content);
            }
            catch
            {
                // Don't fail the donation if email fails
            }
        }

        public async Task SendDonationConfirmationAsync(string toEmail, string fundTitle, decimal amount)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(135deg, #0a0a0f, #1a1a2e); padding: 30px; border-radius: 16px; color: #fff;'>
                        <h1 style='color: #4ade80; margin: 0 0 10px;'>Thank you for your donation!</h1>
                        <p style='color: #a0a3c4; font-size: 16px;'>Your support makes a difference.</p>
                        <div style='background: rgba(74, 222, 128, 0.1); border: 1px solid rgba(74, 222, 128, 0.2); border-radius: 12px; padding: 20px; margin: 20px 0;'>
                            <p style='margin: 0 0 8px; color: #a0a3c4; font-size: 14px;'>Amount donated</p>
                            <p style='margin: 0; color: #4ade80; font-size: 32px; font-weight: 700;'>&euro;{amount:N2}</p>
                        </div>
                        <p style='color: #e0e0f0; font-size: 15px;'>Campaign: <strong>{fundTitle}</strong></p>
                        <p style='color: #a0a3c4; font-size: 13px; margin-top: 20px;'>A receipt for this transaction has been recorded in your KryskataFund account.</p>
                        <hr style='border: none; border-top: 1px solid rgba(255,255,255,0.1); margin: 20px 0;'>
                        <p style='color: #6d6f8c; font-size: 12px; margin: 0;'>KryskataFund - Empowering dreams, one donation at a time.</p>
                    </div>
                </div>";

            await SendEmailAsync(toEmail, $"Donation Confirmed - \u20AC{amount:N2} to {fundTitle}", html);
        }

        public async Task SendDonationReceivedAsync(string toEmail, string donorName, string fundTitle, decimal amount)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(135deg, #0a0a0f, #1a1a2e); padding: 30px; border-radius: 16px; color: #fff;'>
                        <h1 style='color: #22d3ee; margin: 0 0 10px;'>You received a donation!</h1>
                        <p style='color: #a0a3c4; font-size: 16px;'>Someone believes in your campaign.</p>
                        <div style='background: rgba(34, 211, 238, 0.1); border: 1px solid rgba(34, 211, 238, 0.2); border-radius: 12px; padding: 20px; margin: 20px 0;'>
                            <p style='margin: 0 0 8px; color: #a0a3c4; font-size: 14px;'>Amount received</p>
                            <p style='margin: 0; color: #22d3ee; font-size: 32px; font-weight: 700;'>&euro;{amount:N2}</p>
                        </div>
                        <p style='color: #e0e0f0; font-size: 15px;'>From: <strong>{donorName}</strong></p>
                        <p style='color: #e0e0f0; font-size: 15px;'>Campaign: <strong>{fundTitle}</strong></p>
                        <hr style='border: none; border-top: 1px solid rgba(255,255,255,0.1); margin: 20px 0;'>
                        <p style='color: #6d6f8c; font-size: 12px; margin: 0;'>KryskataFund - Empowering dreams, one donation at a time.</p>
                    </div>
                </div>";

            await SendEmailAsync(toEmail, $"New Donation - {donorName} donated \u20AC{amount:N2} to {fundTitle}", html);
        }
    }
}
