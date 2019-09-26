using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace VaultCredentialRotator
{
    public interface IAwsProvider
    {
        Task ExecuteAsync(IConfigurationRoot configuration);
    }
}