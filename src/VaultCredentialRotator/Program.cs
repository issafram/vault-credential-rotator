using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace VaultCredentialRotator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            Console.WriteLine($"Vault Credential Rotator - Version {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion}");
            Console.WriteLine("Created by Issa Fram");
            Console.WriteLine();

            var provider = serviceProvider.GetService<IAwsProvider>();
            await provider.ExecuteAsync(configuration);
        }

        private static void ConfigureServices(ServiceCollection serviceCollection, IConfiguration configuration)
        {
            serviceCollection
                .AddTransient<IAwsProvider, AwsProvider>();

            /// AWS Provider - Begin
            var httpClientBuilder = 
                serviceCollection
                //.AddHttpClient();
                .AddHttpClient("test");

            if (!bool.Parse(configuration["AWS:ValidateServerCertificates"]))
            {
                httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return false; }
                });
            }
            /// AWS Provider - End
        }
    }
}