using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace VaultCredentialRotator
{
    public class AwsProvider : IAwsProvider
    {
        public AwsProvider(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        private static readonly List<char> AcceptableChars = GetAcceptableChars();
        private readonly IHttpClientFactory httpClientFactory;

        public async Task ExecuteAsync(IConfigurationRoot configuration)
        {
            Console.Write("Username: ");
            var username = Console.ReadLine();

            Console.Write("Password: ");
            var passwordStringBuilder = new StringBuilder();

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);

                // Ignore any key out of range.
                if (AcceptableChars.Contains(key.KeyChar))
                {
                    // Append the character to the password.
                    passwordStringBuilder.Append(key.KeyChar);
                    Console.Write("*");
                }

                // Exit if Enter key is pressed.
            } while (key.Key != ConsoleKey.Enter);
            Console.WriteLine();
            //var validateServerCertificates = bool.Parse(configuration["ValidateServerCertificates"]);

            var json = JsonConvert.SerializeObject(new { password = passwordStringBuilder.ToString() });
            var baseClientTokenUri = configuration["AWS:BaseClientTokenUri"];
            baseClientTokenUri = baseClientTokenUri.TrimEnd('/');
            var getClientTokenUri = $@"{baseClientTokenUri}/{username}";

            var clientTokenHttpResponseMessage = await GetClientTokenHttpResponseMessageAsync(getClientTokenUri, json);
            if (!clientTokenHttpResponseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine("Invalid credentials.");
                return;
            }

            var contentString = await clientTokenHttpResponseMessage.Content.ReadAsStringAsync();
            dynamic contentObject = JsonConvert.DeserializeObject(contentString);
            var clientToken = (string)contentObject.auth.client_token;

            Console.WriteLine();

            Console.WriteLine("Roles");
            Console.WriteLine("--------");

            var roleDictionary = new Dictionary<int, string>();
            var roleConfig = configuration.GetSection("AWS:Roles").GetChildren().AsEnumerable().ToList();
            for (int i = 0; i < roleConfig.Count; i++)
            {
                roleDictionary.Add(i + 1, roleConfig[i].Value);
            }


            roleDictionary.ToList().ForEach(x => Console.WriteLine($"{x.Key} - {x.Value}"));
            Console.WriteLine();

            Console.Write("Select a role: ");
            var numberIsEntered = int.TryParse(Console.ReadLine(), out var roleNumber);
            if (!numberIsEntered || !roleDictionary.ContainsKey(roleNumber))
            {
                Console.WriteLine("Invalid role selection.");
                return;
            }

            var awsCredsHttpResponseMessage = await GetAwsCredsAsync(configuration, roleDictionary[roleNumber], clientToken);
            if (!awsCredsHttpResponseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine();
                Console.WriteLine("Access Denied.");
                return;
            }

            var awsCredsContentString = await awsCredsHttpResponseMessage.Content.ReadAsStringAsync();
            dynamic awsCredsDeserializedObject = JsonConvert.DeserializeObject(awsCredsContentString);
            var accessKey = (string)awsCredsDeserializedObject.data.access_key;
            var secretKey = (string)awsCredsDeserializedObject.data.secret_key;
            var leaseID = (string)awsCredsDeserializedObject.lease_id;
            var leaseDuration = (string)awsCredsDeserializedObject.lease_duration;

            Console.WriteLine();
            await SaveCredentialsAsync(accessKey, secretKey);
            Console.WriteLine("Credentials saved!");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Vault Lease ID: {leaseID}");
            Console.WriteLine($"Vault Token Lease Duration: {leaseDuration} seconds");
            Console.ResetColor();
        }

        private async Task<HttpResponseMessage> GetClientTokenHttpResponseMessageAsync(string getClientTokenUri, string json)
        {
            var httpClient = this.httpClientFactory.CreateClient("AwsProvider");
            var result = await httpClient.PostAsync(getClientTokenUri, new StringContent(json, Encoding.UTF8, @"application/json"));

            return result;
        }

        private async Task<HttpResponseMessage> GetAwsCredsAsync(IConfiguration configuration, string role, string clientToken)
        {
            var baseCredentialsUri = configuration["AWS:BaseCredentialsUri"];
            baseCredentialsUri = baseCredentialsUri.TrimEnd('/');
            var getAwsCredsUri = $@"{baseCredentialsUri}/{role}";

            var httpClient = this.httpClientFactory.CreateClient("AwsProvider");
            httpClient.DefaultRequestHeaders.Add("X-Vault-Token", clientToken);
            var result = await httpClient.GetAsync(getAwsCredsUri);

            return result;
        }

        private static async Task SaveCredentialsAsync(string accessKey, string secretKey)
        {
            var path = GetCredentialPath();
            var credentialFile = new FileInfo(path);
            if (!credentialFile.Exists)
            {
                Directory.CreateDirectory(credentialFile.DirectoryName);
                using (File.CreateText(path)) { }
            }

            var contents = (await File.ReadAllLinesAsync(credentialFile.FullName)).ToList();
            var defaultIndex = contents.FindIndex(0, x => x.Contains("[default]"));
            if (defaultIndex == -1)
            {
                contents.Add("[default]");
                contents.Add($"aws_access_key_id = {accessKey}");
                contents.Add($"aws_secret_access_key = {secretKey}");

                await File.WriteAllLinesAsync(credentialFile.FullName, contents);
                return;
            }

            if (
                contents.Count > defaultIndex + 2 &&
                contents[defaultIndex + 1].StartsWith("aws_access_key_id") &&
                contents[defaultIndex + 2].StartsWith("aws_secret_access_key"))
            {
                contents[defaultIndex + 1] = $"aws_access_key_id = {accessKey}";
                contents[defaultIndex + 2] = $"aws_secret_access_key = {secretKey}";

                await File.WriteAllLinesAsync(credentialFile.FullName, contents);
                return;
            }

            if (contents[defaultIndex + 1].StartsWith("aws_access_key_id"))
            {
                contents[defaultIndex + 1] = $"aws_access_key_id = {accessKey}";
                contents.Insert(defaultIndex + 2, $"aws_secret_access_key = {secretKey}");

                await File.WriteAllLinesAsync(credentialFile.FullName, contents);
                return;
            }

            if (contents[defaultIndex + 1].StartsWith("aws_secret_access_key"))
            {
                contents[defaultIndex + 1] = $"aws_secret_access_key = {secretKey}";
                contents.Insert(defaultIndex + 1, $"aws_access_key_id = {accessKey}");

                await File.WriteAllLinesAsync(credentialFile.FullName, contents);
                return;
            }

            await File.WriteAllLinesAsync(credentialFile.FullName, contents);
        }

        private static string GetCredentialPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\.aws\credentials");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return $"{Environment.GetEnvironmentVariable("HOME")}/.aws/credentials";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"{Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")}/.aws/credentials";
            }

            return string.Empty;
        }

        static List<char> GetAcceptableChars()
        {
            var chars = new List<char>();
            chars.AddRange(Range(32, 126).ToList().Select(x => (char)x));

            return chars;
        }

        static IEnumerable<int> Range(int start, int end)
        {
            var lastNumber = end - start + 1;
            return Enumerable.Range(start, lastNumber);
        }
    }
}