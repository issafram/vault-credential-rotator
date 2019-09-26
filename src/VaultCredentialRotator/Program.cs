using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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

            var httpClientBuilder = serviceCollection
                .AddHttpClient<AwsProvider>();

            if (bool.Parse(configuration["AWS:ValidateServerCertificates"]))
            {
                httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
                });
            }   
        }
    }
}