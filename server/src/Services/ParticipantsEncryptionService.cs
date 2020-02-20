using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IsThisAMood.Services
{
    public interface IParticipantsEncryptionService
    {
        string Encrypt(string plainText, string key);
        string Decrypt(string cipherText, string key);
    }

    public class ParticipantsEncryptionService : IParticipantsEncryptionService
    {
        public string Encrypt(string plainText,string key)
        {
            byte[] encrypted;
            var iv = new byte[16];
            
            // Create an Aes object
            // with the specified key and IV.
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(key);
                aesAlg.IV = iv;

                // Create an encryptor to perform the stream transform.
                var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using var msEncrypt = new MemoryStream();
                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    //Write all data to the stream.
                    swEncrypt.Write(plainText);
                }
                encrypted = msEncrypt.ToArray();
            }

            // Return the encrypted bytes from the memory stream.
            return Encoding.UTF8.GetString(encrypted);
        }

        public string Decrypt(string cipherText, string key)
        {

            // Declare the string used to hold
            // the decrypted text.
            var cipherTextBytes = Encoding.UTF8.GetBytes(cipherText);
            string plaintext = null;
            byte[] iv = new byte[16];

            // Create an Aes object
            // with the specified key and IV.
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(key);
                aesAlg.IV = iv;

                // Create a decryptor to perform the stream transform.
                var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using var msDecrypt = new MemoryStream(cipherTextBytes);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                // Read the decrypted bytes from the decrypting stream
                // and place them in a string.
                plaintext = srDecrypt.ReadToEnd();
            }

            return plaintext;
        }
    }
}
