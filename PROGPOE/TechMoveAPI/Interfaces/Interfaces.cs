using TechMoveAPI.Models;

namespace TechMoveAPI.Interfaces
{
    public interface IContractObserver
    {
        void OnContractStatusChanged(Contract contract);
    }

    public interface ICurrencyConverter
    {
        decimal Convert(decimal amount, string fromCurrency, string toCurrency);
        decimal GetExchangeRate(string fromCurrency, string toCurrency);
    }

    public interface IServiceRequestFactory
    {
        ServiceRequest CreateRequest(Dictionary<string, object> data);
        bool ValidateData(Dictionary<string, object> data);
    }
}
