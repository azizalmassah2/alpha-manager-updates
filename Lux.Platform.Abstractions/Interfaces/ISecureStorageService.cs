using System.Threading.Tasks;

namespace Lux.Platform.Abstractions.Interfaces;

public interface ISecureStorageService
{
    string Encrypt(string plainText);
    string Decrypt(string encryptedText);
}
