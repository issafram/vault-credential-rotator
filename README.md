
# vault-credential-rotator
A tool used in conjunction with **HashiCorp Vault** (https://www.vaultproject.io/) to rotate access keys & secret access keys 
for the AWS secrets engine within Vault.

## System Requirements

 - HashiCorp Vault Instance w/ API access
 - [LDAP configured](https://www.vaultproject.io/docs/auth/ldap.html) for authentication
 - [.NET Core 2.2 Runtime](https://dotnet.microsoft.com/download/dotnet-core/2.2)
 
## Roadmap 
 - Additional secrets engine configurations (Microsoft Azure, Google Cloud Platform)
 - AppRole Support
 - Lease TTL warning & awareness

## Configuration
Edit the appsettings.json file and add values for the following:

 - BaseClientTokenUri
 - BaseCredentialsUri
 - ValidateServerCertificates
 - Roles

For example, it may look like this:

    {
      "AWS": {
        "BaseClientTokenUri": "https://vault.contoso.com/v1/auth/ldap/login/",
        "BaseCredentialsUri": "https://vault.contoso.com/v1/aws/creds/",
        "ValidateServerCertificates": "true",
        "Roles": [
          "grp-aws-r-contoso-powerusers"
        ]
      }
    }

Once these parameters have been set in the appsettings.json file, run it with:

    dotnet VaultCredentialRotator.dll
