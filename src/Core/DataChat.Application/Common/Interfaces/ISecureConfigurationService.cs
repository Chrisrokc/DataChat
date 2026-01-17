namespace DataChat.Application.Common.Interfaces;

public interface ISecureConfigurationService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
