namespace KryskataFund.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendDonationConfirmationAsync(string toEmail, string fundTitle, decimal amount);
        Task SendDonationReceivedAsync(string toEmail, string donorName, string fundTitle, decimal amount);
    }
}
