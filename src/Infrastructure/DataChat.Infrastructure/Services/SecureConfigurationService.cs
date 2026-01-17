using System.Security.Cryptography;
using System.Text;
using DataChat.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace DataChat.Infrastructure.Services;

public class SecureConfigurationService : ISecureConfigurationService
{
    private readonly IDataProtector _protector;

    public SecureConfigurationService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("DataChat.SecureConfiguration");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (CryptographicException)
        {
            // If decryption fails, the value might be stored as plaintext (for development)
            return cipherText;
        }
    }
}
